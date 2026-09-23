using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Text;

public static class HtmlToMarkdown
{
    public static bool LooksLikeMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("**", StringComparison.Ordinal) ||
               text.Contains("](", StringComparison.Ordinal) ||
               text.Contains("```", StringComparison.Ordinal) ||
               text.Contains("[[", StringComparison.Ordinal) ||
               text.Contains("![", StringComparison.Ordinal) ||
               Regex.IsMatch(text, @"(?m)^#{1,6} ", RegexOptions.CultureInvariant);
    }

    public static string Convert(string html)
    {
        html ??= string.Empty;
        html = ExtractFragment(html);
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var codeFragments = new List<string>();
        string ProtectCode(string fragment)
        {
            codeFragments.Add(fragment);
            return "\uE000" + (codeFragments.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "\uE001";
        }

        html = Regex.Replace(html, @"<script[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(html, @"<style[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(
            html,
            @"<pre[^>]*>([\s\S]*?)</pre>",
            match => ProtectCode("\n```\n" + StripTags(match.Groups[1].Value) + "\n```\n"),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(
            html,
            @"<h([1-6])[^>]*>([\s\S]*?)</h\1>",
            match => "\n" + new string('#', int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)) + " " + Inner(match.Groups[2].Value) + "\n",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(
            html,
            @"<a[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*>([\s\S]*?)</a>",
            match => "[" + Inner(match.Groups[2].Value) + "](" + Decode(match.Groups[1].Value) + ")",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(html, @"<(strong|b)[^>]*>([\s\S]*?)</\1>", match => "**" + Inner(match.Groups[2].Value) + "**", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(html, @"<(em|i)[^>]*>([\s\S]*?)</\1>", match => "*" + Inner(match.Groups[2].Value) + "*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(html, @"<code[^>]*>([\s\S]*?)</code>", match => ProtectCode("`" + Inner(match.Groups[1].Value) + "`"), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(html, @"<li[^>]*>", "- ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(html, @"</li>", "\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(
            html,
            @"<table[\s\S]*?</table>",
            match => "\n" + MarkdownTables.FromHtml(match.Value) + "\n",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(html, @"</p>", "\n\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(html, @"<p[^>]*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        html = Regex.Replace(html, @"<[^>]+>", string.Empty);
        html = Decode(html).Replace("\r\n", "\n").Replace('\r', '\n');
        html = Regex.Replace(html, @"\n{3,}", "\n\n");
        for (var i = 0; i < codeFragments.Count; i++)
        {
            html = html.Replace("\uE000" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\uE001", Decode(codeFragments[i]), StringComparison.Ordinal);
        }

        return html.Trim();
    }

    public static string ExtractFragment(string html)
    {
        html ??= string.Empty;
        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";
        var markerStart = html.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        var markerEnd = html.IndexOf(endMarker, StringComparison.OrdinalIgnoreCase);
        if (markerStart >= 0 && markerEnd >= markerStart + startMarker.Length)
        {
            return html[(markerStart + startMarker.Length)..markerEnd];
        }

        // CF_HTML offsets count UTF-8 bytes, not UTF-16 string positions.
        var bytes = Encoding.UTF8.GetBytes(html);
        var start = ReadOffset(html, "StartFragment:");
        var end = ReadOffset(html, "EndFragment:");
        if (start >= 0 && end > start && end <= bytes.Length)
        {
            return Encoding.UTF8.GetString(bytes, start, end - start);
        }

        var htmlStart = ReadOffset(html, "StartHTML:");
        var htmlEnd = ReadOffset(html, "EndHTML:");
        if (htmlStart >= 0 && htmlEnd > htmlStart && htmlEnd <= bytes.Length)
        {
            return Encoding.UTF8.GetString(bytes, htmlStart, htmlEnd - htmlStart);
        }

        var firstTag = html.IndexOf('<');
        return firstTag >= 0 ? html[firstTag..] : html;
    }

    private static int ReadOffset(string html, string marker)
    {
        var index = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return -1;
        }

        index += marker.Length;
        var end = index;
        while (end < html.Length && char.IsDigit(html[end]))
        {
            end++;
        }

        return end > index && int.TryParse(html.AsSpan(index, end - index), out var offset) ? offset : -1;
    }

    private static string Inner(string value) => StripTags(value).Trim();

    private static string StripTags(string value) => Regex.Replace(value, @"<[^>]+>", string.Empty);

    private static string Decode(string value) => WebUtility.HtmlDecode(value) ?? string.Empty;
}
