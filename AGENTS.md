# AGENTS.md

Guida rapida per agenti AI e nuovi contributori. Documentazione in italiano; codice e commenti in inglese.

## Progetto

Markdown MkII: gestore di note Markdown per Windows (C#, .NET 10, WinUI 3). Tutte le note stanno in un archivio SQLite locale; una nota aperta alla volta; note protette facoltative con cifratura AES-256-GCM.

| Percorso | Contenuto |
| --- | --- |
| `src/MarkdownMkII.Core` | Parsing e comandi Markdown, anteprima (IR), cronologia annulla/ripeti. Non dipende da WinUI né da SQLite. |
| `src/MarkdownMkII.Storage` | `NoteDatabase` (partial class in più file): SQLite, ricerca FTS5, cronologia, allegati, import/export, cifratura, sigillo di integrità. |
| `src/MarkdownMkII.App` | WinUI 3: `Views/`, `ViewModels/`, `Services/` (sicurezza, backup, reset, localizzazione), `Strings/` (risorse it e en-US). |
| `tests/` | xUnit: `Core.Tests`, `Storage.Tests`, `App.Logic.Tests` (collega singoli file dell'app); `App.RuntimeChecks` si esegue solo dentro l'app. |
| `docs/` | `architettura.md`, `note-protette.md`, `sviluppo.md`, `limiti.md`. |

## Comandi

```powershell
dotnet test tests/MarkdownMkII.Core.Tests -warnaserror
dotnet test tests/MarkdownMkII.Storage.Tests -warnaserror
dotnet test tests/MarkdownMkII.App.Logic.Tests -warnaserror
dotnet build src/MarkdownMkII.App -warnaserror -p:Platform=x64 -p:WindowsPackageType=MSIX -p:WindowsAppSDKSelfContained=false
```

L'app si compila solo su Windows. Per provarla va avviata da Visual Studio con F5 (Debug x64, profilo MSIX); vedi [docs/sviluppo.md](docs/sviluppo.md).

## Regole

- **Zero avvisi:** CI e `scripts/verify.ps1` compilano con `-warnaserror`.
- **Codifica:** i file `.cs`, `.xaml`, `.resw`, `.csproj`, `.props`, `.slnx`, `.appxmanifest`, `.pubxml` e `.ps1` sono UTF-8 con BOM (`.editorconfig`). Markdown, JSON e YAML senza BOM.
- **Stringhe dell'interfaccia:** ogni chiave va aggiunta sia in `Strings/it/Resources.resw` sia in `Strings/en-US/Resources.resw`, con gli stessi segnaposto (`ResourceParityTests`). Nel codice si leggono con `Strings.T("Chiave")` o `Strings.Format(...)`.
- **Comandi dell'editor:** si registrano in `ViewModels/EditorPalette.cs`; menu, scorciatoie e ricerca comandi derivano da lì.
- **Scritture nell'archivio:** le transazioni che modificano note, revisioni o allegati terminano con `Commit(db, tx)`, che prima sigilla le modifiche alle note protette (`NoteDatabase.Integrity.cs`). Il `tx.Commit()` diretto resta solo nelle migrazioni e nel codice del sigillo stesso.
- **Schema del database:** la versione è in `PRAGMA user_version` (attuale: 3). Una modifica allo schema richiede migrazione, aggiornamento dei controlli in `RestoreAsync` e test.
- **Chiavi crittografiche:** si allocano con `SecretMemory` e si azzerano con `CryptographicOperations.ZeroMemory`. Il testo decifrato non va scritto su disco né nei log.
- **Commenti:** in inglese, brevi, solo per vincoli che il codice non rende evidenti.
- **Modifiche coerenti:** quando cambi un comportamento, aggiorna anche i test e la sezione pertinente di `docs/`.

## Dove guardare

| Argomento | File |
| --- | --- |
| Flusso dei dati, editor, salvataggio, backup, reset | `docs/architettura.md` |
| Note protette, chiavi, integrità, memoria | `docs/note-protette.md` |
| Build, test, dati di prova separati, prove manuali | `docs/sviluppo.md` |
| Cosa l'app non fa | `docs/limiti.md` |
| Release automatica | `.github/workflows/release.yml` |
