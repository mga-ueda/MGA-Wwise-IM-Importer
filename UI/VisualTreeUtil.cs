using System.Windows;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

internal static class VisualTreeUtil
{
    public static T? FindVisualDescendant<T>(DependencyObject root, Func<T, bool>? match = null)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed && (match is null || match(typed)))
            {
                return typed;
            }

            if (FindVisualDescendant(child, match) is T found)
            {
                return found;
            }
        }

        return null;
    }
}
