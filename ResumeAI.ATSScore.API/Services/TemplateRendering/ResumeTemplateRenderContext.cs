namespace ResumeAI.ATSScore.API.Services.TemplateRendering;

public sealed record ResumeTemplateRenderContext(
    ResumeTemplateRenderContract Contract,
    IReadOnlyDictionary<string, string> AssetSvgByKey
);
