using System.Text.Json;
using ResumeAI.ATSScore.API.DTO.ResumeBuilder;
using ResumeAI.ATSScore.API.Persistence;

namespace ResumeAI.ATSScore.API.Services;

public class ResumeBuilderGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResumeBuilderGeminiService> _logger;
    private readonly string _apiKey;

    public ResumeBuilderGeminiService(HttpClient httpClient, ILogger<ResumeBuilderGeminiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("API_Key")
            ?? throw new InvalidOperationException("Missing GEMINI_API_KEY (or API_Key) in environment variables.");
    }

    public async Task<(string ModelUsed, ResumeBuilderGeneratedResumeDto? Resume)> GenerateAsync(
        ResumeBuilderTemplateEntity template,
        ResumeBuilderGenerateRequestDto request,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(template, request);
        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = prompt } }
            },
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                response_mime_type = "application/json"
            }
        };

        var (modelUsed, jsonResponse) = await GeminiModelFallbackExecutor.SendWithFallbackAsync(
            _httpClient,
            _apiKey,
            requestBody,
            _logger,
            cancellationToken);

        using var doc = JsonDocument.Parse(jsonResponse);
        var aiResponseText = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(aiResponseText))
        {
            return (modelUsed, null);
        }

        var resume = JsonSerializer.Deserialize<ResumeBuilderGeneratedResumeDto>(aiResponseText, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (resume is null)
        {
            return (modelUsed, null);
        }

        return (modelUsed, resume with
        {
            TemplateId = string.IsNullOrWhiteSpace(resume.TemplateId) ? template.TemplateId : resume.TemplateId,
            TemplateStyle = resume.TemplateStyle with
            {
                TemplateId = string.IsNullOrWhiteSpace(resume.TemplateStyle.TemplateId) ? template.TemplateId : resume.TemplateStyle.TemplateId,
                Title = string.IsNullOrWhiteSpace(resume.TemplateStyle.Title) ? template.Title : resume.TemplateStyle.Title,
                Description = string.IsNullOrWhiteSpace(resume.TemplateStyle.Description) ? template.Description : resume.TemplateStyle.Description
            }
        });
    }

    private static string BuildPrompt(ResumeBuilderTemplateEntity template, ResumeBuilderGenerateRequestDto request)
    {
        var snapshotJson = JsonSerializer.Serialize(request.WizardSnapshot, new JsonSerializerOptions { WriteIndented = true });
        var styleGuide = string.IsNullOrWhiteSpace(template.StyleGuideJson) ? "{}" : template.StyleGuideJson;
        var renderContract = string.IsNullOrWhiteSpace(template.RenderContractJson) ? "{}" : template.RenderContractJson;
        var revisionContext = request.RevisionContext is null
            ? ""
            : JsonSerializer.Serialize(request.RevisionContext, new JsonSerializerOptions { WriteIndented = true });

                return $$"""
You are a resume generation engine.

Rules:
- Use the wizard snapshot as the source of truth for user-provided facts.
- Do not invent achievements, employers, degrees, dates, or skills that are not supported by the input.
- Improve phrasing, ordering, and presentation for a professional resume.
- Respect the selected template style and the supplied length policy.
- Return ONLY valid JSON.
- Do not include markdown, code fences, or commentary.

Output schema:
{
    "templateId": "string",
    "profile": {
        "fullName": "string",
        "professionalRole": "string",
        "email": "string",
        "phone": "string",
        "linkedInUrl": "string",
        "portfolioUrl": "string",
        "location": "string"
    },
    "targetRole": "string",
    "summary": "string",
    "skills": ["string"],
    "education": [{
        "institution": "string",
        "degree": "string",
        "fieldOfStudy": "string",
        "startYear": "string",
        "endYear": "string",
        "isPresent": false,
        "marks": "string"
    }],
    "experience": [{
        "company": "string",
        "role": "string",
        "startDate": "string",
        "endDate": "string",
        "isPresent": false,
        "description": "string"
    }],
    "projects": [{
        "name": "string",
        "techStack": "string",
        "description": "string"
    }],
    "templateStyle": {
        "templateId": "string",
        "title": "string",
        "description": "string",
        "styleGuide": {}
    },
    "generationNotes": ["string"],
    "revisionHistory": ["string"]
}

Template metadata:
{{styleGuide}}

Render contract metadata:
{{renderContract}}

Template id:
{{template.TemplateId}}

Template title:
{{template.Title}}

Template description:
{{template.Description}}

Requested target role:
{{request.TargetRole ?? string.Empty}}

Tone:
{{request.Tone ?? "professional"}}

Length policy:
{{request.LengthPolicy ?? "one_page"}}

Wizard snapshot:
{{snapshotJson}}

{{(string.IsNullOrWhiteSpace(revisionContext) ? string.Empty : $"Revision context:\n{revisionContext}")}}
""";
    }
}

public record ResumeBuilderGeneratedProfileDto(
    string FullName,
    string ProfessionalRole,
    string Email,
    string? Phone = null,
    string? LinkedInUrl = null,
    string? PortfolioUrl = null,
    string? Location = null
);

public record ResumeBuilderGeneratedTemplateStyleDto(
    string TemplateId,
    string Title,
    string Description,
    JsonElement StyleGuide
);

public record ResumeBuilderGeneratedEducationItemDto(
    string Institution,
    string Degree,
    string? FieldOfStudy = null,
    string? StartYear = null,
    string? EndYear = null,
    bool IsPresent = false,
    string? Marks = null
);

public record ResumeBuilderGeneratedExperienceItemDto(
    string Company,
    string Role,
    string? StartDate = null,
    string? EndDate = null,
    bool IsPresent = false,
    string? Description = null
);

public record ResumeBuilderGeneratedProjectItemDto(
    string Name,
    string TechStack,
    string? Description = null
);

public record ResumeBuilderGeneratedResumeDto(
    string TemplateId,
    ResumeBuilderGeneratedProfileDto Profile,
    string TargetRole,
    string Summary,
    List<string> Skills,
    List<ResumeBuilderGeneratedEducationItemDto> Education,
    List<ResumeBuilderGeneratedExperienceItemDto> Experience,
    List<ResumeBuilderGeneratedProjectItemDto> Projects,
    ResumeBuilderGeneratedTemplateStyleDto TemplateStyle,
    List<string> GenerationNotes,
    List<string> RevisionHistory
);