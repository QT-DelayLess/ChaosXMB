using ChaosXMB.Models;
using ChaosXMB.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace ChaosXMB.Views;

public partial class LibraryManagerWindow : Window
{
    private readonly ObservableCollection<GameEntry> _games;
    private readonly LibraryStore _store;
    public LibraryManagerWindow(ObservableCollection<GameEntry> games, LibraryStore store)
    {
        InitializeComponent();
        _games = games; _store = store;
        GamesList.ItemsSource = _games;
    }
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddGameWindow { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;
        _games.Add(dialog.Result);
        if (!await SaveAsync()) { _games.Remove(dialog.Result); return; }
        GamesList.SelectedItem = dialog.Result;
    }
    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (GamesList.SelectedItem is not GameEntry chosen) return;
        var dialog = new AddGameWindow(chosen) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;
        int index = _games.IndexOf(chosen);
        _games[index] = dialog.Result;
        if (!await SaveAsync()) { _games[index] = chosen; return; }
        GamesList.SelectedIndex = index;
    }
    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (GamesList.SelectedItem is not GameEntry chosen) return;
        if (MessageBox.Show($"'{chosen.Title}' kütüphaneden kaldırılsın mı? Oyun dosyaları silinmez.", "ChaosXMB", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        int index = _games.IndexOf(chosen);
        _games.Remove(chosen);
        if (!await SaveAsync()) _games.Insert(Math.Max(0, index), chosen);
    }
    private async Task<bool> SaveAsync()
    {
        try { await _store.SaveAsync(_games); return true; }
        catch (Exception ex)
        {
            MessageBox.Show("Kütüphane kaydedilemedi: " + ex.Message + "\nDeğişiklik geri alındı.", "ChaosXMB", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
