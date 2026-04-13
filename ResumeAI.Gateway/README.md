# ResumeAI.Gateway

Reverse proxy gateway for ResumeAI services, currently focused on routing authentication traffic to `ResumeAI.Auth.API`.

## 1. Current Purpose and Scope

The gateway currently acts as a single-entry HTTP edge for auth endpoints.

What it does right now:

- Terminates incoming client traffic at gateway URLs
- Applies CORS policy for configured frontend origins
- Configures JWT authentication/authorization middleware (infrastructure-level)
- Routes matching requests using YARP (`MapReverseProxy`)
- Forwards `/api/auth/{**remainder}` traffic to Auth API (`http://localhost:5196`)

## 2. Architecture Overview

### High-level

- Runtime: ASP.NET Core (`net10.0`)
- Gateway engine: `Yarp.ReverseProxy`
- Auth stack available in gateway host:
	- `Microsoft.AspNetCore.Authentication.JwtBearer`
	- `Microsoft.IdentityModel.Tokens`
- Config sources:
	- `appsettings.json` (YARP config + standard ASP.NET settings)
	- environment variables from `.env` via `DotNetEnv`

### Current Middleware and Routing Order

`Program.cs` defines this request pipeline:

1. `UseHttpsRedirection()`
2. `UseRouting()`
3. `UseCors("FrontendCors")`
4. `UseAuthentication()`
5. `UseAuthorization()`
6. `MapReverseProxy()`

This order means CORS headers are handled before auth checks and before route forwarding.

## 3. Codebase Structure

```text
ResumeAI.Gateway/
	Program.cs
	appsettings.json
	README.md
	ResumeAI.Gateway.csproj
	ResumeAI.Gateway.http
	Properties/
		launchSettings.json
```

## 4. Important Files, Classes, and Functions

### `Program.cs`

Main startup composition.

Important responsibilities:

- `DotNetEnv.Env.Load()`
	- Loads `.env` variables for local environment-based settings
- `AddAuthentication().AddJwtBearer(...)`
	- Configures token validation (issuer, audience, lifetime, signing key)
	- Pulls values from env vars:
		- `Jwt__Issuer`
		- `Jwt__Audience`
		- `Jwt__Key`
- `AddAuthorization()`
	- Enables policy-based authorization infrastructure
- `AddCors(...)`
	- Registers `FrontendCors` policy for currently allowed origins
	- Allows headers, methods, and credentials
- `AddReverseProxy().LoadFromConfig(...)`
	- Loads YARP routes/clusters from `ReverseProxy` section in `appsettings.json`
- `MapReverseProxy()`
	- Activates YARP endpoint routing/forwarding

### `appsettings.json`

Defines proxy topology.

Current route and cluster:

- Route: `auth-route`
	- Match path: `/api/auth/{**remainder}`
	- Uses cluster: `auth-cluster`
- Cluster: `auth-cluster`
	- Destination: `destination1`
	- Address: `http://localhost:5196`

### `Properties/launchSettings.json`

Current local URLs:

- `http://localhost:5290`
- `https://localhost:7197`

### `ResumeAI.Gateway.csproj`

Current key packages:

- `Yarp.ReverseProxy`
- `DotNetEnv`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Microsoft.AspNetCore.OpenApi`

## 5. Request Flow and Proxy Behavior

### Incoming Auth Request Flow

Example request:

`POST http://localhost:5290/api/auth/login`

Flow:

1. Request arrives at gateway host.
2. HTTPS redirection/routing middleware executes.
3. CORS policy checks origin and appends CORS headers if applicable.
4. Authentication/authorization middleware runs (infrastructure enabled).
5. YARP route matching checks path.
6. Path `/api/auth/login` matches `auth-route`.
7. Request is forwarded to destination:
	 - `http://localhost:5196/api/auth/login`
8. Upstream response from Auth API is relayed back to client.

### Response Handling

- Gateway does not currently transform payloads.
- HTTP status and body from Auth API are passed through as-is in normal cases.
- Typical auth responses relayed through gateway:
	- `200` with token payload (login/refresh)
	- `400` for validation/business errors
	- `401` for invalid credentials or expired refresh token

## 6. Route and Endpoint Matrix (Current)

### Forwarded paths

- `/api/auth/register`
- `/api/auth/login`
- `/api/auth/refresh`
- `/api/auth/logout`

All forwarded to `http://localhost:5196` target service.

## 7. Security and CORS Notes (Current State)

- JWT bearer validation is configured in gateway host.
- Reverse proxy route currently forwards auth bootstrap endpoints.
- `AllowCredentials()` is enabled in CORS policy.
- Allowed origins are explicitly listed:
	- `http://localhost:4200`
	- `https://my-future-frontend-domain.com`

## 8. Local Development

### Prerequisites

- .NET SDK 10.x
- Auth API must be running at configured destination URL

### Run

```bash
cd ResumeAI.Gateway
dotnet restore
dotnet run
```

Default dev endpoints:

- `http://localhost:5290`
- `https://localhost:7197`

### Quick test

```http
POST http://localhost:5290/api/auth/register
POST http://localhost:5290/api/auth/login
POST http://localhost:5290/api/auth/refresh
POST http://localhost:5290/api/auth/logout
```

## 9. Troubleshooting

- `502 Bad Gateway`
	- Auth API is not running, wrong port, or destination URL mismatch in cluster settings
- `404 Not Found`
	- Request path does not match configured route pattern
- CORS browser errors
	- Origin missing from `FrontendCors` allow-list
	- Credentials requested but origin not explicitly allowed
- `401 Unauthorized`
	- Invalid/expired JWT or mismatched issuer/audience/signing key values

## 10. Important Current Limitations

- Gateway currently defines a single business route (`/api/auth/**`).
- No response/request transforms are currently configured in YARP.
- Template `.http` file still references `/weatherforecast`, which is not exposed by this gateway.
