using HabitSystem.Common;
using HabitSystem.Features.Habits;
using HabitSystem.Features.CheckIns;
using HabitSystem.Features.Scores;
using HabitSystem.Features.Auth;
using HabitSystem.Features.Account;
using HabitSystem.Infrastructure;
using HabitSystem.Services;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Startup messages are logged once the app (and its logger) is built
var startupWarnings = new List<string>();

var appVersion = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    .Split('+')[0] ?? "1.0.0";

// Add services to the container.

// Behind Azure App Service (reverse proxy): trust X-Forwarded-For / X-Forwarded-Proto so that
// RemoteIpAddress (used by the rate limiter) is the real client IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add CORS — only origins listed in Cors:AllowedOrigins (env: Cors__AllowedOrigins__0, __1, ...)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Length == 0)
    startupWarnings.Add("Cors:AllowedOrigins is empty — browsers will be blocked by CORS.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Type", "X-Total-Count");
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Em produção, força caminho persistente usando HOME env var ($HOME/data/app.db).
// appsettings.Production.json intentionally has no ConnectionStrings entry.
string? productionDbPath = null;
if (builder.Environment.IsProduction())
{
    var homeDir = Environment.GetEnvironmentVariable("HOME") ?? "/home";
    productionDbPath = Path.Combine(homeDir, "data", "app.db");
    Directory.CreateDirectory(Path.GetDirectoryName(productionDbPath)!);
    connectionString = $"Data Source={productionDbPath}";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// Add Authentication services
builder.Services.AddScoped<AuthService>();
builder.Services.AddHttpContextAccessor();

// Email: Resend when Email:ResendApiKey is set, otherwise console fallback
var resendApiKey = builder.Configuration["Email:ResendApiKey"];
var useResend = !string.IsNullOrWhiteSpace(resendApiKey);
if (useResend)
{
    builder.Services.AddHttpClient<IEmailService, ResendEmailService>(client =>
    {
        client.BaseAddress = new Uri(ResendEmailService.ApiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    });
}
else
{
    builder.Services.AddScoped<IEmailService, ConsoleEmailService>();
    if (!builder.Environment.IsDevelopment())
        startupWarnings.Add("Email:ResendApiKey is not set — verification and password-reset e-mails will NOT be sent (set Email__ResendApiKey).");
}

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["Secret"];

// The secret is only committed for Development (appsettings.Development.json).
// Anywhere else it must come from the environment (JwtSettings__Secret).
if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
    throw new InvalidOperationException(
        "JwtSettings:Secret is missing or shorter than 32 characters. " +
        "Set the JwtSettings__Secret environment variable.");

var issuer = jwtSettings["Issuer"] ?? "HabitSystem";
var audience = jwtSettings["Audience"] ?? "HabitSystemUsers";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Add MediatR for vertical slices
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Configure JSON serialization for enums
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Malformed bodies / bad parameters are thrown to the global exception handler,
// which answers 400 { error, code: "validation" } in every environment
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

// Rate limiting — protects against brute force / abuse (partitioned by client IP)
static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Muitas requisições. Tente novamente em instantes.", code = ErrorCodes.RateLimited }, ct);
    };

    // Global: 120 requests per minute per IP (applies to all endpoints)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIp(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // Auth policy: stricter limit for auth endpoints (10 req/min per IP)
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIp(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Logging.AddConsole();

var app = builder.Build();

app.Logger.LogInformation("Arco API v{Version} starting. Environment: {Environment}", appVersion, app.Environment.EnvironmentName);
if (productionDbPath != null)
    app.Logger.LogInformation("Database path: {DbPath}", productionDbPath);
app.Logger.LogInformation("E-mail provider: {Provider}", useResend ? "Resend" : "Console (no e-mails sent)");
foreach (var warning in startupWarnings)
    app.Logger.LogWarning("{Warning}", warning);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var connection = db.Database.GetDbConnection();
        connection.Open();

        // Check if migration history table exists
        using var checkHistCmd = connection.CreateCommand();
        checkHistCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
        var historyExists = (long)checkHistCmd.ExecuteScalar()! > 0;

        if (!historyExists)
        {
            // Check if Users table already exists (DB created before migrations were tracked)
            using var checkUsersCmd = connection.CreateCommand();
            checkUsersCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'";
            var usersExists = (long)checkUsersCmd.ExecuteScalar()! > 0;

            if (usersExists)
            {
                logger.LogWarning("Database exists without migration history. Bootstrapping history table.");

                // Get existing columns to know which migrations are already applied
                using var pragmaCmd = connection.CreateCommand();
                pragmaCmd.CommandText = "PRAGMA table_info(Users)";
                var columns = new List<string>();
                using (var reader = pragmaCmd.ExecuteReader())
                    while (reader.Read())
                        columns.Add(reader.GetString(1));

                logger.LogInformation("Existing columns: {Columns}", string.Join(", ", columns));

                using var createHistCmd = connection.CreateCommand();
                createHistCmd.CommandText = """
                    CREATE TABLE "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL
                    )
                    """;
                createHistCmd.ExecuteNonQuery();

                // InitialCreate — Users table exists
                using var ins1 = connection.CreateCommand();
                ins1.CommandText = "INSERT INTO \"__EFMigrationsHistory\" VALUES ('20260406163922_InitialCreate', '9.0.9')";
                ins1.ExecuteNonQuery();

                // AddAuthenticationFields — check for PasswordHash column
                if (columns.Contains("PasswordHash"))
                {
                    using var ins2 = connection.CreateCommand();
                    ins2.CommandText = "INSERT INTO \"__EFMigrationsHistory\" VALUES ('20260407000703_AddAuthenticationFields', '9.0.9')";
                    ins2.ExecuteNonQuery();
                }

                logger.LogInformation("Migration history bootstrapped.");
            }
        }

        connection.Close();

        // Migrate() will now only apply truly pending migrations
        db.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        // Never run against a half-migrated schema: fail startup
        logger.LogCritical(ex, "Database migration failed. Aborting startup.");
        throw;
    }
}

