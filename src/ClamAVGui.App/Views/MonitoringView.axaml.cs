using Avalonia.Controls;
using Avalonia.Interactivity;
using ClamAVGui.App.ViewModels;

namespace ClamAVGui.App.Views;

public partial class MonitoringView : UserControl
{
    public MonitoringView()
    {
        InitializeComponent();
    }

    private void OnRemoveMonitoredClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && DataContext is MonitoringViewModel vm)
        {
            vm.RemoveMonitoredFolderCommand.Execute(path);
        }
    }

    private void OnRemoveExcludedClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && DataContext is MonitoringViewModel vm)
        {
            vm.RemoveExcludedFolderCommand.Execute(path);
        }
    }
}
