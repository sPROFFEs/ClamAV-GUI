using System.Configuration;
using System.Data;
using System.Windows;
using ClamAVGui.ViewModels;

namespace ClamAVGui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            var mainViewModel = (MainViewModel)mainWindow.DataContext;

            if (e.Args.Length > 1 && e.Args[0] == "-scan")
            {
                var path = e.Args[1];
                mainViewModel.ScanPathFromCommandLine(path);
            }

            mainWindow.Show();
        }
    }

}
