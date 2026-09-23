# Note protette

Come funzionano le note cifrate: uso, chiavi, salvataggio, blocco, integrità, memoria, backup e verifiche. Il codice è in `src/MarkdownMkII.Storage` (archivio e cifratura) e in `src/MarkdownMkII.App/Services/NoteSecurity.cs` (dialoghi, blocco e sblocco).

## Uso

Dal menu di una nota, **Proteggi nota** configura la protezione, se necessario, e converte quella nota. La password unica deve contenere almeno 12 caratteri, di cui almeno 5 diversi (`PasswordPolicy`, applicata sia dai dialoghi sia dall'archivio); sono disponibili conferma, incolla e passphrase. Il codice di recupero va scaricato e conservato separatamente dall'archivio. La conferma si abilita soltanto dopo la scrittura riuscita del file; solo confermando vengono attivate le credenziali preparate. Configurazione, recupero e rinnovo seguono lo stesso flusso. Nessuna nota esistente viene protetta automaticamente dalla migrazione.

**Impostazioni → Sicurezza** contiene password, Windows Hello, recupero, visibilità, intervallo di inattività e blocco manuale. Lo sblocco vale per tutte le note protette. L'intervallo predefinito è 5 minuti, selezionabile fra 1, 5, 15 e 30. Il blocco o la sospensione di Windows chiudono la sessione; il semplice cambio di applicazione non la chiude immediatamente.

Il titolo e l'organizzazione sono pubblici per impostazione predefinita. **Nascondi tutti i dettagli** elimina le copie pubbliche di titolo, categoria, tag, colore e preferito delle note protette. Da bloccate, queste note sono indicate come “Nota protetta”. L'anteprima del testo non viene salvata in chiaro. Lo sblocco rende i dati disponibili in memoria, senza convertire le note sul disco.

Windows Hello usa il gesto configurato in Windows, compreso il PIN di Windows quando offerto dal sistema. Non esiste un PIN separato dell'app. Password e codice di recupero restano necessari per accedere da un altro dispositivo. Annullare Hello non produce richieste ripetute.

Cambio password, recupero e rinnovo del codice ruotano la chiave principale: le chiavi delle note vengono ricifrate con una chiave nuova, involucri di password e recupero e verificatore vengono ricostruiti, poi WAL e pagine libere vengono ripuliti (checkpoint e VACUUM). Password e codici precedenti non aprono quindi nulla dei dati attuali, nemmeno da una copia del solo file principale. Per poter ruotare anche durante un semplice cambio password, il segreto di recupero è conservato cifrato con la chiave principale (`Protection.RecoverySecret`); non concede nulla oltre alla chiave che lo decifra. Negli archivi creati prima di questa custodia, un cambio password esegue solo la pulizia finché il codice non viene rinnovato o usato. Dopo ogni rotazione Windows Hello, che cifrava la chiave precedente, viene disattivato e va riattivato.

Lo sblocco attende un intervallo crescente dopo ogni tentativo errato (mezzo secondo per errore, fino a 10 secondi). L'inattività considera tasti, clic, rotella e movimento del puntatore nella finestra, l'input nei dialoghi mentre l'app è in primo piano e si sospende mentre è aperto un selettore di file. Copiare negli appunti il testo di una nota protetta (comandi di copia e Ctrl+C/Ctrl+X) chiede la stessa conferma del testo in chiaro di esportazione, stampa e condivisione, una volta per nota e sessione. Con i dettagli nascosti, un collegamento inserito verso una nota protetta usa l'etichetta “Nota protetta” invece del titolo. Il file di recupero contiene solo archivio, data, codice e istruzioni, senza titoli né identificativi di note.

## Archivio e chiavi

`NoteDatabase.Protection.cs`, `NoteDatabase.ProtectedNotes.cs` e `NoteProtection.cs` implementano il contratto `INoteProtection` e la migrazione SQLite 1 → 2; `NoteDatabase.Integrity.cs` aggiunge il sigillo di integrità (schema 3, vedi sotto).

| Elemento | Persistenza |
| --- | --- |
| Contenuto e revisioni | AES-256-GCM con chiave casuale distinta per nota |
| Metadati, collegamenti in uscita e nomi degli allegati | Record cifrati della nota proprietaria |
| Allegati | Ambito privato per nota; identificatore derivato con HMAC per deduplicare senza pubblicare l'hash del contenuto |
| Chiave della nota | Cifrata con la chiave principale casuale dell'archivio |
| Chiave principale | Copie cifrate mediante la chiave derivata dalla password e mediante il codice di recupero |
| Password | Non memorizzata; PBKDF2-HMAC-SHA256, 600.000 iterazioni, salt casuale di 32 byte |
| Codice di recupero | 32 byte casuali; il database conserva soltanto la chiave principale avvolta con questo codice |

Ogni record AES-GCM ha una versione di formato, un nonce casuale di 96 bit e un tag di 128 bit. I dati autenticati comprendono archivio, nota, tipo di elemento e, dove pertinente, identificativo della revisione o dell'allegato. Cambio password, recupero e rinnovo del codice sostituiscono la chiave principale (vedi sopra) e disattivano l'iscrizione Hello locale, che cifrava la chiave precedente.

L'adattatore `WindowsHelloProtection` richiede una chiave RSA non esportabile del **Microsoft Passport Key Storage Provider**, uso di decifratura, politica di autenticazione obbligatoria e RSA-OAEP-SHA256. L'attivazione prova il percorso crittografico completo prima di conservare l'iscrizione. Se il provider rifiuta algoritmo o politica, l'attivazione fallisce senza ripiego su altri provider. La verifica di disponibilità di `UserConsentVerifier` serve soltanto a presentare l'opzione: non sblocca dati.

`windows-hello.json` contiene identificativi e chiave principale cifrata per quel dispositivo, mai la chiave in chiaro. Non viene incluso nei backup. `settings.json` non contiene credenziali. Il ripristino delle sole preferenze non modifica la protezione.

## Concorrenza, salvataggio e blocco

Le operazioni SQLite e la derivazione delle chiavi vengono eseguite fuori dal thread UI. Letture, scritture, conversioni e backup condividono la serializzazione dell'archivio, evitando letture intermedie durante la conversione. Le modifiche dei dati usano transazioni e WAL.

Il salvataggio automatico conserva le scadenze esistenti. Per una nota protetta, il testo viene cifrato prima della scrittura. Al blocco la superficie dell'editor viene nascosta e resa non modificabile prima di completare il salvataggio. Il testo, l'annulla/ripeti, l'anteprima, i pannelli e gli indici della sessione vengono svuotati; le chiavi e i buffer temporanei mutabili vengono azzerati. Token e versioni di sessione impediscono alle operazioni precedenti di ripopolare la UI.

Protezione della memoria (`SecretMemory`, `ProcessHardening`):

- **Chiavi:** chiave principale, chiavi delle note, chiavi derivate e segreti di recupero vivono in buffer dell'heap pinned. Il GC non li sposta, quindi l'azzeramento non lascia copie orfane. Le chiavi tenute per la sessione sono anche bloccate in RAM con `VirtualLock`, dove Windows lo consente, e non finiscono nel file di paging.
- **Testo decifrato:** è una stringa .NET e non si può azzerare; `RichEditBox` e Markdig accettano solo `string`. Per ridurne le copie, la cronologia nativa di `RichEditBox` è disattivata (`UndoLimit = 0`), perché l'annulla/ripeti usa soltanto `TextUndoHistory`. Dopo ogni blocco, un GC aggressivo con compattazione, anche del Large Object Heap, rimuove il testo rilasciato e restituisce a Windows le pagine libere; Windows le azzera prima di riusarle.
- **Crash:** all'avvio `WerSetFlags(WER_FAULT_REPORTING_FLAG_NOHEAP)` esclude l'heap dalle segnalazioni di Windows Error Reporting, quindi i dump non contengono il testo delle note.
- **Limiti:** mentre una nota è aperta, il suo testo è in memoria in chiaro, e Windows può scriverne parti nel file di paging o di ibernazione. **Impostazioni → Sicurezza → Crittografia del disco** consiglia BitLocker o la crittografia del dispositivo e apre la relativa pagina di Windows. Un processo con lo stesso utente può comunque leggere la memoria dell'app finché la nota è aperta.

Se il salvataggio fallisce, l'ultima modifica viene sigillata con la chiave della nota prima di chiudere la sessione e scritta anche su disco (`sealed-draft.json`, solo testo cifrato AES-GCM legato a nota e versione, nessuna chiave). La bozza sopravvive quindi a un crash o a una chiusura forzata; l'uscita volontaria è consentita se la bozza è su disco. Dopo lo sblocco la modifica torna nell'editor; il file viene eliminato solo quando il testo ripristinato è stato salvato. Se nel frattempo la versione persistita è cambiata, viene creata una nuova nota protetta denominata “modifica recuperata”, preservando entrambe le versioni. Se la nota è stata eliminata, la bozza non è più decifrabile e viene scartata con un avviso. Prima di svuotare l'editor, il blocco legge dal controllo anche il testo non ancora sincronizzato, compresa una composizione IME in corso.

Il blocco si completa sempre: anche se il sigillo della bozza fallisce, chiavi e catalogo in memoria vengono azzerati e l'interfaccia viene sbloccata non appena nessuna chiave resta in memoria. Il testo non sigillato resta soltanto nel modello dell'editor, nascosto e non salvato, e riappare al successivo sblocco. Il blocco svuota anche la ricerca della barra laterale e la cache degli allegati.

## Ricerca, risorse e uscita dei dati

I testi cifrati (corpi, revisioni, metadati, nomi, bozze) e gli allegati protetti usano il formato 2: lunghezza cifrata e riempimento a blocchi di 256 byte, con il formato legato ai dati autenticati, così la dimensione non rivela la lunghezza esatta. Il formato 1 resta leggibile. Il corpo delle note è legato anche alla versione della riga (`body:{versione}`): un corpo cifrato più vecchio ricopiato al posto dell'attuale viene rifiutato. Una riga con testo cifrato ma non marcata come protetta viene rifiutata invece di mostrare un contenuto vuoto. Un record danneggiato non impedisce lo sblocco: le altre note si aprono e l'utente viene avvisato. Un'esportazione interrompe la scrittura se nel frattempo la sessione viene bloccata e rimuove gli allegati parziali.

## Integrità: righe o archivio riportati indietro

AES-GCM autentica ogni record, ma un record più vecchio e integro resta valido. Lo schema 3 aggiunge un sigillo dell'intero archivio protetto:

- La tabella `Integrity` contiene un'impronta SHA-256 di ogni record cifrato: corpo e metadati della nota, ogni revisione, ogni allegato privato con il nome. I trigger su `Notes`, `Revisions` e `Attachments` accodano soltanto le righe toccate in `IntegrityPending`. Sono SQL puro, così strumenti esterni che modificano note in chiaro continuano a funzionare. Vengono ricreati a ogni apertura, perché eliminarli o alterarli non interrompa l'aggiornamento.
- Prima di ogni commit (`Commit` in `NoteDatabase.Integrity.cs`, usato da tutte le transazioni) le impronte accodate vengono ricalcolate e l'insieme è firmato con HMAC-SHA256 insieme all'identità dell'archivio e a un numero di generazione crescente. La chiave del sigillo è casuale, cifrata con la chiave principale (`Protection.IntegrityKey`) e ricifrata a ogni rotazione delle credenziali. Senza la password nessuno può ricalcolare il sigillo. Da bloccata, l'app non può modificare record protetti. Le righe accodate da strumenti esterni restano quindi non sigillate, non bloccano i salvataggi delle note in chiaro e vengono segnalate allo sblocco. Lo sblocco svuota la coda, perché la rilevazione confronta le impronte con i record reali.
- Dopo il commit la generazione e il sigillo vengono copiati in un **anchor esterno** (`IIntegrityAnchor`, implementato da `IntegrityAnchorStore`). Si trova nel registro dell'utente (`HKCU\Software\Markdown MkII\Integrity`), fuori dalla cartella dell'archivio, protetto con DPAPI e autenticato con la chiave del sigillo.
- Allo sblocco (password, Hello, recupero) vengono verificati il sigillo, ogni impronta rispetto ai record reali e l'anchor. Il risultato è in `NoteDatabase.IntegrityReport`:
  - **Note modificate:** righe riportate indietro, sostituite, aggiunte o rimosse, con i titoli nel dialogo.
  - **Sigillo non verificabile:** sigillo o impronte alterati, oppure chiave del sigillo rimossa. Il verificatore “sigillato” registra che il sigillo esisteva.
  - **Archivio riportato indietro:** la generazione del file è inferiore a quella dell'anchor, oppure uguale con un sigillo diverso.
- Il dialogo **Controllo di integrità** propone di accettare lo stato attuale (`AcceptIntegrityAsync` ricostruisce impronte e sigillo e supera la generazione dell'anchor) oppure di bloccare le note, per esempio per ripristinare un backup. Finché non si sceglie, le scritture su note protette vengono rifiutate con `ArchiveIntegrityException`. Letture e note in chiaro restano disponibili.
- Casi legittimi:
  - Un file più recente dell'anchor (scrittura dell'anchor mancata dopo un crash) viene accettato e l'anchor aggiornato.
  - Un anchor mancante viene ricreato.
  - Il ripristino di un backup dall'app dimentica l'anchor: la copia ripristinata diventa il nuovo riferimento.
  - Il reset completo elimina tutti gli anchor.
  - Gli archivi precedenti migrano allo schema 3 con le impronte calcolate. Al primo sblocco vengono sigillati così come sono (fiducia al primo uso).

Limite dichiarato: chi controlla l'account Windows può riportare indietro insieme archivio e registro. L'anchor protegge da chi ha accesso solo al file dell'archivio, per esempio una cartella sincronizzata, una copia su un altro PC o un disco esterno. Anche una manomissione eseguita mentre l'app è sbloccata e in uso resta fuori dal modello: chi può farla può leggere direttamente la memoria del processo. Un contatore davvero non riportabile indietro richiederebbe il TPM o un server.

La ricerca persistente indicizza soltanto contenuto pubblico e metadati pubblici. Un secondo indice SQLite in memoria contiene i metadati delle note protette durante lo sblocco; viene svuotato al blocco. Il contenuto delle note protette resta ricercabile soltanto nella nota aperta. Le query del catalogo restano paginate.

Le immagini protette vengono lette da memoria. I collegamenti incorporati verso note riconosciute come bloccate mostrano un segnaposto con azione di sblocco. La duplicazione e l'estrazione da una nota protetta creano note protette con chiavi nuove. L'accesso alle revisioni e agli allegati privati richiede una sessione valida nell'archivio; le API degli allegati accettano l'identificativo della nota proprietaria.

Rimozione della protezione, esportazione, stampa e condivisione richiedono accesso e una conferma dell'uscita in chiaro. Un allegato privato aperto in un'applicazione esterna passa prima da un'esportazione esplicita con scelta della destinazione. Le copie pubbliche di allegati condivisi rimangono soltanto quando servono ancora a note o revisioni non protette.

## Pulizia e backup

La conversione imposta un marcatore persistente di manutenzione. La pulizia ricostruisce gli indici, usa la cancellazione sicura FTS/SQLite, tronca il WAL e ricompatta il database. Il marcatore viene rimosso soltanto al completamento. In caso di interruzione, la manutenzione viene ripresa all'inizializzazione, prima del backup, tramite **Riprova pulizia archivio** nelle impostazioni o in background dopo un salvataggio riuscito (al più ogni 5 minuti). Non viene eseguita dentro il salvataggio di una nota: un suo errore non fa fallire il salvataggio né blocca il cambio di nota o la chiusura. Le cache di allegati gestite dall'app vengono eliminate coordinandosi con la loro scrittura.

I backup includono dati cifrati, involucri della password e del recupero, sigillo di integrità e preferenze. Il ripristino accetta gli archivi con schema 1, 2 e 3 e li migra; conserva una copia dell'archivio sostituito, invalida Hello e rende la copia ripristinata il nuovo riferimento dell'anchor. Backup, file originali importati, esportazioni e altre copie esterne creati in precedenza non vengono modificati dalla protezione. La manutenzione riguarda i file gestiti dall'app, non snapshot del sistema, copie di terzi o la cancellazione fisica garantita dei supporti. Le stringhe .NET e gli oggetti grafici vengono rimossi dalle strutture attive e compattati al blocco (vedi sopra); oltre a questo il loro rilascio fisico resta gestito dal runtime e da Windows.

## Verifiche

**Coperto dai test automatici** (`tests/MarkdownMkII.Storage.Tests`: `NoteProtectionTests`, `IntegrityTests`, `CredentialsAndBulkTests`; `tests/MarkdownMkII.App.Logic.Tests`: `IntegrityAnchorStoreTests`):

- Password corretta ed errata, parametri KDF, manomissione di ciphertext e AAD, cambio password, recupero e rotazione della chiave principale.
- Integrità:
  - riga riportata indietro, anche con trigger eliminato;
  - impronta alterata, revisione rimossa, chiave del sigillo tolta;
  - intero file sostituito con una copia precedente (rilevato solo con l'anchor);
  - anchor non scritto e modifiche esterne che non bloccano le note in chiaro;
  - ripristino da backup e reset;
  - round-trip DPAPI e registro dell'anchor.
- Migrazione e ripristino degli schemi 1, 2 e 3, e coerenza del backup.
- Assenza dei marcatori di prova in database, FTS, WAL e pagine libere dopo la pulizia.
- Allegati privati e condivisi, duplicazione con chiavi distinte, salvataggio fallito con bozza sigillata, pulizia interrotta e ripresa.

**Da provare a mano** nell'app avviata da Visual Studio con F5, perché i test usano un adattatore simulato e non l'hardware:

- Windows Hello: gesto richiesto a ogni sblocco, annullamento, hardware non supportato, ripristino su un altro PC.
- Blocco per inattività, blocco e sospensione di Windows durante una modifica.
- Dialogo **Controllo di integrità**, per esempio sostituendo `notes.db` con una copia precedente ad app chiusa.
- Rendering degli allegati privati, stampa, condivisione ed esportazione con conferma.
- Interfaccia in italiano e inglese a diverse larghezze e scale del testo.

## Riferimenti

- [API .NET AesGcm](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm?view=net-10.0).
- [OWASP: derivazione delle password](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).
- [Microsoft: verifica del consenso e protezione crittografica](https://devblogs.microsoft.com/oldnewthing/20240924-00/?p=110308).
- [Microsoft: proprietà delle chiavi CNG](https://learn.microsoft.com/en-us/windows/win32/seccng/key-storage-property-identifiers).
- [SQLite: cancellazione sicura FTS5](https://www.sqlite.org/fts5.html#the_secure_delete_configuration_option).
