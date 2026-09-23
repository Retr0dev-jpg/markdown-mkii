using MarkdownMkII.Core.Markdown;

namespace MarkdownMkII.Core.Services;

public static class VaultAttachments
{
    public const string FolderName = "assets";

    public const string ArchiveFolderName = "archive";

    public static string? Import(string sourcePath, string? documentPath, IEnumerable<string>? folders = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        var vault = ResolveVaultRoot(documentPath, folders);
        if (string.IsNullOrEmpty(vault) || !Directory.Exists(vault))
        {
            return sourcePath;
        }

        try
        {
            var assets = Path.Combine(vault, FolderName);
            Directory.CreateDirectory(assets);
            var ext = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".png";
            }

            var stem = Path.GetFileNameWithoutExtension(sourcePath);
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                stem = stem.Replace(invalid, '-');
            }

            if (string.IsNullOrWhiteSpace(stem))
            {
                stem = "image";
            }

            var destination = UniquePath(assets, stem, ext);
            File.Copy(sourcePath, destination, overwrite: false);
            return destination;
        }
        catch (IOException)
        {
            return sourcePath;
        }
        catch (UnauthorizedAccessException)
        {
            return sourcePath;
        }
    }

    public static string UniquePath(string directory, string stem, string extension)
    {
        if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        var candidate = Path.Combine(directory, stem + extension);
        var n = 2;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            candidate = Path.Combine(directory, stem + "-" + n + extension);
            n++;
        }

        return candidate;
    }

    public static bool IsArchived(string path, IEnumerable<string>? folders = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var vault = ResolveVaultRoot(path, folders);
        if (string.IsNullOrEmpty(vault))
        {
            return false;
        }

        try
        {
            var archive = Path.GetFullPath(Path.Combine(vault, ArchiveFolderName));
            var full = Path.GetFullPath(path);
            var prefix = archive.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string? Archive(string path, IEnumerable<string>? folders = null, IEnumerable<string>? skipPaths = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        if (IsArchived(path, folders))
        {
            return Path.GetFullPath(path);
        }

        var vault = ResolveVaultRoot(path, folders);
        if (string.IsNullOrEmpty(vault) || !Directory.Exists(vault))
        {
            return null;
        }

        try
        {
            var archive = Path.Combine(vault, ArchiveFolderName);
            Directory.CreateDirectory(archive);
            return WikiIndex.Relocate(path, archive, folders, skipPaths);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string? Unarchive(string path, IEnumerable<string>? folders = null, IEnumerable<string>? skipPaths = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        if (!IsArchived(path, folders))
        {
            return Path.GetFullPath(path);
        }

        var vault = ResolveVaultRoot(path, folders);
        if (string.IsNullOrEmpty(vault) || !Directory.Exists(vault))
        {
            return null;
        }

        try
        {
            return WikiIndex.Relocate(path, vault, folders, skipPaths);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string? ResolveVaultRoot(string? documentPath, IEnumerable<string>? folders)
    {
        var folderList = folders?.Where(folder => !string.IsNullOrWhiteSpace(folder)).ToList() ?? [];
        if (!string.IsNullOrWhiteSpace(documentPath))
        {
            try
            {
                var full = Path.GetFullPath(documentPath);
                foreach (var folder in folderList)
                {
                    var root = Path.GetFullPath(folder);
                    if (full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetDirectoryName(full), root, StringComparison.OrdinalIgnoreCase))
                    {
                        return root;
                    }
                }

                return Path.GetDirectoryName(full);
            }
            catch (Exception)
            {
            }
        }

        return folderList.FirstOrDefault(Directory.Exists);
    }

    public static string? RelativeToVault(string path, IEnumerable<string>? folders = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(path);
            var root = ResolveVaultRoot(full, folders);
            if (string.IsNullOrEmpty(root))
            {
                return Path.GetFileName(full).Replace('\\', '/');
            }

            var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
            return relative.StartsWith("..", StringComparison.Ordinal) ? Path.GetFileName(full) : relative;
        }
        catch (Exception)
        {
            return Path.GetFileName(path);
        }
    }
}
