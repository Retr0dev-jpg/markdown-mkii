using MarkdownMkII.Storage;
using MarkdownMkII.Services.Localization;

namespace MarkdownMkII.Services;

/// <summary>
/// The recovery file holds only the archive id, the creation time, the code and instructions: it is
/// stored apart from the archive, so it must not reveal note titles or identifiers.
/// </summary>
internal static class RecoveryDocument
{
    public static Task WriteAsync(string path, PreparedCredentials credentials, CancellationToken token = default)
        => File.WriteAllTextAsync(path, Format(credentials), new System.Text.UTF8Encoding(true), token);

    public static string FileName(PreparedCredentials credentials)
        => $"MarkdownMkII-Recupero-{credentials.ArchiveId[..8]}-{credentials.Created:yyyyMMdd-HHmmss}.txt";

    public static string Format(PreparedCredentials credentials)
    {
        var lines = new List<string>
        {
            "Markdown MkII — " + Strings.T("SecurityRecoveryCode"), "", Strings.T("RecoveryArchive") + ": " + credentials.ArchiveId,
            Strings.T("RecoveryCreated") + ": " + credentials.Created.ToString("O"),
            "", Strings.T("SecurityRecoveryCode"), credentials.RecoveryCode, "", Strings.T("RecoveryInstructions")
        };
        return string.Join("\r\n", lines) + "\r\n";
    }
}
