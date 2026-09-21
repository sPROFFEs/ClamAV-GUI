using System.Collections.ObjectModel;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed partial class HistoryViewModel : ViewModelBase
{
    private readonly IHistoryService _historyService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INotificationService _notificationService;
    private List<HistoryEvent> _allEvents = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _filterType = "All";

    public ObservableCollection<string> FilterTypes { get; } = new() { "All", "Scan", "Update", "Update Failed", "Config Initialized" };
    public ObservableCollection<HistoryEvent> Events { get; } = new();

    public HistoryViewModel(
        IHistoryService historyService,
        IFileDialogService fileDialogService,
        INotificationService notificationService)
    {
        _historyService = historyService;
        _fileDialogService = fileDialogService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        var list = await _historyService.LoadHistoryAsync();
        _allEvents = list.ToList();
        ApplyFilter();
    }

    [RelayCommand]
    public async Task DeleteEventAsync(HistoryEvent? item)
    {
        if (item == null) return;
        await _historyService.DeleteHistoryEventAsync(item.Id);
        _allEvents.RemoveAll(e => e.Id == item.Id);
        ApplyFilter();
    }

    [RelayCommand]
    public async Task ClearHistoryAsync()
    {
        await _historyService.ClearHistoryAsync();
        _allEvents.Clear();
        Events.Clear();
    }

    [RelayCommand]
    public async Task ExportJsonAsync()
    {
        var path = await _fileDialogService.SaveFileAsync("Export History as JSON", $"ClamAV_History_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
        if (!string.IsNullOrWhiteSpace(path))
        {
            await _historyService.ExportAsJsonAsync(path);
            await _notificationService.ShowAsync("Export Complete", "History exported to JSON.");
        }
    }

    [RelayCommand]
    public async Task ExportCsvAsync()
    {
        var path = await _fileDialogService.SaveFileAsync("Export History as CSV", $"ClamAV_History_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        if (!string.IsNullOrWhiteSpace(path))
        {
            await _historyService.ExportAsCsvAsync(path);
            await _notificationService.ShowAsync("Export Complete", "History exported to CSV.");
        }
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnFilterTypeChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Events.Clear();
        var query = _allEvents.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterType) && FilterType != "All")
        {
            query = query.Where(e => string.Equals(e.EventType, FilterType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            query = query.Where(e => e.Details.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                                     e.EventType.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var ev in query)
        {
            Events.Add(ev);
        }
    }
}
