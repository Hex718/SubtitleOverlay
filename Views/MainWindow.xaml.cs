using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SubtitleOverlay.ViewModels;

namespace SubtitleOverlay.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    private void Progress_MouseDown(object sender, MouseButtonEventArgs e) => _viewModel.BeginSeek();
    private void Progress_MouseUp(object sender, MouseButtonEventArgs e) => _viewModel.EndSeek();
    private void Progress_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e) => _viewModel.EndSeek();

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
