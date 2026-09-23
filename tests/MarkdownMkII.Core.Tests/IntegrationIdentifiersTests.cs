using MarkdownMkII.Core.Highlight;
using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public partial class FoldGitSnippetTests
{
    [Fact]
    public void Email_XyChart_Notes_Bisect_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeEmail("a@b.co"));
        Assert.True(MarkdownEditing.LooksLikeEmail("mailto:a@b.co"));
        Assert.False(MarkdownEditing.LooksLikeEmail("https://example.com"));
        Assert.False(MarkdownEditing.LooksLikeEmail("not-an-email"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("notes"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("bisect"));

        var doi = FrontMatter.EnsureDoi("# Note\n", "10.1000/xyz");
        Assert.Contains("doi: 10.1000/xyz", doi, StringComparison.Ordinal);
        var affiliation = FrontMatter.EnsureAffiliation("# Note\n", "Uni");
        Assert.Contains("affiliation: Uni", affiliation, StringComparison.Ordinal);
        var rights = FrontMatter.EnsureRights("# Note\n", "CC0");
        Assert.Contains("rights: CC0", rights, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("xychart-beta\n    title Sales\n    x-axis [jan, feb, mar]\n    bar [10, 20, 30]\n", out var chart));
        Assert.True(chart.Nodes.Count >= 3);
        Assert.Contains(chart.Nodes, node => node.Label == "jan");

        var dir = Path.Combine(Path.GetTempPath(), "mkii-email-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Mail.md"), "---\nemail: a@b.co\nsubject: Inbox\norganization: Acme\nidentifier: ISBN-1\n---\n# M\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\nemail: not-mail\n---\n# P\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "email IS EMAIL"), hit => hit.Name == "Mail.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "email IS EMAIL"), hit => hit.Name == "Plain.md");
            Assert.Contains(WikiIndex.NotesWithoutSubject([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutSubject([dir]), hit => hit.Name == "Mail.md");
            Assert.Contains(WikiIndex.NotesWithoutOrganization([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutOrganization([dir]), hit => hit.Name == "Mail.md");
            Assert.Contains(WikiIndex.NotesWithoutIdentifier([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutIdentifier([dir]), hit => hit.Name == "Mail.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Uuid_Block_Timing_Describe_ShowRef_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeUuid("00000000-0000-0000-0000-000000000001"));
        Assert.False(MarkdownEditing.LooksLikeUuid("not-a-uuid"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("describe"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("showref"));

        var issn = FrontMatter.EnsureIssn("# Note\n", "1234-5678");
        Assert.Contains("issn: 1234-5678", issn, StringComparison.Ordinal);
        var isbn = FrontMatter.EnsureIsbn("# Note\n", "978-0-00");
        Assert.Contains("isbn: 978-0-00", isbn, StringComparison.Ordinal);
        var abs = FrontMatter.EnsureAbstract("# Note\n", "Lead");
        Assert.Contains("abstract: Lead", abs, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("block-beta\ncolumns 3\nA B C\n", out var block));
        Assert.True(block.Nodes.Count >= 3);
        Assert.Contains(block.Nodes, node => node.Id == "A");

        Assert.True(PlantUmlParser.TryParse("@startuml\nrobust \"Web\" as WB\nconcise \"User\" as WU\n@0\nWU is Idle\n@enduml\n", out var timing));
        Assert.True(timing.Nodes.Count >= 2);
        Assert.Contains(timing.Nodes, node => node.Id == "WB");

        var dir = Path.Combine(Path.GetTempPath(), "mkii-uuid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Guid.md"), "---\nid: 00000000-0000-0000-0000-000000000001\ndoi: 10.1/x\naffiliation: Uni\nrights: CC0\n---\n# G\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\nid: not-uuid\n---\n# P\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "id IS UUID"), hit => hit.Name == "Guid.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "id IS UUID"), hit => hit.Name == "Plain.md");
            Assert.Contains(WikiIndex.NotesWithoutDoi([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutDoi([dir]), hit => hit.Name == "Guid.md");
            Assert.Contains(WikiIndex.NotesWithoutAffiliation([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutAffiliation([dir]), hit => hit.Name == "Guid.md");
            Assert.Contains(WikiIndex.NotesWithoutRights([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutRights([dir]), hit => hit.Name == "Guid.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Isbn_Requirement_Nwdiag_Sparse_LsFiles_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeIsbn("9783161484100"));
        Assert.True(MarkdownEditing.LooksLikeIsbn("0-306-40615-2"));
        Assert.False(MarkdownEditing.LooksLikeIsbn("not-isbn"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("sparse"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("lsfiles"));

        var pmid = FrontMatter.EnsurePmid("# Note\n", "12345678");
        Assert.Contains("pmid: 12345678", pmid, StringComparison.Ordinal);
        var orcid = FrontMatter.EnsureOrcid("# Note\n", "0000-0002-1825-0097");
        Assert.Contains("orcid: 0000-0002-1825-0097", orcid, StringComparison.Ordinal);
        var conference = FrontMatter.EnsureConference("# Note\n", "WWW");
        Assert.Contains("conference: WWW", conference, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("requirementDiagram\n    requirement Auth {\n    id: 1\n    text: login\n    }\n    element App {\n    type: simulation\n    }\n    App - satisfies -> Auth\n", out var req));
        Assert.Contains(req.Nodes, node => node.Id == "Auth");
        Assert.Contains(req.Nodes, node => node.Id == "App");

        Assert.True(PlantUmlParser.TryParse("@startuml\nnwdiag {\n  network dmz {\n    web01;\n    web02;\n  }\n}\n@enduml\n", out var nw));
        Assert.True(nw.Nodes.Count >= 2);
        Assert.Contains(nw.Nodes, node => node.Label.Contains("web01", StringComparison.Ordinal));

        var dir = Path.Combine(Path.GetTempPath(), "mkii-isbn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Book.md"), "---\nisbn: 9783161484100\nissn: 1234-5678\nabstract: Lead\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nisbn: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "isbn IS ISBN"), hit => hit.Name == "Book.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "isbn IS ISBN"), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "isbn IS ISBN"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutIssn([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutIssn([dir]), hit => hit.Name == "Book.md");
            Assert.Contains(WikiIndex.NotesWithoutIsbn([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutIsbn([dir]), hit => hit.Name == "Book.md");
            Assert.Contains(WikiIndex.NotesWithoutAbstract([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutAbstract([dir]), hit => hit.Name == "Book.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Issn_Orcid_C4_LsRemote_RevList_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeIssn("1234-5678"));
        Assert.True(MarkdownEditing.LooksLikeIssn("1234-567X"));
        Assert.False(MarkdownEditing.LooksLikeIssn("12-34"));
        Assert.True(MarkdownEditing.LooksLikeOrcid("0000-0002-1825-0097"));
        Assert.True(MarkdownEditing.LooksLikeOrcid("https://orcid.org/0000-0002-1825-0097"));
        Assert.False(MarkdownEditing.LooksLikeOrcid("not-orcid"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("lsremote"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("revlist"));

        var grant = FrontMatter.EnsureGrant("# Note\n", "ERC-1");
        Assert.Contains("grant: ERC-1", grant, StringComparison.Ordinal);
        var funder = FrontMatter.EnsureFunder("# Note\n", "EU");
        Assert.Contains("funder: EU", funder, StringComparison.Ordinal);
        var volume = FrontMatter.EnsureVolume("# Note\n", "12");
        Assert.Contains("volume: 12", volume, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("C4Context\n    title Context\n    Person(user, \"User\", \"A user\")\n    System(app, \"App\", \"The app\")\n    Rel(user, app, \"Uses\")\n", out var c4));
        Assert.Contains(c4.Nodes, node => node.Id == "user");
        Assert.Contains(c4.Nodes, node => node.Id == "app");
        Assert.Contains(c4.Edges, edge => edge.From == "user" && edge.To == "app");

        var dir = Path.Combine(Path.GetTempPath(), "mkii-orcid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nissn: 1234-5678\norcid: 0000-0002-1825-0097\npmid: 12345678\nconference: WWW\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nissn: 12\norcid: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "issn IS ISSN"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "issn IS ISSN"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "orcid IS ORCID"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "orcid IS ORCID"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutPmid([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutPmid([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutOrcid([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutOrcid([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutConference([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutConference([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Doi_Architecture_CountObjects_NameRev_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeDoi("10.1000/xyz123"));
        Assert.True(MarkdownEditing.LooksLikeDoi("https://doi.org/10.1000/xyz123"));
        Assert.True(MarkdownEditing.LooksLikeDoi("doi:10.1000/xyz123"));
        Assert.False(MarkdownEditing.LooksLikeDoi("not-doi"));
        Assert.False(MarkdownEditing.LooksLikeDoi("10.1/x"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("countobjects"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("namerev"));

        var issue = FrontMatter.EnsureIssue("# Note\n", "3");
        Assert.Contains("issue: 3", issue, StringComparison.Ordinal);
        var pages = FrontMatter.EnsurePages("# Note\n", "12-18");
        Assert.Contains("pages: 12-18", pages, StringComparison.Ordinal);
        var editor = FrontMatter.EnsureEditor("# Note\n", "Ada");
        Assert.Contains("editor: Ada", editor, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("architecture-beta\n    group api(cloud)[API]\n    service web(server)[Web]\n    service db(database)[Database]\n    web:R --> L:db\n", out var arch));
        Assert.Contains(arch.Nodes, node => node.Id == "api" && node.Label == "API");
        Assert.Contains(arch.Nodes, node => node.Id == "web");
        Assert.Contains(arch.Nodes, node => node.Id == "db");
        Assert.Contains(arch.Edges, edge => edge.From == "web" && edge.To == "db");

        var dir = Path.Combine(Path.GetTempPath(), "mkii-doi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\ndoi: 10.1000/xyz123\ngrant: ERC-1\nfunder: EU\nvolume: 12\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\ndoi: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "doi IS DOI"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "doi IS DOI"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutGrant([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutGrant([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutFunder([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutFunder([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutVolume([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutVolume([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Pmid_Packet_ForEachRef_LsTree_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikePmid("12345678"));
        Assert.True(MarkdownEditing.LooksLikePmid("pmid:1234567"));
        Assert.True(MarkdownEditing.LooksLikePmid("https://pubmed.ncbi.nlm.nih.gov/12345678/"));
        Assert.False(MarkdownEditing.LooksLikePmid("12"));
        Assert.False(MarkdownEditing.LooksLikePmid("not-pmid"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("foreachref"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("lstree"));

        var chapter = FrontMatter.EnsureChapter("# Note\n", "4");
        Assert.Contains("chapter: 4", chapter, StringComparison.Ordinal);
        var edition = FrontMatter.EnsureEdition("# Note\n", "2nd");
        Assert.Contains("edition: 2nd", edition, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("packet-beta\ntitle DHCP\n0-15: \"Source Port\"\n16-31: \"Destination Port\"\n", out var packet));
        Assert.Contains(packet.Nodes, node => node.Id == "b0" && node.Label == "Source Port");
        Assert.Contains(packet.Nodes, node => node.Id == "b16");
        Assert.Contains(packet.Edges, edge => edge.From == "b0" && edge.To == "b16");

        var dir = Path.Combine(Path.GetTempPath(), "mkii-pmid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\npmid: 12345678\nissue: 3\npages: 12-18\neditor: Ada\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\npmid: 12\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "pmid IS PMID"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "pmid IS PMID"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutIssue([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutIssue([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutPages([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutPages([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutEditor([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutEditor([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Arxiv_Kanban_RevParse_SymbolicRef_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeArxiv("2301.12345"));
        Assert.True(MarkdownEditing.LooksLikeArxiv("arxiv:hep-th/9901001"));
        Assert.True(MarkdownEditing.LooksLikeArxiv("https://arxiv.org/abs/2301.12345v2"));
        Assert.False(MarkdownEditing.LooksLikeArxiv("not-arxiv"));
        Assert.False(MarkdownEditing.LooksLikeArxiv("12.34"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("revparse"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("symbolicref"));

        var institution = FrontMatter.EnsureInstitution("# Note\n", "MIT");
        Assert.Contains("institution: MIT", institution, StringComparison.Ordinal);
        var school = FrontMatter.EnsureSchool("# Note\n", "CSAIL");
        Assert.Contains("school: CSAIL", school, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("kanban\n  Todo\n    [task1] Write docs\n  Done\n    [task2] Setup\n", out var board));
        Assert.Contains(board.Nodes, node => node.Id == "Todo");
        Assert.Contains(board.Nodes, node => node.Id == "task1" && node.Label == "Write docs");
        Assert.Contains(board.Edges, edge => edge.From == "Todo" && edge.To == "task1");
        Assert.Contains(board.Edges, edge => edge.From == "Done" && edge.To == "task2");

        var dir = Path.Combine(Path.GetTempPath(), "mkii-arxiv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\narxiv: 2301.12345\nchapter: 4\nedition: 2nd\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\narxiv: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "arxiv IS ARXIV"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "arxiv IS ARXIV"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutChapter([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutChapter([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutEdition([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutEdition([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Pmcid_Radar_Salt_Version_MergeBase_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikePmcid("PMC1234567"));
        Assert.True(MarkdownEditing.LooksLikePmcid("pmcid:PMC12345678"));
        Assert.True(MarkdownEditing.LooksLikePmcid("https://www.ncbi.nlm.nih.gov/pmc/articles/PMC1234567/"));
        Assert.False(MarkdownEditing.LooksLikePmcid("12"));
        Assert.False(MarkdownEditing.LooksLikePmcid("not-pmc"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("version"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("mergebase"));

        var department = FrontMatter.EnsureDepartment("# Note\n", "CS");
        Assert.Contains("department: CS", department, StringComparison.Ordinal);
        var advisor = FrontMatter.EnsureAdvisor("# Note\n", "Ada");
        Assert.Contains("advisor: Ada", advisor, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("radar-beta\n  title Skills\n  axis Communication, Leadership, Tech\n  curve team{0.8, 0.6, 0.9}\n", out var radar));
        Assert.Contains(radar.Nodes, node => node.Id == "Communication");
        Assert.Contains(radar.Nodes, node => node.Id == "team");
        Assert.Contains(radar.Edges, edge => edge.From == "Communication" && edge.To == "Leadership");
        Assert.Contains(radar.Edges, edge => edge.From == "team" && edge.To == "Communication");

        Assert.True(PlantUmlParser.TryParse("@startsalt\n{\n  Login\n  [Username]\n  [Password]\n  [OK]\n}\n@endsalt\n", out var salt));
        Assert.Contains(salt.Nodes, node => node.Label == "Username");
        Assert.Contains(salt.Nodes, node => node.Label == "OK");
        Assert.Contains(salt.Edges, edge => edge.From == "w0" && edge.To == "w1");

        var dir = Path.Combine(Path.GetTempPath(), "mkii-pmcid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\npmcid: PMC1234567\ninstitution: MIT\nschool: CSAIL\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\npmcid: 12\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "pmcid IS PMCID"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "pmcid IS PMCID"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutInstitution([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutInstitution([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutSchool([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutSchool([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Wikidata_Treemap_CheckIgnore_DiffName_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeWikidata("Q42"));
        Assert.True(MarkdownEditing.LooksLikeWikidata("https://www.wikidata.org/wiki/Q123"));
        Assert.True(MarkdownEditing.LooksLikeWikidata("wd:P31"));
        Assert.False(MarkdownEditing.LooksLikeWikidata("not-qid"));
        Assert.False(MarkdownEditing.LooksLikeWikidata("Q"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("checkignore"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("diffname"));

        var degree = FrontMatter.EnsureDegree("# Note\n", "PhD");
        Assert.Contains("degree: PhD", degree, StringComparison.Ordinal);
        var thesis = FrontMatter.EnsureThesis("# Note\n", "Notes");
        Assert.Contains("thesis: Notes", thesis, StringComparison.Ordinal);

        Assert.True(MermaidParser.TryParse("treemap-beta\n    title Files\n    \"Docs\"\n        \"README\": 40\n        \"Guide\": 20\n", out var tree));
        Assert.Contains(tree.Nodes, node => node.Label == "Docs");
        Assert.Contains(tree.Nodes, node => node.Label == "README");
        Assert.Contains(tree.Edges, edge => edge.From == "t0" && edge.To == "t1");
        Assert.Contains(tree.Edges, edge => edge.From == "t0" && edge.To == "t2");

        var dir = Path.Combine(Path.GetTempPath(), "mkii-wd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nwikidata: Q42\ndepartment: CS\nadvisor: Ada\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nwikidata: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "wikidata IS WIKIDATA"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "wikidata IS WIKIDATA"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutDepartment([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutDepartment([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutAdvisor([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutAdvisor([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Ror_Wbs_Untracked_Cached_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeRor("02mhbdp94"));
        Assert.True(MarkdownEditing.LooksLikeRor("https://ror.org/02mhbdp94"));
        Assert.True(MarkdownEditing.LooksLikeRor("ror:02mhbdp94"));
        Assert.False(MarkdownEditing.LooksLikeRor("not-a-ror"));
        Assert.False(MarkdownEditing.LooksLikeRor("12mhbdp94"));
        Assert.False(MarkdownEditing.LooksLikeRor("02mhbdp9"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("untracked"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("cached"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("diffcached"));

        var faculty = FrontMatter.EnsureFaculty("# Note\n", "Science");
        Assert.Contains("faculty: Science", faculty, StringComparison.Ordinal);
        var discipline = FrontMatter.EnsureDiscipline("# Note\n", "Physics");
        Assert.Contains("discipline: Physics", discipline, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startwbs\n* Project\n** Task\n*** Sub\n@endwbs\n", out var wbs));
        Assert.Contains(wbs.Nodes, node => node.Label == "Project");
        Assert.Contains(wbs.Nodes, node => node.Label == "Task");
        Assert.Contains(wbs.Nodes, node => node.Label == "Sub");
        Assert.Contains(wbs.Edges, edge => edge.From == "b0" && edge.To == "b1");
        Assert.Contains(wbs.Edges, edge => edge.From == "b1" && edge.To == "b2");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "wbs");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startwbs", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-ror-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nror: 02mhbdp94\ndegree: PhD\nthesis: Notes\nfaculty: Science\ndiscipline: Physics\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nror: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "ror IS ROR"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "ror IS ROR"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutDegree([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutDegree([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutThesis([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutThesis([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutFaculty([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutFaculty([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutDiscipline([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutDiscipline([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Grid_GanttPuml_DiffStat_StashShow_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeGrid("grid.47840.3f"));
        Assert.True(MarkdownEditing.LooksLikeGrid("https://www.grid.ac/institutes/grid.47840.3f"));
        Assert.True(MarkdownEditing.LooksLikeGrid("grid:grid.47840.3f"));
        Assert.False(MarkdownEditing.LooksLikeGrid("not-a-grid"));
        Assert.False(MarkdownEditing.LooksLikeGrid("grid.1"));
        Assert.False(MarkdownEditing.LooksLikeGrid("02mhbdp94"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("diffstat"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("stashshow"));

        var laboratory = FrontMatter.EnsureLaboratory("# Note\n", "Optics");
        Assert.Contains("laboratory: Optics", laboratory, StringComparison.Ordinal);
        var course = FrontMatter.EnsureCourse("# Note\n", "CS101");
        Assert.Contains("course: CS101", course, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startgantt\n[Design] lasts 3 days\n[Code] lasts 5 days\n@endgantt\n", out var gantt));
        Assert.Contains(gantt.Nodes, node => node.Label == "Design");
        Assert.Contains(gantt.Nodes, node => node.Label == "Code");
        Assert.Contains(gantt.Edges, edge => edge.From == "g0" && edge.To == "g1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "ganttpuml");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startgantt", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-grid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\ngrid: grid.47840.3f\nlaboratory: Optics\ncourse: CS101\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\ngrid: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "grid IS GRID"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "grid IS GRID"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutLaboratory([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutLaboratory([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutCourse([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutCourse([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Handle_JsonPuml_Clean_ShowName_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeHandle("20.1000/182"));
        Assert.True(MarkdownEditing.LooksLikeHandle("https://hdl.handle.net/20.1000/182"));
        Assert.True(MarkdownEditing.LooksLikeHandle("hdl:20.500.123/abc"));
        Assert.False(MarkdownEditing.LooksLikeHandle("10.1000/xyz"));
        Assert.False(MarkdownEditing.LooksLikeHandle("not-a-handle"));
        Assert.False(MarkdownEditing.LooksLikeHandle("20.1000"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("clean"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("showname"));

        var semester = FrontMatter.EnsureSemester("# Note\n", "2026-1");
        Assert.Contains("semester: 2026-1", semester, StringComparison.Ordinal);
        var module = FrontMatter.EnsureModule("# Note\n", "CS1");
        Assert.Contains("module: CS1", module, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startjson\n{\n  \"App\": {\n    \"Core\": \"1.0\"\n  }\n}\n@endjson\n", out var json));
        Assert.Contains(json.Nodes, node => node.Label == "App");
        Assert.Contains(json.Nodes, node => node.Label == "Core: 1.0");
        Assert.Contains(json.Edges, edge => edge.From == "j0" && edge.To == "j1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "jsonpuml");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startjson", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-hdl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nhandle: 20.1000/182\nsemester: 2026-1\nmodule: CS1\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nhandle: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "handle IS HANDLE"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "handle IS HANDLE"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutSemester([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutSemester([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutModule([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutModule([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Isni_YamlPuml_Modified_DiffCheck_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeIsni("0000 0001 2103 2683"));
        Assert.True(MarkdownEditing.LooksLikeIsni("https://isni.org/isni/0000000121032683"));
        Assert.True(MarkdownEditing.LooksLikeIsni("isni:0000000121032683"));
        Assert.False(MarkdownEditing.LooksLikeIsni("0000-0001-2345-6789"));
        Assert.False(MarkdownEditing.LooksLikeIsni("not-isni"));
        Assert.False(MarkdownEditing.LooksLikeIsni("0000 0001"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("modified"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("diffcheck"));

        var campus = FrontMatter.EnsureCampus("# Note\n", "North");
        Assert.Contains("campus: North", campus, StringComparison.Ordinal);
        var program = FrontMatter.EnsureProgram("# Note\n", "CS");
        Assert.Contains("program: CS", program, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startyaml\nApp:\n  Core: 1.0\n@endyaml\n", out var yaml));
        Assert.Contains(yaml.Nodes, node => node.Label == "App");
        Assert.Contains(yaml.Nodes, node => node.Label == "Core: 1.0");
        Assert.Contains(yaml.Edges, edge => edge.From == "y0" && edge.To == "y1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "yamlpuml");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startyaml", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-isni-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nisni: 0000 0001 2103 2683\ncampus: North\nprogram: CS\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nisni: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "isni IS ISNI"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "isni IS ISNI"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutCampus([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutCampus([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutProgram([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutProgram([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Viaf_MindmapPuml_Deleted_CachedStat_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeViaf("12345678"));
        Assert.True(MarkdownEditing.LooksLikeViaf("https://viaf.org/viaf/12345678"));
        Assert.True(MarkdownEditing.LooksLikeViaf("viaf:102333412"));
        Assert.False(MarkdownEditing.LooksLikeViaf("not-viaf"));
        Assert.False(MarkdownEditing.LooksLikeViaf("Q42"));
        Assert.False(MarkdownEditing.LooksLikeViaf("1"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("deleted"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("cachedstat"));

        var building = FrontMatter.EnsureBuilding("# Note\n", "A");
        Assert.Contains("building: A", building, StringComparison.Ordinal);
        var room = FrontMatter.EnsureRoom("# Note\n", "101");
        Assert.Contains("room: 101", room, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startmindmap\n* Root\n** Child\n*** Leaf\n@endmindmap\n", out var map));
        Assert.Contains(map.Nodes, node => node.Label == "Root");
        Assert.Contains(map.Nodes, node => node.Label == "Child");
        Assert.Contains(map.Nodes, node => node.Label == "Leaf");
        Assert.Contains(map.Edges, edge => edge.From == "m0" && edge.To == "m1");
        Assert.Contains(map.Edges, edge => edge.From == "m1" && edge.To == "m2");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "mindmappuml");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startmindmap", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-viaf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nviaf: 12345678\nbuilding: A\nroom: 101\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nviaf: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "viaf IS VIAF"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "viaf IS VIAF"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutBuilding([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutBuilding([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutRoom([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutRoom([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Lccn_Ditaa_Unmerged_CachedNs_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeLccn("n78-890351"));
        Assert.True(MarkdownEditing.LooksLikeLccn("https://lccn.loc.gov/2001024325"));
        Assert.True(MarkdownEditing.LooksLikeLccn("lccn:78-890351"));
        Assert.False(MarkdownEditing.LooksLikeLccn("not-lccn"));
        Assert.False(MarkdownEditing.LooksLikeLccn("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("unmerged"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("cachedns"));

        var wing = FrontMatter.EnsureWing("# Note\n", "East");
        Assert.Contains("wing: East", wing, StringComparison.Ordinal);
        var floor = FrontMatter.EnsureFloor("# Note\n", "3");
        Assert.Contains("floor: 3", floor, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startditaa\n+-----+\n| App |\n+-----+\n     |\n     v\n+----+\n| DB |\n+----+\n@endditaa\n", out var ditaa));
        Assert.Contains(ditaa.Nodes, node => node.Label == "App");
        Assert.Contains(ditaa.Nodes, node => node.Label == "DB");
        Assert.Contains(ditaa.Edges, edge => edge.From == "d0" && edge.To == "d1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "ditaa");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startditaa", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-lccn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nlccn: n78-890351\nwing: East\nfloor: 3\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nlccn: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "lccn IS LCCN"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "lccn IS LCCN"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutWing([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutWing([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutFloor([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutFloor([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Oclc_RegexPuml_Killed_DiffRaw_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeOclc("12345678"));
        Assert.True(MarkdownEditing.LooksLikeOclc("https://worldcat.org/oclc/123456789"));
        Assert.True(MarkdownEditing.LooksLikeOclc("oclc:0123456789"));
        Assert.False(MarkdownEditing.LooksLikeOclc("not-oclc"));
        Assert.False(MarkdownEditing.LooksLikeOclc("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("killed"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("diffraw"));

        var office = FrontMatter.EnsureOffice("# Note\n", "A12");
        Assert.Contains("office: A12", office, StringComparison.Ordinal);
        var suite = FrontMatter.EnsureSuite("# Note\n", "400");
        Assert.Contains("suite: 400", suite, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startregex\ntitle Flow\nalpha\nbeta\n@endregex\n", out var regex));
        Assert.Contains(regex.Nodes, node => node.Label == "alpha");
        Assert.Contains(regex.Nodes, node => node.Label == "beta");
        Assert.Contains(regex.Edges, edge => edge.From == "r0" && edge.To == "r1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "regex");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startregex", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-oclc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\noclc: 12345678\noffice: A12\nsuite: 400\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\noclc: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "oclc IS OCLC"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "oclc IS OCLC"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutOffice([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutOffice([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutSuite([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutSuite([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Gnd_Ebnf_SkipWorktree_AssumeUnchanged_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeGnd("118540238"));
        Assert.True(MarkdownEditing.LooksLikeGnd("https://d-nb.info/gnd/118540238"));
        Assert.True(MarkdownEditing.LooksLikeGnd("gnd:118540238"));
        Assert.False(MarkdownEditing.LooksLikeGnd("not-gnd"));
        Assert.False(MarkdownEditing.LooksLikeGnd("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("skipped"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("assumed"));

        var hall = FrontMatter.EnsureHall("# Note\n", "Aula");
        Assert.Contains("hall: Aula", hall, StringComparison.Ordinal);
        var annex = FrontMatter.EnsureAnnex("# Note\n", "B");
        Assert.Contains("annex: B", annex, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startebnf\ntitle syntax\nstart\nrule\n@endebnf\n", out var ebnf));
        Assert.Contains(ebnf.Nodes, node => node.Label == "start");
        Assert.Contains(ebnf.Nodes, node => node.Label == "rule");
        Assert.Contains(ebnf.Edges, edge => edge.From == "e0" && edge.To == "e1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "ebnf");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startebnf", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-gnd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\ngnd: 118540238\nhall: Aula\nannex: B\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\ngnd: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "gnd IS GND"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "gnd IS GND"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutHall([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutHall([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutAnnex([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutAnnex([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Bnf_Archimate_ShowStat_Oneline_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeBnf("cb11907966k"));
        Assert.True(MarkdownEditing.LooksLikeBnf("https://catalogue.bnf.fr/ark:/12148/cb11907966k"));
        Assert.True(MarkdownEditing.LooksLikeBnf("bnf:cb11907966k"));
        Assert.False(MarkdownEditing.LooksLikeBnf("not-bnf"));
        Assert.False(MarkdownEditing.LooksLikeBnf("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("showstat"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("oneline"));

        var tower = FrontMatter.EnsureTower("# Note\n", "North");
        Assert.Contains("tower: North", tower, StringComparison.Ordinal);
        var block = FrontMatter.EnsureBlock("# Note\n", "C");
        Assert.Contains("block: C", block, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startuml\narchimate #Technology \"App\"\narchimate #Application \"DB\"\n@enduml\n", out var archi));
        Assert.Contains(archi.Nodes, node => node.Label == "App");
        Assert.Contains(archi.Nodes, node => node.Label == "DB");
        Assert.Contains(archi.Edges, edge => edge.From == "a0" && edge.To == "a1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "archimate");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("archimate", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-bnf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nbnf: cb11907966k\ntower: North\nblock: C\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nbnf: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "bnf IS BNF"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "bnf IS BNF"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutTower([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutTower([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutBlock([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutBlock([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Sudoc_Chen_CachedNumstat_GraphLog_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeSudoc("026427032"));
        Assert.True(MarkdownEditing.LooksLikeSudoc("https://www.sudoc.fr/026427032"));
        Assert.True(MarkdownEditing.LooksLikeSudoc("sudoc:026427032"));
        Assert.False(MarkdownEditing.LooksLikeSudoc("not-sudoc"));
        Assert.False(MarkdownEditing.LooksLikeSudoc("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("cachednum"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("graphlog"));

        var pavilion = FrontMatter.EnsurePavilion("# Note\n", "East");
        Assert.Contains("pavilion: East", pavilion, StringComparison.Ordinal);
        var unit = FrontMatter.EnsureUnit("# Note\n", "Lab");
        Assert.Contains("unit: Lab", unit, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startchen\nentity User {\n  id\n}\nentity Order {\n  id\n}\n@endchen\n", out var chen));
        Assert.Contains(chen.Nodes, node => node.Label == "User");
        Assert.Contains(chen.Nodes, node => node.Label == "Order");
        Assert.Contains(chen.Edges, edge => edge.From == "c0" && edge.To == "c1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "chen");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startchen", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-sudoc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nsudoc: 026427032\npavilion: East\nunit: Lab\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nsudoc: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "sudoc IS SUDOC"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "sudoc IS SUDOC"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutPavilion([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutPavilion([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutUnit([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutUnit([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void OpenAlex_Ie_Decorate_CachedCheck_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeOpenAlex("W2741809807"));
        Assert.True(MarkdownEditing.LooksLikeOpenAlex("https://openalex.org/W2741809807"));
        Assert.True(MarkdownEditing.LooksLikeOpenAlex("openalex:A5023888391"));
        Assert.False(MarkdownEditing.LooksLikeOpenAlex("not-openalex"));
        Assert.False(MarkdownEditing.LooksLikeOpenAlex("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("decorate"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("cachedcheck"));

        var desk = FrontMatter.EnsureDesk("# Note\n", "12");
        Assert.Contains("desk: 12", desk, StringComparison.Ordinal);
        var zone = FrontMatter.EnsureZone("# Note\n", "Lab");
        Assert.Contains("zone: Lab", zone, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startie\nentity User {\n  id\n}\nentity Order {\n  id\n}\n@endie\n", out var ie));
        Assert.Contains(ie.Nodes, node => node.Label == "User");
        Assert.Contains(ie.Nodes, node => node.Label == "Order");
        Assert.Contains(ie.Edges, edge => edge.From == "i0" && edge.To == "i1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "ie");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startie", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-openalex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nopenalex: W2741809807\ndesk: 12\nzone: Lab\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nopenalex: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "openalex IS OPENALEX"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "openalex IS OPENALEX"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutDesk([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutDesk([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutZone([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutZone([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Olid_MathPuml_FirstParent_IgnoreSpace_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeOlid("OL27408W"));
        Assert.True(MarkdownEditing.LooksLikeOlid("https://openlibrary.org/works/OL27408W"));
        Assert.True(MarkdownEditing.LooksLikeOlid("olid:OL27408W"));
        Assert.False(MarkdownEditing.LooksLikeOlid("not-olid"));
        Assert.False(MarkdownEditing.LooksLikeOlid("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("firstparent"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("ignorespace"));

        var bay = FrontMatter.EnsureBay("# Note\n", "3");
        Assert.Contains("bay: 3", bay, StringComparison.Ordinal);
        var aisle = FrontMatter.EnsureAisle("# Note\n", "A");
        Assert.Contains("aisle: A", aisle, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startmath\nalpha\nbeta\n@endmath\n", out var math));
        Assert.Contains(math.Nodes, node => node.Label == "alpha");
        Assert.Contains(math.Nodes, node => node.Label == "beta");
        Assert.Contains(math.Edges, edge => edge.From == "x0" && edge.To == "x1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "mathpuml");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startmath", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-olid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nolid: OL27408W\nbay: 3\naisle: A\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nolid: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "olid IS OLID"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "olid IS OLID"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutBay([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutBay([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutAisle([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutAisle([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void S2cid_LatexPuml_Merges_NoMerges_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeS2cid("215416146"));
        Assert.True(MarkdownEditing.LooksLikeS2cid("https://www.semanticscholar.org/paper/215416146"));
        Assert.True(MarkdownEditing.LooksLikeS2cid("s2cid:215416146"));
        Assert.False(MarkdownEditing.LooksLikeS2cid("not-s2cid"));
        Assert.False(MarkdownEditing.LooksLikeS2cid("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("merges"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("nomerge"));

        var sector = FrontMatter.EnsureSector("# Note\n", "Nord");
        Assert.Contains("sector: Nord", sector, StringComparison.Ordinal);
        var corridor = FrontMatter.EnsureCorridor("# Note\n", "C");
        Assert.Contains("corridor: C", corridor, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startlatex\nE = mc^2\nF = ma\n@endlatex\n", out var latex));
        Assert.Contains(latex.Nodes, node => node.Label == "E = mc^2");
        Assert.Contains(latex.Nodes, node => node.Label == "F = ma");
        Assert.Contains(latex.Edges, edge => edge.From == "u0" && edge.To == "u1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "latexpuml");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startlatex", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-s2cid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\ns2cid: 215416146\nsector: Nord\ncorridor: C\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\ns2cid: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "s2cid IS S2CID"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "s2cid IS S2CID"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutSector([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutSector([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutCorridor([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutCorridor([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Ndl_ChronologyPuml_Reverse_All_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeNdl("0000001234"));
        Assert.True(MarkdownEditing.LooksLikeNdl("https://id.ndl.go.jp/bib/0000001234"));
        Assert.True(MarkdownEditing.LooksLikeNdl("ndl:0000001234"));
        Assert.False(MarkdownEditing.LooksLikeNdl("not-ndl"));
        Assert.False(MarkdownEditing.LooksLikeNdl("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("logrev"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("logall"));

        var lobby = FrontMatter.EnsureLobby("# Note\n", "Est");
        Assert.Contains("lobby: Est", lobby, StringComparison.Ordinal);
        var plaza = FrontMatter.EnsurePlaza("# Note\n", "1");
        Assert.Contains("plaza: 1", plaza, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startchronology\n2020 : Start\n2021 : End\n@endchronology\n", out var chronology));
        Assert.Contains(chronology.Nodes, node => node.Label == "2020 : Start");
        Assert.Contains(chronology.Nodes, node => node.Label == "2021 : End");
        Assert.Contains(chronology.Edges, edge => edge.From == "h0" && edge.To == "h1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "chronology");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startchronology", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-ndl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\nndl: 0000001234\nlobby: Est\nplaza: 1\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\nndl: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "ndl IS NDL"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "ndl IS NDL"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutLobby([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutLobby([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutPlaza([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutPlaza([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Cinii_SdlPuml_Topo_Date_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeCinii("110000000001"));
        Assert.True(MarkdownEditing.LooksLikeCinii("https://ci.nii.ac.jp/naid/110000000001"));
        Assert.True(MarkdownEditing.LooksLikeCinii("cinii:110000000001"));
        Assert.False(MarkdownEditing.LooksLikeCinii("not-cinii"));
        Assert.False(MarkdownEditing.LooksLikeCinii("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("logtopo"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("logdate"));

        var foyer = FrontMatter.EnsureFoyer("# Note\n", "Ovest");
        Assert.Contains("foyer: Ovest", foyer, StringComparison.Ordinal);
        var courtyard = FrontMatter.EnsureCourtyard("# Note\n", "2");
        Assert.Contains("courtyard: 2", courtyard, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startsdl\n:Ready;\n:Working;\n@endsdl\n", out var sdl));
        Assert.Contains(sdl.Nodes, node => node.Label == ":Ready;");
        Assert.Contains(sdl.Nodes, node => node.Label == ":Working;");
        Assert.Contains(sdl.Edges, edge => edge.From == "l0" && edge.To == "l1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "sdl");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startsdl", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-cinii-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\ncinii: 110000000001\nfoyer: Ovest\ncourtyard: 2\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\ncinii: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "cinii IS CINII"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "cinii IS CINII"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutFoyer([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutFoyer([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutCourtyard([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutCourtyard([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Cas_BoardPuml_FormatPatch_Whatchanged_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeCas("64-17-5"));
        Assert.True(MarkdownEditing.LooksLikeCas("https://commonchemistry.cas.org/detail?cas_rn=64-17-5"));
        Assert.True(MarkdownEditing.LooksLikeCas("cas:64-17-5"));
        Assert.False(MarkdownEditing.LooksLikeCas("not-cas"));
        Assert.False(MarkdownEditing.LooksLikeCas("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("fmtpatch"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("whatchanged"));

        var garden = FrontMatter.EnsureGarden("# Note\n", "Est");
        Assert.Contains("garden: Est", garden, StringComparison.Ordinal);
        var patio = FrontMatter.EnsurePatio("# Note\n", "4");
        Assert.Contains("patio: 4", patio, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startboard\n* Backlog\n** Task\n@endboard\n", out var board));
        Assert.Contains(board.Nodes, node => node.Label == "Backlog");
        Assert.Contains(board.Nodes, node => node.Label == "Task");
        Assert.Contains(board.Edges, edge => edge.From == "k0" && edge.To == "k1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "boardpuml");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startboard", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-cas-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\ncas: 64-17-5\ngarden: Est\npatio: 4\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\ncas: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "cas IS CAS"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "cas IS CAS"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutGarden([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutGarden([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutPatio([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutPatio([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Inchi_GitPuml_Children_NoDecorate_And_Yaml()
    {
        Assert.True(MarkdownEditing.LooksLikeInchi("InChI=1S/C2H6O/c1-2-3/h3H,2H2,1H3"));
        Assert.True(MarkdownEditing.LooksLikeInchi("inchi:InChI=1S/C2H6O/c1-2-3/h3H,2H2,1H3"));
        Assert.False(MarkdownEditing.LooksLikeInchi("not-inchi"));
        Assert.False(MarkdownEditing.LooksLikeInchi("Q42"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("logchildren"));
        Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo("nodecorate"));

        var pond = FrontMatter.EnsurePond("# Note\n", "Nord");
        Assert.Contains("pond: Nord", pond, StringComparison.Ordinal);
        var dock = FrontMatter.EnsureDock("# Note\n", "5");
        Assert.Contains("dock: 5", dock, StringComparison.Ordinal);

        Assert.True(PlantUmlParser.TryParse("@startgit\n*:Initial\n* Commit\n@endgit\n", out var gitUml));
        Assert.Contains(gitUml.Nodes, node => node.Label == "Initial");
        Assert.Contains(gitUml.Nodes, node => node.Label == "Commit");
        Assert.Contains(gitUml.Edges, edge => edge.From == "v0" && edge.To == "v1");

        var insert = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, "gitpuml");
        Assert.Contains("```plantuml", insert.Text, StringComparison.Ordinal);
        Assert.Contains("@startgit", insert.Text, StringComparison.Ordinal);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-inchi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Pub.md"), "---\ninchi: InChI=1S/C2H6O/c1-2-3/h3H,2H2,1H3\npond: Nord\ndock: 5\n---\n# B\n");
            File.WriteAllText(Path.Combine(dir, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(dir, "No.md"), "---\ninchi: nope\n---\n# N\n");
            Assert.Contains(WikiIndex.QueryFrontMatter([dir], "inchi IS INCHI"), hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(WikiIndex.QueryFrontMatter([dir], "inchi IS INCHI"), hit => hit.Name == "No.md");
            Assert.Contains(WikiIndex.NotesWithoutPond([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutPond([dir]), hit => hit.Name == "Pub.md");
            Assert.Contains(WikiIndex.NotesWithoutDock([dir]), hit => hit.Name == "Plain.md");
            Assert.DoesNotContain(WikiIndex.NotesWithoutDock([dir]), hit => hit.Name == "Pub.md");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
