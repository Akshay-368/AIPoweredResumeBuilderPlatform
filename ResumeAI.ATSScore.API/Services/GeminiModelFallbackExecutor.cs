using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Polly.Timeout;

namespace ResumeAI.ATSScore.API.Services;

internal static class GeminiModelFallbackExecutor
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> ModelCooldowns = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan RateLimitCooldown = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ServiceUnavailableCooldown = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan TimeoutCooldown = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan GenericTransientCooldown = TimeSpan.FromMinutes(1);

    internal static IReadOnlyList<string> ResolveOrderedModels()
    {
        var primaryModel = NormalizeModelName(Environment.GetEnvironmentVariable("AI__PrimaryModel"));
        var candidateModels = SplitModelNames(Environment.GetEnvironmentVariable("AI__ModelCandidates"));
        var legacyModel = NormalizeModelName(Environment.GetEnvironmentVariable("AI__Model"));

        var orderedModels = new List<string>();

        if (!string.IsNullOrWhiteSpace(primaryModel))
        {
            orderedModels.Add(primaryModel);
        }

        foreach (var model in candidateModels)
        {
            AddDistinctModel(orderedModels, model);
        }

        if (orderedModels.Count == 0 && !string.IsNullOrWhiteSpace(legacyModel))
        {
            orderedModels.Add(legacyModel);
        }

        if (orderedModels.Count == 0)
        {
            throw new InvalidOperationException("No Gemini models configured. Set AI__PrimaryModel or AI__ModelCandidates.");
        }

        return orderedModels;
    }

    internal static async Task<(string ModelUsed, string ResponseJson)> SendWithFallbackAsync(
        HttpClient httpClient,
        string apiKey,
        object requestBody,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var configuredModels = ResolveOrderedModels();
        var immediateModels = new List<string>();
        var deferredModels = new List<string>();

        foreach (var model in configuredModels)
        {
            if (IsInCooldown(model))
            {
                deferredModels.Add(model);
                continue;
            }

            immediateModels.Add(model);
        }

        if (immediateModels.Count == 0)
        {
            logger.LogWarning("All configured Gemini models are currently in cooldown. Retrying them in configured order.");
            immediateModels.AddRange(deferredModels);
            deferredModels.Clear();
        }

        Exception? lastFailure = null;

        foreach (var model in immediateModels)
        {
            var outcome = await TrySendAsync(httpClient, apiKey, model, requestBody, logger, cancellationToken, respectCooldown: true);

            if (outcome.Success)
            {
                return (model, outcome.ResponseJson!);
            }

            if (!outcome.ShouldFailover)
            {
                throw outcome.Exception!;
            }

            lastFailure = outcome.Exception;
        }

        if (deferredModels.Count > 0)
        {
            logger.LogInformation("Retrying Gemini models that were previously in cooldown.");

            foreach (var model in deferredModels)
            {
                var outcome = await TrySendAsync(httpClient, apiKey, model, requestBody, logger, cancellationToken, respectCooldown: false);

                if (outcome.Success)
                {
                    return (model, outcome.ResponseJson!);
                }

                if (!outcome.ShouldFailover)
                {
                    throw outcome.Exception!;
                }

                lastFailure = outcome.Exception;
            }
        }

        throw new InvalidOperationException("All configured Gemini models failed.", lastFailure);
    }

    private static async Task<GeminiAttemptOutcome> TrySendAsync(
        HttpClient httpClient,
        string apiKey,
        string model,
        object requestBody,
        ILogger logger,
        CancellationToken cancellationToken,
        bool respectCooldown)
    {
        if (respectCooldown && IsInCooldown(model))
        {
            logger.LogInformation("Skipping Gemini model {Model} because it is still in cooldown.", model);
            return new GeminiAttemptOutcome(false, true, null, new InvalidOperationException($"Model {model} is cooling down."));
        }

        logger.LogInformation("Sending Gemini request using model {Model}.", model);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                $"v1beta/models/{model}:generateContent?key={apiKey}",
                requestBody,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                ClearCooldown(model);
                logger.LogInformation("Gemini request succeeded with model {Model}.", model);
                return new GeminiAttemptOutcome(true, false, jsonResponse, null);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (ShouldFailover(response.StatusCode))
            {
                ApplyCooldown(model, response.StatusCode);
                logger.LogWarning(
                    "Gemini model {Model} returned transient status {StatusCode}; trying next model. Response: {ErrorBody}",
                    model,
                    (int)response.StatusCode,
                    errorBody);

                return new GeminiAttemptOutcome(
                    false,
                    true,
                    null,
                    new HttpRequestException($"Gemini API returned {response.StatusCode} for model {model}: {errorBody}"));
            }

            logger.LogError(
                "Gemini model {Model} returned non-retryable status {StatusCode}. Response: {ErrorBody}",
                model,
                (int)response.StatusCode,
                errorBody);

            return new GeminiAttemptOutcome(
                false,
                false,
                null,
                new HttpRequestException($"Gemini API returned {response.StatusCode} for model {model}: {errorBody}"));
        }
        catch (TimeoutRejectedException ex) when (!cancellationToken.IsCancellationRequested)
        {
            ApplyCooldown(model, isTimeout: true);
            logger.LogWarning(ex, "Gemini model {Model} timed out; trying next model.", model);
            return new GeminiAttemptOutcome(false, true, null, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            ApplyCooldown(model, isTimeout: true);
            logger.LogWarning(ex, "Gemini model {Model} timed out; trying next model.", model);
            return new GeminiAttemptOutcome(false, true, null, ex);
        }
        catch (HttpRequestException ex) when (!cancellationToken.IsCancellationRequested)
        {
            ApplyCooldown(model);
            logger.LogWarning(ex, "Gemini model {Model} hit a transport error; trying next model.", model);
            return new GeminiAttemptOutcome(false, true, null, ex);
        }
    }

    private static bool ShouldFailover(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private static void ApplyCooldown(string model, HttpStatusCode? statusCode = null, bool isTimeout = false)
    {
        var cooldownDuration = isTimeout
            ? TimeoutCooldown
            : statusCode switch
            {
                HttpStatusCode.TooManyRequests => RateLimitCooldown,
                HttpStatusCode.ServiceUnavailable => ServiceUnavailableCooldown,
                HttpStatusCode.GatewayTimeout => TimeoutCooldown,
                HttpStatusCode.InternalServerError => GenericTransientCooldown,
                HttpStatusCode.BadGateway => GenericTransientCooldown,
                _ => GenericTransientCooldown
            };

        ModelCooldowns[model] = DateTimeOffset.UtcNow.Add(cooldownDuration);
    }

    private static bool IsInCooldown(string model)
    {
        if (!ModelCooldowns.TryGetValue(model, out var cooldownUntil))
        {
            return false;
        }

        if (cooldownUntil > DateTimeOffset.UtcNow)
        {
            return true;
        }

        ModelCooldowns.TryRemove(model, out _);
        return false;
    }

    private static void ClearCooldown(string model)
    {
        ModelCooldowns.TryRemove(model, out _);
    }

    private static void AddDistinctModel(ICollection<string> models, string model)
    {
        if (models.Any(existing => string.Equals(existing, model, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        models.Add(model);
    }

    private static IEnumerable<string> SplitModelNames(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            yield break;
        }

        foreach (var segment in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var model = NormalizeModelName(segment);
            if (!string.IsNullOrWhiteSpace(model))
            {
                yield return model;
            }
        }
    }

    private static string NormalizeModelName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().Trim('"').Trim('\'');
    }

    private sealed record GeminiAttemptOutcome(bool Success, bool ShouldFailover, string? ResponseJson, Exception? Exception);
}