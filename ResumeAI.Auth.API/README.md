# ResumeAI.Auth.API

Authentication and identity service for the ResumeAI platform.

This service currently provides:

- registration for USER and ADMIN roles
- email or phone login
- JWT access token issuance
- refresh token rotation
- logout and refresh-token invalidation
- forgot-password OTP flow
- delete-account OTP flow
- admin user views and account deletion orchestration

## Current Architecture

- Runtime: ASP.NET Core `net10.0`
- Data: Entity Framework Core + PostgreSQL
- Security: BCrypt password hashing + JWT bearer tokens
- OTP: hashed OTP challenge rows with expiry, attempt count, and consume-on-success
- Configuration: `appsettings.json`, environment variables, and `.env` loading

## Request Pipeline

1. `Program.cs` loads environment variables from a discovered `.env` file.
2. Controllers are registered.
3. PostgreSQL DbContext is configured.
4. JWT bearer auth and authorization are configured.
5. Rate limiter policies are registered for register and OTP routes.
6. Request timing middleware runs on each request.
7. Controllers are mapped.

## Key Controllers

### `Controllers/AuthController.cs`

Base route: `/api/auth`

Implemented actions:

- `POST /register`
- `POST /login`
- `POST /refresh`
- `POST /logout`
- `POST /forgot-password/request-otp`
- `POST /forgot-password/verify-otp`
- `POST /forgot-password/reset`
- `POST /delete-account/request-otp`
- `POST /delete-account/confirm`

Behavior:

- roles are normalized to USER or ADMIN
- phone numbers are normalized to digits only
- passwords must satisfy complexity requirements
- admin registrations require a valid admin secret key
- login supports either email or phone
- refresh tokens are rotated on successful refresh
- logout clears the stored refresh token
- delete-account confirmation first purges ATS project data through the ATS API before removing the user

### `Controllers/UsersController.cs`

Base route: `/api/users`

- `GET /directory`
- returns active users and roles for notification recipient selection

### `Controllers/AdminController.cs`

Base route: `/api/admin`

Restricted to `ADMIN`

- `GET /overview`
- `GET /users`
- `GET /users/{id}/activity`
- `DELETE /users/{id}`

This controller also calls ATS admin endpoints to purge project data before deleting a user.

## Services

### `TokenService`

- creates JWTs with claims for email, user id, role, and `permission=parse:resume`
- generates cryptographically secure refresh tokens

### `OtpService`

- creates OTP challenges
- stores OTP hashes instead of raw OTPs
- tracks attempts and expiry
- consumes OTPs on success

### `MailKitEmailService`

- sends OTP emails through SMTP
- reads host, port, username, password, from-address, and socket mode from config or environment

## Database Model

Auth data is stored in the Auth database context.

Main entities:

- users
- user OTP challenges

Important user fields:

- `UserId`
- `Email`
- `PhoneNumber`
- `PasswordHash`
- `Role`
- `RefreshToken`
- `RefreshTokenExpiryTime`
- `IsActive`
- `Provider`
- `SubscriptionPlan`

## API Contracts

### Register

`POST /api/auth/register`

### Login

`POST /api/auth/login`

### Refresh

`POST /api/auth/refresh`

### Logout

`POST /api/auth/logout`

### OTP flows

`POST /api/auth/forgot-password/request-otp`

`POST /api/auth/forgot-password/verify-otp`

`POST /api/auth/forgot-password/reset`

`POST /api/auth/delete-account/request-otp`

`POST /api/auth/delete-account/confirm`

## Configuration

Important settings:

- `ConnectionStrings:DefaultConnection`
- `Jwt:Key`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:DurationInMinutes`
- `Admin_Key`
- `Smtp:*`
- `Otp:Secret`
- `AtsProjectsApi:BaseUrl`

## Running Locally

```powershell
cd ResumeAI.Auth.API
dotnet restore
dotnet run
```

Default local URL:

- `http://localhost:5196`

## Notes

- Swagger is enabled in development.
- Request timing is logged by the custom middleware.
- The service automatically migrates the auth database on startup when the connection string is available.