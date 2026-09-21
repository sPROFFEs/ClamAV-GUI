using System.Collections.ObjectModel;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed partial class QuarantineViewModel : ViewModelBase
{
    private readonly IQuarantineService _quarantineService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INotificationService _notificationService;

    public ObservableCollection<QuarantineItem> Items { get; } = new();

    public QuarantineViewModel(
        IQuarantineService quarantineService,
        IFileDialogService fileDialogService,
        INotificationService notificationService)
    {
        _quarantineService = quarantineService;
        _fileDialogService = fileDialogService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        Items.Clear();
        await _quarantineService.ClearMissingFilesAsync();
        var items = await _quarantineService.LoadItemsAsync();
        foreach (var it in items)
        {
            Items.Add(it);
        }
    }

    [RelayCommand]
    public async Task RestoreItemAsync(QuarantineItem? item)
    {
        if (item == null) return;

        string? destination = item.OriginalPath;
        if (string.IsNullOrWhiteSpace(destination) || string.Equals(destination, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            destination = await _fileDialogService.SaveFileAsync("Select Restore Destination", Path.GetFileName(item.QuarantinePath));
            if (string.IsNullOrWhiteSpace(destination)) return;
        }

        try
        {
            await _quarantineService.RestoreItemAsync(item.Id, destination);
            Items.Remove(item);
            await _notificationService.ShowAsync("File Restored", $"Restored file to {destination}");
        }
        catch (Exception ex)
        {
            await _notificationService.ShowAsync("Restore Error", ex.Message, NotificationSeverity.Error);
        }
    }

    [RelayCommand]
    public async Task DeleteItemAsync(QuarantineItem? item)
    {
        if (item == null) return;
        try
        {
            await _quarantineService.DeleteItemAsync(item.Id);
            Items.Remove(item);
            await _notificationService.ShowAsync("File Deleted", "Quarantined item permanently deleted.");
        }
        catch (Exception ex)
        {
            await _notificationService.ShowAsync("Delete Error", ex.Message, NotificationSeverity.Error);
        }
    }
}
