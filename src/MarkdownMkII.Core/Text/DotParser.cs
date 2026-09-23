using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Text;

public static class DotParser
{
    private static readonly Regex Header = new(
        @"^\s*(?:di)?graph\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Edge = new(
        @"^\s*""?([A-Za-z][\w-]*)""?\s*(?:->|--)\s*""?([A-Za-z][\w-]*)""?\s*(?:\[([^\]]*)\])?\s*;?\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Node = new(
        @"^\s*""?([A-Za-z][\w-]*)""?\s*\[([^\]]*)\]\s*;?\s*$",
        RegexOptions.CultureInvariant);

    public static bool TryParse(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], []);
        code ??= string.Empty;
        var lines = code.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var started = false;
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();

        foreach (var raw in lines)
        {
            var line = raw.Trim().TrimEnd(';');
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal) || line is "{" or "}")
            {
                continue;
            }

            if (!started)
            {
                if (!Header.IsMatch(line))
                {
                    return false;
                }

                started = true;
                continue;
            }

            var edge = Edge.Match(line);
            if (edge.Success)
            {
                var from = edge.Groups[1].Value;
                var to = edge.Groups[2].Value;
                nodes.TryAdd(from, from);
                nodes.TryAdd(to, to);
                var label = LabelFromAttributes(edge.Groups[3].Value);
                edges.Add(new MermaidEdge(from, to, label));
                continue;
            }

            var node = Node.Match(line);
            if (node.Success)
            {
                var id = node.Groups[1].Value;
                nodes[id] = LabelFromAttributes(node.Groups[2].Value) ?? id;
            }
        }

        if (!started || nodes.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            true,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges);
        return true;
    }

    private static string? LabelFromAttributes(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var match = Regex.Match(
            raw,
            @"label\s*=\s*""?([^""\]]+)""?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var value = match.Success ? match.Groups[1].Value.Trim() : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
