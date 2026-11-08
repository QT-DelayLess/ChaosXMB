using System.Diagnostics;
using System.IO;
using ChaosXMB.Models;

namespace ChaosXMB.Services;

public sealed class GameLaunchService
{
    /// <summary>
    /// Native EXEs provide exact session timing. Launchers use their URI protocol,
    /// and are timed only when a matching game process can be located in its install folder.
    /// A URI launcher process exiting is never counted as playtime.
    /// </summary>
    public async Task LaunchAndTrackAsync(GameEntry game, Func<Task> saveAsync)
    {
        bool isStoreLaunch = !string.IsNullOrWhiteSpace(game.LaunchUri);
        if (!isStoreLaunch && !File.Exists(game.ExecutablePath))
            throw new FileNotFoundException("Oyun dosyası bulunamadı. Düzenle bölümünden yolu güncelle.", game.ExecutablePath);
        if (isStoreLaunch && !IsSupportedGameUri(game.LaunchUri))
            throw new InvalidOperationException("Güvenilmeyen başlatma adresi engellendi.");

        // Baseline must be captured BEFORE activating a store, otherwise fast-starting
        // games may be incorrectly treated as already-running and never timed.
        var runningBefore = isStoreLaunch && !string.IsNullOrWhiteSpace(game.InstallFolder) && Directory.Exists(game.InstallFolder)
            ? await Task.Run(() => FindGameProcesses(game.InstallFolder)) : new List<Process>();
        var existingPids = runningBefore.Select(p => p.Id).ToHashSet();
        foreach (var p in runningBefore) p.Dispose();
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = isStoreLaunch ? game.LaunchUri : game.ExecutablePath,
            Arguments = isStoreLaunch ? "" : game.Arguments,
            WorkingDirectory = isStoreLaunch ? Environment.CurrentDirectory : (Path.GetDirectoryName(game.ExecutablePath) ?? Environment.CurrentDirectory),
            UseShellExecute = true
        });
        if (process == null && !isStoreLaunch) throw new InvalidOperationException("Oyun başlatılamadı.");

        var started = DateTimeOffset.UtcNow;
        game.LastPlayedUtc = started;
        game.FirstPlayedUtc ??= started;
        game.LaunchCount++;
        await saveAsync();

        if (!isStoreLaunch)
        {
            await TrackProcessAsync(process!, game, started, saveAsync);
            return;
        }

        // Steam / Epic protocol activation returns the launcher process, not the game.
        // Wait for an executable under the known INSTALL DIRECTORY (never count Steam itself).
        if (string.IsNullOrWhiteSpace(game.InstallFolder) || !Directory.Exists(game.InstallFolder)) return;
        for (int attempt = 0; attempt < 18; attempt++)
        {
            await Task.Delay(1000);
            var processes = await Task.Run(() => FindGameProcesses(game.InstallFolder));
            var child = processes.FirstOrDefault(p => !existingPids.Contains(p.Id));
            foreach (var p in processes) if (p != child) p.Dispose();
            if (child == null) continue;
            using (child) { await TrackProcessAsync(child, game, DateTimeOffset.UtcNow, saveAsync); }
            return;
        }
    }

    private static bool IsSupportedGameUri(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) &&
        (parsed.Scheme.Equals("steam", StringComparison.OrdinalIgnoreCase) ||
         parsed.Scheme.Equals("com.epicgames.launcher", StringComparison.OrdinalIgnoreCase));

    private static List<Process> FindGameProcesses(string installFolder)
    {
        var found = new List<Process>();
        string directory = Path.GetFullPath(installFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = p.MainModule?.FileName;
                if (path != null && path.StartsWith(directory, StringComparison.OrdinalIgnoreCase) && !p.HasExited)
                { found.Add(p); continue; }
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException or IOException) { }
            p.Dispose();
        }
        return found;
    }

    private static async Task TrackProcessAsync(Process process, GameEntry game, DateTimeOffset started, Func<Task> saveAsync)
    {
        var watch = Stopwatch.StartNew();
        double credited = 0;
        try
        {
            // A single exit task, instead of accumulating abandoned wait tasks on each timeout.
            var exitTask = process.WaitForExitAsync();
            while (await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(25))) != exitTask)
            {
                double elapsed = watch.Elapsed.TotalSeconds;
                game.TotalPlaySeconds += Math.Max(0, elapsed - credited);
                credited = elapsed;
                await saveAsync(); // Periodic checkpoint: at most ~25 seconds lost on crash.
            }
            await exitTask;
        }
        catch (InvalidOperationException) { /* The process stopped before tracking could attach. */ }
        finally
        {
            watch.Stop();
            double total = watch.Elapsed.TotalSeconds;
            game.TotalPlaySeconds += Math.Max(0, total - credited);
            game.LastSessionSeconds = total;
            game.LongestSessionSeconds = Math.Max(game.LongestSessionSeconds, total);
            game.RecentSessions ??= new List<PlaySession>();
            game.RecentSessions.Insert(0, new PlaySession { StartedUtc = started, EndedUtc = DateTimeOffset.UtcNow, DurationSeconds = total });
            if (game.RecentSessions.Count > 15) game.RecentSessions.RemoveRange(15, game.RecentSessions.Count - 15);
            await saveAsync();
        }
    }
}
