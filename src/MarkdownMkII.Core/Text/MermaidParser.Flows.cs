namespace MarkdownMkII.Core.Text;

public static partial class MermaidParser
{
    private static bool TryParseFlow(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], []);
        var horizontal = false;
        var started = false;
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            if (!started)
            {
                var header = Header.Match(line);
                if (!header.Success)
                {
                    return false;
                }

                var dir = header.Groups[1].Value.ToUpperInvariant();
                horizontal = dir is "LR" or "RL";
                started = true;
                continue;
            }

            var edge = Edge.Match(line);
            if (edge.Success)
            {
                var from = edge.Groups[1].Value;
                var to = edge.Groups[3].Value;
                EnsureNode(nodes, from, ExtractLabel(line, from));
                EnsureNode(nodes, to, ExtractLabel(line, to, from.Length));
                var label = edge.Groups[2].Success ? edge.Groups[2].Value.Trim() : null;
                edges.Add(new MermaidEdge(from, to, string.IsNullOrEmpty(label) ? null : label));
                continue;
            }

            var node = Node.Match(line);
            if (node.Success)
            {
                var id = node.Groups[1].Value;
                var label = First(node.Groups[2].Value, node.Groups[3].Value, node.Groups[4].Value, node.Groups[5].Value);
                EnsureNode(nodes, id, string.IsNullOrWhiteSpace(label) ? id : label.Trim());
            }
        }

        if (!started || nodes.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            horizontal,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges);
        return true;
    }

    private static bool TryParseSequence(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Sequence);
        var actors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var messages = new List<MermaidSequenceMessage>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("%%", StringComparison.Ordinal) ||
                line.StartsWith("sequenceDiagram", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("participant ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Note ", StringComparison.OrdinalIgnoreCase))
            {
                if (line.StartsWith("participant ", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = line["participant ".Length..].Trim();
                    var asIndex = rest.IndexOf(" as ", StringComparison.OrdinalIgnoreCase);
                    var id = asIndex < 0 ? rest : rest[..asIndex].Trim();
                    var label = asIndex < 0 ? rest : rest[(asIndex + 4)..].Trim();
                    if (id.Length > 0)
                    {
                        actors[id] = label.Length == 0 ? id : label;
                    }
                }

                continue;
            }

            var match = SequenceArrow.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var from = match.Groups[1].Value;
            var to = match.Groups[3].Value;
            var dashed = match.Groups[2].Value.Contains("--", StringComparison.Ordinal);
            actors.TryAdd(from, from);
            actors.TryAdd(to, to);
            messages.Add(new MermaidSequenceMessage(from, to, match.Groups[4].Value.Trim(), dashed));
        }

        if (messages.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            true,
            actors.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            [],
            MermaidKind.Sequence,
            messages);
        return true;
    }

    private static bool TryParseState(string[] lines, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], []);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("%%", StringComparison.Ordinal) ||
                line.StartsWith("stateDiagram", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = StateEdge.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var from = NormalizeState(match.Groups[1].Value, isTarget: false);
            var to = NormalizeState(match.Groups[3].Value, isTarget: true);
            nodes.TryAdd(from, from is "_start" or "_end" ? "[*]" : from);
            nodes.TryAdd(to, to is "_start" or "_end" ? "[*]" : to);
            var label = match.Groups[2].Success && match.Groups[2].Value.Length > 0
                ? match.Groups[2].Value.Trim()
                : match.Groups[4].Success ? match.Groups[4].Value.Trim() : null;
            edges.Add(new MermaidEdge(from, to, string.IsNullOrWhiteSpace(label) ? null : label));
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            false,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges);
        return true;
    }

    private static string NormalizeState(string id, bool isTarget)
        => id == "[*]" ? (isTarget ? "_end" : "_start") : id;
}
