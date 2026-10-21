# DropLink Native

DropLink Native is a Windows desktop file-sharing application built with **WPF and .NET 10**. The desktop interface uses native Windows controls and contains no WebView. The recipient's browser displays a download page; file bytes are streamed from the sender's PC.

> **Version 0.4.7 — native in-app operation feedback.** This archive contains source code and scripts, not a compiled `DropLink.exe`. Build and test on Windows before relying on it. DropLink automatically downloads the official `zrok2` client on first use, verifies its SHA-256 checksum, and handles first-time account authorization in a native window.

### New in 0.4.7

- Removed the decorative arrow in the lower-right corner of the sharing hero artwork.
- Added native WPF in-app toast feedback for copying, creating/revoking shares, opening links, saving settings, DNS checks, and starting/stopping the temporary Internet tunnel.
- Routine failures now appear as in-app feedback; confirmation and first-time account setup dialogs remain modal where user input is required.
- Added GitHub publishing instructions; the existing Windows workflow already runs tests and publishes a win-x64 artifact.

### New in 0.4.6

- Recognizes zrok public hostnames printed without `https://` and endpoints embedded in JSON logs, normalizing them to HTTPS.
- Adds regression tests for HTTPS URLs, bare hostnames, JSON logs, and invalid domains.
- Removes the technical footer from the Shares page.

### New in 0.4.5

- Detects the current zrok CLI message `unable to load environment; did you 'zrok2 enable'?` and opens the in-app authorization dialog instead of failing generically. Adds regression tests for this case.

### New in 0.4.2

- Automatic download and SHA-256 verification of the official `zrok2` client; no manual installation or PowerShell commands.
- One-time account authorization through a native DropLink dialog; later sessions start the tunnel with one click.
- Existing share links are updated when the public tunnel starts and reverted when it stops.

## Features

- Native Windows desktop UI, no Tauri and no WebView.
- Italian, English, Spanish, French, German, Portuguese, Chinese and Japanese.
- Single-file shares with cryptographically random 128-bit tokens, copy/open/revoke actions.
- Embedded Kestrel server streams file contents from the sender's PC without cloud storage. LAN transfers are direct; temporary Internet traffic is forwarded through the zrok tunnel.
- `HEAD`, single byte-range requests, Unicode filenames and bounded 64 KiB streaming.
- Local links use HTTP on the LAN. With a public hostname configured, new links use `https://` on port 443 through Caddy.
- DNS-resolution check and PowerShell network diagnostic.


## Temporary Internet sharing (zrok2)

To create a temporary HTTPS URL that can work behind CGNAT without router port forwarding:

1. Start DropLink and click **Start temporary Internet link**. On first use, DropLink downloads the official Windows zrok client from GitHub and verifies its SHA-256 checksum. No manual `zrok2` installation is needed.
2. The first run opens a native setup dialog. Create or sign in to a free account at <https://myzrok.io/>, copy your personal account token from the account page, and paste it into DropLink. The app performs the authorization; no PowerShell commands are needed.
3. After authorization, the button starts the temporary HTTPS tunnel. Existing shares are updated to use the public URL; copy the displayed link and send it to the recipient.
4. Keep DropLink open while the recipient downloads the file. Click **Stop Internet link** or close the app to end the tunnel.

The account token is personal. Do not commit it to GitHub or share it with others. DropLink does not store it in its own files; zrok stores its configuration in the current Windows user profile. Each user must authorize their own account because the hosted service requires it; the public source repository does not include shared credentials.

File bytes are still read from the sender's PC, but public traffic passes through zrok, so this is a tunnel rather than a guaranteed direct connection. Latency, throughput, quotas and a provider interstitial may apply; see <https://zrok.io/pricing/> for current terms. Details are in `docs/TEMPORARY_INTERNET_SHARING.md`.

## Public HTTPS setup

DropLink's embedded server uses HTTP on its local port (default `43821`). Caddy terminates TLS on TCP 443, redirects HTTP from TCP 80 to HTTPS, and proxies requests to DropLink on the same PC. File bytes still come directly from the sender's computer.

1. Own a public domain/subdomain and point its DNS `A`/`AAAA` record or DDNS hostname to your Internet connection.
2. Run `.\tools\Test-DropLinkNetwork.ps1` to show the public IP and local network information. Compare the detected public IPv4 with the router's WAN IPv4. Private/shared WAN addresses or mismatches may indicate double NAT or CGNAT; the diagnostic cannot prove reachability.
3. Install Caddy from the [official download page](https://caddyserver.com/download). Put `caddy.exe` in `PATH` or in `deployment/caddy/`.
4. Open PowerShell **as Administrator** and run `.\deployment\caddy\Enable-CaddyFirewall.ps1`.
5. Forward router TCP 80 to this PC's TCP 80 and TCP 443 to this PC's TCP 443. Do **not** forward DropLink's internal port (default `43821`) directly to the Internet.
6. Start DropLink, then run from the repository root:

   ```powershell
   .\deployment\caddy\Start-PublicHttps.ps1 -Domain "share.example.com" -ServerPort 43821
   ```

   Replace the example host and keep Caddy running while sharing. Caddy can provision and renew a public certificate automatically when DNS and ports are reachable; see <https://caddyserver.com/docs/automatic-https>.
7. In DropLink Settings, enter the same hostname and save, then create new shares. Public links look like `https://share.example.com/s/<token>`. Existing shares keep the URL they had when created.
8. Test from a phone with Wi-Fi disabled. DNS resolution alone does not prove router forwarding or public reachability.

If the ISP uses CGNAT and cannot provide inbound connectivity, normal port forwarding will not work. Ask for a public IPv4 or suitable IPv6 access. A VPS tunnel or relay changes the network path and is not a strictly direct transfer.

## Build and tests

Requirements: Windows 11 x64 and .NET 10 SDK (<https://dotnet.microsoft.com/download/dotnet/10.0>). Test packages use the official nuget.org feed.

```powershell
dotnet restore .\tests\DropLink.Tests\DropLink.Tests.csproj
dotnet test .\tests\DropLink.Tests\DropLink.Tests.csproj -c Release
dotnet run --project .\src\DropLink\DropLink.csproj
```

Publish a self-contained Windows x64 build:

```powershell
dotnet publish .\src\DropLink\DropLink.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\win-x64
```

The archive contains source, not a prebuilt executable. Review `docs/NETWORKING.md` and `docs/SECURITY.md` before public use.

## License

MIT. See `LICENSE`.
