using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SubtitleOverlay.Models;

namespace SubtitleOverlay.Services;

public static partial class SrtParser
{
    [GeneratedRegex(@"^\s*(?<start>\d{1,2}:\d{2}:\d{2},\d{3})\s*-->\s*(?<end>\d{1,2}:\d{2}:\d{2},\d{3})", RegexOptions.Compiled)]
    private static partial Regex TimeLineRegex();

    public static async Task<IReadOnlyList<SubtitleEntry>> ParseFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(path, new UTF8Encoding(false, true), cancellationToken);
        return Parse(text);
    }

    public static IReadOnlyList<SubtitleEntry> Parse(string content)
    {
        var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n').Trim('\uFEFF', '\n', ' ');
        var blocks = Regex.Split(normalized, @"\n\s*\n");
        var entries = new List<SubtitleEntry>(blocks.Length);
        foreach (var block in blocks)
        {
            var lines = block.Split('\n');
            var timeIndex = Array.FindIndex(lines, line => TimeLineRegex().IsMatch(line));
            if (timeIndex < 0 || timeIndex + 1 >= lines.Length) continue;
            var match = TimeLineRegex().Match(lines[timeIndex]);
            if (!TryTime(match.Groups["start"].Value, out var start) ||
                !TryTime(match.Groups["end"].Value, out var end) || end <= start) continue;
            var subtitle = string.Join(Environment.NewLine, lines.Skip(timeIndex + 1)).Trim();
            if (subtitle.Length > 0) entries.Add(new SubtitleEntry(start, end, subtitle));
        }
        return entries.OrderBy(entry => entry.Start).ToArray();
    }

    private static bool TryTime(string value, out TimeSpan result) =>
        TimeSpan.TryParseExact(value, @"h\:mm\:ss\,fff", CultureInfo.InvariantCulture, out result) ||
        TimeSpan.TryParseExact(value, @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture, out result);
}
