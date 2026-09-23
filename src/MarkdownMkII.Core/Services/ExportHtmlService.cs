using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MarkdownMkII.Core.Highlight;
using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Models;
using MarkdownMkII.Core.Preview;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Services;

public static partial class ExportHtmlService
{
    [GeneratedRegex(@"<pre><code(?:\s+class=""language-([^""]+)"")?>([\s\S]*?)</code></pre>", RegexOptions.IgnoreCase)]
    private static partial Regex CodeBlockRegex();

    public static string Export(string markdown, string? title = null, ExportSettings? settings = null, string? documentPath = null)
    {
        settings ??= new ExportSettings();
        markdown ??= string.Empty;
        var parsed = DocumentParser.Parse(markdown, documentPath);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = parsed.FrontMatter.TryGetValue("title", out var frontTitle)
                ? frontTitle
                : parsed.Outline.FirstOrDefault()?.Title ?? "Documento";
        }

        var body = RewriteMarkdownHrefs(HighlightCodeBlocks(Markdig.Markdown.ToHtml(markdown, PipelineFactory.Export)));
        var css = settings.EmbedCss ? EmbeddedCss(settings.CssTheme) : string.Empty;
        var front = settings.IncludeFrontMatter && parsed.FrontMatter.Count > 0
            ? RenderFrontMatter(parsed.FrontMatter)
            : string.Empty;
        var toc = settings.IncludeToc ? RenderToc(parsed.Outline) : string.Empty;

