using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ResumeAI.ATSScore.API.Services.TemplateRendering;

public static class ResumeRenderHelpers
{
    public static string BuildContactLine(ResumeBuilderGeneratedProfileDto profile)
    {
        var parts = new List<string?>
        {
            profile.Email,
            profile.Phone,
            profile.Location,
            profile.LinkedInUrl,
            profile.PortfolioUrl
        };

        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part))!);
    }

    public static List<ResumeBuilderGeneratedExperienceItemDto> ClampExperience(
        IEnumerable<ResumeBuilderGeneratedExperienceItemDto> items,
        LimitSpec limits)
    {
        return items
            .Take(Math.Max(1, limits.MaxExperienceItems))
            .ToList();
    }

    public static List<ResumeBuilderGeneratedProjectItemDto> ClampProjects(
        IEnumerable<ResumeBuilderGeneratedProjectItemDto> items,
        LimitSpec limits)
    {
        return items
            .Take(Math.Max(1, limits.MaxProjectItems))
            .ToList();
    }

    public static List<string> ClampSkills(IEnumerable<string> skills, int max = 14)
    {
        return skills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Take(Math.Max(4, max))
            .ToList();
    }

    public static TextStyle BaseStyle(TypographySpec typography, ColorSpec colors)
    {
        return TextStyle.Default.FontFamily(typography.FontFamily).FontSize(typography.BodySize).FontColor(colors.Secondary);
    }

    public static void AddSectionTitle(ColumnDescriptor column, string title, TypographySpec typography, ColorSpec colors)
    {
        column.Item().PaddingTop(4).PaddingBottom(2).Text(title)
            .FontSize(typography.SectionTitleSize)
            .SemiBold()
            .FontColor(colors.Primary);
    }

    public static string BuildEducationLine(ResumeBuilderGeneratedEducationItemDto education)
    {
        var parts = new List<string?>
        {
            education.FieldOfStudy,
            education.StartYear,
            education.IsPresent ? "Present" : education.EndYear,
            education.Marks
        };

        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part))!);
    }

    public static PageSize ResolvePageSize(string? size)
    {
        return string.Equals(size, "Letter", StringComparison.OrdinalIgnoreCase)
            ? PageSizes.Letter
            : PageSizes.A4;
    }
}
