using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Text;

public static partial class PlantUmlParser
{
    private static readonly Regex SequenceArrow = new(
        @"^\s*([A-Za-z][\w]*)\s*(->|-->|<-|<--)\s*([A-Za-z][\w]*)\s*(?::\s*(.+))?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ClassArrow = new(
        @"^\s*([A-Za-z][\w]*)\s*(<\|--|--\|>|--\*|\*--|o--|--o|-->|<--|<\.\.|\.\.>|--)\s*([A-Za-z][\w]*)\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ClassDecl = new(
        @"^\s*class\s+([A-Za-z][\w]*)\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex ActivityStep = new(
        @"^\s*:([^;]+);\s*$",
        RegexOptions.CultureInvariant);

    public static bool TryParse(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Sequence);
        code ??= string.Empty;
        if (LooksLikeClass(code) && TryParseClass(code, out diagram))
        {
            return true;
        }

        if (LooksLikeBpmn(code) && TryParseBpmn(code, out diagram))
        {
            return true;
        }

        if (LooksLikeActivity(code) && TryParseActivity(code, out diagram))
        {
            return true;
        }

        if (LooksLikeState(code) && TryParseState(code, out diagram))
        {
            return true;
        }

        if (LooksLikeUsecase(code) && TryParseUsecase(code, out diagram))
        {
            return true;
        }

        if (LooksLikeDeployment(code) && TryParseDeployment(code, out diagram))
        {
            return true;
        }

        if (LooksLikeTiming(code) && TryParseTiming(code, out diagram))
        {
            return true;
        }

        if (LooksLikeNwdiag(code) && TryParseNwdiag(code, out diagram))
        {
            return true;
        }

        if (LooksLikeSalt(code) && TryParseSalt(code, out diagram))
        {
            return true;
        }

        if (LooksLikeWbs(code) && TryParseWbs(code, out diagram))
        {
            return true;
        }

        if (LooksLikeGantt(code) && TryParseGantt(code, out diagram))
        {
            return true;
        }

        if (LooksLikeJson(code) && TryParseJson(code, out diagram))
        {
            return true;
        }

        if (LooksLikeYaml(code) && TryParseYaml(code, out diagram))
        {
            return true;
        }

        if (LooksLikeMindmap(code) && TryParseMindmap(code, out diagram))
        {
            return true;
        }

        if (LooksLikeDitaa(code) && TryParseDitaa(code, out diagram))
        {
            return true;
        }

        if (LooksLikeRegex(code) && TryParseRegex(code, out diagram))
        {
            return true;
        }

        if (LooksLikeEbnf(code) && TryParseEbnf(code, out diagram))
        {
            return true;
        }

        if (LooksLikeArchimate(code) && TryParseArchimate(code, out diagram))
        {
            return true;
        }

        if (LooksLikeChen(code) && TryParseChen(code, out diagram))
        {
            return true;
        }

        if (LooksLikeIe(code) && TryParseIe(code, out diagram))
        {
            return true;
        }

        if (LooksLikeMath(code) && TryParseMath(code, out diagram))
        {
            return true;
        }

        if (LooksLikeLatex(code) && TryParseLatex(code, out diagram))
        {
            return true;
        }

        if (LooksLikeChronology(code) && TryParseChronology(code, out diagram))
        {
            return true;
        }

        if (LooksLikeSdl(code) && TryParseSdl(code, out diagram))
        {
            return true;
        }

        if (LooksLikeBoard(code) && TryParseBoard(code, out diagram))
        {
            return true;
        }

        if (LooksLikeGitUml(code) && TryParseGitUml(code, out diagram))
        {
            return true;
        }

        if (LooksLikeFiles(code) && TryParseFiles(code, out diagram))
        {
            return true;
        }

        if (LooksLikeJcckit(code) && TryParseJcckit(code, out diagram))
        {
            return true;
        }

        if (LooksLikeWireviz(code) && TryParseWireviz(code, out diagram))
        {
            return true;
        }

        if (LooksLikeProject(code) && TryParseProject(code, out diagram))
        {
            return true;
        }

