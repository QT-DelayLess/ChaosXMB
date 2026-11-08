using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChaosXMB.Services;

/// <summary>
/// Reads installed launcher's LOCAL manifests/registry. No account login, web scraping,
/// unbounded drive search, or changes to the original launcher installs.
/// </summary>
public sealed class InstalledGamesDiscovery
{
    public sealed class FoundGame
    {
        public string Name { get; init; } = "";
        public string Store { get; init; } = "";
        public string StoreId { get; init; } = "";
        public string InstallFolder { get; init; } = "";
        public string ExecutablePath { get; init; } = "";
        public string LaunchUri { get; init; } = "";
        public int? SteamAppId { get; init; }
    }

    public sealed class ScanResult
    {
        public List<FoundGame> Games { get; } = new();
        public List<string> Warnings { get; } = new();
    }

    public ScanResult Scan()
    {
        var result = new ScanResult();
        TryScan("Steam", () => ScanSteam(result.Games), result);
        TryScan("Epic Games", () => ScanEpic(result.Games), result);
        TryScan("GOG", () => ScanGog(result.Games), result);
        var deduped = result.Games
            .GroupBy(x => (x.Store + "|" + x.StoreId).ToUpperInvariant())
            .Select(x => x.First()).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        result.Games.Clear(); result.Games.AddRange(deduped);
        return result;
    }

    private static void TryScan(string source, Action scan, ScanResult result)
    {
        try { scan(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or System.Security.SecurityException)
        { result.Warnings.Add(source + " taranamadı: " + ex.Message); }
    }

    private static string Field(string source, string field)
    {
        // Steam KeyValues stores strings as quoted key/value pairs.
        var match = Regex.Match(source, "\"" + Regex.Escape(field) + "\"\\s*\"(?<v>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["v"].Value.Replace("\\\\", "\\").Replace("\\\"", "\"") : "";
    }

    private static void ScanSteam(List<FoundGame> found)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var configured = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        AddDirectory(roots, configured?.GetValue("SteamPath")?.ToString());
        using (var steamLocal = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
            AddDirectory(roots, steamLocal?.GetValue("InstallPath")?.ToString());
        AddDirectory(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        foreach (var root in roots.ToArray())
        {
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) vdf = Path.Combine(root, "config", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            var text = File.ReadAllText(vdf);
            foreach (Match match in Regex.Matches(text, "\"path\"\\s*\"(?<v>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase))
                AddDirectory(roots, match.Groups["v"].Value.Replace("\\\\", "\\"));
            // Older Steam libraryfolders format: "1" "D:\\SteamLibrary"
            foreach (Match match in Regex.Matches(text, "(?m)^\\s*\"[0-9]+\"\\s*\"(?<v>(?:\\\\.|[^\"])*)\""))
                AddDirectory(roots, match.Groups["v"].Value.Replace("\\\\", "\\"));
        }
        foreach (var root in roots)
        {
            var steamApps = Path.Combine(root, "steamapps");
            if (!Directory.Exists(steamApps)) continue;
            IEnumerable<string> manifests;
            try { manifests = Directory.EnumerateFiles(steamApps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly).ToList(); }
            catch (IOException) { continue; }
            foreach (var file in manifests)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    var idText = Field(content, "appid");
                    if (!int.TryParse(idText, out var id) || id <= 0) continue;
                    var title = Field(content, "name");
                    var folderName = Field(content, "installdir");
                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(folderName)) continue;
                    var install = Path.GetFullPath(Path.Combine(steamApps, "common", folderName));
                    if (!Directory.Exists(install)) continue;
                    found.Add(new FoundGame { Name = title, Store = "Steam", StoreId = idText,
                        SteamAppId = id, InstallFolder = install, LaunchUri = $"steam://rungameid/{id}" });
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
            }
        }
    }

    private static void AddDirectory(HashSet<string> result, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { var full = Path.GetFullPath(path.Trim().Replace('/', '\\')); if (Directory.Exists(full)) result.Add(full); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
    }

    private static void ScanEpic(List<FoundGame> found)
    {
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(baseDir)) return;
        foreach (var path in Directory.EnumerateFiles(baseDir, "*.item", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var data = doc.RootElement;
                if (data.TryGetProperty("bIsIncompleteInstall", out var incomplete) && incomplete.ValueKind == JsonValueKind.True) continue;
                string appName = Text(data, "AppName");
                string title = Text(data, "DisplayName");
                string install = Text(data, "InstallLocation");
                string relativeExe = Text(data, "LaunchExecutable").Replace('/', '\\').TrimStart('\\');
                if (string.IsNullOrWhiteSpace(appName) || string.IsNullOrWhiteSpace(title) || !Directory.Exists(install)) continue;
                string executable = "";
                if (!string.IsNullOrWhiteSpace(relativeExe))
                {
                    var target = Path.GetFullPath(Path.Combine(install, relativeExe));
                    if (File.Exists(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) executable = target;
                }
                found.Add(new FoundGame { Name = title, Store = "Epic", StoreId = appName, InstallFolder = install,
                    ExecutablePath = executable,
                    LaunchUri = $"com.epicgames.launcher://apps/{Uri.EscapeDataString(appName)}?action=launch&silent=true" });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException) { }
        }
    }

    private static string Text(JsonElement element, string key) =>
        element.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() ?? "" : "";

    private static void ScanGog(List<FoundGame> found)
    {
        // GOG installers normally create Windows registry game entries. GOG Galaxy's
        // SQLite database is not touched; games lacking registry entries require manual add.
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var games = baseKey.OpenSubKey(@"SOFTWARE\GOG.com\Games");
            if (games == null) continue;
            foreach (string id in games.GetSubKeyNames())
            {
                using var game = games.OpenSubKey(id);
                if (game == null) continue;
                string title = game.GetValue("gameName")?.ToString() ?? "";
                string install = game.GetValue("path")?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(title) || !Directory.Exists(install)) continue;
                string exe = game.GetValue("exe")?.ToString() ?? "";
                if (!Path.IsPathRooted(exe)) exe = Path.Combine(install, exe);
                if (!File.Exists(exe) || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    // Registry entry exists but EXE is missing: don't create an unusable game.
                    continue;
                }
                found.Add(new FoundGame { Name = title, Store = "GOG", StoreId = id, InstallFolder = install, ExecutablePath = exe });
            }
        }
    }
}
