using QuestPDF.Fluent;
using ResumeAI.ATSScore.API.Persistence;

namespace ResumeAI.ATSScore.API.Services.TemplateRendering;

public class JakesResumeRenderer : IResumeTemplateRenderer
{
    public string TemplateId => "jakes-template";

    public byte[] Render(ResumeBuilderGeneratedResumeDto resume, ResumeBuilderTemplateEntity template, ResumeTemplateRenderContext context)
    {
        var contract = context.Contract;
        var typography = contract.Typography;
        var colors = contract.Colors;
        var limits = contract.Limits;

        var experience = ResumeRenderHelpers.ClampExperience(resume.Experience, limits);
        var projects = ResumeRenderHelpers.ClampProjects(resume.Projects, limits);
        var skills = ResumeRenderHelpers.ClampSkills(resume.Skills, 16);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(ResumeRenderHelpers.ResolvePageSize(contract.Page.Size));
                page.Margin(contract.Page.Margin);
                page.DefaultTextStyle(ResumeRenderHelpers.BaseStyle(typography, colors));

                page.Content().Column(main =>
                {
                    main.Spacing(8);

                    main.Item().AlignCenter().Column(header =>
                    {
                        header.Item().AlignCenter().Text(resume.Profile.FullName).FontSize(typography.NameSize).SemiBold().FontColor(colors.Secondary);
                        header.Item().AlignCenter().Text(string.IsNullOrWhiteSpace(resume.Profile.ProfessionalRole) ? resume.TargetRole : resume.Profile.ProfessionalRole)
                            .FontSize(typography.RoleSize)
                            .FontColor(colors.Muted);
                        header.Item().AlignCenter().Text(ResumeRenderHelpers.BuildContactLine(resume.Profile)).FontSize(typography.SmallTextSize).FontColor(colors.Muted);
                    });

                    ResumeRenderHelpers.AddSectionTitle(main, "Summary", typography, colors);
                    main.Item().Text(resume.Summary ?? string.Empty).FontSize(typography.BodySize);

                    if (experience.Count > 0)
                    {
                        ResumeRenderHelpers.AddSectionTitle(main, "Experience", typography, colors);
                        foreach (var item in experience)
                        {
                            main.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{item.Role} - {item.Company}").SemiBold().FontSize(typography.BodySize);
                                row.ConstantItem(150).AlignRight().Text(string.Join(" - ", new[] { item.StartDate, item.IsPresent ? "Present" : item.EndDate }.Where(x => !string.IsNullOrWhiteSpace(x))))
                                    .FontSize(typography.SmallTextSize)
                                    .FontColor(colors.Muted);
                            });

                            if (!string.IsNullOrWhiteSpace(item.Description))
                            {
                                main.Item().Text($"• {item.Description}").FontSize(typography.BodySize);
                            }
                        }
                    }

                    if (projects.Count > 0)
                    {
                        ResumeRenderHelpers.AddSectionTitle(main, "Projects", typography, colors);
                        foreach (var project in projects)
                        {
                            main.Item().Text($"{project.Name} ({project.TechStack})").SemiBold().FontSize(typography.BodySize);
                            if (!string.IsNullOrWhiteSpace(project.Description))
                            {
                                main.Item().Text(project.Description).FontSize(typography.BodySize);
                            }
                        }
                    }

                    if (resume.Education.Count > 0)
                    {
                        ResumeRenderHelpers.AddSectionTitle(main, "Education", typography, colors);
                        foreach (var education in resume.Education.Take(3))
                        {
                            main.Item().Text($"{education.Degree} - {education.Institution}").SemiBold().FontSize(typography.BodySize);
                            main.Item().Text(ResumeRenderHelpers.BuildEducationLine(education)).FontSize(typography.SmallTextSize).FontColor(colors.Muted);
                        }
                    }

                    if (skills.Count > 0)
                    {
                        ResumeRenderHelpers.AddSectionTitle(main, "Skills", typography, colors);
                        main.Item().Text(string.Join(" | ", skills)).FontSize(typography.BodySize);
                    }
                });
            });
        }).GeneratePdf();
    }
}
