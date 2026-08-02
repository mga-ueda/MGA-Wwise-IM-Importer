using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        AppStorage.Initialize();
        Application.Run(new Form1());
    }
}
