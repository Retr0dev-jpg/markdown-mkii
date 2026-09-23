using System.Text;

namespace MarkdownMkII.Core.Preview;

public static class LinkResolver
{
    public static (string Path, string? Fragment) SplitFragment(string url)
    {
        url ??= string.Empty;
        var hash = url.LastIndexOf('#');
        if (hash < 0)
        {
            return (url, null);
        }

        var fragment = url[(hash + 1)..];
        if (fragment.Contains('/') || fragment.Contains('\\'))
        {
            return (url, null);
        }

        return (url[..hash], fragment);
    }

    public static bool IsRemote(string url)
        => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
           url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public static bool IsSafeLocalImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsRemote(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    public static string ToMarkdownRelativePath(string? documentPath, string targetPath)
    {
        targetPath ??= string.Empty;
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return targetPath.Replace('\\', '/');
        }

        try
        {
            var root = Path.GetDirectoryName(documentPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return targetPath.Replace('\\', '/');
            }

            var relative = Path.GetRelativePath(root, targetPath).Replace('\\', '/');
            if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal) || relative.StartsWith("./", StringComparison.Ordinal))
            {
                return relative;
            }

            return "./" + relative;
        }
        catch (Exception)
        {
            return targetPath.Replace('\\', '/');
        }
    }

    public static string? TryResolveLocal(string url, string? documentPath)
    {
        var (path, _) = SplitFragment(url);
        return TryResolveLocalPath(path, documentPath);
    }

    private static string? TryResolveLocalPath(string url, string? documentPath)
    {
        if (string.IsNullOrWhiteSpace(url) || IsRemote(url) || url.StartsWith('#') || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(url, UriKind.Absolute, out var fileUri))
                url = fileUri.LocalPath;
            else
            {
                var query = url.IndexOf('?');
                url = Uri.UnescapeDataString(query < 0 ? url : url[..query]);
            }
            if (Path.IsPathRooted(url)) return File.Exists(url) ? Path.GetFullPath(url) : null;
            if (string.IsNullOrWhiteSpace(documentPath)) return File.Exists(url) ? Path.GetFullPath(url) : null;

            var root = Path.GetDirectoryName(documentPath);
            if (root is null)
            {
                return null;
            }

            var combined = Path.GetFullPath(Path.Combine(root, url.Replace('/', Path.DirectorySeparatorChar)));
            return File.Exists(combined) ? combined : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string EncodeHtml(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(ch switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => ch.ToString()
            });
        }

        return builder.ToString();
    }
}
