using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ClamAVGui.Platform.Interfaces;

namespace ClamAVGui.App.Services;

public sealed class AvaloniaFileDialogService : IFileDialogService
{
    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    public async Task<string?> SelectFileAsync(string title = "Select File", string? filter = null)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<IReadOnlyList<string>> SelectFilesAsync(string title = "Select Files", string? filter = null)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return Array.Empty<string>();

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true
        });

        return files.Select(f => f.Path.LocalPath).ToList();
    }

    public async Task<string?> SelectFolderAsync(string title = "Select Folder")
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public async Task<string?> SaveFileAsync(string title = "Save File", string? defaultName = null, string? filter = null)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultName
        });

        return file?.Path.LocalPath;
    }
}

public sealed class AvaloniaNotificationService : INotificationService
{
    public event Action<string, string, NotificationSeverity>? NotificationReceived;

    public Task ShowAsync(string title, string message, NotificationSeverity severity = NotificationSeverity.Information, CancellationToken cancellationToken = default)
    {
        NotificationReceived?.Invoke(title, message, severity);
        return Task.CompletedTask;
    }
}

public sealed class AvaloniaTrayService : ITrayService
{
    private TrayIcon? _trayIcon;

    public void Initialize()
    {
        _trayIcon ??= new TrayIcon { ToolTipText = "ClamAV GUI" };
    }

    public void SetStatus(TrayStatus status)
    {
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = $"ClamAV GUI - {status}";
        }
    }

    public void Show()
    {
        if (_trayIcon != null) _trayIcon.IsVisible = true;
    }

    public void Hide()
    {
        if (_trayIcon != null) _trayIcon.IsVisible = false;
    }
}
