namespace MarkdownMkII.Core.Text;

public static partial class MermaidParser
{
    private static bool TryParseTimeline(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        string? previous = null;
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
                if (!trimmed.StartsWith("timeline", StringComparison.OrdinalIgnoreCase))
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

            string label;
            var colon = trimmed.IndexOf(':');
            if (colon >= 0)
            {
                var stamp = trimmed[..colon].Trim();
                var ev = trimmed[(colon + 1)..].Trim();
                label = ev.Length == 0 ? stamp : (stamp.Length == 0 ? ev : stamp + " " + ev);
            }
            else
            {
                label = trimmed;
            }

            if (label.Length == 0)
            {
                continue;
            }

            index++;
            var id = "t" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            nodes[id] = label;
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

    private static bool TryParseJourney(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        string? previous = null;
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
                if (!trimmed.StartsWith("journey", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            if (trimmed.StartsWith("title ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("section ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var colon = trimmed.IndexOf(':');
            var label = colon > 0 ? trimmed[..colon].Trim() : trimmed;
            if (label.Length == 0)
            {
                continue;
            }

            index++;
            var id = "j" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            nodes[id] = label;
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

    private static bool TryParseQuadrant(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        string? previous = null;
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
                if (!trimmed.StartsWith("quadrantChart", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            if (trimmed.StartsWith("title ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("x-axis ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("y-axis ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("quadrant-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var colon = trimmed.IndexOf(':');
            var label = colon > 0 ? trimmed[..colon].Trim() : trimmed;
            if (label.Length == 0)
            {
                continue;
            }

            index++;
            var id = "q" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            nodes[id] = label;
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

    private static bool TryParseSankey(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
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
                if (!trimmed.StartsWith("sankey", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            var parts = trimmed.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
            {
                continue;
            }

            var source = parts[0].Trim().Trim('"', '\'');
            var target = parts[1].Trim().Trim('"', '\'');
            var value = parts[2].Trim().Trim('"', '\'');
            if (source.Length == 0 || target.Length == 0)
            {
                continue;
            }

            var from = IdFor(source);
            var to = IdFor(target);
            edges.Add(new MermaidEdge(from, to, value.Length == 0 ? null : value));
        }

        if (nodes.Count == 0 || edges.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            false,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges,
            MermaidKind.Flow);
        return true;

        string IdFor(string label)
        {
            if (ids.TryGetValue(label, out var id))
            {
                return id;
            }

            index++;
            id = "s" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ids[label] = id;
            nodes[id] = label;
            return id;
        }
    }

    private static bool TryParseXyChart(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        var started = false;
        var index = 0;
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
                if (!trimmed.StartsWith("xychart", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                started = true;
                continue;
            }

            if (trimmed.StartsWith("title ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("y-axis ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("yaxis ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var open = trimmed.IndexOf('[');
            var close = trimmed.LastIndexOf(']');
            if (open < 0 || close <= open)
            {
                continue;
            }

            var inner = trimmed[(open + 1)..close];
            foreach (var piece in inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var label = piece.Trim().Trim('"', '\'');
                if (label.Length == 0)
                {
                    continue;
                }

                index++;
                var id = "x" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                nodes[id] = label;
                if (previous is not null)
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
}
