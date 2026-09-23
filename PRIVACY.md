# Privacy policy

Last updated: September 23, 2026

Markdown MkII is a note app that works on your PC. It has no account, no cloud service, no advertising, and no analytics or telemetry. The developer does not receive or store any of your data.

## Data stored on your PC

Everything the app keeps stays in its data folder on your device (see [where the data lives](README.md#where-the-data-lives)):

- **Notes, attachments, history, and categories and tags**, in a local SQLite archive (`notes.db`). Protected notes are encrypted with AES-256-GCM using your password; other notes are stored as plain text.
- **Preferences** (`settings.json`).
- **A diagnostic log** (`diagnostics.log`), with technical error messages only, never note text.
- **Windows Hello unlock**, if you turn it on: a key protected by Windows, stored on this device only.
- **An integrity marker** in your Windows user registry, which lets the app detect an archive replaced with an older copy. It contains a version number and a signature, not your notes.

Backups and exports are written only where you choose. **Settings → Data → Reset the app completely** deletes all of the data above. Uninstalling the Microsoft Store version also makes Windows delete its data folder, notes included; uninstalling the version installed from GitHub leaves your notes in place.

## When the app connects to the internet

The app never sends your notes, titles, passwords, or files anywhere. It connects to the internet only in these cases:

- **Update check** (copies installed from GitHub only): at startup, if **About → Updates → Check at startup** is on, the app asks GitHub for the latest release and downloads it. GitHub receives the usual request data, such as your IP address. The Microsoft Store version does not do this; the Store handles its updates.
- **Images from the web in your notes:** if a note contains an image with a web address, the preview loads it from that website, like a browser would.
- **Links you open:** web links in your notes and the **GitHub**, **License**, and **Report a problem** buttons open in your browser.

## Reporting a problem

**About → Report a problem** opens a GitHub issue in your browser with a technical report already filled in: app and Windows versions, archive statistics (counts and sizes), and recent error messages. It does not contain note text, titles, or passwords, and hides your Windows user name. Nothing is sent until you review it and submit the issue yourself, and it is then public on GitHub under GitHub's [privacy statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).

## Microsoft Store and Windows

If you install the app from the Microsoft Store, Microsoft may collect data about the installation and crashes under the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement). The developer only sees the aggregated reports that Partner Center provides.

## Contact

For questions about this policy, open an issue on [GitHub](https://github.com/Retr0dev-jpg/markdown-mkii/issues). For security problems, follow [SECURITY.md](SECURITY.md).
