using System.Text.Json;

namespace ResumeAI.ATSScore.API.Services.TemplateRendering;

public sealed record ResumeTemplateRenderContract
{
    public string LayoutType { get; init; } = "single_column";
    public PageSpec Page { get; init; } = new();
    public ColumnSpec Columns { get; init; } = new();
    public TypographySpec Typography { get; init; } = new();
    public ColorSpec Colors { get; init; } = new();
    public SectionOrderSpec SectionOrder { get; init; } = new();
    public LimitSpec Limits { get; init; } = new();
    public AssetSpec Assets { get; init; } = new();

    public static ResumeTemplateRenderContract ParseOrDefault(string? rawJson, string templateId)
    {
        if (!string.IsNullOrWhiteSpace(rawJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<ResumeTemplateRenderContract>(rawJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed is not null)
                {
                    return parsed.WithSafeDefaults();
                }
            }
            catch
            {
                // Fallback to template defaults.
            }
        }

        return BuildTemplateDefault(templateId);
    }

    public ResumeTemplateRenderContract WithSafeDefaults()
    {
        var left = Columns.LeftRatio <= 0 ? 0.34f : Columns.LeftRatio;
        var right = Columns.RightRatio <= 0 ? 0.66f : Columns.RightRatio;

        return this with
        {
            Page = Page with
            {
                Margin = Page.Margin <= 0 ? 24 : Page.Margin
            },
            Columns = Columns with
            {
                LeftRatio = left,
                RightRatio = right,
                Gap = Columns.Gap < 8 ? 8 : Columns.Gap
            },
            Typography = Typography with
            {
                FontFamily = string.IsNullOrWhiteSpace(Typography.FontFamily) ? "Arial" : Typography.FontFamily,
                NameSize = Typography.NameSize <= 0 ? 20 : Typography.NameSize,
                RoleSize = Typography.RoleSize <= 0 ? 10 : Typography.RoleSize,
                SectionTitleSize = Typography.SectionTitleSize <= 0 ? 11 : Typography.SectionTitleSize,
                BodySize = Typography.BodySize <= 0 ? 9 : Typography.BodySize,
                SmallTextSize = Typography.SmallTextSize <= 0 ? 8 : Typography.SmallTextSize
            },
            Colors = Colors with
            {
                Primary = string.IsNullOrWhiteSpace(Colors.Primary) ? "#0f766e" : Colors.Primary,
                Secondary = string.IsNullOrWhiteSpace(Colors.Secondary) ? "#111827" : Colors.Secondary,
                Muted = string.IsNullOrWhiteSpace(Colors.Muted) ? "#4b5563" : Colors.Muted
            },
            Limits = Limits with
            {
                MaxPages = Limits.MaxPages <= 0 ? 1 : Limits.MaxPages,
                MaxBulletsPerJob = Limits.MaxBulletsPerJob <= 0 ? 4 : Limits.MaxBulletsPerJob,
                MaxExperienceItems = Limits.MaxExperienceItems <= 0 ? 3 : Limits.MaxExperienceItems,
                MaxProjectItems = Limits.MaxProjectItems <= 0 ? 3 : Limits.MaxProjectItems
            }
        };
    }

    public static ResumeTemplateRenderContract BuildTemplateDefault(string templateId)
    {
        return templateId switch
        {
            "deedy-one-page-two-column" => new ResumeTemplateRenderContract
            {
                LayoutType = "two_column",
                SectionOrder = new SectionOrderSpec
                {
                    Left = ["summary", "skills", "education"],
                    Right = ["experience", "projects"]
                }
            },
            "simple-hipster" => new ResumeTemplateRenderContract
            {
                LayoutType = "hipster",
                Colors = new ColorSpec("#155e75", "#0f172a", "#475569"),
                Assets = new AssetSpec(["phone", "email", "linkedin", "github"])
            },
            _ => new ResumeTemplateRenderContract
            {
                LayoutType = "single_column",
                Colors = new ColorSpec("#0f172a", "#1f2937", "#6b7280")
            }
        };
    }
}

public sealed record PageSpec(string Size = "A4", float Margin = 24);

public sealed record ColumnSpec(float LeftRatio = 0.34f, float RightRatio = 0.66f, float Gap = 16);

public sealed record TypographySpec(
    string FontFamily = "Arial",
    float NameSize = 20,
    float RoleSize = 10,
    float SectionTitleSize = 11,
    float BodySize = 9,
    float SmallTextSize = 8);

public sealed record ColorSpec(string Primary = "#0f766e", string Secondary = "#111827", string Muted = "#4b5563");

public sealed record SectionOrderSpec(
    List<string>? Left = null,
    List<string>? Right = null,
    List<string>? Main = null);

public sealed record LimitSpec(
    int MaxPages = 1,
    bool TruncateOverflow = true,
    int MaxBulletsPerJob = 4,
    int MaxExperienceItems = 3,
    int MaxProjectItems = 3);

public sealed record AssetSpec(List<string>? IconKeys = null);
