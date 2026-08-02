namespace SubtitleOverlay.Models;

public sealed record SubtitleEntry(TimeSpan Start, TimeSpan End, string Text);
