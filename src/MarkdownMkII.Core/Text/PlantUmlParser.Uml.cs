using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Text;

public static partial class PlantUmlParser
{
    private static bool LooksLikeClass(string code)
        => code.Contains("class ", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("<|--", StringComparison.Ordinal) ||
           code.Contains("--|>", StringComparison.Ordinal);

    private static bool LooksLikeActivity(string code)
    {
        var hasStart = false;
        var hasStep = false;
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Equals("start", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                hasStart = true;
            }
            else if (ActivityStep.IsMatch(line))
            {
                hasStep = true;
            }
        }

        return hasStart && hasStep;
    }

    private static bool TryParseSequence(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Sequence);
        var actors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var messages = new List<MermaidSequenceMessage>();
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("'", StringComparison.Ordinal) ||
                line.StartsWith("title ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("participant ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("actor ", StringComparison.OrdinalIgnoreCase))
            {
                if (line.StartsWith("participant ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("actor ", StringComparison.OrdinalIgnoreCase))
                {
                    var name = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim('"');
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        actors.TryAdd(name, name);
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
            var arrow = match.Groups[2].Value;
            if (arrow is "<-" or "<--")
            {
                (from, to) = (to, from);
            }

            actors.TryAdd(from, from);
            actors.TryAdd(to, to);
            var dashed = arrow.Contains("--", StringComparison.Ordinal);
            var text = match.Groups[4].Success ? match.Groups[4].Value.Trim() : string.Empty;
            messages.Add(new MermaidSequenceMessage(from, to, text, dashed));
        }

        if (actors.Count == 0 || messages.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            false,
            actors.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            [],
            MermaidKind.Sequence,
            messages);
        return true;
    }

    private static bool TryParseClass(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Class);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("'", StringComparison.Ordinal) ||
                line is "{" or "}")
            {
                continue;
            }

            var relation = ClassArrow.Match(line);
            if (relation.Success)
            {
                var from = relation.Groups[1].Value;
                var to = relation.Groups[3].Value;
                nodes.TryAdd(from, from);
                nodes.TryAdd(to, to);
                edges.Add(new MermaidEdge(from, to, relation.Groups[2].Value));
                continue;
            }

            var decl = ClassDecl.Match(line);
            if (decl.Success)
            {
                nodes.TryAdd(decl.Groups[1].Value, decl.Groups[1].Value);
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
            MermaidKind.Class);
        return true;
    }

