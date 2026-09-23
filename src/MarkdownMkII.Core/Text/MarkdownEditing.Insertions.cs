namespace MarkdownMkII.Core.Text;

public static partial class MarkdownEditing
{
    public static EditResult InsertWikilink(string text, int start, int length, string? target = null)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var selected = length > 0 ? text.Substring(start, length) : string.Empty;
        var dest = string.IsNullOrWhiteSpace(target) ? (selected.Length == 0 ? "note" : selected) : target.Trim();
        string inserted;
        if (selected.Length > 0 && !string.Equals(selected, dest, StringComparison.Ordinal))
        {
            inserted = $"[[{dest}|{selected}]]";
        }
        else
        {
            inserted = $"[[{dest}]]";
        }

        var innerStart = start + 2;
        return Replace(text, start, length, inserted, innerStart, dest.Length);
    }

    public static EditResult InsertFootnote(string text, int start, int length)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var used = new HashSet<int>();
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                     text,
                     @"\[\^(\d+)\]",
                     System.Text.RegularExpressions.RegexOptions.None,
                     TimeSpan.FromMilliseconds(50)))
        {
            if (int.TryParse(match.Groups[1].Value, out var n))
            {
                used.Add(n);
            }
        }

        var index = 1;
        while (used.Contains(index))
        {
            index++;
        }

        var marker = $"[^{index}]";
        var withMarker = text[..start] + marker + text[(start + length)..];
        var definition = (withMarker.EndsWith('\n') ? string.Empty : "\n") + $"\n[^{index}]: ";
        var rebuilt = withMarker + definition;
        return new EditResult(rebuilt, rebuilt.Length, 0);
    }

    public static EditResult InsertFrontMatter(string text)
    {
        text ??= string.Empty;
        if (text.StartsWith("---", StringComparison.Ordinal))
        {
            return new EditResult(text, 0, 0);
        }

        var block = "---\ntitle: \n---\n\n";
        return new EditResult(block + text, 11, 0);
    }

    public static EditResult InsertMermaid(string text, int start, int length, string? kind = "flowchart")
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var selected = text.Substring(start, length).Trim('\r', '\n');
        var sample = (kind ?? "flowchart").Trim().ToLowerInvariant() switch
        {
            "sequence" => "sequenceDiagram\nAlice->>Bob: Hello\nBob-->>Alice: Hi",
            "pie" => "pie title Share\n\"A\": 40\n\"B\": 60",
            "state" => "stateDiagram-v2\n[*] --> Idle\nIdle --> Active : start\nActive --> [*]",
            "class" => "classDiagram\nAnimal <|-- Duck\nAnimal : +int age",
            "gantt" => "gantt\ntitle Sprint\nsection Build\nDesign :a1, 2024-01-01, 3d\nCode :a2, after a1, 5d",
            "er" => "erDiagram\nCUSTOMER ||--o{ ORDER : places\nORDER ||--|{ LINE : contains",
            "plantuml" => "@startuml\nAlice -> Bob: hello\nBob --> Alice: hi\n@enduml",
            "usecase" => "@startuml\nactor User\nUser --> (Login)\n(Login) --> (Home)\n@enduml",
            "object" => "@startuml\nobject User\nobject Order\nUser --> Order\n@enduml",
            "deployment" => "@startuml\nartifact App\nnode Server\n[App] --> [Server]\n@enduml",
            "mindmap" => "mindmap\n  root((Idea))\n    Topic\n    Other",
            "gitgraph" => "gitGraph\n    commit\n    commit id: \"A\"\n    commit id: \"B\"",
            "timeline" => "timeline\n    title History\n    2020 : Start\n    2024 : Now",
            "journey" => "journey\n    title Day\n    section Home\n      Wake: 5: Me\n      Coffee: 3: Me",
            "quadrant" => "quadrantChart\n    title Reach\n    Campaign A: [0.3, 0.6]\n    Campaign B: [0.45, 0.23]",
            "sankey" => "sankey-beta\nA,B,10\nB,C,5",
            "xychart" => "xychart-beta\n    title Sales\n    x-axis [jan, feb, mar]\n    bar [10, 20, 30]",
            "block" => "block-beta\ncolumns 3\nA B C\nD space E",
            "timing" => "@startuml\nrobust \"Web\" as WB\nconcise \"User\" as WU\n@0\nWU is Idle\nWB is Idle\n@enduml",
            "requirement" => "requirementDiagram\n    requirement Auth {\n    id: 1\n    text: login\n    }\n    element App {\n    type: simulation\n    }\n    App - satisfies -> Auth",
            "nwdiag" => "@startuml\nnwdiag {\n  network dmz {\n    web01;\n    web02;\n  }\n}\n@enduml",
            "c4" => "C4Context\n    title Context\n    Person(user, \"User\", \"A user\")\n    System(app, \"App\", \"The app\")\n    Rel(user, app, \"Uses\")",
            "architecture" => "architecture-beta\n    group api(cloud)[API]\n    service web(server)[Web]\n    service db(database)[Database]\n    web:R --> L:db",
            "packet" => "packet-beta\ntitle DHCP\n0-15: \"Source Port\"\n16-31: \"Destination Port\"",
            "kanban" => "kanban\n  Todo\n    [task1] Write docs\n  Done\n    [task2] Setup",
            "radar" => "radar-beta\n  title Skills\n  axis Communication, Leadership, Tech\n  curve team{0.8, 0.6, 0.9}",
            "salt" => "@startsalt\n{\n  Login\n  [Username]\n  [Password]\n  [OK]\n}\n@endsalt",
            "treemap" => "treemap-beta\n    title Files\n    \"Docs\"\n        \"README\": 40\n        \"Guide\": 20",
            "wbs" => "@startwbs\n* Project\n** Task\n*** Sub\n@endwbs",
            "ganttpuml" => "@startgantt\n[Design] lasts 3 days\n[Code] lasts 5 days\n@endgantt",
            "jsonpuml" => "@startjson\n{\n  \"App\": {\n    \"Core\": \"1.0\"\n  }\n}\n@endjson",
            "yamlpuml" => "@startyaml\nApp:\n  Core: 1.0\n@endyaml",
            "mindmappuml" => "@startmindmap\n* Root\n** Child\n*** Leaf\n@endmindmap",
            "ditaa" => "@startditaa\n+-----+\n| App |\n+-----+\n     |\n     v\n+----+\n| DB |\n+----+\n@endditaa",
            "regex" => "@startregex\ntitle Flow\nalpha\nbeta\n@endregex",
            "ebnf" => "@startebnf\ntitle syntax\nstart\nrule\n@endebnf",
            "archimate" => "@startuml\narchimate #Technology \"App\"\narchimate #Application \"DB\"\n@enduml",
            "chen" => "@startchen\nentity User {\n  id\n}\nentity Order {\n  id\n}\n@endchen",
            "ie" => "@startie\nentity User {\n  id\n}\nentity Order {\n  id\n}\n@endie",
            "mathpuml" => "@startmath\nalpha\nbeta\n@endmath",
            "latexpuml" => "@startlatex\nE = mc^2\nF = ma\n@endlatex",
            "chronology" => "@startchronology\n2020 : Start\n2021 : End\n@endchronology",
            "sdl" => "@startsdl\n:Ready;\n:Working;\n@endsdl",
            "bpmn" => "@startbpmn\nstart\n:Task;\nend\n@endbpmn",
            "boardpuml" => "@startboard\n* Backlog\n** Task\n@endboard",
            "gitpuml" => "@startgit\n*:Initial\n* Commit\n@endgit",
            "filespuml" => "@startfiles\nfile App\nfile Core\n@endfiles",
            "jcckit" => "@startjcckit\nChart\nPlot\n@endjcckit",
            "wireviz" => "@startwireviz\nPower\nGround\n@endwireviz",
            "projectpuml" => "@startproject\nDesign\nCode\n@endproject",
            "dotpuml" => "@startdot\nApp\nCore\n@enddot",
            "neato" => "@startneato\nApp\nCore\n@endneato",
            "circo" => "@startcirco\nApp\nCore\n@endcirco",
            "fdp" => "@startfdp\nApp\nCore\n@endfdp",
            "twopi" => "@starttwopi\nApp\nCore\n@endtwopi",
            "osage" => "@startosage\nApp\nCore\n@endosage",
            "patchwork" => "@startpatchwork\nApp\nCore\n@endpatchwork",
            "sfdp" => "@startsfdp\nApp\nCore\n@endsfdp",
            "nop" => "@startnop\nApp\nCore\n@endnop",
            "rack" => "@startrackdiag\nApp\nCore\n@endrackdiag",
            "packetpuml" => "@startpacketdiag\nApp\nCore\n@endpacketdiag",
            "c4puml" => "@startc4\nApp\nCore\n@endc4",
            "elk" => "@startelk\nApp\nCore\n@endelk",
            "smetana" => "@startsmetana\nApp\nCore\n@endsmetana",
            "vizjs" => "@startvizjs\nApp\nCore\n@endvizjs",
            "svek" => "@startsvek\nApp\nCore\n@endsvek",
            "teoz" => "@startteoz\nApp\nCore\n@endteoz",
            "picpuml" => "@startpic\nApp\nCore\n@endpic",
            "creole" => "@startcreole\nApp\nCore\n@endcreole",
            "eps" => "@starteps\nApp\nCore\n@endeps",
            "umlet" => "@startumlet\nApp\nCore\n@endumlet",
            "jlatexmath" => "@startjlatexmath\nApp\nCore\n@endjlatexmath",
            "cute" => "@startcute\nApp\nCore\n@endcute",
            "flowpuml" => "@startflow\nApp\nCore\n@endflow",
            "defpuml" => "@startdef\nApp\nCore\n@enddef",
            "mappuml" => "@startmap\nApp\nCore\n@endmap",
            "listpuml" => "@startlist\nApp\nCore\n@endlist",
            "networkpuml" => "@startnetwork\nApp\nCore\n@endnetwork",
            "xmipuml" => "@startxmi\nApp\nCore\n@endxmi",
            "scxmlpuml" => "@startscxml\nApp\nCore\n@endscxml",
            "junglepuml" => "@startjungle\nApp\nCore\n@endjungle",
            "infopuml" => "@startinfo\nApp\nCore\n@endinfo",
            "entitypuml" => "@startentity\nApp\nCore\n@endentity",
            "sudokupuml" => "@startsudoku\nApp\nCore\n@endsudoku",
            "packagepuml" => "@startpackage\nApp\nCore\n@endpackage",
            "folderpuml" => "@startfolder\nApp\nCore\n@endfolder",
            "framepuml" => "@startframe\nApp\nCore\n@endframe",
            "cloudpuml" => "@startcloud\nApp\nCore\n@endcloud",
            "nodepuml" => "@startnode\nApp\nCore\n@endnode",
            "queuepuml" => "@startqueue\nApp\nCore\n@endqueue",
            "databasepuml" => "@startdatabase\nApp\nCore\n@enddatabase",
            "rectanglepuml" => "@startrectangle\nApp\nCore\n@endrectangle",
            "storagepuml" => "@startstorage\nApp\nCore\n@endstorage",
            "cardpuml" => "@startcard\nApp\nCore\n@endcard",
            "stackpuml" => "@startstack\nApp\nCore\n@endstack",
            "artifactpuml" => "@startartifact\nApp\nCore\n@endartifact",
            "hexagonpuml" => "@starthexagon\nApp\nCore\n@endhexagon",
            "boundarypuml" => "@startboundary\nApp\nCore\n@endboundary",
            "controlpuml" => "@startcontrol\nApp\nCore\n@endcontrol",
            "interfacepuml" => "@startinterface\nApp\nCore\n@endinterface",
            "actorpuml" => "@startactor\nApp\nCore\n@endactor",
            "agentpuml" => "@startagent\nApp\nCore\n@endagent",
            "labelpuml" => "@startlabel\nApp\nCore\n@endlabel",
            "personpuml" => "@startperson\nApp\nCore\n@endperson",
            "circlepuml" => "@startcircle\nApp\nCore\n@endcircle",
            "collectionspuml" => "@startcollections\nApp\nCore\n@endcollections",
            "togetherpuml" => "@starttogether\nApp\nCore\n@endtogether",
            "notepuml" => "@startnote\nApp\nCore\n@endnote",
            "boxpuml" => "@startbox\nApp\nCore\n@endbox",
            "ovalpuml" => "@startoval\nApp\nCore\n@endoval",
            "roundedpuml" => "@startrounded\nApp\nCore\n@endrounded",
            "hiddenpuml" => "@starthidden\nApp\nCore\n@endhidden",
            "partitionpuml" => "@startpartition\nApp\nCore\n@endpartition",
            "grouppuml" => "@startgroup\nApp\nCore\nendgroup",
            "objectpuml" => "@startobject\nApp\nCore\n@endobject",
            "treepuml" => "@starttree\nApp\nCore\n@endtree",
            "legendpuml" => "@startlegend\nApp\nCore\n@endlegend",
            "lanepuml" => "@startlane\nApp\nCore\nendlane",
            "swimpuml" => "@startswim\nApp\nCore\nendswim",
            "headerpuml" => "@startheader\nApp\nCore\nendheader",
            "footerpuml" => "@startfooter\nApp\nCore\nendfooter",
            "titlepuml" => "@starttitle\nApp\nCore\nendtitle",
            "captionpuml" => "@startcaption\nApp\nCore\nendcaption",
            "newpagepuml" => "@startnewpage\nApp\nCore\nendnewpage",
            _ => "flowchart TD\nA[Start] --> B[End]"
        };
        var kindKey = (kind ?? "flowchart").Trim();
        var language = kindKey.Equals("plantuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("usecase", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("object", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("deployment", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("timing", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("nwdiag", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("salt", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("wbs", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("ganttpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("jsonpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("yamlpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("mindmappuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("ditaa", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("regex", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("ebnf", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("archimate", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("chen", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("ie", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("mathpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("latexpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("chronology", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("sdl", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("bpmn", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("boardpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("gitpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("filespuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("jcckit", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("wireviz", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("projectpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("dotpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("neato", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("circo", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("fdp", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("twopi", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("osage", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("patchwork", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("sfdp", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("nop", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("rack", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("packetpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("c4puml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("elk", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("smetana", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("vizjs", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("svek", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("teoz", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("picpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("creole", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("eps", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("umlet", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("jlatexmath", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("cute", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("flowpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("defpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("mappuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("listpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("networkpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("xmipuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("scxmlpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("junglepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("infopuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("entitypuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("sudokupuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("packagepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("folderpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("framepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("cloudpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("nodepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("queuepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("databasepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("rectanglepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("storagepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("cardpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("stackpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("artifactpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("hexagonpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("boundarypuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("controlpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("interfacepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("actorpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("agentpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("labelpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("personpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("circlepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("collectionspuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("togetherpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("notepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("boxpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("ovalpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("roundedpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("hiddenpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("partitionpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("grouppuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("objectpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("treepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("legendpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("lanepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("swimpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("headerpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("footerpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("titlepuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("captionpuml", StringComparison.OrdinalIgnoreCase) ||
                       kindKey.Equals("newpagepuml", StringComparison.OrdinalIgnoreCase)
            ? "plantuml"
            : "mermaid";
        var body = selected.Length == 0 ? sample : selected;
        var fence = "```" + language + "\n" + body + "\n```";
        var caret = start + ("```" + language + "\n").Length;
        return Replace(text, start, length, fence, caret, body.Length);
    }
}
