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
- `.github/workflows/release.yml` publishes a versioned release, marked **Latest**, on every push to `main`, with the installer and the portable zip (see [Versioning](#versioning) and [Installer and updates](#installer-and-updates)).

## Versioning

The version has a single source, `Directory.Build.props`:

- `AppVersionBase` holds `Major.Minor` and is changed by hand: raise the minor for new features, the major for incompatible changes (for example an archive the previous version cannot open).
- The patch number is `VersionPatch`. The release workflow sets it to the GitHub Actions run number, so every release gets a new version (`1.0.42`). Local builds use `0` and add the `-dev` suffix (`1.0.0-dev`).
- The .NET SDK appends the commit to the informational version (`1.0.42+<commit>`). The **About** page and the diagnostic report show both.
- The MSIX package identity gets the same version with a fourth part set to zero (`1.0.42.0`): the `StampAppxManifestVersion` target in `MarkdownMkII.App.csproj` writes it into the generated `AppxManifest.xml`. The `Version` in `Package.appxmanifest` is not used.

Each release is tagged `v<version>` (for example `v1.0.42`), is marked **Latest**, and has notes that GitHub generates from the commits since the previous release. The workflow fails if the published executable does not carry the expected version.

## Installer and updates

Releases are packaged with [Velopack](https://velopack.io). The `vpk` tool runs in the release workflow, with the same version as the `Velopack` package in `MarkdownMkII.App.csproj`, and produces:

| File | Purpose |
| --- | --- |
| `MarkdownMkII-<version>-Setup.exe` | Per-user installer (no administrator rights), Start menu and desktop shortcuts, uninstall from Windows settings |
| `MarkdownMkII-<version>-Portable.zip` | The same app without installation |
| `MarkdownMkII-<version>-full.nupkg`, `-delta.nupkg`, `releases.win.json`, `assets.win.json`, `RELEASES` | Update packages and the feed the app reads; the delta is built from the previous release |

In the app:

- `Program.cs` replaces the XAML-generated entry point (`DISABLE_XAML_GENERATED_MAIN`) so `VelopackApp.Build().Run()` runs before WinUI; it then calls `XamlGeneratedProgram.XamlGeneratedMain()`.
- `Services/AppUpdates.cs` checks the latest GitHub release at startup (the **About → Updates → Check at startup** option, on by default) and downloads a newer version in the background. The update is installed only after a normal close, when notes are saved and locked; **Restart and update** goes through the same close flow.
- MSIX and development builds are not managed by Velopack: the Updates card says so and never checks.

Velopack installs the app in `%LOCALAPPDATA%\MarkdownMkII`, while the notes stay in `%LOCALAPPDATA%\Markdown MkII`, so uninstalling or updating never touches them. Code signing is not configured yet; Velopack can sign the setup and the packages with `vpk pack --signParams` once a certificate or a signing service is available.

## Trying the app with separate data

Only **Debug** builds can use a data folder other than the real one, so trials do not touch your notes. Release builds always ignore both of these mechanisms.

1. The `MARKDOWN_MKII_DATA_DIR` environment variable, set to an absolute path.
2. The file `.artifacts/debug-data-root.txt`, which contains an absolute path. This is for the MSIX profile, which does not always receive the environment variables from `launchSettings.json`.

Edit the file while the app is closed, and clear it when you finish so the app returns to the normal archive.

## Diagnostic report

**About → Report a problem** opens a new GitHub issue with a Markdown report already filled in. It also copies the report to the clipboard, because GitHub trims long prefilled bodies. **Copy report** only copies it. `Services/DiagnosticsReport.cs` builds the report, which contains:

- App version, commit, architecture, installation type (MSIX, Velopack setup, Velopack portable, or neither), build configuration, and update state.
- Windows version and build, .NET, Windows App SDK, app and Windows languages, theme, memory and uptime.
- Archive state (`NoteDatabase.DiagnosticsAsync`): schema, SQLite version, sizes, counts of notes, revisions and attachments, and the state of protection, Windows Hello, cleanup and integrity.
- The last 20 errors from the diagnostic log.

It never contains note text or titles, passwords or keys. Paths inside the user profile become `%USERPROFILE%`, and the Windows user name becomes `<user>`. The author (`Authors`) and the repository address (`RepositoryUrl`) are set in `Directory.Build.props` and read by `Services/AppLinks.cs` from the assembly.

## Microsoft Store

Every release run also builds the Store package with the same version, `MarkdownMkII-<version>-Store.msixupload`: an x64 + ARM64 bundle, not signed, because the Store signs it. It is attached to the workflow run as an artifact (kept 90 days), not to the release, because nobody can install an unsigned Store package. Download it from the run page in **Actions** and upload it to Partner Center. The package depends on the Windows App SDK runtime, which the Store installs automatically.

To build it locally, with the .NET SDK alone:

```powershell
./scripts/build-store.ps1 -VersionPatch 42   # use the patch of the GitHub release you are submitting
```

**Automatic submission (optional).** Once the app is live in the Store, the workflow can submit each new version for certification. It needs a Microsoft Entra app with the **Manager** role in Partner Center (Account settings → User management → Microsoft Entra applications), then in the GitHub repository:

- the variable `MSSTORE_APP_ID` (Settings → Secrets and variables → Actions → Variables): the Store ID of the app, such as `9N…`;
- the secrets `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET`, and `SELLER_ID`.

Without the variable the submission steps are skipped. A failed submission, for example while the previous one is still in certification, shows a warning but does not block the GitHub release.

- `Identity` `Name` and `Publisher` and `PublisherDisplayName` in `Package.appxmanifest` must match **Product management → Product identity** in Partner Center. Changing them changes the package identity, and with it the data folder of the MSIX app.
- The only target device family is `Windows.Desktop`; in Partner Center select only **Windows 10/11 Desktop**.
- In the Store copy, automatic updates come from the Store: Velopack does not manage MSIX, so the app never downloads updates itself.
- The runtime identifier follows the requested `Platform`, and `RuntimeIdentifiers` lists every supported runtime, so a single build can produce the ARM64 package on an x64 PC.

## App icon

`scripts/generate-icons.ps1` draws the icon in code (the Markdown mark on a rounded light-to-medium blue square) and writes everything into `src/MarkdownMkII.App/Assets`:

- `AppIcon.ico`, with sizes from 16 to 256 pixels: the executable icon (`ApplicationIcon`), the window icon of the unpackaged build, and the setup icon (`vpk pack --icon`).
- The MSIX images (`Square44x44Logo`, `Square150x150Logo`, `Wide310x150Logo`, `StoreLogo`, `SplashScreen`, `LockScreenLogo`) at every scale, plus the `targetsize` variants used by the taskbar, Start, and Explorer.

To change the icon, edit the colors or the shapes in the script and run it again (`powershell -File scripts/generate-icons.ps1`); it replaces all images in the folder. Do not edit the generated images by hand.

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
