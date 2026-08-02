namespace SubtitleOverlay.Models;

public sealed class AppSettings
{
    public double OverlayLeft { get; set; } = double.NaN;
    public double OverlayTop { get; set; } = double.NaN;
    public double OverlayWidth { get; set; } = 900;
    public double OverlayHeight { get; set; } = 150;
    public double FontSize { get; set; } = 32;
    public string FontFamily { get; set; } = "Segoe UI";
    public double BackgroundOpacity { get; set; } = 0.65;
    public double TextOpacity { get; set; } = 1;
    public string TextColor { get; set; } = "#FFFFFFFF";
    public string BackgroundColor { get; set; } = "#FF000000";
    public bool TextShadow { get; set; } = true;
    public bool Borderless { get; set; } = true;
    public bool ClickThrough { get; set; }
    public int Volume { get; set; } = 80;
    public int SubtitleOffsetMs { get; set; }
    public double SubtitleRate { get; set; } = 1;
}
