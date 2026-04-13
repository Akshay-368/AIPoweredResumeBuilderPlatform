# ResumeAI.Auth.API

Authentication and identity service for the ResumeAI platform.

This service currently provides:

- User registration (USER and ADMIN roles)
- Login with JWT + refresh token issuance
- Refresh token rotation
- Logout (refresh token invalidation)
- PostgreSQL-backed user storage via EF Core

## 1. Current Architecture

### High-level

- Runtime: ASP.NET Core (`net10.0`)
- Data layer: Entity Framework Core + PostgreSQL (`Npgsql`)
- Security: BCrypt password hashing + JWT bearer token generation
- Token lifecycle: Access token + server-stored refresh token
- Configuration sources:
  - `appsettings.json`
  - environment variables loaded from `.env` using `DotNetEnv`

### Request Pipeline (current)

1. Application starts in `Program.cs`
2. `.env` is searched upward from runtime output folder and loaded if found
3. Services are registered:
   - MVC controllers
   - `AuthDbContext`
   - `ITokenService` -> `TokenService`
4. Middleware pipeline:
   - `UseTimeLogging()` custom middleware
   - `MapControllers()`
5. Endpoints:
   - `GET /` health check
   - `POST /api/auth/register`
   - `POST /api/auth/login`
   - `POST /api/auth/refresh`
   - `POST /api/auth/logout`

## 2. Codebase Structure

```text
ResumeAI.Auth.API/
  Program.cs
  appsettings.json
  Controllers/
    AuthController.cs
  Data/
    AuthDbContext.cs
  DTO/
    RegisterDTO.cs
    LoginDTO.cs
    RefreshDTO.cs
    LogoutDTO.cs
  Models/
    User.cs
  Services/
    ITokenService.cs
    TokenService.cs
  CustomMiddleware/
    TimeLogging.cs
  Migrations/
    ... EF Core migrations
  Properties/
    launchSettings.json
```

## 3. Important Files, Classes, and Functions

### `Program.cs`

Primary bootstrapper.

Important responsibilities:

- `LoadEnvironmentFile()`
  - Walks up directory parents (max 6 levels) to locate `.env`
  - Loads environment variables via `Env.Load(...)`
- Configures PostgreSQL DbContext using `ConnectionStrings:DefaultConnection`
- Registers token service (`Scoped`)
- Exposes root health endpoint (`GET /`)
- Wires custom request-time logging middleware

### `Controllers/AuthController.cs`

Core API controller for auth workflows.

Key actions:

- `Register(RegisterDto dto)`
  - Normalizes role (`USER`/`ADMIN`) and phone number (digits only)
  - Validates duplicate account by email or phone
  - For ADMIN registration, requires and validates admin secret key
  - Hashes password with BCrypt salt (work factor 12)
  - Creates user with defaults (`SubscriptionPlan = FREE`, `Provider = LOCAL`, `IsActive = true`)
  - Returns success message

- `Login(LoginDto dto)`
  - Authenticates by phone number (if present) or email
  - Verifies account is active and password hash matches
  - Issues JWT and refresh token
  - Stores refresh token + 7-day expiry in DB
  - Returns tokens

- `Refresh(RefreshDto dto)`
  - Validates request contains refresh token
  - Verifies refresh token exists and is not expired
  - Rotates both JWT and refresh token
  - Updates DB with new refresh token and expiry
  - Returns new tokens

- `Logout(LogoutDto dto)`
  - Finds user by email
  - Clears server-side refresh token
  - Returns confirmation message

Internal helpers:

- `NormalizeRole(string? role)`
- `NormalizePhoneNumber(string? phoneNumber)`
- `IsValidAdminKey(string providedKey)`

### `Services/ITokenService.cs` and `Services/TokenService.cs`

Token abstraction + implementation.

Main methods:

- `CreateToken(User user)`
  - Builds claims:
    - `email`
    - `nameid` (`UserId`)
    - `role`
  - Signs JWT using `Jwt:Key`
  - Sets issuer/audience/duration from configuration
- `GenerateRefreshToken()`
  - Uses cryptographic RNG for 64-byte random token
  - Returns Base64 token string

### `Data/AuthDbContext.cs`

EF Core context for auth data.

- `DbSet<User> Users`
- `OnModelCreating(...)`
  - unique index on `Email`
  - unique index on `PhoneNumber`

