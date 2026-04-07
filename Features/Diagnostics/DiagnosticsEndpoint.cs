using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HabitSystem.Features.Diagnostics;

/// <summary>
/// Endpoint de diagnóstico temporário para verificar configurações
/// REMOVER ESTE ARQUIVO APÓS RESOLVER O PROBLEMA!
/// </summary>
public static class DiagnosticsEndpoint
{
    public static void MapDiagnosticsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/diagnostics/config", ([FromServices] IConfiguration config) =>
        {
            var jwtSettings = config.GetSection("JwtSettings");
            var secret = jwtSettings["Secret"];
            
            return Results.Ok(new
            {
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                hasJwtSection = config.GetSection("JwtSettings").Exists(),
                hasSecret = !string.IsNullOrEmpty(secret),
                secretLength = secret?.Length ?? 0,
                secretFirst10 = secret?.Length > 10 ? secret.Substring(0, 10) + "..." : "N/A",
                issuer = jwtSettings["Issuer"],
                audience = jwtSettings["Audience"],
                accessTokenExpiration = jwtSettings["AccessTokenExpirationMinutes"],
                refreshTokenExpiration = jwtSettings["RefreshTokenExpirationDays"],
                timestamp = DateTime.UtcNow
            });
        })
        .WithName("DiagnosticsConfig")
        .WithOpenApi()
        .AllowAnonymous();

        app.MapGet("/api/diagnostics/database", ([FromServices] HabitSystem.Infrastructure.AppDbContext db) =>
        {
            try
            {
                var canConnect = db.Database.CanConnect();
                var appliedMigrations = db.Database.GetAppliedMigrations().ToList();
                var pendingMigrations = db.Database.GetPendingMigrations().ToList();
                
                return Results.Ok(new
                {
                    canConnect,
                    appliedMigrationsCount = appliedMigrations.Count,
                    appliedMigrations,
                    pendingMigrationsCount = pendingMigrations.Count,
                    pendingMigrations,
                    databaseProvider = db.Database.ProviderName
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new
                {
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        })
        .WithName("DiagnosticsDatabase")
        .WithOpenApi()
        .AllowAnonymous();

        app.MapPost("/api/diagnostics/test-auth", async ([FromServices] HabitSystem.Features.Auth.AuthService authService) =>
        {
            try
            {
                // Testar hash de senha
                var passwordHash = authService.HashPassword("teste123");
                
                // Testar geração de refresh token
                var refreshToken = authService.GenerateRefreshToken();
                
                // Testar geração de token JWT (vai falhar se secret não estiver configurado)
                var testUser = new HabitSystem.Domain.User
                {
                    Id = Guid.NewGuid(),
                    Email = "test@test.com",
                    Name = "Test User",
                    CreatedAt = DateTime.UtcNow
                };
                
                var accessToken = authService.GenerateAccessToken(testUser);
                
                return Results.Ok(new
                {
                    success = true,
                    passwordHashLength = passwordHash.Length,
                    refreshTokenLength = refreshToken.Length,
                    accessTokenLength = accessToken.Length,
                    accessTokenPreview = accessToken.Length > 50 ? accessToken.Substring(0, 50) + "..." : accessToken
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new
                {
                    success = false,
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        })
        .WithName("DiagnosticsTestAuth")
        .WithOpenApi()
        .AllowAnonymous();
    }
}
