using System.Windows;
using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        UiColors.Load();
        AppFonts.EnsureRegistered();
        base.OnStartup(e);
    }
}
