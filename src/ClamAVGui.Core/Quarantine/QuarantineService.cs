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
    private readonly Func<Task<string>> _directoryProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<QuarantineService> _logger;

    public QuarantineService(
        string defaultQuarantineDirectory,
        ILogger<QuarantineService>? logger = null)
        : this(() => Task.FromResult(defaultQuarantineDirectory), logger)
    {
    }

    public QuarantineService(
        Func<Task<string>> directoryProvider,
        ILogger<QuarantineService>? logger = null)
    {
        _directoryProvider = directoryProvider;
        _logger = logger ?? NullLogger<QuarantineService>.Instance;
    }

    private async Task<(string Dir, string MetaFile)> GetPathsAsync()
    {
        var dir = await _directoryProvider();
        Directory.CreateDirectory(dir);
        var meta = Path.Combine(dir, "quarantine.json");
        return (dir, meta);
    }

    public async Task<IReadOnlyList<QuarantineItem>> LoadItemsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (_, metaFile) = await GetPathsAsync();
            return await LoadInternalAsync(metaFile, cancellationToken);
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

        var (dir, metaFile) = await GetPathsAsync();
        var id = Guid.NewGuid();
        var destinationFileName = $"{id:N}.quarantine";
        var destinationPath = Path.Combine(dir, destinationFileName);

        long size;
        string sha256;

        using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            size = sourceStream.Length;
            using var sha = SHA256.Create();
            var hashBytes = await sha.ComputeHashAsync(sourceStream, cancellationToken);
            sha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

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
            var items = (await LoadInternalAsync(metaFile, cancellationToken)).ToList();
            items.Insert(0, item);
            await SaveInternalAsync(metaFile, items, cancellationToken);
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
            var (_, metaFile) = await GetPathsAsync();
            var items = (await LoadInternalAsync(metaFile, cancellationToken)).ToList();
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
            await SaveInternalAsync(metaFile, items, cancellationToken);
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
            var (_, metaFile) = await GetPathsAsync();
            var items = (await LoadInternalAsync(metaFile, cancellationToken)).ToList();
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
                await SaveInternalAsync(metaFile, items, cancellationToken);
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
            var (_, metaFile) = await GetPathsAsync();
            var items = await LoadInternalAsync(metaFile, cancellationToken);
            var existing = items.Where(i => File.Exists(i.QuarantinePath)).ToList();
            if (existing.Count != items.Count)
            {
                await SaveInternalAsync(metaFile, existing, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<List<QuarantineItem>> LoadInternalAsync(string metaFile, CancellationToken cancellationToken)
    {
        if (!File.Exists(metaFile))
        {
            return new List<QuarantineItem>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(metaFile, cancellationToken);
            return JsonSerializer.Deserialize<List<QuarantineItem>>(json) ?? new List<QuarantineItem>();
        }
        catch
        {
            return new List<QuarantineItem>();
        }
    }

    private static async Task SaveInternalAsync(string metaFile, List<QuarantineItem> items, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        var temp = metaFile + ".tmp." + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temp, json, cancellationToken);
        File.Move(temp, metaFile, overwrite: true);
    }
}
