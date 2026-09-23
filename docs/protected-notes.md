# Protected notes

How encrypted notes work: use, keys, saving, lock, integrity, memory, backup, and checks. The code is in `src/MarkdownMkII.Storage` (archive and encryption) and in `src/MarkdownMkII.App/Services/NoteSecurity.cs` (dialogs, lock, and unlock).

## Use

From a note's menu, **Protect note** configures protection when needed and converts that note. The single password must contain at least 12 characters, of which at least 5 are distinct (`PasswordPolicy`, enforced both by the dialogs and by the archive); confirmation, paste, and a passphrase are available. The recovery code must be downloaded and kept apart from the archive. Confirmation is enabled only after the file has been written successfully; the prepared credentials are activated only when you confirm. Setup, recovery, and renewal follow the same flow. Migration does not protect any existing note automatically.

**Settings → Security** holds the password, Windows Hello, recovery, visibility, the idle interval, and manual lock. Unlock applies to every protected note. The default interval is 5 minutes, chosen from 1, 5, 15, and 30. Locking or suspending Windows ends the session; merely switching applications does not end it immediately.

The title and organization are public by default. **Hide all details** removes the public copies of title, category, tags, color, and favorite for protected notes. While locked, those notes are labeled "Protected note". The text preview is not stored in plaintext. Unlock makes the data available in memory, without converting the notes on disk.

Windows Hello uses the gesture configured in Windows, including the Windows PIN when the system offers it. The app has no separate PIN. The password and the recovery code are still required to open the archive from another device. Cancelling Hello does not produce repeated prompts.

Changing the password, recovering, and renewing the code rotate the master key: note keys are re-encrypted with a new key, the password and recovery wrappers and the verifier are rebuilt, then the WAL and free pages are cleaned (checkpoint and VACUUM). Previous passwords and codes therefore open none of the current data, not even from a copy of the main file alone. So that rotation can also happen during a plain password change, the recovery secret is stored encrypted with the master key (`Protection.RecoverySecret`); it grants nothing beyond the key that decrypts it. In archives created before this wrapping existed, a password change only runs the cleanup until the code is renewed or used. After every rotation, Windows Hello, which had encrypted the previous key, is turned off and must be turned on again.

Unlock waits a growing interval after each wrong attempt (half a second per failure, up to 10 seconds). Idle tracking counts keystrokes, clicks, wheel movement, and pointer movement in the window, input in dialogs while the app is in the foreground, and pauses while a file picker is open. Copying the text of a protected note to the clipboard (copy commands and Ctrl+C/Ctrl+X) asks for the same confirmation as plaintext export, print, and share, once per note and session. With details hidden, a link inserted toward a protected note uses the label "Protected note" instead of the title. The recovery file contains only the archive, the date, the code, and instructions, with no titles and no note identifiers.

## Archive and keys

`NoteDatabase.Protection.cs`, `NoteDatabase.ProtectedNotes.cs`, and `NoteProtection.cs` implement the `INoteProtection` contract and the SQLite 1 → 2 migration; `NoteDatabase.Integrity.cs` adds the integrity seal (schema 3, below).

| Item | Persistence |
| --- | --- |
| Content and revisions | AES-256-GCM with a distinct random key per note |
| Metadata, outgoing links, and attachment names | Encrypted records of the owning note |
| Attachments | A private scope per note; an identifier derived with HMAC so content can be deduplicated without publishing the content hash |
| Note key | Encrypted with the archive's random master key |
| Master key | Copies encrypted with the key derived from the password and with the recovery code |
| Password | Not stored; PBKDF2-HMAC-SHA256, 600,000 iterations, a random 32-byte salt |
| Recovery code | 32 random bytes; the database stores only the master key wrapped with this code |

Every AES-GCM record has a format version, a random 96-bit nonce, and a 128-bit tag. Authenticated data includes the archive, the note, the item type, and, where relevant, the revision or attachment identifier. Changing the password, recovering, and renewing the code replace the master key (see above) and turn off the local Hello enrollment, which had encrypted the previous key.

The `WindowsHelloProtection` adapter requires a non-exportable RSA key from the **Microsoft Passport Key Storage Provider**, decryption usage, a mandatory authentication policy, and RSA-OAEP-SHA256. Activation proves the full cryptographic path before storing the enrollment. If the provider rejects the algorithm or the policy, activation fails with no fallback to another provider. The `UserConsentVerifier` availability check only decides whether to show the option: it does not unlock data.

`windows-hello.json` contains identifiers and the master key encrypted for that device, never the key in the clear. It is not included in backups. `settings.json` contains no credentials. Restoring preferences alone does not change protection.

## Concurrency, saving, and lock

SQLite operations and key derivation run off the UI thread. Reads, writes, conversions, and backups share the archive's serialization, so there are no intermediate reads during a conversion. Data changes use transactions and WAL.

