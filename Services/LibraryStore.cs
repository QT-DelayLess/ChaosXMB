using System.IO;
using System.Text.Json;
using ChaosXMB.Models;

namespace ChaosXMB.Services;

/// <summary>
/// User library is stored independently of the program installation directory.
/// A completed write atomically replaces games.json and keeps the previous
/// good version in games.json.bak.
/// </summary>
public sealed class LibraryStore
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChaosXMB");
    public static readonly string DatabaseFile = Path.Combine(Root, "games.json");
    public static readonly string BackupFile = DatabaseFile + ".bak";
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    /// <summary>Non-null when library was restored from the last automatic backup.</summary>
    public string? RecoveryMessage { get; private set; }

    public async Task<List<GameEntry>> LoadAsync()
    {
        Directory.CreateDirectory(Root);
        RecoveryMessage = null;

        if (!File.Exists(DatabaseFile))
        {
            if (File.Exists(BackupFile))
            {
                var restored = await ReadFileAsync(BackupFile);
                File.Copy(BackupFile, DatabaseFile, overwrite: false);
                RecoveryMessage = "games.json bulunamadı. Otomatik yedekten geri yüklendi.";
                return restored;
            }

            // The save location exists even before the first game is added.
            var empty = new List<GameEntry>();
            await SaveAsync(empty);
            return empty;
        }

        try
        {
            return await ReadFileAsync(DatabaseFile);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            if (!File.Exists(BackupFile))
                throw new InvalidDataException($"Oyun kütüphanesi bozuk: {DatabaseFile}. Yedek bulunamadı; dosya değiştirilmedi.", ex);

            List<GameEntry> restored;
            try { restored = await ReadFileAsync(BackupFile); }
            catch (Exception backupEx) when (backupEx is JsonException or InvalidDataException or IOException)
            {
                throw new InvalidDataException($"Kayıt ve yedek okunamadı. Veriler korunuyor: {Root}", backupEx);
            }

            // Preserve the corrupt original for manual recovery; never erase it.
            string damagedPath = Path.Combine(Root, $"games.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");
            File.Move(DatabaseFile, damagedPath);
            File.Copy(BackupFile, DatabaseFile);
            RecoveryMessage = $"games.json bozuk olduğu için bir önceki yedek yüklendi. Bozuk dosya saklandı: {damagedPath}";
            return restored;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"Oyun kütüphanesi okunamadı: {DatabaseFile}\n{ex.Message}", ex);
        }
    }

    private static async Task<List<GameEntry>> ReadFileAsync(string path)
    {
        await using var file = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<GameEntry>>(file, JsonOptions)
               ?? throw new InvalidDataException($"Geçersiz oyun listesi: {path}");
    }

    public async Task SaveAsync(IEnumerable<GameEntry> games)
    {
        await _writeGate.WaitAsync();
        string temporary = DatabaseFile + ".tmp";
        try
        {
            Directory.CreateDirectory(Root);
            // Snapshot after taking the gate so concurrent saves cannot undo a newer write.
            var snapshot = games.ToList();
            await using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(file, snapshot, JsonOptions);
                await file.FlushAsync();
            }

            if (File.Exists(DatabaseFile))
                File.Replace(temporary, DatabaseFile, BackupFile);
            else
                File.Move(temporary, DatabaseFile);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try { File.Delete(temporary); } catch (IOException) { }
            }
            _writeGate.Release();
        }
    }
}
