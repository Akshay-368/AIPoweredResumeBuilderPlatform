using QuestPDF.Fluent;
using ResumeAI.ATSScore.API.Persistence;

namespace ResumeAI.ATSScore.API.Services.TemplateRendering;

public class SimpleHipsterResumeRenderer : IResumeTemplateRenderer
{
    public string TemplateId => "simple-hipster";

    public byte[] Render(ResumeBuilderGeneratedResumeDto resume, ResumeBuilderTemplateEntity template, ResumeTemplateRenderContext context)
    {
        var contract = context.Contract;
        var typography = contract.Typography;
        var colors = contract.Colors;
        var limits = contract.Limits;

        var experience = ResumeRenderHelpers.ClampExperience(resume.Experience, limits);
        var projects = ResumeRenderHelpers.ClampProjects(resume.Projects, limits);
        var skills = ResumeRenderHelpers.ClampSkills(resume.Skills, 14);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(ResumeRenderHelpers.ResolvePageSize(contract.Page.Size));
                page.Margin(contract.Page.Margin);
                page.DefaultTextStyle(ResumeRenderHelpers.BaseStyle(typography, colors));

                page.Content().Column(main =>
                {
                    main.Spacing(9);

                    main.Item().PaddingBottom(2).Column(header =>
                    {
                        header.Item().Text(resume.Profile.FullName).FontSize(typography.NameSize).SemiBold().FontColor(colors.Primary);
                        header.Item().Text(string.IsNullOrWhiteSpace(resume.Profile.ProfessionalRole) ? resume.TargetRole : resume.Profile.ProfessionalRole)
                            .FontSize(typography.RoleSize)
                            .FontColor(colors.Muted);

                        header.Item().Row(contact =>
                        {
                            contact.Spacing(10);
                            AddContactChip(contact, context.AssetSvgByKey, "email", resume.Profile.Email, typography, colors);
                            AddContactChip(contact, context.AssetSvgByKey, "phone", resume.Profile.Phone, typography, colors);
                            AddContactChip(contact, context.AssetSvgByKey, "linkedin", resume.Profile.LinkedInUrl, typography, colors);
                            AddContactChip(contact, context.AssetSvgByKey, "github", resume.Profile.PortfolioUrl, typography, colors);
                        });
                    });

                    ResumeRenderHelpers.AddSectionTitle(main, "About", typography, colors);
                    main.Item().Text(resume.Summary ?? string.Empty).FontSize(typography.BodySize);

                    if (experience.Count > 0)
                    {
                        ResumeRenderHelpers.AddSectionTitle(main, "Experience", typography, colors);
                        foreach (var item in experience)
                        {
                            main.Item().Text($"{item.Role} @ {item.Company}").SemiBold().FontSize(typography.BodySize);
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
                            main.Item().Text(project.Name).SemiBold().FontSize(typography.BodySize);
                            main.Item().Text(project.TechStack).FontSize(typography.SmallTextSize).FontColor(colors.Muted);
                            if (!string.IsNullOrWhiteSpace(project.Description))
                            {
                                main.Item().Text(project.Description).FontSize(typography.BodySize);
                            }
                        }
                    }

                    main.Item().Row(bottom =>
                    {
                        bottom.Spacing(12);

                        bottom.RelativeItem().Column(col =>
                        {
                            if (resume.Education.Count > 0)
                            {
                                ResumeRenderHelpers.AddSectionTitle(col, "Education", typography, colors);
                                foreach (var education in resume.Education.Take(2))
                                {
                                    col.Item().Text($"{education.Degree} - {education.Institution}").SemiBold().FontSize(typography.BodySize);
                                    col.Item().Text(ResumeRenderHelpers.BuildEducationLine(education)).FontSize(typography.SmallTextSize).FontColor(colors.Muted);
                                }
                            }
                        });

                        bottom.RelativeItem().Column(col =>
                        {
                            if (skills.Count > 0)
                            {
                                ResumeRenderHelpers.AddSectionTitle(col, "Skills", typography, colors);
                                foreach (var skill in skills)
                                {
                                    col.Item().Text($"• {skill}").FontSize(typography.BodySize);
                                }
                            }
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void AddContactChip(
        RowDescriptor row,
        IReadOnlyDictionary<string, string> icons,
        string key,
        string? value,
        TypographySpec typography,
        ColorSpec colors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        row.AutoItem().Row(chip =>
        {
            chip.Spacing(3);

            if (icons.TryGetValue(key, out var svg) && !string.IsNullOrWhiteSpace(svg))
            {
                chip.ConstantItem(10).Height(10).Svg(svg);
            }

            chip.AutoItem().Text(value).FontSize(typography.SmallTextSize).FontColor(colors.Muted);
        });
    }
}
