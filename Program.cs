using System.Text;
using System.Windows;
using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter;

static class Program
{
    [STAThread]
    static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var singleInstance = SingleInstanceGuard.TryAcquire();
        if (singleInstance is null)
        {
            return;
        }

        AppStorage.Initialize();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
