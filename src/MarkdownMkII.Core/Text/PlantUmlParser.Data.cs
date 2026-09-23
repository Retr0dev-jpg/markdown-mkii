using System.Text.Json;

namespace MarkdownMkII.Core.Text;

public static partial class PlantUmlParser
{
    private static bool LooksLikeJson(string code)
        => code.Contains("@startjson", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startjson", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseJson(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var json = ExtractJson(code);
        if (json is null)
        {
            return false;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return false;
        }

        using (doc)
        {
            var nodes = new List<MermaidNode>();
            var edges = new List<MermaidEdge>();
            var index = 0;
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    WalkJson(prop.Value, null, prop.Name, nodes, edges, ref index);
                }
            }
            else
            {
                WalkJson(root, null, JsonPrimitive(root), nodes, edges, ref index);
            }

            if (nodes.Count == 0)
            {
                return false;
            }

            diagram = new MermaidDiagram(false, nodes, edges, MermaidKind.Flow);
            return true;
        }
    }

    private static string? ExtractJson(string code)
    {
        var startObj = code.IndexOf('{');
        var endObj = code.LastIndexOf('}');
        if (startObj >= 0 && endObj > startObj)
        {
            return code[startObj..(endObj + 1)];
        }

        var startArr = code.IndexOf('[');
        var endArr = code.LastIndexOf(']');
        if (startArr >= 0 && endArr > startArr)
        {
            return code[startArr..(endArr + 1)];
        }

        return null;
    }

    private static void WalkJson(
        JsonElement element,
        string? parentId,
        string label,
        List<MermaidNode> nodes,
        List<MermaidEdge> edges,
        ref int index)
    {
        var id = "j" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        index++;
        if (element.ValueKind == JsonValueKind.Object)
        {
            nodes.Add(new MermaidNode(id, label));
            if (parentId is not null)
            {
                edges.Add(new MermaidEdge(parentId, id, null));
            }

            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    WalkJson(prop.Value, id, prop.Name, nodes, edges, ref index);
                }
                else
                {
                    var childId = "j" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    index++;
                    nodes.Add(new MermaidNode(childId, prop.Name + ": " + JsonPrimitive(prop.Value)));
                    edges.Add(new MermaidEdge(id, childId, null));
                }
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            nodes.Add(new MermaidNode(id, label));
            if (parentId is not null)
            {
                edges.Add(new MermaidEdge(parentId, id, null));
            }

            var itemIndex = 0;
            foreach (var item in element.EnumerateArray())
            {
                var itemLabel = item.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                    ? "[" + itemIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]"
                    : JsonPrimitive(item);
                WalkJson(item, id, itemLabel, nodes, edges, ref index);
                itemIndex++;
            }

            return;
        }

        nodes.Add(new MermaidNode(id, label.Length > 0 ? label : JsonPrimitive(element)));
        if (parentId is not null)
        {
            edges.Add(new MermaidEdge(parentId, id, null));
        }
    }

    private static string JsonPrimitive(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };

    private static bool LooksLikeYaml(string code)
        => code.Contains("@startyaml", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startyaml", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseYaml(string code, out MermaidDiagram diagram)
    {
        diagram = new MermaidDiagram(false, [], [], MermaidKind.Flow);
        var nodes = new List<MermaidNode>();
        var edges = new List<MermaidEdge>();
        var stack = new List<(int Indent, string Id)>();
        var index = 0;
        foreach (var raw in Split(code))
        {
            if (raw.Length == 0)
            {
                continue;
            }

            var indent = 0;
            while (indent < raw.Length && (raw[indent] == ' ' || raw[indent] == '\t'))
            {
                indent++;
            }

            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("#", StringComparison.Ordinal) ||
                line.StartsWith("@startyaml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endyaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().Trim('"', '\'');
            if (key.Length == 0)
            {
                continue;
            }

            var label = value.Length == 0 ? key : key + ": " + value;
            var id = "y" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
            nodes.Add(new MermaidNode(id, label));
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

        diagram = new MermaidDiagram(false, nodes, edges, MermaidKind.Flow);
        return true;
    }

    private static bool LooksLikeMindmap(string code)
        => code.Contains("@startmindmap", StringComparison.OrdinalIgnoreCase) ||
           code.Contains("startmindmap", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseMindmap(string code, out MermaidDiagram diagram)
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
                line.StartsWith("@startmindmap", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@endmindmap", StringComparison.OrdinalIgnoreCase) ||
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

            var id = "m" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
}
