# Changelog

## 0.4.7

- Removed the decorative arrow from the sharing hero artwork.
- Added native in-app toast feedback for copy, create/revoke share, browser launch, settings save, DNS checks, and Internet tunnel start/stop.
- Routine errors now surface in the app without modal interruption; confirmations and first-time account authorization remain modal by design.
- Updated publishing documentation and confirmed the existing Windows CI workflow also publishes a win-x64 artifact.


## 0.4.6 - 2026-10-21

- Fixed public zrok endpoint parsing when the CLI prints only a bare `*.shares.zrok.io` hostname or embeds it in structured log output.
- Normalize recognized zrok endpoints to their HTTPS origin before updating share links.
- Added regression tests for bare-host, JSON-log, HTTPS-URL, and invalid-host cases.
- Removed the redundant “APP WINDOWS NATIVA · NESSUNA WEBVIEW” footer from the Shares page.

## 0.4.5 — 2026-10-20
- Fixed detection of the current zrok CLI error `unable to load environment; did you 'zrok2 enable'?` so first-use authorization opens inside DropLink rather than showing a generic failure.
- Added regression tests for the environment-setup error classifier.
- No token is embedded in the project; each user still authorizes their own zrok environment once.


## 0.4.4 — 2026-10-20
- Added an explicit `System.IO` import to `ZrokTunnelService.cs` so file, path, stream, directory, and data exception types resolve consistently on Windows builds.
- Made the decompression-mode argument explicit when opening the downloaded gzip archive.
- Updated the application and HTTP User-Agent version to 0.4.4.
- No sharing or tunnel behavior was intentionally changed.


## 0.4.3 — 2026-10-20
- Fixed the missing `System.Net.Http` import in `ZrokTunnelService`, which prevented the Windows app from compiling.
- Bumped the application version to 0.4.3.


## 0.4.2 — automatic zrok setup

- DropLink downloads the official Windows x64 `zrok2` release on first use, verifies its SHA-256 checksum, and stores it in the per-user tools folder; no separate manual install is required.
- If the hosted zrok environment is not authorized, a native in-app dialog guides the user through the one-time personal account-token setup; later tunnel starts require one click.
- Existing share cards are rewritten to public HTTPS URLs when the tunnel starts and reverted to their local URLs when the tunnel stops.
- Added English/Italian documentation for automatic download, authorization and provider limits.

## 0.4.1 — build fix for the zrok tunnel integration

- Added an explicit `System.IO` import to `ZrokTunnelService.cs`, resolving missing `Path`, `File`, and `FileNotFoundException` symbols on Windows builds.
- No behavior changes to sharing, tunnel activation, or shutdown.

## 0.3.0-native — HTTPS reverse-proxy support

- Public links use `https://` on standard port 443 when a public hostname is configured; local-only links remain HTTP on the LAN.
- Added Caddy startup/configuration script for automatic public TLS, plus an optional Windows Firewall setup script for TCP 80/443.
- Added PowerShell diagnostics for public IPv4, local network details, optional DNS resolution and TCP 443 testing.
- Updated UI warnings and networking/security documentation to explain HTTPS setup and CGNAT limitations.
- Public port validation now requires HTTPS port 443; create new shares after configuring the public hostname.

## 0.2.0-native — Windows-native development preview

- Replaced the Tauri/WebView desktop shell with a WPF Windows-native UI.
- Added Italian, English, Spanish, French, German, Portuguese, Chinese and Japanese.
- Added optional public hostname/DDNS field and DNS-resolution check.
- Implemented local direct HTTP streaming, single byte ranges, HEAD, cryptographically random tokens and revocation in .NET/Kestrel.
- Added unit tests and local HTTP integration coverage.

### Fixes

- Added explicit `System.IO` imports for filesystem APIs in settings, sharing and HTTP server services.
- Added repository-level `NuGet.Config` for the official nuget.org feed.
- Added `StartupUri` to launch the native WPF window.
