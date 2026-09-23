namespace MarkdownMkII.Core.Text;

public sealed record LaidOutNode(string Id, string Label, double X, double Y, double Width, double Height);

public sealed record LaidOutEdge(
    string From,
    string To,
    string? Label,
    double X1,
    double Y1,
    double X2,
    double Y2,
    bool Dashed = false);

public sealed record PieSliceLayout(
    string Label,
    double Value,
    double Fraction,
    double StartDegrees,
    double SweepDegrees);

public sealed record GraphLayout(
    double Width,
    double Height,
    IReadOnlyList<LaidOutNode> Nodes,
    IReadOnlyList<LaidOutEdge> Edges);

public static class GraphLayoutEngine
{
    public static GraphLayout Layered(
        IReadOnlyList<(string Id, string Label)> nodes,
        IReadOnlyList<(string From, string To, string? Label)> edges,
        bool horizontal = false,
        double nodeWidth = 140,
        double nodeHeight = 36,
        double gap = 36)
    {
        if (nodes.Count == 0)
        {
            return new GraphLayout(0, 0, [], []);
        }

        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < nodes.Count; i++)
        {
            index[nodes[i].Id] = i;
        }

        var incoming = new int[nodes.Count];
        var outgoing = Enumerable.Range(0, nodes.Count).Select(_ => new List<int>()).ToArray();
        foreach (var edge in edges)
        {
            if (!index.TryGetValue(edge.From, out var from) || !index.TryGetValue(edge.To, out var to) || from == to)
            {
                continue;
            }

            incoming[to]++;
            outgoing[from].Add(to);
        }

        var layer = new int[nodes.Count];
        Array.Fill(layer, -1);
        var queue = new Queue<int>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (incoming[i] == 0)
            {
                layer[i] = 0;
                queue.Enqueue(i);
            }
        }

        if (queue.Count == 0)
        {
            layer[0] = 0;
            queue.Enqueue(0);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in outgoing[current])
            {
                var candidate = layer[current] + 1;
                if (candidate > layer[next] && candidate < nodes.Count)
                {
                    layer[next] = candidate;
                    queue.Enqueue(next);
                }
            }
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (layer[i] < 0)
            {
                layer[i] = 0;
            }
        }

        var maxLayer = layer.Max();
        var columns = new List<int>[maxLayer + 1];
        for (var i = 0; i <= maxLayer; i++)
        {
            columns[i] = [];
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            columns[layer[i]].Add(i);
        }

        var laid = new LaidOutNode[nodes.Count];
        for (var depth = 0; depth <= maxLayer; depth++)
        {
            var column = columns[depth];
            for (var slot = 0; slot < column.Count; slot++)
            {
                var i = column[slot];
                var x = horizontal ? depth * (nodeWidth + gap) : slot * (nodeWidth + gap);
                var y = horizontal ? slot * (nodeHeight + gap) : depth * (nodeHeight + gap);
                laid[i] = new LaidOutNode(nodes[i].Id, nodes[i].Label, x, y, nodeWidth, nodeHeight);
            }
        }

        var laidEdges = new List<LaidOutEdge>();
        foreach (var edge in edges)
        {
            if (!index.TryGetValue(edge.From, out var from) || !index.TryGetValue(edge.To, out var to))
            {
                continue;
            }

            var a = laid[from];
            var b = laid[to];
            laidEdges.Add(new LaidOutEdge(
                edge.From,
                edge.To,
                edge.Label,
                a.X + a.Width / 2,
                a.Y + a.Height / 2,
                b.X + b.Width / 2,
                b.Y + b.Height / 2));
        }

        var width = laid.Max(node => node.X + node.Width) + 8;
        var height = laid.Max(node => node.Y + node.Height) + 8;
        return new GraphLayout(width, height, laid, laidEdges);
    }

    public static GraphLayout ForMermaid(MermaidDiagram diagram)
        => diagram.Kind switch
        {
            MermaidKind.Sequence => ForSequence(diagram),
            MermaidKind.Pie => new GraphLayout(0, 0, [], []),
            MermaidKind.Gantt => ForGantt(diagram),
            _ => Layered(
                diagram.Nodes.Select(node => (node.Id, node.Label)).ToList(),
                diagram.Edges.Select(edge => (edge.From, edge.To, edge.Label)).ToList(),
                diagram.Horizontal)
        };

    public static GraphLayout ForSequence(MermaidDiagram diagram)
    {
        var actors = diagram.Nodes;
        var messages = diagram.Messages ?? [];
        if (actors.Count == 0)
        {
            return new GraphLayout(0, 0, [], []);
        }

        const double actorWidth = 120;
        const double actorHeight = 32;
        const double gapX = 64;
        const double left = 16;
        const double top = 8;
        const double messageGap = 40;
        var laid = new LaidOutNode[actors.Count];
        var centerX = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < actors.Count; i++)
        {
            var x = left + i * (actorWidth + gapX);
            laid[i] = new LaidOutNode(actors[i].Id, actors[i].Label, x, top, actorWidth, actorHeight);
            centerX[actors[i].Id] = x + actorWidth / 2;
        }

        var edges = new List<LaidOutEdge>();
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            if (!centerX.TryGetValue(message.From, out var x1) || !centerX.TryGetValue(message.To, out var x2))
            {
                continue;
            }

            var y = top + actorHeight + 24 + i * messageGap;
            edges.Add(new LaidOutEdge(message.From, message.To, message.Text, x1, y, x2, y, message.Dashed));
        }

        var width = laid.Max(node => node.X + node.Width) + 8;
        var height = edges.Count == 0 ? top + actorHeight + 16 : edges.Max(edge => edge.Y1) + 28;
        return new GraphLayout(width, height, laid, edges);
    }

    public static IReadOnlyList<PieSliceLayout> ForPie(MermaidDiagram diagram)
    {
        var slices = (diagram.Slices ?? []).Where(slice => double.IsFinite(slice.Value) && slice.Value > 0).ToList();
        var scale = slices.Count == 0 ? 1 : slices.Max(slice => slice.Value);
        var total = slices.Sum(slice => slice.Value / scale);
        if (total <= 0)
        {
            return [];
        }

        var start = -90.0;
        var laid = new List<PieSliceLayout>(slices.Count);
        foreach (var slice in slices)
        {
            var fraction = slice.Value / scale / total;
            var sweep = fraction * 360.0;
            laid.Add(new PieSliceLayout(slice.Label, slice.Value, fraction, start, sweep));
            start += sweep;
        }

        return laid;
    }

    public static GraphLayout ForGantt(MermaidDiagram diagram)
    {
        var tasks = diagram.Tasks ?? [];
        if (tasks.Count == 0)
        {
            return new GraphLayout(0, 0, [], []);
        }

        const double left = 8;
        const double top = 8;
        const double scale = 22;
        const double rowHeight = 28;
        var laid = new List<LaidOutNode>(tasks.Count);
        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            laid.Add(new LaidOutNode(
                task.Label,
                string.IsNullOrWhiteSpace(task.Section) ? task.Label : task.Section + " · " + task.Label,
                left + task.Start * scale,
                top + i * rowHeight,
                Math.Max(24, task.Duration * scale),
                22));
        }

        var width = laid.Max(node => node.X + node.Width) + 16;
        var height = laid.Max(node => node.Y + node.Height) + 12;
        return new GraphLayout(width, height, laid, []);
    }
}