Autosave keeps the existing deadlines. For a protected note, the text is encrypted before the write. On lock, the editor surface is hidden and made read-only before the save finishes. The text, undo/redo, preview, panels, and session indexes are cleared; keys and mutable temporary buffers are wiped. Session tokens and versions stop earlier operations from repopulating the UI.

Memory protection (`SecretMemory`, `ProcessHardening`):

- **Keys:** the master key, note keys, derived keys, and recovery secrets live in pinned heap buffers. The GC does not move them, so wiping leaves no orphaned copies. Keys held for the session are also locked in RAM with `VirtualLock`, where Windows allows it, and do not land in the paging file.
- **Decrypted text:** it is a .NET string and cannot be wiped; `RichEditBox` and Markdig accept only `string`. To reduce copies, the native `RichEditBox` history is disabled (`UndoLimit = 0`), because undo/redo uses only `TextUndoHistory`. After every lock, an aggressive GC with compaction, including the Large Object Heap, removes the released text and returns free pages to Windows; Windows zeroes them before reuse.
- **Crash:** at startup `WerSetFlags(WER_FAULT_REPORTING_FLAG_NOHEAP)` excludes the heap from Windows Error Reporting, so dumps do not contain note text.
- **Limits:** while a note is open, its text is in memory in the clear, and Windows can write parts of it to the paging file or the hibernation file. **Settings → Security → Disk encryption** recommends BitLocker or device encryption and opens the related Windows page. A process running as the same user can still read the app's memory while the note is open.

If saving fails, the latest edit is sealed with the note key before the session closes, and is also written to disk (`sealed-draft.json`: AES-GCM ciphertext bound to the note and the version only, no key). The draft therefore survives a crash or a forced close; a voluntary exit is allowed once the draft is on disk. After unlock the edit returns to the editor; the file is deleted only after the restored text has been saved. If the persisted version changed in the meantime, a new protected note is created with "(recovered edit)" in the name, and both versions are kept. If the note was deleted, the draft can no longer be decrypted and is discarded with a warning. Before clearing the editor, lock also reads from the control any text not yet synchronized, including an IME composition in progress.

Lock always runs to completion: even if sealing the draft fails, in-memory keys and the catalog are wiped, and the interface becomes responsive again as soon as no key remains in memory. Unsealed text stays only in the editor model, hidden and unsaved, and reappears on the next unlock. Lock also clears the sidebar search and the attachment cache.

## Search, resources, and data leaving the app

Encrypted texts (bodies, revisions, metadata, names, drafts) and protected attachments use format 2: an encrypted length and padding to 256-byte blocks, with the format bound into the authenticated data, so the size does not reveal the exact length. Format 1 remains readable. The note body is also bound to the row version (`body:{version}`): an older ciphertext copied over the current body is rejected. A row that holds ciphertext but is not marked protected is rejected instead of showing empty content. A damaged record does not block unlock: the other notes open, and the user is warned. An export stops writing if the session is locked in the meantime and removes partial attachments.

## Integrity: rolled-back rows or archive

AES-GCM authenticates every record, but an older, intact record is still valid. Schema 3 adds a seal over the whole protected archive:

- The `Integrity` table holds a SHA-256 fingerprint of every encrypted record: the note body and metadata, every revision, and every private attachment together with its name. Triggers on `Notes`, `Revisions`, and `Attachments` only enqueue the touched rows in `IntegrityPending`. They are pure SQL, so external tools that edit plaintext notes keep working. They are recreated on every open, so deleting or altering them does not stop the update.
- Before every commit (`Commit` in `NoteDatabase.Integrity.cs`, used by every transaction) the queued fingerprints are recomputed and the set is signed with HMAC-SHA256 together with the archive identity and a rising generation number. The seal key is random, encrypted with the master key (`Protection.IntegrityKey`), and re-encrypted on every credential rotation. Without the password, nobody can recompute the seal. While locked, the app cannot modify protected records. Rows queued by external tools therefore stay unsealed, do not block saves of plaintext notes, and are reported at unlock. Unlock clears the queue, because detection compares fingerprints with the real records.
- After the commit, the generation and the seal are copied to an **external anchor** (`IIntegrityAnchor`, implemented by `IntegrityAnchorStore`). It lives in the user registry (`HKCU\Software\Markdown MkII\Integrity`), outside the archive folder, protected with DPAPI and authenticated with the seal key.
- At unlock (password, Hello, or recovery) the seal, every fingerprint against the real records, and the anchor are checked. The result is `NoteDatabase.IntegrityReport`:
  - **Modified notes:** rows rolled back, replaced, added, or removed, with titles in the dialog.
  - **Seal cannot be verified:** the seal or the fingerprints were altered, or the seal key was removed. The "sealed" verifier records that a seal used to exist.
  - **Archive rolled back:** the file's generation is lower than the anchor's, or equal with a different seal.
