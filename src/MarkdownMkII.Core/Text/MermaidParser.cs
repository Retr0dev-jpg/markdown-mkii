using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Text;

public sealed record MermaidNode(string Id, string Label);

public sealed record MermaidEdge(string From, string To, string? Label);

public sealed record MermaidSequenceMessage(string From, string To, string Text, bool Dashed);

public sealed record MermaidPieSlice(string Label, double Value);

public enum MermaidKind
{
    Flow,
    Sequence,
    Pie,
    Class,
    Gantt,
    Er
}

public sealed record MermaidGanttTask(string Label, string Section, double Start, double Duration);

public sealed record MermaidDiagram(
    bool Horizontal,
    IReadOnlyList<MermaidNode> Nodes,
    IReadOnlyList<MermaidEdge> Edges,
    MermaidKind Kind = MermaidKind.Flow,
    IReadOnlyList<MermaidSequenceMessage>? Messages = null,
    IReadOnlyList<MermaidPieSlice>? Slices = null,
    string? Title = null,
    IReadOnlyList<MermaidGanttTask>? Tasks = null);

public static partial class MermaidParser
{
    private static readonly Regex Header = new(
        @"^\s*(?:graph|flowchart)\s+(TD|TB|BT|RL|LR)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Edge = new(
        @"^\s*([A-Za-z][\w-]*)(?:\[[^\]]*\]|\([^\)]*\)|\{[^}]*\}|\(\([^\)]*\)\))?\s*(?:-->|---|-.->|==>)\s*(?:\|([^|]+)\|)?\s*([A-Za-z][\w-]*)(?:\[[^\]]*\]|\([^\)]*\)|\{[^}]*\}|\(\([^\)]*\)\))?\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Node = new(
        @"^\s*([A-Za-z][\w-]*)(?:\[([^\]]*)\]|\(([^\)]*)\)|\{([^}]*)\}|\(\(([^\)]*)\)\))\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex SequenceArrow = new(
        @"^\s*([A-Za-z][\w]*(?:-[\w]+)*)\s*(->>|-->>|->|-->)\s*([A-Za-z][\w]*(?:-[\w]+)*)\s*:\s*(.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex StateEdge = new(
        @"^\s*(\[\*\]|[A-Za-z][\w]*)\s*-->\s*(?:\|([^|]+)\|)?\s*(\[\*\]|[A-Za-z][\w]*)(?:\s*:\s*(.+))?\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PieSlice = new(
        @"^\s*""?([^"":]+)""?\s*:\s*([0-9]+(?:\.[0-9]+)?)\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ClassRelation = new(
        @"^\s*([A-Za-z][\w]*)\s*(<\|--|\|--|>|--\*|\*--|o--|-->|<--|<\.\.|\.\.>)\s*([A-Za-z][\w]*)\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ClassDecl = new(
        @"^\s*class\s+([A-Za-z][\w]*)\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex ClassMember = new(
        @"^\s*([A-Za-z][\w]*)\s*:\s*(.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex GanttTask = new(
        @"^\s*(.+?)\s*:\s*(?:[\w]+,\s*)?(?:after\s+[\w]+,\s*)?(?:([0-9]{4}-[0-9]{2}-[0-9]{2}),\s*)?([0-9]+)\s*d?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ErEdge = new(
        @"^\s*([A-Za-z][\w-]*)\s+(\S+)\s+([A-Za-z][\w-]*)\s*(?::\s*(.+))?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex GitCommitLine = new(
        @"^\s*commit(?:\s+id:\s*[""']?([^""'\s]+)[""']?)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryParse(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], []);
        code ??= string.Empty;
        var lines = code.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var first = lines.Select(line => line.Trim()).FirstOrDefault(line =>
            line.Length > 0 && !line.StartsWith("%%", StringComparison.Ordinal));
        if (first is null)
        {
            return false;
        }

        if (first.StartsWith("sequenceDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseSequence(lines, out diagram);
        }

        if (first.StartsWith("stateDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseState(lines, out diagram);
        }

        if (first.StartsWith("classDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseClass(lines, out diagram);
        }

        if (first.Equals("gantt", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("gantt ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseGantt(lines, out diagram);
        }

        if (first.StartsWith("erDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseEr(lines, out diagram);
        }

        if (first.Equals("mindmap", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("mindmap ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseMindmap(lines, out diagram);
        }

        if (first.Equals("gitGraph", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("gitGraph ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseGitGraph(lines, out diagram);
        }

        if (first.Equals("timeline", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("timeline ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseTimeline(lines, out diagram);
        }

        if (first.Equals("journey", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("journey ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseJourney(lines, out diagram);
        }

        if (first.Equals("quadrantChart", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("quadrantChart ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseQuadrant(lines, out diagram);
        }

        if (first.Equals("sankey-beta", StringComparison.OrdinalIgnoreCase) ||
            first.Equals("sankey", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("sankey-beta ", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("sankey ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseSankey(lines, out diagram);
        }

        if (first.Equals("xychart-beta", StringComparison.OrdinalIgnoreCase) ||
            first.Equals("xychart", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("xychart-beta ", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("xychart ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseXyChart(lines, out diagram);
        }

        if (first.Equals("block-beta", StringComparison.OrdinalIgnoreCase) ||
            first.Equals("block", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("block-beta ", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("block ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseBlock(lines, out diagram);
        }

        if (first.Equals("requirementDiagram", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("requirementDiagram ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseRequirement(lines, out diagram);
        }

        if (first.Equals("C4Context", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("C4Context ", StringComparison.OrdinalIgnoreCase) ||
            first.Equals("C4Container", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("C4Container ", StringComparison.OrdinalIgnoreCase) ||
            first.Equals("C4Component", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("C4Component ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseC4(lines, out diagram);
        }

        if (first.Equals("architecture-beta", StringComparison.OrdinalIgnoreCase) ||
            first.Equals("architecture", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("architecture-beta ", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("architecture ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseArchitecture(lines, out diagram);
        }

        if (first.Equals("packet-beta", StringComparison.OrdinalIgnoreCase) ||
            first.Equals("packet", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("packet-beta ", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("packet ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParsePacket(lines, out diagram);
        }

        if (first.Equals("kanban", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("kanban ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseKanban(lines, out diagram);
        }

        if (first.Equals("radar-beta", StringComparison.OrdinalIgnoreCase) ||
            first.Equals("radar", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("radar-beta ", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("radar ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseRadar(lines, out diagram);
        }

        if (first.Equals("treemap-beta", StringComparison.OrdinalIgnoreCase) ||
            first.Equals("treemap", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("treemap-beta ", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("treemap ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseTreemap(lines, out diagram);
        }

        if (first.Equals("pie", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith("pie ", StringComparison.OrdinalIgnoreCase))
        {
            return TryParsePie(lines, out diagram);
        }

        return TryParseFlow(lines, out diagram);
    }

    private static bool TryParsePie(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Pie);
        string? title = null;
        var slices = new List<MermaidPieSlice>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.Equals("pie", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("pie ", StringComparison.OrdinalIgnoreCase))
            {
                var rest = line.Equals("pie", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : line[3..].Trim();
                var titleAt = rest.IndexOf("title ", StringComparison.OrdinalIgnoreCase);
                if (titleAt >= 0)
                {
                    title = rest[(titleAt + 6)..].Trim().Trim('"');
                }

                continue;
            }

            if (line.StartsWith("title ", StringComparison.OrdinalIgnoreCase))
            {
                title = line[6..].Trim().Trim('"');
                continue;
            }

            var match = PieSlice.Match(line);
            if (!match.Success)
            {
                continue;
            }

            slices.Add(new MermaidPieSlice(match.Groups[1].Value.Trim(), double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (slices.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(false, [], [], MermaidKind.Pie, Slices: slices, Title: title);
        return true;
    }

    private static bool TryParseMindmap(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        var stack = new List<(int Indent, string Id)>();
        var started = false;
        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            if (!started)
            {
                if (!trimmed.StartsWith("mindmap", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            var indent = 0;
            while (indent < raw.Length && (raw[indent] == ' ' || raw[indent] == '\t'))
            {
                indent++;
            }

            var label = trimmed
                .TrimStart('(', '[', '{')
                .TrimEnd(')', ']', '}')
                .Trim('(', ')');
            if (label.Length == 0)
            {
                continue;
            }

            var idChars = label.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
            var id = new string(idChars).Trim('_');
            if (id.Length == 0 || !char.IsLetter(id[0]))
            {
                id = "n" + id;
            }

            var unique = id;
            var n = 2;
            while (nodes.ContainsKey(unique) && !string.Equals(nodes[unique], label, StringComparison.Ordinal))
            {
                unique = id + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                n++;
            }

            id = unique;
            nodes.TryAdd(id, label);
            while (stack.Count > 0 && stack[^1].Indent >= indent)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            if (stack.Count > 0)
            {
                edges.Add(new MermaidEdge(stack[^1].Id, id, null));
            }

            stack.Add((indent, id));
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            false,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges,
            MermaidKind.Flow);
        return true;
    }

    private static bool TryParseGitGraph(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        string? previous = null;
        var index = 0;
        var started = false;
        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            if (!started)
            {
                if (!trimmed.StartsWith("gitGraph", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            var match = GitCommitLine.Match(raw);
            if (!match.Success)
            {
                continue;
            }

            index++;
            var id = match.Groups[1].Success ? match.Groups[1].Value.Trim() : string.Empty;
            if (id.Length == 0)
            {
                id = "C" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var unique = id;
            var n = 2;
            while (nodes.ContainsKey(unique))
            {
                unique = id + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                n++;
            }

            nodes[unique] = unique;
            if (previous is not null)
            {
                edges.Add(new MermaidEdge(previous, unique, null));
            }

            previous = unique;
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            false,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges,
            MermaidKind.Flow);
        return true;
    }

    private static bool TryParseBlock(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        var started = false;
        string? previous = null;
        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            if (!started)
            {
                if (!trimmed.StartsWith("block", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            if (trimmed.StartsWith("columns ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("end", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("block:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var token in trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var piece = token.Trim().Trim('"', '\'');
                if (piece.Length == 0 ||
                    piece.Equals("space", StringComparison.OrdinalIgnoreCase) ||
                    piece is "{" or "}")
                {
                    continue;
                }

                var idEnd = 0;
                while (idEnd < piece.Length && (char.IsLetterOrDigit(piece[idEnd]) || piece[idEnd] is '_' or '-'))
                {
                    idEnd++;
                }

                if (idEnd == 0 || !char.IsLetter(piece[0]))
                {
                    continue;
                }

                var id = piece[..idEnd];
                var label = id;
                if (idEnd < piece.Length && piece[idEnd] == '[' && piece.EndsWith(']'))
                {
                    label = piece[(idEnd + 1)..^1].Trim().Trim('"', '\'');
                    if (label.Length == 0)
                    {
                        label = id;
                    }
                }

                nodes.TryAdd(id, label);
                if (previous is not null && !string.Equals(previous, id, StringComparison.OrdinalIgnoreCase))
                {
                    edges.Add(new MermaidEdge(previous, id, null));
                }

                previous = id;
            }
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            false,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges,
            MermaidKind.Flow);
        return true;
    }

    private static void EnsureNode(Dictionary<string, string> nodes, string id, string? label)
    {
        if (!nodes.ContainsKey(id) || (!string.IsNullOrWhiteSpace(label) && !string.Equals(label, id, StringComparison.Ordinal)))
        {
            nodes[id] = string.IsNullOrWhiteSpace(label) ? id : label!;
        }
    }

    private static string? ExtractLabel(string line, string id, int startAt = 0)
    {
        var index = line.IndexOf(id, startAt, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return id;
        }

        var after = index + id.Length;
        if (after >= line.Length)
        {
            return id;
        }

        return line[after] switch
        {
            '[' => Slice(line, after + 1, ']'),
            '{' => Slice(line, after + 1, '}'),
            '(' when after + 1 < line.Length && line[after + 1] == '(' => Slice(line, after + 2, ')'),
            '(' => Slice(line, after + 1, ')'),
            _ => id
        };
    }

    private static string Slice(string line, int start, char close)
    {
        var end = line.IndexOf(close, start);
        return end < 0 ? string.Empty : line[start..end];
    }

    private static string First(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
