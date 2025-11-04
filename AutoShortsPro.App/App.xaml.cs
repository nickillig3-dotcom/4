using System.Windows;
using AutoShortsPro.App.Services;

namespace AutoShortsPro.App
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args != null && e.Args.Length > 0)
            {
                int code = CliProcessor.Run(e.Args);
                Shutdown(code);
                return;
            }

            var w = new MainWindow();
            w.Show();
        }
    }
}

