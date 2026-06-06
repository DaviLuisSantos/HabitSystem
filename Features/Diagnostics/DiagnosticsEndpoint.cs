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

        // Endpoint para aplicar alterações de autenticação manualmente
        app.MapPost("/api/diagnostics/fix-auth-schema", async ([FromServices] HabitSystem.Infrastructure.AppDbContext db) =>
        {
            var results = new List<string>();
            
            try
            {
                // Verificar se os campos de autenticação existem na tabela Users
                var connection = db.Database.GetDbConnection();
                await connection.OpenAsync();
                
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info(Users)";
                
                var columns = new List<string>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        columns.Add(reader.GetString(1)); // Column name is at index 1
                    }
                }
                
                results.Add($"Existing columns: {string.Join(", ", columns)}");
                
                // Adicionar colunas se não existirem
                if (!columns.Contains("PasswordHash"))
                {
                    using var addCmd = connection.CreateCommand();
                    addCmd.CommandText = "ALTER TABLE Users ADD COLUMN PasswordHash TEXT NOT NULL DEFAULT ''";
                    await addCmd.ExecuteNonQueryAsync();
                    results.Add("Added PasswordHash column");
                }
                
                if (!columns.Contains("RefreshToken"))
                {
                    using var addCmd = connection.CreateCommand();
                    addCmd.CommandText = "ALTER TABLE Users ADD COLUMN RefreshToken TEXT NULL";
                    await addCmd.ExecuteNonQueryAsync();
                    results.Add("Added RefreshToken column");
                }
                
                if (!columns.Contains("RefreshTokenExpiryTime"))
                {
                    using var addCmd = connection.CreateCommand();
                    addCmd.CommandText = "ALTER TABLE Users ADD COLUMN RefreshTokenExpiryTime TEXT NULL";
                    await addCmd.ExecuteNonQueryAsync();
                    results.Add("Added RefreshTokenExpiryTime column");
                }
                
                return Results.Ok(new
                {
                    success = true,
                    results,
                    message = "Schema updated successfully"
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new
                {
                    success = false,
                    results,
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }
        })
        .WithName("DiagnosticsFixAuthSchema")
        .WithOpenApi()
        .AllowAnonymous();

        app.MapPost("/api/diagnostics/run-migrations", async ([FromServices] HabitSystem.Infrastructure.AppDbContext db) =>
        {
            try
            {
                var pending = db.Database.GetPendingMigrations().ToList();
                db.Database.Migrate();
                var applied = db.Database.GetAppliedMigrations().ToList();
                return Results.Ok(new { success = true, migrationsApplied = pending, totalApplied = applied.Count });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { success = false, error = ex.Message, inner = ex.InnerException?.Message });
            }
        })
        .WithName("DiagnosticsRunMigrations")
        .WithOpenApi()
        .AllowAnonymous();

        app.MapPost("/api/diagnostics/fix-plan-schema", async ([FromServices] HabitSystem.Infrastructure.AppDbContext db) =>
        {
            var results = new List<string>();
            try
            {
                var connection = db.Database.GetDbConnection();
                await connection.OpenAsync();

                using var pragmaCmd = connection.CreateCommand();
                pragmaCmd.CommandText = "PRAGMA table_info(Users)";
                var columns = new List<string>();
                using (var reader = await pragmaCmd.ExecuteReaderAsync())
                    while (await reader.ReadAsync())
                        columns.Add(reader.GetString(1));

                results.Add($"Columns found: {string.Join(", ", columns)}");

                var toAdd = new Dictionary<string, string>
                {
                    ["Plan"] = "ALTER TABLE Users ADD COLUMN Plan TEXT NOT NULL DEFAULT 'Free'",
                    ["IsEmailVerified"] = "ALTER TABLE Users ADD COLUMN IsEmailVerified INTEGER NOT NULL DEFAULT 0",
                    ["EmailVerificationToken"] = "ALTER TABLE Users ADD COLUMN EmailVerificationToken TEXT",
                    ["EmailVerificationTokenExpiry"] = "ALTER TABLE Users ADD COLUMN EmailVerificationTokenExpiry TEXT",
                    ["PasswordResetToken"] = "ALTER TABLE Users ADD COLUMN PasswordResetToken TEXT",
                    ["PasswordResetTokenExpiry"] = "ALTER TABLE Users ADD COLUMN PasswordResetTokenExpiry TEXT",
                };

                foreach (var (col, sql) in toAdd)
                {
                    if (!columns.Contains(col))
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = sql;
                        await cmd.ExecuteNonQueryAsync();
                        results.Add($"Added column: {col}");
                    }
                    else
                    {
                        results.Add($"Column already exists: {col}");
                    }
                }

                // Mark everyone as Pro/verified
                using var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = "UPDATE Users SET Plan = 'Pro', IsEmailVerified = 1";
                var rows = await updateCmd.ExecuteNonQueryAsync();
                results.Add($"Updated {rows} user(s) to Pro/verified");

                // Create migration history if missing and record all migrations
                using var createHistCmd = connection.CreateCommand();
                createHistCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL
                    )
                    """;
                await createHistCmd.ExecuteNonQueryAsync();

                var migrations = new[]
                {
                    "20260406163922_InitialCreate",
                    "20260407000703_AddAuthenticationFields",
                    "20260605140750_AddPlanAndEmailVerification",
                    "20260605144514_SeedOwnerAsPro",
                };
                foreach (var migId in migrations)
                {
                    using var insCmd = connection.CreateCommand();
                    insCmd.CommandText = $"INSERT OR IGNORE INTO \"__EFMigrationsHistory\" VALUES ('{migId}', '9.0.9')";
                    await insCmd.ExecuteNonQueryAsync();
                    results.Add($"Migration recorded: {migId}");
                }

                return Results.Ok(new { success = true, results });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { success = false, results, error = ex.Message });
            }
        })
        .WithName("DiagnosticsFixPlanSchema")
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
