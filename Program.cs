using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var singleInstance = SingleInstanceGuard.TryAcquire();
        if (singleInstance is null)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        AppStorage.Initialize();
        Application.Run(new Form1());
    }
}
