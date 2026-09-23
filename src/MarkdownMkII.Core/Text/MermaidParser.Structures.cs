using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Text;

public static partial class MermaidParser
{
    private static bool TryParseRequirement(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        var started = false;
        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("%%", StringComparison.Ordinal) || trimmed is "{" or "}")
            {
                continue;
            }

            if (!started)
            {
                if (!trimmed.StartsWith("requirementDiagram", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            if (trimmed.StartsWith("id:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("text:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("type:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("risk:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("verifymethod:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            const string req = "requirement ";
            const string element = "element ";
            if (trimmed.StartsWith(req, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(element, StringComparison.OrdinalIgnoreCase))
            {
                var rest = trimmed[(trimmed.StartsWith(req, StringComparison.OrdinalIgnoreCase) ? req.Length : element.Length)..].Trim();
                var brace = rest.IndexOf('{');
                if (brace >= 0)
                {
                    rest = rest[..brace].Trim();
                }

                if (rest.Length > 0)
                {
                    nodes.TryAdd(rest, rest);
                }

                continue;
            }

            var arrow = trimmed.IndexOf("->", StringComparison.Ordinal);
            if (arrow <= 0)
            {
                continue;
            }

            var left = trimmed[..arrow].Trim().TrimEnd('-').Trim();
            var dash = left.LastIndexOf(' ');
            if (dash > 0 && left[dash..].Trim().Length > 0 && !char.IsLetter(left[dash..].Trim()[0]))
            {
                left = left[..dash].Trim();
            }

            var space = left.LastIndexOf(" - ", StringComparison.Ordinal);
            if (space > 0)
            {
                left = left[..space].Trim();
            }

            var right = trimmed[(arrow + 2)..].Trim();
            if (left.Length == 0 || right.Length == 0)
            {
                continue;
            }

            nodes.TryAdd(left, left);
            nodes.TryAdd(right, right);
            edges.Add(new MermaidEdge(left, right, null));
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

    private static readonly Regex C4Entity = new(
        @"^\s*(?:Person|System|Container|Component|SystemDb|System_Ext|Person_Ext|Container_Ext)\s*\(\s*([A-Za-z][\w]*)\s*(?:,\s*""([^""]*)"")?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex C4Rel = new(
        @"^\s*Rel(?:_U|_D|_L|_R|_Neighbor)?\s*\(\s*([A-Za-z][\w]*)\s*,\s*([A-Za-z][\w]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool TryParseC4(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
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
                if (!trimmed.StartsWith("C4", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            if (trimmed.StartsWith("title ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("UpdateElementStyle", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("UpdateRelStyle", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("UpdateLayoutConfig", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entity = C4Entity.Match(trimmed);
            if (entity.Success)
            {
                var id = entity.Groups[1].Value;
                var label = entity.Groups[2].Success && entity.Groups[2].Value.Length > 0
                    ? entity.Groups[2].Value
                    : id;
                nodes.TryAdd(id, label);
                continue;
            }

            var rel = C4Rel.Match(trimmed);
            if (!rel.Success)
            {
                continue;
            }

            var from = rel.Groups[1].Value;
            var to = rel.Groups[2].Value;
            nodes.TryAdd(from, from);
            nodes.TryAdd(to, to);
            edges.Add(new MermaidEdge(from, to, null));
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

    private static readonly Regex ArchitectureEntity = new(
        @"^\s*(?:group|service|junction)\s+([A-Za-z][\w]*)(?:\([^)]*\))?(?:\[([^\]]+)\])?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ArchitectureEdge = new(
        @"^\s*([A-Za-z][\w]*)(?::[TBLR])?\s*(?:-->|--)\s*(?:[TBLR]:)?([A-Za-z][\w]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool TryParseArchitecture(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
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
                if (!trimmed.StartsWith("architecture", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            if (trimmed.StartsWith("title ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entity = ArchitectureEntity.Match(trimmed);
            if (entity.Success)
            {
                var id = entity.Groups[1].Value;
                var label = entity.Groups[2].Success && entity.Groups[2].Value.Length > 0
                    ? entity.Groups[2].Value
                    : id;
                nodes.TryAdd(id, label);
                continue;
            }

            var edge = ArchitectureEdge.Match(trimmed);
            if (!edge.Success)
            {
                continue;
            }

            var from = edge.Groups[1].Value;
            var to = edge.Groups[2].Value;
            nodes.TryAdd(from, from);
            nodes.TryAdd(to, to);
            edges.Add(new MermaidEdge(from, to, null));
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

    private static readonly Regex PacketField = new(
        @"^\s*(\d+)\s*-\s*(\d+)\s*:\s*""?([^""]+?)""?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool TryParsePacket(string[] lines, out MermaidDiagram diagram)
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
                if (!trimmed.StartsWith("packet", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            if (trimmed.StartsWith("title ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var field = PacketField.Match(trimmed);
            if (!field.Success)
            {
                continue;
            }

            var id = "b" + field.Groups[1].Value;
            var label = field.Groups[3].Value.Trim();
            if (label.Length == 0)
            {
                label = field.Groups[1].Value + "-" + field.Groups[2].Value;
            }

            nodes.TryAdd(id, label);
            if (previous is not null)
            {
                edges.Add(new MermaidEdge(previous, id, null));
            }

            previous = id;
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

    private static readonly Regex KanbanTicket = new(
        @"^\s*\[([^\]]+)\]\s*(.*)$",
        RegexOptions.CultureInvariant);

    private static bool TryParseKanban(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        var started = false;
        string? column = null;
        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            if (!started)
            {
                if (!trimmed.StartsWith("kanban", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            if (trimmed.StartsWith("title ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var ticket = KanbanTicket.Match(trimmed);
            if (ticket.Success)
            {
                var id = ticket.Groups[1].Value.Trim();
                var label = ticket.Groups[2].Value.Trim();
                if (id.Length == 0)
                {
                    continue;
                }

                nodes.TryAdd(id, label.Length == 0 ? id : label);
                if (column is not null)
                {
                    edges.Add(new MermaidEdge(column, id, null));
                }

                continue;
            }

            var columnId = new string(trimmed.Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-').ToArray());
            if (columnId.Length == 0)
            {
                continue;
            }

            column = columnId;
            nodes.TryAdd(columnId, trimmed);
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

    private static readonly Regex RadarAxis = new(
        @"^\s*axis\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RadarCurve = new(
        @"^\s*curve\s+([A-Za-z][\w]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RadarToken = new(
        @"([A-Za-z][\w]*)(?:\s*\[[^\]]*\])?",
        RegexOptions.CultureInvariant);

    private static bool TryParseRadar(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        var axes = new List<string>();
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
                if (!trimmed.StartsWith("radar", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            if (trimmed.StartsWith("title ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var axis = RadarAxis.Match(trimmed);
            if (axis.Success)
            {
                foreach (Match token in RadarToken.Matches(axis.Groups[1].Value))
                {
                    var id = token.Groups[1].Value;
                    if (id.Equals("axis", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    nodes.TryAdd(id, id);
                    if (axes.Count > 0)
                    {
                        edges.Add(new MermaidEdge(axes[^1], id, null));
                    }

                    axes.Add(id);
                }

                continue;
            }

            var curve = RadarCurve.Match(trimmed);
            if (!curve.Success)
            {
                continue;
            }

            var curveId = curve.Groups[1].Value;
            nodes.TryAdd(curveId, curveId);
            if (axes.Count > 0)
            {
                edges.Add(new MermaidEdge(curveId, axes[0], null));
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

    private static readonly Regex TreemapItem = new(
        @"^(\s*)""([^""]+)""(?:\s*:\s*[\d.]+)?",
        RegexOptions.CultureInvariant);

    private static bool TryParseTreemap(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        var stack = new List<(int Indent, string Id)>();
        var started = false;
        var index = 0;
        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            if (!started)
            {
                if (!trimmed.StartsWith("treemap", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            if (trimmed.StartsWith("title ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var item = TreemapItem.Match(raw);
            if (!item.Success)
            {
                continue;
            }

            var indent = item.Groups[1].Value.Length;
            var label = item.Groups[2].Value.Trim();
            if (label.Length == 0)
            {
                continue;
            }

            var id = "t" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
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
}
