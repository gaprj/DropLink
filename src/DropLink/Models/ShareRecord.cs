namespace DropLink.Models;

public sealed record ShareRecord(
    string Token,
    string FullPath,
    string Name,
    long Size,
    DateTime CreatedUtc,
    string Url);
