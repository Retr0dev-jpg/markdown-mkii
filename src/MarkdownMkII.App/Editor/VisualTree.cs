using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace MarkdownMkII.Editor;

public static class VisualTree
{
    public static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null)
        {
            return null;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    public static T? FindNamedDescendant<T>(DependencyObject? root, string name) where T : FrameworkElement
    {
        if (root is null)
        {
            return null;
        }

        if (root is T self && string.Equals(self.Name, name, StringComparison.Ordinal))
        {
            return self;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var nested = FindNamedDescendant<T>(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