// Configure the HTTP request pipeline.

// Must run first so RemoteIpAddress/Scheme reflect the real client
app.UseForwardedHeaders();

// Global exception handler: never leak stack traces, consistent error body
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    if (exception is BadHttpRequestException badRequest)
    {
        context.Response.StatusCode = badRequest.StatusCode;
        await context.Response.WriteAsJsonAsync(new { error = "Requisição inválida", code = ErrorCodes.Validation });
        return;
    }

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "Erro interno", code = ErrorCodes.InternalError });
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Use CORS - DEVE estar ANTES de UseAuthorization
app.UseCors("AllowFrontend");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint for Azure monitoring (includes a DB connectivity check)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status == HealthStatus.Healthy ? "healthy" : "unhealthy",
            version = appVersion
        });
    }
})
.AllowAnonymous();

app.MapGet("/api/version", () => Results.Ok(new { version = appVersion }))
    .WithName("GetVersion")
    .WithOpenApi()
    .AllowAnonymous();

// Map Auth endpoints
app.MapRegisterEndpoint();
app.MapLoginEndpoint();
app.MapRefreshTokenEndpoint();
app.MapLogoutEndpoint();
app.MapGetCurrentUserEndpoint();
app.MapForgotPasswordEndpoint();
app.MapResetPasswordEndpoint();
app.MapVerifyEmailEndpoints();

// Map Account endpoints
app.MapChangePasswordEndpoint();
app.MapDeleteAccountEndpoint();
app.MapGetPlanEndpoint();

// Map Habit endpoints
app.MapCreateHabit();
app.MapGetHabits();
app.MapGetHabitById();
app.MapUpdateHabit();
app.MapArchiveHabit();
app.MapRestoreHabit();

// Map CheckIn endpoints
app.MapCreateCheckIn();
app.MapGetTodayCheckIns();
app.MapGetCheckInsByDateRange();
app.MapUpdateCheckIn();

// Map Score endpoints
app.MapGetDailyScore();
app.MapGetWeeklyScores();

app.Run();
