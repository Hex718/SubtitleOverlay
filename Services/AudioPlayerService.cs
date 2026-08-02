using LibVLCSharp.Shared;

namespace SubtitleOverlay.Services;

public sealed class AudioPlayerService : IDisposable
{
    private readonly LibVLC _libVlc;
    private Media? _media;
    public MediaPlayer Player { get; }

    public AudioPlayerService()
    {
        Core.Initialize();
        _libVlc = new LibVLC("--no-video");
        Player = new MediaPlayer(_libVlc);
    }

    public long Time { get => Math.Max(0, Player.Time); set => Player.Time = Math.Max(0, value); }
    public long Length => Math.Max(0, Player.Length);
    public bool IsPlaying => Player.IsPlaying;
    public int Volume { get => Player.Volume; set => Player.Volume = Math.Clamp(value, 0, 100); }

    public void Open(string path)
    {
        _media?.Dispose();
        _media = new Media(_libVlc, new Uri(path));
        Player.Media = _media;
    }

    public void Play() => Player.Play();
    public void Pause() => Player.SetPause(true);
    public void Resume() => Player.SetPause(false);
    public void Stop() => Player.Stop();
    public void SeekBy(long milliseconds) => Time = Math.Clamp(Time + milliseconds, 0, Length > 0 ? Length : long.MaxValue);

    public void Dispose()
    {
        Player.Stop();
        Player.Dispose();
        _media?.Dispose();
        _libVlc.Dispose();
    }
}
