# Limiti noti

Cosa l'app volutamente non fa, o fa solo in parte. Ampliare uno di questi punti è una nuova funzione, non una correzione.

## Contenuto e anteprima

| Area | Comportamento attuale |
| --- | --- |
| Front matter YAML | Chiavi di primo livello, elenchi a blocchi e valori su più righe. Le modifiche toccano solo la riga interessata. Non è un interprete YAML completo: le mappe annidate non diventano campi. |
| Diagrammi | Mermaid, PlantUML e DOT hanno parser e disegno locali per un sottoinsieme della sintassi. Alcuni tipi di diagramma usano una rappresentazione generica; ciò che non viene riconosciuto resta un blocco di codice. |
| Matematica | Le formule vengono convertite in simboli Unicode. Non c'è un motore TeX completo. |
| Note incorporate | Al massimo 4 livelli e 32 incorporamenti per nota; le note oltre 200.000 caratteri non vengono incorporate. Cicli e limiti mostrano un segnaposto. |
| Documenti grandi | Da 1.000.000 caratteri o 10.000 righe l'evidenziazione viene ridotta. Parsing e anteprima possono comunque richiedere tempo. |
| Note periodiche | Esistono comandi per creare note giornaliere, settimanali, mensili, trimestrali e annuali, ma non c'è una vista calendario. |

## Archivio

- Una nota aperta alla volta; non ci sono schede.
- Le cartelle non sono monitorate. I file Markdown si importano su richiesta e gli originali non vengono modificati.
- Non c'è sincronizzazione fra dispositivi. Aprire lo stesso archivio da più PC contemporaneamente non è supportato.

## Sicurezza

I limiti della protezione (memoria, file di paging, ripristino dell'intero archivio da parte di chi controlla l'account Windows) sono descritti in [note protette](note-protette.md).

## Piattaforma

- Solo Windows, x64. I profili di pubblicazione x86 e ARM64 esistono ma non vengono provati.
- La release pubblicata è la versione non pacchettizzata (zip). Il pacchetto MSIX richiede un certificato di firma, che non è incluso nel progetto.
