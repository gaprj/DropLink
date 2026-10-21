using System.IO;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using DropLink.Models;

namespace DropLink.Services;

public sealed class ShareManager
{
    private readonly ConcurrentDictionary<string, ShareRecord> _shares = new(StringComparer.Ordinal);

    public IReadOnlyCollection<ShareRecord> Shares => _shares.Values
        .OrderByDescending(item => item.CreatedUtc)
        .ToArray();

    public ShareRecord CreateShare(string selectedPath, Func<string, string> urlFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        ArgumentNullException.ThrowIfNull(urlFactory);

        var fullPath = Path.GetFullPath(selectedPath);
        var info = new FileInfo(fullPath);
        if (!info.Exists || (info.Attributes & FileAttributes.Directory) != 0)
            throw new FileNotFoundException("Il file selezionato non esiste o non è accessibile.", fullPath);

        var name = info.Name;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Non è possibile determinare il nome del file.");

        string token;
        do
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        } while (_shares.ContainsKey(token));

        var record = new ShareRecord(token, fullPath, name, info.Length, DateTime.UtcNow, urlFactory(token));
        if (!_shares.TryAdd(token, record))
            throw new InvalidOperationException("Non è stato possibile creare la condivisione.");
        return record;
    }

    public bool TryGet(string token, out ShareRecord record)
    {
        record = null!;
        return IsValidToken(token) && _shares.TryGetValue(token, out record!);
    }

    public void UpdateUrls(Func<string, string> urlFactory)
    {
        ArgumentNullException.ThrowIfNull(urlFactory);
        foreach (var pair in _shares.ToArray())
        {
            while (_shares.TryGetValue(pair.Key, out var current))
            {
                var updated = current with { Url = urlFactory(current.Token) };
                if (_shares.TryUpdate(pair.Key, updated, current)) break;
            }
        }
    }

    public bool Revoke(string token) => IsValidToken(token) && _shares.TryRemove(token, out _);

    public bool IsCurrent(ShareRecord expected) =>
        _shares.TryGetValue(expected.Token, out var current) && ReferenceEquals(expected, current);

    public static bool IsValidToken(string? token) =>
        token is { Length: 32 } && token.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
