using ClamAVGui.Core.Interfaces;
using ClamAVGui.Platform.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClamAVGui.App.ViewModels;

public sealed partial class UpdateViewModel : ViewModelBase
{
    private readonly IClamAvUpdater _updater;
    private readonly IHistoryService _historyService;
    private readonly INotificationService _notificationService;
    private CancellationTokenSource? _updateCts;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private string _statusText = "Ready to update";

    [ObservableProperty]
    private string _outputText = string.Empty;

    public UpdateViewModel(
        IClamAvUpdater updater,
        IHistoryService historyService,
        INotificationService notificationService)
    {
        _updater = updater;
        _historyService = historyService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    public async Task UpdateSignaturesAsync()
    {
        if (IsUpdating) return;

        IsUpdating = true;
        StatusText = "Downloading virus definitions, please wait...";
        OutputText = string.Empty;

        _updateCts?.Cancel();
        _updateCts?.Dispose();
        _updateCts = new CancellationTokenSource();

        await _historyService.LogEventAsync("Update", "Signature update process started.");

        try
        {
            var result = await _updater.UpdateDefinitionsAsync(cancellationToken: _updateCts.Token);

            if (result.WasCancelled)
            {
                StatusText = "Update was cancelled.";
                await _historyService.LogEventAsync("Update", "Signature update cancelled.");
            }
            else if (!result.Success)
            {
                StatusText = "An error occurred during signature update.";
                OutputText = $"Errors:\n{result.Error}\n\nOutput:\n{result.Output}";
                await _historyService.LogEventAsync("Update Failed", $"Error: {result.Error}");
                await _notificationService.ShowAsync("Update Failed", result.Error, NotificationSeverity.Error);
            }
            else if (result.IsAlreadyUpToDate)
            {
                StatusText = "Virus database is already up to date.";
                OutputText = result.Output;
                await _historyService.LogEventAsync("Update", "Database already up to date.");
                await _notificationService.ShowAsync("ClamAV Update", "Database is up to date.");
            }
            else
            {
                StatusText = $"Update successful! (Signatures: {result.SignaturesCount?.ToString() ?? "Updated"})";
                OutputText = result.Output;
                await _historyService.LogEventAsync("Update", $"Database updated successfully. Signatures: {result.SignaturesCount}");
                await _notificationService.ShowAsync("ClamAV Update", "Definitions updated successfully.");
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Update cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Update failed: {ex.Message}";
            OutputText = ex.ToString();
            await _historyService.LogEventAsync("Update Failed", ex.Message);
            await _notificationService.ShowAsync("Update Error", ex.Message, NotificationSeverity.Error);
        }
        finally
        {
            IsUpdating = false;
            _updateCts?.Dispose();
            _updateCts = null;
        }
    }

    [RelayCommand]
    public void CancelUpdate()
    {
        _updateCts?.Cancel();
    }
}
