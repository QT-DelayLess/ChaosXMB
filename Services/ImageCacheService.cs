using System.IO;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ChaosXMB.Services;

/// <summary>Bounded RAM images, persistent URL file cache. All decoding off UI thread; frozen bitmaps.</summary>
public static class ImageCacheService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(18) };
    private static readonly string CacheDir = Path.Combine(LibraryStore.Root, "artwork");
    private static readonly ConcurrentDictionary<string, Lazy<Task<BitmapSource?>>> Pending = new();
    private static readonly Dictionary<string, BitmapSource> RamCache = new();
    private static readonly LinkedList<string> Lru = new();
    private static readonly object Sync = new();
    private const int MaximumRamEntries = 32;

    static ImageCacheService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("ChaosXMB/1.0 (personal desktop game launcher)");
    }

    public static async Task<BitmapSource?> LoadAsync(string? pathOrUrl, int maxWidth = 700)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl)) return null;
        maxWidth = Math.Clamp(maxWidth, 96, 2048);
        string key = pathOrUrl + "|" + maxWidth;
        lock (Sync)
        {
            if (RamCache.TryGetValue(key, out var cached))
            {
                Lru.Remove(key);
                Lru.AddLast(key);
                return cached;
            }
        }
        var lazy = Pending.GetOrAdd(key, _ => new Lazy<Task<BitmapSource?>>(() => LoadCoreAsync(pathOrUrl, maxWidth)));
        try
        {
            var bitmap = await lazy.Value;
            if (bitmap != null)
            {
                lock (Sync)
                {
                    if (!RamCache.ContainsKey(key))
                    {
                        RamCache[key] = bitmap;
                        Lru.AddLast(key);
                        while (Lru.Count > MaximumRamEntries)
                        {
                            var oldest = Lru.First!.Value;
                            Lru.RemoveFirst();
                            RamCache.Remove(oldest);
                        }
                    }
                }
            }
            return bitmap;
        }
        finally { Pending.TryRemove(key, out _); }
    }

    private static async Task<BitmapSource?> LoadCoreAsync(string pathOrUrl, int width)
    {
        try
        {
            byte[] data;
            if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http"))
            {
                Directory.CreateDirectory(CacheDir);
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pathOrUrl)));
                var path = Path.Combine(CacheDir, hash + ".img");
                if (File.Exists(path)) data = await File.ReadAllBytesAsync(path);
                else
                {
                    using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    var contentLength = response.Content.Headers.ContentLength;
                    if (contentLength > 15_000_000) return null;
                    await using var network = await response.Content.ReadAsStreamAsync();
                    using var memory = new MemoryStream();
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await network.ReadAsync(buffer)) > 0)
                    {
                        if (memory.Length + read > 15_000_000) return null;
                        memory.Write(buffer, 0, read);
                    }
                    data = memory.ToArray();
                    try { await File.WriteAllBytesAsync(path, data); } catch (IOException) { }
                }
            }
            else
            {
                if (!File.Exists(pathOrUrl)) return null;
                data = await File.ReadAllBytesAsync(pathOrUrl);
            }
            return await Task.Run(() =>
            {
                using var ms = new MemoryStream(data);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = width;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return (BitmapSource)image;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException or NotSupportedException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            return null;
        }
    }

    public static void Prefetch(string? url, int width = 700)
    {
        if (!string.IsNullOrWhiteSpace(url)) _ = LoadAsync(url, width);
    }
}
