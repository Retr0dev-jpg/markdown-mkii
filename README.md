# Markdown MkII

Un gestore di note Markdown per Windows, veloce e locale. Scritto in C# con WinUI 3.

Le note vivono in un unico archivio sul tuo PC: niente cloud, niente account. Puoi proteggere le note più riservate con una password.

## Download

Scarica `MarkdownMkII-win-x64.zip` dall'ultima [release](../../releases/latest), estrai la cartella e avvia `MarkdownMkII.App.exe`. Non serve installare nulla.

Requisiti: Windows 10 (1809) o Windows 11, 64 bit.

## Cosa fa

- **Scrittura:** editor Markdown con salvataggio automatico, formattazione, tabelle, ricerca e sostituzione, outline e minimappa.
- **Anteprima:** anteprima nativa con wikilink, collegamenti tra note, allegati, note incorporate e diagrammi.
- **Organizzazione:** categorie, tag, preferiti, colori, archivio e cestino, con ricerca a testo pieno.
- **Cronologia e backup:** fino a 30 versioni per nota, più backup completi e ripristino dell'archivio.
- **Note protette:** cifratura AES-256 per singola nota, codice di recupero e sblocco facoltativo con Windows Hello ([dettagli](docs/note-protette.md)).
- **Importazione ed esportazione:** file e cartelle Markdown in entrata; Markdown, HTML, stampa e condivisione in uscita.
- **Interfaccia:** italiano e inglese, tema chiaro o scuro, scorciatoie personalizzabili.

I file importati vengono copiati nell'archivio. Gli originali non vengono mai modificati.

## Dove sono i dati

- **Versione zip:** `%LOCALAPPDATA%\Markdown MkII`.
- **Versione MSIX:** la cartella dati del pacchetto.

In entrambi i casi la cartella contiene `notes.db` (le note) e `settings.json` (le preferenze). Per spostare tutto su un altro PC usa **Impostazioni → Dati → Backup**.

## Compilare dal codice

Servono Windows e il [.NET SDK 10](https://dotnet.microsoft.com/download).

```powershell
git clone <url-del-repository>
cd markdown-mkii
dotnet build src/MarkdownMkII.App -p:Platform=x64
```

Per sviluppare, apri `MarkdownMkII.slnx` in Visual Studio, scegli `MarkdownMkII.App` in configurazione Debug x64 e premi **F5**.

Test:

```powershell
dotnet test tests/MarkdownMkII.Core.Tests
dotnet test tests/MarkdownMkII.Storage.Tests
dotnet test tests/MarkdownMkII.App.Logic.Tests
```

Documentazione:

- [Sviluppo e verifica](docs/sviluppo.md): build, test, dati di prova separati, convenzioni.
- [Architettura](docs/architettura.md): come sono organizzati archivio, editor e interfaccia.
- [Note protette](docs/note-protette.md): cifratura, integrità e limiti di sicurezza.
- [Limiti noti](docs/limiti.md): cosa l'app non fa.
- [AGENTS.md](AGENTS.md): guida rapida per agenti AI.

| Cartella | Contenuto |
| --- | --- |
| `src/MarkdownMkII.Core` | Parsing e trasformazioni Markdown, anteprima |
| `src/MarkdownMkII.Storage` | Archivio SQLite, ricerca, cifratura, import ed export |
| `src/MarkdownMkII.App` | Interfaccia WinUI 3 |
| `tests/` | Test automatici |

## Contribuire

Segnalazioni e pull request sono benvenute. Prima di aprire una pull request esegui i test e segui le convenzioni in [docs/sviluppo.md](docs/sviluppo.md#convenzioni).

## Licenza

Distribuito con licenza [GNU General Public License v3.0](LICENSE). Puoi usare, studiare, modificare e ridistribuire il programma, anche modificato, purché le versioni distribuite restino sotto la stessa licenza e con il codice sorgente disponibile.
