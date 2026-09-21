using System.Security.Cryptography;
using System.Text.Json;
using ClamAVGui.Core.Exceptions;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClamAVGui.Core.Quarantine;

public sealed class QuarantineService : IQuarantineService
{
    private readonly string _quarantineDirectory;
    private readonly string _metadataFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<QuarantineService> _logger;

    public QuarantineService(
        string quarantineDirectory,
        ILogger<QuarantineService>? logger = null)
    {
        _quarantineDirectory = quarantineDirectory;
        Directory.CreateDirectory(_quarantineDirectory);
        _metadataFilePath = Path.Combine(_quarantineDirectory, "quarantine.json");
        _logger = logger ?? NullLogger<QuarantineService>.Instance;
    }

    public async Task<IReadOnlyList<QuarantineItem>> LoadItemsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await LoadInternalAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<QuarantineItem> QuarantineFileAsync(
        string sourcePath,
        string threatName,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("File to quarantine does not exist.", sourcePath);
        }

        var id = Guid.NewGuid();
        var destinationFileName = $"{id:N}.quarantine";
        var destinationPath = Path.Combine(_quarantineDirectory, destinationFileName);

        long size;
        string sha256;

        using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            size = sourceStream.Length;
            using var sha = SHA256.Create();
            var hashBytes = await sha.ComputeHashAsync(sourceStream, cancellationToken);
            sha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        // Copy/move file
        try
        {
            File.Move(sourcePath, destinationPath, overwrite: true);
        }
        catch
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
            try
            {
                File.Delete(sourcePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete original file after copy: {SourcePath}", sourcePath);
            }
        }

        var item = new QuarantineItem
        {
            Id = id,
            QuarantinePath = destinationPath,
            OriginalPath = sourcePath,
            ThreatName = threatName,
            QuarantinedAt = DateTime.UtcNow,
            FileHashSha256 = sha256,
            FileSizeBytes = size,
            Notes = notes ?? string.Empty
        };

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = (await LoadInternalAsync(cancellationToken)).ToList();
            items.Insert(0, item);
            await SaveInternalAsync(items, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        return item;
    }

    public async Task RestoreItemAsync(Guid id, string? targetPath = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = (await LoadInternalAsync(cancellationToken)).ToList();
            var item = items.FirstOrDefault(i => i.Id == id);
            if (item == null)
            {
                throw new ClamAvException(ClamAvErrorCode.QuarantineFailed, "Quarantined item not found in database.");
            }

            if (!File.Exists(item.QuarantinePath))
            {
                throw new FileNotFoundException("Quarantined physical file is missing.", item.QuarantinePath);
            }

            var dest = targetPath ?? item.OriginalPath;
            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrWhiteSpace(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Verify hash integrity before restoring
            using (var stream = new FileStream(item.QuarantinePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
            {
                var hashBytes = await sha.ComputeHashAsync(stream, cancellationToken);
                var currentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                if (!string.IsNullOrEmpty(item.FileHashSha256) && !string.Equals(currentHash, item.FileHashSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ClamAvException(ClamAvErrorCode.QuarantineFailed, "Quarantined file hash mismatch - file may be corrupted.");
                }
            }

            File.Move(item.QuarantinePath, dest, overwrite: true);

            items.Remove(item);
            await SaveInternalAsync(items, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = (await LoadInternalAsync(cancellationToken)).ToList();
            var item = items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                if (File.Exists(item.QuarantinePath))
                {
                    try
                    {
                        File.Delete(item.QuarantinePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete quarantined physical file {Path}", item.QuarantinePath);
                    }
                }

                items.Remove(item);
                await SaveInternalAsync(items, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearMissingFilesAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadInternalAsync(cancellationToken);
            var existing = items.Where(i => File.Exists(i.QuarantinePath)).ToList();
            if (existing.Count != items.Count)
            {
                await SaveInternalAsync(existing, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<QuarantineItem>> LoadInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_metadataFilePath))
        {
            return new List<QuarantineItem>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_metadataFilePath, cancellationToken);
            return JsonSerializer.Deserialize<List<QuarantineItem>>(json) ?? new List<QuarantineItem>();
        }
        catch
        {
            return new List<QuarantineItem>();
        }
    }

    private async Task SaveInternalAsync(List<QuarantineItem> items, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        var temp = _metadataFilePath + ".tmp." + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temp, json, cancellationToken);
        File.Move(temp, _metadataFilePath, overwrite: true);
    }
}
