using SubtitleOverlay.Models;

namespace SubtitleOverlay.Services;

public sealed class SubtitleService
{
    private IReadOnlyList<SubtitleEntry> _entries = Array.Empty<SubtitleEntry>();
    private int _currentIndex = -1;

    public int Count => _entries.Count;
    public SubtitleEntry? CurrentEntry =>
        _currentIndex >= 0 && _currentIndex < _entries.Count ? _entries[_currentIndex] : null;

    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        _entries = await SrtParser.ParseFileAsync(path, cancellationToken);
        _currentIndex = -1;
    }

    public void ResetSearch() => _currentIndex = -1;

    public SubtitleEntry? GetEntry(int index) =>
        index >= 0 && index < _entries.Count ? _entries[index] : null;

    public int FindNearestIndex(TimeSpan time)
    {
        if (_entries.Count == 0) return -1;
        var low = 0;
        var high = _entries.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (_entries[middle].Start < time) low = middle + 1;
            else high = middle - 1;
        }

        if (low == 0) return 0;
        if (low >= _entries.Count) return _entries.Count - 1;
        var before = _entries[low - 1];
        var after = _entries[low];
        return time - before.Start <= after.Start - time ? low - 1 : low;
    }

    public string FindAt(TimeSpan time)
    {
        if (_currentIndex >= 0 && _currentIndex < _entries.Count)
        {
            var current = _entries[_currentIndex];
            if (time >= current.Start && time <= current.End) return current.Text;
            if (_currentIndex + 1 < _entries.Count)
            {
                var next = _entries[_currentIndex + 1];
                if (time >= next.Start && time <= next.End)
                {
                    _currentIndex++;
                    return next.Text;
                }
            }
        }

        var low = 0;
        var high = _entries.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var entry = _entries[middle];
            if (time < entry.Start) high = middle - 1;
            else if (time > entry.End) low = middle + 1;
            else
            {
                _currentIndex = middle;
                return entry.Text;
            }
        }
        _currentIndex = Math.Clamp(high, -1, _entries.Count - 1);
        return string.Empty;
    }
}
