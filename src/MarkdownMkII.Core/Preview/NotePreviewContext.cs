namespace MarkdownMkII.Core.Preview;

public sealed record EmbeddedNote(string Id,string Title,string Markdown);

/// <summary>Only the notes embedded by the current document, never the entire archive.</summary>
public sealed record NotePreviewContext(string CurrentId,IReadOnlyDictionary<string,EmbeddedNote> Embedded)
{
    public EmbeddedNote? Resolve(string target)
    {
        target=Uri.UnescapeDataString(target.Split('#')[0]);
        if(target.StartsWith("note:",StringComparison.OrdinalIgnoreCase))target=target[5..].TrimStart('/');
        if(target.EndsWith(".md",StringComparison.OrdinalIgnoreCase))target=target[..^3];
        return Embedded.GetValueOrDefault(target);
    }
}