        return "<!DOCTYPE html>\n<html lang=\"it\">\n<head>\n<meta charset=\"utf-8\" />\n"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n"
            + "<title>" + LinkResolver.EncodeHtml(title) + "</title>\n<style>\n"
            + css + "\n</style>\n</head>\n<body>\n<article class=\"markdown-body\">\n"
            + front + "\n" + toc + body + "\n</article>\n</body>\n</html>\n";
    }

    public static int ExportWithAssets(
        string markdown,
        string htmlPath,
        string? documentPath = null,
        IEnumerable<string>? folders = null,
        ExportSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(htmlPath))
        {
            return 0;
        }

        var html = Export(markdown, Path.GetFileNameWithoutExtension(htmlPath), settings, documentPath);
        var destDir = Path.GetDirectoryName(Path.GetFullPath(htmlPath))!;
        Directory.CreateDirectory(destDir);
        var assets = Path.Combine(destDir, "assets");
        var folderList = folders?.ToArray() ?? [];
        var copied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        html = AssetTagRegex().Replace(html, tag => AssetAttributeRegex().Replace(tag.Value, attribute =>
        {
            var source = ResolveAsset(attribute.Groups[2].Value, documentPath, folderList);
            if (source is null)
            {
                return attribute.Value;
            }

            if (!copied.TryGetValue(source, out var relative))
            {
                try
                {
                    Directory.CreateDirectory(assets);
                    var dest = VaultAttachments.UniquePath(assets, Path.GetFileNameWithoutExtension(source), Path.GetExtension(source));
                    File.Copy(source, dest, overwrite: false);
                    relative = "assets/" + Uri.EscapeDataString(Path.GetFileName(dest));
                    copied.Add(source, relative);
                }
                catch (IOException) { return attribute.Value; }
                catch (UnauthorizedAccessException) { return attribute.Value; }
            }

            var original = WebUtility.HtmlDecode(attribute.Groups[2].Value);
            var (_, fragment) = LinkResolver.SplitFragment(original);
            var target = relative + (fragment is null ? string.Empty : "#" + fragment);
            return attribute.Groups[1].Value + "=\"" + LinkResolver.EncodeHtml(target) + "\"";
        }));

        File.WriteAllText(htmlPath, html);
        return copied.Count;
    }

    public static IReadOnlyList<string> CollectLocalAssets(
        string markdown,
        string? documentPath,
        IEnumerable<string>? folders = null)
    {
        var html = Markdig.Markdown.ToHtml(markdown ?? string.Empty, PipelineFactory.Export);
        var folderList = folders?.ToArray() ?? [];
        var hits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match tag in AssetTagRegex().Matches(html))
        {
            foreach (Match attribute in AssetAttributeRegex().Matches(tag.Value))
            {
                if (ResolveAsset(attribute.Groups[2].Value, documentPath, folderList) is { } source)
                {
                    hits.Add(source);
                }
            }
        }

        return hits.ToArray();
    }

    private static string? ResolveAsset(string encodedUrl, string? documentPath, IEnumerable<string> folders)
    {
        var url = WebUtility.HtmlDecode(encodedUrl);
        if (LinkResolver.IsRemote(url))
        {
            return null;
        }

        var (target, _) = LinkResolver.SplitFragment(url);
        try
        {
            target = Uri.UnescapeDataString(target);
        }
        catch (UriFormatException)
        {
            return null;
        }

        return WikiIndex.IsMediaTarget(target)
            ? LinkResolver.TryResolveLocal(target, documentPath) ?? WikiIndex.ResolveMedia(target, documentPath, folders)
            : null;
    }

    [GeneratedRegex(@"<(?:img|audio|video|source|a)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex AssetTagRegex();

    [GeneratedRegex(@"\b(src|href)=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex AssetAttributeRegex();

    internal static string HighlightCodeBlocks(string html)
    {
        return CodeBlockRegex().Replace(html, match =>
        {
            var language = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
            var decoded = WebUtility.HtmlDecode(match.Groups[2].Value);
            var tokens = CodeTokenizer.Tokenize(decoded, language);
            var builder = new StringBuilder(decoded.Length + 64);
            builder.Append("<pre><code");
            if (!string.IsNullOrWhiteSpace(language))
            {
                builder.Append(" class=\"language-").Append(LinkResolver.EncodeHtml(language)).Append('"');
            }

            builder.Append('>');
            var cursor = 0;
            foreach (var token in tokens)
            {
                if (token.Start > cursor)
                {
                    builder.Append(LinkResolver.EncodeHtml(decoded[cursor..token.Start]));
                }

                var slice = decoded.Substring(token.Start, token.Length);
                if (token.Kind == CodeTokenKind.Plain)
                {
                    builder.Append(LinkResolver.EncodeHtml(slice));
                }
                else
                {
                    builder.Append("<span class=\"tok-")
                        .Append(token.Kind.ToString().ToLowerInvariant())
                        .Append("\">")
                        .Append(LinkResolver.EncodeHtml(slice))
                        .Append("</span>");
                }

                cursor = token.Start + token.Length;
            }

            if (cursor < decoded.Length)
            {
                builder.Append(LinkResolver.EncodeHtml(decoded[cursor..]));
            }

            builder.Append("</code></pre>");
            return builder.ToString();
        });
    }

    private static string RenderFrontMatter(IReadOnlyDictionary<string, string> fields)
    {
        var rows = string.Join(
            Environment.NewLine,
            fields.Select(pair => $"<tr><th>{LinkResolver.EncodeHtml(pair.Key)}</th><td>{LinkResolver.EncodeHtml(pair.Value)}</td></tr>"));
        return $"<details class=\"front-matter\"><summary>Front matter</summary><table>{rows}</table></details>";
    }

    private static string RenderToc(IReadOnlyList<OutlineNode> outline)
    {
        if (outline is null || outline.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("<nav class=\"toc\"><ol>");
        foreach (var item in outline)
        {
            builder.Append("<li class=\"toc-").Append(item.Level).Append("\"><a href=\"#")
                .Append(LinkResolver.EncodeHtml(item.Id)).Append("\">")
                .Append(LinkResolver.EncodeHtml(item.Title)).Append("</a></li>");
        }

        builder.Append("</ol></nav>\n");
        return builder.ToString();
    }

    public static string EmbeddedCss(string? theme)
    {
        var dark = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase);
        var bg = dark ? "#1c1c1c" : "#f3f3f3";
        var fg = dark ? "#f3f3f3" : "#1c1c1c";
        var accent = "#0067c0";
        var codeBg = dark ? "#2d2d2d" : "#f6f8fa";
        var quote = dark ? "#3b3b3b" : "#d1d1d1";
        return ":root { color-scheme: " + (dark ? "dark" : "light") + "; }\n"
            + "html, body { margin: 0; padding: 0; background: " + bg + "; color: " + fg + ";\n"
            + "  font-family: \"Segoe UI Variable\", \"Segoe UI\", sans-serif; line-height: 1.55; }\n"
            + ".markdown-body { max-width: 860px; margin: 32px auto; padding: 0 24px 64px; }\n"
            + "h1, h2, h3, h4 { font-weight: 600; line-height: 1.25; }\n"
            + "h1 { font-size: 2rem; } h2 { font-size: 1.5rem; } h3 { font-size: 1.25rem; }\n"
            + "a { color: " + accent + "; }\n"
            + "code, pre { font-family: \"Cascadia Code\", Consolas, monospace; background: " + codeBg + "; }\n"
            + "pre { padding: 12px 16px; overflow: auto; border-radius: 8px; }\n"
            + "code { padding: 0.1em 0.35em; border-radius: 4px; }\n"
            + "pre code { padding: 0; background: transparent; }\n"
            + ".tok-keyword { color: " + (dark ? "#79c0ff" : "#0550ae") + "; }\n"
            + ".tok-string { color: " + (dark ? "#a5d6ff" : "#0a3069") + "; }\n"
            + ".tok-comment { color: " + (dark ? "#8b949e" : "#6e7781") + "; font-style: italic; }\n"
            + ".tok-number { color: " + (dark ? "#79c0ff" : "#0550ae") + "; }\n"
            + ".tok-typename { color: " + (dark ? "#ffa657" : "#953800") + "; }\n"
            + ".tok-punctuation { opacity: 0.85; }\n"
            + "blockquote { margin: 0; padding: 0.2em 1em; border-left: 4px solid " + quote + "; color: inherit; opacity: 0.9; }\n"
            + "table { border-collapse: collapse; width: 100%; }\n"
            + "th, td { border: 1px solid " + quote + "; padding: 6px 10px; }\n"
            + "img { max-width: 100%; }\n"
            + "hr { border: none; border-top: 1px solid " + quote + "; }\n"
            + ".front-matter { margin-bottom: 1.5rem; }\n"
            + ".toc { margin: 0 0 1.5rem; padding: 0.75rem 1.25rem; border: 1px solid " + quote + "; border-radius: 8px; }\n"
            + ".toc ol { margin: 0; padding-left: 1.25rem; }\n"
            + ".toc a { text-decoration: none; }\n"
            + ".toc-1 { font-weight: 600; }\n"
            + ".toc-2 { margin-left: 0.35rem; }\n"
            + ".toc-3 { margin-left: 0.7rem; }\n";
    }

    public static int ExportVault(IEnumerable<string> folders, string destination, ExportSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return 0;
        }

        Directory.CreateDirectory(destination);
        settings ??= new ExportSettings();
        var folderList = folders?.ToList() ?? [];
        var count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folderList)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var nestedDestination = LibraryCatalog.IsWithin(destination, folder);
            foreach (var path in WorkspaceScanner.EnumerateMarkdownFiles(folder))
            {
                if (!seen.Add(path) || (nestedDestination && LibraryCatalog.IsWithin(path, destination)))
                {
                    continue;
                }

                try
                {
                    var markdown = File.ReadAllText(path);
                    string relative;
                    try
                    {
                        relative = Path.GetRelativePath(folder, path);
                    }
                    catch (Exception)
                    {
                        relative = Path.GetFileName(path);
                    }

                    if (relative.StartsWith("..", StringComparison.Ordinal))
                    {
                        relative = Path.GetFileName(path);
                    }

                    var dest = Path.Combine(destination, Path.ChangeExtension(relative, ".html"));
                    var unique = dest;
                    var n = 2;
                    var stem = Path.GetFileNameWithoutExtension(dest);
                    var destDir = Path.GetDirectoryName(dest) ?? destination;
                    Directory.CreateDirectory(destDir);
                    while (File.Exists(unique))
                    {
                        unique = Path.Combine(destDir, stem + "-" + n + ".html");
                        n++;
                    }

                    ExportWithAssets(markdown, unique, path, folderList, settings);
                    count++;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        WriteIndex(destination, settings);
        return count;
    }

    public static int ExportMarkdownVault(IEnumerable<string> folders, string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return 0;
        }

        Directory.CreateDirectory(destination);
        var destRoot = Path.GetFullPath(destination);
        var count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders ?? [])
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                continue;
            }

            var sourceRoot = Path.GetFullPath(folder);
            var nestedDestination = LibraryCatalog.IsWithin(destRoot, sourceRoot);
            foreach (var path in WorkspaceScanner.EnumerateMarkdownFiles(folder))
            {
                if (!seen.Add(path) || (nestedDestination && LibraryCatalog.IsWithin(path, destRoot)))
                {
                    continue;
                }

                try
                {
                    string relative;
                    try
                    {
                        relative = Path.GetRelativePath(sourceRoot, path);
                    }
                    catch (Exception)
                    {
                        relative = Path.GetFileName(path);
                    }

                    if (relative.StartsWith("..", StringComparison.Ordinal))
                    {
                        relative = Path.GetFileName(path);
                    }

                    var dest = Path.Combine(destRoot, relative);
                    var destDir = Path.GetDirectoryName(dest) ?? destRoot;
                    Directory.CreateDirectory(destDir);
                    if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var unique = dest;
                    var n = 2;
                    var stem = Path.GetFileNameWithoutExtension(dest);
                    var ext = Path.GetExtension(dest);
                    while (File.Exists(unique))
                    {
                        unique = Path.Combine(destDir, stem + "-" + n + ext);
                        n++;
                    }

                    File.Copy(path, unique, overwrite: false);
                    count++;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        return count;
    }

    private static void WriteIndex(string destination, ExportSettings settings)
    {
        try
        {
            var indexPath = Path.Combine(destination, "index.html");
            const string markerComment = "<!-- Markdown MkII generated vault index -->";
            if (File.Exists(indexPath) && !File.ReadAllText(indexPath).StartsWith(markerComment, StringComparison.Ordinal))
            {
                return;
            }

            var files = Directory.GetFiles(destination, "*.html", SearchOption.AllDirectories)
                .Where(path => !string.Equals(Path.GetFileName(path), "index.html", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetRelativePath(destination, path), StringComparer.OrdinalIgnoreCase)
                .ToList();
            var items = string.Join(
                "\n",
                files.Select(path =>
                {
                    var href = Path.GetRelativePath(destination, path).Replace('\\', '/');
                    var name = Path.ChangeExtension(href, null).Replace('\\', '/');
                    return "<li><a href=\"" + LinkResolver.EncodeHtml(href) + "\">" + LinkResolver.EncodeHtml(name) + "</a></li>";
                }));
            var shell = Export("# Vault", "Vault", settings);
            var marker = "</article>";
            var index = shell.LastIndexOf(marker, StringComparison.Ordinal);
            var html = index < 0
                ? shell
                : shell[..index] + "<ul>\n" + items + "\n</ul>\n" + shell[index..];
            File.WriteAllText(indexPath, markerComment + "\n" + html);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static string RewriteMarkdownHrefs(string html)
    {
        return MarkdownHrefRegex().Replace(html, match =>
        {
            var target = WebUtility.HtmlDecode(match.Groups[1].Value).Replace('\\', '/');
            if (LinkResolver.IsRemote(target) || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            var fragment = WebUtility.HtmlDecode(match.Groups[2].Value);
            return "href=\"" + LinkResolver.EncodeHtml(target + ".html" + fragment) + "\"";
        });
    }

    [GeneratedRegex(@"href=""([^""]+)\.md(#[^""]*)?""", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownHrefRegex();
}
