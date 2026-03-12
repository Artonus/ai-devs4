using System.Security.Cryptography;
using System.Text;

namespace Agent.Core.Tools.Implementations;

/// <summary>
///     Simple disk cache for URL-fetched content. Keyed by SHA256 of the cache key string.
///     All entries are stored as UTF-8 text files under <see cref="CacheDir"/>.
/// </summary>
internal static class UrlCache
{
    internal const string CacheDir = "files/cache";

    /// <summary>Returns the cached value if present, otherwise null.</summary>
    internal static async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var path = CachePath(key);
        if (!File.Exists(path))
            return null;
        return await File.ReadAllTextAsync(path, ct);
    }

    /// <summary>Persists a value to the cache.</summary>
    internal static async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        Directory.CreateDirectory(CacheDir);
        await File.WriteAllTextAsync(CachePath(key), value, ct);
    }

    private static string CachePath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(CacheDir, hash + ".txt");
    }
}
