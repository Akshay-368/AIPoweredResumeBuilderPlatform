using Microsoft.EntityFrameworkCore;
using ResumeAI.ATSScore.API.Data;

namespace ResumeAI.ATSScore.API.Services.TemplateRendering;

public class ResumeTemplateAssetResolver
{
    private readonly ProjectsDbContext _db;

    public ResumeTemplateAssetResolver(ProjectsDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<string, string>> GetSvgAssetsAsync(string templateId, CancellationToken cancellationToken)
    {
        var assets = await _db.ResumeTemplateAssets
            .Where(asset => asset.TemplateId == templateId && asset.IsActive)
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in assets)
        {
            if (string.IsNullOrWhiteSpace(asset.AssetKey) || string.IsNullOrWhiteSpace(asset.Base64Data))
            {
                continue;
            }

            try
            {
                var bytes = Convert.FromBase64String(asset.Base64Data);
                var svg = System.Text.Encoding.UTF8.GetString(bytes);
                map[asset.AssetKey] = svg;
            }
            catch
            {
                // Ignore malformed asset and continue rendering.
            }
        }

        return map;
    }
}
