using HabitSystem.Features.Habits;
using HabitSystem.Features.CheckIns;
using HabitSystem.Features.Scores;
using HabitSystem.Features.Auth;
using HabitSystem.Features.Diagnostics;
using HabitSystem.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

// Se estiver em produ��o, for�a caminho persistente
if (builder.Environment.IsProduction())
{
    connectionString = "Data Source=/home/data/app.db";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Add Authentication services
builder.Services.AddScoped<AuthService>();
builder.Services.AddHttpContextAccessor();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
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

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Logging.AddConsole();

var app = builder.Build();

// Initialize database - handle both fresh installs and existing databases
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Check if database exists
        if (db.Database.CanConnect())
        {
            logger.LogInformation("Database exists, checking for pending migrations...");
            
            // Check if migrations table exists (if not, db was created with EnsureCreated)
            var pendingMigrations = db.Database.GetPendingMigrations().ToList();
            var appliedMigrations = db.Database.GetAppliedMigrations().ToList();
            
            if (appliedMigrations.Count == 0 && pendingMigrations.Count > 0)
            {
                // Database was created with EnsureCreated, we need to mark migrations as applied
                logger.LogWarning("Database exists but has no migrations history. This may cause issues.");
                // Try to apply migrations anyway - this might fail if schema already exists
                try
                {
                    db.Database.Migrate();
                }
                catch (Exception migrationEx)
                {
                    logger.LogWarning(migrationEx, "Migration failed, database schema may already exist");
                }
            }
            else if (pendingMigrations.Count > 0)
            {
                logger.LogInformation($"Applying {pendingMigrations.Count} pending migrations...");
                db.Database.Migrate();
            }
            else
            {
                logger.LogInformation("Database is up to date");
            }
        }
        else
        {
            logger.LogInformation("Database does not exist, creating with migrations...");
            db.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during database initialization. Attempting EnsureCreated as fallback...");
        try
        {
            db.Database.EnsureCreated();
        }
        catch (Exception ensureEx)
        {
            logger.LogError(ensureEx, "EnsureCreated also failed");
        }
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
