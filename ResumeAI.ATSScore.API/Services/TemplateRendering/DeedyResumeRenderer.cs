using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ResumeAI.ATSScore.API.Persistence;

namespace ResumeAI.ATSScore.API.Services.TemplateRendering;

public class DeedyResumeRenderer : IResumeTemplateRenderer
{
    public string TemplateId => "deedy-one-page-two-column";

    public byte[] Render(ResumeBuilderGeneratedResumeDto resume, ResumeBuilderTemplateEntity template, ResumeTemplateRenderContext context)
    {
        var contract = context.Contract;
        var typography = contract.Typography;
        var colors = contract.Colors;
        var limits = contract.Limits;

        var experience = ResumeRenderHelpers.ClampExperience(resume.Experience, limits);
        var projects = ResumeRenderHelpers.ClampProjects(resume.Projects, limits);
        var skills = ResumeRenderHelpers.ClampSkills(resume.Skills, 12);

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

                    main.Item().Column(header =>
                    {
                        header.Item().Text(resume.Profile.FullName).FontSize(typography.NameSize).SemiBold().FontColor(colors.Primary);
                        header.Item().Text(string.IsNullOrWhiteSpace(resume.Profile.ProfessionalRole) ? resume.TargetRole : resume.Profile.ProfessionalRole)
                            .FontSize(typography.RoleSize)
                            .FontColor(colors.Muted);
                        header.Item().Text(ResumeRenderHelpers.BuildContactLine(resume.Profile)).FontSize(typography.SmallTextSize).FontColor(colors.Muted);
                    });

                    main.Item().Row(row =>
                    {
                        row.Spacing(contract.Columns.Gap);

                        row.RelativeItem(contract.Columns.LeftRatio).Column(left =>
                        {
                            ResumeRenderHelpers.AddSectionTitle(left, "Summary", typography, colors);
                            left.Item().Text(resume.Summary ?? string.Empty).FontSize(typography.BodySize);

                            if (skills.Count > 0)
                            {
                                ResumeRenderHelpers.AddSectionTitle(left, "Skills", typography, colors);
                                foreach (var skill in skills)
                                {
                                    left.Item().Text($"• {skill}").FontSize(typography.BodySize);
                                }
                            }

                            if (resume.Education.Count > 0)
                            {
                                ResumeRenderHelpers.AddSectionTitle(left, "Education", typography, colors);
                                foreach (var education in resume.Education.Take(3))
                                {
                                    left.Item().Text($"{education.Degree} - {education.Institution}").SemiBold().FontSize(typography.BodySize);
                                    left.Item().Text(ResumeRenderHelpers.BuildEducationLine(education)).FontSize(typography.SmallTextSize).FontColor(colors.Muted);
                                }
                            }
                        });

                        row.RelativeItem(contract.Columns.RightRatio).Column(right =>
                        {
                            if (experience.Count > 0)
                            {
                                ResumeRenderHelpers.AddSectionTitle(right, "Experience", typography, colors);
                                foreach (var item in experience)
                                {
                                    right.Item().Text($"{item.Role} - {item.Company}").SemiBold().FontSize(typography.BodySize);
                                    var period = string.Join(" - ", new[] { item.StartDate, item.IsPresent ? "Present" : item.EndDate }.Where(part => !string.IsNullOrWhiteSpace(part)));
                                    if (!string.IsNullOrWhiteSpace(period))
                                    {
                                        right.Item().Text(period).FontSize(typography.SmallTextSize).FontColor(colors.Muted);
                                    }

                                    if (!string.IsNullOrWhiteSpace(item.Description))
                                    {
                                        right.Item().Text($"• {item.Description}").FontSize(typography.BodySize);
                                    }

                                    right.Item().PaddingBottom(2);
                                }
                            }

                            if (projects.Count > 0)
                            {
                                ResumeRenderHelpers.AddSectionTitle(right, "Projects", typography, colors);
                                foreach (var project in projects)
                                {
                                    right.Item().Text(project.Name).SemiBold().FontSize(typography.BodySize);
                                    if (!string.IsNullOrWhiteSpace(project.TechStack))
                                    {
                                        right.Item().Text(project.TechStack).FontSize(typography.SmallTextSize).FontColor(colors.Muted);
                                    }

                                    if (!string.IsNullOrWhiteSpace(project.Description))
                                    {
                                        right.Item().Text(project.Description).FontSize(typography.BodySize);
                                    }

                                    right.Item().PaddingBottom(2);
                                }
                            }
                        });
                    });
                });
            });
        }).GeneratePdf();
    }
}
