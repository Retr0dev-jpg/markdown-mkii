namespace MarkdownMkII.Services;

/// <summary>Project identity shown in Information and used to open issues.</summary>
public static class AppLinks
{
    public const string Author = "Marco Simone";
    public const string License = "GNU General Public License v3.0";
    private const string RepositoryUrl = "https://github.com/Retr0dev-jpg/markdown-mkii";
    public static readonly Uri Repository = new(RepositoryUrl);
    public static readonly Uri LicenseText = new(RepositoryUrl + "/blob/main/LICENSE");

    /// <summary>A new issue whose body is prefilled; GitHub rejects very long URLs, so the body is trimmed.</summary>
    public static Uri NewIssue(string body)
    {
        const int maxBody = 6000;
        if (body.Length > maxBody) body = body[..maxBody] + "\n…";
        return new Uri(RepositoryUrl + "/issues/new?body=" + Uri.EscapeDataString(body));
    }
}
