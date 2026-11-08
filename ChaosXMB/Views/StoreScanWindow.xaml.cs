using ChaosXMB.Models;
using ChaosXMB.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ChaosXMB.Views;

public partial class StoreScanWindow : Window
{
    public sealed class Choice : INotifyPropertyChanged
    {
        public InstalledGamesDiscovery.FoundGame Found { get; init; } = null!;
        public string Name => Found.Name;
        public string Store => Found.Store;
        public string LocationLabel => Found.InstallFolder;
        public bool CanAdd { get; init; }
        public string StateLabel => CanAdd ? "Yeni" : "Kütüphanede";
        private bool _isChecked;
        public bool IsChecked { get => _isChecked; set { _isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly HashSet<string> _knownKeys;
    private readonly HashSet<string> _knownPaths;
    private readonly HashSet<int> _knownSteamIds;
    private readonly List<string> _knownInstallPaths;
    private readonly InstalledGamesDiscovery _discovery = new();
    private readonly ObservableCollection<Choice> _choices = new();
    private bool _running;
    public List<GameEntry> SelectedGames { get; } = new();

    public StoreScanWindow(IEnumerable<GameEntry> existing)
    {
        InitializeComponent();
        ResultsList.ItemsSource = _choices;
        _knownKeys = existing.Where(g => !string.IsNullOrWhiteSpace(g.StoreId))
            .Select(g => Key(g.SourceStore, g.StoreId)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _knownPaths = existing.Where(g => !string.IsNullOrWhiteSpace(g.ExecutablePath))
            .Select(g => g.ExecutablePath.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _knownSteamIds = existing.Where(g => g.SteamAppId.HasValue)
            .Select(g => g.SteamAppId!.Value).ToHashSet();
        _knownInstallPaths = existing.Where(g => !string.IsNullOrWhiteSpace(g.ExecutablePath))
            .Select(g => g.ExecutablePath).ToList();
    }

    private static string Key(string store, string id) => store + ":" + id;
    private async void Window_Loaded(object sender, RoutedEventArgs e) => await ScanAsync();
    private async void Rescan_Click(object sender, RoutedEventArgs e) => await ScanAsync();

    private async Task ScanAsync()
    {
        if (_running) return;
        _running = true;
        RescanButton.IsEnabled = ImportButton.IsEnabled = false;
        StatusText.Text = "Yerel mağaza kayıtları taranıyor...";
        try
        {
            var result = await Task.Run(_discovery.Scan);
            _choices.Clear();
            foreach (var game in result.Games)
            {
                bool present = _knownKeys.Contains(Key(game.Store, game.StoreId)) ||
                    (game.SteamAppId is int id && _knownSteamIds.Contains(id)) ||
                    (!string.IsNullOrWhiteSpace(game.ExecutablePath) && _knownPaths.Contains(game.ExecutablePath)) ||
                    (!string.IsNullOrWhiteSpace(game.InstallFolder) && _knownInstallPaths.Any(p =>
                        p.StartsWith(game.InstallFolder.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)));
                _choices.Add(new Choice { Found = game, CanAdd = !present, IsChecked = !present });
            }
            int newCount = _choices.Count(x => x.CanAdd);
            StatusText.Text = $"{_choices.Count} yüklü oyun bulundu · {newCount} yeni" +
                (result.Warnings.Count > 0 ? " · " + string.Join(" | ", result.Warnings.Take(2)) : "");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Tarama başarısız: " + ex.Message;
        }
        finally { _running = false; RescanButton.IsEnabled = ImportButton.IsEnabled = true; }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        SelectedGames.Clear();
        foreach (var choice in _choices.Where(x => x.CanAdd && x.IsChecked))
        {
            var f = choice.Found;
            SelectedGames.Add(new GameEntry
            {
                Title = f.Name, Category = "Games", SourceStore = f.Store, StoreId = f.StoreId,
                ExecutablePath = f.ExecutablePath, LaunchUri = f.LaunchUri,
                InstallFolder = f.InstallFolder, SteamAppId = f.SteamAppId,
                MetadataSource = f.Store + " (yerel tarama)"
            });
        }
        if (SelectedGames.Count == 0)
        {
            MessageBox.Show("Eklemek için yeni oyunlardan en az birini seç.", "ChaosXMB"); return;
        }
        DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
