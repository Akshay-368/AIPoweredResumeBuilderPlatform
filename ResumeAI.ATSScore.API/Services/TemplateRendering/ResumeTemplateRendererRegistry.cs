namespace ResumeAI.ATSScore.API.Services.TemplateRendering;

public class ResumeTemplateRendererRegistry
{
    private readonly Dictionary<string, IResumeTemplateRenderer> _renderers;

    public ResumeTemplateRendererRegistry(IEnumerable<IResumeTemplateRenderer> renderers)
    {
        _renderers = renderers.ToDictionary(renderer => renderer.TemplateId, StringComparer.OrdinalIgnoreCase);
    }

    public IResumeTemplateRenderer Resolve(string? templateId)
    {
        if (!string.IsNullOrWhiteSpace(templateId) && _renderers.TryGetValue(templateId, out var renderer))
        {
            return renderer;
        }

        if (_renderers.TryGetValue("deedy-one-page-two-column", out var deedy))
        {
            return deedy;
        }

        return _renderers.Values.First();
    }
}
