# Development and verification

How to build, test, and try the app without touching your own notes.

## Requirements

- Windows 10 1809 or later, x64.
- [.NET SDK 10](https://dotnet.microsoft.com/download).
- For debugging: Visual Studio with the WinUI app workload (Windows App SDK).

## Build and run

In Visual Studio: open `MarkdownMkII.slnx`, select `MarkdownMkII.App`, the **Debug x64** configuration, the MSIX profile, and press **F5**. This is the recommended way to try the app: MSIX, resources, and Windows Hello behave as they do in real use.

From the command line:

```powershell
# Unpackaged app (produces MarkdownMkII.App.exe under bin/x64/...)
dotnet build src/MarkdownMkII.App -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true

# MSIX app
dotnet build src/MarkdownMkII.App -p:Platform=x64 -p:WindowsPackageType=MSIX -p:WindowsAppSDKSelfContained=false
```

## Tests

| Project | What it checks |
| --- | --- |
| `tests/MarkdownMkII.Core.Tests` | Parsing, Markdown commands, preview, the save queue, it/en resource parity |
| `tests/MarkdownMkII.Storage.Tests` | SQLite, search, history, attachments, import/export, encryption, integrity, 100,000 notes |
| `tests/MarkdownMkII.App.Logic.Tests` | Editor state, settings, recovery files, the integrity anchor |
| `tests/MarkdownMkII.App.RuntimeChecks` | Resource checks to run inside the app started from the debugger ([instructions](../tests/MarkdownMkII.App.RuntimeChecks/README.md)) |

The first three projects also run on Linux; the Windows-registry anchor test exits there without performing checks.

```powershell
# All tests, warnings treated as errors (TRX results in .artifacts/test-results)
./scripts/verify.ps1

# Tests plus a Release build of the app, both MSIX and unpackaged
./scripts/verify.ps1 -IncludeApp -Configuration Release
```

The script requires PowerShell 7 (`pwsh`). With `-AppBuild MSIX` or `-AppBuild Unpackaged` it builds only one variant.

## Continuous integration

- `.github/workflows/core-tests.yml` runs the tests on Linux and `verify.ps1 -IncludeApp` on Windows, on every push and pull request.
- `.github/workflows/release.yml` publishes the **Latest** release on every push to `main`, with the zip of the self-contained unpackaged app (`MarkdownMkII-win-x64.zip`).

## Trying the app with separate data

Only **Debug** builds can use a data folder other than the real one, so trials do not touch your notes. Release builds always ignore both of these mechanisms.

1. The `MARKDOWN_MKII_DATA_DIR` environment variable, set to an absolute path.
2. The file `.artifacts/debug-data-root.txt`, which contains an absolute path. This is for the MSIX profile, which does not always receive the environment variables from `launchSettings.json`.

Edit the file while the app is closed, and clear it when you finish so the app returns to the normal archive.

## Conventions

- **Encoding:** C#, XAML, RESW, project files, the solution, the manifest, and PowerShell scripts are UTF-8 **with BOM** (see `.editorconfig`); without a BOM, Visual Studio reads files that contain only ASCII as Windows-1252. Documentation, JSON, and YAML are UTF-8 without a BOM.
- **Warnings:** the build uses `-warnaserror`, so new warnings must not appear.
- **Interface text:** every string exists in `src/MarkdownMkII.App/Strings/it/Resources.resw` and `src/MarkdownMkII.App/Strings/en-US/Resources.resw`, with the same keys and the same placeholders. A parity test checks this.
- **Commands:** every editor command is registered once in `EditorPalette`. Menus, shortcuts, and command search read from there.
- **Code comments:** in English and brief, only to explain constraints the code alone does not show.

## Recommended manual checks

Repeat these with F5 after changes to the editor, menus, or security:

1. **Context menu:** select text by dragging and by double-click, and confirm the menu does not open on its own. Right-click and Shift+F10 open it; an outside click and Esc close it without reopening it and without losing the selection.
2. **Format:** use Format from the toolbar and from the right-click menu on the same selection, then undo.
3. **Markdown structures:** move between text, a table, a list, a heading, and a code block. Contextual groups (table, lines) must follow the structure, and must not treat code samples as tables.
4. **Saving:** type, close the note, and reopen it; close the app and start it again.
5. **Security:** see [protected notes](protected-notes.md#checks) (Windows Hello, lock, integrity dialog).
6. **Interface:** Italian and English, light and dark theme, a narrow window.
