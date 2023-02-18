using ACDCs.Extension.Magnetic;

namespace MagneticTool;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new MagneticRawPage();
    }
}