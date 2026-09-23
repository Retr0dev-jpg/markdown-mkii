using MarkdownMkII.Services.Localization;

namespace MarkdownMkII.ViewModels;
public static class CommandPresentation
{
    public static string Category(string id) => Strings.T("CommandCategory" + CategoryId(id));
    private static string CategoryId(string id)
    {
        if (id.StartsWith("ensure", StringComparison.OrdinalIgnoreCase) || id.Contains("Yaml", StringComparison.OrdinalIgnoreCase) || id == "frontMatter")
            return "Metadata";
        if (id.StartsWith("new") || id is "protectNote" or "unprotectNote" or "lockNotes" or "unlockNotes" or "open" or "quickOpen" or "searchNotes" or "close" or "save" or "rename" or "duplicateNote" or "star" or "history")
            return "Notes";
        if (id.StartsWith("export") || id is "print" or "share" or "copyMd" or "copyHtml")
            return "Export";
        if (id.StartsWith("view") || id.StartsWith("zoom") || id is "replace" or "refreshPreview" or "inspector" or "find" or "goToLine" or "goToHeading" or "goToDefinition" or "focus" or "immersive" or "slides" or "kanban" or "wrap" or "nextIssue" or "prevIssue" || id.Contains("fold", StringComparison.OrdinalIgnoreCase))
            return "View";
        if (id.StartsWith("insert") || id is "link" or "image" or "wiki" or "footnote" or "table" or "fence" or "rule" or "math" or "mathBlock" or "mermaid" or "callout")
            return "Insert";
        return "Format";
    }

    public static string Keywords(string id) => id switch
    {
        "bold" => "bold grassetto forte",
        "italic" => "corsivo italic inclinato",
        "strike" => "barrato cancellato strikethrough",
        "new" => "crea scrivi nuova nota",
        "open" => "importa file markdown",
        "quickOpen" => "cerca apri nota libreria",
        "searchNotes" => "cerca filtra note barra laterale search",
        "star" => "preferito stella importante favorite",
        "wiki" => "collega nota wikilink",
        "find" => "cerca trova testo search",
        "exportMd" => "esporta salva copia markdown file",
        "close" => "chiudi editor torna libreria",
        _ => ""
    };
}