### `Models/User.cs`

Auth domain model representing a local account.

Important fields:

- Identity: `UserId`, `Email`, `PhoneNumber`
- Credentials: `PasswordHash`
- Authorization: `Role`
- Session security: `RefreshToken`, `RefreshTokenExpiryTime`
- State: `IsActive`, `Provider`, `SubscriptionPlan`, `CreatedAt`

### `CustomMiddleware/TimeLogging.cs`

Custom middleware that measures request duration using `Stopwatch` and logs:

`Request{path} took {ms} ms`

## 4. Request and Response Contracts (Current)

All endpoints are under base route: `/api/auth`

### Register

- Method/Path: `POST /api/auth/register`
- Body:

```json
{
  "fullName": "Akshay Kumar",
  "email": "akshay@example.com",
  "password": "Str0ng#Pass",
  "phoneNumber": "9876543210",
  "role": "user",
  "adminSecretKey": null
}
```

- Success (`200 OK`):

```json
{
  "message": "Registration successful. You can now sign in."
}
```

- Common failures:
  - `400 Bad Request` invalid role
  - `400 Bad Request` duplicate email/phone
  - `400 Bad Request` missing/invalid admin key for admin registration

### Login

- Method/Path: `POST /api/auth/login`
- Body (email login):

```json
{
  "email": "akshay@example.com",
  "password": "Str0ng#Pass"
}
```

- Body (phone login):

```json
{
  "phoneNumber": "9876543210",
  "password": "Str0ng#Pass"
}
```

- Success (`200 OK`):

```json
{
  "token": "<jwt>",
  "refreshToken": "<refresh-token>"
}
```

- Failure:
  - `401 Unauthorized` with message `Invalid Credentials`

### Refresh

- Method/Path: `POST /api/auth/refresh`
- Body:

```json
{
  "refreshToken": "<refresh-token>"
}
```

- Success (`200 OK`):

```json
{
  "token": "<new-jwt>",
  "refreshToken": "<new-refresh-token>"
}
```

- Common failures:
  - `400 Bad Request` missing token in request body
  - `401 Unauthorized` invalid or expired refresh token

### Logout

- Method/Path: `POST /api/auth/logout`
- Body:

```json
{
  "email": "akshay@example.com"
}
```

- Success (`200 OK`):

```json
{
  "message": "Logged out successfully!"
}
```

- Failure:
  - `400 Bad Request` user not found

## 5. End-to-End Flow (Auth Service)

### Registration Flow

1. Client sends registration payload.
2. Controller validates role/phone/admin key and uniqueness.
3. Password is BCrypt-hashed.
4. User entity saved to PostgreSQL.
5. API returns success message.

### Login Flow

1. Client sends email or phone + password.
2. API fetches user from DB and verifies hash.
3. JWT and refresh token are generated.
4. Refresh token + expiry stored in DB.
5. API returns both tokens.

### Token Refresh Flow

1. Client sends refresh token.
2. API validates token ownership + expiry.
3. API rotates tokens (new JWT + new refresh token).
4. DB updated with new refresh token.
5. API returns new pair.

### Logout Flow

1. Client sends email.
2. API finds user and clears refresh token.
3. API returns logout confirmation.

## 6. Configuration and Environment

### `appsettings.json`

Key sections:

- `ConnectionStrings:DefaultConnection`
- `Jwt:Key`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:DurationInMinutes`

### `.env` support

- The app attempts to load `.env` at startup.
- Useful for secrets in local development.

## 7. Database and Migrations

- Migrations are present in `Migrations/` and reflect iterative model changes.
- Context currently enforces uniqueness on email and phone.
- Typical commands:

```bash
dotnet ef migrations add <MigrationName> --project ResumeAI.Auth.API
dotnet ef database update --project ResumeAI.Auth.API
```

## 8. Running Locally

```bash
cd ResumeAI.Auth.API
dotnet restore
dotnet run
```

Current dev URL in launch profile: `http://localhost:5196`

## 9. Important Notes (Current State)

- Swagger/OpenAPI UI is not enabled in current `Program.cs`.
- Refresh token is invalidated on logout by clearing `RefreshToken`.
- Access token validation middleware is not yet used in this service for protected routes because current controller actions are auth bootstrap actions.
- A template `*.http` file still points to `/weatherforecast`, which is not an active route in this project.