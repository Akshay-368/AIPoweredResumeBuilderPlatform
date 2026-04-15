using ResumeAI.ATSScore.API.Persistence;

namespace ResumeAI.ATSScore.API.Services.TemplateRendering;

public interface IResumeTemplateRenderer
{
    string TemplateId { get; }
    byte[] Render(ResumeBuilderGeneratedResumeDto resume, ResumeBuilderTemplateEntity template, ResumeTemplateRenderContext context);
}
