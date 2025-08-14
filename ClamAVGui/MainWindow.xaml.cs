using System.Text;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ClamAVGui.Models;
using ClamAVGui.ViewModels;

namespace ClamAVGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _isExiting;

        public MainWindow()
        {
            InitializeComponent();
        }


        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                MyNotifyIcon.Visibility = Visibility.Visible;
            }
        }

        private void ShowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            MyNotifyIcon.Visibility = Visibility.Collapsed;
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _isExiting = true;
            Close();
        }

        private void MyNotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            MyNotifyIcon.Visibility = Visibility.Collapsed;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExiting)
            {
                e.Cancel = true;
                Hide();
                MyNotifyIcon.ShowBalloonTip("ClamAV GUI", "The application is still running in the background.", BalloonIcon.Info);
            }
            else
            {
                MyNotifyIcon.Dispose();
            }

            base.OnClosing(e);
        }
    }
}