    private static bool TryParseActivity(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], []);
        var nodes = new List<MermaidNode>();
        var edges = new List<MermaidEdge>();
        string? previous = null;
        var index = 0;
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("'", StringComparison.Ordinal) ||
                line.StartsWith("title ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? id = null;
            string? label = null;
            if (line.Equals("start", StringComparison.OrdinalIgnoreCase))
            {
                id = "start";
                label = "start";
            }
            else if (line.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
                     line.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                id = "stop";
                label = "stop";
            }
            else
            {
                var step = ActivityStep.Match(line);
                if (!step.Success)
                {
                    continue;
                }

                id = "s" + index;
                label = step.Groups[1].Value.Trim();
                index++;
            }

            nodes.Add(new MermaidNode(id, label));
            if (previous is not null)
            {
                edges.Add(new MermaidEdge(previous, id, null));
            }

            previous = id;
        }

        if (nodes.Count < 2)
        {
            return false;
        }

        diagram = new MermaidDiagram(false, nodes, edges);
        return true;
    }

    private static bool LooksLikeComponent(string code)
        => code.Contains("component ", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("interface ", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("node ", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("cloud ", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("object ", StringComparison.OrdinalIgnoreCase) ||
           (code.Contains('[', StringComparison.Ordinal) &&
            (code.Contains("->", StringComparison.Ordinal) || code.Contains("-->", StringComparison.Ordinal)));

    private static readonly Regex ComponentArrow = new(
        @"^\s*(?:component\s+)?\[([^\]]+)\]\s*(-->|->)\s*\[([^\]]+)\]\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ComponentDecl = new(
        @"^\s*(?:component|interface|database|node|cloud|actor|object)\s+""?([A-Za-z][\w ]*)""?\s*$",
        RegexOptions.CultureInvariant);

    private static bool TryParseComponent(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Class);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("'", StringComparison.Ordinal) ||
                line.StartsWith("title ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("package ", StringComparison.OrdinalIgnoreCase) ||
                line is "{" or "}")
            {
                continue;
            }

            var arrow = ComponentArrow.Match(line);
            if (arrow.Success)
            {
                var from = ComponentId(arrow.Groups[1].Value);
                var to = ComponentId(arrow.Groups[3].Value);
                nodes[from] = arrow.Groups[1].Value.Trim();
                nodes[to] = arrow.Groups[3].Value.Trim();
                edges.Add(new MermaidEdge(from, to, null));
                continue;
            }

            var relation = ClassArrow.Match(line);
            if (relation.Success)
            {
                var from = ComponentId(relation.Groups[1].Value);
                var to = ComponentId(relation.Groups[3].Value);
                nodes.TryAdd(from, relation.Groups[1].Value);
                nodes.TryAdd(to, relation.Groups[3].Value);
                edges.Add(new MermaidEdge(from, to, relation.Groups[2].Value));
                continue;
            }

            var decl = ComponentDecl.Match(line);
            if (decl.Success)
            {
                var id = ComponentId(decl.Groups[1].Value);
                nodes.TryAdd(id, decl.Groups[1].Value.Trim());
            }
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            true,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges,
            MermaidKind.Class);
        return true;
    }

    private static string ComponentId(string label)
    {
        var chars = (label ?? string.Empty).Trim().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        var id = new string(chars).Trim('_');
        if (id.Length == 0 || !char.IsLetter(id[0]))
        {
            id = "c" + id;
        }

        return id;
    }

    private static bool LooksLikeDeployment(string code)
        => code.Contains("artifact ", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("folder ", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("queue ", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("storage ", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("frame ", StringComparison.OrdinalIgnoreCase);

    private static readonly Regex DeploymentDecl = new(
        @"^\s*(?:node|cloud|database|artifact|folder|queue|storage|frame|actor)\s+""?([A-Za-z][\w ]*)""?\s*$",
        RegexOptions.CultureInvariant);

    private static bool TryParseDeployment(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("'", StringComparison.Ordinal) ||
                line.StartsWith("title ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("package ", StringComparison.OrdinalIgnoreCase) ||
                line is "{" or "}")
            {
                continue;
            }

            var arrow = ComponentArrow.Match(line);
            if (arrow.Success)
            {
                var from = ComponentId(arrow.Groups[1].Value);
                var to = ComponentId(arrow.Groups[3].Value);
                nodes[from] = arrow.Groups[1].Value.Trim();
                nodes[to] = arrow.Groups[3].Value.Trim();
                edges.Add(new MermaidEdge(from, to, null));
                continue;
            }

            var relation = ClassArrow.Match(line);
            if (relation.Success)
            {
                var from = ComponentId(relation.Groups[1].Value);
                var to = ComponentId(relation.Groups[3].Value);
                nodes.TryAdd(from, relation.Groups[1].Value);
                nodes.TryAdd(to, relation.Groups[3].Value);
                edges.Add(new MermaidEdge(from, to, relation.Groups[2].Value));
                continue;
            }

            var decl = DeploymentDecl.Match(line);
            if (decl.Success)
            {
                var id = ComponentId(decl.Groups[1].Value);
                nodes.TryAdd(id, decl.Groups[1].Value.Trim());
            }
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            true,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges,
            MermaidKind.Flow);
        return true;
    }

    private static bool LooksLikeState(string code)
    {
        if (code.Contains("[*]", StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            return Regex.IsMatch(
                code,
                @"^\s*state\s+",
                RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(80));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static readonly Regex StateArrow = new(
        @"^\s*(\[\*\]|[A-Za-z][\w]*)\s*(-->|->)\s*(\[\*\]|[A-Za-z][\w]*)(?:\s*:\s*(.+))?$",
        RegexOptions.CultureInvariant);

    private static bool TryParseState(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("'", StringComparison.Ordinal) ||
                line.StartsWith("title ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("state ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var arrow = StateArrow.Match(line);
            if (!arrow.Success)
            {
                continue;
            }

            var fromLabel = arrow.Groups[1].Value.Trim();
            var toLabel = arrow.Groups[3].Value.Trim();
            var from = fromLabel == "[*]" ? "start" : ComponentId(fromLabel);
            var to = toLabel == "[*]" ? "end" : ComponentId(toLabel);
            nodes.TryAdd(from, fromLabel == "[*]" ? "start" : fromLabel);
            nodes.TryAdd(to, toLabel == "[*]" ? "end" : toLabel);
            var label = arrow.Groups[4].Success ? arrow.Groups[4].Value.Trim() : null;
            edges.Add(new MermaidEdge(from, to, string.IsNullOrEmpty(label) ? null : label));
        }

        if (nodes.Count < 2 || edges.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            true,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges,
            MermaidKind.Flow);
        return true;
    }

    private static bool LooksLikeUsecase(string code)
    {
        if (code.Contains("actor ", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("usecase ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            return code.Contains('(') &&
                   (code.Contains("->", StringComparison.Ordinal) || code.Contains("-->", StringComparison.Ordinal)) &&
                   Regex.IsMatch(
                       code,
                       @"\([A-Za-z][^)\n]*\)",
                       RegexOptions.CultureInvariant,
                       TimeSpan.FromMilliseconds(80));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static readonly Regex UsecaseArrow = new(
        @"^\s*(?:\(([^\)]+)\)|([A-Za-z][\w]*))\s*(-->|->)\s*(?:\(([^\)]+)\)|([A-Za-z][\w]*))\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex UsecaseActor = new(
        @"^\s*actor\s+""?([A-Za-z][\w ]*)""?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static bool TryParseUsecase(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Class);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("'", StringComparison.Ordinal) ||
                line.StartsWith("title ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("usecase ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var actor = UsecaseActor.Match(line);
            if (actor.Success)
            {
                var label = actor.Groups[1].Value.Trim();
                nodes.TryAdd(ComponentId(label), label);
                continue;
            }

            var arrow = UsecaseArrow.Match(line);
            if (!arrow.Success)
            {
                continue;
            }

            var fromLabel = arrow.Groups[1].Success ? arrow.Groups[1].Value.Trim() : arrow.Groups[2].Value.Trim();
            var toLabel = arrow.Groups[4].Success ? arrow.Groups[4].Value.Trim() : arrow.Groups[5].Value.Trim();
            if (fromLabel.Length == 0 || toLabel.Length == 0)
            {
                continue;
            }

            var from = ComponentId(fromLabel);
            var to = ComponentId(toLabel);
            nodes.TryAdd(from, fromLabel);
            nodes.TryAdd(to, toLabel);
            edges.Add(new MermaidEdge(from, to, null));
        }

        if (nodes.Count < 2 || edges.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            true,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges,
            MermaidKind.Class);
        return true;
    }
}
