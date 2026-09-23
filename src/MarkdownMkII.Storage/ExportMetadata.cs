using System.Text.Json;
using System.Text.RegularExpressions;

namespace MarkdownMkII.Storage;
public static partial class ExportMetadata
{
    public static string Apply(string markdown, NoteSummary note)
    {
        // Patch only owned top-level fields. Preserve comments, unknown mappings and multiline YAML verbatim.
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = JsonSerializer.Serialize(note.Title),
            ["tags"] = JsonSerializer.Serialize(note.Tags),
            ["category"] = JsonSerializer.Serialize(note.Category ?? ""),
            ["mkii_favorite"] = note.Favorite ? "true" : "false",
            ["mkii_color"] = note.Color.ToString()
        };
        var match = Header().Match(markdown);
        var yaml = match.Success ? match.Groups[1].Value : "";
        var kept = new List<string>();
        var skipping = false;
        foreach (var line in yaml.Replace("\r\n", "\n").Split('\n'))
        {
            var field = Field().Match(line);
            if (field.Success)
                skipping = fields.ContainsKey(field.Groups[1].Value.Trim('"', '\''));
            else if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.StartsWith('#'))
                skipping = false;
            if (!skipping || line.TrimStart().StartsWith('#'))
                kept.Add(line);
        }

        var preserved = string.Join('\n', kept).TrimEnd('\n');
        var header = (preserved.Length > 0 ? preserved + "\n" : "") + string.Join('\n', fields.Select(p => p.Key + ": " + p.Value));
        return "---\n" + header + "\n---\n" + (match.Success ? markdown[match.Length..] : markdown);
    }

    [GeneratedRegex(@"\A(?:\uFEFF)?---\r?\n(.*?)^(?:---|\.\.\.)[ \t]*(?:\r?\n|$)", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex Header();
    [GeneratedRegex("^([A-Za-z_][A-Za-z0-9_-]*|\"[^\"]+\"|'[^']+'):[ \\t]*(.*)$")]
    private static partial Regex Field();
}
