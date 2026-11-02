using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChaosXMB.Models;
using ChaosXMB.Services;

namespace ChaosXMB.Views;

public partial class AddGameWindow : Window
{
    private readonly SteamMetadataService _steam = new();
    private readonly GameEntry _working;
    private CancellationTokenSource? _searchToken;
    private int _previewVersion;
    public GameEntry? Result { get; private set; }

    public AddGameWindow(GameEntry? existing = null)
    {
        InitializeComponent();
        _working = existing == null ? new GameEntry() : new GameEntry
        {
            Id = existing.Id, Title = existing.Title, ExecutablePath = existing.ExecutablePath, Arguments = existing.Arguments,
            LaunchUri = existing.LaunchUri, InstallFolder = existing.InstallFolder, SourceStore = existing.SourceStore, StoreId = existing.StoreId,
            Category = existing.Category, Description = existing.Description, Developer = existing.Developer,
            Genre = existing.Genre, ReleaseDate = existing.ReleaseDate, CoverUrl = existing.CoverUrl,
            HeroUrl = existing.HeroUrl, CoverFallbackUrl = existing.CoverFallbackUrl, MetadataSource = existing.MetadataSource, SteamAppId = existing.SteamAppId,
            IsFavorite = existing.IsFavorite, LaunchCount = existing.LaunchCount,
            TotalPlaySeconds = existing.TotalPlaySeconds, LastPlayedUtc = existing.LastPlayedUtc,
            LastSessionSeconds = existing.LastSessionSeconds, LongestSessionSeconds = existing.LongestSessionSeconds,
            FirstPlayedUtc = existing.FirstPlayedUtc, RecentSessions = existing.RecentSessions ?? new()
        };
        ExePathBox.Text = _working.ExecutablePath;
        TitleBox.Text = _working.Title;
        SearchBox.Text = _working.Title;
        DeveloperBox.Text = _working.Developer;
        GenreBox.Text = _working.Genre;
        ReleaseBox.Text = _working.ReleaseDate;
        DescriptionBox.Text = _working.Description;
        CoverBox.Text = _working.CoverUrl;
        HeroBox.Text = _working.HeroUrl;
        ArgumentsBox.Text = _working.Arguments;
        CategoryChoice.SelectedIndex = _working.Category == "Apps" ? 1 : 0;
        Loaded += async (_, _) => await RefreshPreviewAsync();
        Closed += (_, _) => _searchToken?.Cancel();
    }

