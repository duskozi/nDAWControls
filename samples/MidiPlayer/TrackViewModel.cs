using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using nDAWControls.MidiPlayer.Engine;

namespace nDAWControls.MidiPlayer;

/// <summary>UI state of one track. Property changes are forwarded to the engine by the window.</summary>
public sealed class TrackViewModel : INotifyPropertyChanged
{
    private bool _isMuted;
    private bool _isSoloed;
    private bool _isShownOnPiano = true;
    private bool _isAudible = true;
    private bool _isSelected;
    private string _device;

    public TrackViewModel(SongTrack track, Brush color, string device)
    {
        Track = track;
        Color = color;
        _device = device;
    }

    public SongTrack Track { get; }
    public int Index => Track.Index;
    public int Number => Track.Index + 1;
    public string Name => Track.Name;
    public string Subtitle => Track.Subtitle;
    public Brush Color { get; }

    public bool IsMuted { get => _isMuted; set => Set(ref _isMuted, value); }
    public bool IsSoloed { get => _isSoloed; set => Set(ref _isSoloed, value); }
    public bool IsShownOnPiano { get => _isShownOnPiano; set => Set(ref _isShownOnPiano, value); }
    public bool IsAudible { get => _isAudible; set => Set(ref _isAudible, value); }
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public string Device { get => _device; set => Set(ref _device, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
