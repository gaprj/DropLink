using System.Globalization;

namespace DropLink.Services;

public static class SettingsValidator
{
    public static string NormalizeHostname(string? value)
    {
        var host = (value ?? string.Empty).Trim().TrimEnd('.');
        if (host.Length == 0) return string.Empty;
        if (host.Contains('/') || host.Contains('\\') || host.Contains(':') || host.Contains('@') || host.Any(char.IsWhiteSpace))
            throw new ArgumentException("Inserisci solo il dominio, ad esempio dl.example.com, senza protocollo o percorso.");

        try
        {
            host = new IdnMapping().GetAscii(host).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("Il dominio inserito non è valido.");
        }

        var kind = Uri.CheckHostName(host);
        if (kind != UriHostNameType.Dns || !host.Contains('.') ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".home.arpa", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Inserisci un dominio DNS pubblico completo (non un IP o un nome solo locale), ad esempio share.example.com.");
        if (host.Length > 253)
            throw new ArgumentException("Il dominio è troppo lungo.");
        return host;
    }

    public static int ParseServerPort(string value)
    {
        if (!int.TryParse(value, out var port) || port is < 1024 or > 65535 || port is 80 or 443)
            throw new ArgumentException("La porta locale deve essere compresa tra 1024 e 65535, esclusi 80 e 443.");
        return port;
    }

    public static int ParsePublicPort(string value)
    {
        if (!int.TryParse(value, out var port) || port != 443)
            throw new ArgumentException("Per i link pubblici HTTPS, la porta deve essere 443. Il proxy Caddy gestisce la cifratura.");
        return port;
    }
}
