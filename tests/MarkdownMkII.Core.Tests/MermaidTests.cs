using MarkdownMkII.Core.Preview;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class MermaidTests
{
    [Fact]
    public void Parses_Flowchart_And_Lays_Out()
    {
        Assert.True(MermaidParser.TryParse("""
            graph TD
            A[Start] --> B{Go?}
            B -->|yes| C[Done]
            """, out var diagram));
        Assert.False(diagram.Horizontal);
        Assert.Equal(3, diagram.Nodes.Count);
        Assert.Contains(diagram.Nodes, node => node.Label.Contains("Start", StringComparison.Ordinal));
        Assert.Contains(diagram.Edges, edge => edge.Label == "yes");
        var layout = GraphLayoutEngine.ForMermaid(diagram);
        Assert.Equal(3, layout.Nodes.Count);
        Assert.True(layout.Width > 0);
        Assert.True(layout.Height > layout.Nodes.Min(node => node.Height));
    }

    [Fact]
    public void Preview_Uses_Mermaid_Ir()
    {
        var parsed = DocumentParser.Parse("""
            ```mermaid
            flowchart LR
            A --> B
            ```
            """);
        Assert.Contains(parsed.Preview.Blocks, block => block is MermaidBlockIr);
    }

    [Fact]
    public void Parses_Sequence_And_Pie()
    {
        Assert.True(MermaidParser.TryParse("""
            sequenceDiagram
            participant A as Alice
            Alice->>Bob: Hello
            Bob-->>Alice: Hi
            """, out var sequence));
        Assert.Equal(MermaidKind.Sequence, sequence.Kind);
        Assert.NotNull(sequence.Messages);
        Assert.Equal(2, sequence.Messages!.Count);
        Assert.Contains(sequence.Messages, message => message.Dashed);
        var layout = GraphLayoutEngine.ForSequence(sequence);
        Assert.True(layout.Nodes.Count >= 2);
        Assert.Equal(2, layout.Edges.Count);
        Assert.Contains(layout.Edges, edge => edge.Dashed);

        Assert.True(MermaidParser.TryParse("""
            pie title Pets
            "Dogs": 386
            "Cats": 85
            """, out var pie));
        Assert.Equal(MermaidKind.Pie, pie.Kind);
        Assert.Equal("Pets", pie.Title);
        Assert.Equal(2, pie.Slices!.Count);
        var slices = GraphLayoutEngine.ForPie(pie);
        Assert.Equal(2, slices.Count);
        Assert.True(Math.Abs(slices.Sum(slice => slice.Fraction) - 1.0) < 0.0001);
    }

    [Fact]
    public void Preview_Uses_Sequence_And_Pie_Ir()
    {
        var sequence = DocumentParser.Parse("""
            ```mermaid
            sequenceDiagram
            Alice->>Bob: Hello
            ```
            """);
        Assert.Contains(sequence.Preview.Blocks, block => block is MermaidBlockIr mermaid && mermaid.Diagram.Kind == MermaidKind.Sequence);
        var pie = DocumentParser.Parse("""
            ```mermaid
            pie
            title Share
            "A": 40
            "B": 60
            ```
            """);
        Assert.Contains(pie.Preview.Blocks, block => block is MermaidBlockIr mermaid && mermaid.Diagram.Kind == MermaidKind.Pie && mermaid.Diagram.Title == "Share");
    }

    [Fact]
    public void Parses_State_And_Dot()
    {
        Assert.True(MermaidParser.TryParse("""
            stateDiagram-v2
            [*] --> Idle
            Idle --> Active : start
            """, out var state));
        Assert.Contains(state.Nodes, node => node.Label == "[*]" || node.Id == "_start");
        Assert.Contains(state.Edges, edge => edge.Label == "start");
        Assert.True(DotParser.TryParse("""
            digraph G {
            A -> B [label="go"]
            B -> C
            }
            """, out var dot));
        Assert.Equal(3, dot.Nodes.Count);
        Assert.Contains(dot.Edges, edge => edge.Label == "go");
        var parsed = DocumentParser.Parse("""
            ```dot
            digraph {
            A -> B
            }
            ```
            """);
        Assert.Contains(parsed.Preview.Blocks, block => block is MermaidBlockIr);
    }

    [Fact]
    public void Parses_Class_And_Gantt()
    {
        Assert.True(MermaidParser.TryParse("""
            classDiagram
            Animal <|-- Duck
            Animal : +int age
            """, out var diagram));
        Assert.Equal(MermaidKind.Class, diagram.Kind);
        Assert.Contains(diagram.Nodes, node => node.Id == "Animal" && node.Label.Contains("age", StringComparison.Ordinal));
        Assert.Contains(diagram.Edges, edge => edge.From == "Animal" && edge.To == "Duck");
        Assert.True(MermaidParser.TryParse("""
            gantt
            title Sprint
            section Build
            Design :a1, 2024-01-01, 3d
            Code :after a1, 5d
            """, out var gantt));
        Assert.Equal(MermaidKind.Gantt, gantt.Kind);
        Assert.Equal("Sprint", gantt.Title);
        Assert.Equal(2, gantt.Tasks!.Count);
        var layout = GraphLayoutEngine.ForGantt(gantt);
        Assert.Equal(2, layout.Nodes.Count);
        Assert.True(layout.Width > layout.Nodes[0].Width);
        var mermaid = MarkdownEditing.InsertMermaid("", 0, 0, "class");
        Assert.Contains("classDiagram", mermaid.Text);
        var ganttInsert = MarkdownEditing.InsertMermaid("", 0, 0, "gantt");
        Assert.Contains("gantt", ganttInsert.Text);
    }
}
