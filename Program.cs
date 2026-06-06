using HabitSystem.Features.Habits;
using HabitSystem.Features.CheckIns;
using HabitSystem.Features.Scores;
using HabitSystem.Features.Auth;
using HabitSystem.Features.Account;
using HabitSystem.Features.Diagnostics;
using HabitSystem.Infrastructure;
using HabitSystem.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Type", "X-Total-Count");
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Se estiver em produção, força caminho persistente usando HOME env var
if (builder.Environment.IsProduction())
{
    var homeDir = Environment.GetEnvironmentVariable("HOME") ?? "/home";
    var dbPath = Path.Combine(homeDir, "data", "app.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    connectionString = $"Data Source={dbPath}";
    Console.WriteLine($"[STARTUP] Database path: {dbPath}");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Add Authentication services
builder.Services.AddScoped<AuthService>();
builder.Services.AddHttpContextAccessor();

// Add Email service (pluggable: swap ConsoleEmailService for a real provider in production)
builder.Services.AddScoped<IEmailService, ConsoleEmailService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["Secret"];

// Log JWT configuration status
Console.WriteLine($"[STARTUP] Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"[STARTUP] JWT Secret configured: {!string.IsNullOrEmpty(secret)}");
Console.WriteLine($"[STARTUP] JWT Secret length: {secret?.Length ?? 0}");

if (string.IsNullOrEmpty(secret))
{
    // Use a default secret for startup, but auth will fail
    Console.WriteLine("[STARTUP] WARNING: JWT Secret not configured! Using fallback.");
    secret = "FallbackSecretKeyForStartupOnlyDoNotUseInProduction123456";
}

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

// Rate limiting — protects against brute force / abuse
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global: 120 requests per minute per IP (applies to all endpoints)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // Auth policy: stricter limit for login/register/forgot-password (10 req/min per IP)
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Logging.AddConsole();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        db.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed. App will start but may be unstable.");
        throw;
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Enable OpenAPI in production as well for Swagger UI
    app.MapOpenApi();
}

// Use CORS - DEVE estar ANTES de UseAuthorization
app.UseCors("AllowAll");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint for Azure monitoring
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health")
    .WithOpenApi()
    .AllowAnonymous();

// Map Diagnostics endpoints (REMOVER APÓS DEBUG)
app.MapDiagnosticsEndpoint();

// Map Auth endpoints
app.MapRegisterEndpoint();
app.MapLoginEndpoint();
app.MapRefreshTokenEndpoint();
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

// Map CheckIn endpoints
app.MapCreateCheckIn();
app.MapGetTodayCheckIns();
app.MapGetCheckInsByDateRange();
app.MapUpdateCheckIn();

// Map Score endpoints
app.MapGetDailyScore();
app.MapGetWeeklyScores();

app.Run();
