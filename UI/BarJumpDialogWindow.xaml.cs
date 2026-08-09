using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 小節番号を入力してジャンプする簡易ダイアログ（Form1 BarJumpDialogForm 相当）。
/// Enter で確定、Esc でキャンセル。枠なし・影付き。
/// </summary>
internal partial class BarJumpDialogWindow : Window
{
    public int? BarNumber { get; private set; }

    public BarJumpDialogWindow(int? initialBarNumber = null)
    {
        InitializeComponent();
        WindowIconHelper.Apply(this);

        if (initialBarNumber is int initial && initial > 0)
        {
            BarNumberBox.Text = initial.ToString(CultureInfo.InvariantCulture);
        }

        SourceInitialized += (_, _) =>
        {
            if (Owner is { Topmost: true })
            {
                Topmost = true;
            }
        };

        Loaded += (_, _) =>
        {
            BarNumberBox.Focus();
            BarNumberBox.SelectAll();
        };
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            AcceptIfValid();
            e.Handled = true;
        }
    }

    private void BarNumberBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AcceptIfValid();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
            return;
        }

        // スペース等の編集キーは受け付けない（数字・移動・削除のみ）
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private void BarNumberBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // 数字以外は拒否（以前は判定が逆で数字が一切入らなかった）
        e.Handled = e.Text.Length == 0 || e.Text.Any(ch => !char.IsAsciiDigit(ch));
    }

    private void BarNumberBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (text.Length == 0 || text.Any(ch => !char.IsAsciiDigit(ch)))
        {
            e.CancelCommand();
        }
    }

    private void AcceptIfValid()
    {
        var text = BarNumberBox.Text.Trim();
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bar)
            || bar < 1)
        {
            BarNumberBox.Focus();
            BarNumberBox.SelectAll();
            return;
        }

        BarNumber = bar;
        DialogResult = true;
        Close();
    }
}
