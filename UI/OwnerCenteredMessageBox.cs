using System.Windows;

namespace MgaWwiseIMImporter.UI;

/// <summary>オーナー中央のメッセージボックス（WPF）。</summary>
internal static class OwnerCenteredMessageBox
{
    public static MessageBoxResult Show(
        Window? owner,
        string text,
        string caption,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        if (owner is null)
        {
            return MessageBox.Show(text, caption, buttons, icon, defaultResult);
        }

        return MessageBox.Show(owner, text, caption, buttons, icon, defaultResult);
    }
}