    private async void BrowseExecutable_Click(object sender, RoutedEventArgs e)
    {
        var chooser = new OpenFileDialog
        {
            Title = "Oyunun EXE veya kısayol dosyasını seç",
            Filter = "Programlar ve kısayollar|*.exe;*.lnk;*.bat;*.cmd;*.url|Tüm Dosyalar|*.*"
        };
        if (chooser.ShowDialog(this) != true) return;
        ExePathBox.Text = chooser.FileName;
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            var name = Path.GetFileNameWithoutExtension(chooser.FileName);
            foreach (var suffix in new[] { "-Win64-Shipping", "_Win64_Shipping", "_x64", "-Shipping" })
                name = name.Replace(suffix, "", StringComparison.OrdinalIgnoreCase);
            TitleBox.Text = name.Replace('_', ' ').Replace('-', ' ').Trim();
            SearchBox.Text = TitleBox.Text;
        }
        await SearchSteamAsync(autoMatch: true);
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchSteamAsync(false);
    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await SearchSteamAsync(false); }
    }

    private async Task SearchSteamAsync(bool autoMatch)
    {
        _searchToken?.Cancel();
        _searchToken?.Dispose();
        _searchToken = new CancellationTokenSource();
        var token = _searchToken.Token;
        MatchesList.ItemsSource = null;
        StatusLabel.Text = "Steam kataloğunda aranıyor…";
        try
        {
            var hits = await _steam.SearchAsync(SearchBox.Text, token);
            if (token.IsCancellationRequested) return;
            MatchesList.ItemsSource = hits;
            StatusLabel.Text = hits.Count == 0 ? "Eşleşme yok. Elle bilgi girebilir veya farklı isim arayabilirsin." : $"{hits.Count} sonuç bulundu; doğru oyunu seç.";
            if (autoMatch)
            {
                var exact = hits.FirstOrDefault(x => string.Equals(x.Name.Trim(), TitleBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (exact != null) MatchesList.SelectedItem = exact;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            StatusLabel.Text = "Steam şu an yanıt vermedi: " + ex.Message + ". Elle devam edebilirsin.";
        }
    }

    private async void MatchesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MatchesList.SelectedItem is not SteamSearchHit chosen) return;
        _searchToken?.Cancel();
        _searchToken?.Dispose();
        _searchToken = new CancellationTokenSource();
        var token = _searchToken.Token;
        StatusLabel.Text = $"{chosen.Name} için oyun bilgileri alınıyor…";
        try
        {
            var info = await _steam.FetchAsync(chosen.AppId, token);
            if (info == null || token.IsCancellationRequested) { StatusLabel.Text = "Detay alınamadı; elle doldurabilirsin."; return; }
            TitleBox.Text = info.Name;
            DescriptionBox.Text = info.Description;
            DeveloperBox.Text = info.Developer;
            GenreBox.Text = info.Genre;
            ReleaseBox.Text = info.ReleaseDate;
            CoverBox.Text = info.CoverUrl;
            _working.CoverFallbackUrl = info.CoverFallbackUrl;
            HeroBox.Text = info.HeroUrl;
            _working.SteamAppId = info.AppId;
            _working.MetadataSource = "Steam Store";
            StatusLabel.Text = "Oyun bilgileri alındı. Görselleri kontrol edip kaydet.";
            await RefreshPreviewAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { StatusLabel.Text = "Bilgiler alınamadı: " + ex.Message; }
    }

    private async void BrowseCover_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Resimler|*.png;*.jpg;*.jpeg;*.webp;*.bmp" };
        if (dialog.ShowDialog(this) == true) { CoverBox.Text = dialog.FileName; await RefreshPreviewAsync(); }
    }
    private async void BrowseHero_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Resimler|*.png;*.jpg;*.jpeg;*.webp;*.bmp" };
        if (dialog.ShowDialog(this) == true) { HeroBox.Text = dialog.FileName; await RefreshPreviewAsync(); }
    }
    private async void ImageText_LostFocus(object sender, RoutedEventArgs e) => await RefreshPreviewAsync();

    private async Task RefreshPreviewAsync()
    {
        var version = ++_previewVersion;
        var cover = await ImageCacheService.LoadAsync(CoverBox.Text, 450);
        if (cover == null) cover = await ImageCacheService.LoadAsync(_working.CoverFallbackUrl, 450);
        if (version != _previewVersion) return;
        CoverPreview.Source = cover;
        var hero = await ImageCacheService.LoadAsync(HeroBox.Text, 800);
        if (version != _previewVersion) return;
        HeroPreview.Source = hero;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var file = ExePathBox.Text.Trim();
        if (!File.Exists(file) && string.IsNullOrWhiteSpace(_working.LaunchUri)) { MessageBox.Show("Önce geçerli bir EXE/kısayol seç.", "ChaosXMB"); return; }
        if (string.IsNullOrWhiteSpace(TitleBox.Text)) { MessageBox.Show("Oyun adı boş olamaz.", "ChaosXMB"); return; }
        _working.Title = TitleBox.Text.Trim();
        _working.ExecutablePath = file;
        _working.Arguments = ArgumentsBox.Text.Trim();
        _working.Category = CategoryChoice.SelectedIndex == 1 ? "Apps" : "Games";
        _working.Developer = DeveloperBox.Text.Trim();
        _working.Genre = GenreBox.Text.Trim();
        _working.ReleaseDate = ReleaseBox.Text.Trim();
        _working.Description = DescriptionBox.Text.Trim();
        _working.CoverUrl = CoverBox.Text.Trim();
        _working.HeroUrl = HeroBox.Text.Trim();
        Result = _working;
        DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
