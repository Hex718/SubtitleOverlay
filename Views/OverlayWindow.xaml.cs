using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SubtitleOverlay.ViewModels;

namespace SubtitleOverlay.Views;

public partial class OverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private readonly MainViewModel _viewModel;
    private bool _closingForExit;

    public OverlayWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Width = viewModel.Settings.OverlayWidth;
        Height = viewModel.Settings.OverlayHeight;
        Loaded += OnLoaded;
        LocationChanged += (_, _) => StoreBounds();
        SizeChanged += (_, _) => StoreBounds();
        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        viewModel.OverlayPositionRequested += (_, position) => PositionOverlay(position);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (double.IsFinite(_viewModel.Settings.OverlayLeft) && double.IsFinite(_viewModel.Settings.OverlayTop))
        {
            Left = _viewModel.Settings.OverlayLeft;
            Top = _viewModel.Settings.OverlayTop;
        }
        else PositionOverlay("Bas");
        ApplyClickThrough();
        ApplyAppearance();
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ClickThrough)) ApplyClickThrough();
        if (e.PropertyName is nameof(MainViewModel.TextShadow) or nameof(MainViewModel.Borderless)) ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        ShadowText.Visibility = _viewModel.TextShadow ? Visibility.Visible : Visibility.Collapsed;
        OverlayBorder.BorderThickness = _viewModel.Borderless ? new Thickness(0) : new Thickness(1);
        OverlayBorder.BorderBrush = System.Windows.Media.Brushes.DimGray;
    }

    private void ApplyClickThrough()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var style = GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        style = _viewModel.ClickThrough ? style | WsExTransparent | WsExLayered : style & ~WsExTransparent;
        SetWindowLongPtr(handle, GwlExstyle, new IntPtr(style));
    }

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !_viewModel.ClickThrough) DragMove();
    }

    private void PositionOverlay(string position)
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = position switch
        {
            "Haut" => area.Top + 30,
            "Centre" => area.Top + (area.Height - Height) / 2,
            _ => area.Bottom - Height - 30
        };
    }

    private void StoreBounds()
    {
        if (WindowState != WindowState.Normal) return;
        _viewModel.Settings.OverlayLeft = Left;
        _viewModel.Settings.OverlayTop = Top;
        _viewModel.Settings.OverlayWidth = Width;
        _viewModel.Settings.OverlayHeight = Height;
    }

    public void CloseForExit() { _closingForExit = true; Close(); }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_closingForExit) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value);
}
