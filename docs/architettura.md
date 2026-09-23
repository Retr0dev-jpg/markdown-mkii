# Architettura

Come sono organizzati archivio, editor e interfaccia. Per build e test vedi [sviluppo](sviluppo.md); per la cifratura [note protette](note-protette.md).

## Flusso dei dati

`Views → EditorViewModel / NoteBrowser → NoteArchive → MarkdownMkII.Storage → SQLite`

`MarkdownMkII.Core` rimane indipendente da WinUI e SQLite. Contiene parsing, trasformazioni, cronologia di annullamento e IR dell'anteprima. Gli helper per i file servono all'importazione/esportazione e ai formati Markdown compatibili.

## Archivio

`INoteRepository` definisce creazione, lettura, salvataggio, ricerca e organizzazione. `INoteResources` risolve collegamenti e allegati. `NoteDatabase` implementa entrambi, con schema versionato tramite `user_version`, WAL, chiavi esterne e transazioni. Le operazioni del provider vengono eseguite su thread di lavoro; un semaforo serializza le scritture. Un errore temporaneo di inizializzazione può essere ritentato. Le transazioni che modificano dati terminano con `Commit(db, tx)`, che prima sigilla le modifiche ai record protetti (schema 3, [integrità](note-protette.md#integrità-righe-o-archivio-riportati-indietro)) e dopo il commit aggiorna l'anchor esterno.

Le note hanno ID stabili. Titolo e organizzazione sono separati dal contenuto; la versione del testo rileva scritture obsolete senza invalidare una modifica ai metadati. L'indice FTS5 viene aggiornato tramite trigger. Le query delle liste restituiscono riepiloghi, non corpi Markdown, con pagine da 100 elementi e limite massimo 200.

Le tabelle associano categorie, tag e collegamenti alle note. Gli allegati sono BLOB identificati dall'hash SHA-256 e deduplicati per contenuto. I collegamenti nuovi usano `note://ID` e `attachment://HASH`; i wikilink per titolo sono risolti anche per note importate. Un titolo ambiguo richiede una scelta nell'interfaccia.

## Editor e salvataggio

`NoteEditorViewModel` contiene testo, selezione, parsing e `TextUndoHistory`. `EditorViewModel.CurrentNote` è l'unica nota aperta. `LatestSaveQueue` serializza i salvataggi e continua finché gli snapshot scritti comprendono tutte le modifiche sopraggiunte durante l'I/O.

Due timer richiedono il salvataggio: dopo 750 ms di inattività e dopo 5 secondi di scrittura continua. Cambio nota, chiusura dell'editor e uscita attendono lo svuotamento della coda. Nel cambio nota il caricamento precede il salvataggio finale, così anche il testo digitato durante la lettura viene conservato. Un errore lascia il buffer modificato, mostra lo stato di errore, ritenta dopo 5 secondi (tranne i conflitti di versione) e impedisce una chiusura che lo perderebbe. `SaveAsync` costruisce il risultato dai dati appena scritti, senza rileggere e decifrare la nota. La manutenzione della protezione non fa parte del salvataggio: riprende all'avvio, dalle impostazioni di sicurezza o in background dopo un salvataggio riuscito (al più ogni 5 minuti), e un suo errore non rende fallito il salvataggio.

La cronologia SQLite conserva al massimo 30 contenuti distinti, con checkpoint al minuto e alla chiusura. Ripristinare una versione passa attraverso una normale modifica dell'editor e può essere annullato. Annulla/ripeti del testo resta indipendente dalle modifiche native di colore e formattazione del RichEditBox, la cui cronologia nativa è disattivata (`UndoLimit = 0`). La digitazione viene raggruppata per parola (inserimenti contigui, Backspace e Canc consecutivi); i comandi restano passi distinti. Ripeti risponde a Ctrl+Y e Ctrl+Shift+Z.

I comandi applicano al RichEditBox soltanto l'intervallo modificato (`ReplaceChangedRange`), così scorrimento, IME e layout restano stabili; se la verifica del testo risultante fallisce si ricade su `SetText`. Il focus torna all'editor dopo ogni comando. Durante una composizione IME il testo non viene sincronizzato né evidenziato, e Invio/Tab/parentesi restano all'IME. Shift+Invio (tabulazione verticale di RichEdit) e i salti pagina vengono letti come a capo. L'evidenziazione non viene applicata se il testo mostrato non coincide ancora con il modello. I comandi che non calcolano una selezione riportano quella precedente attraverso la modifica (`MarkdownEditing.MapSelection`) invece di spostare il cursore all'inizio.

L'accoppiamento automatico (impostazione **Chiusura automatica delle parentesi**) avvolge una selezione con qualunque carattere accoppiabile; senza selezione chiude solo `( [ {` e il backtick a confine di parola, e scavalca la parentesi di chiusura già presente. Incolla decide in modo sincrono se gestire gli appunti: i formati non supportati tornano all'incolla nativo. Incolla e trascinamento inseriscono prima le immagini nella nota di partenza, poi aprono i file Markdown; gli errori di lettura dei file mostrano un messaggio.

Il front matter viene modificato riga per riga: aggiornare, aggiungere, rinominare, ordinare o rimuovere una chiave lascia invariati commenti, valori su più righe, elenchi a blocchi, virgolette e il corpo. I valori che YAML leggerebbe diversamente vengono messi tra virgolette; gli elenchi vengono divisi rispettando le virgolette. I campi si impostano con **Imposta campo front matter…** o con i comandi dedicati a id, titolo, alias, tag, descrizione, data, stato, autore, cssclass, lingua e tipo.

## Importazione, esportazione e backup

`MarkdownTransfer` esamina i percorsi selezionati, presenta candidati e importa copie nel database. `ImportMetadata` legge YAML senza istanziare tipi applicativi; `ExportMetadata` aggiorna soltanto i campi gestiti e preserva gli altri dati del front matter. `MarkdownReference` usa gli span del parser per riscrivere collegamenti senza toccare esempi di codice.

Le risorse locali vengono copiate solo se selezionate; nessuna risorsa remota viene scaricata dall'importazione. La provenienza e l'hash del documento evitano importazioni identiche ripetute. Avvisi segnalano collegamenti non risolti e allegati mancanti. L'esportazione converte gli ID in percorsi relativi per le note incluse nella raccolta.

`ArchiveBackup` crea un contenitore ZIP con snapshot ottenuto tramite `SqliteConnection.BackupDatabase` e preferenze JSON. Il ripristino controlla schema, integrità e associazioni, conserva un backup preventivo e ripristina lo stato precedente se la sostituzione delle preferenze fallisce. La copia ripristinata diventa il nuovo riferimento dell'anchor di integrità, così non viene segnalata come archivio riportato indietro.

`ArchiveReset` (Impostazioni → Dati → **Reset completo dell’app**) richiede di digitare una parola di conferma e propone un backup preventivo; se il backup viene annullato o fallisce non elimina nulla. Chiude l'editor senza salvare, scarta le bozze protette, rimuove la credenziale Windows Hello (file e chiave), svuota la cache degli allegati, elimina e ricrea il database (`NoteDatabase.ResetAsync`, che azzera anche chiavi e catalogo di sessione), rimuove gli anchor di integrità dal registro, cancella backup automatici, file temporanei, registro diagnostico e cartelle legacy di recupero e cronologia, quindi ripristina le preferenze predefinite mantenendo la lingua.

## Interfaccia e comandi

`NoteBrowser` è l’unico catalogo, nella barra laterale: ricerca con debounce, sezione, filtri e lista virtualizzata. La sezione (Tutte, Preferiti, Archivio, Cestino) è un menu a discesa distinto dai filtri; categoria e tag sono gli unici filtri, mostrati come chip rimovibili. `ISupportIncrementalLoading` aggiunge riepiloghi in blocchi da 100 durante lo scorrimento verticale. Cambio di sezione o filtro, blocco e scaricamento del controllo invalidano le richieste precedenti; un errore sospende il caricamento automatico fino a Riprova. Gli stati vuoti distinguono archivio vuoto, sezioni vuote e ricerca senza risultati. All’avvio e dopo la chiusura dell’editor, lo spazio di lavoro mostra soltanto l’invito ad aprire una nota. L’importazione è nel menu del pulsante Nuova nota; l’esportazione della raccolta nel menu della sezione (`ArchiveDialogs.ExportCollectionAsync`). `ArchiveDialogs.NoteMenu` è l'unico costruttore del menu delle note. Un evento ContextRequested gestisce mouse e tastiera; il pulsante con tre punti compare al passaggio del puntatore o con il focus da tastiera. Nel Cestino il clic su una nota apre il menu di ripristino. `MainWindow.FocusNoteSearch` porta il focus sulla ricerca (comando `searchNotes`, Ctrl+Shift+F).

`EditorPalette` è il registro dei comandi: ID, etichetta, categoria, ambito, disponibilità e combinazione predefinita. Menu, tooltip, ricerca comandi e `ShortcutService` usano lo stesso registro. Le assegnazioni personalizzate sono salvate nelle preferenze; i conflitti non vengono sovrascritti. Il dispatcher protegge AltGr, digitazione, finestre modali e acquisizione delle combinazioni; i simboli vengono risolti secondo il layout della tastiera.

La barra dell'editor contiene quattro menu, grassetto e corsivo (nascosti nelle finestre strette), **Comandi** (ricerca di tutti i comandi, Ctrl+Shift+P) e la chiusura dell'editor. I menu sono costruiti in `EditorPage.Menus.cs` a partire da `EditorPalette`:

| Menu | Contenuto |
| --- | --- |
| Documento | Nuova, apri, rinomina, duplica, preferito, categoria, tag, esportazione e condivisione, protezione, blocco, cronologia, chiudi |
| Formato | Stili del testo, titoli 1–6, elenchi, citazione, commento, trasformazioni |
| Inserisci | Link, immagine, wikilink, nota a piè di pagina, tabella, codice, callout, separatore, formule e diagrammi, indice, front matter |
| Vista | Editor, anteprima, affiancamenti, pannello laterale, a capo, navigazione, lettura, sezioni |

Il clic destro contiene taglia, copia, incolla, seleziona tutto, annulla, ripeti, Formato, Inserisci e Righe. I gruppi Tabella, Elenco e Titolo compaiono solo quando il cursore è in quella struttura (`EditorContext`, verificato da `EditorContextTests`); una selezione aggiunge **Estrai in nota**. Il menu non si apre selezionando il testo; clic esterno ed Esc lo chiudono senza perdere la selezione.

`EditorPage` separa input, selezione, rendering, outline, ricerca, layout e interop. L'anteprima riceve un `NotePreviewContext` con ID e documenti incorporati risolti dal database; profondità, dimensioni e cicli sono limitati. I risultati asincroni vengono applicati soltanto alla nota e al testo che li hanno richiesti; una richiesta arrivata durante un aggiornamento viene rieseguita al termine invece di andare persa. Nelle note dell'archivio le immagini e i media con percorso relativo (`./foto.png`, `![[foto.png]]`) diventano `attachment-name://nota/nome` e vengono risolti per nome fra gli allegati (`NoteDatabase.FindAttachmentAsync`), prima quelli protetti della nota e poi quelli pubblici. Le liste ordinate conservano marcatore alfabetico o romano e delimitatore; gli elenchi di definizioni mostrano tutti i termini che condividono una definizione; le celle di tabella mantengono tutti i paragrafi; un embed con un titolo inesistente viene segnalato come mancante. Lo scorrimento sincronizzato scende nei blocchi annidati di liste, citazioni e tabelle. I gestori di stampa e condivisione della finestra vengono scollegati quando la pagina viene scaricata.

`SettingsService` serializza mutazioni e scritture delle preferenze. All'avvio, prima di `InitializeComponent`, `LocalizationService.EffectiveLanguage` imposta sempre un override esplicito della lingua. Per **Lingua di sistema** lo ricava dall'elenco lingue di Windows (`GlobalizationPreferences.Languages`, prima lingua supportata, altrimenti italiano). Svuotare l'override persistito non rimuove infatti in modo affidabile una lingua scelta in una sessione precedente. Le risorse italiane e inglesi vengono controllate dalle suite di parità e dai controlli di risoluzione nel processo WinUI.
