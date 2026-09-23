namespace MarkdownMkII.Core.Text;

public static partial class PlantUmlParser
{
    private static bool LooksLikeDitaa(string code)
        => code.Contains("@startditaa", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startditaa", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseDitaa(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startditaa", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endditaa", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!line.StartsWith('|') || line.LastIndexOf('|') <= 0)
            {
                continue;
            }

            var inner = line.Trim('|').Trim();
            if (inner.Length == 0 || inner.All(static ch => ch is '-' or '+' or '=' or ' '))
            {
                continue;
            }

            var hasLetter = false;
            foreach (var ch in inner)
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

            var id = "d" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
            nodes.Add(new MermaidNode(id, inner));
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

    private static bool LooksLikeRegex(string code)
        => code.Contains("@startregex", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startregex", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseRegex(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startregex", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endregex", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("title", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var token in line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var label = token.Trim();
                var hasLetter = false;
                foreach (var ch in label)
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

                var id = "r" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                index++;
                nodes.Add(new MermaidNode(id, label));
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

        diagram = new MermaidDiagram(false, nodes, edges, MermaidKind.Flow);
        return true;
    }

    private static bool LooksLikeEbnf(string code)
        => code.Contains("@startebnf", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startebnf", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseEbnf(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startebnf", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endebnf", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("title", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var token in line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var label = token.Trim().Trim('"', '\'', ';');
                var hasLetter = false;
                foreach (var ch in label)
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

                var id = "e" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                index++;
                nodes.Add(new MermaidNode(id, label));
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

        diagram = new MermaidDiagram(false, nodes, edges, MermaidKind.Flow);
        return true;
    }

    private static bool LooksLikeArchimate(string code)
        => code.Contains("archimate", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseArchimate(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                !line.Contains("archimate", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var quote = line.IndexOf('"');
            var end = line.LastIndexOf('"');
            var label = quote >= 0 && end > quote
                ? line[(quote + 1)..end].Trim()
                : string.Empty;
            var hasLetter = false;
            foreach (var ch in label)
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

            var id = "a" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
            nodes.Add(new MermaidNode(id, label));
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

    private static bool LooksLikeChen(string code)
        => code.Contains("@startchen", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startchen", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseChen(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startchen", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endchen", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                !line.StartsWith("entity ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var label = line["entity ".Length..].Trim();
            var brace = label.IndexOf('{');
            if (brace >= 0)
            {
                label = label[..brace].Trim();
            }

            var hasLetter = false;
            foreach (var ch in label)
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

            var id = "c" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
            nodes.Add(new MermaidNode(id, label));
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

    private static bool LooksLikeIe(string code)
        => code.Contains("@startie", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startie", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseIe(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startie", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endie", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@enduml", StringComparison.OrdinalIgnoreCase) ||
                !line.StartsWith("entity ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var label = line["entity ".Length..].Trim();
            var brace = label.IndexOf('{');
            if (brace >= 0)
            {
                label = label[..brace].Trim();
            }

            var hasLetter = false;
            foreach (var ch in label)
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

            var id = "i" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
            nodes.Add(new MermaidNode(id, label));
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

    private static bool LooksLikeMath(string code)
        => code.Contains("@startmath", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startmath", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseMath(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startmath", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endmath", StringComparison.OrdinalIgnoreCase) ||
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

            var id = "x" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

    private static bool LooksLikeLatex(string code)
        => code.Contains("@startlatex", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startlatex", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseLatex(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startlatex", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endlatex", StringComparison.OrdinalIgnoreCase) ||
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

            var id = "u" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

    private static bool LooksLikeChronology(string code)
        => code.Contains("@startchronology", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startchronology", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseChronology(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startchronology", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endchronology", StringComparison.OrdinalIgnoreCase) ||
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

            var id = "h" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

    private static bool LooksLikeSdl(string code)
        => code.Contains("@startsdl", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startsdl", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseSdl(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startsdl", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endsdl", StringComparison.OrdinalIgnoreCase) ||
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

            var id = "l" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

    private static bool LooksLikeBpmn(string code)
        => code.Contains("@startbpmn", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startbpmn", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseBpmn(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startbpmn", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endbpmn", StringComparison.OrdinalIgnoreCase) ||
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

            var id = "w" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

    private static bool LooksLikeBoard(string code)
        => code.Contains("@startboard", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startboard", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseBoard(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new List<MermaidNode>();
        var edges = new List<MermaidEdge>();
        string? previous = null;
        var index = 0;
        foreach (var raw in Split(code))
        {
            var line = raw.Trim().TrimStart('*').Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startboard", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endboard", StringComparison.OrdinalIgnoreCase) ||
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

            var id = "k" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

    private static bool LooksLikeGitUml(string code)
        => code.Contains("@startgit", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startgit", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseGitUml(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new List<MermaidNode>();
        var edges = new List<MermaidEdge>();
        string? previous = null;
        var index = 0;
        foreach (var raw in Split(code))
        {
            var line = raw.Trim().TrimStart('*', ':').Trim();
            if (line.Length == 0 ||
                line.StartsWith("@startgit", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endgit", StringComparison.OrdinalIgnoreCase) ||
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

            var id = "v" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
}
