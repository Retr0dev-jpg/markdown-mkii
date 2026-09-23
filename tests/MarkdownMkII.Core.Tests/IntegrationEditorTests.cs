using MarkdownMkII.Core.Highlight;
using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Preview;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public partial class FoldGitSnippetTests
{
    [Fact]
    public void HeadingFold_Hides_Body_Ranges()
    {
        var text = "# A\n\nbody\n\n## B\nmore\n";
        var outline = TocExtractor.Extract(text);
        var folded = new HashSet<int> { outline[0].SourceLine };
        Assert.True(HeadingFold.IsLineHidden(outline, folded, outline[0].SourceLine + 1));
        Assert.False(HeadingFold.IsLineHidden(outline, folded, outline[0].SourceLine));
        var ranges = HeadingFold.HiddenRanges(text, outline, folded);
        Assert.NotEmpty(ranges);
        Assert.True(ranges[0].Length > 0);
    }

    [Fact]
    public void Snippets_Expand_Date_Token()
    {
        var now = new DateTimeOffset(2026, 9, 10, 15, 4, 0, TimeSpan.Zero);
        var result = MarkdownSnippets.TryExpand("see :date", 9, now);
        Assert.NotNull(result);
        Assert.Equal("see 2026-09-10", result.Value.Text);
        Assert.Null(MarkdownSnippets.TryExpand("plain", 5, now));
    }

    [Fact]
    public void PlantUml_Parses_Sequence_Arrows()
    {
        Assert.True(PlantUmlParser.TryParse("""
            @startuml
            Alice -> Bob: hello
            Bob --> Alice: hi
            @enduml
            """, out var diagram));
        Assert.Equal(MermaidKind.Sequence, diagram.Kind);
        Assert.Equal(2, diagram.Messages!.Count);
        Assert.Contains(diagram.Messages, message => message.Dashed);
        var parsed = DocumentParser.Parse("""
            ```plantuml
            @startuml
            A -> B: x
            @enduml
            ```
            """);
        Assert.Contains(parsed.Preview.Blocks, block => block is MermaidBlockIr);
    }

    [Fact]
    public void ExportVault_Writes_Html_Files()
    {
        var src = Path.Combine(Path.GetTempPath(), "mkii-exp-src-" + Guid.NewGuid().ToString("N"));
        var dest = Path.Combine(Path.GetTempPath(), "mkii-exp-dst-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(src);
        try
        {
            File.WriteAllText(Path.Combine(src, "Alpha.md"), "# Hello\n[Spoke](notes/Spoke.md)\n");
            Directory.CreateDirectory(Path.Combine(src, "notes"));
            File.WriteAllText(Path.Combine(src, "notes", "Spoke.md"), "# Spoke\n");
            var count = ExportHtmlService.ExportVault([src], dest);
            Assert.Equal(2, count);
            Assert.True(File.Exists(Path.Combine(dest, "Alpha.html")));
            Assert.True(File.Exists(Path.Combine(dest, "notes", "Spoke.html")));
            Assert.True(File.Exists(Path.Combine(dest, "index.html")));
            Assert.Contains("Hello", File.ReadAllText(Path.Combine(dest, "Alpha.html")), StringComparison.Ordinal);
            Assert.Contains("notes/Spoke.html", File.ReadAllText(Path.Combine(dest, "index.html")), StringComparison.Ordinal);
            Assert.Contains("notes/Spoke.html", File.ReadAllText(Path.Combine(dest, "Alpha.html")), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(src, recursive: true);
            if (Directory.Exists(dest))
            {
                Directory.Delete(dest, recursive: true);
            }
        }
    }

    [Fact]
    public void OpenTasks_Finds_Unchecked_Items()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-tasks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Note.md"), "- [ ] Ship it\n- [x] Done\n");
            var tasks = WikiIndex.OpenTasks([dir]);
            Assert.Contains(tasks, hit => hit.Preview == "Ship it");
            Assert.DoesNotContain(tasks, hit => hit.Preview == "Done");
            var done = WikiIndex.CompletedTasks([dir]);
            Assert.Contains(done, hit => hit.Preview == "Done");
            Assert.DoesNotContain(done, hit => hit.Preview == "Ship it");
            Assert.Equal(Path.Combine(dir, "Note.md"), WikiIndex.PickRandom([dir], new Random(1)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Wiki_Resolves_Aliases_And_Block_Ids()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-alias-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Real.md"), """
                ---
                aliases: [Home, Index]
                ---

                # Real

                Keep this.

                Target paragraph ^abc
                """);
            Assert.Equal(Path.Combine(dir, "Real.md"), WikiIndex.Resolve("Home", null, [dir]));
            Assert.Contains("Home", WikiIndex.Catalog([dir]));
            var section = WikiIndex.ExtractHeadingSection(File.ReadAllText(Path.Combine(dir, "Real.md")), "^abc");
            Assert.Contains("Target paragraph", section, StringComparison.Ordinal);
            Assert.DoesNotContain("Keep this", section, StringComparison.Ordinal);
            Assert.Contains(WikiIndex.NotesWithAliases([dir]), hit => hit.Preview.Contains("Home", StringComparison.Ordinal));
            var monday = new DateTime(2026, 9, 7);
            File.WriteAllText(Path.Combine(dir, "2026-09-07.md"), "# Day\n");
            Assert.Contains(WikiIndex.WeekNotes([dir], monday), hit => hit.Preview == "2026-09-07");
            Assert.NotEmpty(WikiIndex.RecentNotes([dir]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PlantUml_Parses_Class_And_Activity()
    {
        Assert.True(PlantUmlParser.TryParse("""
            @startuml
            class Foo
            Foo -- Bar
            @enduml
            """, out var classes));
        Assert.Equal(MermaidKind.Class, classes.Kind);
        Assert.Contains(classes.Edges, edge => edge.From == "Foo" && edge.To == "Bar");
        Assert.True(PlantUmlParser.TryParse("""
            @startuml
            start
            :Hello;
            stop
            @enduml
            """, out var activity));
        Assert.Equal(MermaidKind.Flow, activity.Kind);
        Assert.True(activity.Edges.Count >= 1);
        Assert.True(MermaidParser.TryParse("""
            erDiagram
            CUSTOMER ||--o{ ORDER : places
            """, out var er));
        Assert.Equal(MermaidKind.Er, er.Kind);
        Assert.Contains(er.Edges, edge => edge.From == "CUSTOMER" && edge.To == "ORDER");
        Assert.True(PlantUmlParser.TryParse("""
            @startuml
            [Web] --> [API]
            component Database
            @enduml
            """, out var components));
        Assert.Contains(components.Edges, edge => edge.From == "Web" && edge.To == "API");
        Assert.Contains(components.Nodes, node => node.Label == "Database");
        Assert.True(MermaidParser.TryParse("""
            gantt
            Design :a1, 2026-01-01, 3d
            Code :a2, 2026-01-04, 2d
            """, out var dated));
        Assert.True(dated.Tasks![1].Start >= dated.Tasks[0].Start);
    }

    [Fact]
    public void Vault_Attachments_And_Related_Notes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Alpha.md"), "---\ntags: shared, one\n---\n# Alpha\n#shared\n");
            File.WriteAllText(Path.Combine(dir, "Beta.md"), "---\ntags: shared\n---\n# Beta\n");
            File.WriteAllText(Path.Combine(dir, "Empty.md"), "---\ntitle: Empty\n---\n# Empty\n");
            File.WriteAllText(Path.Combine(dir, "Draft.md"), "---\nstatus: draft\n---\n# Draft\nbody\n");
            Assert.Contains(WikiIndex.RelatedNotes([dir], "Alpha"), hit => hit.Name == "Beta.md");
            Assert.Contains(WikiIndex.EmptyNotes([dir]), hit => hit.Name == "Empty.md");
            Assert.DoesNotContain(WikiIndex.EmptyNotes([dir]), hit => hit.Name == "Alpha.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "status=draft"), hit => hit.Name == "Draft.md");
            Assert.Contains(WikiIndex.Untagged([dir]), hit => hit.Name == "Empty.md");
            var stats = WikiIndex.Summarize([dir]);
            Assert.True(stats.Notes >= 4);
            Assert.True(stats.Tags >= 1);
            var source = Path.Combine(dir, "pic.png");
            File.WriteAllBytes(source, [1, 2, 3, 4]);
            var imported = VaultAttachments.Import(sourcePath: source, documentPath: Path.Combine(dir, "Alpha.md"), folders: [dir]);
            Assert.NotNull(imported);
            Assert.Contains(Path.Combine(dir, "assets"), imported, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(imported));
            var expanded = MarkdownTemplates.Expand("Hi {{title}} {{date}}", "Note", new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero));
            Assert.Equal("Hi Note 2026-09-10", expanded);
            Assert.Contains("id:", MarkdownTemplates.Zettel("Idea", new DateTimeOffset(2026, 9, 10, 15, 4, 0, TimeSpan.Zero)));
            Assert.True(WikiIndex.IsEmptyNote("# Only heading\n"));
            Assert.False(WikiIndex.IsEmptyNote("# Title\n\nParagraph\n"));
            var rewritten = MarkdownTags.Rewrite("---\ntags: shared, one\n---\n# T\n#shared\n", "shared", "team");
            Assert.Contains("team", rewritten, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("#shared", rewritten, StringComparison.OrdinalIgnoreCase);
            File.WriteAllText(Path.Combine(dir, "Alpha.md"), rewritten);
            Assert.Equal(1, WikiIndex.RewriteTag([dir], "team", "crew"));
            Assert.Contains("[[Alpha]]", WikiIndex.GenerateMoc([dir], "MOC"));
            Assert.Contains("\"stem\"", WikiIndex.ToGraphJson(WikiIndex.Graph([dir])));
            Assert.Contains(WikiIndex.MatchingTitle([dir], "Alp"), hit => hit.Name == "Alpha.md");
            Assert.Empty(WikiIndex.WithoutFrontMatter([dir]));
            Assert.NotEmpty(WikiIndex.LargestNotes([dir]));
            Assert.Contains(WikiIndex.ModifiedToday([dir], DateTime.Now), hit => hit.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
            var andHits = WorkspaceScanner.SearchContent([dir], "crew T");
            Assert.NotEmpty(andHits);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Media_FenceFold_NumberHeadings_Citations_And_ExportAssets()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "assets"));
        try
        {
            var picture = Path.Combine(dir, "assets", "pic.png");
            File.WriteAllBytes(picture, [0x89, 0x50, 0x4E, 0x47]);
            var note = Path.Combine(dir, "Note.md");
            File.WriteAllText(note, "![[pic.png]]\nSee [@smith2020] and [@jones].\n");
            File.WriteAllText(Path.Combine(dir, "Broken.md"), "Missing ![[gone.jpg]]\n");

            Assert.Equal(picture, WikiIndex.Resolve("pic.png", note, [dir]));
            Assert.True(WikiIndex.IsMediaTarget("clip.mp4"));
            Assert.Contains(WikiIndex.MissingMedia([dir]), hit => hit.Name == "Broken.md");
            Assert.DoesNotContain(WikiIndex.MissingMedia([dir]), hit => hit.Name == "Note.md");

            var assets = ExportHtmlService.CollectLocalAssets("![[pic.png]]\n![x](assets/pic.png)", note, [dir]);
            Assert.Contains(assets, path => string.Equals(path, picture, StringComparison.OrdinalIgnoreCase));
            var htmlPath = Path.Combine(dir, "out.html");
            Assert.Equal(1, ExportHtmlService.ExportWithAssets("![[pic.png]]", htmlPath, note, [dir]));
            Assert.True(File.Exists(htmlPath));
            Assert.Contains("assets/", File.ReadAllText(htmlPath), StringComparison.OrdinalIgnoreCase);

            var numbered = MarkdownEditing.NumberHeadings("# A\n\n```\n# not\n```\n\n## B\n");
            Assert.Contains("# 1 A", numbered.Text, StringComparison.Ordinal);
            Assert.Contains("## 1.1 B", numbered.Text, StringComparison.Ordinal);
            Assert.Contains("# not", numbered.Text, StringComparison.Ordinal);

            var fence = "```\nsecret\n```\nvisible\n";
            var folded = new HashSet<int>();
            Assert.True(FenceFold.Toggle(folded, fence, 0));
            Assert.True(FenceFold.IsLineHidden(fence, folded, 1));
            Assert.False(FenceFold.IsLineHidden(fence, folded, 0));
            Assert.False(FenceFold.IsLineHidden(fence, folded, 3));
            Assert.True(HeadingFold.IsLineHidden([], folded, 1, fence));
            HeadingFold.FoldAll(folded, [], fence);
            Assert.Contains(0, folded);

            var keys = MarkdownCitations.Keys("See [@smith2020] and [@jones].");
            Assert.Equal(["jones", "smith2020"], keys);
            var cited = MarkdownCitations.Upsert("See [@a].\n");
            Assert.Contains("## References", cited.Text, StringComparison.Ordinal);
            Assert.Contains("- [@a]", cited.Text, StringComparison.Ordinal);
            var twice = MarkdownCitations.Upsert(cited.Text);
            Assert.Equal(1, twice.Text.Split("## References", StringSplitOptions.None).Length - 1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SameNote_Monthly_Due_ListFold_Relocate_DataviewSort_And_BrokenLinks()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-period-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var nested = Path.Combine(dir, "notes");
        Directory.CreateDirectory(nested);
        try
        {
            var note = Path.Combine(dir, "Hub.md");
            File.WriteAllText(note, "# Hub\n\n## Local\n\nBody\n\n- parent\n  - child\n\n- [ ] Pay 2026-09-01\n- [ ] Later 2099-01-01\n\nSee [[#Local]] and [gone](missing.md)\n");
            File.WriteAllText(Path.Combine(dir, "Zed.md"), "---\nstatus: z\n---\n# Zed\n");
            File.WriteAllText(Path.Combine(dir, "Aye.md"), "---\nstatus: a\n---\n# Aye\n");

            Assert.Equal(note, WikiIndex.Resolve(string.Empty, note, [dir]));
            var parsed = DocumentParser.Parse(File.ReadAllText(note), note, vaultFolders: [dir]);
            Assert.Contains(parsed.Preview.Blocks.OfType<ParagraphBlockIr>(), block =>
                block.Inlines.OfType<LinkInline>().Any(link => (link.Url ?? string.Empty).Contains("#Local", StringComparison.Ordinal)));

            Assert.True(WikiIndex.IsMonthlyStem("2026-09"));
            Assert.Equal("2026-09", WikiIndex.MonthlyStem(new DateTime(2026, 9, 10)));
            File.WriteAllText(Path.Combine(dir, "2026-09.md"), MarkdownTemplates.Monthly("2026-09", new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero)));
            Assert.Equal(Path.Combine(dir, "2026-09.md"), WikiIndex.ExistingMonthly([dir], new DateTime(2026, 9, 10)));
            Assert.Contains("2026-W37", MarkdownTemplates.Expand("{{week}} {{month}}", "T", new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero)), StringComparison.Ordinal);
            Assert.Contains("2026-09", MarkdownTemplates.Expand("{{week}} {{month}}", "T", new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero)), StringComparison.Ordinal);

            Assert.Equal(new DateTime(2026, 9, 1), WikiIndex.TaskDueDate("- [ ] Pay 2026-09-01"));
            Assert.Contains(WikiIndex.DueTasks([dir], overdueOnly: true, today: new DateTime(2026, 9, 10)), hit => hit.Preview.Contains("Pay", StringComparison.Ordinal));
            Assert.Contains(WikiIndex.DueTasks([dir], on: new DateTime(2099, 1, 1), today: new DateTime(2026, 9, 10)), hit => hit.Preview.Contains("Later", StringComparison.Ordinal));

            var list = "- parent\n  - child\nvisible\n";
            var folded = new HashSet<int>();
            Assert.True(ListFold.Toggle(folded, list, 0));
            Assert.True(ListFold.IsLineHidden(list, folded, 1));
            Assert.False(ListFold.IsLineHidden(list, folded, 0));

            var moved = WikiIndex.Relocate(Path.Combine(dir, "Aye.md"), nested, [dir]);
            Assert.Equal(Path.Combine(nested, "Aye.md"), moved);
            Assert.True(File.Exists(Path.Combine(nested, "Aye.md")));
            Assert.False(File.Exists(Path.Combine(dir, "Aye.md")));

            var sorted = WikiIndex.Dataview([dir], "FROM SORT title ASC LIMIT 1");
            Assert.Single(sorted);
            Assert.Equal("2026-09.md", sorted[0].Name);

            Assert.Contains(WikiIndex.BrokenMarkdownLinks([dir]), hit => hit.Name == "Hub.md");

            Assert.True(PlantUmlParser.TryParse("node Web\ncloud API\n[Web] --> [API]\n", out var deploy));
            Assert.Contains(deploy.Nodes, node => node.Label.Contains("Web", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RewriteHeading_Updates_Wiki_And_Markdown_Anchors()
    {
        var text = "See [[Hub#Old]] [[#Old]] [x](Hub.md#Old) [y](#Old)\n";
        var named = WikiLinks.RewriteHeading(text, "Hub", "Old", "New");
        Assert.Contains("[[Hub#New]]", named, StringComparison.Ordinal);
        Assert.Contains("(Hub.md#New)", named, StringComparison.Ordinal);
        Assert.Contains("[[#Old]]", named, StringComparison.Ordinal);
        Assert.Contains("(#Old)", named, StringComparison.Ordinal);

        var local = WikiLinks.RewriteHeading(text, string.Empty, "Old", "New");
        Assert.Contains("[[#New]]", local, StringComparison.Ordinal);
        Assert.Contains("(#New)", local, StringComparison.Ordinal);
        Assert.Contains("[[Hub#Old]]", local, StringComparison.Ordinal);

        var slug = WikiLinks.RewriteHeading("[[Hub#old-title]]", "Hub", "Old Title", "New Title");
        Assert.Contains("[[Hub#New Title]]", slug, StringComparison.Ordinal);
    }

    [Fact]
    public void RenameHeading_Rewrites_Same_Note_Anchors()
    {
        var text = "# Old\n\nSee [[#Old]]\n";
        var renamed = MarkdownEditing.RenameHeading(text, 0, "New");
        Assert.NotNull(renamed);
        Assert.Equal("Old", renamed.Value.OldTitle);
        Assert.Contains("# New", renamed.Value.Result.Text, StringComparison.Ordinal);
        Assert.Contains("[[#New]]", renamed.Value.Result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("[[#Old]]", renamed.Value.Result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TransposeTable_Swaps_Header_And_Columns()
    {
        var table = "| A | B |\n| --- | --- |\n| 1 | 2 |\n";
        var result = MarkdownEditing.TransposeTable(table, 0, 0);
        Assert.NotNull(result);
        var lines = result.Value.Text.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        Assert.Contains("A", lines[0], StringComparison.Ordinal);
        Assert.Contains("1", lines[0], StringComparison.Ordinal);
        Assert.Contains("---", lines[1], StringComparison.Ordinal);
        Assert.Contains("B", lines[2], StringComparison.Ordinal);
        Assert.Contains("2", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void ShiftHeading_Tasks_Table_Alias_And_Vault_Filters()
    {
        var promoted = MarkdownEditing.ShiftHeadingLevel("## Hello\n", 0, 0, -1);
        Assert.StartsWith("# Hello", promoted.Text, StringComparison.Ordinal);
        var demoted = MarkdownEditing.ShiftHeadingLevel("# Hello\n", 0, 0, 1);
        Assert.StartsWith("## Hello", demoted.Text, StringComparison.Ordinal);

        var tasks = "- [ ] a\n- [ ] b\nplain\n";
        var checkedTasks = MarkdownEditing.SetTasksChecked(tasks, 0, tasks.Length, true);
        Assert.Contains("- [x] a", checkedTasks.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("- [x] b", checkedTasks.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plain", checkedTasks.Text, StringComparison.Ordinal);
        var uncheckedTasks = MarkdownEditing.SetTasksChecked(checkedTasks.Text, 0, checkedTasks.Text.Length, false);
        Assert.Contains("- [ ] a", uncheckedTasks.Text, StringComparison.Ordinal);

        var table = "| A | B |\n| --- | --- |\n| 1 | 2 |\n";
        var duplicated = MarkdownEditing.DuplicateTableRow(table, table.IndexOf('1', StringComparison.Ordinal), 0);
        Assert.NotNull(duplicated);
        Assert.Equal(2, duplicated!.Value.Text.Split('\n').Count(line => line.Contains('1')));

        var moved = MarkdownEditing.MoveTableColumn(table, table.IndexOf('A', StringComparison.Ordinal), 0, 1);
        Assert.NotNull(moved);
        var header = moved!.Value.Text.Split('\n')[0];
        Assert.True(header.IndexOf('B', StringComparison.Ordinal) < header.IndexOf('A', StringComparison.Ordinal));

        var aliased = FrontMatter.EnsureAlias("# Note\n", "Home");
        Assert.Contains("aliases:", aliased, StringComparison.Ordinal);
        Assert.Contains("Home", aliased, StringComparison.Ordinal);
        Assert.Equal(aliased, FrontMatter.EnsureAlias(aliased, "home"));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-filters-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "2026-09-10.md"), "# Day\n");
            File.WriteAllText(Path.Combine(dir, "Cite.md"), "See [@smith]\n");
            File.WriteAllText(Path.Combine(dir, "Math.md"), "Value $a+b$\n");
            File.WriteAllText(Path.Combine(dir, "Code.md"), "```csharp\nvar x = 1;\n```\n");
            File.WriteAllText(Path.Combine(dir, "Tiny.md"), "x\n");

            var missing = WikiIndex.MissingDaily([dir], new DateTime(2026, 9, 10));
            Assert.Contains(missing, hit => hit.Preview == "2026-09-01");
            Assert.DoesNotContain(missing, hit => hit.Preview == "2026-09-10");

            Assert.Contains(WikiIndex.NotesWithCitations([dir]), hit => hit.Name == "Cite.md");
            Assert.Contains(WikiIndex.NotesWithMath([dir]), hit => hit.Name == "Math.md");
            Assert.Contains(WikiIndex.NotesWithFence([dir]), hit => hit.Name == "Code.md");
            Assert.Contains(WikiIndex.ShortestNotes([dir]), hit => hit.Name == "Tiny.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TableRows_Csv_Diff_Images_PlantUmlState_And_FenceLanguages()
    {
        var table = "| A | B |\n| --- | --- |\n| 1 | 2 |\n| 3 | 4 |\n";
        var moved = MarkdownEditing.MoveTableRow(table, table.IndexOf('1', StringComparison.Ordinal), 0, 1);
        Assert.NotNull(moved);
        var body = moved!.Value.Text.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        Assert.Contains("A", body[0], StringComparison.Ordinal);
        Assert.Contains("3", body[2], StringComparison.Ordinal);
        Assert.Contains("1", body[3], StringComparison.Ordinal);
        Assert.Null(MarkdownEditing.MoveTableRow(table, table.IndexOf('A', StringComparison.Ordinal), 0, 1));

        var csv = MarkdownEditing.TableToDelimited(table, 0);
        Assert.NotNull(csv);
        Assert.Contains("A,B", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("---", csv, StringComparison.Ordinal);
        var rebuilt = MarkdownEditing.InsertTableFromDelimited(string.Empty, 0, 0, csv!);
        Assert.Contains("| A | B |", rebuilt.Text, StringComparison.Ordinal);
        Assert.Contains("| 1 | 2 |", rebuilt.Text, StringComparison.Ordinal);

        var unified = TextDiff.ToUnified("hello\n", "hello\nworld\n", "note.md");
        Assert.Contains("--- a/note.md", unified, StringComparison.Ordinal);
        Assert.Contains("+world", unified, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-images-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pic.md"), "![gone](missing.png)\n[ok](missing.md)\n");
            File.WriteAllText(Path.Combine(dir, "Link.md"), "[only](missing.md)\n");
            Assert.Contains(WikiIndex.BrokenImages([dir]), hit => hit.Name == "Pic.md");
            Assert.DoesNotContain(WikiIndex.BrokenImages([dir]), hit => hit.Name == "Link.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }

        Assert.True(PlantUmlParser.TryParse("""
            @startuml
            [*] --> Idle
            Idle --> [*]
            @enduml
            """, out var state));
        Assert.Equal(MermaidKind.Flow, state.Kind);
        Assert.Contains(state.Nodes, node => node.Id == "start");
        Assert.Contains(state.Nodes, node => node.Id == "end");
        Assert.Contains(state.Edges, edge => edge.From == "start" && edge.To == "Idle");

        Assert.Equal(LanguageId.CSharp, LanguageCatalog.FromInfo("cpp"));
        Assert.Equal(LanguageId.Java, LanguageCatalog.FromInfo("kotlin"));
        Assert.Equal(LanguageId.Yaml, LanguageCatalog.FromInfo("toml"));
    }

    [Fact]
    public void TableList_Callouts_Embeds_And_Footnotes()
    {
        var table = "| A | B |\n| --- | --- |\n| 1 | 2 |\n| 3 | 4 |\n";
        var duplicated = MarkdownEditing.DuplicateTableColumn(table, table.IndexOf('A', StringComparison.Ordinal), 0);
        Assert.NotNull(duplicated);
        Assert.Contains("| A | A | B |", duplicated!.Value.Text.Replace("\r", "", StringComparison.Ordinal), StringComparison.Ordinal);

        var listed = MarkdownEditing.TableToList(table, 0, 0);
        Assert.NotNull(listed);
        Assert.Contains("- 1 — 2", listed!.Value.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("| A |", listed.Value.Text, StringComparison.Ordinal);

        var list = "- apple\n- banana\n";
        var fromList = MarkdownEditing.ListToTable(list, 0, list.Length);
        Assert.NotNull(fromList);
        Assert.Contains("| Item |", fromList!.Value.Text, StringComparison.Ordinal);
        Assert.Contains("| apple |", fromList.Value.Text, StringComparison.Ordinal);

        var sorted = MarkdownEditing.SortTableColumn(table, table.IndexOf('1', StringComparison.Ordinal), 0, descending: true);
        Assert.NotNull(sorted);
        var body = sorted!.Value.Text.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        Assert.True(body[2].Contains('3', StringComparison.Ordinal));
        Assert.True(body[3].Contains('1', StringComparison.Ordinal));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-blocks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Call.md"), "> [!tip] Hello\n");
            File.WriteAllText(Path.Combine(dir, "Emb.md"), "![[pic.png]]\n");
            File.WriteAllText(Path.Combine(dir, "Note.md"), "See [^1]\n\n[^1]: source\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "plain\n");
            Assert.Contains(WikiIndex.NotesWithCallouts([dir]), hit => hit.Name == "Call.md");
            Assert.Contains(WikiIndex.NotesWithEmbeds([dir]), hit => hit.Name == "Emb.md");
            Assert.Contains(WikiIndex.NotesWithFootnotes([dir]), hit => hit.Name == "Note.md");
            Assert.DoesNotContain(WikiIndex.NotesWithCallouts([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithEmbeds([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithFootnotes([dir]), hit => hit.Name == "Plain.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EnsureTitle_Uses_First_Heading_Or_Keeps_Existing()
    {
        Assert.Contains("title: Hello", FrontMatter.EnsureTitle("# Hello\n"), StringComparison.Ordinal);
        var existing = "---\ntitle: Keep\n---\n# Other\n";
        Assert.Equal(existing, FrontMatter.EnsureTitle(existing));
    }

    [Fact]
    public void Vault_Tags_Dates_Mutual_Export_And_Relative()
    {
        var src = Path.Combine(Path.GetTempPath(), "mkii-vault-md-" + Guid.NewGuid().ToString("N"));
        var dest = Path.Combine(Path.GetTempPath(), "mkii-vault-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(src, "notes"));
        try
        {
            File.WriteAllText(Path.Combine(src, "Hub.md"), """
                ---
                title: Hub
                created: 2026-09-10
                tags: [inbox, work]
                ---

                # Old

                #inbox #work

                See [[Spoke]] and [[#Old]]
                """);
            File.WriteAllText(Path.Combine(src, "Spoke.md"), """
                ---
                id: spoke
                ---

                # Spoke

                #inbox

                See [[Hub]] and [[Hub#Old]] and [x](Hub.md#Old)
                """);
            File.WriteAllText(Path.Combine(src, "notes", "Nested.md"), "# Nested\n");

            var both = WikiIndex.FilesWithAllTags([src], ["inbox", "work"]);
            Assert.Contains(both, hit => hit.Name.Equals("Hub.md", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(both, hit => hit.Name.Equals("Spoke.md", StringComparison.OrdinalIgnoreCase));
            var inbox = WikiIndex.FilesWithAllTags([src], ["inbox"]);
            Assert.Contains(inbox, hit => hit.Name.Equals("Hub.md", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(inbox, hit => hit.Name.Equals("Spoke.md", StringComparison.OrdinalIgnoreCase));

            var any = WikiIndex.FilesWithAnyTag([src], ["work", "missing"]);
            Assert.Contains(any, hit => hit.Name.Equals("Hub.md", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(any, hit => hit.Name.Equals("Spoke.md", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(WikiIndex.WithoutTitle([src]), hit => hit.Name.Equals("Spoke.md", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(WikiIndex.WithoutTitle([src]), hit => hit.Name.Equals("Hub.md", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                WikiIndex.QueryFrontMatter([src], "created!=1999"),
                hit => hit.Name.Equals("Hub.md", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                WikiIndex.QueryFrontMatter([src], "created!=2026-09-10"),
                hit => hit.Name.Equals("Hub.md", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                WikiIndex.Dataview([src], "FROM #inbox #work"),
                hit => hit.Name.Equals("Hub.md", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                WikiIndex.Dataview([src], "FROM #inbox #work"),
                hit => hit.Name.Equals("Spoke.md", StringComparison.OrdinalIgnoreCase));

            var mutual = WikiIndex.MutualLinks([src]);
            Assert.Contains(mutual, hit =>
                (hit.Name.Equals("Hub.md", StringComparison.OrdinalIgnoreCase) &&
                 hit.Preview.Contains("Spoke", StringComparison.OrdinalIgnoreCase)) ||
                (hit.Name.Equals("Spoke.md", StringComparison.OrdinalIgnoreCase) &&
                 hit.Preview.Contains("Hub", StringComparison.OrdinalIgnoreCase)));

            Assert.Contains(
                WikiIndex.NotesWithDateField([src], "created", "2026-09-10"),
                hit => hit.Name.Equals("Hub.md", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                WikiIndex.NotesWithDateField([src], "created", "2026-09"),
                hit => hit.Name.Equals("Hub.md", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(WikiIndex.WithoutId([src]), hit => hit.Name.Equals("Hub.md", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(WikiIndex.WithoutId([src]), hit => hit.Name.Equals("Spoke.md", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(
                "notes/Nested.md",
                VaultAttachments.RelativeToVault(Path.Combine(src, "notes", "Nested.md"), [src]));

            var copied = ExportHtmlService.ExportMarkdownVault([src], dest);
            Assert.True(copied >= 3);
            Assert.True(File.Exists(Path.Combine(dest, "Hub.md")));
            Assert.True(File.Exists(Path.Combine(dest, "notes", "Nested.md")));
            Assert.Contains("[[Spoke]]", File.ReadAllText(Path.Combine(dest, "Hub.md")), StringComparison.Ordinal);

            var rewritten = WikiIndex.RewriteHeadingAnchors([src], "Hub", "Old", "New");
            Assert.True(rewritten >= 1);
            Assert.Contains("[[#New]]", File.ReadAllText(Path.Combine(src, "Hub.md")), StringComparison.Ordinal);
            Assert.Contains("[[Hub#New]]", File.ReadAllText(Path.Combine(src, "Spoke.md")), StringComparison.Ordinal);
            Assert.Contains("(Hub.md#New)", File.ReadAllText(Path.Combine(src, "Spoke.md")), StringComparison.Ordinal);

            Assert.Contains(WikiIndex.WordiestNotes([src]), hit => hit.Preview.Contains("w", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(src, recursive: true);
            if (Directory.Exists(dest))
            {
                Directory.Delete(dest, recursive: true);
            }
        }
    }

    [Fact]
    public void Lists_Date_Html_Object_Math_Starts_And_Whitespace()
    {
        var duplicated = MarkdownEditing.DuplicateListItem("- foo\n- bar\n", 0, 0);
        Assert.NotNull(duplicated);
        Assert.StartsWith("- foo\n- foo\n- bar", duplicated!.Value.Text.Replace("\r", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Null(MarkdownEditing.DuplicateListItem("plain\n", 0, 0));

        var ordered = MarkdownEditing.ConvertList("- [ ] foo\n- bar\n", 0, 0, ordered: true);
        Assert.NotNull(ordered);
        Assert.Contains("1. [ ] foo", ordered!.Value.Text, StringComparison.Ordinal);
        Assert.Contains("2. bar", ordered.Value.Text, StringComparison.Ordinal);
        var bullets = MarkdownEditing.ConvertList("1. [x] foo\n2. bar\n", 0, 0, ordered: false);
        Assert.NotNull(bullets);
        Assert.Contains("- [x] foo", bullets!.Value.Text, StringComparison.Ordinal);
        Assert.Contains("- bar", bullets.Value.Text, StringComparison.Ordinal);

        var dated = FrontMatter.EnsureDate("# Note\n", new DateTime(2020, 1, 2));
        Assert.Contains("date: 2020-01-02", dated, StringComparison.Ordinal);
        Assert.Equal(dated, FrontMatter.EnsureDate(dated, DateTime.Now));
        var fromStem = FrontMatter.EnsureDate("# Note\n", new DateTime(1999, 1, 1), "2026-09-10");
        Assert.Contains("date: 2026-09-10", fromStem, StringComparison.Ordinal);

        var stripped = MarkdownEditing.StripTrailingWhitespace("a  \n\nb  \n```\ncode  \n```\n");
        Assert.Contains("a\n\nb\n", stripped, StringComparison.Ordinal);
        Assert.Contains("code  ", stripped, StringComparison.Ordinal);
        Assert.DoesNotContain("b  ", stripped, StringComparison.Ordinal);

        Assert.Contains("ρ", MathText.ToDisplay(@"\rho"), StringComparison.Ordinal);
        Assert.Contains("⊆", MathText.ToDisplay(@"\subseteq"), StringComparison.Ordinal);
        Assert.Contains("°", MathText.ToDisplay(@"\degree"), StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("""
            @startuml
            object User
            object Order
            User --> Order
            @enduml
            """, out var objects));
        Assert.Contains(objects.Nodes, node => node.Label == "User");
        Assert.Contains(objects.Nodes, node => node.Label == "Order");
        Assert.Contains(objects.Edges, edge => edge.From == "User" && edge.To == "Order");
        var sample = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "object");
        Assert.Contains("```plantuml", sample.Text, StringComparison.Ordinal);
        Assert.Contains("object User", sample.Text, StringComparison.Ordinal);

        Assert.Equal(LanguageId.Bash, LanguageCatalog.FromInfo("dockerfile"));
        Assert.Equal(LanguageId.Bash, LanguageCatalog.FromInfo("makefile"));
        Assert.Equal(LanguageId.Yaml, LanguageCatalog.FromInfo("graphql"));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-html-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Html.md"), "---\ntitle: Hello world\n---\n<p>body</p>\n");
            File.WriteAllText(Path.Combine(dir, "Det.md"), "<details>\n<summary>Tip</summary>\nbody\n</details>\n");
            File.WriteAllText(Path.Combine(dir, "Other.md"), "---\ntitle: Other\n---\nplain\n");
            Assert.Contains(WikiIndex.NotesWithHtml([dir]), hit => hit.Name == "Html.md");
            Assert.DoesNotContain(WikiIndex.NotesWithHtml([dir]), hit => hit.Name == "Det.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "title STARTS Hel"), hit => hit.Name == "Html.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "title STARTS Hel"), hit => hit.Name == "Other.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "title CONTAINS world"), hit => hit.Name == "Html.md");
            Assert.Contains(WikiIndex.Dataview([dir], "WHERE title STARTS Hel"), hit => hit.Name == "Html.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Dataview_Nested_Where_Respects_Parentheses()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-where-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "DraftHigh.md"), "---\nstatus: draft\ntype: note\npriority: 5\n---\n# Draft high\n");
            File.WriteAllText(Path.Combine(dir, "WikiHigh.md"), "---\nstatus: published\ntype: wiki\npriority: 8\n---\n# Wiki high\n");
            File.WriteAllText(Path.Combine(dir, "DraftLow.md"), "---\nstatus: draft\ntype: note\npriority: 1\n---\n# Draft low\n");
            File.WriteAllText(Path.Combine(dir, "Other.md"), "---\nstatus: published\ntype: note\npriority: 9\n---\n# Other\n");

            var nested = WikiIndex.QueryFrontMatter([dir], "(status=draft OR type=wiki) AND priority>=5");
            Assert.Contains(nested, hit => hit.Name == "DraftHigh.md");
            Assert.Contains(nested, hit => hit.Name == "WikiHigh.md");
            Assert.DoesNotContain(nested, hit => hit.Name == "DraftLow.md");
            Assert.DoesNotContain(nested, hit => hit.Name == "Other.md");

            var grouped = WikiIndex.QueryFrontMatter([dir], "((status=draft AND type=note) OR type=wiki) AND priority>=5");
            Assert.Contains(grouped, hit => hit.Name == "DraftHigh.md");
            Assert.Contains(grouped, hit => hit.Name == "WikiHigh.md");
            Assert.DoesNotContain(grouped, hit => hit.Name == "DraftLow.md");

            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "status IN (draft, published) AND type=note"), hit => hit.Name == "DraftHigh.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "status IN (draft, published) AND type=note"), hit => hit.Name == "WikiHigh.md");
            Assert.Contains(WikiIndex.Dataview([dir], "FROM WHERE (status=draft OR type=wiki) AND priority>=5"), hit => hit.Name == "WikiHigh.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
