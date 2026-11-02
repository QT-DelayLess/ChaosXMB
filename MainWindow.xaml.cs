using ChaosXMB.Engine;
using ChaosXMB.Models;
using ChaosXMB.Services;
using ChaosXMB.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ChaosXMB;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly LibraryStore _store = new();
    private readonly GameLaunchService _launcher = new();
    private readonly SteamMetadataService _steam = new();
    private readonly XmbNavigator _navigator = new();
    private readonly XInputController _controller = new();
    private readonly Stopwatch _frameClock = Stopwatch.StartNew();
    private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly ObservableCollection<GameEntry> _games = new();
    public ObservableCollection<NavCategory> Categories { get; } = new();
    private ObservableCollection<NavItem> _visibleItems = new();
    public ObservableCollection<NavItem> VisibleItems { get => _visibleItems; private set { _visibleItems = value; Notify(); } }
    private GameEntry? _detailGame;
    private int _imageGeneration;
    private string _currentBackground = "";
    private bool _frontIsA;
    private double _alphaA, _alphaB;
    private double _waveTime, _waveBudget;
    private double _zoomA = 1.035, _zoomB = 1.035;
    private bool _initialized;
    private int _fpsFrames;
    private double _fpsSeconds;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        string[] names = { "Games", "Apps", "Favorites", "Recent", "Settings" };
        string[] symbols = { "▣", "◇", "☆", "◷", "⚙" };
        for (int i = 0; i < names.Length; i++) Categories.Add(new NavCategory { Title = names[i], Symbol = symbols[i], Left = i * 305 });
        _controller.Direction += key => { if (IsActive) Navigate(key); };
        _controller.Confirm += () => { if (IsActive) ActivateCurrent(); };
        _controller.Cancel += () => { if (IsActive) { if (DetailsOverlay.Visibility == Visibility.Visible) CloseDetails(); else Close(); } };
        _uiTimer.Tick += (_, _) =>
        {
            ClockText.Text = DateTime.Now.ToString("HH:mm");
            DateText.Text = DateTime.Now.ToString("dd.MM.yyyy");
            if (WindowState != WindowState.Minimized) _controller.Poll();
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            foreach (var game in await _store.LoadAsync()) _games.Add(game);
            if (_store.RecoveryMessage != null)
                MessageBox.Show(_store.RecoveryMessage, "ChaosXMB · Kayıt kurtarma", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + "\nVeri dosyan korunuyor; üzerinde değişiklik yapılmayacak.", "ChaosXMB · Kütüphane", MessageBoxButton.OK, MessageBoxImage.Warning);
            // Prevent silent overwrite of malformed/inaccessible library data.
            Close(); return;
        }
        SelectCategory(0);
        UpdateCounts();
        _uiTimer.Start();
        _frameClock.Restart();
        CompositionTarget.Rendering += RenderFrame;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _uiTimer.Stop();
        CompositionTarget.Rendering -= RenderFrame;
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized) { _frameClock.Restart(); return; }
        var frameSeconds = _frameClock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(frameSeconds, 0, 0.05);
        _frameClock.Restart();
        _fpsFrames++;
        _fpsSeconds += frameSeconds;
        if (_fpsSeconds >= 1.0)
        {
            if (FpsText.Visibility == Visibility.Visible) FpsText.Text = $"FPS {(_fpsFrames / _fpsSeconds):0}";
            _fpsFrames = 0; _fpsSeconds = 0;
        }
        _navigator.Tick(dt);
        CategoryTranslate.X = _navigator.HorizontalOffset;
        // Scroll the content INSIDE a stationary viewport. This keeps the
        // clipping region fixed even at the last game (no disappearing rows).
        double scrollTo = Math.Max(0, -_navigator.VerticalOffset);
        if (Math.Abs(ItemScrollHost.VerticalOffset - scrollTo) > 0.03)
            ItemScrollHost.ScrollToVerticalOffset(scrollTo);
        _waveBudget += dt;
        _waveTime += dt;
        if (_waveBudget >= 1.0 / 30.0)
        {
            _waveBudget = 0;
            WaveHost.Draw(_waveTime, _navigator.HorizontalOffset, ActualWidth, ActualHeight);
        }
        double blend = 1 - Math.Exp(-dt * 5.5);
        double targetA = _frontIsA ? 0.66 : 0.0;
        double targetB = _frontIsA ? 0.0 : 0.66;
        _alphaA += (targetA - _alphaA) * blend;
        _alphaB += (targetB - _alphaB) * blend;
        if (Math.Abs(_alphaA - targetA) < 0.001) _alphaA = targetA;
        if (Math.Abs(_alphaB - targetB) < 0.001) _alphaB = targetB;
        if (BgImageA.Opacity != _alphaA) BgImageA.Opacity = _alphaA;
        if (BgImageB.Opacity != _alphaB) BgImageB.Opacity = _alphaB;
        if (targetA > 0) _zoomA = Math.Min(1.08, _zoomA + dt * 0.003);
        if (targetB > 0) _zoomB = Math.Min(1.08, _zoomB + dt * 0.003);
        BgScaleA.ScaleX = BgScaleA.ScaleY = _zoomA;
        BgScaleB.ScaleX = BgScaleB.ScaleY = _zoomB;
        double parallax = _navigator.HorizontalOffset * 0.017;
        BgMoveA.X = parallax;
        BgMoveB.X = parallax;
    }

    private void SelectCategory(int index)
    {
        _navigator.SelectCategory(index);
        for (int i = 0; i < Categories.Count; i++) Categories[i].IsSelected = i == _navigator.CategoryIndex;
        RefreshItems();
    }

    private void RefreshItems()
    {
        var chosen = _navigator.CategoryIndex;
        IEnumerable<GameEntry> games = chosen switch
        {
            0 => _games.Where(g => g.Category != "Apps").OrderBy(g => g.Title),
            1 => _games.Where(g => g.Category == "Apps").OrderBy(g => g.Title),
            2 => _games.Where(g => g.IsFavorite).OrderBy(g => g.Title),
            3 => _games.Where(g => g.LastPlayedUtc != null).OrderByDescending(g => g.LastPlayedUtc),
            _ => Enumerable.Empty<GameEntry>()
        };
        var items = new ObservableCollection<NavItem>();
        if (chosen == 4)
        {
            items.Add(ActionItem(NavKind.AddGame, "+", "Oyun Ekle", "EXE seç ve internetten görsellerini al"));
            items.Add(ActionItem(NavKind.ScanStores, "⌕", "Oyunları Tara", "Steam / Epic / GOG · Seçerek içe aktar"));
            items.Add(ActionItem(NavKind.Library, "▤", "Kütüphaneyi Yönet", "Düzenle, sil ve oyun yollarını değiştir"));
            items.Add(ActionItem(NavKind.ReloadMetadata, "↻", "Steam Bilgilerini Yenile", "Bilinen oyunların eksik metadata bilgilerini tamamla"));
            items.Add(ActionItem(NavKind.OpenDataFolder, "▣", "Kayıt Klasörünü Aç", "Otomatik kaydedilen games.json ve games.json.bak yedeğini görüntüle"));
            items.Add(ActionItem(NavKind.Exit, "⏻", "Çıkış", "Windows masaüstüne dön"));
        }
        else
        {
            foreach (var game in games)
                items.Add(new NavItem
                {
                    Kind = NavKind.Game, Game = game, Title = game.Title,
                    Subtitle = game.Genre.Length > 0 ? game.Genre : (game.Category == "Apps" ? "Uygulama" : "Oyun"), Symbol = "▣"
                });
            if (items.Count == 0)
                items.Add(new NavItem { Title = "Henüz burada oyun yok", Subtitle = "Settings → Oyun Ekle", Symbol = "◇" });
        }
        for (int i = 0; i < items.Count; i++) items[i].Top = i * 94;
        VisibleItems = items;
        _navigator.SelectItem(0, VisibleItems.Count);
        FocusCurrent();
    }

    private static NavItem ActionItem(NavKind kind, string symbol, string title, string subtitle) =>
        new() { Kind = kind, Symbol = symbol, Title = title, Subtitle = subtitle };

    private void FocusCurrent()
    {
        for (int i = 0; i < VisibleItems.Count; i++) VisibleItems[i].IsSelected = i == _navigator.ItemIndex;
        var selected = SelectedItem();
        SummaryTitle.Text = selected?.Title ?? "ChaosXMB";
        SummaryMeta.Text = selected?.Game is { } game
            ? string.Join("   ·   ", new[] { game.Genre, game.ReleaseDate }.Where(s => !string.IsNullOrWhiteSpace(s)))
            : Categories[_navigator.CategoryIndex].Title;
        SummaryDescription.Text = selected?.Game is { } g
            ? (string.IsNullOrWhiteSpace(g.Description) ? "Detay ekranına gitmek için Enter'a bas." : g.Description)
            : (selected?.Subtitle ?? "Ayarlar menüsünden oyun ekle.");
        if (selected?.Game is { } focusedGame)
        {
            _ = LoadBackgroundAsync(focusedGame.HeroUrl, focusedGame.CoverUrl);
            LoadNearbyCovers();
        }
        else _ = LoadBackgroundAsync("");
    }

    private NavItem? SelectedItem() => VisibleItems.Count == 0 ? null : VisibleItems[Math.Clamp(_navigator.ItemIndex, 0, VisibleItems.Count - 1)];

    private void LoadNearbyCovers()
    {
        int start = Math.Max(0, _navigator.ItemIndex - 3);
        int end = Math.Min(VisibleItems.Count, _navigator.ItemIndex + 5);
        for (int i = start; i < end; i++)
            if (VisibleItems[i].Game != null && VisibleItems[i].Thumbnail == null) _ = LoadThumbnailAsync(VisibleItems[i]);
    }

    private static async Task<BitmapSource?> LoadCoverAsync(GameEntry game, int width)
    {
        var art = await ImageCacheService.LoadAsync(game.CoverUrl, width);
        if (art == null) art = await ImageCacheService.LoadAsync(game.CoverFallbackUrl, width);
        if (art == null && game.SteamAppId is int steamId)
            art = await ImageCacheService.LoadAsync($"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{steamId}/library_600x900.jpg", width);
        if (art == null) art = await ImageCacheService.LoadAsync(game.HeroUrl, width);
        return art;
    }

    private async Task LoadThumbnailAsync(NavItem item)
    {
        var game = item.Game;
        if (game == null) return;
        var artwork = await LoadCoverAsync(game, 220);
        if (VisibleItems.Contains(item)) item.Thumbnail = artwork;
    }

    private async Task LoadBackgroundAsync(string url, string fallback = "")
    {
        string cacheKey = url + "|" + fallback;
        if (cacheKey == _currentBackground) return;
        _currentBackground = cacheKey;
        int version = ++_imageGeneration;
        if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(fallback))
        {
            // Both layers fade out naturally; no stale wallpaper in Settings.
            _frontIsA = ! _frontIsA;
            BgImageA.Source = null; BgImageB.Source = null;
            _alphaA = 0; _alphaB = 0;
            return;
        }
        var image = await ImageCacheService.LoadAsync(url, 1920);
        if (image == null) image = await ImageCacheService.LoadAsync(fallback, 1920);
        if (version != _imageGeneration) return;
        if (image == null) { _currentBackground = ""; return; }
        if (_frontIsA)
        {
            BgImageB.Source = image;
            _zoomB = 1.035;
            _frontIsA = false;
        }
        else
        {
            BgImageA.Source = image;
            _zoomA = 1.035;
            _frontIsA = true;
        }
    }

    private void Navigate(Key key)
    {
        if (DetailsOverlay.Visibility == Visibility.Visible)
        {
            if (key == Key.Left || key == Key.Right)
            {
                var direction = key == Key.Right ? FocusNavigationDirection.Next : FocusNavigationDirection.Previous;
                (Keyboard.FocusedElement as UIElement ?? PlayButton).MoveFocus(new TraversalRequest(direction));
            }
            return;
        }
        switch (key)
        {
            case Key.Left: SelectCategory(_navigator.CategoryIndex - 1); break;
            case Key.Right: SelectCategory(_navigator.CategoryIndex + 1); break;
            case Key.Up: _navigator.SelectItem(_navigator.ItemIndex - 1, VisibleItems.Count); FocusCurrent(); break;
            case Key.Down: _navigator.SelectItem(_navigator.ItemIndex + 1, VisibleItems.Count); FocusCurrent(); break;
        }
    }

    // The wheel must change focused item, not scroll the viewport independently;
    // otherwise wheel position and selection fall out of sync.
    private void ItemScrollHost_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DetailsOverlay.Visibility != Visibility.Visible)
            Navigate(e.Delta > 0 ? Key.Up : Key.Down);
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F3) { FpsText.Visibility = FpsText.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; e.Handled = true; return; }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (DetailsOverlay.Visibility == Visibility.Visible) CloseDetails(); else Close();
            return;
        }
        if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            if (DetailsOverlay.Visibility == Visibility.Visible) return; // Buttons retain normal keyboard focus.
            e.Handled = true; Navigate(e.Key);
        }
        else if (e.Key == Key.Enter && DetailsOverlay.Visibility != Visibility.Visible)
        {
            e.Handled = true; ActivateCurrent();
        }
    }

    private void Category_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is NavCategory category)
        {
            SelectCategory(Categories.IndexOf(category)); e.Handled = true;
        }
    }
    // Deliberately no MouseEnter selection: WPF can raise this event when the
    // animated item rail moves under a stationary mouse pointer. That created
    // a feedback loop which made the selection drift down the screen.
    // Mouse clicks still select/open items; arrow keys and gamepad remain usable.

    private void NavItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not NavItem item) return;
        int index = VisibleItems.IndexOf(item);
        if (index < 0) return;
        _navigator.SelectItem(index, VisibleItems.Count);
        FocusCurrent();
        ActivateCurrent(); // Single mouse click opens details or activates Settings.
        e.Handled = true;
    }

    private void ActivateCurrent()
    {
        if (DetailsOverlay.Visibility == Visibility.Visible)
        {
            if (Keyboard.FocusedElement is Button focused)
                focused.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else PlayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return;
        }
        var item = SelectedItem();
        if (item == null) return;
        if (item.Game != null) { _ = OpenDetailsAsync(item.Game); return; }
        switch (item.Kind)
        {
            case NavKind.AddGame: _ = AddGameAsync(); break;
            case NavKind.ScanStores: _ = ScanStoresAsync(); break;
            case NavKind.Library: OpenManager(); break;
            case NavKind.ReloadMetadata: _ = UpdateMetadataAsync(); break;
            case NavKind.OpenDataFolder: OpenDataFolder(); break;
            case NavKind.Exit: Close(); break;
        }
    }

    private static void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(LibraryStore.Root);
            Process.Start(new ProcessStartInfo { FileName = LibraryStore.Root, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kayıt klasörü açılamadı: {ex.Message}\nKonum: {LibraryStore.Root}", "ChaosXMB", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task AddGameAsync()
    {
        var dialog = new AddGameWindow { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;
        _games.Add(dialog.Result);
        try { await _store.SaveAsync(_games); }
        catch (Exception ex) { _games.Remove(dialog.Result); MessageBox.Show("Oyun kaydedilemedi: " + ex.Message); return; }
        UpdateCounts();
        SelectCategory(dialog.Result.Category == "Apps" ? 1 : 0);
        int index = VisibleItems.ToList().FindIndex(x => x.Game?.Id == dialog.Result.Id);
        if (index >= 0) { _navigator.SelectItem(index, VisibleItems.Count); FocusCurrent(); }
    }

    private void OpenManager()
    {
        new LibraryManagerWindow(_games, _store) { Owner = this }.ShowDialog();
        UpdateCounts();
        RefreshItems();
    }

    private async Task UpdateMetadataAsync()
    {
        var candidates = _games.Where(g => g.SteamAppId != null).ToList();
        if (candidates.Count == 0) { MessageBox.Show("Steam ile eşleştirilmiş oyun yok. Bir oyun eklerken Steam sonuçlarından seç."); return; }
        int updated = 0;
        foreach (var game in candidates)
        {
            try
            {
                var info = await _steam.FetchAsync(game.SteamAppId!.Value);
                if (info == null) continue;
                if (string.IsNullOrWhiteSpace(game.Description)) game.Description = info.Description;
                if (string.IsNullOrWhiteSpace(game.Developer)) game.Developer = info.Developer;
                if (string.IsNullOrWhiteSpace(game.Genre)) game.Genre = info.Genre;
                if (string.IsNullOrWhiteSpace(game.CoverUrl)) game.CoverUrl = info.CoverUrl;
                if (string.IsNullOrWhiteSpace(game.CoverFallbackUrl)) game.CoverFallbackUrl = info.CoverFallbackUrl;
                if (string.IsNullOrWhiteSpace(game.HeroUrl)) game.HeroUrl = info.HeroUrl;
                updated++;
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { }
        }
        try { await _store.SaveAsync(_games); } catch (Exception ex) { MessageBox.Show(ex.Message); }
        RefreshItems();
        MessageBox.Show($"{updated}/{candidates.Count} oyun için Steam bilgileri kontrol edildi.", "ChaosXMB");
    }

    private async Task OpenDetailsAsync(GameEntry game)
    {
        _detailGame = game;
        DetailsTitle.Text = game.Title;
        DetailsGenre.Text = string.Join("   ·   ", new[] { game.Genre, game.Category }.Where(s => !string.IsNullOrWhiteSpace(s)));
        DetailsDescription.Text = string.IsNullOrWhiteSpace(game.Description) ? "Henüz oyun açıklaması bulunamadı. Düzenle seçeneğinden ekleyebilirsin." : game.Description;
        DetailsDeveloper.Text = string.IsNullOrWhiteSpace(game.Developer) ? "Bilinmiyor" : game.Developer;
        DetailsRelease.Text = string.IsNullOrWhiteSpace(game.ReleaseDate) ? "—" : game.ReleaseDate;
        DetailsPlayTime.Text = game.PlayTimeLabel;
        DetailsLastPlayed.Text = "Son oynanma: " + game.LastPlayedLabel + $"   ·   {game.LaunchCount} kez açıldı";
        DetailsSessionStats.Text = $"Son oturum: {game.LastSessionLabel}    ·    En uzun oturum: {game.LongestSessionLabel}" +
            (game.LaunchUri.Length > 0 ? "    ·    Mağaza oyunlarında süre, işlem tespitine bağlıdır." : "");
        FavoriteButton.Content = game.IsFavorite ? "★  FAVORİDEN ÇIKAR" : "☆  FAVORİ";
        DetailsCover.Source = null; DetailsHero.Source = null;
        DetailsOverlay.Visibility = Visibility.Visible;
        DetailsOverlay.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)));
        PlayButton.Focus();
        var current = game;
        var art = await LoadCoverAsync(game, 850);
        if (_detailGame != current || DetailsOverlay.Visibility != Visibility.Visible) return;
        DetailsCover.Source = art;
        var hero = await ImageCacheService.LoadAsync(game.HeroUrl, 1920);
        if (_detailGame == current && DetailsOverlay.Visibility == Visibility.Visible) DetailsHero.Source = hero;
    }

    private void CloseDetails()
    {
        _detailGame = null;
        DetailsOverlay.BeginAnimation(OpacityProperty, null);
        DetailsOverlay.Opacity = 0;
        DetailsOverlay.Visibility = Visibility.Collapsed;
        Focus();
    }
    private void Back_Click(object sender, RoutedEventArgs e) => CloseDetails();

    private async void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (_detailGame == null) return;
        var game = _detailGame;
        game.IsFavorite = !game.IsFavorite;
        FavoriteButton.Content = game.IsFavorite ? "★  FAVORİDEN ÇIKAR" : "☆  FAVORİ";
        try { await _store.SaveAsync(_games); }
        catch (Exception ex)
        {
            game.IsFavorite = !game.IsFavorite;
            if (_detailGame == game) FavoriteButton.Content = game.IsFavorite ? "★  FAVORİDEN ÇIKAR" : "☆  FAVORİ";
            MessageBox.Show("Favori kaydedilemedi: " + ex.Message);
            return;
        }
        if (_navigator.CategoryIndex == 2) RefreshItems();
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_detailGame == null) return;
        var old = _detailGame;
        var editor = new AddGameWindow(old) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result == null) return;
        var index = _games.IndexOf(old);
        if (index < 0) return;
        _games[index] = editor.Result;
        try { await _store.SaveAsync(_games); }
        catch (Exception ex) { _games[index] = old; MessageBox.Show("Düzenleme kaydedilemedi: " + ex.Message); return; }
        CloseDetails(); RefreshItems();
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_detailGame == null) return;
        var old = _detailGame;
        if (MessageBox.Show($"{old.Title} kütüphaneden kaldırılsın mı? Dosyalar silinmeyecek.", "ChaosXMB", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _games.Remove(old);
        try { await _store.SaveAsync(_games); }
        catch (Exception ex) { _games.Add(old); MessageBox.Show("Silme işlemi kaydedilemedi: " + ex.Message); return; }
        CloseDetails(); RefreshItems(); UpdateCounts();
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_detailGame == null) return;
        var game = _detailGame;
        CloseDetails();
        WindowState = WindowState.Minimized;
        try
        {
            await _launcher.LaunchAndTrackAsync(game, () => _store.SaveAsync(_games));
        }
        catch (Exception ex)
        {
            WindowState = WindowState.Maximized;
            MessageBox.Show("Başlatma hatası: " + ex.Message, "ChaosXMB", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Maximized;
            UpdateCounts();
            RefreshItems();
        }
    }

    private void UpdateCounts() => LibraryCount.Text = $"{_games.Count} UYGULAMA / OYUN";

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Give the list an explicit viewport height. A huge fixed-height ItemsControl
        // inside a stretched/clipped Grid previously hid almost every game row.
        if (ItemsViewport != null)
        {
            double viewportHeight = Math.Max(120, Root.ActualHeight - 289 - 92);
            ItemsViewport.Height = viewportHeight;
            if (ListTailPadding != null)
                ListTailPadding.Height = Math.Max(0, viewportHeight - 94);
        }
        // Keep smaller monitors uncluttered rather than allowing preview to overlap the rail.
        if (SidePreview != null)
            SidePreview.Visibility = Root.ActualWidth < 1320 ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task ScanStoresAsync()
    {
        var dialog = new StoreScanWindow(_games) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedGames.Count == 0) return;
        var imported = dialog.SelectedGames;
        foreach (var item in imported) _games.Add(item);
        try { await _store.SaveAsync(_games); }
        catch (Exception ex)
        {
            foreach (var item in imported) _games.Remove(item);
            MessageBox.Show("Oyunlar kaydedilemedi: " + ex.Message, "ChaosXMB", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        UpdateCounts();
        SelectCategory(0);
        int selected = VisibleItems.ToList().FindIndex(x => x.Game?.Id == imported[0].Id);
        if (selected >= 0) { _navigator.SelectItem(selected, VisibleItems.Count); FocusCurrent(); }
        // Background enrichment is intentionally optional and NEVER blocks library import.
        _ = EnrichDiscoveredGamesAsync(imported);
        MessageBox.Show($"{imported.Count} oyun kütüphaneye eklendi. İnternet erişimi varsa kapak ve bilgiler arka planda tamamlanacak.",
            "ChaosXMB", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task EnrichDiscoveredGamesAsync(List<GameEntry> imported)
    {
        // Sequential requests avoid Steam API overload and allow UI to stay responsive.
        foreach (var game in imported)
        {
            if (!_games.Contains(game)) continue;
            try
            {
                int? steamId = game.SteamAppId;
                if (steamId == null)
                {
                    var candidates = await _steam.SearchAsync(game.Title);
                    // Only exact matches; fuzzy matching may silently assign the wrong artwork.
                    steamId = candidates.FirstOrDefault(x =>
                        string.Equals(x.Name.Trim(), game.Title.Trim(), StringComparison.OrdinalIgnoreCase))?.AppId;
                }
                if (steamId == null) continue;
                var info = await _steam.FetchAsync(steamId.Value);
                if (info == null || !_games.Contains(game)) continue;
                if (string.IsNullOrWhiteSpace(game.Description)) game.Description = info.Description;
                if (string.IsNullOrWhiteSpace(game.Developer)) game.Developer = info.Developer;
                if (string.IsNullOrWhiteSpace(game.Genre)) game.Genre = info.Genre;
                if (string.IsNullOrWhiteSpace(game.ReleaseDate)) game.ReleaseDate = info.ReleaseDate;
                if (string.IsNullOrWhiteSpace(game.CoverUrl)) game.CoverUrl = info.CoverUrl;
                if (string.IsNullOrWhiteSpace(game.CoverFallbackUrl)) game.CoverFallbackUrl = info.CoverFallbackUrl;
                if (string.IsNullOrWhiteSpace(game.HeroUrl)) game.HeroUrl = info.HeroUrl;
                game.SteamAppId ??= steamId;
                game.MetadataSource = "Steam Store";
                await _store.SaveAsync(_games);
                if (DetailsOverlay.Visibility != Visibility.Visible)
                    RefreshItemsKeepingSelection();
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException) { }
        }
    }

    private void RefreshItemsKeepingSelection()
    {
        var oldId = SelectedItem()?.Game?.Id;
        RefreshItems();
        if (oldId is Guid id)
        {
            var index = VisibleItems.ToList().FindIndex(x => x.Game?.Id == id);
            if (index >= 0) { _navigator.SelectItem(index, VisibleItems.Count); FocusCurrent(); }
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
