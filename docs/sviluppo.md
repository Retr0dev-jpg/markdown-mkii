# Sviluppo e verifica

Come compilare, testare e provare l'app senza toccare le proprie note.

## Requisiti

- Windows 10 1809 o successivo, x64.
- [.NET SDK 10](https://dotnet.microsoft.com/download).
- Per il debug: Visual Studio con il carico di lavoro per le app WinUI (Windows App SDK).

## Compilare e avviare

In Visual Studio: apri `MarkdownMkII.slnx`, scegli `MarkdownMkII.App`, configurazione **Debug x64**, profilo MSIX, e premi **F5**. È il modo consigliato per provare l'app: MSIX, risorse e Windows Hello funzionano come nell'uso reale.

Da riga di comando:

```powershell
# App non pacchettizzata (produce MarkdownMkII.App.exe in bin/x64/...)
dotnet build src/MarkdownMkII.App -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true

# App MSIX
dotnet build src/MarkdownMkII.App -p:Platform=x64 -p:WindowsPackageType=MSIX -p:WindowsAppSDKSelfContained=false
```

## Test

| Progetto | Cosa verifica |
| --- | --- |
| `tests/MarkdownMkII.Core.Tests` | Parsing, comandi Markdown, anteprima, coda dei salvataggi, parità delle risorse it/en |
| `tests/MarkdownMkII.Storage.Tests` | SQLite, ricerca, cronologia, allegati, import/export, cifratura, integrità, 100.000 note |
| `tests/MarkdownMkII.App.Logic.Tests` | Stato dell'editor, impostazioni, file di recupero, anchor di integrità |
| `tests/MarkdownMkII.App.RuntimeChecks` | Controlli delle risorse da eseguire dentro l'app avviata dal debugger ([istruzioni](../tests/MarkdownMkII.App.RuntimeChecks/README.md)) |

I primi tre progetti girano anche su Linux; il test dell'anchor nel registro di Windows lì termina senza verifiche.

```powershell
# Tutti i test, avvisi trattati come errori (risultati TRX in .artifacts/test-results)
./scripts/verify.ps1

# Test più build Release dell'app, MSIX e non pacchettizzata
./scripts/verify.ps1 -IncludeApp -Configuration Release
```

Lo script richiede PowerShell 7 (`pwsh`). Con `-AppBuild MSIX` o `-AppBuild Unpackaged` compila una sola variante.

## Integrazione continua

- `.github/workflows/core-tests.yml` esegue i test su Linux e `verify.ps1 -IncludeApp` su Windows, a ogni push e pull request.
- `.github/workflows/release.yml` pubblica la release **Latest** a ogni push su `main`, con lo zip dell'app non pacchettizzata e autonoma (`MarkdownMkII-win-x64.zip`).

## Provare l'app con dati separati

Solo le build **Debug** possono usare una cartella dati diversa da quella reale, così le prove non toccano le tue note. Le build Release ignorano sempre questi due meccanismi.

1. La variabile d'ambiente `MARKDOWN_MKII_DATA_DIR`, con un percorso assoluto.
2. Il file `.artifacts/debug-data-root.txt`, che contiene un percorso assoluto. Serve per il profilo MSIX, che non sempre riceve le variabili d'ambiente di `launchSettings.json`.

Modifica il file ad app chiusa, e svuotalo al termine delle prove per tornare all'archivio normale.

## Convenzioni

- **Codifica:** sorgenti C#, XAML, RESW, file di progetto, soluzione, manifest e script PowerShell sono in UTF-8 **con BOM** (vedi `.editorconfig`); senza BOM Visual Studio legge come Windows-1252 i file che contengono solo ASCII. Documentazione, JSON e YAML sono in UTF-8 senza BOM.
- **Avvisi:** la build usa `-warnaserror`, quindi non devono comparire nuovi avvisi.
- **Testi dell'interfaccia:** ogni stringa esiste in `src/MarkdownMkII.App/Strings/it/Resources.resw` e `src/MarkdownMkII.App/Strings/en-US/Resources.resw`, con le stesse chiavi e gli stessi segnaposto. Un test di parità lo verifica.
- **Comandi:** ogni comando dell'editor è registrato una sola volta in `EditorPalette`. Menu, scorciatoie e ricerca dei comandi leggono da lì.
- **Commenti nel codice:** in inglese e brevi, solo per spiegare vincoli che il codice da solo non mostra.

## Prove manuali consigliate

Da ripetere con F5 dopo modifiche a editor, menu o sicurezza:

1. **Menu contestuale:** selezionare testo con trascinamento e doppio clic, senza che il menu si apra da solo. Clic destro e Maiusc+F10 lo aprono; clic esterno ed Esc lo chiudono senza riaprirlo e senza perdere la selezione.
2. **Formato:** usare Formato dalla barra e dal clic destro sulla stessa selezione, poi annullare.
3. **Strutture Markdown:** spostarsi fra testo, tabella, elenco, titolo e blocco di codice. I gruppi contestuali (tabella, righe) devono seguire la struttura, senza scambiare esempi di codice per tabelle.
4. **Salvataggio:** scrivere, chiudere la nota e riaprirla; chiudere l'app e riavviarla.
5. **Sicurezza:** vedi [note protette](note-protette.md#verifiche) (Windows Hello, blocco, dialogo di integrità).
6. **Interfaccia:** italiano e inglese, tema chiaro e scuro, finestra stretta.
