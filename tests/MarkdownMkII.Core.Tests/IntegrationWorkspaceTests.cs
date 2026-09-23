using MarkdownMkII.Core.Highlight;
using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Preview;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public partial class FoldGitSnippetTests
{

    [Fact]
    public void Snippets_Export_And_GitLog_Cover_Vault_Tools()
    {
        var now = new DateTimeOffset(2026, 9, 10, 15, 4, 0, TimeSpan.Zero);
        var week = MarkdownSnippets.TryExpand("w :week", 7, now);
        Assert.NotNull(week);
        Assert.Equal("w 2026-W37", week.Value.Text);
        var file = MarkdownSnippets.TryExpand(":filename", 9, now, "Note.md");
        Assert.Equal("Note", file!.Value.Text);
        var stamped = FrontMatter.StampUpdated("# Hi\n", now);
        Assert.Contains("updated: 2026-09-10", stamped, StringComparison.Ordinal);
        Assert.Equal(["Home", "Index"], FrontMatter.Aliases(new Dictionary<string, string> { ["aliases"] = "[Home, Index]" }));
        var block = MarkdownEditing.InsertBlockId("Hello", 0, 0, "abc");
        Assert.Contains("^abc", block.Text, StringComparison.Ordinal);
        var graph = WikiIndex.ToMermaid(new WikiGraph(
            [new WikiGraphNode("a.md", "Alpha", 1, 0)],
            [new WikiGraphEdge("Alpha", "Beta", true)]));
        Assert.Contains("flowchart LR", graph, StringComparison.Ordinal);
        Assert.Contains("Alpha", graph, StringComparison.Ordinal);
        Assert.Equal("href=\"Note.html#x\"", ExportHtmlService.RewriteMarkdownHrefs("href=\"Note.md#x\""));
        Assert.Equal("href=\"notes/Spoke.html\"", ExportHtmlService.RewriteMarkdownHrefs("href=\"notes/Spoke.md\""));
    }

    [Fact]
    public void Ego_Archive_Id_Path_Regex_And_Backlinks()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
    }

    [Fact]
    public void Yearly_Hubs_DeadEnds_Reflow_Created_And_GitLog()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-graph-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
    }

    [Fact]
    public void Headings_Unused_Quarterly_Sources_Table_And_Strip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-next-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var assets = Path.Combine(dir, "assets");
        Directory.CreateDirectory(assets);
    }

    [Fact]
    public void Usecase_Tags_Unwrap_Details_Blame_And_Vault_Keys()
    {
        Assert.True(PlantUmlParser.TryParse("""
            @startuml
            actor User
            User --> (Login)
            (Login) --> (Home)
            @enduml
            """, out var usecase));
        Assert.Contains(usecase.Nodes, node => node.Label == "User");
        Assert.Contains(usecase.Edges, edge => edge.From == "User" && edge.To == "Login");
        var sample = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "usecase");
        Assert.Contains("```plantuml", sample.Text, StringComparison.Ordinal);
        Assert.Contains("actor User", sample.Text, StringComparison.Ordinal);

        var tagged = FrontMatter.EnsureTags("# Note\n", "inbox");
        Assert.Contains("tags:", tagged, StringComparison.Ordinal);
        Assert.Contains("inbox", tagged, StringComparison.Ordinal);
        Assert.Equal(tagged, FrontMatter.EnsureTags(tagged, "Inbox"));

        Assert.Equal("See Hi and Other", WikiLinks.Unwrap("See [[Note|Hi]] and [[Other]]"));
        Assert.Equal("![[pic.png]]", WikiLinks.Unwrap("![[pic.png]]"));

        var details = MarkdownEditing.WrapDetails("hello", 0, 5, "Tip");
        Assert.Contains("<details>", details.Text, StringComparison.Ordinal);
        Assert.Contains("<summary>Tip</summary>", details.Text, StringComparison.Ordinal);
        Assert.Contains("hello", details.Text, StringComparison.Ordinal);

        var section = MarkdownEditing.DuplicateHeadingSection("# A\n\nbody\n\n# B\n", 0);
        Assert.NotNull(section);
        Assert.Equal(2, section!.Value.Text.Split('\n').Count(line => line.StartsWith("# A", StringComparison.Ordinal)));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "_templates"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "Draft.md"), "---\ndraft: true\nstatus: wip\n---\n# D\n");
            File.WriteAllText(Path.Combine(dir, "_templates", "Meet.md"), "# Meet\n");
            File.WriteAllText(Path.Combine(dir, "Live.md"), "# Live\n");
            Assert.Contains(WikiIndex.NotesWithDraft([dir]), hit => hit.Name == "Draft.md");
            Assert.DoesNotContain(WikiIndex.NotesWithDraft([dir]), hit => hit.Name == "Live.md");
            Assert.Contains(WikiIndex.TemplateNotes([dir]), hit => hit.Name == "Meet.md");
            Assert.Contains(WikiIndex.NotesWithYamlKey([dir], "status"), hit => hit.Name == "Draft.md");
            Assert.DoesNotContain(WikiIndex.NotesWithYamlKey([dir], "status"), hit => hit.Name == "Live.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Wiki_Urls_Ids_Lists_Setext_And_Git_Stat()
    {
        var lists = MarkdownEditing.NormalizeListMarkers("* item\n+ other\n* [ ] task\n- keep\n1. numbered\n```\n* fenced\n```\n");
        Assert.Contains("- item", lists.Text, StringComparison.Ordinal);
        Assert.Contains("- other", lists.Text, StringComparison.Ordinal);
        Assert.Contains("- [ ] task", lists.Text, StringComparison.Ordinal);
        Assert.Contains("- keep", lists.Text, StringComparison.Ordinal);
        Assert.Contains("1. numbered", lists.Text, StringComparison.Ordinal);
        Assert.Contains("* fenced", lists.Text, StringComparison.Ordinal);

        var setext = MarkdownEditing.ToSetextHeading("# Title\n\nbody\n", 0);
        Assert.NotNull(setext);
        Assert.StartsWith("Title\n=====", setext!.Value.Text, StringComparison.Ordinal);
        Assert.Null(MarkdownEditing.ToSetextHeading("### H3\n", 0));
        Assert.Null(MarkdownEditing.ToSetextHeading("Title\n=====\n", 0));

        var h2 = MarkdownEditing.ToSetextHeading("## Sub\n", 0);
        Assert.NotNull(h2);
        Assert.StartsWith("Sub\n---", h2!.Value.Text, StringComparison.Ordinal);

        var yaml = "---\ntitle: A\ntags: inbox\n---\n# Note\n";
        var withoutTags = FrontMatter.RemoveKey(yaml, "tags");
        Assert.Contains("title: A", withoutTags, StringComparison.Ordinal);
        Assert.DoesNotContain("tags:", withoutTags, StringComparison.Ordinal);
        var stripped = FrontMatter.RemoveKey(withoutTags, "title");
        Assert.Equal("# Note\n", stripped);
        Assert.Equal(yaml, FrontMatter.RemoveKey(yaml, "missing"));

        var table = "| A | B |\n| --- | --- |\n| 1 | 2 |\n";
        var tsv = MarkdownEditing.TableToDelimited(table, 0, '\t');
        Assert.NotNull(tsv);
        Assert.Contains("A\tB", tsv, StringComparison.Ordinal);
        Assert.Contains("1\t2", tsv, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-wiki-stat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Wiki.md"), "See [[Note]]\n");
            File.WriteAllText(Path.Combine(dir, "Emb.md"), "![[pic.png]]\n");
            File.WriteAllText(Path.Combine(dir, "Web.md"), "Visit https://example.com/docs\n");
            File.WriteAllText(Path.Combine(dir, "A.md"), "---\nid: same\n---\n# A\n");
            File.WriteAllText(Path.Combine(dir, "B.md"), "---\nid: same\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "C.md"), "---\nid: other\n---\n# C\n");
            File.WriteAllText(Path.Combine(dir, "Draft.md"), "---\ndraft: true\n---\n# D\n");
            File.WriteAllText(Path.Combine(dir, "Live.md"), "---\ntitle: Live\n---\n# Live\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "# Plain\n");

            Assert.Contains(WikiIndex.NotesWithWikilinks([dir]), hit => hit.Name == "Wiki.md");
            Assert.DoesNotContain(WikiIndex.NotesWithWikilinks([dir]), hit => hit.Name == "Emb.md");
            Assert.Contains(WikiIndex.NotesWithoutWikilinks([dir]), hit => hit.Name == "Emb.md");
            Assert.Contains(WikiIndex.NotesWithoutWikilinks([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutWikilinks([dir]), hit => hit.Name == "Wiki.md");
            Assert.Contains(WikiIndex.NotesWithUrls([dir]), hit => hit.Name == "Web.md");
            Assert.DoesNotContain(WikiIndex.NotesWithUrls([dir]), hit => hit.Name == "Plain.md");
            Assert.Contains(WikiIndex.DuplicateIds([dir]), hit => hit.Name == "A.md" && hit.Preview.StartsWith("same", StringComparison.Ordinal));
            Assert.Contains(WikiIndex.DuplicateIds([dir]), hit => hit.Name == "B.md");
            Assert.DoesNotContain(WikiIndex.DuplicateIds([dir]), hit => hit.Name == "C.md");
            Assert.Contains(WikiIndex.PublishedNotes([dir]), hit => hit.Name == "Live.md");
            Assert.DoesNotContain(WikiIndex.PublishedNotes([dir]), hit => hit.Name == "Draft.md");
            Assert.DoesNotContain(WikiIndex.PublishedNotes([dir]), hit => hit.Name == "Plain.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Setext_Details_TableHtml_YamlRename_Fixme_And_NumStat()
    {
        var setext = MarkdownEditing.ToSetextHeading("# Title\n\nbody\n", 0);
        Assert.NotNull(setext);
        var atx = MarkdownEditing.FromSetextHeading(setext!.Value.Text, 0);
        Assert.NotNull(atx);
        Assert.StartsWith("# Title", atx!.Value.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("=====", atx.Value.Text, StringComparison.Ordinal);
        Assert.Null(MarkdownEditing.FromSetextHeading("# Title\n", 0));

        var wrapped = MarkdownEditing.WrapDetails("hello", 0, 5, "Tip");
        var unwrapped = MarkdownEditing.UnwrapDetails(wrapped.Text, 0);
        Assert.NotNull(unwrapped);
        Assert.Contains("hello", unwrapped!.Value.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<details>", unwrapped.Value.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<summary>", unwrapped.Value.Text, StringComparison.Ordinal);

        var yaml = "---\ntitle: A\ntags: inbox\n---\n# Note\n";
        var renamed = FrontMatter.RenameKey(yaml, "tags", "topics");
        Assert.Contains("topics: inbox", renamed, StringComparison.Ordinal);
        Assert.DoesNotContain("tags:", renamed, StringComparison.Ordinal);
        Assert.Equal(yaml, FrontMatter.RenameKey(yaml, "tags", "title"));
        Assert.Equal(yaml, FrontMatter.RenameKey(yaml, "missing", "topics"));

        var table = "| A | B |\n| --- | --- |\n| 1 | 22 |\n";
        var html = MarkdownEditing.TableToHtml(table, 0);
        Assert.NotNull(html);
        Assert.Contains("<th>A</th>", html, StringComparison.Ordinal);
        Assert.Contains("<td>22</td>", html, StringComparison.Ordinal);
        var formatted = MarkdownEditing.FormatTable("|A|B|\n|---|---|\n|1|22|\n", 0, 0);
        Assert.NotNull(formatted);
        Assert.Contains("| A ", formatted!.Value.Text, StringComparison.Ordinal);
        Assert.Contains("| 22", formatted.Value.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-fixme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Fix.md"), "Need to FIXME later\n");
            File.WriteAllText(Path.Combine(dir, "Code.md"), "```\nTODO in fence\n```\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "# Plain\n");
            Assert.Contains(WikiIndex.NotesWithFixme([dir]), hit => hit.Name == "Fix.md");
            Assert.DoesNotContain(WikiIndex.NotesWithFixme([dir]), hit => hit.Name == "Code.md");
            Assert.DoesNotContain(WikiIndex.NotesWithFixme([dir]), hit => hit.Name == "Plain.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Quotes_Lists_Description_Diff_Details_And_Setext_Query()
    {
        var nested = MarkdownEditing.NestQuote("said\n", 0, 0);
        Assert.StartsWith("> said", nested.Text, StringComparison.Ordinal);
        var unnested = MarkdownEditing.UnnestQuote(nested.Text, 0, 0);
        Assert.StartsWith("said", unnested.Text, StringComparison.Ordinal);

        var sorted = MarkdownEditing.SortListItems("- zeta\n- Alpha\n- beta\n", 0, 0);
        Assert.NotNull(sorted);
        var sortedLines = sorted!.Value.Text.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        Assert.Contains("Alpha", sortedLines[0], StringComparison.Ordinal);
        Assert.Contains("beta", sortedLines[1], StringComparison.Ordinal);
        Assert.Contains("zeta", sortedLines[2], StringComparison.Ordinal);

        var reversed = MarkdownEditing.ReverseSelectedLines("a\nb\nc\n", 0, 5);
        Assert.StartsWith("c", reversed.Text, StringComparison.Ordinal);

        var described = FrontMatter.EnsureDescription("# Title\n\nHello world from the vault.\n");
        Assert.Contains("description: Hello world from the vault.", described, StringComparison.Ordinal);
        Assert.Equal(described, FrontMatter.EnsureDescription(described));

        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("patch"));
        var tokens = CodeTokenizer.Tokenize("--- a\n+++ b\n+added\n-removed\n", "diff");
        Assert.Contains(tokens, token => token.Kind == CodeTokenKind.Keyword);
        Assert.Contains(tokens, token => token.Kind == CodeTokenKind.TypeName);
        Assert.Contains(tokens, token => token.Kind == CodeTokenKind.Comment);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-details-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Det.md"), "<details>\n<summary>Tip</summary>\nbody\n</details>\n");
            File.WriteAllText(Path.Combine(dir, "Set.md"), "Title\n=====\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "# Plain\n");
            Assert.Contains(WikiIndex.NotesWithDetails([dir]), hit => hit.Name == "Det.md");
            Assert.DoesNotContain(WikiIndex.NotesWithDetails([dir]), hit => hit.Name == "Plain.md");
            Assert.Contains(WikiIndex.NotesWithSetext([dir]), hit => hit.Name == "Set.md");
            Assert.DoesNotContain(WikiIndex.NotesWithSetext([dir]), hit => hit.Name == "Plain.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Lists_Status_Tables_Comments_Ends_And_GitNameStatus()
    {
        var moved = MarkdownEditing.MoveListItem("- a\n- b\n- c\n", 0, 0, 1);
        Assert.NotNull(moved);
        Assert.StartsWith("- b\n- a\n- c", moved!.Value.Text.Replace("\r", "", StringComparison.Ordinal), StringComparison.Ordinal);
        var deleted = MarkdownEditing.DeleteListItem("- a\n- b\n", 0, 0);
        Assert.NotNull(deleted);
        Assert.StartsWith("- b", deleted!.Value.Text.Replace("\r", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Null(MarkdownEditing.DeleteListItem("plain\n", 0, 0));

        var status = FrontMatter.EnsureStatus("# Note\n");
        Assert.Contains("status: draft", status, StringComparison.Ordinal);
        Assert.Equal(status, FrontMatter.EnsureStatus(status, "published"));
        var custom = FrontMatter.EnsureStatus("# Note\n", "wip");
        Assert.Contains("status: wip", custom, StringComparison.Ordinal);

        Assert.Contains("∧", MathText.ToDisplay(@"\wedge"), StringComparison.Ordinal);
        Assert.Contains("∴", MathText.ToDisplay(@"\therefore"), StringComparison.Ordinal);
        Assert.Equal(LanguageId.CSharp, LanguageCatalog.FromInfo("swift"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("status"));
        Assert.Equal(LanguageId.Java, LanguageCatalog.FromInfo("dart"));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-table-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Tab.md"), "---\ntitle: Hello world\n---\n| A | B |\n| --- | --- |\n| 1 | 2 |\n");
            File.WriteAllText(Path.Combine(dir, "Cmt.md"), "Hello<!-- note -->\n");
            File.WriteAllText(Path.Combine(dir, "Task.md"), "- [ ] do it\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "# Plain\n");
            Assert.Contains(WikiIndex.NotesWithTables([dir]), hit => hit.Name == "Tab.md");
            Assert.DoesNotContain(WikiIndex.NotesWithTables([dir]), hit => hit.Name == "Plain.md");
            Assert.Contains(WikiIndex.NotesWithComments([dir]), hit => hit.Name == "Cmt.md");
            Assert.DoesNotContain(WikiIndex.NotesWithComments([dir]), hit => hit.Name == "Plain.md");
            Assert.Contains(WikiIndex.NotesWithoutTasks([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutTasks([dir]), hit => hit.Name == "Task.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "title ENDS world"), hit => hit.Name == "Tab.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "title ENDS world"), hit => hit.Name == "Cmt.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Indent_Callout_Author_Block_Images_Reflog_And_Numeric_Query()
    {
        var indented = MarkdownEditing.IndentList("- a\n- b\n", 0, 0);
        Assert.NotNull(indented);
        Assert.StartsWith("  - a\n  - b", indented!.Value.Text.Replace("\r", "", StringComparison.Ordinal), StringComparison.Ordinal);
        var outdented = MarkdownEditing.OutdentList(indented.Value.Text, 0, 0);
        Assert.NotNull(outdented);
        Assert.StartsWith("- a\n- b", outdented!.Value.Text.Replace("\r", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Null(MarkdownEditing.IndentList("plain\n", 0, 0));

        var callout = MarkdownEditing.QuoteToCallout("> said\n", 0, 0);
        Assert.NotNull(callout);
        Assert.Contains(":::info", callout!.Value.Text, StringComparison.Ordinal);
        Assert.Contains("said", callout.Value.Text, StringComparison.Ordinal);
        Assert.Null(MarkdownEditing.QuoteToCallout("plain\n", 0, 0));

        var authored = FrontMatter.EnsureAuthor("# Note\n", "Marco");
        Assert.Contains("author: Marco", authored, StringComparison.Ordinal);
        Assert.Equal(authored, FrontMatter.EnsureAuthor(authored, "Other"));
        var css = FrontMatter.EnsureCssclass("# Note\n", "wide");
        Assert.Contains("cssclass:", css, StringComparison.Ordinal);
        Assert.Contains("wide", css, StringComparison.Ordinal);
        Assert.Equal(css, FrontMatter.EnsureCssclass(css, "Wide"));

        Assert.Equal(LanguageId.Rust, LanguageCatalog.FromInfo("zig"));
        Assert.Equal(LanguageId.Bash, LanguageCatalog.FromInfo("cmake"));
        Assert.Equal(LanguageId.CSharp, LanguageCatalog.FromInfo("protobuf"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("reflog"));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-block-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Id.md"), "---\npriority: 10\ncreated: 2026-09-10\n---\nHello ^abc\n");
            File.WriteAllText(Path.Combine(dir, "Def.md"), "Term\n: Definition\n");
            File.WriteAllText(Path.Combine(dir, "Pic.md"), "![alt](pic.png)\n");
            File.WriteAllText(Path.Combine(dir, "Fence.md"), "```\ncode ^skip\n```\n");
            File.WriteAllText(Path.Combine(dir, "Low.md"), "---\npriority: 2\ncreated: 2020-01-01\n---\nplain\n");
            Assert.Contains(WikiIndex.NotesWithBlockIds([dir]), hit => hit.Name == "Id.md");
            Assert.DoesNotContain(WikiIndex.NotesWithBlockIds([dir]), hit => hit.Name == "Fence.md");
            Assert.Contains(WikiIndex.NotesWithDefinitionLists([dir]), hit => hit.Name == "Def.md");
            Assert.DoesNotContain(WikiIndex.NotesWithDefinitionLists([dir]), hit => hit.Name == "Pic.md");
            Assert.Contains(WikiIndex.NotesWithMarkdownImages([dir]), hit => hit.Name == "Pic.md");
            Assert.DoesNotContain(WikiIndex.NotesWithMarkdownImages([dir]), hit => hit.Name == "Def.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "priority>=5"), hit => hit.Name == "Id.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "priority>=5"), hit => hit.Name == "Low.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "priority < 3"), hit => hit.Name == "Low.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "created > 2026-01-01"), hit => hit.Name == "Id.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "created > 2026-01-01"), hit => hit.Name == "Low.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Callout_Tasks_Lang_Stash_Hr_And_Duplicate_Blocks()
    {
        var quoted = MarkdownEditing.CalloutToQuote(":::info\nsaid\n:::\n", 0, 0);
        Assert.NotNull(quoted);
        Assert.Contains("> said", quoted!.Value.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(":::", quoted.Value.Text, StringComparison.Ordinal);
        Assert.Null(MarkdownEditing.CalloutToQuote("plain\n", 0, 0));
        Assert.Null(MarkdownEditing.UnwrapCallout("plain\n", 0, 0));

        var tasks = MarkdownEditing.LinesToTaskList("hello\nworld\n", 0, 12);
        Assert.NotNull(tasks);
        Assert.Contains("- [ ] hello", tasks!.Value.Text, StringComparison.Ordinal);
        Assert.Contains("- [ ] world", tasks.Value.Text, StringComparison.Ordinal);
        Assert.Null(MarkdownEditing.LinesToTaskList("- [ ] foo\n", 0, 0));
        var fromList = MarkdownEditing.LinesToTaskList("- a\n", 0, 0);
        Assert.NotNull(fromList);
        Assert.Contains("- [ ] a", fromList!.Value.Text, StringComparison.Ordinal);
        var fromOrdered = MarkdownEditing.LinesToTaskList("1. [x] done\n", 0, 0);
        Assert.NotNull(fromOrdered);
        Assert.Contains("- [x] done", fromOrdered!.Value.Text, StringComparison.Ordinal);

        var sentence = MarkdownEditing.SentenceCase("hello world. next", 0, "hello world. next".Length);
        Assert.Equal("Hello world. Next", sentence.Text);

        var lang = FrontMatter.EnsureLang("# Note\n", "it");
        Assert.Contains("lang: it", lang, StringComparison.Ordinal);
        Assert.Equal(lang, FrontMatter.EnsureLang(lang, "en"));
        var typed = FrontMatter.EnsureType("# Note\n", "note");
        Assert.Contains("type: note", typed, StringComparison.Ordinal);
        Assert.Equal(typed, FrontMatter.EnsureType(typed, "wiki"));

        Assert.Equal(LanguageId.Xml, LanguageCatalog.FromInfo("svelte"));
        Assert.Equal(LanguageId.Xml, LanguageCatalog.FromInfo("astro"));
        Assert.Equal(LanguageId.Yaml, LanguageCatalog.FromInfo("terraform"));
        Assert.Equal(LanguageId.Yaml, LanguageCatalog.FromInfo("hcl"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("stash"));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-stash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "DupA.md"), "Hello ^abc\n");
            File.WriteAllText(Path.Combine(dir, "DupB.md"), "Other ^abc\n");
            File.WriteAllText(Path.Combine(dir, "Unique.md"), "Only ^uniq\n");
            File.WriteAllText(Path.Combine(dir, "Hr.md"), "# Title\n\n---\n");
            File.WriteAllText(Path.Combine(dir, "Yaml.md"), "---\ntitle: x\nlang: it\n---\nbody\n");
            File.WriteAllText(Path.Combine(dir, "Setext.md"), "Title\n---\n");
            File.WriteAllText(Path.Combine(dir, "Pic.md"), "![alt](pic.png)\n");
            File.WriteAllText(Path.Combine(dir, "NoPic.md"), "plain text\n");
            File.WriteAllText(Path.Combine(dir, "Fence.md"), "```\n---\n^skip\n```\n");
            Assert.Contains(WikiIndex.DuplicateBlockIds([dir]), hit => hit.Name == "DupA.md");
            Assert.Contains(WikiIndex.DuplicateBlockIds([dir]), hit => hit.Name == "DupB.md");
            Assert.DoesNotContain(WikiIndex.DuplicateBlockIds([dir]), hit => hit.Name == "Unique.md");
            Assert.Contains(WikiIndex.NotesWithHr([dir]), hit => hit.Name == "Hr.md");
            Assert.DoesNotContain(WikiIndex.NotesWithHr([dir]), hit => hit.Name == "Yaml.md");
            Assert.DoesNotContain(WikiIndex.NotesWithHr([dir]), hit => hit.Name == "Setext.md");
            Assert.DoesNotContain(WikiIndex.NotesWithHr([dir]), hit => hit.Name == "Fence.md");
            Assert.Contains(WikiIndex.NotesWithoutImages([dir]), hit => hit.Name == "NoPic.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutImages([dir]), hit => hit.Name == "Pic.md");
            Assert.Contains(WikiIndex.NotesWithYamlKey([dir], "lang"), hit => hit.Name == "Yaml.md");
            Assert.DoesNotContain(WikiIndex.NotesWithYamlKey([dir], "lang"), hit => hit.Name == "Hr.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Dataview_Or_In_Deployment_BlockRefs_Tags_And_Yaml_Project()
    {
        var stripped = MarkdownEditing.TaskListToLines("- [ ] hello\n- [x] done\nplain\n", 0, 0);
        Assert.NotNull(stripped);
        Assert.StartsWith("hello", stripped!.Value.Text, StringComparison.Ordinal);
        Assert.Null(MarkdownEditing.TaskListToLines("plain\n", 0, 0));

        var project = FrontMatter.EnsureProject("# Note\n", "vault");
        Assert.Contains("project: vault", project, StringComparison.Ordinal);
        Assert.Equal(project, FrontMatter.EnsureProject(project, "other"));
        var priority = FrontMatter.EnsurePriority("# Note\n", "5");
        Assert.Contains("priority: 5", priority, StringComparison.Ordinal);
        var sorted = FrontMatter.SortKeys("---\nz: 1\na: 2\n---\nbody\n");
        var aAt = sorted.IndexOf("a:", StringComparison.Ordinal);
        var zAt = sorted.IndexOf("z:", StringComparison.Ordinal);
        Assert.True(aAt >= 0 && zAt > aAt);

        Assert.True(PlantUmlParser.TryParse("artifact App\nnode Server\n[App] --> [Server]\n", out var deploy));
        Assert.Contains(deploy.Nodes, node => node.Label.Contains("App", StringComparison.Ordinal));
        Assert.Equal(MermaidKind.Flow, deploy.Kind);
        var sample = MarkdownEditing.InsertMermaid("", 0, 0, "deployment");
        Assert.Contains("artifact App", sample.Text, StringComparison.Ordinal);

        Assert.Contains("ι", MathText.ToDisplay("\\iota"), StringComparison.Ordinal);
        Assert.Contains("↔", MathText.ToDisplay("\\leftrightarrow"), StringComparison.Ordinal);
        Assert.Equal(LanguageId.Markdown, LanguageCatalog.FromInfo("mdx"));
        Assert.Equal(LanguageId.Bash, LanguageCatalog.FromInfo("nix"));
        Assert.Equal(LanguageId.CSharp, LanguageCatalog.FromInfo("prisma"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("tags"));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-or-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Target.md"), "Hello ^abc\n");
            File.WriteAllText(Path.Combine(dir, "Good.md"), "See [[Target#^abc]]\n");
            File.WriteAllText(Path.Combine(dir, "Bad.md"), "See [[Target#^missing]]\n");
            File.WriteAllText(Path.Combine(dir, "Head.md"), "See [[Target#Nope]]\n");
            File.WriteAllText(Path.Combine(dir, "Draft.md"), "---\nstatus: draft\ntype: note\naliases: [shared]\n---\n# Titled\n");
            File.WriteAllText(Path.Combine(dir, "Live.md"), "---\nstatus: published\ntype: wiki\naliases: [shared]\n---\nplain\n");
            File.WriteAllText(Path.Combine(dir, "Slides.md"), "# A\n\n---\n\n# B\n\n---\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "no heading here\n");
            Assert.Contains(WikiIndex.BrokenBlockRefs([dir]), hit => hit.Name == "Bad.md");
            Assert.DoesNotContain(WikiIndex.BrokenBlockRefs([dir]), hit => hit.Name == "Good.md");
            Assert.DoesNotContain(WikiIndex.BrokenHeadings([dir]), hit => hit.Name == "Bad.md");
            Assert.Contains(WikiIndex.BrokenHeadings([dir]), hit => hit.Name == "Head.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "status IN (draft, published)"), hit => hit.Name == "Draft.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "status IN (draft, published)"), hit => hit.Name == "Live.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "status=draft OR type=wiki"), hit => hit.Name == "Draft.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "status=draft OR type=wiki"), hit => hit.Name == "Live.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "status=draft AND type=note"), hit => hit.Name == "Draft.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "status=draft AND type=note"), hit => hit.Name == "Live.md");
            Assert.Contains(WikiIndex.NotesWithoutHeadings([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutHeadings([dir]), hit => hit.Name == "Draft.md");
            Assert.Contains(WikiIndex.NotesWithSlides([dir]), hit => hit.Name == "Slides.md");
            Assert.DoesNotContain(WikiIndex.NotesWithSlides([dir]), hit => hit.Name == "Plain.md");
            Assert.Contains(WikiIndex.DuplicateAliases([dir]), hit => hit.Name == "Draft.md");
            Assert.Contains(WikiIndex.DuplicateAliases([dir]), hit => hit.Name == "Live.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Empty_Embeds_Callout_BlockIds_Branches_And_Mindmap()
    {
        var cycled = MarkdownEditing.CycleCalloutKind(":::info\nsaid\n:::\n", 0, 0);
        Assert.NotNull(cycled);
        Assert.Contains(":::warning", cycled!.Value.Text, StringComparison.Ordinal);
        var ids = MarkdownEditing.AssignMissingBlockIds("# Hello\n# Other\n");
        Assert.NotNull(ids);
        Assert.Contains("^hello", ids!.Value.Text, StringComparison.Ordinal);
        Assert.Contains("^other", ids.Value.Text, StringComparison.Ordinal);
        Assert.Null(MarkdownEditing.AssignMissingBlockIds("# Hello ^hello\n"));

        var series = FrontMatter.EnsureSeries("# Note\n", "alpha");
        Assert.Contains("series: alpha", series, StringComparison.Ordinal);
        Assert.Equal(series, FrontMatter.EnsureSeries(series, "beta"));
        var license = FrontMatter.EnsureLicense("# Note\n", "MIT");
        Assert.Contains("license: MIT", license, StringComparison.Ordinal);
        var source = FrontMatter.EnsureSource("# Note\n", "https://example.com");
        Assert.Contains("source: https://example.com", source, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("mindmap\n  root((Idea))\n    Topic\n    Other\n", out var mind));
        Assert.Contains(mind.Nodes, node => node.Label.Contains("Idea", StringComparison.Ordinal));
        Assert.True(mind.Edges.Count >= 2);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Blank.md"), "---\nstatus:\ntitle: x\n---\n# Titled\n");
            File.WriteAllText(Path.Combine(dir, "Draft.md"), "---\nstatus: draft\ndescription: hello\naliases: [a]\n---\n# Titled\n");
            File.WriteAllText(Path.Combine(dir, "Embed.md"), "![[MissingNote]]\n");
            File.WriteAllText(Path.Combine(dir, "Target.md"), "# Target\n");
            File.WriteAllText(Path.Combine(dir, "GoodEmbed.md"), "![[Target]]\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "status IS EMPTY"), hit => hit.Name == "Blank.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "status IS EMPTY"), hit => hit.Name == "Draft.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "status IS NOT EMPTY"), hit => hit.Name == "Draft.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "status IS NOT EMPTY"), hit => hit.Name == "Blank.md");
            Assert.Contains(WikiIndex.BrokenEmbeds([dir]), hit => hit.Name == "Embed.md");
            Assert.DoesNotContain(WikiIndex.BrokenEmbeds([dir]), hit => hit.Name == "GoodEmbed.md");
            Assert.Contains(WikiIndex.NotesWithoutAliases([dir]), hit => hit.Name == "Blank.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutAliases([dir]), hit => hit.Name == "Draft.md");
            Assert.Contains(WikiIndex.NotesWithoutDescription([dir]), hit => hit.Name == "Blank.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutDescription([dir]), hit => hit.Name == "Draft.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Regex_Descriptions_Worktrees_GitGraph_And_Edits()
    {
        var duplicated = MarkdownEditing.DuplicateLine("a\nb\n", 0, 0);
        Assert.Contains("a\na\n", duplicated.Text, StringComparison.Ordinal);
        var callout = MarkdownEditing.InsertCallout("body", 0, 4, "tip");
        Assert.Contains(":::tip", callout.Text, StringComparison.Ordinal);
        Assert.Contains("body", callout.Text, StringComparison.Ordinal);
        var stripped = MarkdownEditing.StripBlockIds("# Hello ^hello\ncode\n");
        Assert.NotNull(stripped);
        Assert.Contains("# Hello\n", stripped!.Value.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("^hello", stripped.Value.Text, StringComparison.Ordinal);

        var cover = FrontMatter.EnsureCover("# Note\n", "cover.png");
        Assert.Contains("cover: cover.png", cover, StringComparison.Ordinal);
        Assert.Equal(cover, FrontMatter.EnsureCover(cover, "other.png"));
        var canonical = FrontMatter.EnsureCanonical("# Note\n", "https://example.com/note");
        Assert.Contains("canonical: https://example.com/note", canonical, StringComparison.Ordinal);
        var keywords = FrontMatter.EnsureKeywords("# Note\n", "alpha");
        Assert.Contains("keywords:", keywords, StringComparison.Ordinal);
        Assert.Contains("alpha", keywords, StringComparison.Ordinal);
        Assert.Equal(keywords, FrontMatter.EnsureKeywords(keywords, "ALPHA"));

        Assert.True(MermaidParser.TryParse("gitGraph\n    commit\n    commit id: \"A\"\n    commit id: \"B\"\n", out var graph));
        Assert.True(graph.Nodes.Count >= 3);
        Assert.True(graph.Edges.Count >= 2);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-regex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Hello.md"), "---\ntitle: Hello World\ndescription: same\n---\n# Hello\n");
            File.WriteAllText(Path.Combine(dir, "Help.md"), "---\ntitle: Help Desk\ndescription: same\n---\n# Help\n");
            File.WriteAllText(Path.Combine(dir, "Other.md"), "---\ntitle: Other\nlicense: MIT\n---\n# Other\n");
            File.WriteAllText(Path.Combine(dir, "Bare.md"), "---\ntitle: Bare\n---\n[[Hello]]\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "title MATCHES ^Hel"), hit => hit.Name == "Hello.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "title MATCHES ^Hel"), hit => hit.Name == "Help.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "title MATCHES ^Hel"), hit => hit.Name == "Other.md");
            Assert.Contains(WikiIndex.DuplicateDescriptions([dir]), hit => hit.Name == "Hello.md");
            Assert.Contains(WikiIndex.DuplicateDescriptions([dir]), hit => hit.Name == "Help.md");
            Assert.DoesNotContain(WikiIndex.DuplicateDescriptions([dir]), hit => hit.Name == "Other.md");
            Assert.Contains(WikiIndex.NotesWithoutLicense([dir]), hit => hit.Name == "Hello.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutLicense([dir]), hit => hit.Name == "Other.md");
            Assert.Contains(WikiIndex.FindBacklinks([dir], "Hello"), hit => hit.Name == "Bare.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Between_Cover_Submodules_Timeline_And_Bullets()
    {
        var cycled = MarkdownEditing.CycleBulletMarker("- a\n* b\n", 0, 0);
        Assert.NotNull(cycled);
        Assert.StartsWith("* a\n", cycled!.Value.Text);

        var subtitle = FrontMatter.EnsureSubtitle("# Note\n", "Lead");
        Assert.Contains("subtitle: Lead", subtitle, StringComparison.Ordinal);
        Assert.Equal(subtitle, FrontMatter.EnsureSubtitle(subtitle, "Other"));
        var category = FrontMatter.EnsureCategory("# Note\n", "wiki");
        Assert.Contains("category: wiki", category, StringComparison.Ordinal);
        var version = FrontMatter.EnsureVersion("# Note\n");
        Assert.Contains("version: 1.0.0", version, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("timeline\n    title History\n    2020 : Start\n    2024 : Now\n", out var timeline));
        Assert.True(timeline.Nodes.Count >= 2);
        Assert.Contains(timeline.Nodes, node => node.Label.Contains("Start", StringComparison.Ordinal));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-between-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Low.md"), "---\npriority: 1\ncover: a.png\ncanonical: https://a\n---\n```mermaid\nflowchart TD\nA-->B\n```\n");
            File.WriteAllText(Path.Combine(dir, "Mid.md"), "---\npriority: 3\n---\n```plantuml\nAlice -> Bob\n```\n");
            File.WriteAllText(Path.Combine(dir, "High.md"), "---\npriority: 9\ncover: b.png\n---\nplain\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "priority BETWEEN 2, 5"), hit => hit.Name == "Mid.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "priority BETWEEN 2, 5"), hit => hit.Name == "Low.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "priority BETWEEN 2, 5"), hit => hit.Name == "High.md");
            Assert.Contains(WikiIndex.NotesWithoutCover([dir]), hit => hit.Name == "Mid.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutCover([dir]), hit => hit.Name == "Low.md");
            Assert.Contains(WikiIndex.NotesWithoutCanonical([dir]), hit => hit.Name == "Mid.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutCanonical([dir]), hit => hit.Name == "Low.md");
            Assert.Contains(WikiIndex.NotesWithMermaid([dir]), hit => hit.Name == "Low.md");
            Assert.DoesNotContain(WikiIndex.NotesWithMermaid([dir]), hit => hit.Name == "Mid.md");
            Assert.Contains(WikiIndex.NotesWithPlantuml([dir]), hit => hit.Name == "Mid.md");
            Assert.DoesNotContain(WikiIndex.NotesWithPlantuml([dir]), hit => hit.Name == "Low.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Number_Date_Config_Journey_Unwrap_And_Comments()
    {
        var fenced = "```csharp\nx\n```\n";
        var unwrapped = MarkdownEditing.UnwrapFence(fenced, 12, 0);
        Assert.NotNull(unwrapped);
        Assert.Contains("x", unwrapped!.Value.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("```", unwrapped.Value.Text, StringComparison.Ordinal);

        var comments = MarkdownEditing.StripHtmlComments("hi <!-- secret --> there\n```\n<!-- keep -->\n```\n");
        Assert.NotNull(comments);
        Assert.Contains("hi", comments!.Value.Text, StringComparison.Ordinal);
        Assert.Contains("there", comments.Value.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", comments.Value.Text, StringComparison.Ordinal);
        Assert.Contains("keep", comments.Value.Text, StringComparison.Ordinal);

        var publisher = FrontMatter.EnsurePublisher("# Note\n", "House");
        Assert.Contains("publisher: House", publisher, StringComparison.Ordinal);
        Assert.Equal(publisher, FrontMatter.EnsurePublisher(publisher, "Other"));
        var audience = FrontMatter.EnsureAudience("# Note\n", "dev");
        Assert.Contains("audience: dev", audience, StringComparison.Ordinal);
        var copyright = FrontMatter.EnsureCopyright("# Note\n");
        Assert.Contains("copyright: All rights reserved", copyright, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("journey\n    title Day\n    section Home\n      Wake: 5: Me\n      Coffee: 3: Me\n", out var journey));
        Assert.True(journey.Nodes.Count >= 2);
        Assert.Contains(journey.Nodes, node => node.Label.Contains("Wake", StringComparison.Ordinal));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-isnum-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Num.md"), "---\npriority: 3\ncreated: 2026-09-11\ncover: same.png\n---\n# N\n");
            File.WriteAllText(Path.Combine(dir, "Text.md"), "---\npriority: high\ncreated: soon\ncover: same.png\ncategory: wiki\n---\n# T\n");
            File.WriteAllText(Path.Combine(dir, "Cat.md"), "---\ncategory: ops\nversion: 2\nsubtitle: Lead\n---\n# C\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "priority IS NUMBER"), hit => hit.Name == "Num.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "priority IS NUMBER"), hit => hit.Name == "Text.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "created IS DATE"), hit => hit.Name == "Num.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "created IS DATE"), hit => hit.Name == "Text.md");
            Assert.Contains(WikiIndex.DuplicateCovers([dir]), hit => hit.Name == "Num.md");
            Assert.Contains(WikiIndex.DuplicateCovers([dir]), hit => hit.Name == "Text.md");
            Assert.Contains(WikiIndex.NotesWithoutCategory([dir]), hit => hit.Name == "Num.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutCategory([dir]), hit => hit.Name == "Cat.md");
            Assert.Contains(WikiIndex.NotesWithoutVersion([dir]), hit => hit.Name == "Num.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutVersion([dir]), hit => hit.Name == "Cat.md");
            Assert.Contains(WikiIndex.NotesWithoutSubtitle([dir]), hit => hit.Name == "Num.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutSubtitle([dir]), hit => hit.Name == "Cat.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void True_False_Quadrant_Shortlog_Ignore_And_Yaml()
    {
        var fenced = "```csharp\nx\n```\n";
        var language = MarkdownEditing.SetFenceLanguage(fenced, 12, "python");
        Assert.NotNull(language);
        Assert.Contains("```python", language!.Value.Text, StringComparison.Ordinal);

        var emptied = FrontMatter.StripEmptyKeys("---\ntitle: x\nstatus:\n---\n# H\n");
        Assert.Contains("title: x", emptied, StringComparison.Ordinal);
        Assert.DoesNotContain("status:", emptied, StringComparison.Ordinal);
        var summary = FrontMatter.EnsureSummary("# Note\n", "Brief");
        Assert.Contains("summary: Brief", summary, StringComparison.Ordinal);
        var location = FrontMatter.EnsureLocation("# Note\n", "Rome");
        Assert.Contains("location: Rome", location, StringComparison.Ordinal);
        var revision = FrontMatter.EnsureRevision("# Note\n");
        Assert.Contains("revision: 1", revision, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("quadrantChart\n    title Reach\n    Campaign A: [0.3, 0.6]\n    Campaign B: [0.45, 0.23]\n", out var quadrant));
        Assert.True(quadrant.Nodes.Count >= 2);
        Assert.Contains(quadrant.Nodes, node => node.Label.Contains("Campaign A", StringComparison.Ordinal));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-bool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "On.md"), "---\ndraft: true\ncategory: wiki\n---\n# On\n");
            File.WriteAllText(Path.Combine(dir, "Off.md"), "---\ndraft: false\ncategory: wiki\n---\n# Off\n");
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\npublisher: House\naudience: dev\ncopyright: MIT\n---\n# P\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "draft IS TRUE"), hit => hit.Name == "On.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "draft IS TRUE"), hit => hit.Name == "Off.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "draft IS FALSE"), hit => hit.Name == "Off.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "draft IS FALSE"), hit => hit.Name == "On.md");
            Assert.Contains(WikiIndex.DuplicateCategories([dir]), hit => hit.Name == "On.md");
            Assert.Contains(WikiIndex.DuplicateCategories([dir]), hit => hit.Name == "Off.md");
            Assert.Contains(WikiIndex.NotesWithoutPublisher([dir]), hit => hit.Name == "On.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutPublisher([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutAudience([dir]), hit => hit.Name == "On.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutAudience([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutCopyright([dir]), hit => hit.Name == "On.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutCopyright([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Url_Sankey_Cherry_Hooks_And_Yaml()
    {
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("cherry"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("hooks"));

        var subject = FrontMatter.EnsureSubject("# Note\n", "Inbox");
        Assert.Contains("subject: Inbox", subject, StringComparison.Ordinal);
        var organization = FrontMatter.EnsureOrganization("# Note\n", "Acme");
        Assert.Contains("organization: Acme", organization, StringComparison.Ordinal);
        var identifier = FrontMatter.EnsureIdentifier("# Note\n", "ISBN-1");
        Assert.Contains("identifier: ISBN-1", identifier, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("sankey-beta\nA,B,10\nB,C,5\n", out var sankey));
        Assert.True(sankey.Nodes.Count >= 3);
        Assert.Contains(sankey.Edges, edge => edge.Label == "10");
        Assert.Contains(sankey.Nodes, node => node.Label == "A");

        var mermaid = MarkdownEditing.InsertMermaid("", 0, 0, "sankey");
        Assert.Contains("sankey-beta", mermaid.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-url-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Link.md"), "---\nurl: https://example.com/x\nsummary: Brief\nlocation: Rome\nrevision: 2\n---\n# L\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\nurl: notaurl\nsubject: Inbox\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "Mail.md"), "---\nurl: mailto:a@b.c\n---\n# M\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "url IS URL"), hit => hit.Name == "Link.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "url IS URL"), hit => hit.Name == "Mail.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "url IS URL"), hit => hit.Name == "Plain.md");
            Assert.Contains(WikiIndex.NotesWithoutSummary([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutSummary([dir]), hit => hit.Name == "Link.md");
            Assert.Contains(WikiIndex.NotesWithoutLocation([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutLocation([dir]), hit => hit.Name == "Link.md");
            Assert.Contains(WikiIndex.NotesWithoutRevision([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutRevision([dir]), hit => hit.Name == "Link.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
