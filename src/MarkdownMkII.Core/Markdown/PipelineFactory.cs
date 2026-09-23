using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.EmphasisExtras;

namespace MarkdownMkII.Core.Markdown;

/// <summary>
/// Unica pipeline Markdig dell'app. Le estensioni sono elencate una per una
/// (niente <c>UseAdvancedExtensions</c>) così il comportamento resta deterministico.
/// </summary>
public static class PipelineFactory
{
    public static MarkdownPipeline Preview { get; } = Create(disableHtml: true);

    public static MarkdownPipeline Export { get; } = Create(disableHtml: false);

    public static MarkdownPipeline Create(bool disableHtml)
    {
        var builder = new MarkdownPipelineBuilder()
            .UsePreciseSourceLocation()
            .UsePipeTables()
            .UseGridTables()
            .UseTaskLists()
            .UseAutoLinks()
            .UseFootnotes()
            .UseDefinitionLists()
            .UseListExtras()
            .UseEmphasisExtras(EmphasisExtraOptions.Default)
            .UseGenericAttributes()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .UseYamlFrontMatter()
            .UseMediaLinks()
            .UseFigures()
            .UseCitations()
            .UseCustomContainers()
            .UseMathematics()
            .UseEmojiAndSmiley()
            .Use<WikiLinkExtension>();

        if (disableHtml)
        {
            builder.DisableHtml();
        }

        return builder.Build();
    }
}
