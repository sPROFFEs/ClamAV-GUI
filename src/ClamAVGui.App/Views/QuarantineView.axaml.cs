using Avalonia.Controls;
using Avalonia.Interactivity;
using ClamAVGui.App.ViewModels;
using ClamAVGui.Core.Models;

namespace ClamAVGui.App.Views;

public partial class QuarantineView : UserControl
{
    public QuarantineView()
    {
        InitializeComponent();
    }

    private void OnRestoreClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is QuarantineItem item && DataContext is QuarantineViewModel vm)
        {
            vm.RestoreItemCommand.Execute(item);
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is QuarantineItem item && DataContext is QuarantineViewModel vm)
        {
            vm.DeleteItemCommand.Execute(item);
        }
    }
}
