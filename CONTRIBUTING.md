# Contributing to Markdown MkII

Thanks for your interest. Bug reports, ideas, and pull requests are all welcome.

## Reporting a bug

1. Check the [open issues](https://github.com/Retr0dev-jpg/markdown-mkii/issues) first: the problem may already be known.
2. In the app, open **About → Report a problem**. It opens a new issue with a technical report already filled in: version, Windows, archive state, and recent errors. The report never contains note text, titles, or passwords, and hides your Windows user name.
3. Describe what happened, the steps to reproduce it, and what you expected. Attach screenshots if the problem is visual.

If the app does not start, open an issue by hand and include your Windows version and the last lines of `diagnostics.log` (see [where the data lives](README.md#where-the-data-lives)). Remove any personal information first.

**Security problems** (for example a way to read protected notes without the password) must not go in a public issue: follow [SECURITY.md](SECURITY.md).

## Suggesting a feature

Open an issue that explains the problem you want to solve, not just the solution. Check [docs/limits.md](docs/limits.md) first: it lists what the app deliberately does not do.

## Pull requests

1. Open an issue first for large changes, so we can agree on the approach before you write the code.
2. Set up the project as described in [docs/development.md](docs/development.md).
3. Keep each pull request focused on a single change.
4. Before opening the pull request, make sure that:
   - all tests pass with warnings treated as errors (`./scripts/verify.ps1`, or the `dotnet test ... -warnaserror` commands in [AGENTS.md](AGENTS.md#commands));
   - the app builds (`./scripts/verify.ps1 -IncludeApp`);
   - you added or updated tests for the behavior you changed;
   - every new interface string exists in both `Strings/it/Resources.resw` and `Strings/en-US/Resources.resw`;
   - the relevant page in `docs/` still matches the code;
   - you tried the change in the app with F5 when it affects the interface, the editor, or security.
5. Describe what changes and why, and link the related issue.

The project conventions (encoding, zero warnings, comments, archive and security rules) are in [docs/development.md](docs/development.md#conventions) and [AGENTS.md](AGENTS.md#rules).

## License

Markdown MkII is distributed under the [GNU General Public License v3.0](LICENSE). By submitting a contribution you agree that it is distributed under the same license.
