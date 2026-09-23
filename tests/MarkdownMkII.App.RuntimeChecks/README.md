# Check the deployed resource catalog in Visual Studio

The portable test suites cannot exercise MRT Core, the generated PRI, package
identity, or WinRT first-chance exceptions. This small diagnostic assembly runs
inside the actual WinUI process started by Visual Studio; it is not shipped with
the application and is not a `dotnet test` project.

1. Build it with `dotnet build tests/MarkdownMkII.App.RuntimeChecks`.
2. Select **MarkdownMkII.App**, **Debug**, **x64**, and the MSIX launch profile in
   Visual Studio. Start with **F5**.
3. Add a temporary breakpoint in `Strings.Resolve`, after the resource manager
   and context are initialized. Open a page whose strings have not been cached
   yet (for example, Information), or restart the app to reach the breakpoint.
4. Remove that breakpoint while remaining paused. Breakpoints inside the resolver
   interrupt the diagnostic invocation too. Select the managed UI-thread frame.
5. In the Immediate window, load the assembly using the absolute repository path:

   ```csharp
   System.Reflection.Assembly.LoadFrom(@"C:\path\to\markdown-mkii\tests\MarkdownMkII.App.RuntimeChecks\bin\Debug\net10.0\MarkdownMkII.App.RuntimeChecks.dll")
   ```

   Then evaluate:

   ```csharp
   ?MarkdownMkII.RuntimeChecks.LocalizationChecks.Run(@"C:\path\to\markdown-mkii")
   ```

The result must start with `PASS`, report every entry in the current language's
RESW catalog, **zero first-chance exceptions**, and the MSIX package identity.
The check bypasses the string cache, compares translations with the source RESW,
and probes a missing key and a missing map. It unsubscribes its exception observer
even if validation fails. No note or setting is changed.

Repeat after changing the app language and restarting to validate another
language. Also exercise navigation, menus, editing, save, preview, and normal
window closure with COMException's **Break when thrown** enabled. Restore any
temporary debugger setting after verification.

`scripts/verify.ps1 -IncludeApp` builds both unpackaged and MSIX configurations
and this diagnostic assembly, in addition to running the portable tests. Use
`-AppBuild MSIX` or `-AppBuild Unpackaged` to select just one build. Building is
not a substitute for this in-process check or the Visual Studio UI smoke test.

## Editor context menu regression

Run with F5 in Visual Studio, using a disposable Markdown note. These checks
exercise native pointer, selection, focus, and flyout behavior; portable tests
cannot reproduce the original dismissal/reopening loop.

1. Drag to select part of a sentence, then repeat with a double-click on a word.
   The selection stays visible and no menu opens automatically.
2. Right-click inside the selection. One menu opens beside the pointer and the
   selected text remains highlighted.
3. Click outside the menu. It closes and stays closed. A subsequent ordinary
   click places the caret without opening a menu.
4. Select text and press Shift+F10. The menu opens with keyboard focus. Escape
   closes it and preserves the selection.
5. Open the menu again and choose Comment. Only the selected text is wrapped in
   an HTML comment. Reopen the menu and choose Undo; both text and selection
   return to their previous values. Undo/Redo availability updates between opens.

Verified on 2026-09-14 in the Debug x64 MSIX app started by Visual Studio;
Release x64 MSIX also builds with zero errors and warnings. The old
`ContextMenuOpening` handler opened a new menu during automatic selection
flyouts and light dismissal. The editor now owns one `ContextFlyout` and opts
out of the automatic selection flyout, as supported by
[RichEditBox.SelectionFlyout](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.richeditbox.selectionflyout?view=windows-app-sdk-1.8).
