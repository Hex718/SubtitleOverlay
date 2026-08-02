using System.Windows;
using SubtitleOverlay.Services;
using SubtitleOverlay.ViewModels;
using SubtitleOverlay.Views;
using WpfMessageBox = System.Windows.MessageBox;

namespace SubtitleOverlay;

public partial class App : System.Windows.Application
{
    private AudioPlayerService? _audio;
    private GlobalHotkeyService? _hotkeys;
    private TrayIconService? _tray;
    private MainViewModel? _viewModel;
    private OverlayWindow? _overlay;
    private bool _reallyExit;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var settingsService = new SettingsService();
            var settings = await settingsService.LoadAsync();
            _audio = new AudioPlayerService();
            var subtitles = new SubtitleService();
            _viewModel = new MainViewModel(_audio, subtitles, settingsService, settings);
            _overlay = new OverlayWindow(_viewModel);
            var main = new MainWindow(_viewModel);
            MainWindow = main;

            _viewModel.OverlayVisibilityRequested += (_, visible) =>
            {
                if (visible) _overlay.Show(); else _overlay.Hide();
            };
            _viewModel.ExitRequested += (_, _) => ExitApplication();

            _overlay.Show();
            main.Show();

            _hotkeys = new GlobalHotkeyService(main);
            _hotkeys.HotkeyPressed += (_, action) => _viewModel.ExecuteHotkey(action);
            var failures = _hotkeys.RegisterDefaults();
            if (failures.Count > 0)
                WpfMessageBox.Show("Certains raccourcis sont déjà utilisés :\n" + string.Join("\n", failures),
                    "Raccourcis globaux", MessageBoxButton.OK, MessageBoxImage.Warning);

            _tray = new TrayIconService(
                () => Dispatcher.Invoke(() => { main.Show(); main.WindowState = WindowState.Normal; main.Activate(); }),
                () => Dispatcher.Invoke(_viewModel.ToggleOverlay),
                () => Dispatcher.Invoke(_viewModel.TogglePlayPause),
                () => Dispatcher.Invoke(ExitApplication));
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Impossible de démarrer Subtitle Overlay.\n\n{ex.Message}", "Erreur",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private async void ExitApplication()
    {
        if (_reallyExit) return;
        _reallyExit = true;
        if (_viewModel is not null) await _viewModel.SaveSettingsAsync();
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _overlay?.CloseForExit();
        _audio?.Dispose();
        MainWindow?.Close();
        Shutdown();
    }
}
