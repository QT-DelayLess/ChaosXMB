using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ChaosXMB.Models;

public enum NavKind { Game, AddGame, ScanStores, Library, ReloadMetadata, OpenDataFolder, Exit }

public sealed class NavItem : INotifyPropertyChanged
{
    public NavKind Kind { get; init; }
    public GameEntry? Game { get; init; }
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Symbol { get; init; } = "◇";
    public double Top { get; set; }
    private ImageSource? _thumbnail;
    public ImageSource? Thumbnail { get => _thumbnail; set { _thumbnail = value; Changed(); Changed(nameof(HasThumbnail)); } }
    public bool HasThumbnail => Thumbnail != null;
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class NavCategory : INotifyPropertyChanged
{
    public string Title { get; init; } = "";
    public string Symbol { get; init; } = "";
    public double Left { get; init; }
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
