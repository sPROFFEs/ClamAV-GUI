using Avalonia.Controls;
using Avalonia.Interactivity;
using ClamAVGui.App.ViewModels;

namespace ClamAVGui.App.Views;

public partial class ScanView : UserControl
{
    public ScanView()
    {
        InitializeComponent();
    }

    private void OnQuarantineClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DisplayScanResult item && DataContext is ScanViewModel vm)
        {
            vm.QuarantineItemCommand.Execute(item);
        }
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DisplayScanResult item && DataContext is ScanViewModel vm)
        {
            vm.OpenContainingFolderCommand.Execute(item);
        }
    }
}
