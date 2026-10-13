# Arco API (HabitSystem)

.NET 9 minimal API + EF Core (SQLite) for the Arco habit tracker. Vertical slices with MediatR in `Features/`.

## Run locally

```bash
dotnet restore
dotnet run            # http://localhost:5232 (Development)
```

Migrations are applied automatically on startup. If they fail, the app does not start.

## Configuration (environment variables)

ASP.NET Core maps `__` to `:` (for example `Email__ResendApiKey` maps to `Email:ResendApiKey`).

| Variable | Required | Description |
|---|---|---|
| `JwtSettings__Secret` | yes (outside Development) | HMAC key for JWTs, at least 32 chars. The app refuses to start without it. |
| `Cors__AllowedOrigins__0` (`__1`, ...) | yes | Allowed frontend origin(s), e.g. `https://arco.example.com`. |
| `AppBaseUrl` | yes for e-mails | Frontend base URL used in e-mail links (`/verify-email?token=`, `/reset-password?token=`). |
| `Email__ResendApiKey` | recommended | [Resend](https://resend.com) API key. If empty, e-mails are **not sent**: a console stub is used, and a startup warning is logged outside Development. |
| `Email__From` | with Resend | Sender, e.g. `Arco <no-reply@yourdomain.com>` (the domain must be verified in Resend). |
| `ConnectionStrings__DefaultConnection` | no | SQLite connection string (Development/other envs). In **Production** the DB is always `$HOME/data/app.db`. |

In Development the console e-mail stub logs verification/reset links so you can test without a provider. In other environments it never logs tokens or links.

## Endpoints

- `GET /health`: DB health check, returns `{ status, version }` (503 when unhealthy).
- `GET /api/version`: returns `{ version }`.
- Errors always use `{ "error": string, "code": string }`. See `Common/ErrorCodes.cs` for the codes and their HTTP status.

## Conventions

- `FrequencyDays`: `0=Sunday ... 6=Saturday` (JS `getDay()`). `7` is accepted as Sunday and stored as `0`.
- "Today" is resolved from `User.Timezone` (IANA id, default `America/Sao_Paulo`).
- Refresh tokens are per device (table `RefreshTokens`, SHA-256 hashed) and rotate on every `/api/auth/refresh`.
