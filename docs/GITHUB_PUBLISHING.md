# Publishing DropLink on GitHub

This project contains source code and no prebuilt application binary. Before pushing, review staged files and confirm that no personal zrok token, `zrok2.exe`, local Caddy configuration, generated build output, or private screenshots have been added. The `.gitignore` excludes local build output, the optional zrok executable, and `deployment/caddy/Caddyfile`.

## Create the repository

1. Sign in to GitHub and open <https://github.com/new>.
2. Create a repository named `DropLink-Native` (or another available name). Choose **Public** only if you intend to publish the source openly; otherwise choose **Private**.
3. Do not initialize it with a README, license, or `.gitignore`: those files are already included here.

## Push from PowerShell

Run these commands from the extracted `DropLink_Native` project directory after creating the empty remote repository. Replace the remote URL with the URL GitHub displays for your repository.

```powershell
git init -b main
git add .
git status --short
# Review the staged file list and confirm it contains no secrets or local configuration.
git commit -m "Release DropLink 0.4.7"
git remote add origin https://github.com/<YOUR-USER>/DropLink-Native.git
git push -u origin main
```

Git Credential Manager or GitHub's sign-in prompt should handle authentication; do not paste an access token into source files or commit it. The existing GitHub Actions workflow at `.github/workflows/windows-build.yml` runs the tests and publishes a self-contained win-x64 artifact.

## Validate before release

On Windows 11 with the .NET 10 SDK:

```powershell
dotnet restore .\tests\DropLink.Tests\DropLink.Tests.csproj
dotnet test .\tests\DropLink.Tests\DropLink.Tests.csproj -c Release
dotnet build .\src\DropLink\DropLink.csproj -c Release
```

The source archive does not include a prebuilt `DropLink.exe`. Check both the local Windows build and the GitHub Actions result before publishing a release binary.
