# Test status and verification procedure

## Tests included in the repository

- Token format, length and uniqueness.
- Revocation and token lookup.
- Valid single byte ranges, suffix ranges and invalid/multipart ranges.
- Hostname validation and public-domain URL creation.
- Local HTTP integration: download page, `HEAD`, full download, `Range` response and revocation.

## Tests not yet run by the authoring environment

The authoring container does not have the .NET SDK installed and cannot resolve the SDK download host. Therefore this source package has **not** been compiled or executed here, and no results are claimed for the native Windows build. The Windows GitHub Actions workflow is included to perform restore, test and publish after the repository is pushed.

## Run locally on Windows 11

```powershell
dotnet restore .\tests\DropLink.Tests\DropLink.Tests.csproj
dotnet test .\tests\DropLink.Tests\DropLink.Tests.csproj -c Release
dotnet run --project .\src\DropLink\DropLink.csproj
```

## Manual tests required before a public release

1. Build and launch the native app on Windows 11.
2. Share a harmless text file and download it from the same PC.
3. Download the same file from another device on the same Wi-Fi.
4. Validate large files and a resumable Range download.
5. Revoke a share during a transfer and confirm the connection stops.
6. Test host DNS resolution; separately test TCP reachability from mobile data without Wi-Fi.
7. Click the temporary Internet tunnel button on a clean Windows profile. Verify `zrok2.exe` is downloaded to `%LOCALAPPDATA%\DropLink\tools`, the official release checksum is validated, and the app shows the one-time authorization dialog instead of requiring PowerShell/manual installation.
8. With a test account token, verify the app authorizes zrok, resumes the tunnel automatically, and rewrites currently listed share URLs to the public HTTPS hostname. Stop the tunnel and confirm local URLs are restored; then verify the public URL fails as expected. Never use a personal production token for tests in logs or screenshots.
9. Test the public URL from a phone with Wi-Fi disabled. The authoring environment has not run the Windows build or a real external connectivity test.
