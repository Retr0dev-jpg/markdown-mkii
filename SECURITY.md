# Security policy

Markdown MkII keeps notes in a local archive and can encrypt protected notes. Problems that could expose protected notes or bypass their protection are handled privately.

## Supported versions

Only the latest release ([Latest](https://github.com/Retr0dev-jpg/markdown-mkii/releases/latest)) receives security fixes. Please check that the problem still occurs there before reporting it.

## Reporting a vulnerability

**Do not open a public issue**, and do not use **About → Report a problem** in the app, because it creates a public issue.

Report it privately through GitHub instead: open [Report a vulnerability](https://github.com/Retr0dev-jpg/markdown-mkii/security/advisories/new) (the **Security** tab of the repository).

Please include:

- the app version (shown on the **About** page) and your Windows version;
- what an attacker can do, and what access they need (for example: a copy of `notes.db`, access to your Windows account, physical access to the PC);
- the steps to reproduce it, ideally with a test archive rather than your real notes.

Never send your real notes, passwords, or recovery codes.

You will get a first reply within 7 days. Once a fix is available, it is published in a new release and the report is disclosed, with credit to you if you wish.

## What counts as a vulnerability

In scope:

- reading the content, titles, or attachments of protected notes without the password, the recovery code, or Windows Hello;
- changing or rolling back protected notes without the integrity check noticing, beyond the limits described below;
- decrypted text or key material written to disk or to logs;
- a password or recovery code that keeps working after it was changed;
- data from outside the app (for example imported Markdown) that makes the app run code or access files it should not.

Known limits, described in [docs/protected-notes.md](docs/protected-notes.md), are not vulnerabilities on their own:

- anyone who controls your Windows account while a note is open can read it in memory;
- anyone who controls your Windows account can roll back both the archive and the integrity anchor together;
- note text can reach the page file or the hibernation file unless the disk is encrypted (BitLocker or device encryption);
- titles, categories, and tags of protected notes are visible unless **Hide all details** is on;
- unprotected notes are stored in plain text by design.
