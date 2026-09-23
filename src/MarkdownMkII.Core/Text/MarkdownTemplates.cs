using MarkdownMkII.Core.Services;

namespace MarkdownMkII.Core.Text;

public static class MarkdownTemplates
{
    public const string BlankId = "blank";

    public const string StarterId = "starter";

    public const string DailyId = "daily";

    public const string MeetingId = "meeting";

    public const string ZettelId = "zettel";

    public const string WeeklyId = "weekly";

    public const string MonthlyId = "monthly";

    public const string YearlyId = "yearly";

    public const string QuarterlyId = "quarterly";

    public const string VaultFolderName = "_templates";

    public static IReadOnlyList<string> Ids { get; } =
        [BlankId, StarterId, DailyId, MeetingId, ZettelId, WeeklyId, MonthlyId, YearlyId, QuarterlyId];

    public static string NormalizeId(string? id)
        => Ids.Contains((id ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            ? (id ?? StarterId).Trim().ToLowerInvariant()
            : StarterId;

    public static string Render(string? id, string title)
    {
        var now = DateTimeOffset.Now;
        var resolved = TitleOrDefault(title);
        var raw = NormalizeId(id) switch
        {
            BlankId => Blank(resolved),
            DailyId => Daily(resolved),
            MeetingId => Meeting(resolved),
            ZettelId => Zettel(resolved, now),
            WeeklyId => Weekly(resolved, now),
            MonthlyId => Monthly(resolved, now),
            YearlyId => Yearly(resolved, now),
            QuarterlyId => Quarterly(resolved, now),
            _ => Starter(resolved)
        };
        return Expand(raw, resolved, now);
    }

    public static string Expand(string markdown, string title, DateTimeOffset? now = null)
    {
        markdown ??= string.Empty;
        var stamp = now ?? DateTimeOffset.Now;
        var expanded = markdown
            .Replace("{{title}}", title, StringComparison.OrdinalIgnoreCase)
            .Replace("{{date}}", stamp.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{{time}}", stamp.ToString("HH:mm"), StringComparison.OrdinalIgnoreCase)
            .Replace("{{week}}", WeeklyStem(stamp.DateTime), StringComparison.OrdinalIgnoreCase)
            .Replace("{{month}}", stamp.ToString("yyyy-MM"), StringComparison.OrdinalIgnoreCase)
            .Replace("{{year}}", stamp.ToString("yyyy"), StringComparison.OrdinalIgnoreCase)
            .Replace("{{quarter}}", QuarterlyStem(stamp.DateTime), StringComparison.OrdinalIgnoreCase)
            .Replace("{{uuid}}", Guid.NewGuid().ToString("N")[..8], StringComparison.OrdinalIgnoreCase);
        return FrontMatter.EnsureCreated(expanded, stamp);
    }

    public static string Blank(string title)
        => "# " + TitleOrDefault(title) + "\n\n";

    public static string Starter(string title)
    {
        title = TitleOrDefault(title);
        return $"""
            ---
            title: {title}
            ---

            # {title}

            - [ ] 
            - [ ] 

            ## 

            **Grassetto**, *corsivo*, `codice`, [link](https://example.com).

            ```csharp
            Console.WriteLine("Markdown MkII");
            ```

            """;
    }

    public static string Daily(string? title = null, DateTimeOffset? now = null)
    {
        var stamp = now ?? DateTimeOffset.Now;
        var day = stamp.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        title = string.IsNullOrWhiteSpace(title) ? day : title.Trim();
        if (DateTime.TryParseExact(
                title,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed))
        {
            day = parsed.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        return $"""
            ---
            title: {title}
            date: {day}
            tags: daily
            ---

            # {title}

            - [ ] 

            ## Note


            """;
    }

    public static string Meeting(string title)
    {
        title = TitleOrDefault(title);
        var day = DateTime.Now.ToString("yyyy-MM-dd");
        return $"""
            ---
            title: {title}
            date: {day}
            tags: meeting
            ---

            # {title}

            ## Partecipanti

            - 

            ## Ordine del giorno

            1. 

            ## Note


            ## Azioni

            - [ ] 
            """;
    }

    public static string Zettel(string title, DateTimeOffset? now = null)
    {
        title = TitleOrDefault(title);
        var stamp = now ?? DateTimeOffset.Now;
        var id = stamp.ToString("yyyyMMddHHmm");
        return $"""
            ---
            title: {title}
            id: {id}
            ---

            # {title}


            """;
    }

    public static string Weekly(string? title = null, DateTimeOffset? now = null)
    {
        var stamp = now ?? DateTimeOffset.Now;
        var stem = WeeklyStem(stamp.DateTime);
        title = string.IsNullOrWhiteSpace(title) ? stem : title.Trim();
        var monday = stamp.Date.AddDays(-(((int)stamp.DayOfWeek + 6) % 7));
        var days = new System.Text.StringBuilder();
        for (var i = 0; i < 7; i++)
        {
            days.Append("- [[").Append(monday.AddDays(i).ToString("yyyy-MM-dd")).AppendLine("]]");
        }

        return $"""
            ---
            title: {title}
            tags: weekly
            ---

            # {title}

            {days}
            """;
    }

    public static string Monthly(string? title = null, DateTimeOffset? now = null)
    {
        var stamp = now ?? DateTimeOffset.Now;
        var stem = stamp.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        title = string.IsNullOrWhiteSpace(title) ? stem : title.Trim();
        return $"""
            ---
            title: {title}
            tags: monthly
            ---

            # {title}

            ## Settimane

            - [[{WeeklyStem(stamp.DateTime)}]]

            """;
    }

    public static string Yearly(string? title = null, DateTimeOffset? now = null)
    {
        var stamp = now ?? DateTimeOffset.Now;
        var stem = stamp.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture);
        title = string.IsNullOrWhiteSpace(title) ? stem : title.Trim();
        var months = new System.Text.StringBuilder();
        for (var month = 1; month <= 12; month++)
        {
            months.Append("- [[")
                .Append(stem)
                .Append('-')
                .Append(month.ToString("00", System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine("]]");
        }

        return $"""
            ---
            title: {title}
            tags: yearly
            ---

            # {title}

            ## Mesi

            {months}
            """;
    }

    public static string Quarterly(string? title = null, DateTimeOffset? now = null)
    {
        var stamp = now ?? DateTimeOffset.Now;
        var stem = QuarterlyStem(stamp.DateTime);
        title = string.IsNullOrWhiteSpace(title) ? stem : title.Trim();
        var year = stamp.Year;
        var quarter = ((stamp.Month - 1) / 3) + 1;
        var startMonth = ((quarter - 1) * 3) + 1;
        var months = new System.Text.StringBuilder();
        for (var i = 0; i < 3; i++)
        {
            months.Append("- [[")
                .Append(year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture))
                .Append('-')
                .Append((startMonth + i).ToString("00", System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine("]]");
        }

        return $"""
            ---
            title: {title}
            tags: quarterly
            ---

            # {title}

            ## Mesi

            {months}
            """;
    }

    public static string QuarterlyStem(DateTime date)
    {
        var year = date.Year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture);
        var quarter = ((date.Month - 1) / 3) + 1;
        return year + "-Q" + quarter.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string WeeklyStem(DateTime date)
    {
        var year = System.Globalization.ISOWeek.GetYear(date);
        var week = System.Globalization.ISOWeek.GetWeekOfYear(date);
        return year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)
               + "-W"
               + week.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static IReadOnlyList<(string Name, string Path)> VaultTemplates(IEnumerable<string>? folders)
    {
        var hits = new List<(string Name, string Path)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders ?? [])
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            var root = Path.Combine(folder, VaultFolderName);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in WorkspaceScanner.EnumerateMarkdownFiles(root, recursive: false))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.Length == 0 || !seen.Add(name))
                {
                    continue;
                }

                hits.Add((name, path));
            }
        }

        return hits;
    }

    public static string? RenderVault(string path, string title, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return Expand(File.ReadAllText(path), TitleOrDefault(title), now);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string SuggestedTitle(string? id, string fallback)
        => NormalizeId(id) switch
        {
            DailyId => DateTime.Now.ToString("yyyy-MM-dd"),
            WeeklyId => WeeklyStem(DateTime.Now),
            MonthlyId => DateTime.Now.ToString("yyyy-MM"),
            YearlyId => DateTime.Now.ToString("yyyy"),
            QuarterlyId => QuarterlyStem(DateTime.Now),
            ZettelId => DateTime.Now.ToString("yyyyMMddHHmm"),
            _ => string.IsNullOrWhiteSpace(fallback) ? "Untitled" : fallback.Trim()
        };

    private static string TitleOrDefault(string title)
        => string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
}
