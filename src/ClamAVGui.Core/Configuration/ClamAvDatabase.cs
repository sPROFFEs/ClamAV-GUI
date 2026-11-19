using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;

namespace ClamAVGui.Core.Configuration;

public enum DatabaseStatus
{
    Unknown,
    Missing,
    Unreadable,
    Empty,
    Available
}

public static class ClamAvDatabase
{
    private static readonly HashSet<string> SignatureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cvd", ".cld", ".cud", ".ndb", ".ldb", ".hdb", ".hsb", ".mdb", ".msb", ".sdb",
        ".zmd", ".rmd", ".fp", ".sfp", ".ign", ".ign2", ".idb", ".cbc", ".db", ".db2",
        ".db3", ".gdb", ".wdb", ".pdb", ".ftm", ".cfg", ".crb", ".cat", ".cdb", ".imp"
    };

    public static async Task<ClamAvInstallation?> FindInstallationAsync(
        this IClamAvBinaryLocator locator, ApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        var installation = await locator.FindInstallationAsync(settings.CustomClamAvPath, cancellationToken);
        return installation != null && !string.IsNullOrWhiteSpace(settings.CustomDatabaseDirectory)
            ? installation with { DatabaseDirectory = settings.CustomDatabaseDirectory.Trim(), UsesCustomDatabaseDirectory = true }
            : installation;
    }

    public static DatabaseStatus Inspect(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return DatabaseStatus.Unknown;

        try
        {
            var unreadable = false;
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                if (!SignatureExtensions.Contains(Path.GetExtension(path))) continue;
                try
                {
                    using var stream = File.OpenRead(path);
                    if (stream.Length > 0) return DatabaseStatus.Available;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    unreadable = true;
                }
            }

            return unreadable ? DatabaseStatus.Unreadable : DatabaseStatus.Empty;
        }
        catch (DirectoryNotFoundException)
        {
            return DatabaseStatus.Missing;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return DatabaseStatus.Unreadable;
        }
    }

    public static string Describe(DatabaseStatus status) => status switch
    {
        DatabaseStatus.Available => "Signature files found",
        DatabaseStatus.Empty => "No signature files found",
        DatabaseStatus.Unreadable => "Database cannot be read",
        DatabaseStatus.Missing => "Database folder not found",
        _ => "Database location unknown"
    };
}
