using System.Text.RegularExpressions;

namespace BoardMgmt.Application.Common.Parsing;

public static class SimpleVtt
{
    public sealed record Cue(TimeSpan Start, TimeSpan End, string Text, string? SpeakerName, string? SpeakerEmail);

    // 00:01:02.000 --> 00:01:06.000
    private static readonly Regex TimeLine = new(
        @"(?<s>\d\d:\d\d:\d\d\.\d+)\s-->\s(?<e>\d\d:\d\d:\d\d\.\d+)",
        RegexOptions.Compiled);

    private static TimeSpan ParseTime(string s)
        => TimeSpan.ParseExact(s, @"hh\:mm\:ss\.fff", null);

    public static IEnumerable<Cue> Parse(string vtt)
    {
        var lines = vtt.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var m = TimeLine.Match(lines[i]);
            if (!m.Success) continue;

            var start = ParseTime(m.Groups["s"].Value);
            var end = ParseTime(m.Groups["e"].Value);

            var textParts = new List<string>();
            string? speaker = null;
            string? email = null;

            // read until blank line or EOF
            for (i = i + 1; i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]); i++)
            {
                var line = lines[i].Trim();

                // Try infer speaker variants:
                // [Name] Hello...
                if (speaker is null && line.StartsWith('[') && line.Contains(']'))
                {
                    var endAt = line.IndexOf(']');
                    if (endAt > 1)
                    {
                        speaker = line.Substring(1, endAt - 1).Trim();
                        line = line[(endAt + 1)..].TrimStart('-', ' ', ':');
                    }
                }
                // Name: Hello...
                else if (speaker is null && line.Contains(':') && !line.StartsWith("http"))
                {
                    var idx = line.IndexOf(':');
                    if (idx > 0 && idx <= 60)
                    {
                        var maybeName = line[..idx].Trim();
                        if (maybeName.Length > 0 && maybeName.Count(c => char.IsWhiteSpace(c)) <= 4)
                        {
                            speaker = maybeName;
                            line = line[(idx + 1)..].Trim();
                        }
                    }
                }

                textParts.Add(line);
            }

            var text = string.Join(' ', textParts).Trim();
            if (text.Length == 0) continue;

            yield return new Cue(start, end, text, speaker, email);
        }
    }
}
