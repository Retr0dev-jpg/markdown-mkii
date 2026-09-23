namespace MarkdownMkII.Core.Text;

public static partial class PlantUmlParser
{
    // Dialects without a specialized renderer retain the existing sequential fallback.
    private static readonly (string Marker, string IdPrefix)[] GenericDialects =
    [
        ("startdot", "n"),
        ("startneato", "m"),
        ("startcirco", "r"),
        ("startfdp", "s"),
        ("starttwopi", "t"),
        ("startosage", "p"),
        ("startpatchwork", "g"),
        ("startsfdp", "f"),
        ("startnop", "e"),
        ("startrackdiag", "a"),
        ("startpacketdiag", "b"),
        ("startc4", "d"),
        ("startelk", "i"),
        ("startsmetana", "j"),
        ("startvizjs", "k"),
        ("startsvek", "l"),
        ("startteoz", "m"),
        ("startpic", "n"),
        ("startcreole", "o"),
        ("starteps", "p"),
        ("startumlet", "q"),
        ("startjlatexmath", "r"),
        ("startcute", "s"),
        ("startflow", "t"),
        ("startdef", "u"),
        ("startmap", "v"),
        ("startlist", "w"),
        ("startnetwork", "x"),
        ("startxmi", "y"),
        ("startscxml", "z"),
        ("startjungle", "aa"),
        ("startinfo", "ab"),
        ("startentity", "ac"),
        ("startsudoku", "ad"),
        ("startpackage", "ae"),
        ("startfolder", "af"),
        ("startframe", "ag"),
        ("startcloud", "ah"),
        ("startnode", "ai"),
        ("startqueue", "aj"),
        ("startdatabase", "ak"),
        ("startrectangle", "al"),
        ("startstorage", "am"),
        ("startcard", "an"),
        ("startstack", "ao"),
        ("startartifact", "ap"),
        ("starthexagon", "aq"),
        ("startboundary", "ar"),
        ("startcontrol", "as"),
        ("startinterface", "at"),
        ("startactor", "au"),
        ("startagent", "av"),
        ("startlabel", "aw"),
        ("startperson", "ax"),
        ("startcircle", "ay"),
        ("startcollections", "az"),
        ("starttogether", "ba"),
        ("startnote", "bb"),
        ("startbox", "bc"),
        ("startoval", "bd"),
        ("startrounded", "be"),
        ("starthidden", "bf"),
        ("startpartition", "bg"),
        ("startgroup", "bh"),
        ("startobject", "bi"),
        ("starttree", "bj"),
        ("startlegend", "bk"),
        ("startlane", "bl"),
        ("startswim", "bm"),
        ("startheader", "bn"),
        ("startfooter", "bo"),
        ("starttitle", "bp"),
        ("startcaption", "bq"),
        ("startnewpage", "br"),
    ];

    private static bool TryParseGenericFlow(string code, string idPrefix, out MermaidDiagram diagram)
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
                line.StartsWith("title", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("digraph", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("graph", StringComparison.OrdinalIgnoreCase) ||
                line is "{" or "}")
            {
                continue;
            }

            if (!line.Any(char.IsLetter))
            {
                continue;
            }

            var id = idPrefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
