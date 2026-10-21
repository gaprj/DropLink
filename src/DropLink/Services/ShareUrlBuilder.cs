using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DropLink.Models;

namespace DropLink.Services;

public static class ShareUrlBuilder
{
    public static string Build(string token, AppSettings settings, int runningPort)
    {
        if (!ShareManager.IsValidToken(token)) throw new ArgumentException("Token non valido.", nameof(token));
        if (!string.IsNullOrWhiteSpace(settings.PublicHost))
        {
            var host = SettingsValidator.NormalizeHostname(settings.PublicHost);
            if (settings.PublicPort != 443)
                throw new ArgumentException("I link pubblici DropLink richiedono HTTPS sulla porta 443.", nameof(settings));
            return $"https://{host}/s/{token}";
        }

        return $"http://{GetLocalIPv4()}:{runningPort}/s/{token}";
    }

    public static string GetLocalIPv4()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.OperationalStatus == OperationalStatus.Up &&
                                  network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(network => network.GetIPProperties().UnicastAddresses)
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.Address)
                .Where(address => !IPAddress.IsLoopback(address) && !address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                .ToArray();
            var preferred = candidates.FirstOrDefault(address =>
                address.ToString().StartsWith("192.168.", StringComparison.Ordinal) ||
                address.ToString().StartsWith("10.", StringComparison.Ordinal) ||
                IsPrivate172(address));
            return (preferred ?? candidates.FirstOrDefault() ?? IPAddress.Loopback).ToString();
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    private static bool IsPrivate172(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31;
    }
}
