using Avalonia.Controls;
using Avalonia.Interactivity;
using ClamAVGui.App.ViewModels;
using ClamAVGui.Core.Models;

namespace ClamAVGui.App.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is HistoryEvent item && DataContext is HistoryViewModel vm)
        {
            vm.DeleteEventCommand.Execute(item);
        }
    }
}
