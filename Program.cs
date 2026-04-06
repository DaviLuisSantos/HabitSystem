using HabitSystem.Features.Habits;
using HabitSystem.Features.CheckIns;
using HabitSystem.Features.Scores;
using HabitSystem.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

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

// Se estiver em produção, força caminho persistente
if (builder.Environment.IsProduction())
{
    connectionString = "Data Source=/home/data/app.db";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

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

// Create database if it doesn't exist
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
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

app.UseAuthorization();

app.MapControllers();

// Health check endpoint for Azure monitoring
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health")
    .WithOpenApi()
    .AllowAnonymous();

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
