using System.Drawing;
using System.Windows.Forms;

namespace SubtitleOverlay.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIconService(Action showControls, Action toggleOverlay, Action playPause, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Afficher les contrôles", null, (_, _) => showControls());
        menu.Items.Add("Afficher / masquer l’overlay", null, (_, _) => toggleOverlay());
        menu.Items.Add("Lecture / pause", null, (_, _) => playPause());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => exit());
        _icon = new NotifyIcon
        {
            Text = "Subtitle Overlay",
            Icon = SystemIcons.Information,
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => showControls();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
