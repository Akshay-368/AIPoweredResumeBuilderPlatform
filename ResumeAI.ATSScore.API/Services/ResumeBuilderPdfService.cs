using System.Security.Cryptography;
using QuestPDF.Infrastructure;
using ResumeAI.ATSScore.API.Persistence;
using ResumeAI.ATSScore.API.Services.TemplateRendering;

namespace ResumeAI.ATSScore.API.Services;

public class ResumeBuilderPdfService
{
    private readonly ResumeTemplateRendererRegistry _registry;
    private readonly ResumeTemplateAssetResolver _assetResolver;
    private readonly ILogger<ResumeBuilderPdfService> _logger;

    public ResumeBuilderPdfService(
        ResumeTemplateRendererRegistry registry,
        ResumeTemplateAssetResolver assetResolver,
        ILogger<ResumeBuilderPdfService> logger)
    {
        _registry = registry;
        _assetResolver = assetResolver;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> RenderAsync(ResumeBuilderGeneratedResumeDto resume, ResumeBuilderTemplateEntity template, CancellationToken cancellationToken)
    {
        var requestedTemplateId = string.IsNullOrWhiteSpace(template.TemplateId)
            ? "deedy-one-page-two-column"
            : template.TemplateId;

        var contract = ResumeTemplateRenderContract.ParseOrDefault(template.RenderContractJson, requestedTemplateId);
        var assetMap = await _assetResolver.GetSvgAssetsAsync(requestedTemplateId, cancellationToken);
        var renderer = _registry.Resolve(requestedTemplateId);

        if (!string.Equals(renderer.TemplateId, requestedTemplateId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Template renderer fallback applied. Requested template: {RequestedTemplateId}, resolved renderer: {ResolvedTemplateId}",
                requestedTemplateId,
                renderer.TemplateId);
        }

        return renderer.Render(resume, template, new ResumeTemplateRenderContext(contract, assetMap));
    }

    public static string BuildSha256(byte[] pdfBytes)
    {
        var hash = SHA256.HashData(pdfBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}