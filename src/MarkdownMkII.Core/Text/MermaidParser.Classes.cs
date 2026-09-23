namespace MarkdownMkII.Core.Text;

public static partial class MermaidParser
{
    private static bool TryParseClass(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Class);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var members = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("%%", StringComparison.Ordinal) ||
                line.StartsWith("classDiagram", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relation = ClassRelation.Match(line);
            if (relation.Success)
            {
                var from = relation.Groups[1].Value;
                var to = relation.Groups[3].Value;
                EnsureNode(nodes, from, from);
                EnsureNode(nodes, to, to);
                edges.Add(new MermaidEdge(from, to, relation.Groups[2].Value));
                continue;
            }

            var decl = ClassDecl.Match(line);
            if (decl.Success)
            {
                EnsureNode(nodes, decl.Groups[1].Value, decl.Groups[1].Value);
                continue;
            }

            var member = ClassMember.Match(line);
            if (member.Success)
            {
                var id = member.Groups[1].Value;
                EnsureNode(nodes, id, id);
                if (!members.TryGetValue(id, out var list))
                {
                    list = [];
                    members[id] = list;
                }

                list.Add(member.Groups[2].Value.Trim());
            }
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        var labeled = nodes.Select(pair =>
        {
            if (!members.TryGetValue(pair.Key, out var list) || list.Count == 0)
            {
                return new MermaidNode(pair.Key, pair.Value);
            }

            return new MermaidNode(pair.Key, pair.Value + "\n" + string.Join('\n', list.Take(3)));
        }).ToList();
        diagram = new MermaidDiagram(false, labeled, edges, MermaidKind.Class);
        return true;
    }

    private static bool TryParseGantt(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Gantt);
        string? title = null;
        var section = "Tasks";
        var pending = new List<(string Label, string Section, double Duration, DateTime? StartDate)>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("%%", StringComparison.Ordinal) ||
                line.Equals("gantt", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("dateFormat", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("axisFormat", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith("title ", StringComparison.OrdinalIgnoreCase))
            {
                title = line[6..].Trim();
                continue;
            }

            if (line.StartsWith("section ", StringComparison.OrdinalIgnoreCase))
            {
                section = line[8..].Trim();
                if (section.Length == 0)
                {
                    section = "Tasks";
                }

                continue;
            }

            var match = GanttTask.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var duration = double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
            duration = Math.Max(1, duration);
            DateTime? startDate = null;
            if (match.Groups[2].Success &&
                DateTime.TryParseExact(
                    match.Groups[2].Value,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsedDate))
            {
                startDate = parsedDate;
            }

            pending.Add((match.Groups[1].Value.Trim().Trim('"'), section, duration, startDate));
        }

        if (pending.Count == 0)
        {
            return false;
        }

        var origin = pending
            .Where(item => item.StartDate is not null)
            .Select(item => item.StartDate!.Value)
            .DefaultIfEmpty()
            .Min();
        var cursor = 0d;
        var tasks = new List<MermaidGanttTask>(pending.Count);
        foreach (var item in pending)
        {
            double start;
            if (item.StartDate is { } date && origin != default)
            {
                start = Math.Max(0, (date - origin).TotalDays);
            }
            else
            {
                start = cursor;
                cursor += item.Duration;
            }

            tasks.Add(new MermaidGanttTask(item.Label, item.Section, start, item.Duration));
            if (item.StartDate is null)
            {
                continue;
            }

            cursor = Math.Max(cursor, start + item.Duration);
        }

        diagram = new MermaidDiagram(false, [], [], MermaidKind.Gantt, Title: title, Tasks: tasks);
        return true;
    }

    private static bool TryParseEr(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Er);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("%%", StringComparison.Ordinal) ||
                line.StartsWith("erDiagram", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = ErEdge.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var from = match.Groups[1].Value;
            var to = match.Groups[3].Value;
            EnsureNode(nodes, from, from);
            EnsureNode(nodes, to, to);
            var cardinality = match.Groups[2].Value;
            var label = match.Groups[4].Success ? match.Groups[4].Value.Trim() : cardinality;
            edges.Add(new MermaidEdge(from, to, string.IsNullOrWhiteSpace(label) ? cardinality : label));
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            true,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges,
            MermaidKind.Er);
        return true;
    }
}
