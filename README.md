# DropLink

DropLink is a native Windows desktop application for sharing individual files from your PC. It is built with **WPF and .NET 10** and uses native Windows controls rather than an embedded WebView. Recipients open a download page in their browser.

## Features

- Native Windows desktop interface; no Tauri or WebView.
- Interface translations for Italian, English, Spanish, French, German, Portuguese, Chinese and Japanese.
- Share one selected file with a cryptographically random 128-bit link token; copy, open or revoke links at any time.
- Embedded Kestrel server with streaming, `HEAD` support, single byte-range requests, Unicode filenames and a bounded 64 KiB buffer.
- Direct transfers over a reachable local network without uploading files to persistent cloud storage.
- Temporary public HTTPS links through the official `zrok2` client for networks behind NAT/CGNAT.
- Optional custom-domain HTTPS setup through Caddy for networks that allow inbound connections.
- DNS checks and a PowerShell network diagnostic script.

## Temporary Internet sharing with zrok

DropLink can create a temporary HTTPS link without router port forwarding. Public traffic passes through the zrok tunnel, so it is not guaranteed to be as fast as a direct LAN transfer and is subject to the provider's current quotas and terms.

1. Click **Start temporary Internet link** in DropLink.
2. On first use, DropLink downloads the official Windows x64 `zrok2` client and verifies its SHA-256 checksum.
3. Create or sign in to your own account at <https://myzrok.io/>. Copy your personal account token and enter it in DropLink's native setup window. This authorization is normally required only once per Windows user profile.
4. DropLink starts the tunnel and updates the share links to use the public HTTPS address. Send the displayed link to the recipient.
5. Keep DropLink open until the recipient has downloaded the file. Stopping the tunnel or closing the app ends public access.

Never commit or share your zrok account token. Each user must authorize their own account; the public repository does not include a shared credential. The file remains on the sender's PC, while public requests travel through zrok. Check the provider's current limits at <https://zrok.io/pricing/>.

## Custom domain and public HTTPS with Caddy

For networks that allow inbound access, DropLink can also use a domain you control with Caddy as a reverse proxy. Caddy handles public TLS on TCP 443 and redirects HTTP on TCP 80, then forwards requests to DropLink's local server (default port `43821`).

This option requires a public DNS record or DDNS, valid inbound network access, Windows Firewall rules and router port forwarding for TCP 80/443. **Do not expose DropLink's internal port directly to the Internet.** CGNAT can prevent conventional port forwarding; use the temporary zrok tunnel instead when direct inbound access is unavailable.

Detailed instructions are in [docs/NETWORKING.md](docs/NETWORKING.md).

## Build and test

Requirements: Windows 11 x64 and the .NET 10 SDK. Download it from <https://dotnet.microsoft.com/download/dotnet/10.0>. Test dependencies use the official nuget.org feed.

Run from the repository root:

```powershell
dotnet restore .\tests\DropLink.Tests\DropLink.Tests.csproj
dotnet test .\tests\DropLink.Tests\DropLink.Tests.csproj -c Release
dotnet run --project .\src\DropLink\DropLink.csproj
```

To create a self-contained Windows x64 build:

```powershell
dotnet publish .\src\DropLink\DropLink.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\win-x64
```

The source repository does not store a compiled executable. Tagged builds are configured to publish `DropLink.exe` as a GitHub Release asset; see [Releases](https://github.com/gaprj/DropLink/releases).

## Security and limitations

- A share URL is a bearer link: anyone who has it can download the associated file until the share is revoked or DropLink/the tunnel is stopped.
- Local LAN links use HTTP and are not encrypted. Do not use them for confidential files on untrusted networks.
- Internet sharing through zrok uses a service-mediated tunnel and may add latency or throughput overhead; availability and quotas depend on the provider.
- Custom-domain public HTTPS requires valid DNS, a trusted certificate, inbound access and correct firewall/router configuration.
- Password protection, automatic link expiry, download-count limits and persistent transfer history are not implemented.

See [docs/SECURITY.md](docs/SECURITY.md), [docs/TEMPORARY_INTERNET_SHARING.md](docs/TEMPORARY_INTERNET_SHARING.md) and [docs/TESTING.md](docs/TESTING.md) before public use.

## License

DropLink is distributed under the MIT License. See [LICENSE](LICENSE).