        foreach (var (marker, idPrefix) in GenericDialects)
        {
            if (code.Contains(marker, StringComparison.OrdinalIgnoreCase) &&
                TryParseGenericFlow(code, idPrefix, out diagram))
            {
                return true;
            }
        }

        if (LooksLikeComponent(code) && TryParseComponent(code, out diagram))
        {
            return true;
        }

        return TryParseSequence(code, out diagram);
    }

    private static bool LooksLikeTiming(string code)
        => code.Contains("robust ", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("concise ", StringComparison.OrdinalIgnoreCase);

    private static readonly Regex TimingActor = new(
        @"^\s*(?:robust|concise)\s+(?:""([^""]+)""|([A-Za-z][\w]*))(?:\s+as\s+([A-Za-z][\w]*))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TimingState = new(
        @"^\s*([A-Za-z][\w]*)\s+is\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool TryParseTiming(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(true, [], [], MermaidKind.Sequence);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        string? previous = null;
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("'", StringComparison.Ordinal) ||
                line.StartsWith("@"))
            {
                continue;
            }

            var actor = TimingActor.Match(line);
            if (actor.Success)
            {
                var label = actor.Groups[1].Success ? actor.Groups[1].Value.Trim() : actor.Groups[2].Value.Trim();
                var id = actor.Groups[3].Success && actor.Groups[3].Value.Length > 0
                    ? actor.Groups[3].Value.Trim()
                    : ComponentId(label);
                if (id.Length == 0)
                {
                    continue;
                }

                nodes.TryAdd(id, string.IsNullOrWhiteSpace(label) ? id : label);
                if (previous is not null)
                {
                    edges.Add(new MermaidEdge(previous, id, null));
                }

                previous = id;
                continue;
            }

            var state = TimingState.Match(line);
            if (!state.Success)
            {
                continue;
            }

            var actorId = state.Groups[1].Value.Trim();
            nodes.TryAdd(actorId, actorId);
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(
            true,
            nodes.Select(pair => new MermaidNode(pair.Key, pair.Value)).ToList(),
            edges,
            MermaidKind.Sequence);
        return true;
    }

    private static bool LooksLikeNwdiag(string code)
        => code.Contains("nwdiag", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseNwdiag(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        string? previousInNetwork = null;
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("'", StringComparison.Ordinal) ||
                line.StartsWith("nwdiag", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("network ", StringComparison.OrdinalIgnoreCase) ||
                line is "{" or "}")
            {
                if (line.StartsWith("network ", StringComparison.OrdinalIgnoreCase) || line == "}")
                {
                    previousInNetwork = null;
                }

                continue;
            }

            var name = line.TrimEnd(';').Trim();
            if (name.Length == 0 || !char.IsLetter(name[0]))
            {
                continue;
            }

            var id = ComponentId(name);
            nodes.TryAdd(id, name);
            if (previousInNetwork is not null)
            {
                edges.Add(new MermaidEdge(previousInNetwork, id, null));
            }

            previousInNetwork = id;
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

    private static bool LooksLikeSalt(string code)
        => code.Contains("@startsalt", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startsalt", StringComparison.OrdinalIgnoreCase);

    private static readonly Regex SaltWidget = new(
        @"\[([^\]]+)\]",
        RegexOptions.CultureInvariant);

    private static bool TryParseSalt(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        string? previous = null;
        var index = 0;
        foreach (Match match in SaltWidget.Matches(code ?? string.Empty))
        {
            var label = match.Groups[1].Value.Trim();
            if (label.Length == 0)
            {
                continue;
            }

            var id = "w" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
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

    private static bool LooksLikeWbs(string code)
        => code.Contains("@startwbs", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startwbs", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseWbs(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new List<MermaidNode>();
        var edges = new List<MermaidEdge>();
        var stack = new List<(int Level, string Id)>();
        var index = 0;
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startwbs", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endwbs", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("'", StringComparison.Ordinal) ||
                line.StartsWith("title ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var level = 0;
            while (level < line.Length && line[level] == '*')
            {
                level++;
            }

            if (level == 0)
            {
                continue;
            }

            var label = line[level..].Trim();
            if (label.Length == 0)
            {
                continue;
            }

            var id = "b" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
            nodes.Add(new MermaidNode(id, label));
            while (stack.Count > 0 && stack[^1].Level >= level)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            if (stack.Count > 0)
            {
                edges.Add(new MermaidEdge(stack[^1].Id, id, null));
            }

            stack.Add((level, id));
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        diagram = new MermaidDiagram(false, nodes, edges, MermaidKind.Flow);
        return true;
    }

    private static bool LooksLikeGantt(string code)
        => code.Contains("@startgantt", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startgantt", StringComparison.OrdinalIgnoreCase);

    private static readonly Regex GanttTask = new(
        @"\[([^\]]+)\]",
        RegexOptions.CultureInvariant);

    private static bool TryParseGantt(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MermaidEdge>();
        string? previous = null;
        var index = 0;
        foreach (Match match in GanttTask.Matches(code ?? string.Empty))
        {
            var label = match.Groups[1].Value.Trim();
            if (label.Length == 0)
            {
                continue;
            }

            var id = "g" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
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

    private static bool LooksLikeFiles(string code)
        => code.Contains("@startfiles", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startfiles", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeJcckit(string code)
        => code.Contains("@startjcckit", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startjcckit", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeWireviz(string code)
        => code.Contains("@startwireviz", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startwireviz", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeProject(string code)
        => code.Contains("@startproject", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startproject", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseFiles(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new List<MermaidNode>();
        var edges = new List<MermaidEdge>();
        string? previous = null;
        var index = 0;
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.StartsWith("file ", StringComparison.OrdinalIgnoreCase))
            {
                line = line["file ".Length..].Trim();
            }

            if (line.Length == 0 ||
                line.StartsWith("@startfiles", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endfiles", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("title", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hasLetter = false;
            foreach (var ch in line)
            {
                if (char.IsLetter(ch))
                {
                    hasLetter = true;
                    break;
                }
            }

            if (!hasLetter)
            {
                continue;
            }

            var id = "z" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
            nodes.Add(new MermaidNode(id, line));
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

        diagram = new MermaidDiagram(false, nodes, edges, MermaidKind.Flow);
        return true;
    }

    private static bool TryParseJcckit(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new List<MermaidNode>();
        var edges = new List<MermaidEdge>();
        string? previous = null;
        var index = 0;
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@start", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@end", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("title", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hasLetter = false;
            foreach (var ch in line)
            {
                if (char.IsLetter(ch))
                {
                    hasLetter = true;
                    break;
                }
            }

            if (!hasLetter)
            {
                continue;
            }

            var id = "q" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
            nodes.Add(new MermaidNode(id, line));
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

        diagram = new MermaidDiagram(false, nodes, edges, MermaidKind.Flow);
        return true;
    }

    private static bool TryParseWireviz(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new List<MermaidNode>();
        var edges = new List<MermaidEdge>();
        string? previous = null;
        var index = 0;
        foreach (var raw in Split(code))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("@start", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@end", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("title", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hasLetter = false;
            foreach (var ch in line)
            {
                if (char.IsLetter(ch))
                {
                    hasLetter = true;
                    break;
                }
            }

            if (!hasLetter)
            {
                continue;
            }

            var id = "y" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
            nodes.Add(new MermaidNode(id, line));
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

        diagram = new MermaidDiagram(false, nodes, edges, MermaidKind.Flow);
        return true;
    }

    private static bool TryParseProject(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new List<MermaidNode>();
        var edges = new List<MermaidEdge>();
        string? previous = null;
        var index = 0;
        foreach (var raw in Split(code))
        {
            var line = raw.Trim().TrimStart('[', ']').Trim();
            if (line.Length == 0 ||
                line.StartsWith("@start", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@end", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("title", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hasLetter = false;
            foreach (var ch in line)
            {
                if (char.IsLetter(ch))
                {
                    hasLetter = true;
                    break;
                }
            }

            if (!hasLetter)
            {
                continue;
            }

            var id = "o" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
            nodes.Add(new MermaidNode(id, line));
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

        diagram = new MermaidDiagram(false, nodes, edges, MermaidKind.Flow);
        return true;
    }

    private static string[] Split(string code)
        => code.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}