- The **Protected notes integrity check** dialog offers accepting the current state (`AcceptIntegrityAsync` rebuilds fingerprints and the seal and advances past the anchor's generation) or locking the notes, for example to restore a backup. Until a choice is made, writes to protected notes are rejected with `ArchiveIntegrityException`. Reads and plaintext notes stay available.
- Legitimate cases:
  - A file newer than the anchor (the anchor write was missed after a crash) is accepted and the anchor is updated.
  - A missing anchor is recreated.
  - Restoring a backup from the app forgets the anchor: the restored copy becomes the new reference.
  - A full reset deletes every anchor.
  - Older archives migrate to schema 3 with the fingerprints computed. On the first unlock they are sealed as they are (trust on first use).

Stated limit: someone who controls the Windows account can roll the archive and the registry back together. The anchor protects against someone who has access only to the archive file, for example a synced folder, a copy on another PC, or an external disk. Tampering done while the app is unlocked and in use is also outside the model: anyone who can do that can read the process memory directly. A counter that truly cannot be rolled back would require the TPM or a server.

Persistent search indexes only public content and public metadata. A second, in-memory SQLite index holds protected-note metadata while unlocked; it is cleared on lock. Protected-note content stays searchable only in the open note. Catalog queries stay paginated.

Protected images are read from memory. Embedded links to notes recognized as locked show a placeholder with an unlock action. Duplicating and extracting from a protected note create protected notes with new keys. Access to revisions and private attachments requires a valid archive session; attachment APIs accept the owning note's identifier.

Removing protection, export, print, and share require access and a confirmation of the plaintext exit. A private attachment opened in an external application first goes through an explicit export with a chosen destination. Public copies of shared attachments remain only while unprotected notes or revisions still need them.

## Cleanup and backup

Conversion sets a persistent maintenance marker. Cleanup rebuilds the indexes, uses FTS/SQLite secure delete, truncates the WAL, and compacts the database. The marker is removed only on completion. If it is interrupted, maintenance resumes at initialization, before backup, through **Retry archive cleanup** in settings, or in the background after a successful save (at most every 5 minutes). It does not run inside a note save: a failure there does not fail the save, and does not block switching notes or closing. Attachment caches managed by the app are deleted in coordination with their writes.

Backups include encrypted data, the password and recovery wrappers, the integrity seal, and preferences. Restore accepts archives with schema 1, 2, and 3 and migrates them; it keeps a copy of the replaced archive, invalidates Hello, and makes the restored copy the new anchor reference. Backups, imported original files, exports, and other external copies created earlier are not modified by protection. Maintenance covers files the app manages, not system snapshots, third-party copies, or guaranteed physical erasure of the media. .NET strings and graphics objects are removed from live structures and compacted on lock (see above); beyond that, their physical release stays with the runtime and Windows.

## Checks

**Covered by automated tests** (`tests/MarkdownMkII.Storage.Tests`: `NoteProtectionTests`, `IntegrityTests`, `CredentialsAndBulkTests`; `tests/MarkdownMkII.App.Logic.Tests`: `IntegrityAnchorStoreTests`):

- Correct and wrong passwords, KDF parameters, ciphertext and AAD tampering, password change, recovery, and master-key rotation.
- Integrity:
  - a row rolled back, including with the trigger deleted;
  - an altered fingerprint, a removed revision, the seal key removed;
  - the whole file replaced with an earlier copy (detected only with the anchor);
  - an unwritten anchor, and external edits that do not block plaintext notes;
  - restore from backup, and reset;
  - DPAPI and registry round-trip of the anchor.
- Migration and restore of schemas 1, 2, and 3, and backup consistency.
- Absence of the trial markers in the database, FTS, WAL, and free pages after cleanup.
- Private and shared attachments, duplication with distinct keys, a failed save with a sealed draft, and cleanup interrupted and resumed.

**To try by hand** in the app started from Visual Studio with F5, because the tests use a simulated adapter and not the hardware:

- Windows Hello: a gesture required on every unlock, cancellation, unsupported hardware, restore on another PC.
- Idle lock, and locking or suspending Windows during an edit.
- The **Protected notes integrity check** dialog, for example by replacing `notes.db` with an earlier copy while the app is closed.
- Rendering of private attachments, print, share, and export with confirmation.
- The interface in Italian and English at different widths and text scales.

## References

- [.NET AesGcm API](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm?view=net-10.0).
- [OWASP: password storage](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).
- [Microsoft: consent verification and cryptographic protection](https://devblogs.microsoft.com/oldnewthing/20240924-00/?p=110308).
- [Microsoft: CNG key properties](https://learn.microsoft.com/en-us/windows/win32/seccng/key-storage-property-identifiers).
- [SQLite: FTS5 secure delete](https://www.sqlite.org/fts5.html#the_secure_delete_configuration_option).
