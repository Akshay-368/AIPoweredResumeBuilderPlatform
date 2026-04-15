# ResumeAI.Gateway

Reverse proxy gateway for the ResumeAI services.

This gateway is the public entry point for the frontend and routes requests to Auth, Parser, ATS, Projects, Notifications, Feedback, and Admin endpoints.

## Purpose

- terminate browser traffic at a single `/api` entry point
- apply CORS for the frontend origin
- validate JWTs at the gateway layer
- enforce parser route authorization policy
- forward requests to the correct backend service with YARP

## Current Middleware Order

`Program.cs` uses:

1. `UseHttpsRedirection()`
2. `UseCors("FrontendCors")`
3. `UseRouting()`
4. `UseAuthentication()`
5. `UseAuthorization()`
6. `MapReverseProxy()`

## Route Map

Defined in `appsettings.json`.

Routes:

- `/api/auth/{**remainder}` -> Auth API
- `/api/users/{**remainder}` -> Auth API
- `/api/admin/{**remainder}` -> Auth API
- `/api/parser/{**remainder}` -> File Parser API
- `/api/ats/{**remainder}` -> ATS API
- `/api/projects/{**remainder}` -> ATS API
- `/api/notifications/{**remainder}` -> ATS API
- `/api/feedback/{**remainder}` -> ATS API
- `/api/admin/notifications/{**remainder}` -> ATS API

Clusters:

- `auth-cluster` -> `http://localhost:5196`
- `parser-cluster` -> `http://localhost:5111`
- `ats-cluster` -> `http://localhost:5050`

## Security Notes

- JWT bearer validation is configured in the gateway host.
- The parser route requires the `ParseResumePolicy` claim policy.
- CORS allows credentials for the frontend origin.
- Frontend traffic is expected from `http://localhost:4200` during development.

## Local Run

```powershell
cd ResumeAI.Gateway
dotnet restore
dotnet run
```

Default local URLs:

- `http://localhost:5290`
- `https://localhost:7197`

## Example Requests

```http
POST /api/auth/login
POST /api/auth/register
POST /api/parser/upload
POST /api/ats/score
POST /api/projects
```

## Troubleshooting

- `502 Bad Gateway`: backend service is not running or cluster address is wrong
- `404 Not Found`: route pattern does not match the request path
- `401 Unauthorized`: JWT is missing, expired, or invalid
- CORS errors: frontend origin is not allowed in the gateway policy
