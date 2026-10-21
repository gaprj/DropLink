namespace DropLink.Services;

public readonly record struct ByteRange(long Start, long End)
{
    public long Length => End - Start + 1;
}

public static class RangeParser
{
    public static bool TryParse(string? value, long size, out ByteRange range)
    {
        range = default;
        if (string.IsNullOrWhiteSpace(value) || size < 0 || size == 0 || value.Contains(',')) return false;
        var parts = value.Trim().Split('=', 2);
        if (parts.Length != 2 || !string.Equals(parts[0].Trim(), "bytes", StringComparison.OrdinalIgnoreCase)) return false;
        var spec = parts[1].Trim();
        var numbers = spec.Split('-', 2);
        if (numbers.Length != 2) return false;

        if (numbers[0].Length == 0)
        {
            if (!long.TryParse(numbers[1], out var suffix) || suffix <= 0) return false;
            var length = Math.Min(suffix, size);
            range = new ByteRange(size - length, size - 1);
            return true;
        }

        if (!long.TryParse(numbers[0], out var start) || start < 0 || start >= size) return false;
        long end;
        if (numbers[1].Length == 0) end = size - 1;
        else if (!long.TryParse(numbers[1], out end) || end < start) return false;
        end = Math.Min(end, size - 1);
        range = new ByteRange(start, end);
        return true;
    }
}
