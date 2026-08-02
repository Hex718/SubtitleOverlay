using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace SubtitleOverlay.Services;

public enum HotkeyAction { PlayPause, Backward, Forward, OffsetForward, OffsetBackward, ClickThrough, ToggleOverlay, Resync }

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private readonly Window _window;
    private HwndSource? _source;
    private readonly Dictionary<int, HotkeyAction> _actions = new();

    public event EventHandler<HotkeyAction>? HotkeyPressed;

    public GlobalHotkeyService(Window window)
    {
        _window = window;
        _window.SourceInitialized += OnSourceInitialized;
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Initialize();
    }

    public IReadOnlyList<string> RegisterDefaults()
    {
        Initialize();
        var definitions = new[]
        {
            (1, HotkeyAction.PlayPause, Key.Space, "Ctrl + Alt + Espace"),
            (2, HotkeyAction.Backward, Key.Left, "Ctrl + Alt + Gauche"),
            (3, HotkeyAction.Forward, Key.Right, "Ctrl + Alt + Droite"),
            (4, HotkeyAction.OffsetForward, Key.Up, "Ctrl + Alt + Haut"),
            (5, HotkeyAction.OffsetBackward, Key.Down, "Ctrl + Alt + Bas"),
            (6, HotkeyAction.ClickThrough, Key.T, "Ctrl + Alt + T"),
            (7, HotkeyAction.ToggleOverlay, Key.O, "Ctrl + Alt + O"),
            (8, HotkeyAction.Resync, Key.R, "Ctrl + Alt + R")
        };
        var failures = new List<string>();
        foreach (var (id, action, key, label) in definitions)
        {
            if (RegisterHotKey(_source!.Handle, id, ModControl | ModAlt, (uint)KeyInterop.VirtualKeyFromKey(key)))
                _actions[id] = action;
            else failures.Add($"{label} ({new Win32Exception(Marshal.GetLastWin32Error()).Message})");
        }
        return failures;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => Initialize();

    private void Initialize()
    {
        if (_source is not null) return;
        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero) return;
        _source = HwndSource.FromHwnd(handle);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            HotkeyPressed?.Invoke(this, action);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is null) return;
        foreach (var id in _actions.Keys) UnregisterHotKey(_source.Handle, id);
        _source.RemoveHook(WndProc);
        _actions.Clear();
        _source = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
