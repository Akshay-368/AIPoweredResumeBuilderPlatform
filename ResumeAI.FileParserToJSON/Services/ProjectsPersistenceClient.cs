using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ResumeAI.FileParserToJson.Services;

public class ProjectsPersistenceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProjectsPersistenceClient> _logger;

    public ProjectsPersistenceClient(HttpClient httpClient, ILogger<ProjectsPersistenceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<object?> TryGetResumeArtifactAsync(Guid projectId, string? bearerToken, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/projects/{projectId}/resume-artifact");
        ApplyToken(request, bearerToken);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch existing resume artifact for project {ProjectId}. Status {StatusCode}", projectId, (int)response.StatusCode);
                return null;
            }

            using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            if (payload == null || !payload.RootElement.TryGetProperty("parsedResumeJson", out var parsedResume))
            {
                return null;
            }

            return JsonSerializer.Deserialize<object>(parsedResume.GetRawText());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resume artifact lookup failed for project {ProjectId}", projectId);
            return null;
        }
    }

    public async Task<object?> TryGetJdArtifactAsync(Guid projectId, string? bearerToken, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/projects/{projectId}/jd-artifact");
        ApplyToken(request, bearerToken);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch existing JD artifact for project {ProjectId}. Status {StatusCode}", projectId, (int)response.StatusCode);
                return null;
            }

            using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            if (payload == null || !payload.RootElement.TryGetProperty("parsedJdJson", out var parsedJd))
            {
                return null;
            }

            return JsonSerializer.Deserialize<object>(parsedJd.GetRawText());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JD artifact lookup failed for project {ProjectId}", projectId);
            return null;
        }
    }

    public async Task<bool> UpsertResumeArtifactAsync(Guid projectId, string? rawText, object parsedResume, string sourceType, string? bearerToken, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"api/projects/{projectId}/resume-artifact")
        {
            Content = JsonContent.Create(new
            {
                rawText,
                parsedResumeJson = parsedResume,
                sourceType
            })
        };

        ApplyToken(request, bearerToken);
        var result = await SendBestEffortWithDetailsAsync(request, "resume", projectId, cancellationToken);
        return result.Success;
    }

    public async Task<(bool Success, int? StatusCode, string? ErrorBody)> UpsertResumeArtifactWithDetailsAsync(Guid projectId, string? rawText, object parsedResume, string sourceType, string? bearerToken, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"api/projects/{projectId}/resume-artifact")
        {
            Content = JsonContent.Create(new
            {
                rawText,
                parsedResumeJson = parsedResume,
                sourceType
            })
        };

        ApplyToken(request, bearerToken);
        return await SendBestEffortWithDetailsAsync(request, "resume", projectId, cancellationToken);
    }

    public async Task<bool> UpsertJdArtifactAsync(Guid projectId, string? rawText, object parsedJd, string sourceType, string? bearerToken, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"api/projects/{projectId}/jd-artifact")
        {
            Content = JsonContent.Create(new
            {
                rawText,
                parsedJdJson = parsedJd,
                sourceType
            })
        };

        ApplyToken(request, bearerToken);
        return await SendBestEffortAsync(request, "jd", projectId, cancellationToken);
    }

    public async Task<bool> UpsertWizardStateAsync(Guid projectId, string module, int currentStep, object stateJson, string? bearerToken, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"api/projects/{projectId}/wizard-state/{module}")
        {
            Content = JsonContent.Create(new
            {
                currentStep,
                stateJson
            })
        };

        ApplyToken(request, bearerToken);
        return await SendBestEffortAsync(request, "wizard", projectId, cancellationToken);
    }

    private async Task<bool> SendBestEffortAsync(HttpRequestMessage request, string operation, Guid projectId, CancellationToken cancellationToken)
    {
        var result = await SendBestEffortWithDetailsAsync(request, operation, projectId, cancellationToken);
        return result.Success;
    }

    private async Task<(bool Success, int? StatusCode, string? ErrorBody)> SendBestEffortWithDetailsAsync(HttpRequestMessage request, string operation, Guid projectId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Projects persistence {Operation} upsert failed for project {ProjectId}. Status {StatusCode}. Body: {Body}",
                    operation,
                    projectId,
                    (int)response.StatusCode,
                    string.IsNullOrWhiteSpace(body) ? "<empty>" : body);
                return (false, (int)response.StatusCode, body);
            }

            Console.WriteLine($"[ProjectsPersistenceClient] {operation} write succeeded. ProjectId={projectId}, StatusCode={(int)response.StatusCode}");
            return (true, (int)response.StatusCode, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Projects persistence {Operation} upsert failed for project {ProjectId}", operation, projectId);
            return (false, null, ex.GetBaseException().Message);
        }
    }

    private static void ApplyToken(HttpRequestMessage request, string? bearerToken)
    {
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }
}
