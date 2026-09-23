# Architecture

How the archive, editor, and interface are organized. For the build and tests see [development](development.md); for encryption see [protected notes](protected-notes.md).

## Data flow

`Views → EditorViewModel / NoteBrowser → NoteArchive → MarkdownMkII.Storage → SQLite`

`MarkdownMkII.Core` stays independent of WinUI and SQLite. It contains parsing, transforms, undo history, and the preview IR. File helpers serve import/export and compatible Markdown formats.

## Archive

`INoteRepository` defines create, read, save, search, and organization. `INoteResources` resolves links and attachments. `NoteDatabase` implements both, with a schema versioned through `user_version`, WAL, foreign keys, and transactions. Provider operations run on worker threads; a semaphore serializes writes. A transient initialization failure can be retried. Transactions that modify data end with `Commit(db, tx)`, which first seals changes to protected records (schema 3, [integrity](protected-notes.md#integrity-rolled-back-rows-or-archive)) and, after the commit, updates the external anchor.

Notes have stable IDs. Title and organization are separate from the content; the text version detects stale writes without invalidating a metadata change. The FTS5 index is updated through triggers. List queries return summaries, not Markdown bodies, in pages of 100 items with a maximum of 200.

Tables associate categories, tags, and links with notes. Attachments are BLOBs identified by a SHA-256 hash and deduplicated by content. New links use `note://ID` and `attachment://HASH`; title wikilinks are also resolved for imported notes. An ambiguous title requires a choice in the interface.

## Editor and saving

`NoteEditorViewModel` holds the text, the selection, parsing, and `TextUndoHistory`. `EditorViewModel.CurrentNote` is the only open note. `LatestSaveQueue` serializes saves and continues until the written snapshots cover every change that arrived during I/O.

Two timers request a save: after 750 ms of inactivity, and after 5 seconds of continuous typing. Switching notes, closing the editor, and exiting wait for the queue to drain. On a note switch, loading precedes the final save, so text typed during the read is kept as well. An error leaves the buffer modified, shows the error state, retries after 5 seconds (except version conflicts), and blocks a close that would lose the change. `SaveAsync` builds the result from the data just written, without rereading and decrypting the note. Protection maintenance is not part of saving: it resumes at startup, from the security settings, or in the background after a successful save (at most every 5 minutes), and a failure there does not fail the save.

SQLite history keeps at most 30 distinct contents, with a checkpoint each minute and on close. Restoring a version goes through a normal editor edit and can be undone. Text undo/redo stays independent of the RichEditBox native color and formatting changes; that native history is disabled (`UndoLimit = 0`). Typing is grouped by word (contiguous inserts, and consecutive Backspace and Delete); commands stay separate steps. Redo responds to Ctrl+Y and Ctrl+Shift+Z.

Commands apply only the changed range to the RichEditBox (`ReplaceChangedRange`), so scrolling, IME, and layout stay stable; if verification of the resulting text fails, the code falls back to `SetText`. Focus returns to the editor after every command. During an IME composition the text is neither synchronized nor highlighted, and Enter, Tab, and brackets stay with the IME. Shift+Enter (the RichEdit vertical tab) and page breaks are read as line breaks. Highlighting is not applied when the displayed text does not yet match the model. Commands that do not compute a selection carry the previous one through the edit (`MarkdownEditing.MapSelection`) instead of moving the caret to the start.

Automatic pairing (the **Auto-close brackets** setting) wraps a selection in any pairable character; with no selection it closes only `( [ {` and a backtick at a word boundary, and skips a closing bracket that is already there. Paste decides synchronously whether to handle the clipboard: unsupported formats fall back to the native paste. Paste and drag-and-drop insert images into the source note first, then open Markdown files; file-read errors show a message.

Front matter is edited line by line: updating, adding, renaming, sorting, or removing a key leaves comments, multiline values, block lists, quotes, and the body unchanged. Values that YAML would interpret differently are quoted; lists are split while respecting quotes. Fields are set with **Set front matter field…** or with the dedicated commands for id, title, aliases, tags, description, date, status, author, cssclass, language, and type.

## Import, export, and backup

`MarkdownTransfer` inspects the selected paths, presents candidates, and imports copies into the database. `ImportMetadata` reads YAML without instantiating application types; `ExportMetadata` updates only the managed fields and preserves the rest of the front matter. `MarkdownReference` uses parser spans to rewrite links without touching code samples.

Local resources are copied only when selected; import downloads no remote resource. Document origin and hash prevent the same import from being repeated. Warnings report unresolved links and missing attachments. Export turns IDs into relative paths for the notes included in the collection.

`ArchiveBackup` creates a ZIP container with a snapshot taken through `SqliteConnection.BackupDatabase` and JSON preferences. Restore checks the schema, integrity, and associations, keeps a backup made beforehand, and restores the previous state if replacing the preferences fails. The restored copy becomes the new integrity-anchor reference, so the integrity check treats it as the current archive.

`ArchiveReset` (**Settings → Data → Reset the app completely**) asks you to type a confirmation word and offers a backup beforehand; if the backup is cancelled or fails, nothing is deleted. It closes the editor without saving, discards protected drafts, removes the Windows Hello credential (file and key), clears the attachment cache, deletes and recreates the database (`NoteDatabase.ResetAsync`, which also wipes keys and the session catalog), removes integrity anchors from the registry, deletes automatic backups, temporary files, the diagnostic log, and the legacy recovery and history folders, then restores the default preferences while keeping the language.

## Interface and commands

`NoteBrowser` is the only catalog, in the sidebar: debounced search, a section, filters, and a virtualized list. The section (All notes, Favorites, Archived, Trash) is a drop-down distinct from the filters; category and tag are the only filters, shown as removable chips. `ISupportIncrementalLoading` appends summaries in blocks of 100 while scrolling vertically. Changing the section or a filter, locking, and unloading the control cancel earlier requests; an error suspends automatic loading until Retry. Empty states distinguish an empty archive, empty sections, and a search with no results. At startup and after the editor closes, the workspace shows only the prompt to open a note. Import is in the **New note** button menu; collection export is in the section menu (`ArchiveDialogs.ExportCollectionAsync`). `ArchiveDialogs.NoteMenu` is the only builder of the note menu. A `ContextRequested` event handles mouse and keyboard; the three-dot button appears on pointer hover or keyboard focus. In Trash, clicking a note opens the restore menu. `MainWindow.FocusNoteSearch` moves focus to search (command `searchNotes`, Ctrl+Shift+F).

`EditorPalette` is the command registry: id, label, category, scope, availability, and default shortcut. Menus, tooltips, command search, and `ShortcutService` use the same registry. Custom assignments are saved in preferences; conflicts are not overwritten. The dispatcher protects AltGr, typing, modal windows, and shortcut capture; symbols are resolved from the keyboard layout.

The editor bar contains four menus, bold and italic (hidden in narrow windows), **Commands** (search of every command, Ctrl+Shift+P), and closing the editor. Menus are built in `EditorPage.Menus.cs` from `EditorPalette`:

| Menu | Contents |
| --- | --- |
| Note | New, open, rename, duplicate, favorite, category, tags, export and share, protection, lock, history, close |
| Format | Text styles, headings 1–6, lists, quote, comment, transforms |
| Insert | Link, image, wikilink, footnote, table, code, callout, separator, formulas and diagrams, table of contents, front matter |
| View | Editor, preview, side by side, side panel, word wrap, navigation, reading, sections |

The right-click menu contains cut, copy, paste, select all, undo, redo, Format, Insert, and Lines. The Table, List, and Heading groups appear only when the caret is inside that structure (`EditorContext`, checked by `EditorContextTests`); a selection adds **Extract to note**. The menu does not open when selecting text; an outside click and Esc close it without losing the selection.

`EditorPage` separates input, selection, rendering, outline, search, layout, and interop. The preview receives a `NotePreviewContext` with the id and embedded documents resolved from the database; depth, size, and cycles are limited. Asynchronous results are applied only to the note and text that requested them; a request that arrives during an update is run again when that update finishes, so it is not dropped. In archive notes, images and media with a relative path (`./foto.png`, `![[foto.png]]`) become `attachment-name://note/name` and are resolved by name among attachments (`NoteDatabase.FindAttachmentAsync`), protected ones of that note first, then public ones. Ordered lists keep an alphabetic or Roman marker and its delimiter; definition lists show every term that shares a definition; table cells keep every paragraph; an embed whose title does not exist is reported as missing. Synchronized scrolling descends into nested blocks of lists, quotes, and tables. The window's print and share handlers are detached when the page is unloaded.

`SettingsService` serializes preference mutations and writes. At startup, before `InitializeComponent`, `LocalizationService.EffectiveLanguage` always sets an explicit language override. For **Match system** it derives the override from the Windows language list (`GlobalizationPreferences.Languages`: the first supported language, otherwise Italian). The persisted override has to stay explicit, because clearing it does not reliably drop a language chosen in a previous session. Italian and English resources are checked by the parity suites and by resolution checks inside the WinUI process.
