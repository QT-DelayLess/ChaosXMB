using System.Text.Json.Serialization;

namespace ChaosXMB.Models;

public sealed class GameEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string LaunchUri { get; set; } = "";
    public string InstallFolder { get; set; } = "";
    public string SourceStore { get; set; } = "";
    public string StoreId { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string Category { get; set; } = "Games";
    public string Description { get; set; } = "";
    public string Developer { get; set; } = "";
    public string Genre { get; set; } = "";
    public string ReleaseDate { get; set; } = "";
    public string CoverUrl { get; set; } = "";
    public string HeroUrl { get; set; } = "";
    public string CoverFallbackUrl { get; set; } = "";
    public string MetadataSource { get; set; } = "Manual";
    public int? SteamAppId { get; set; }
    public bool IsFavorite { get; set; }
    public int LaunchCount { get; set; }
    public double LastSessionSeconds { get; set; }
    public double LongestSessionSeconds { get; set; }
    public DateTimeOffset? FirstPlayedUtc { get; set; }
    public List<PlaySession> RecentSessions { get; set; } = new();
    public double TotalPlaySeconds { get; set; }
    public DateTimeOffset? LastPlayedUtc { get; set; }
    [JsonIgnore] public string PlayTimeLabel => FormatDuration(TotalPlaySeconds);
    [JsonIgnore] public string LastSessionLabel => FormatDuration(LastSessionSeconds);
    [JsonIgnore] public string LongestSessionLabel => FormatDuration(LongestSessionSeconds);
    public static string FormatDuration(double seconds)
    {
        if (seconds < 60) return seconds <= 0 ? "Henüz yok" : "1 dk'dan az";
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours} sa {span.Minutes} dk" : $"{span.Minutes} dk";
    }
    [JsonIgnore] public string LastPlayedLabel => LastPlayedUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "Hiç";
}

public sealed class PlaySession
{
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset EndedUtc { get; set; }
    public double DurationSeconds { get; set; }
}
