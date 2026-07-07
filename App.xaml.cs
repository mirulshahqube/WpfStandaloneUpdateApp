using System.Windows;
using Velopack;

namespace WpfStandaloneUpdateApp
{
    public partial class App : Application
    {
        public App()
        {
            // Must run before anything else - handles Velopack's install-time hooks
            // (e.g. creating shortcuts) and lets Velopack recognize this as a managed install.
            VelopackApp.Build().Run();
        }
    }
}
