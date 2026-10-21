# Dependencies

## Application runtime

- .NET 10 and the Windows Desktop runtime: Microsoft .NET platform.
- WPF: Windows desktop UI framework; no browser/WebView control is used inside the application.
- ASP.NET Core Kestrel: referenced through `Microsoft.AspNetCore.App` for the embedded local HTTP server.
- Windows APIs: native file picker, default browser launch, clipboard and optional dark title-bar attribute.
- **Caddy (optional external component):** used only for public HTTPS links. It is not bundled in this source archive; download it from <https://caddyserver.com/download>. Caddy terminates public TLS and reverse-proxies to DropLink on the same PC.

No third-party NuGet package is required by the application project.

## Test-only dependencies

- `Microsoft.NET.Test.Sdk` 17.14.1
- `xunit` 2.9.3
- `xunit.runner.visualstudio` 3.1.5

These are used only by the test project, not by the published app. Review their current license files before creating a formal third-party notice for a release.
