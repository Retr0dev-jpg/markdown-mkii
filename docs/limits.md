# Known limits

What the app deliberately does not do, or does only in part. Extending any of these is a new feature, not a bug fix.

## Content and preview

| Area | Current behavior |
| --- | --- |
| YAML front matter | Top-level keys, block lists, and multiline values. Edits touch only the affected line. This is not a full YAML interpreter: nested maps do not become fields. |
| Diagrams | Mermaid, PlantUML, and DOT have local parsers and drawing for a subset of the syntax. Some diagram types use a generic representation; anything unrecognized stays a code block. |
| Math | Formulas are converted to Unicode symbols. There is no full TeX engine. |
| Embedded notes | At most 4 levels and 32 embeds per note; notes longer than 200,000 characters are not embedded. Cycles and limits show a placeholder. |
| Large documents | At 1,000,000 characters or 10,000 lines, highlighting is reduced. Parsing and preview can still take time. |
| Periodic notes | Commands exist to create daily, weekly, monthly, quarterly, and yearly notes, but there is no calendar view. |

## Archive

- One note is open at a time; there are no tabs.
- Folders are not watched. Markdown files are imported on request, and the originals are not modified.
- There is no sync between devices. Opening the same archive from more than one PC at the same time is not supported.

## Security

The limits of protection (memory, the paging file, and restoring the whole archive by someone who controls the Windows account) are described in [protected notes](protected-notes.md).

## Platform

- Windows only, x64. x86 and ARM64 publish profiles exist but are not tested.
- The published release is the unpackaged build (zip). The MSIX package requires a signing certificate, which is not included in the project.
