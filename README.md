# Markdown MkII

A fast, local Markdown note manager for Windows. Written in C# with WinUI 3.

Notes live in a single archive on your PC: no cloud, no account. You can protect the most private notes with a password.

## Download

Download `MarkdownMkII-win-x64.zip` from the latest [release](../../releases/latest), extract the folder, and start `MarkdownMkII.App.exe`. Nothing else needs to be installed.

Requirements: Windows 10 (1809) or Windows 11, 64-bit.

## What it does

- **Writing:** a Markdown editor with autosave, formatting, tables, find and replace, an outline, and a minimap.
- **Preview:** a native preview with wikilinks, links between notes, attachments, embedded notes, and diagrams.
- **Organization:** categories, tags, favorites, colors, archive, and trash, with full-text search.
- **History and backup:** up to 30 versions per note, plus full backups and archive restore.
- **Protected notes:** per-note AES-256 encryption, a recovery code, and optional unlock with Windows Hello ([details](docs/protected-notes.md)).
- **Import and export:** Markdown files and folders in; Markdown, HTML, print, and share out.
- **Interface:** Italian and English, light or dark theme, customizable shortcuts.

Imported files are copied into the archive. The originals are never modified.

## Where the data lives

- **Zip build:** `%LOCALAPPDATA%\Markdown MkII`.
- **MSIX build:** the package data folder.

In both cases the folder contains `notes.db` (the notes) and `settings.json` (preferences). To move everything to another PC, use **Settings → Data → Backup**.

## Build from source

You need Windows and the [.NET SDK 10](https://dotnet.microsoft.com/download).

```powershell
git clone <repository-url>
cd markdown-mkii
dotnet build src/MarkdownMkII.App -p:Platform=x64
```

To develop, open `MarkdownMkII.slnx` in Visual Studio, select `MarkdownMkII.App` in the Debug x64 configuration, and press **F5**.

Tests:

```powershell
dotnet test tests/MarkdownMkII.Core.Tests
dotnet test tests/MarkdownMkII.Storage.Tests
dotnet test tests/MarkdownMkII.App.Logic.Tests
```

Documentation:

- [Development and verification](docs/development.md): build, tests, separate trial data, conventions.
- [Architecture](docs/architecture.md): how the archive, editor, and interface are organized.
- [Protected notes](docs/protected-notes.md): encryption, integrity, and security limits.
- [Known limits](docs/limits.md): what the app does not do.
- [AGENTS.md](AGENTS.md): a short guide for AI agents.

| Folder | Contents |
| --- | --- |
| `src/MarkdownMkII.Core` | Markdown parsing and transforms, preview |
| `src/MarkdownMkII.Storage` | SQLite archive, search, encryption, import and export |
| `src/MarkdownMkII.App` | WinUI 3 interface |
| `tests/` | Automated tests |

## Contributing

Issues and pull requests are welcome. Before opening a pull request, run the tests and follow the conventions in [docs/development.md](docs/development.md#conventions).

## License

Distributed under the [GNU General Public License v3.0](LICENSE). You may use, study, modify, and redistribute the program, including modified versions, provided that distributed versions stay under the same license and the source code remains available.
