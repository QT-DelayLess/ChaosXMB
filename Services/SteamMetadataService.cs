using System.Net.Http;
using System.Text.Json;

namespace ChaosXMB.Services;

public sealed class SteamSearchHit
{
    public int AppId { get; set; }
    public string Name { get; set; } = "";
    public string PreviewUrl { get; set; } = "";
    public override string ToString() => $"{Name}  ·  Steam #{AppId}";
}

public sealed class SteamGameMetadata
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Developer { get; set; } = "";
    public string Genre { get; set; } = "";
    public string ReleaseDate { get; set; } = "";
    public string CoverUrl { get; set; } = "";
    public string CoverFallbackUrl { get; set; } = "";
    public string HeroUrl { get; set; } = "";
    public int AppId { get; set; }
}

/// <summary>Public Steam storefront endpoints; these may be rate-limited or unavailable in some regions.</summary>
public sealed class SteamMetadataService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(16) };
    static SteamMetadataService() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 ChaosXMB/1.0");

    public async Task<List<SteamSearchHit>> SearchAsync(string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return new();
        string url = "https://store.steampowered.com/api/storesearch/?term=" + Uri.EscapeDataString(title.Trim()) + "&l=english&cc=us";
        using var response = await Http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;
        var results = new List<SteamSearchHit>();
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return results;
        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var id)) continue;
            results.Add(new SteamSearchHit
            {
                AppId = id.GetInt32(), Name = GetString(item, "name"), PreviewUrl = GetString(item, "tiny_image")
            });
        }
        return results;
    }

    public async Task<SteamGameMetadata?> FetchAsync(int appId, CancellationToken ct = default)
    {
        string url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=english&cc=us";
        using var response = await Http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!document.RootElement.TryGetProperty(appId.ToString(), out var app) ||
            !app.TryGetProperty("success", out var success) || !success.GetBoolean() ||
            !app.TryGetProperty("data", out var data)) return null;

        var metadata = new SteamGameMetadata
        {
            AppId = appId,
            Name = GetString(data, "name"),
            Description = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(GetString(data, "short_description"), "<[^>]*>", "")),
            HeroUrl = GetString(data, "background_raw"),
            CoverUrl = $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appId}/library_600x900.jpg",
            CoverFallbackUrl = GetString(data, "capsule_imagev5"),
            ReleaseDate = data.TryGetProperty("release_date", out var release) ? GetString(release, "date") : ""
        };
        if (data.TryGetProperty("developers", out var developers) && developers.ValueKind == JsonValueKind.Array)
            metadata.Developer = string.Join(", ", developers.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)));
        if (data.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array)
            metadata.Genre = string.Join(", ", genres.EnumerateArray().Select(x => GetString(x, "description")).Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(metadata.HeroUrl)) metadata.HeroUrl = GetString(data, "background");
        if (string.IsNullOrWhiteSpace(metadata.CoverFallbackUrl)) metadata.CoverFallbackUrl = GetString(data, "header_image");
        return metadata;
    }

    private static string GetString(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "";
}
