using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SubtitleOverlay.Commands;
using SubtitleOverlay.Models;
using SubtitleOverlay.Services;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace SubtitleOverlay.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AudioPlayerService _audio;
    private readonly SubtitleService _subtitles;
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _timer;
    private string _audioName = "Aucun audio";
    private string _subtitleName = "Aucun SRT";
    private string _currentSubtitle = "";
    private long _position;
    private long _duration;
    private bool _isPlaying;
    private bool _overlayVisible = true;
    private bool _isSeeking;
    private int _syncCandidateIndex = -1;
    private double? _syncAnchorAudioMs;
    private double? _syncAnchorSubtitleMs;
    private string _syncStatus = "Choisissez la phrase entendue pour effectuer un recalage précis.";

    public AppSettings Settings { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<bool>? OverlayVisibilityRequested;
    public event EventHandler<string>? OverlayPositionRequested;
    public event EventHandler? ExitRequested;

    public ICommand OpenAudioCommand { get; }
    public ICommand OpenSrtCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand BackwardCommand { get; }
    public ICommand ForwardCommand { get; }
    public ICommand ToggleOverlayCommand { get; }
    public ICommand ToggleClickThroughCommand { get; }
    public ICommand OffsetMinusCommand { get; }
    public ICommand OffsetPlusCommand { get; }
    public ICommand OffsetResetCommand { get; }
    public ICommand ResyncCommand { get; }
    public ICommand PreviousSyncCandidateCommand { get; }
    public ICommand NextSyncCandidateCommand { get; }
    public ICommand CorrectDriftCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand PositionOverlayCommand { get; }

    public MainViewModel(AudioPlayerService audio, SubtitleService subtitles, SettingsService settingsService, AppSettings settings)
    {
        _audio = audio;
        _subtitles = subtitles;
        _settingsService = settingsService;
        Settings = settings;
        if (!double.IsFinite(Settings.SubtitleRate) || Settings.SubtitleRate is < 0.9 or > 1.1)
            Settings.SubtitleRate = 1;
        _audio.Volume = settings.Volume;
        OpenAudioCommand = new RelayCommand(async _ => await OpenAudioAsync());
        OpenSrtCommand = new RelayCommand(async _ => await OpenSrtAsync());
        PlayPauseCommand = new RelayCommand(_ => TogglePlayPause());
        StopCommand = new RelayCommand(_ => Stop());
        BackwardCommand = new RelayCommand(_ => SeekBy(-10_000));
        ForwardCommand = new RelayCommand(_ => SeekBy(10_000));
        ToggleOverlayCommand = new RelayCommand(_ => ToggleOverlay());
        ToggleClickThroughCommand = new RelayCommand(_ => ClickThrough = !ClickThrough);
        OffsetMinusCommand = new RelayCommand(_ => SubtitleOffsetMs -= 500);
        OffsetPlusCommand = new RelayCommand(_ => SubtitleOffsetMs += 500);
        OffsetResetCommand = new RelayCommand(_ => ResetSynchronization());
        ResyncCommand = new RelayCommand(_ => SetSyncAnchor());
        PreviousSyncCandidateCommand = new RelayCommand(_ => SelectSyncCandidate(-1));
        NextSyncCandidateCommand = new RelayCommand(_ => SelectSyncCandidate(1));
        CorrectDriftCommand = new RelayCommand(_ => CorrectSynchronizationDrift());
        ExitCommand = new RelayCommand(_ => ExitRequested?.Invoke(this, EventArgs.Empty));
        PositionOverlayCommand = new RelayCommand(position =>
            OverlayPositionRequested?.Invoke(this, position?.ToString() ?? "Bas"));
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(75), DispatcherPriority.Background, OnTick, Dispatcher.CurrentDispatcher);
        _timer.Start();
    }

    public string AudioName { get => _audioName; private set => Set(ref _audioName, value); }
    public string SubtitleName { get => _subtitleName; private set => Set(ref _subtitleName, value); }
    public string CurrentSubtitle { get => _currentSubtitle; private set => Set(ref _currentSubtitle, value); }
    public long Position
    {
        get => _position;
        set
        {
            Set(ref _position, value);
            OnPropertyChanged(nameof(CurrentTime));
        }
    }
    public long Duration { get => _duration; private set { if (Set(ref _duration, value)) OnPropertyChanged(nameof(TotalTime)); } }
    public bool IsPlaying { get => _isPlaying; private set { if (Set(ref _isPlaying, value)) OnPropertyChanged(nameof(PlayPauseLabel)); } }
    public string PlayPauseLabel => IsPlaying ? "Pause" : "Lecture";
    public string CurrentTime => FormatTime(Position);
    public string TotalTime => FormatTime(Duration);
    public bool IsSeeking { get => _isSeeking; set => _isSeeking = value; }
    public int Volume
    {
        get => Settings.Volume;
        set { Settings.Volume = value; _audio.Volume = value; OnPropertyChanged(); }
    }
    public int SubtitleOffsetMs
    {
        get => Settings.SubtitleOffsetMs;
        set { Settings.SubtitleOffsetMs = Math.Clamp(value, -3_600_000, 3_600_000); OnPropertyChanged(); OnPropertyChanged(nameof(OffsetDisplay)); }
    }
    public string OffsetDisplay => $"{(SubtitleOffsetMs >= 0 ? "+" : "")}{SubtitleOffsetMs} ms";
    public double SubtitleRate
    {
        get => Settings.SubtitleRate;
        private set
        {
            Settings.SubtitleRate = Math.Clamp(value, 0.9, 1.1);
            OnPropertyChanged();
            OnPropertyChanged(nameof(DriftDisplay));
        }
    }
    public string DriftDisplay
    {
        get
        {
            var millisecondsPerMinute = (SubtitleRate - 1) * 60_000;
            return $"{millisecondsPerMinute:+0.0;-0.0;0.0} ms/min";
        }
    }
    public string SyncCandidateText =>
        _subtitles.GetEntry(_syncCandidateIndex)?.Text ?? "Aucune phrase SRT sélectionnée";
    public string SyncCandidateTime =>
        _subtitles.GetEntry(_syncCandidateIndex)?.Start.ToString(@"hh\:mm\:ss\,fff") ?? "--:--:--,---";
    public string SyncStatus { get => _syncStatus; private set => Set(ref _syncStatus, value); }
    public bool ClickThrough
    {
        get => Settings.ClickThrough;
        set { Settings.ClickThrough = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClickThroughLabel)); }
    }
    public string ClickThroughLabel => ClickThrough ? "Désactiver clic à travers" : "Activer clic à travers";
    public bool OverlayVisible { get => _overlayVisible; private set => Set(ref _overlayVisible, value); }
    public double FontSize { get => Settings.FontSize; set { Settings.FontSize = value; OnPropertyChanged(); } }
    public string FontFamily { get => Settings.FontFamily; set { Settings.FontFamily = value; OnPropertyChanged(); } }
    public double BackgroundOpacity { get => Settings.BackgroundOpacity; set { Settings.BackgroundOpacity = value; OnPropertyChanged(); } }
    public double TextOpacity { get => Settings.TextOpacity; set { Settings.TextOpacity = value; OnPropertyChanged(); } }
    public bool TextShadow { get => Settings.TextShadow; set { Settings.TextShadow = value; OnPropertyChanged(); } }
    public bool Borderless { get => Settings.Borderless; set { Settings.Borderless = value; OnPropertyChanged(); } }
    public IEnumerable<MediaFontFamily> SystemFontFamilies => Fonts.SystemFontFamilies.OrderBy(font => font.Source);
    public string TextColor
    {
        get => Settings.TextColor;
        set { Settings.TextColor = value; OnPropertyChanged(); OnPropertyChanged(nameof(TextBrush)); }
    }
    public string BackgroundColor
    {
        get => Settings.BackgroundColor;
        set { Settings.BackgroundColor = value; OnPropertyChanged(); OnPropertyChanged(nameof(BackgroundBrush)); }
    }
    public MediaBrush TextBrush => ParseBrush(Settings.TextColor, MediaBrushes.White);
    public MediaBrush BackgroundBrush => ParseBrush(Settings.BackgroundColor, MediaBrushes.Black);

    private async Task OpenAudioAsync()
    {
        var dialog = new WpfOpenFileDialog
        {
            Title = "Ouvrir un fichier audio",
            Filter = "Fichiers audio|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus|Tous les fichiers|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _audio.Open(dialog.FileName);
            AudioName = Path.GetFileName(dialog.FileName);
            _audio.Play();
            var automaticSrt = Path.ChangeExtension(dialog.FileName, ".srt");
            if (File.Exists(automaticSrt)) await LoadSrtAsync(automaticSrt);
        }
        catch (Exception ex) { ShowError("Impossible d’ouvrir l’audio", ex); }
    }

    private async Task OpenSrtAsync()
    {
        var dialog = new WpfOpenFileDialog { Title = "Ouvrir un fichier SRT", Filter = "Sous-titres SRT|*.srt" };
        if (dialog.ShowDialog() == true)
            try { await LoadSrtAsync(dialog.FileName); }
            catch (Exception ex) { ShowError("Impossible de charger le fichier SRT", ex); }
    }

    private async Task LoadSrtAsync(string path)
    {
        await _subtitles.LoadAsync(path);
        SubtitleName = $"{Path.GetFileName(path)} ({_subtitles.Count} entrées)";
        _syncCandidateIndex = -1;
        PrepareSyncCandidate();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_isSeeking) Position = _audio.Time;
        Duration = _audio.Length;
        IsPlaying = _audio.IsPlaying;
        if (IsPlaying) PrepareSyncCandidate();
        var lookup = TimeSpan.FromMilliseconds(MapAudioToSubtitleMilliseconds(Position));
        CurrentSubtitle = _subtitles.FindAt(lookup);
    }

    public void TogglePlayPause()
    {
        if (_audio.IsPlaying) _audio.Pause(); else _audio.Resume();
    }
    private void Stop() { _audio.Stop(); Position = 0; CurrentSubtitle = ""; }
    private void SeekBy(long value)
    {
        _audio.SeekBy(value);
        _subtitles.ResetSearch();
        _syncCandidateIndex = -1;
    }
    public void BeginSeek() => IsSeeking = true;
    public void EndSeek()
    {
        if (!IsSeeking) return;
        IsSeeking = false;
        _audio.Time = Position;
        _subtitles.ResetSearch();
        _syncCandidateIndex = -1;
        RefreshSubtitle();
    }
    private void RefreshSubtitle()
    {
        var lookup = TimeSpan.FromMilliseconds(MapAudioToSubtitleMilliseconds(_audio.Time));
        CurrentSubtitle = _subtitles.FindAt(lookup);
    }

    private double MapAudioToSubtitleMilliseconds(double audioMilliseconds) =>
        Math.Max(0, audioMilliseconds * SubtitleRate + SubtitleOffsetMs);

    private void PrepareSyncCandidate()
    {
        var candidateIndex = _subtitles.FindNearestIndex(
            TimeSpan.FromMilliseconds(MapAudioToSubtitleMilliseconds(_audio.Time)));
        if (candidateIndex != _syncCandidateIndex)
        {
            _syncCandidateIndex = candidateIndex;
            NotifySyncCandidateChanged();
        }
    }

    private void SelectSyncCandidate(int direction)
    {
        if (_subtitles.Count == 0)
        {
            SyncStatus = "Chargez d’abord un fichier SRT.";
            return;
        }
        if (_syncCandidateIndex < 0) PrepareSyncCandidate();
        _syncCandidateIndex = Math.Clamp(_syncCandidateIndex + direction, 0, _subtitles.Count - 1);
        NotifySyncCandidateChanged();
    }

    private void NotifySyncCandidateChanged()
    {
        OnPropertyChanged(nameof(SyncCandidateText));
        OnPropertyChanged(nameof(SyncCandidateTime));
    }

    private SubtitleEntry? GetCalibrationEntry()
    {
        if (_syncCandidateIndex < 0) PrepareSyncCandidate();
        return _subtitles.GetEntry(_syncCandidateIndex);
    }

    private void SetSyncAnchor()
    {
        var entry = GetCalibrationEntry();
        if (entry is null)
        {
            WpfMessageBox.Show(
                "Aucune phrase SRT n’est disponible. Chargez un fichier SRT avant de synchroniser.",
                "Resynchronisation", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_audio.IsPlaying) _audio.Pause();
        var audioTime = _audio.Time;
        var subtitleTime = entry.Start.TotalMilliseconds;
        _syncAnchorAudioMs = audioTime;
        _syncAnchorSubtitleMs = subtitleTime;
        SubtitleOffsetMs = (int)Math.Round(subtitleTime - SubtitleRate * audioTime);
        _subtitles.ResetSearch();
        RefreshSubtitle();
        SyncStatus = $"Point A enregistré à {FormatTime(audioTime)}. Recommencez plus loin avec Point B pour corriger la dérive.";
    }

    private void CorrectSynchronizationDrift()
    {
        if (_syncAnchorAudioMs is null || _syncAnchorSubtitleMs is null)
        {
            SyncStatus = "Définissez d’abord le Point A au début d’une phrase.";
            return;
        }

        var entry = GetCalibrationEntry();
        if (entry is null) return;
        if (_audio.IsPlaying) _audio.Pause();
        var secondAudioTime = (double)_audio.Time;
        var secondSubtitleTime = entry.Start.TotalMilliseconds;
        var audioDistance = secondAudioTime - _syncAnchorAudioMs.Value;
        if (Math.Abs(audioDistance) < 60_000)
        {
            SyncStatus = "Le Point B doit être situé au moins une minute après le Point A.";
            return;
        }

        var rate = (secondSubtitleTime - _syncAnchorSubtitleMs.Value) / audioDistance;
        if (!double.IsFinite(rate) || rate is < 0.95 or > 1.05)
        {
            SyncStatus = "Calibration refusée : les deux phrases choisies ne semblent pas correspondre.";
            return;
        }

        var offset = _syncAnchorSubtitleMs.Value - rate * _syncAnchorAudioMs.Value;
        SubtitleRate = rate;
        SubtitleOffsetMs = (int)Math.Round(offset);
        _syncAnchorAudioMs = null;
        _syncAnchorSubtitleMs = null;
        _subtitles.ResetSearch();
        RefreshSubtitle();
        SyncStatus = $"Dérive corrigée : {DriftDisplay}. Les deux points sont maintenant alignés.";
    }

    private void ResetSynchronization()
    {
        SubtitleRate = 1;
        SubtitleOffsetMs = 0;
        _syncAnchorAudioMs = null;
        _syncAnchorSubtitleMs = null;
        _subtitles.ResetSearch();
        _syncCandidateIndex = -1;
        RefreshSubtitle();
        SyncStatus = "Synchronisation remise à zéro.";
    }
    public void ToggleOverlay()
    {
        OverlayVisible = !OverlayVisible;
        OverlayVisibilityRequested?.Invoke(this, OverlayVisible);
    }
    public void ExecuteHotkey(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.PlayPause: TogglePlayPause(); break;
            case HotkeyAction.Backward: SeekBy(-10_000); break;
            case HotkeyAction.Forward: SeekBy(10_000); break;
            case HotkeyAction.OffsetForward: SubtitleOffsetMs += 500; break;
            case HotkeyAction.OffsetBackward: SubtitleOffsetMs -= 500; break;
            case HotkeyAction.ClickThrough: ClickThrough = !ClickThrough; break;
            case HotkeyAction.ToggleOverlay: ToggleOverlay(); break;
            case HotkeyAction.Resync: SetSyncAnchor(); break;
        }
    }

    public Task SaveSettingsAsync() => _settingsService.SaveAsync(Settings);
    private static string FormatTime(long milliseconds) => TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).ToString(@"hh\:mm\:ss");
    private static MediaBrush ParseBrush(string value, MediaBrush fallback)
    {
        try { return (MediaBrush)new BrushConverter().ConvertFromString(value)!; } catch { return fallback; }
    }
    private static void ShowError(string title, Exception ex) =>
        WpfMessageBox.Show($"{title}.\n\n{ex.Message}", "Subtitle Overlay", MessageBoxButton.OK, MessageBoxImage.Error);
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name); return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
