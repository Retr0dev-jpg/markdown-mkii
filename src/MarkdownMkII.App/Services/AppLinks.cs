namespace MarkdownMkII.Services;

/// <summary>Project identity shown in Information and used to open issues.</summary>
public static class AppLinks
{
    // Authors in Directory.Build.props: the SDK stamps it as the assembly company when Company is not set.
    public static readonly string Author = typeof(AppLinks).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyCompanyAttribute), false)
        .OfType<System.Reflection.AssemblyCompanyAttribute>().FirstOrDefault()?.Company ?? "";
    public const string License = "GNU General Public License v3.0";
    // RepositoryUrl in Directory.Build.props, embedded as assembly metadata.
    private static readonly string RepositoryUrl = typeof(AppLinks).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
        .OfType<System.Reflection.AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "RepositoryUrl")?.Value?.TrimEnd('/') ?? "";
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
