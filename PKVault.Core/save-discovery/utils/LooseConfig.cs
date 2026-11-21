using System.Text;

namespace PKVault.Core.SaveDiscovery;

/// <summary>
/// Tolerant reader for emulator config files: INI (mGBA, melonDS legacy, DeSmuME, Qt),
/// simple TOML (melonDS 1.x) and RetroArch .cfg ("key = \"value\"").
/// Only what discovery needs: sections, key = value, quoted strings, one-level string arrays.
/// </summary>
public sealed class LooseConfig
{
    public record Entry(string Section, string Key, string Value, string[] Values);

    public IReadOnlyList<Entry> Entries { get; }

    private LooseConfig(IReadOnlyList<Entry> entries)
    {
        Entries = entries;
    }

    public static LooseConfig Parse(string text)
    {
        List<Entry> entries = [];
        var section = "";

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#')
                continue;

            if (line[0] == '[' && line.EndsWith(']') && !line.Contains('='))
            {
                section = line.Trim('[', ']').Trim().Trim('"');
                continue;
            }

            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = line[..eqIndex].Trim().Trim('"');
            var rawValue = line[(eqIndex + 1)..].Trim();

            if (rawValue.StartsWith('['))
            {
                // TOML array, may span multiple lines
                var sb = new StringBuilder(rawValue);
                while (!ContainsArrayEnd(sb.ToString()) && i + 1 < lines.Length)
                {
                    i++;
                    sb.Append('\n').Append(lines[i].Trim());
                }
                var values = ExtractQuotedStrings(sb.ToString());
                entries.Add(new(section, key, values.FirstOrDefault() ?? "", values));
                continue;
            }

            var value = rawValue.StartsWith('"')
                ? ExtractQuotedStrings(rawValue).FirstOrDefault() ?? ""
                : rawValue;

            entries.Add(new(section, key, value, [value]));
        }

        return new(entries);
    }

    /// <summary>
    /// First value for key, in given section (null section = any section). Key compare is case-insensitive.
    /// </summary>
    public string? Get(string key, string? section = null) => Find(key, section).FirstOrDefault()?.Value;

    public IEnumerable<Entry> Find(string key, string? section = null) => Entries.Where(e =>
        string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase)
        && (section == null || string.Equals(e.Section, section, StringComparison.OrdinalIgnoreCase))
    );

    public IEnumerable<Entry> InSection(string section) => Entries.Where(e =>
        string.Equals(e.Section, section, StringComparison.OrdinalIgnoreCase)
    );

    public bool HasSectionStartingWith(string prefix) => Entries.Any(e =>
        e.Section.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
    );

    private static bool ContainsArrayEnd(string text)
    {
        var inQuote = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\' && inQuote)
            {
                i++;
                continue;
            }
            if (c == '"')
                inQuote = !inQuote;
            else if (c == ']' && !inQuote)
                return true;
        }
        return false;
    }

    private static string[] ExtractQuotedStrings(string text)
    {
        List<string> values = [];
        StringBuilder? current = null;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (current == null)
            {
                if (c == '"')
                    current = new();
                continue;
            }

            if (c == '\\' && i + 1 < text.Length && (text[i + 1] == '"' || text[i + 1] == '\\'))
            {
                current.Append(text[i + 1]);
                i++;
                continue;
            }

            if (c == '"')
            {
                values.Add(current.ToString());
                current = null;
                continue;
            }

            current.Append(c);
        }

        return [.. values];
    }
}
