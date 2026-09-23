using MarkdownMkII.Core.Text;
using MarkdownMkII.Services.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    private FindReplaceOptions CurrentFindOptions()
    {
        var inSelection = FindInSelectionBox.IsChecked == true && findRangeLength >= 0;
        return new FindReplaceOptions
        {
            MatchCase = MatchCaseBox.IsChecked == true,
            WholeWord = WholeWordBox.IsChecked == true,
            UseRegex = RegexBox.IsChecked == true,
            WrapAround = !inSelection,
            RangeStart = inSelection ? findRangeStart : 0,
            RangeLength = inSelection ? findRangeLength : -1
        };
    }

    private void OnFindInSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (FindInSelectionBox.IsChecked == true)
        {
            var (start, length) = GetSelection();
            findRangeStart = start;
            findRangeLength = length;
        }
        else
        {
            findRangeLength = -1;
        }

        UpdateFindCount();
    }

    private void UpdateFindCount()
    {
        if (isApplyingText) return;
        if (ViewModel.CurrentNote is { } tab) tab.FindQuery = FindBox.Text;
        if (ViewModel.CurrentNote is null || string.IsNullOrEmpty(FindBox.Text))
        {
            FindCount.Text = string.Empty;
            ApplyHighlight();
            return;
        }

        var count = FindReplace.FindAll(ViewModel.CurrentNote.Text, FindBox.Text, CurrentFindOptions()).Count;
        FindCount.Text = Strings.Format(Strings.DefaultMap, "FindCount", count);
        ApplyHighlight();
    }

    private void OnFindPrevious(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentNote is null)
        {
            return;
        }

        var (start, _) = GetSelection();
        var match = FindReplace.FindPrevious(ViewModel.CurrentNote.Text, FindBox.Text, start, CurrentFindOptions());
        if (match is { } found)
        {
            SetSelection(found.Start, found.Length);
            FocusEditor();
        }

        UpdateFindCount();
    }

    private void OnFindNext(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentNote is null)
        {
            return;
        }

        var (start, length) = GetSelection();
        var from = length == 0 ? start : start + length;
        var match = FindReplace.FindNext(ViewModel.CurrentNote.Text, FindBox.Text, from, CurrentFindOptions());
        if (match is { } found)
        {
            SetSelection(found.Start, found.Length);
            FocusEditor();
        }

        UpdateFindCount();
    }

    private void OnReplace(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentNote is null)
        {
            return;
        }

        var (start, length) = GetSelection();
        if (length == 0)
        {
            OnFindNext(sender, e);
            return;
        }

        var result = FindReplace.ReplaceMatch(
            ViewModel.CurrentNote.Text,
            FindBox.Text,
            ReplaceBox.Text,
            new FindMatch(start, length),
            CurrentFindOptions());
        if (result is not { } edit)
        {
            OnFindNext(sender, e);
            return;
        }
        ViewModel.CurrentNote.ApplyEdit(edit);
        if (FindInSelectionBox.IsChecked == true) findRangeLength += edit.SelectionLength - length;
        ApplyText(edit.Text, edit.SelectionStart, edit.SelectionLength);
        UpdateFindCount();
    }

    private void OnReplaceAll(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentNote is null)
        {
            return;
        }

        var original = ViewModel.CurrentNote.Text;
        var (text, count) = FindReplace.ReplaceAll(original, FindBox.Text, ReplaceBox.Text, CurrentFindOptions());
        if (count == 0 || text == original) return;
        ViewModel.CurrentNote.ApplyEdit(new EditResult(text, 0, 0));
        if (FindInSelectionBox.IsChecked == true) findRangeLength += text.Length - original.Length;
        ApplyText(text, 0, 0);
        UpdateFindCount();
    }

    private void OnFindClose(object sender, RoutedEventArgs e)
    {
        FindBar.Visibility = Visibility.Collapsed;
        if (ViewModel.CurrentNote is not null)
        {
            ViewModel.CurrentNote.FindBarOpen = false;
        }

        ApplyHighlight();
    }

    private void OnFindKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            OnFindNext(sender, e);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            OnFindClose(sender, e);
            e.Handled = true;
        }
    }
}
