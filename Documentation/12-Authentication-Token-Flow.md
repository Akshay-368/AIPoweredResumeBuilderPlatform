## 12. Authentication & Token Flow

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant FE as Angular Frontend
    participant AuthAPI as ResumeAI.Auth.API
    participant AtsAPI as ResumeAI.ATSScore.API
    participant AuthDB as auth_db
    participant Google as Google OAuth

    Note over FE,AtsAPI: ─── Local Login Flow ───
    User->>FE: Enter credentials
    FE->>AuthAPI: POST /api/auth/login {email, password}
    AuthAPI->>AuthDB: SELECT user WHERE email = ?
    AuthDB-->>AuthAPI: User record
    AuthAPI->>AuthAPI: BCrypt.Verify(password, hash)
    AuthAPI->>AuthAPI: CreateToken(user) → JWT (15min)
    AuthAPI->>AuthAPI: GenerateRefreshToken() → 7-day token
    AuthAPI->>AuthDB: UPDATE user SET RefreshToken = ?
    AuthAPI-->>FE: {Token, RefreshToken}
    FE->>FE: Store tokens (memory / secure storage)

    Note over FE,AtsAPI: ─── Authenticated API Call ───
    FE->>AtsAPI: POST /api/ats/score<br/>Authorization: Bearer {JWT}
    AtsAPI->>AtsAPI: Validate JWT signature<br/>(shared secret/key)
    AtsAPI->>AtsAPI: Extract claims<br/>(UserId, Role)
    AtsAPI-->>FE: 200 OK response

    Note over FE,AtsAPI: ─── Token Refresh Flow ───
    FE->>AuthAPI: POST /api/auth/refresh<br/>{AccessToken, RefreshToken}
    AuthAPI->>AuthDB: Validate refresh token + expiry
    AuthDB-->>AuthAPI: Valid
    AuthAPI->>AuthAPI: Issue new JWT + new RefreshToken
    AuthAPI->>AuthDB: UPDATE RefreshToken
    AuthAPI-->>FE: {Token, RefreshToken}

    Note over FE,Google: ─── Google OAuth Flow ───
    FE->>AuthAPI: GET /api/auth/google/config
    AuthAPI-->>FE: {clientId}
    FE->>Google: Google Sign-In popup
    Google-->>FE: ID Token (JWT)
    FE->>AuthAPI: POST /api/auth/google {IdToken}
    AuthAPI->>Google: GoogleJsonWebSignature.ValidateAsync(idToken)
    Google-->>AuthAPI: Payload {email, name, emailVerified}
    AuthAPI->>AuthDB: UPSERT user (Provider='GOOGLE')
    AuthAPI-->>FE: {Token, RefreshToken}
```

---

## Summary — Technology Stack

| Layer | Technology |
|---|---|
| **Frontend** | Angular 17 (SSR via Angular Universal / Node.js) |
| **Auth API** | ASP.NET Core 8, Entity Framework Core, BCrypt.Net, Google.Apis.Auth |
| **Core API** | ASP.NET Core 8, Entity Framework Core, Npgsql |
| **AI Engine** | Google Gemini API (gemini-1.5-flash with pro fallback) |
| **Database** | PostgreSQL (two separate databases: auth_db, projects_db) |
| **PDF Generation** | Custom HTML/LaTeX template renderers (Deedy, Jake's, Hipster) |
| **Auth** | JWT Bearer tokens + Refresh Tokens + Google OAuth 2.0 + OTP (email) |
| **Containerization** | Docker (Dockerfile per service) |
| **Soft Deletes** | All major entities use `IsDeleted` flag pattern |
| **Data Safety** | `ProjectDatabaseSanitizer` (Win1252 encoding for PostgreSQL compatibility) |
