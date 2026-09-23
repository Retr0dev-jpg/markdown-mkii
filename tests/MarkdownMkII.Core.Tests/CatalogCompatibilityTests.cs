using MarkdownMkII.Core.Highlight;
using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public partial class CatalogCompatibilityTests
{
    public static IEnumerable<object[]> CaseNames => Cases.Select(item => new object[] { item.Name });

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void PreservesIdentifierMetadataAndDiagramCompatibility(string name)
    {
        var item = Cases.Single(value => value.Name == name);
        foreach (var input in item.ValidIdentifiers)
        {
            Assert.True(item.Validate(input), input);
        }

        foreach (var input in item.InvalidIdentifiers)
        {
            Assert.False(item.Validate(input), input);
        }

        foreach (var alias in item.LanguageAliases)
        {
            Assert.Equal(LanguageId.Diff, LanguageCatalog.FromInfo(alias));
        }

        foreach (var field in new[] { item.FirstField, item.SecondField })
        {
            Assert.Contains(field.Expected, field.Ensure("# Note\n", field.Value), StringComparison.Ordinal);
        }

        Assert.True(PlantUmlParser.TryParse(item.DiagramSource, out var diagram));
        foreach (var label in item.NodeLabels)
        {
            Assert.Contains(diagram.Nodes, node => node.Label == label);
        }
        Assert.Contains(diagram.Edges, edge => edge.From == item.EdgeFrom && edge.To == item.EdgeTo);
        var inserted = MarkdownEditing.InsertMermaid(string.Empty, 0, 0, item.InsertionKind);
        Assert.Contains("```plantuml", inserted.Text, StringComparison.Ordinal);
        Assert.Contains(item.InsertionDirective, inserted.Text, StringComparison.Ordinal);

        var directory = Path.Combine(Path.GetTempPath(), "mkii-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Pub.md"), item.ValidDocument);
            File.WriteAllText(Path.Combine(directory, "Plain.md"), "---\ntitle: P\n---\n# P\n");
            File.WriteAllText(Path.Combine(directory, "No.md"), item.InvalidDocument);
            var matches = WikiIndex.QueryFrontMatter([directory], item.Query);
            Assert.Contains(matches, hit => hit.Name == "Pub.md");
            Assert.DoesNotContain(matches, hit => hit.Name == "No.md");
            foreach (var field in new[] { item.FirstField, item.SecondField })
            {
                var missing = field.NotesWithout([directory]);
                Assert.Contains(missing, hit => hit.Name == "Plain.md");
                Assert.DoesNotContain(missing, hit => hit.Name == "Pub.md");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed record FieldCase(
        Func<string, string, string> Ensure,
        Func<IEnumerable<string>, IReadOnlyList<WikiBacklink>> NotesWithout,
        string Value,
        string Expected);


    private sealed record CompatibilityCase(
        string Name,
        Func<string, bool> Validate,
        string[] ValidIdentifiers,
        string[] InvalidIdentifiers,
        string[] LanguageAliases,
        FieldCase FirstField,
        FieldCase SecondField,
        string DiagramSource,
        string[] NodeLabels,
        string EdgeFrom,
        string EdgeTo,
        string InsertionKind,
        string InsertionDirective,
        string ValidDocument,
        string InvalidDocument,
        string Query);
}
