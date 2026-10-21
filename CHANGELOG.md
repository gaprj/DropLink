# Changelog

## 0.4.8 — 2026-10-21

- Made English the default interface language for new installations while retaining all eight interface languages.
- Removed the duplicate English README file; the main README is now English and focuses on current features instead of version-by-version highlights.
- Translated the user-facing output of the PowerShell diagnostics and Caddy scripts into English.
- Updated the GitHub publishing guide and networking documentation for the automatic zrok setup flow.
- Added tag-based GitHub Release publishing for the self-contained Windows x64 executable.

## 0.4.7 — 2026-10-21

- Removed the decorative arrow from the sharing hero artwork.
- Added native in-app toast feedback for copying, creating/revoking shares, opening links, saving settings, DNS checks and starting/stopping the Internet tunnel.
- Routine errors appear in the app without modal interruption; confirmation and account-setup dialogs remain modal where user input is required.

## 0.4.6 — 2026-10-21

- Fixed public zrok endpoint parsing when the CLI prints a bare `*.shares.zrok.io` hostname or embeds the endpoint in JSON logs.
- Added regression tests for bare hostnames, JSON logs, HTTPS URLs and invalid hosts.
- Removed the redundant technical footer from the Shares page.

## 0.4.5 — 2026-10-20

- Fixed detection of the zrok CLI message `unable to load environment; did you 'zrok2 enable'?` so first-use authorization opens inside DropLink.
- Added regression tests for the environment-setup error classifier.
- No account token is embedded in the project; each user authorizes their own zrok environment.

## 0.4.4 — 2026-10-20

- Added an explicit `System.IO` import to `ZrokTunnelService.cs` so file, path, stream, directory and data exception types resolve consistently on Windows builds.
- Made the decompression-mode argument explicit when opening the downloaded gzip archive.
- Updated the application and HTTP User-Agent version to 0.4.4.

## 0.4.3 — 2026-10-20

- Fixed the missing `System.Net.Http` import in `ZrokTunnelService.cs`, which prevented the app from compiling.
- Updated the application version to 0.4.3.

## 0.4.2 — automatic zrok setup

- Added automatic download and SHA-256 verification of the official Windows x64 `zrok2` client.
- Added a native in-app dialog for one-time, personal account authorization.
- Existing share cards are updated to public HTTPS URLs when the tunnel starts and reverted when it stops.

## 0.4.1 — zrok tunnel build fix

- Added an explicit `System.IO` import to resolve missing filesystem symbols on Windows builds.

## 0.3.0-native — HTTPS reverse-proxy support

- Added public HTTPS links through Caddy, PowerShell networking diagnostics and Windows Firewall setup scripts.
- Updated link validation to require HTTPS on public port 443.

## 0.2.0-native — Windows-native preview

- Replaced the Tauri/WebView shell with a WPF Windows-native UI.
- Added eight interface languages, local file streaming, random share tokens, link revocation and automated tests.
