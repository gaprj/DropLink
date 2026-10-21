# Test status and verification procedure

## Automated coverage

The test project covers token format and uniqueness, revocation and lookup, byte-range parsing, hostname validation, public-domain URL construction, and local HTTP integration (`HEAD`, full downloads, `Range` responses and revocation).

The repository's Windows GitHub Actions workflow restores dependencies, runs the test suite, publishes a self-contained Windows x64 application and uploads the build as an artifact. The maintainer has also confirmed 30 tests passing and a successful Release build on Windows for version 0.4.7. Re-run tests after applying changes to the source or workflow.

## Run locally on Windows 11

Requirements: the .NET 10 SDK and network access to the official NuGet feed.

```powershell
dotnet restore .\tests\DropLink.Tests\DropLink.Tests.csproj
dotnet test .\tests\DropLink.Tests\DropLink.Tests.csproj -c Release
dotnet build .\src\DropLink\DropLink.csproj -c Release
dotnet run --project .\src\DropLink\DropLink.csproj
```

## Manual acceptance tests before a public release

1. Build and launch the native app on a clean Windows 11 profile.
2. Share a harmless text file and download it from the same PC.
3. Download the file from another device on the same local network.
4. Test large files and a byte-range/resume download.
5. Revoke a share during a transfer and confirm access is stopped.
6. Test DNS resolution; separately test public TCP reachability from mobile data with Wi-Fi disabled for custom-domain mode.
7. On a clean Windows profile, activate the temporary Internet tunnel and verify that `zrok2.exe` is downloaded to `%LOCALAPPDATA%\DropLink\tools` and its official release checksum is validated.
8. Complete the one-time zrok account setup with a test account, confirm the tunnel starts and existing share URLs change to the public HTTPS hostname, then stop the tunnel and confirm public access ends.
9. Download a file from a phone using mobile data. Never capture or publish a personal token, private path or private share URL in logs or screenshots.
