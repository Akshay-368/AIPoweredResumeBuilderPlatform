# AI Resume Builder — Architecture & Design Diagrams

> **Render tip:** Open this file in VS Code and install the **"Markdown Preview Mermaid Support"** extension (by Matt Bierner) to see all diagrams rendered inline. Alternatively use [mermaid.live](https://mermaid.live) to paste individual blocks.

---

## Table of Contents

1. [System Architecture Diagram](#1-system-architecture-diagram)
2. [Services / Microservices Diagram](#2-services--microservices-diagram)
3. [Entity-Relationship Diagram (ERD)](#3-entity-relationship-diagram-erd)
4. [UML Class Diagram](#4-uml-class-diagram)
5. [Workflow / Activity Diagram](#5-workflow--activity-diagram)
6. [UML Sequence Diagram — ATS Scoring Flow](#6-uml-sequence-diagram--ats-scoring-flow)
7. [UML Sequence Diagram — Resume Builder Flow](#7-uml-sequence-diagram--resume-builder-flow)
8. [API / Endpoints Map — Auth API](#8-api--endpoints-map--auth-api)
9. [API / Endpoints Map — ATSScore API](#9-api--endpoints-map--atsscore-api)
10. [Frontend Component Tree](#10-frontend-component-tree)
11. [Database Schema Overview](#11-database-schema-overview)
12. [Authentication & Token Flow](#12-authentication--token-flow)

---

## 1. System Architecture Diagram

```mermaid
graph TB
    subgraph Client["🖥️ Client Layer"]
        FE["Angular 17 SSR Frontend<br/>ResumeBuilderFrontend<br/>(Angular Universal / Node.js)"]
    end

    subgraph Gateway["🔀 API Gateway / Reverse Proxy"]
        NG["Nginx / Load Balancer<br/>(Dockerized)"]
    end

    subgraph AuthService["🔐 Auth Microservice<br/>ResumeAI.Auth.API (.NET 8)"]
        AC["AuthController"]
        UC["UsersController"]
        AuthAdminC["AdminController"]
        TS["TokenService (JWT)"]
        ES["EmailService (OTP)"]
        OS["OtpService"]
        AuthDB[("PostgreSQL<br/>auth_db<br/>Users + OTP Challenges")]
    end

    subgraph ATSService["🤖 Core AI Microservice<br/>ResumeAI.ATSScore.API (.NET 8)"]
        AtsC["AtsController"]
        ProjC["ProjectsController"]
        RBC["ResumeBuilderController"]
        NotifC["NotificationsController"]
        FeedC["FeedbackController"]
        AtsAdminC["AdminController"]
        
        AtsScoreSvc["AtsScoringService"]
        GeminiAtsSvc["GeminiAtsService"]
        GeminiFallback["GeminiModelFallbackExecutor"]
        RBGeminiSvc["ResumeBuilderGeminiService"]
        RBPdfSvc["ResumeBuilderPdfService"]
        
        subgraph TemplateRenderers["Template Rendering Engine"]
            TRR["ResumeTemplateRendererRegistry"]
            DeedyR["DeedyResumeRenderer"]
            JakesR["JakesResumeRenderer"]
            HipsterR["SimpleHipsterResumeRenderer"]
        end
        
        AtsDB[("PostgreSQL<br/>projects_db<br/>All project data")]
    end

    subgraph External["☁️ External Services"]
        GeminiAPI["Google Gemini AI API<br/>(gemini-1.5-flash / pro)"]
        GoogleOAuth["Google OAuth 2.0<br/>(ID Token Validation)"]
        EmailProvider["Email Provider<br/>(SMTP / SendGrid)"]
    end

    FE -->|"HTTPS requests"| NG
    NG -->|"/api/auth/*"| AuthService
    NG -->|"/api/projects/* /api/ats/* /api/notifications/* /api/feedback/*"| ATSService
    AuthAdminC -->|"Internal HTTP call<br/>/api/admin/overview-context"| AtsAdminC

    AC --> TS
    AC --> ES
    AC --> OS
    AC --> GoogleOAuth
    AuthService --> AuthDB

    AtsC --> AtsScoreSvc
    AtsScoreSvc --> GeminiAtsSvc
    GeminiAtsSvc --> GeminiFallback
    RBC --> RBGeminiSvc
    RBC --> RBPdfSvc
    RBPdfSvc --> TRR
    TRR --> DeedyR
    TRR --> JakesR
    TRR --> HipsterR
    GeminiFallback --> GeminiAPI
    RBGeminiSvc --> GeminiAPI
    ES --> EmailProvider

    ATSService --> AtsDB

    style Client fill:#dbeafe,stroke:#3b82f6
    style Gateway fill:#fef3c7,stroke:#f59e0b
    style AuthService fill:#d1fae5,stroke:#10b981
    style ATSService fill:#ede9fe,stroke:#8b5cf6
    style External fill:#fee2e2,stroke:#ef4444
    style TemplateRenderers fill:#f3e8ff,stroke:#a855f7
```

---

## 2. Services / Microservices Diagram

```mermaid
graph LR
    subgraph "ResumeAI.Auth.API"
        direction TB
        AuthCtrl["AuthController<br/>── register<br/>── login<br/>── google OAuth<br/>── refresh token<br/>── logout<br/>── forgot-password (OTP)<br/>── delete-account (OTP)"]
        UsersCtrl["UsersController<br/>── directory<br/>── profile<br/>── update phone"]
        AuthAdminCtrl["AdminController (ADMIN)<br/>── overview<br/>── list users<br/>── user activity"]
        
        ITokenSvc(["«interface»<br/>ITokenService"])
        IEmailSvc(["«interface»<br/>IEmailService"])
        IOtpSvc(["«interface»<br/>IOtpService"])
        
        AuthCtrl --> ITokenSvc
        AuthCtrl --> IEmailSvc
        AuthCtrl --> IOtpSvc
    end

    subgraph "ResumeAI.ATSScore.API"
        direction TB
        AtsCtrl["AtsController<br/>── POST /score<br/>── GET /health"]
        ProjCtrl["ProjectsController<br/>── CRUD projects<br/>── resume-library<br/>── resume/jd artifacts<br/>── wizard-state<br/>── ats-results"]
        RBCtrl["ResumeBuilderController<br/>── templates<br/>── generate / revise<br/>── export-pdf<br/>── preview-pdf<br/>── pdf metadata"]
        NotifCtrl["NotificationsController<br/>── create<br/>── inbox / sent<br/>── mark read<br/>── delete"]
        FeedCtrl["FeedbackController<br/>── submit<br/>── list"]
        AtsAdminCtrl["AdminController (ADMIN)<br/>── overview-context<br/>── user activity-context"]

        AtsSvc["AtsScoringService"]
        GeminiAtsSvc2["GeminiAtsService"]
        FallbackExec["GeminiModelFallbackExecutor<br/>(gemini-1.5-flash → pro fallback)"]
        RBGemini["ResumeBuilderGeminiService"]
        RBPdf["ResumeBuilderPdfService"]
        
        TRReg["ResumeTemplateRendererRegistry"]
        DeedyRend["DeedyResumeRenderer<br/>(Deedy LaTeX-style)"]
        JakesRend["JakesResumeRenderer<br/>(Jake's Resume)"]
        HipRend["SimpleHipsterResumeRenderer<br/>(Modern Hipster)"]

        AtsCtrl --> AtsSvc
        AtsSvc --> GeminiAtsSvc2
        GeminiAtsSvc2 --> FallbackExec
        RBCtrl --> RBGemini
        RBCtrl --> RBPdf
        RBPdf --> TRReg
        TRReg --> DeedyRend
        TRReg --> JakesRend
        TRReg --> HipRend
    end

    subgraph "Shared / Infrastructure"
        JWT["JWT Bearer Auth<br/>(shared secret across APIs)"]
        DBSanitizer["ProjectDatabaseSanitizer<br/>(Win1252 encoding safety)"]
        TimeMiddleware["TimeLogging Middleware<br/>(request duration)"]
    end

    FallbackExec -->|"REST"| GeminiCloud(["Google Gemini API"])
    RBGemini -->|"REST"| GeminiCloud
    AuthCtrl -->|"Google.Apis.Auth"| GoogleOAuthCloud(["Google OAuth"])
```

---

## 3. Entity-Relationship Diagram (ERD)

```mermaid
erDiagram
    %% ── Auth Database ──────────────────────────────────────────
    USERS {
        int UserId PK
        string FullName
        string Email UK
        string PhoneNumber UK
        string PasswordHash
        string Role
        string SubscriptionPlan
        string Provider
        string RefreshToken
        datetime RefreshTokenExpiryTime
        bool IsActive
        datetime CreatedAt
    }

    USER_OTP_CHALLENGES {
        guid Id PK
        int UserId FK
        string Purpose
        string OtpHash
        datetime ExpiresAt
        int AttemptCount
        bool IsConsumed
        datetime CreatedAt
        datetime ConsumedAt
    }

    %% ── Projects Database ──────────────────────────────────────
    PROJECTS {
        guid ProjectId PK
        int UserId
        string Name
        string Type
        string Status
        int CurrentStep
        bool IsDeleted
        datetime CreatedAt
        datetime UpdatedAt
    }

    RESUME_ARTIFACTS {
        guid Id PK
        guid ProjectId FK
        string RawText
        string ParsedResumeJson
        string SourceType
        bool IsDeleted
        datetime CreatedAt
        datetime UpdatedAt
    }

    JD_ARTIFACTS {
        guid Id PK
        guid ProjectId FK
        string RawText
        string ParsedJdJson
        string SourceType
        bool IsDeleted
        datetime CreatedAt
        datetime UpdatedAt
    }

    WIZARD_STATES {
        guid Id PK
        guid ProjectId FK
        string Module
        int CurrentStep
        string StateJson
        bool IsDeleted
        datetime UpdatedAt
    }

    ATS_RESULTS {
        guid Id PK
        guid ProjectId FK
        string JobRole
        string CustomRole
        string AtsResultJson
        int OverallScore
        bool IsDeleted
        datetime CreatedAt
    }

    RESUME_BUILDER_ARTIFACTS {
        guid Id PK
        guid ProjectId FK
        string TemplateId FK
        string BuilderSnapshotJson
        string GeneratedResumeJson
        string GenerationModel
        string LastChangeRequest
        int RevisionCount
        bool IsFinalized
        datetime FinalizedAt
        bool IsDeleted
        datetime CreatedAt
        datetime UpdatedAt
    }

    RESUME_BUILDER_TEMPLATES {
        string TemplateId PK
        string Title
        string Description
        string Category
        string PreviewThumbnailBase64
        string AssetGroupKey
        string RenderContractJson
        string StyleGuideJson
        bool IsDefault
        bool IsActive
        datetime CreatedAt
        datetime UpdatedAt
    }

    RESUME_TEMPLATE_ASSETS {
        guid Id PK
        string TemplateId FK
        string AssetKey
        string MimeType
        string Base64Data
        int Width
        int Height
        bool IsActive
        datetime CreatedAt
        datetime UpdatedAt
    }

    RESUME_PDF_EXPORTS {
        guid Id PK
        guid ProjectId FK
        guid ArtifactId FK
        string TemplateId FK
        string RenderOptionsJson
        bytes PdfBytes
        string Sha256
        string FileName
        bool IsDeleted
        datetime CreatedAt
    }

    NOTIFICATIONS {
        guid Id PK
        int SenderUserId
        int RecipientUserId
        string Subject
        string Body
        bool IsDeleted
        datetime CreatedAt
    }

    NOTIFICATION_USER_STATES {
        guid Id PK
        guid NotificationId FK
        int UserId
        bool IsRead
        datetime ReadAt
        bool IsDeletedForUser
        datetime UpdatedAt
    }

    FEEDBACK {
        guid Id PK
        int UserId
        int Rating
        string FeedbackText
        bool IsDeleted
        datetime CreatedAt
        datetime UpdatedAt
    }

    USER_RESUME_PREFERENCES {
        int UserId PK
        string DefaultResumeRefType
        string DefaultResumeRefId
        datetime UpdatedAt
    }

    %% ── Relationships ──────────────────────────────────────────
    USERS ||--o{ USER_OTP_CHALLENGES : "has"
    PROJECTS ||--o| RESUME_ARTIFACTS : "has one"
    PROJECTS ||--o| JD_ARTIFACTS : "has one"
    PROJECTS ||--o| WIZARD_STATES : "has one"
    PROJECTS ||--o| RESUME_BUILDER_ARTIFACTS : "has one"
    PROJECTS ||--o{ ATS_RESULTS : "has many"
    PROJECTS ||--o{ RESUME_PDF_EXPORTS : "has many"
    RESUME_BUILDER_ARTIFACTS ||--o{ RESUME_PDF_EXPORTS : "exported as"
    RESUME_BUILDER_TEMPLATES ||--o{ RESUME_BUILDER_ARTIFACTS : "used by"
    RESUME_BUILDER_TEMPLATES ||--o{ RESUME_TEMPLATE_ASSETS : "owns"
    NOTIFICATIONS ||--o{ NOTIFICATION_USER_STATES : "tracked by"
```

---

## 4. UML Class Diagram

```mermaid
classDiagram
    %% ── Interfaces ────────────────────────────────────────────
    class IAtsScoringService {
        <<interface>>
        +ScoreResumeAsync(resumeData, jobDesc, jobRole, customRole, ct) Task~AtsScoreResponseDto~
    }

    class IGeminiAtsService {
        <<interface>>
        +GetAtsScoreAsync(resumeData, jobDesc, jobRole, customRole, ct) Task~AtsScoreResponseDto~
    }

    class IResumeTemplateRenderer {
        <<interface>>
        +TemplateId string
        +RenderAsync(resume, context, ct) Task~string~
    }

    class ITokenService {
        <<interface>>
        +CreateToken(user) string
        +GenerateRefreshToken() string
    }

    class IEmailService {
        <<interface>>
        +SendOtpEmailAsync(email, otp, purpose) Task
    }

    class IOtpService {
        <<interface>>
        +GenerateOtpAsync(userId, purpose, ct) Task~string~
        +ValidateOtpAsync(userId, purpose, otp, ct) Task~bool~
    }

    %% ── Services ──────────────────────────────────────────────
    class AtsScoringService {
        -_geminiService IGeminiAtsService
        -_logger ILogger
        +ScoreResumeAsync(resumeData, jobDesc, jobRole, customRole, ct) Task~AtsScoreResponseDto~
    }

    class GeminiAtsService {
        -_httpClient HttpClient
        -_apiKey string
        -_logger ILogger
        +GetAtsScoreAsync(resumeData, jobDesc, jobRole, customRole, ct) Task~AtsScoreResponseDto~
    }

    class GeminiModelFallbackExecutor {
        -_apiKey string
        -_logger ILogger
        +ExecuteWithFallbackAsync(prompt, systemInstruction, ct) Task~string~
    }

    class ResumeBuilderGeminiService {
        -_fallbackExecutor GeminiModelFallbackExecutor
        -_logger ILogger
        +GenerateAsync(template, request, ct) Task~(string, ResumeBuilderGeneratedResumeDto)~
    }

    class ResumeBuilderPdfService {
        -_rendererRegistry ResumeTemplateRendererRegistry
        +RenderAsync(resume, template, ct) Task~byte[]~
        +BuildSha256(bytes) string$
    }

    class ResumeTemplateRendererRegistry {
        -_renderers Dictionary~string, IResumeTemplateRenderer~
        +Register(renderer) void
        +Resolve(templateId) IResumeTemplateRenderer
    }

    class DeedyResumeRenderer {
        +TemplateId string
        +RenderAsync(resume, context, ct) Task~string~
    }

    class JakesResumeRenderer {
        +TemplateId string
        +RenderAsync(resume, context, ct) Task~string~
    }

    class SimpleHipsterResumeRenderer {
        +TemplateId string
        +RenderAsync(resume, context, ct) Task~string~
    }

    %% ── Controllers ───────────────────────────────────────────
    class AtsController {
        -_atsScoringService IAtsScoringService
        -_projectsDb ProjectsDbContext
        +ScoreResume(request, ct) Task~IActionResult~
        +Health() IActionResult
    }

    class ProjectsController {
        -_db ProjectsDbContext
        +CreateProject(request, ct) Task~IActionResult~
        +GetProjects(ct) Task~IActionResult~
        +GetProject(id, ct) Task~IActionResult~
        +UpdateProject(id, request, ct) Task~IActionResult~
        +DeleteProject(id, ct) Task~IActionResult~
        +RestoreProject(id, ct) Task~IActionResult~
    }

    class ResumeBuilderController {
        -_db ProjectsDbContext
        -_geminiService ResumeBuilderGeminiService
        -_pdfService ResumeBuilderPdfService
        +GetTemplates(ct) Task~IActionResult~
        +GetArtifact(projectId, ct) Task~IActionResult~
        +Generate(projectId, request, ct) Task~IActionResult~
        +Revise(projectId, request, ct) Task~IActionResult~
        +ExportPdf(projectId, request, ct) Task~IActionResult~
        +PreviewPdf(projectId, request, ct) Task~IActionResult~
    }

    class AuthController {
        -_context AuthDbContext
        -_tokenService ITokenService
        -_emailService IEmailService
        -_otpService IOtpService
        +Register(dto) Task~IActionResult~
        +Login(dto) Task~IActionResult~
        +AuthenticateWithGoogle(dto) Task~IActionResult~
        +Refresh(dto) Task~IActionResult~
        +Logout() Task~IActionResult~
        +ForgotPasswordRequestOtp(dto) Task~IActionResult~
        +ForgotPasswordVerifyOtp(dto) Task~IActionResult~
        +ForgotPasswordReset(dto) Task~IActionResult~
    }

    %% ── Persistence Entities ─────────────────────────────────
    class ProjectEntity {
        +ProjectId Guid
        +UserId int
        +Name string
        +Type string
        +Status string
        +CurrentStep int
        +IsDeleted bool
        +ResumeArtifact ResumeArtifactEntity
        +JobDescriptionArtifact JobDescriptionArtifactEntity
        +WizardState WizardStateEntity
        +AtsResults ICollection~AtsResultEntity~
    }

    class ResumeBuilderArtifactEntity {
        +Id Guid
        +ProjectId Guid
        +TemplateId string
        +BuilderSnapshotJson string
        +GeneratedResumeJson string
        +RevisionCount int
        +IsFinalized bool
        +PdfExports ICollection~ResumePdfExportEntity~
    }

    %% ── Relationships ─────────────────────────────────────────
    IAtsScoringService <|.. AtsScoringService : implements
    IGeminiAtsService <|.. GeminiAtsService : implements
    IResumeTemplateRenderer <|.. DeedyResumeRenderer : implements
    IResumeTemplateRenderer <|.. JakesResumeRenderer : implements
    IResumeTemplateRenderer <|.. SimpleHipsterResumeRenderer : implements
    ITokenService <|.. TokenService : implements

    AtsScoringService --> IGeminiAtsService : uses
    GeminiAtsService --> GeminiModelFallbackExecutor : delegates
    ResumeBuilderGeminiService --> GeminiModelFallbackExecutor : uses
    ResumeBuilderPdfService --> ResumeTemplateRendererRegistry : uses
    ResumeTemplateRendererRegistry --> IResumeTemplateRenderer : resolves

    AtsController --> IAtsScoringService : injects
    ProjectsController --> ProjectEntity : manages
    ResumeBuilderController --> ResumeBuilderGeminiService : injects
    ResumeBuilderController --> ResumeBuilderPdfService : injects
    AuthController --> ITokenService : injects
    AuthController --> IEmailService : injects
    AuthController --> IOtpService : injects
```

---

## 5. Workflow / Activity Diagram

### 5a. ATS Resume Scoring Workflow

```mermaid
flowchart TD
    Start([User opens ATS page]) --> Upload["Upload Resume PDF / Paste Text"]
    Upload --> ParseResume["Frontend parses resume<br/>via /api/projects/{id}/resume-artifact"]
    ParseResume --> EnterJD["Enter Job Description<br/>(paste text or upload)"]
    EnterJD --> SelectRole["Select Job Role<br/>(predefined or custom)"]
    SelectRole --> HasCache{Project has<br/>cached ATS result?}
    HasCache -->|Yes| ReturnCached["Return cached<br/>AtsResultEntity"]
    HasCache -->|No| CallGemini["Call Gemini AI<br/>via GeminiAtsService"]
    CallGemini --> Fallback{Primary model<br/>succeeded?}
    Fallback -->|No| TryFallback["Try fallback Gemini model<br/>(GeminiModelFallbackExecutor)"]
    TryFallback --> ParseResult
    Fallback -->|Yes| ParseResult["Parse JSON response<br/>→ AtsScoreResponseDto"]
    ParseResult --> PersistResult["Persist AtsResultEntity<br/>to PostgreSQL"]
    PersistResult --> UpdateProject["Update Project.Status = completed<br/>Project.CurrentStep = 4"]
    UpdateProject --> DisplayScore["Display Score Dashboard<br/>OverallScore, KeywordMatch,<br/>ExperienceRelevance,<br/>ProjectRelevance, SkillsMatch"]
    DisplayScore --> ShowRecommendations["Show Recommendations<br/>Missing keywords, Quick wins,<br/>Improved bullet points"]
    ReturnCached --> DisplayScore
    ShowRecommendations --> End([Done])

    style Start fill:#10b981,color:#fff
    style End fill:#10b981,color:#fff
    style HasCache fill:#f59e0b
    style Fallback fill:#f59e0b
```

### 5b. Resume Builder Workflow

```mermaid
flowchart TD
    S([Start Resume Builder]) --> SelectTemplate["Select Template<br/>(Deedy / Jake's / Simple Hipster)"]
    SelectTemplate --> FillWizard["Fill Wizard Steps<br/>Personal Info → Education →<br/>Experience → Projects → Skills"]
    FillWizard --> HasPrefilled{Prefilled resume<br/>data available?}
    HasPrefilled -->|Yes| ReuseData["Reuse parsed resume data<br/>(no AI call needed)"]
    HasPrefilled -->|No| CallGeminiRB["Call ResumeBuilderGeminiService<br/>→ Gemini AI generates<br/>optimized resume JSON"]
    ReuseData --> SaveArtifact
    CallGeminiRB --> SaveArtifact["Save ResumeBuilderArtifactEntity<br/>to DB"]
    SaveArtifact --> PreviewResume["Preview Resume<br/>(rendered HTML/PDF)"]
    PreviewResume --> WantsRevision{User wants<br/>changes?}
    WantsRevision -->|Yes| RevisionRequest["User submits revision request<br/>POST /resume-builder/revise"]
    RevisionRequest --> CallGeminiRB
    WantsRevision -->|No| ExportPdf["Export Final PDF<br/>POST /resume-builder/export-pdf"]
    ExportPdf --> RenderTemplate["Template Renderer<br/>(DeedyRenderer / JakesRenderer / HipsterRenderer)"]
    RenderTemplate --> GeneratePdf["Generate PDF bytes<br/>SHA-256 computed"]
    GeneratePdf --> PersistPdfExport["Persist ResumePdfExportEntity"]
    PersistPdfExport --> UpdateStep["Project.CurrentStep = 7<br/>Status = completed"]
    UpdateStep --> DownloadPdf["User downloads PDF"]
    DownloadPdf --> E([End])

    style S fill:#8b5cf6,color:#fff
    style E fill:#8b5cf6,color:#fff
    style HasPrefilled fill:#f59e0b
    style WantsRevision fill:#f59e0b
```

### 5c. User Authentication Workflow

```mermaid
flowchart TD
    A([Visit App]) --> AuthMethod{Auth method?}
    AuthMethod -->|Local| LoginForm["Enter Email + Password"]
    AuthMethod -->|Google| GoogleBtn["Click Google Sign-In"]
    AuthMethod -->|Register| RegForm["Fill Registration Form<br/>Name, Email, Phone, Password, Role"]

    RegForm --> ValidatePolicy["Validate password policy<br/>(8+ chars, upper/lower/digit/special)"]
    ValidatePolicy --> AdminRole{Admin<br/>role?}
    AdminRole -->|Yes| ValidateAdminKey["Validate AdminSecretKey"]
    AdminRole -->|No| HashPwd
    ValidateAdminKey --> HashPwd["BCrypt hash password (cost 12)"]
    HashPwd --> SaveUser["Save User to auth_db"]
    SaveUser --> RedirectLogin["Redirect to Login"]

    LoginForm --> FindUser["Find user by email/phone"]
    FindUser --> VerifyHash["BCrypt.Verify(password, hash)"]
    VerifyHash --> IssueTokens

    GoogleBtn --> GetGoogleClientId["GET /api/auth/google/config"]
    GetGoogleClientId --> GooglePopup["Google OAuth Popup<br/>returns ID Token"]
    GooglePopup --> ValidateIdToken["POST /api/auth/google<br/>Validate ID Token via Google SDK"]
    ValidateIdToken --> UpsertUser["Upsert user record<br/>(create if first login)"]
    UpsertUser --> IssueTokens

    IssueTokens["Issue JWT Access Token +<br/>Refresh Token (7-day)"] --> StoreTokens["Store tokens in frontend"]
    StoreTokens --> AccessApp["Access protected routes"]

    AccessApp --> TokenExpired{Token<br/>expired?}
    TokenExpired -->|Yes| RefreshFlow["POST /api/auth/refresh<br/>→ new JWT issued"]
    TokenExpired -->|No| Continue["Continue using app"]
    RefreshFlow --> Continue

    style A fill:#10b981,color:#fff
    style AuthMethod fill:#f59e0b
    style AdminRole fill:#f59e0b
    style TokenExpired fill:#f59e0b
```

---

## 6. UML Sequence Diagram — ATS Scoring Flow

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant FE as Angular Frontend
    participant AuthAPI as Auth API<br/>(JWT validation)
    participant AtsCtrl as AtsController
    participant AtsSvc as AtsScoringService
    participant GeminiSvc as GeminiAtsService
    participant Fallback as GeminiModelFallbackExecutor
    participant Gemini as Google Gemini API
    participant DB as PostgreSQL<br/>(projects_db)

    User->>FE: Submit resume + job description + role
    FE->>AtsCtrl: POST /api/ats/score<br/>{ResumeData, JobDescriptionText, JobRole, ProjectId}
    AtsCtrl->>AuthAPI: Validate JWT Bearer token
    AuthAPI-->>AtsCtrl: Claims (userId, role)

    alt ProjectId provided
        AtsCtrl->>DB: Query Projects WHERE ProjectId = ? AND UserId = ?
        DB-->>AtsCtrl: ProjectEntity (or null)
        AtsCtrl->>DB: Query AtsResults WHERE ProjectId = ? ORDER BY CreatedAt DESC
        DB-->>AtsCtrl: Latest AtsResultEntity (or null)

        alt Cached result exists
            AtsCtrl-->>FE: 200 OK — Cached AtsScoreResponseDto
            FE-->>User: Display cached ATS score
        end
    end

    AtsCtrl->>AtsSvc: ScoreResumeAsync(resumeData, jobDesc, jobRole)
    AtsSvc->>GeminiSvc: GetAtsScoreAsync(resumeData, jobDesc, jobRole)
    GeminiSvc->>Fallback: ExecuteWithFallbackAsync(prompt, systemInstruction)
    Fallback->>Gemini: POST /v1beta/models/gemini-1.5-flash:generateContent
    
    alt Primary model succeeds
        Gemini-->>Fallback: JSON response
    else Primary model fails / quota exceeded
        Fallback->>Gemini: POST /v1beta/models/gemini-1.5-pro:generateContent
        Gemini-->>Fallback: JSON response
    end

    Fallback-->>GeminiSvc: Raw JSON string
    GeminiSvc-->>AtsSvc: AtsScoreResponseDto
    AtsSvc-->>AtsCtrl: AtsScoreResponseDto

    AtsCtrl->>DB: INSERT AtsResultEntity (projectId, atsResultJson, overallScore)
    AtsCtrl->>DB: UPDATE Project SET CurrentStep=4, Status='completed'
    DB-->>AtsCtrl: Saved

    AtsCtrl-->>FE: 200 OK — AtsScoreResponseDto
    FE-->>User: Render score dashboard<br/>(OverallScore, KeywordMatch,<br/>Recommendations, Missing keywords)
```

---

## 7. UML Sequence Diagram — Resume Builder Flow

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant FE as Angular Frontend
    participant RBCtrl as ResumeBuilderController
    participant RBGemini as ResumeBuilderGeminiService
    participant Fallback as GeminiModelFallbackExecutor
    participant Gemini as Google Gemini API
    participant PdfSvc as ResumeBuilderPdfService
    participant Registry as TemplateRendererRegistry
    participant Renderer as Template Renderer<br/>(Deedy/Jake's/Hipster)
    participant DB as PostgreSQL

    User->>FE: Open Resume Builder, select template
    FE->>RBCtrl: GET /api/projects/resume-builder/templates
    RBCtrl->>DB: SELECT templates WHERE IsActive = true
    DB-->>RBCtrl: List of ResumeBuilderTemplateEntity
    RBCtrl-->>FE: Template list with metadata + style guides

    User->>FE: Fill wizard (personal info, education, experience, etc.)
    FE->>RBCtrl: POST /api/projects/{id}/resume-builder/generate<br/>{WizardSnapshot, TemplateId, PrefilledResumeJson?}
    
    alt PrefilledResumeJson available (no revision)
        RBCtrl->>RBCtrl: Build resume from parsed data<br/>(TryBuildGeneratedResumeFromPrefilled)
        Note right of RBCtrl: No AI call — reuse existing data
    else AI generation needed
        RBCtrl->>RBGemini: GenerateAsync(template, request)
        RBGemini->>Fallback: ExecuteWithFallbackAsync(prompt)
        Fallback->>Gemini: POST generateContent (gemini-1.5-flash)
        Gemini-->>Fallback: Generated resume JSON
        Fallback-->>RBGemini: Resume JSON string
        RBGemini-->>RBCtrl: (modelUsed, ResumeBuilderGeneratedResumeDto)
    end

    RBCtrl->>DB: UPSERT ResumeBuilderArtifactEntity
    DB-->>RBCtrl: Saved artifact
    RBCtrl-->>FE: ResumeBuilderArtifactResponseDto

    User->>FE: Click "Export PDF"
    FE->>RBCtrl: POST /api/projects/{id}/resume-builder/export-pdf<br/>{TemplateId, ResumeJson, RenderOptions}
    RBCtrl->>PdfSvc: RenderAsync(resume, template)
    PdfSvc->>Registry: Resolve(templateId)
    Registry-->>PdfSvc: IResumeTemplateRenderer
    PdfSvc->>Renderer: RenderAsync(resume, context)
    Renderer-->>PdfSvc: HTML/LaTeX string
    PdfSvc-->>RBCtrl: byte[] PDF

    RBCtrl->>DB: INSERT ResumePdfExportEntity (pdfBytes, sha256, fileName)
    RBCtrl->>DB: UPDATE Project.CurrentStep = 7, Status = 'completed'
    DB-->>RBCtrl: Saved
    RBCtrl-->>FE: application/pdf file download
    FE-->>User: PDF downloaded
```

---

## 8. API / Endpoints Map — Auth API

> Base URL: `https://<host>/api`  
> All endpoints require **JWT Bearer** unless marked `[Anonymous]`

```mermaid
graph LR
    subgraph AUTH["🔐 /api/auth"]
        A1["POST /register<br/>Rate-limited<br/>[Anonymous]<br/>Body: FullName, Email, PhoneNumber,<br/>Password, Role, AdminSecretKey?"]
        A2["POST /login<br/>[Anonymous]<br/>Body: Email?, PhoneNumber?, Password<br/>Returns: JWT + RefreshToken"]
        A3["GET /google/config<br/>[Anonymous]<br/>Returns: Google ClientId"]
        A4["POST /google<br/>Rate-limited, [Anonymous]<br/>Body: IdToken<br/>Returns: JWT + RefreshToken"]
        A5["POST /refresh<br/>[Anonymous]<br/>Body: AccessToken, RefreshToken<br/>Returns: new JWT + RefreshToken"]
        A6["POST /logout<br/>[Authorized]<br/>Revokes RefreshToken"]
        A7["POST /forgot-password/request-otp<br/>[Anonymous]<br/>Body: Email → sends OTP email"]
        A8["POST /forgot-password/verify-otp<br/>[Anonymous]<br/>Body: Email, OTP → returns reset token"]
        A9["POST /forgot-password/reset<br/>[Anonymous]<br/>Body: Email, OTP, NewPassword"]
        A10["POST /delete-account/request-otp<br/>[Authorized]<br/>Sends OTP for account deletion"]
        A11["POST /delete-account/confirm<br/>[Authorized]<br/>Body: OTP → deletes account"]
    end

    subgraph USERS["👤 /api/users"]
        U1["GET /directory<br/>[Authorized]<br/>Returns: [{userId, role}]"]
        U2["GET /profile<br/>[Authorized]<br/>Returns: UserProfileDto"]
        U3["PUT /profile/phone-number<br/>[Authorized]<br/>Body: PhoneNumber"]
    end

    subgraph ADMIN_AUTH["🛡️ /api/admin [ADMIN only]"]
        AA1["GET /overview<br/>Returns: totalUsers, activeUsers,<br/>totalAdmins + ATS context"]
        AA2["GET /users<br/>Returns: full user list"]
        AA3["GET /users/{id}/activity<br/>Returns: user details + activity"]
    end
```

---

## 9. API / Endpoints Map — ATSScore API

> Base URL: `https://<host>/api`  
> All endpoints require **JWT Bearer** unless marked `[Anonymous]`

```mermaid
graph LR
    subgraph ATS["🤖 /api/ats"]
        AT1["POST /score<br/>[Authorized]<br/>Body: ResumeData, JobDescriptionText,<br/>JobRole, CustomRole?, ProjectId?<br/>Returns: AtsScoreResponseDto"]
        AT2["GET /health<br/>[Anonymous]<br/>Returns: status, serviceName, utcNow"]
    end

    subgraph PROJ["📁 /api/projects"]
        P1["POST /<br/>Create new project<br/>Body: Name, Type, Status, CurrentStep"]
        P2["GET /<br/>List user's active projects"]
        P3["GET /history<br/>?includeDeleted=true<br/>Project history including soft-deleted"]
        P4["GET /{id}<br/>Get single project"]
        P5["PATCH /{id}<br/>Update project<br/>Body: Name?, Type?, Status?, CurrentStep?"]
        P6["DELETE /{id}<br/>Soft-delete project + cascade JD + wizard + ATS"]
        P7["POST /{id}/restore<br/>Restore soft-deleted project"]
        P8["DELETE /{id}/permanent<br/>Hard-delete project and all data"]
        P9["DELETE /account/purge<br/>Delete ALL projects for current user"]

        P10["GET /resume-library<br/>User's saved resume artifacts"]
        P11["POST /resume-library/default/{resumeId}<br/>Set default resume"]
        P12["GET /resume-library/default<br/>Get default resume metadata"]
        P13["GET /resume-library/default/resolve<br/>Get resolved default resume JSON"]

        P14["PUT /{id}/resume-artifact<br/>Body: resume file/text<br/>Upsert parsed resume data"]
        P15["GET /{id}/resume-artifact<br/>Get parsed resume artifact"]
        P16["PUT /{id}/jd-artifact<br/>Body: JD text<br/>Upsert parsed job description"]
        P17["GET /{id}/jd-artifact<br/>Get parsed JD artifact"]

        P18["POST /{id}/ats-results<br/>Manually post ATS result"]
        P19["GET /{id}/ats-results/latest<br/>Get latest ATS result for project"]

        P20["PUT /{id}/wizard-state/{module}<br/>Body: StateJson, CurrentStep<br/>Upsert wizard state (ats/resume-builder)"]
        P21["GET /{id}/wizard-state/{module}<br/>Get wizard state"]
    end

    subgraph RB["🏗️ /api/projects — Resume Builder"]
        RB1["GET /resume-builder/templates<br/>List all active templates"]
        RB2["GET /{projectId}/resume-builder/artifact<br/>Get current resume builder artifact"]
        RB3["POST /{projectId}/resume-builder/generate<br/>Body: WizardSnapshot, TemplateId?,<br/>PrefilledResumeJson?, TargetRole?"]
        RB4["POST /{projectId}/resume-builder/revise<br/>Body: WizardSnapshot + RevisionContext<br/>(same handler as generate)"]
        RB5["POST /{projectId}/resume-builder/export-pdf<br/>Body: TemplateId?, ResumeJson?, RenderOptions?<br/>Returns: application/pdf"]
        RB6["POST /{projectId}/resume-builder/preview-pdf<br/>Body: TemplateId?, ResumeJson?<br/>Returns: application/pdf (unsaved)"]
        RB7["GET /{projectId}/resume-builder/pdf/latest/metadata<br/>Returns: export metadata (no bytes)"]
        RB8["GET /{projectId}/resume-builder/pdf/latest<br/>Returns: latest exported PDF bytes"]
    end

    subgraph NOTIF["🔔 /api/notifications"]
        N1["POST /<br/>Body: ToUserId, Subject, Body<br/>Creates notification + user states"]
        N2["GET /inbox<br/>Recipient's inbox with read status"]
        N3["GET /sent<br/>Sender's sent notifications"]
        N4["POST /{id}/read<br/>Mark notification as read for current user"]
        N5["DELETE /{id}<br/>Soft-delete notification for current user"]
    end

    subgraph FEED["⭐ /api/feedback"]
        F1["POST /<br/>Body: Rating (1–10), FeedbackText<br/>(Users only, not admins)"]
        F2["GET /<br/>List all feedback (any auth user)"]
    end

    subgraph ADMIN_ATS["🛡️ /api/admin [ADMIN only]"]
        AD1["GET /overview-context<br/>Returns: project stats + notification stats + feedback count"]
        AD2["GET /users/{id}/activity-context<br/>Returns: projectCount, completedProjects,<br/>notification stats, recent projects"]
    end
```

---

## 10. Frontend Component Tree

```mermaid
graph TD
    App["AppComponent<br/>(Angular 17 SSR)"]

    App --> AuthModule["Auth Module"]
    App --> DashboardModule["Dashboard Module"]

    AuthModule --> LoginComp["LoginComponent<br/>Email/Phone + Password<br/>Google Sign-In button"]
    AuthModule --> RegisterComp["RegisterComponent<br/>Full registration form"]
    AuthModule --> AuthSvc["AuthService<br/>(HTTP + token management)"]

    DashboardModule --> Sidebar["SidebarComponent<br/>(navigation)"]
    DashboardModule --> AtsPage["AtsPage<br/>(ATS Scoring)"]
    DashboardModule --> ResumePage["ResumeBuilderPage<br/>(full wizard + preview)"]
    DashboardModule --> ProjectsPage["ProjectsPage<br/>(project cards)"]
    DashboardModule --> NotifPage["NotificationsPage<br/>(inbox view)"]
    DashboardModule --> AccountPage["AccountSettingsPage<br/>(profile + phone)"]
    DashboardModule --> AdminPage["AdminPage<br/>(admin dashboard)"]
    DashboardModule --> RateUsPage["RateUsPage<br/>(feedback form)"]
    DashboardModule --> SubscriptionPage["SubscriptionPage"]
    DashboardModule --> TemplatesPage["TemplatesPage"]
    DashboardModule --> ThemePage["ThemePage"]

    ProjectsPage --> ProjectCardComp["ProjectCardComponent"]
    ProjectsPage --> ProjectModalComp["ProjectModalComponent"]
    DashboardModule --> ThemeCardComp["ThemeCardComponent"]

    subgraph Services["Angular Services"]
        AtsScoreApiSvc["AtsScoreApiService"]
        ProjectsApiSvc["ProjectsApiService"]
        ResumeBuilderApiSvc["ResumeBuilderApiService"]
        NotificationsApiSvc["NotificationsApiService"]
        FeedbackApiSvc["FeedbackApiService"]
        AdminApiSvc["AdminApiService"]
        JobDescParserSvc["JobDescriptionParserApiService"]
        ResumeParserSvc["ResumeParserApiService"]
        ProjectsStore["ProjectsStore<br/>(state management)"]
        ThemeSvc["ThemeService"]
    end

    AtsPage --> AtsScoreApiSvc
    ResumePage --> ResumeBuilderApiSvc
    ProjectsPage --> ProjectsApiSvc
    ProjectsPage --> ProjectsStore
    NotifPage --> NotificationsApiSvc
    RateUsPage --> FeedbackApiSvc
    AdminPage --> AdminApiSvc
```

---

## 11. Database Schema Overview

```mermaid
graph TB
    subgraph AuthDB["🗄️ auth_db (Auth API)"]
        Users["users<br/>PK: UserId (int)<br/>Email UNIQUE<br/>PhoneNumber UNIQUE<br/>Role, SubscriptionPlan<br/>Provider, RefreshToken<br/>IsActive"]
        OTP["user_otp_challenges<br/>PK: Id (guid)<br/>FK: UserId<br/>Purpose, OtpHash<br/>ExpiresAt, IsConsumed<br/>AttemptCount"]
        Users -->|1 to many| OTP
    end

    subgraph ProjectsDB["🗄️ projects_db (ATSScore API)"]
        Projects["projects<br/>PK: ProjectId (guid)<br/>UserId (int, no FK)<br/>Name, Type, Status<br/>CurrentStep, IsDeleted"]
        
        ResumeArtifacts["resume_artifacts<br/>PK: Id (guid)<br/>FK: ProjectId<br/>ParsedResumeJson, SourceType"]
        
        JdArtifacts["jd_artifacts<br/>PK: Id (guid)<br/>FK: ProjectId<br/>ParsedJdJson, SourceType"]
        
        WizardStates["wizard_states<br/>PK: Id (guid)<br/>FK: ProjectId<br/>Module, CurrentStep, StateJson"]
        
        AtsResults["ats_results<br/>PK: Id (guid)<br/>FK: ProjectId<br/>JobRole, AtsResultJson<br/>OverallScore, IsDeleted"]
        
        RBTemplates["resume_builder_templates<br/>PK: TemplateId (string)<br/>Title, Category<br/>RenderContractJson<br/>StyleGuideJson, IsDefault"]
        
        RBAssets["resume_template_assets<br/>PK: Id (guid)<br/>FK: TemplateId<br/>AssetKey, MimeType<br/>Base64Data"]
        
        RBArtifacts["resume_builder_artifacts<br/>PK: Id (guid)<br/>FK: ProjectId, TemplateId<br/>BuilderSnapshotJson<br/>GeneratedResumeJson<br/>RevisionCount, IsFinalized"]
        
        PdfExports["resume_pdf_exports<br/>PK: Id (guid)<br/>FK: ProjectId, ArtifactId, TemplateId<br/>PdfBytes, Sha256, FileName"]
        
        Notifications["notifications<br/>PK: Id (guid)<br/>SenderUserId, RecipientUserId<br/>Subject, Body, IsDeleted"]
        
        NotifStates["notification_user_states<br/>PK: Id (guid)<br/>FK: NotificationId<br/>UserId, IsRead, ReadAt<br/>IsDeletedForUser"]
        
        Feedback["feedback<br/>PK: Id (guid)<br/>UserId, Rating (1-10)<br/>FeedbackText, IsDeleted"]
        
        UserPrefs["user_resume_preferences<br/>PK: UserId (int)<br/>DefaultResumeRefType<br/>DefaultResumeRefId"]

        Projects -->|1:0..1| ResumeArtifacts
        Projects -->|1:0..1| JdArtifacts
        Projects -->|1:0..1| WizardStates
        Projects -->|1:0..1| RBArtifacts
        Projects -->|1:N| AtsResults
        Projects -->|1:N| PdfExports
        RBTemplates -->|1:N| RBArtifacts
        RBTemplates -->|1:N| RBAssets
        RBArtifacts -->|1:N| PdfExports
        Notifications -->|1:N| NotifStates
    end

    AuthDB -.->|"UserId shared<br/>(no cross-DB FK)"| ProjectsDB
```

---

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
