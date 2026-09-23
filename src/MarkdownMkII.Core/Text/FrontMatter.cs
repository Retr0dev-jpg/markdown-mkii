using System.Text;

namespace MarkdownMkII.Core.Text;

public static partial class FrontMatter
{
    /// <summary>
    /// Reads top-level <c>key: value</c> entries. Indented lines, block lists and block scalars belong to
    /// the preceding key instead of being read as keys of their own; block lists become a flow list.
    /// </summary>
    public static bool TryParse(string? raw, out IReadOnlyDictionary<string, string> fields)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        fields = map;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        foreach (var entry in Entries(raw))
        {
            if (entry.Key is not null)
            {
                map[entry.Key] = EntryValue(entry);
            }
        }

        return map.Count > 0;
    }

    public static string ToRaw(IReadOnlyDictionary<string, string> fields)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        foreach (var pair in fields)
        {
            builder.Append(pair.Key).Append(": ").Append(FormatValue(pair.Value)).Append('\n');
        }

        builder.Append("---\n");
        return builder.ToString();
    }

    /// <summary>Sets one key and leaves every other line of the front matter untouched.</summary>
    public static string Upsert(string markdown, string key, string value)
    {
        markdown ??= string.Empty;
        key = (key ?? string.Empty).Trim();
        value ??= string.Empty;
        if (key.Length == 0)
        {
            return markdown;
        }

        if (!TryLocate(markdown, out var start, out var end, out _))
        {
            return "---\n" + key + ": " + FormatValue(value) + "\n---\n" + markdown;
        }

        var entries = Entries(markdown[start..end]);
        var index = entries.FindIndex(entry => KeyEquals(entry.Key, key));
        if (index >= 0)
        {
            var existing = entries[index];
            entries[index] = existing with { Lines = [existing.Key + ": " + FormatValue(value)] };
        }
        else
        {
            var insertAt = entries.Count;
            while (insertAt > 0 && entries[insertAt - 1].Key is null && entries[insertAt - 1].Lines.All(string.IsNullOrWhiteSpace))
            {
                insertAt--;
            }

            entries.Insert(insertAt, new Entry(key, [key + ": " + FormatValue(value)]));
        }

        return Replace(markdown, start, end, entries);
    }

    public static bool TrySplit(string markdown, out Dictionary<string, string> fields, out string body)
    {
        fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        body = markdown ?? string.Empty;
        if (markdown is null || !TryLocate(markdown, out var start, out var end, out var bodyStart))
        {
            return false;
        }

        body = markdown[bodyStart..].TrimStart('\r', '\n');
        TryParse(markdown[start..end], out var parsed);
        fields = new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
        return true;
    }

    /// <summary>
    /// Finds the front matter: <paramref name="contentStart"/>..<paramref name="contentEnd"/> is the YAML
    /// between the delimiters and <paramref name="bodyStart"/> follows the closing delimiter line.
    /// </summary>
    internal static bool TryLocate(string markdown, out int contentStart, out int contentEnd, out int bodyStart)
    {
        contentStart = contentEnd = bodyStart = 0;
        var lines = new LineMap(markdown ?? string.Empty);
        if (lines.LineText(0).TrimEnd() != "---")
        {
            return false;
        }

        for (var line = 1; line < lines.LineCount; line++)
        {
            if (lines.LineText(line).TrimEnd() is not ("---" or "..."))
            {
                continue;
            }

            contentStart = lines.OffsetOfLine(1);
            contentEnd = lines.OffsetOfLine(line);
            bodyStart = lines.OffsetOfLine(line + 1);
            return true;
        }

        return false;
    }

    private sealed record Entry(string? Key, List<string> Lines);

    private static List<Entry> Entries(string content)
    {
        var result = new List<Entry>();
        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        Entry? current = null;
        var pending = new List<string>();
        foreach (var line in lines)
        {
            if (TopLevelKey(line) is { } key)
            {
                if (pending.Count > 0) result.Add(new Entry(null, [.. pending]));
                pending.Clear();
                current = new Entry(key, [line]);
                result.Add(current);
            }
            else if (line.Trim().Length == 0 || line.StartsWith('#'))
            {
                pending.Add(line);
            }
            else if (current is not null && (char.IsWhiteSpace(line[0]) || line.StartsWith("- ", StringComparison.Ordinal) || line == "-"))
            {
                current.Lines.AddRange(pending);
                pending.Clear();
                current.Lines.Add(line);
            }
            else
            {
                pending.Add(line);
            }
        }

        if (pending.Count > 0) result.Add(new Entry(null, [.. pending]));
        return result;
    }

    private static string? TopLevelKey(string line)
    {
        var colon = KeyColon(line);
        if (colon < 0)
        {
            return null;
        }

        var key = Unquote(line[..colon].Trim());
        return key.Length == 0 ? null : key;
    }

    private static int KeyColon(string line)
    {
        if (line.Length == 0 || char.IsWhiteSpace(line[0]) || line[0] is '#' or '-' or '[' or '{')
        {
            return -1;
        }

        for (var i = 1; i < line.Length; i++)
        {
            if (line[i] == ':' && (i + 1 == line.Length || line[i + 1] is ' ' or '\t'))
            {
                return i;
            }
        }

        return -1;
    }

    private static string EntryValue(Entry entry)
    {
        var first = entry.Lines[0];
        var value = first[(KeyColon(first) + 1)..].Trim();
        if (value.StartsWith('#')) value = string.Empty;
        var rest = entry.Lines.Skip(1).Where(line => !line.TrimStart().StartsWith('#')).ToList();
        if (value.Length > 0 && value[0] is '|' or '>')
        {
            var parts = rest.Select(line => line.Trim());
            return value[0] == '|' ? string.Join("\n", parts).Trim('\n') : string.Join(" ", parts.Where(p => p.Length > 0));
        }

        var items = rest.Where(line => line.TrimStart().StartsWith("- ", StringComparison.Ordinal)).ToList();
        if (value.Length == 0 && items.Count > 0)
        {
            return "[" + string.Join(", ", items.Select(line => FormatListItem(Unquote(line.TrimStart()[2..].Trim())))) + "]";
        }

        if (value.Length > 0 && rest.Count > 0 && value[0] is not ('[' or '{'))
        {
            value = string.Join(" ", new[] { value }.Concat(rest.Select(line => line.Trim())).Where(p => p.Length > 0));
        }

        return Unquote(value);
    }

    private static bool KeyEquals(string? a, string b) => a is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Replace(string markdown, int start, int end, List<Entry> entries)
    {
        var newline = markdown.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var builder = new StringBuilder();
        foreach (var line in entries.SelectMany(entry => entry.Lines))
        {
            builder.Append(line).Append(newline);
        }

        return markdown[..start] + builder + markdown[end..];
    }

    /// <summary>Quotes a scalar only when plain YAML would read it differently.</summary>
    internal static string FormatValue(string value)
    {
        value ??= string.Empty;
        if (value.Length == 0 || (value.StartsWith('[') && value.EndsWith(']')))
        {
            return value;
        }

        var needsQuotes = value != value.Trim() || value.Contains(": ", StringComparison.Ordinal) || value.EndsWith(':') ||
            value.Contains(" #", StringComparison.Ordinal) || value.Contains('\n') || value.Contains('\r') ||
            value[0] is '#' or '&' or '*' or '!' or '|' or '>' or '\'' or '"' or '%' or '@' or '`' or '{' or '[' or ',' ||
            value == "-" || value.StartsWith("- ", StringComparison.Ordinal) || value.StartsWith("? ", StringComparison.Ordinal);
        return needsQuotes
            ? "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n") + "\""
            : value;
    }

    private static string FormatListItem(string item)
        => item.IndexOfAny([',', '[', ']', '{', '}', '"', '\'', '#']) >= 0 || item.Contains(": ", StringComparison.Ordinal) || item != item.Trim()
            ? "\"" + item.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
            : item;

    private static string FormatList(IEnumerable<string> items) => "[" + string.Join(", ", items.Select(FormatListItem)) + "]";

    /// <summary>Splits a flow list on commas, semicolons or pipes outside quotes.</summary>
    private static IEnumerable<string> SplitList(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith('[') && raw.EndsWith(']'))
        {
            raw = raw[1..^1];
        }

        var builder = new StringBuilder();
        char quote = '\0';
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (quote != '\0')
            {
                if (c == '\\' && quote == '"' && i + 1 < raw.Length) { builder.Append(c).Append(raw[++i]); continue; }
                if (c == quote) quote = '\0';
                builder.Append(c);
            }
            else if (c is '"' or '\'')
            {
                quote = c;
                builder.Append(c);
            }
            else if (c is ',' or ';' or '|')
            {
                yield return builder.ToString();
                builder.Clear();
            }
            else
            {
                builder.Append(c);
            }
        }

        yield return builder.ToString();
    }

    public static IReadOnlyList<string> List(IReadOnlyDictionary<string, string>? fields, params string[] keys)
    {
        if (fields is null || keys is null || keys.Length == 0)
        {
            return [];
        }

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key) || !fields.TryGetValue(key, out var raw))
            {
                continue;
            }

            raw = (raw ?? string.Empty).Trim();
            if (raw.Trim('[', ']').Trim().Length == 0)
            {
                continue;
            }

            var items = SplitList(raw)
                .Select(item => Unquote(item.Trim()))
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (items.Count > 0)
            {
                return items;
            }
        }

        return [];
    }

    public static IReadOnlyList<string> Aliases(IReadOnlyDictionary<string, string> fields)
        => List(fields, "aliases", "alias");

    public static string? Id(IReadOnlyDictionary<string, string>? fields)
    {
        if (fields is null)
        {
            return null;
        }

        if (!fields.TryGetValue("id", out var raw) && !fields.TryGetValue("uid", out raw))
        {
            return null;
        }

        raw = Unquote(raw ?? string.Empty).Trim();
        return raw.Length == 0 ? null : raw;
    }

    public static string EnsureId(string markdown, string? id = null)
    {
        markdown ??= string.Empty;
        if (TrySplit(markdown, out var fields, out _) && Id(fields) is not null)
        {
            return markdown;
        }

        var value = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N")[..8] : id.Trim();
        return Upsert(markdown, "id", value);
    }

    public static string StampUpdated(string markdown, DateTimeOffset now)
    {
        var stamp = now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("updated", out var current) &&
            string.Equals(current, stamp, StringComparison.Ordinal))
        {
            return markdown;
        }

        return Upsert(markdown, "updated", stamp);
    }

    public static string EnsureCreated(string markdown, DateTimeOffset? now = null)
    {
        markdown ??= string.Empty;
        if (!TrySplit(markdown, out var fields, out _) || fields.ContainsKey("created"))
        {
            return markdown;
        }

        var stamp = (now ?? DateTimeOffset.Now).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return Upsert(markdown, "created", stamp);
    }

    public static string EnsureDate(string markdown, DateTime? now = null, string? stem = null)
    {
        markdown ??= string.Empty;
        var stamp = DailyDate(stem, now ?? DateTime.Now);
        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("date", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        return Upsert(markdown, "date", stamp);
    }

    private static string DailyDate(string? stem, DateTime now)
    {
        if (stem is not null &&
            DateTime.TryParseExact(
                stem,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        return now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Adds a nonempty field only when the document has no value for that key.</summary>
    public static string EnsureField(string markdown, string key, string? value)
    {
        markdown ??= string.Empty;
        key = (key ?? string.Empty).Trim();
        value = (value ?? string.Empty).Trim();
        if (key.Length == 0 || value.Length == 0 ||
            (TrySplit(markdown, out var fields, out _) &&
             fields.TryGetValue(key, out var current) && !string.IsNullOrWhiteSpace(current)))
        {
            return markdown;
        }

        return Upsert(markdown, key, value);
    }

    public static string EnsureStatus(string markdown, string? status = "draft")
    {
        markdown ??= string.Empty;
        status = (status ?? "draft").Trim();
        if (status.Length == 0)
        {
            status = "draft";
        }

        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("status", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        return Upsert(markdown, "status", status);
    }

    public static string EnsureAuthor(string markdown, string? author = null)
    {
        markdown ??= string.Empty;
        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("author", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        author = (author ?? string.Empty).Trim();
        if (author.Length == 0)
        {
            author = Environment.UserName;
        }

        if (string.IsNullOrWhiteSpace(author))
        {
            return markdown;
        }

        return Upsert(markdown, "author", author.Trim());
    }

    public static string EnsureCssclass(string markdown, string cssclass)
    {
        markdown ??= string.Empty;
        cssclass = (cssclass ?? string.Empty).Trim();
        if (cssclass.Length == 0)
        {
            return markdown;
        }

        TrySplit(markdown, out var fields, out _);
        var values = List(fields, "cssclass", "cssclasses").ToList();
        if (values.Any(item => string.Equals(item, cssclass, StringComparison.OrdinalIgnoreCase)))
        {
            return markdown;
        }

        values.Add(cssclass);
        return Upsert(markdown, "cssclass", FormatList(values));
    }

    public static string EnsureLang(string markdown, string? lang = "it")
    {
        markdown ??= string.Empty;
        lang = (lang ?? "it").Trim();
        if (lang.Length == 0)
        {
            lang = "it";
        }

        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("lang", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        return Upsert(markdown, "lang", lang);
    }

    public static string EnsureType(string markdown, string? type = "note")
    {
        markdown ??= string.Empty;
        type = (type ?? "note").Trim();
        if (type.Length == 0)
        {
            type = "note";
        }

        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("type", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        return Upsert(markdown, "type", type);
    }

    public static string EnsurePriority(string markdown, string? priority = "0")
    {
        markdown ??= string.Empty;
        priority = (priority ?? "0").Trim();
        if (priority.Length == 0)
        {
            priority = "0";
        }

        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("priority", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        return Upsert(markdown, "priority", priority);
    }

    public static string EnsureLicense(string markdown, string? license = "CC-BY-4.0")
    {
        markdown ??= string.Empty;
        license = (license ?? "CC-BY-4.0").Trim();
        if (license.Length == 0)
        {
            license = "CC-BY-4.0";
        }

        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("license", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        return Upsert(markdown, "license", license);
    }

    public static string EnsureKeywords(string markdown, string keyword)
    {
        markdown ??= string.Empty;
        keyword = (keyword ?? string.Empty).Trim();
        if (keyword.Length == 0)
        {
            return markdown;
        }

        TrySplit(markdown, out var fields, out _);
        var values = List(fields, "keywords", "keyword").ToList();
        if (values.Any(item => string.Equals(item, keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return markdown;
        }

        values.Add(keyword);
        return Upsert(markdown, "keywords", FormatList(values));
    }

    public static string EnsureVersion(string markdown, string? version = "1.0.0")
    {
        markdown ??= string.Empty;
        version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Trim();
        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("version", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        return Upsert(markdown, "version", version);
    }

    public static string EnsureCopyright(string markdown, string? copyright = "All rights reserved")
    {
        markdown ??= string.Empty;
        copyright = string.IsNullOrWhiteSpace(copyright) ? "All rights reserved" : copyright.Trim();
        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("copyright", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        return Upsert(markdown, "copyright", copyright);
    }

    public static string EnsureRevision(string markdown, string? revision = "1")
    {
        markdown ??= string.Empty;
        revision = string.IsNullOrWhiteSpace(revision) ? "1" : revision.Trim();
        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("revision", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        return Upsert(markdown, "revision", revision);
    }

    public static string EnsureAbstract(string markdown, string? value)
    {
        markdown ??= string.Empty;
        value = (value ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return markdown;
        }

        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("abstract", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        return Upsert(markdown, "abstract", value);
    }

    public static string StripEmptyKeys(string markdown)
    {
        markdown ??= string.Empty;
        if (!TryLocate(markdown, out var start, out var end, out var bodyStart))
        {
            return markdown;
        }

        var entries = Entries(markdown[start..end]);
        var removed = entries.RemoveAll(entry => entry.Key is not null && entry.Lines.Count == 1 && EntryValue(entry).Trim().Length == 0);
        if (removed == 0)
        {
            return markdown;
        }

        return entries.Any(entry => entry.Key is not null)
            ? Replace(markdown, start, end, entries)
            : markdown[bodyStart..].TrimStart('\r', '\n');
    }

    /// <summary>Sorts keys; comment lines travel with the key that follows them.</summary>
    public static string SortKeys(string markdown)
    {
        markdown ??= string.Empty;
        if (!TryLocate(markdown, out var start, out var end, out _))
        {
            return markdown;
        }

        var groups = new List<(string Key, List<string> Lines)>();
        var pending = new List<string>();
        foreach (var entry in Entries(markdown[start..end]))
        {
            if (entry.Key is null)
            {
                pending.AddRange(entry.Lines);
                continue;
            }

            groups.Add((entry.Key, [.. pending, .. entry.Lines]));
            pending.Clear();
        }

        if (groups.Count == 0)
        {
            return markdown;
        }

        var ordered = groups.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new Entry(group.Key, group.Lines)).ToList();
        if (pending.Count > 0) ordered.Add(new Entry(null, pending));
        return Replace(markdown, start, end, ordered);
    }

    public static string EnsureTitle(string markdown, string? title = null)
    {
        markdown ??= string.Empty;
        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("title", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = FirstHeading(markdown) ?? "Untitled";
        }

        return Upsert(markdown, "title", title.Trim());
    }

    public static string EnsureAlias(string markdown, string alias)
    {
        markdown ??= string.Empty;
        alias = (alias ?? string.Empty).Trim();
        if (alias.Length == 0)
        {
            return markdown;
        }

        TrySplit(markdown, out var fields, out _);
        var aliases = Aliases(fields).ToList();
        if (aliases.Any(item => string.Equals(item, alias, StringComparison.OrdinalIgnoreCase)))
        {
            return markdown;
        }

        aliases.Add(alias);
        return Upsert(markdown, "aliases", FormatList(aliases));
    }

    public static string EnsureTags(string markdown, string tag)
    {
        markdown ??= string.Empty;
        tag = (tag ?? string.Empty).Trim().TrimStart('#');
        if (tag.Length == 0)
        {
            return markdown;
        }

        TrySplit(markdown, out var fields, out _);
        var tags = List(fields, "tags", "tag").ToList();
        if (tags.Any(item => string.Equals(item, tag, StringComparison.OrdinalIgnoreCase)))
        {
            return markdown;
        }

        tags.Add(tag);
        return Upsert(markdown, "tags", FormatList(tags));
    }

    public static string RemoveKey(string markdown, string key)
    {
        markdown ??= string.Empty;
        key = (key ?? string.Empty).Trim();
        if (key.Length == 0 || !TryLocate(markdown, out var start, out var end, out var bodyStart))
        {
            return markdown;
        }

        var entries = Entries(markdown[start..end]);
        if (entries.RemoveAll(entry => KeyEquals(entry.Key, key)) == 0)
        {
            return markdown;
        }

        return entries.Any(entry => entry.Key is not null)
            ? Replace(markdown, start, end, entries)
            : markdown[bodyStart..].TrimStart('\r', '\n');
    }

    public static string RenameKey(string markdown, string oldKey, string newKey)
    {
        markdown ??= string.Empty;
        oldKey = (oldKey ?? string.Empty).Trim();
        newKey = (newKey ?? string.Empty).Trim();
        if (oldKey.Length == 0 ||
            newKey.Length == 0 ||
            string.Equals(oldKey, newKey, StringComparison.OrdinalIgnoreCase) ||
            !TryLocate(markdown, out var start, out var end, out _))
        {
            return markdown;
        }

        var entries = Entries(markdown[start..end]);
        var index = entries.FindIndex(entry => KeyEquals(entry.Key, oldKey));
        if (index < 0 || entries.Any(entry => KeyEquals(entry.Key, newKey)))
        {
            return markdown;
        }

        var lines = entries[index].Lines.ToList();
        lines[0] = newKey + lines[0][KeyColon(lines[0])..];
        entries[index] = new Entry(newKey, lines);
        return Replace(markdown, start, end, entries);
    }

    public static string EnsureDescription(string markdown, int maxLength = 160)
    {
        markdown ??= string.Empty;
        maxLength = Math.Clamp(maxLength, 16, 400);
        if (TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("description", out var current) &&
            !string.IsNullOrWhiteSpace(current))
        {
            return markdown;
        }

        var body = TrySplit(markdown, out _, out var splitBody) ? splitBody : markdown;
        var description = FirstParagraph(body, maxLength);
        return string.IsNullOrWhiteSpace(description) ? markdown : Upsert(markdown, "description", description);
    }

    private static string? FirstParagraph(string markdown, int maxLength)
    {
        var map = new LineMap(markdown);
        var fenceState = new MarkdownFence();
        for (var i = 0; i < map.LineCount; i++)
        {
            var raw = map.LineText(i);
            var trimmed = raw.Trim();
            if (fenceState.Advance(raw))
            {
                continue;
            }

            if (fenceState.IsOpen || trimmed.Length == 0 || trimmed is "---" or "***" or "___")
            {
                continue;
            }

            if (trimmed[0] is '#' or '>' or '|' || trimmed.StartsWith("- ", StringComparison.Ordinal) ||
                trimmed.StartsWith("* ", StringComparison.Ordinal) || trimmed.StartsWith("+ ", StringComparison.Ordinal))
            {
                continue;
            }

            var collapsed = string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (collapsed.Length == 0)
            {
                continue;
            }

            return collapsed.Length <= maxLength ? collapsed : collapsed[..maxLength].TrimEnd();
        }

        return null;
    }

    private static string? FirstHeading(string markdown)
    {
        var map = new LineMap(markdown);
        var codeLines = MarkdownFence.CodeLines(map);
        for (var i = 0; i < map.LineCount; i++)
        {
            if (codeLines[i])
            {
                continue;
            }

            var trimmed = map.LineText(i).TrimStart();
            var hashes = 0;
            while (hashes < trimmed.Length && trimmed[hashes] == '#')
            {
                hashes++;
            }

            if (hashes is < 1 or > 6 || hashes >= trimmed.Length || trimmed[hashes] != ' ')
            {
                continue;
            }

            var heading = trimmed[hashes..].Trim();
            if (heading.Length > 0)
            {
                return heading;
            }
        }

        return null;
    }

    public static string Strip(string markdown)
    {
        markdown ??= string.Empty;
        return TrySplit(markdown, out _, out var body) ? body : markdown;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            var inner = value[1..^1];
            var builder = new StringBuilder(inner.Length);
            for (var i = 0; i < inner.Length; i++)
            {
                if (inner[i] == '\\' && i + 1 < inner.Length)
                {
                    var next = inner[++i];
                    builder.Append(next switch { 'n' => '\n', 't' => '\t', _ => next });
                }
                else builder.Append(inner[i]);
            }

            return builder.ToString();
        }

        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'");
        }

        return value;
    }
}
