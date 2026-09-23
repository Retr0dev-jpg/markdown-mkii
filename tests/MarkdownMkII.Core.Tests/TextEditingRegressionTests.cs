using MarkdownMkII.Core.Highlight;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class TextEditingRegressionTests
{
    [Fact]
    public void RenumberKeepsContinuationsDelimitersAndStartNumber()
    {
        var text = "1. first\n   continuation\n1. second\n\n1) alpha\n1) beta\n\n3. three\n3. four\n";
        Assert.Equal("1. first\n   continuation\n2. second\n\n1) alpha\n2) beta\n\n3. three\n4. four\n", MarkdownEditing.FormatDocument(text).Text);
        Assert.Equal("1. a\n\n   ```\n   code\n   ```\n2. b\n", MarkdownEditing.FormatDocument("1. a\n\n   ```\n   code\n   ```\n1. b\n").Text);
    }

    [Fact]
    public void FormatDocumentKeepsHardLineBreaks()
        => Assert.Equal("line one  \nline two\n", MarkdownEditing.FormatDocument("line one  \nline two\n").Text);

    [Fact]
    public void ReflowAndJoinLeaveCodeAndFrontMatterAlone()
    {
        const string fenced = "```\nalpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron\n```\n";
        Assert.Equal(fenced, MarkdownEditing.ReflowParagraph(fenced, 6, 0, 20).Text);
        const string yaml = "---\ntitle: A\ntags: [b]\n---\nBody";
        Assert.Equal(yaml, MarkdownEditing.JoinLines(yaml, 5, 0).Text);
    }

    [Fact]
    public void BlockCommandsDoNotRunInsideFences()
    {
        const string text = "```\n| a | b |\n| --- | --- |\n| 1 | 2 |\nitem\n```\n";
        Assert.Null(MarkdownEditing.AddTableRow(text, text.IndexOf("| 1", StringComparison.Ordinal), 0));
        var item = text.IndexOf("item", StringComparison.Ordinal);
        Assert.Equal(text, MarkdownEditing.ToggleList(text, item, 0, ordered: false).Text);
        Assert.Equal(text, MarkdownEditing.ToggleHeading(text, item, 0, 1).Text);
        Assert.Equal(text, MarkdownEditing.ToggleQuote(text, item, 0).Text);
    }

    [Fact]
    public void HeadingsToggleOffAndAcceptIndentation()
    {
        Assert.Equal("Title", MarkdownEditing.ToggleHeading("# Title", 2, 0, 1).Text);
        Assert.Equal("Title", MarkdownEditing.ToggleHeading(" # Title", 2, 0, 1).Text);
        Assert.Equal("# Title", MarkdownEditing.ToggleHeading(" ## Title", 3, 0, 1).Text);
        Assert.Equal(1, MarkdownEditing.HeadingLevel("   # Title"));
        Assert.Equal(0, MarkdownEditing.HeadingLevel("    # code"));
    }

    [Fact]
    public void ListToggleSkipsBlankLinesAndPlusTasksToggle()
    {
        Assert.Equal("- a\n\n- b", MarkdownEditing.ToggleList("a\n\nb", 0, 4, ordered: false).Text);
        Assert.Equal("item", MarkdownEditing.ToggleTaskItem("+ [ ] item", 0, 0).Text);
    }

    [Fact]
    public void TypographerSkipsCodeRulesAndFrontMatter()
    {
        var result = MarkdownEditing.ApplyTypographer("---\ntitle: a -- b\n---\nuse `a--b` here -- ok\n----\n").Text;
        Assert.Contains("title: a -- b", result);
        Assert.Contains("use `a--b` here – ok", result);
        Assert.EndsWith("----\n", result);
    }

    [Fact]
    public void CaseCommandsKeepUrlsAndCode()
    {
        const string text = "see https://example.com/Foo and `const` [Link](https://x.org/Path)";
        var upper = MarkdownEditing.ChangeCase(text, 0, text.Length, upper: true).Text;
        Assert.Equal("SEE https://example.com/Foo AND `const` [LINK](https://x.org/Path)", upper);
        Assert.Equal("See https://example.com/Foo and `const` [link](https://x.org/Path)", MarkdownEditing.SentenceCase(text, 0, text.Length).Text);
    }

    [Fact]
    public void DoubleClickSelectionWithTrailingSpaceProducesValidMarkdown()
    {
        var bold = MarkdownEditing.ToggleBold("Questa nota", 0, 7);
        Assert.Equal("**Questa** nota", bold.Text);
        Assert.Equal("Questa", bold.Text.Substring(bold.SelectionStart, bold.SelectionLength));
        Assert.Contains("<strong>Questa</strong> nota", DocumentParser.ToHtml(bold.Text));
        Assert.Equal("Questa nota", MarkdownEditing.ToggleBold(bold.Text, bold.SelectionStart, bold.SelectionLength).Text);
        Assert.Equal("  *nota* ", MarkdownEditing.ToggleItalic("  nota ", 0, 7).Text);
        Assert.Equal("  ", MarkdownEditing.ToggleBold("  ", 0, 2).Text);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void LineMapAndStatsAgreeOnNewlines(string newline)
    {
        var text = "one" + newline + "two" + newline;
        var map = new LineMap(text);
        Assert.Equal(3, map.LineCount);
        Assert.Equal(map.LineCount, WordStats.Compute(text).Lines);
        Assert.Equal("two", map.LineText(1));
        Assert.Equal(3 + newline.Length, map.OffsetOfLine(1));
        Assert.Equal((0, 0), map.LineSpan(-1));
        Assert.Equal((text.Length, 0), map.LineSpan(int.MaxValue));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void LineCommandsPreserveNewlinesAndSelectedContent(string newline)
    {
        var text = "hello world" + newline + "second";
        var quoted = MarkdownEditing.ToggleQuote(text, 6, 5);
        Assert.Equal("> hello world" + newline + "second", quoted.Text);
        Assert.Equal("world", quoted.Text.Substring(quoted.SelectionStart, quoted.SelectionLength));
        var caret = MarkdownEditing.Indent(text, 6, 0, 4);
        Assert.Equal(10, caret.SelectionStart);
        Assert.Equal(0, caret.SelectionLength);
        var startCaret = MarkdownEditing.ToggleHeading(text, 0, 0, 2);
        Assert.Equal(3, startCaret.SelectionStart);
        Assert.Equal(0, startCaret.SelectionLength);
    }

    [Fact]
    public void SelectionsAreClampedBeforeComputingAffectedLines()
    {
        var result = MarkdownEditing.ToggleHeading("one\ntwo", 4, int.MaxValue, 1);
        Assert.Equal("one\n# two", result.Text);
        Assert.InRange(result.SelectionStart + result.SelectionLength, 0, result.Text.Length);
        var quoted = MarkdownEditing.ToggleQuote("one\ntwo", 0, 4);
        Assert.Equal("> one\ntwo", quoted.Text);
        Assert.Equal(3, WordStats.Compute("one", 0, int.MaxValue).SelectedCharacters);
    }

    [Fact]
    public void BulkTaskChangesHandleAllListMarkersAndKeepCaret()
    {
        var text = "+ [ ] one\r\n1. [ ] two\r\n- [ ]three";
        var result = MarkdownEditing.SetTasksChecked(text, 0, text.Length, true);
        Assert.Equal("+ [x] one\r\n1. [x] two\r\n- [ ]three", result.Text);
        var caret = MarkdownEditing.SetTasksChecked(text, 3, 0, true);
        Assert.Equal(0, caret.SelectionLength);
    }

    [Fact]
    public void FenceClosingMustMatchMarkerLengthAndHaveNoInfoString()
    {
        var text = "````md\n~~~\n```\n````wrong\n`````\n";
        Assert.Equal(new FenceRange(0, 4), Assert.Single(FenceFold.Ranges(text)));
        Assert.Empty(FenceFold.Ranges("```\n~~~\n"));
        Assert.Empty(FenceFold.Ranges("    ```\ntext\n    ```"));
    }

    [Theory]
    [InlineData("---\n---\nbody", true, "body")]
    [InlineData("---\rtitle: demo\r...\rbody", true, "body")]
    [InlineData("---not yaml\nx: 1\n---\nbody", false, "---not yaml\nx: 1\n---\nbody")]
    [InlineData("---\nx: 1\n---still yaml\nbody", false, "---\nx: 1\n---still yaml\nbody")]
    public void FrontMatterRequiresCompleteDelimiterLines(string text, bool expected, string expectedBody)
    {
        Assert.Equal(expected, FrontMatter.TrySplit(text, out _, out var body));
        Assert.Equal(expectedBody, body);
    }

    [Fact]
    public void ExistingMetadataValuesAreNeverOverwrittenByEnsureField()
    {
        const string original = "---\nproject: existing\n---\nbody";
        Assert.Equal(original, FrontMatter.EnsureField(original, "project", "replacement"));
        var updated = FrontMatter.EnsureProject("body", " value ");
        Assert.True(FrontMatter.TrySplit(updated, out var fields, out var body));
        Assert.Equal("value", fields["project"]);
        Assert.Equal("body", body);
    }

    [Fact]
    public void HeadingsClassifyEachInlineOnlyOnce()
    {
        var spans = MarkdownSpanClassifier.Classify("# **bold** and `code`");
        Assert.Single(spans, span => span.Kind == MarkdownSpanKind.Strong);
        Assert.Single(spans, span => span.Kind == MarkdownSpanKind.Code);
    }

    [Fact]
    public void FormattingPreservesFencedContentIncludingUnclosedFences()
    {
        const string code = "````\n1. first  \n1. second\n\n\n\n```\n1. third  \n````\n";
        Assert.Equal(code, MarkdownEditing.FormatDocument(code).Text);
        Assert.Equal("```\ncode  ", MarkdownEditing.StripTrailingWhitespace("```\ncode  "));
        Assert.Equal("text\r\n```\r\ncode  \r\n```\r\n", MarkdownEditing.StripTrailingWhitespace("text  \r\n```\r\ncode  \r\n```\r\n"));
    }

    [Fact]
    public void EnterHandlesEmptyQuotesAndContinuesUncheckedTasks()
    {
        Assert.Equal(new EditResult(string.Empty, 0, 0), MarkdownEditing.ContinueBlockOnEnter(">", 1));
        Assert.Equal("+ [x] done\n+ [ ] ", MarkdownEditing.ContinueBlockOnEnter("+ [x] done", 10)!.Value.Text);
        Assert.Equal("2) done\n3) ", MarkdownEditing.ContinueBlockOnEnter("2) done", 7)!.Value.Text);
        Assert.Null(MarkdownEditing.ContinueBlockOnEnter("```\n- sample", 12));
        Assert.Null(MarkdownEditing.ContinueBlockOnEnter("9999999999999999999. text", 24));
    }

    [Fact]
    public void TablesShareCsvParsingAndRespectQuotedMultilineCells()
    {
        const string csv = "name,value\r\n\"line one\r\nline two\",\"a|b\"\r\n";
        Assert.True(MarkdownTables.TryFromDelimited(csv, out var table));
        Assert.Equal("| name | value |\n| --- | --- |\n| line one line two | a\\|b |\n", table);
        Assert.Equal(table, MarkdownEditing.InsertTableFromDelimited(string.Empty, 0, 0, csv).Text);
        const string tabInsideQuotedCell = "name,value\n\"a\tb\",42";
        Assert.True(MarkdownTables.TryFromDelimited(tabInsideQuotedCell, out var commaTable));
        Assert.Contains("| a\tb | 42 |", commaTable);
    }

    [Fact]
    public void TableFormattingAndNavigationKeepEscapedPipesInsideTheCell()
    {
        const string table = "| a\\|b | c |\r\n| --- | --- |\r\n| d | e |";
        var result = MarkdownEditing.FormatTable(table, 3, 0);
        Assert.NotNull(result);
        Assert.Contains("a\\|b", result.Value.Text);
        Assert.Contains("\r\n", result.Value.Text);
        var next = MarkdownEditing.AdvanceTableCell(table, 3, 0, false);
        Assert.NotNull(next);
        Assert.Equal("c", next.Value.Text.Substring(next.Value.SelectionStart, next.Value.SelectionLength));
        Assert.Null(MarkdownEditing.FormatTable("```\n" + table + "\n```", 7, 0));
    }

    [Fact]
    public void FencedExamplesDoNotBecomeTagsTasksDescriptionsOrInlineWarnings()
    {
        const string text = "````\n# Wrong\n~~~\n- [ ] sample #hidden `\n````\n# Right\nActual paragraph #visible";
        Assert.Equal(["visible"], MarkdownTags.Extract(text));
        Assert.Empty(MarkdownKanban.Extract(text));
        Assert.DoesNotContain(MarkdownDiagnostics.Analyze(text), issue => issue.Code is "code" or "fence");
        var described = FrontMatter.EnsureDescription(text);
        Assert.Contains("description: \"Actual paragraph #visible\"", described);
        Assert.True(FrontMatter.TrySplit(described, out var fields, out _));
        Assert.Equal("Actual paragraph #visible", fields["description"]);
        Assert.Contains("title: Right", FrontMatter.EnsureTitle(text));
    }

    [Fact]
    public void LargeDiffRetainsRepeatedLineDeletions()
    {
        var left = string.Join('\n', Enumerable.Repeat("repeat", 805));
        var right = string.Join('\n', Enumerable.Repeat("repeat", 804));
        var diff = TextDiff.Lines(left, right);
        Assert.Single(diff, line => line.Kind == '-');
        Assert.Equal(804, diff.Count(line => line.Kind == ' '));
    }

    [Fact]
    public void HtmlConversionPreservesCodeAndUnicodeClipboardOffsets()
    {
        Assert.Equal("```\nList<T>\n```", HtmlToMarkdown.Convert("<pre><code>List&lt;T&gt;</code></pre>"));
        Assert.Equal("`<tag>`", HtmlToMarkdown.Convert("<code>&lt;tag&gt;</code>"));
        const string prefix = "StartFragment:0000000000\r\nEndFragment:0000000000\r\n<html>è😀";
        const string fragment = "<p>città</p>";
        var start = System.Text.Encoding.UTF8.GetByteCount(prefix);
        var end = start + System.Text.Encoding.UTF8.GetByteCount(fragment);
        var source = prefix.Replace("StartFragment:0000000000", $"StartFragment:{start:D10}")
            .Replace("EndFragment:0000000000", $"EndFragment:{end:D10}") + fragment + "</html>";
        Assert.Equal(fragment, HtmlToMarkdown.ExtractFragment(source));
    }

    [Fact]
    public void MathCommandsRespectBoundariesAndPieLayoutRejectsNonfiniteValues()
    {
        Assert.Equal(@"\sinh x + sin x + α{R}", MathText.ToDisplay(@"\sinh x + \sin x + \alpha{R}"));
        var diagram = new MermaidDiagram(false, [], [], MermaidKind.Pie,
            Slices: [new("a", double.MaxValue), new("b", double.MaxValue), new("bad", double.NaN)]);
        var slices = GraphLayoutEngine.ForPie(diagram);
        Assert.Equal(2, slices.Count);
        Assert.All(slices, slice => Assert.Equal(0.5, slice.Fraction));
    }

    [Fact]
    public void LanguageInfoAcceptsWhitespaceAndPowerShellKeywordsIgnoreCase()
    {
        Assert.Equal(LanguageId.CSharp, LanguageCatalog.FromInfo("csharp\tlinenums"));
        var tokens = CodeTokenizer.Tokenize("IF #comment\rRETURN", LanguageId.PowerShell);
        Assert.Contains(tokens, token => token.Start == 0 && token.Kind == MarkdownMkII.Core.Preview.CodeTokenKind.Keyword);
        Assert.Contains(tokens, token => token.Start == 12 && token.Kind == MarkdownMkII.Core.Preview.CodeTokenKind.Keyword);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void MovingAndDuplicatingLastLineKeepItsCaretAndNewlines(string newline)
    {
        var text = "aaa" + newline + "bbb" + newline + "ccc";
        var start = 3 + newline.Length + 2;
        var moved = MarkdownEditing.MoveLines(text, start, 0, 1);
        Assert.Equal("aaa" + newline + "ccc" + newline + "bbb", moved.Text);
        Assert.Equal(moved.Text.Length - 1, moved.SelectionStart);
        Assert.Equal(0, moved.SelectionLength);
        var duplicate = MarkdownEditing.DuplicateLine("aaa" + newline + "bbb", 3 + newline.Length, 0);
        Assert.Equal("aaa" + newline + "bbb" + newline + "bbb", duplicate.Text);
        Assert.Equal(duplicate.Text.Length - 3, duplicate.SelectionStart);
    }

    [Fact]
    public void SetextOutlineStartsAtTheTitleInsteadOfItsUnderline()
    {
        const string text = "Introduction\n\nFirst line\nsecond line\n===========\n\nbody";
        var heading = Assert.Single(TocExtractor.Extract(text));
        Assert.Equal(2, heading.SourceLine);
        Assert.Equal(6, heading.SourceEndLine);
    }

    [Fact]
    public void FenceInsertionKeepsNestedMarkersAndSelectsBodyAfterAddedLineBreaks()
    {
        var math = MarkdownEditing.InsertMathBlock("before x after", 7, 1);
        Assert.Equal("x", math.Text.Substring(math.SelectionStart, math.SelectionLength));
        const string body = "```\ncode\n```";
        var code = MarkdownEditing.InsertCodeFence(body, 0, body.Length, "md");
        Assert.StartsWith("````md\n", code.Text);
        Assert.Equal(new FenceRange(0, 4), Assert.Single(FenceFold.Ranges(code.Text)));
        var language = MarkdownEditing.SetFenceLanguage(code.Text, 0, "text");
        Assert.NotNull(language);
        Assert.StartsWith("````text\n", language.Value.Text);
        Assert.Equal(new FenceRange(0, 4), Assert.Single(FenceFold.Ranges(language.Value.Text)));
    }
}

public class FindReplaceRegressionTests
{
    [Theory]
    [InlineData("xcat", "cat", true)]
    [InlineData("xcat", "(?<!x)cat", false)]
    public void RangeBoundariesDoNotInventMatches(string text, string query, bool wholeWord)
    {
        var options = new FindReplaceOptions { RangeStart = 1, RangeLength = 3, WholeWord = wholeWord, UseRegex = !wholeWord };
        Assert.Empty(FindReplace.FindAll(text, query, options));
        Assert.Equal((text, 0), FindReplace.ReplaceAll(text, query, "dog", options));
    }

    [Fact]
    public void RangeReplacementRetainsLookaroundContextAndCaptureGroups()
    {
        const string text = "prefix:42 suffix";
        var options = new FindReplaceOptions { UseRegex = true, RangeStart = 7, RangeLength = 2 };
        const string query = @"(?<=prefix:)(\d+)(?= suffix)";
        Assert.Single(FindReplace.FindAll(text, query, options));
        Assert.Equal(("prefix:[42] suffix", 1), FindReplace.ReplaceAll(text, query, "[$1]", options));
        var one = FindReplace.ReplaceMatch(text, query, "[$1]", new FindMatch(7, 2), options);
        Assert.Equal(new EditResult("prefix:[42] suffix", 7, 4), one);
    }

    [Fact]
    public void SingleReplacementRequiresTheExactSelectedMatch()
    {
        Assert.Null(FindReplace.ReplaceMatch("cat dog", "cat", "bird", new FindMatch(4, 3)));
        Assert.Null(FindReplace.ReplaceMatch("cat dog", "cat", "bird", new FindMatch(0, 2)));
        Assert.Equal(new EditResult("bird dog", 0, 4), FindReplace.ReplaceMatch("cat dog", "cat", "bird", new FindMatch(0, 3)));
    }

    [Fact]
    public void ReplacementRejectsInvalidSpansAndRangeArithmeticCannotOverflow()
    {
        Assert.Equal("text", FindReplace.ReplaceAt("text", new FindMatch(0, -1), "x"));
        Assert.Equal("text", FindReplace.ReplaceAt("text", new FindMatch(int.MaxValue, 1), "x"));
        var options = new FindReplaceOptions { RangeStart = 1, RangeLength = int.MaxValue };
        Assert.Single(FindReplace.FindAll("x cat", "cat", options));
        Assert.Equal(("x dog", 1), FindReplace.ReplaceAll("x cat", "cat", "dog", options));
    }
}
