# Publishing DropLink on GitHub

The repository is public at <https://github.com/gaprj/DropLink>. Source code is stored in Git; compiled output, local settings and personal service credentials must not be committed. The `.gitignore` excludes build output and local zrok/Caddy files.

## Protect your commit email

To use GitHub's private no-reply address for future local commits, run:

```powershell
git config --global user.email "85964899+gaprj@users.noreply.github.com"
```

This changes future commits only. It does not rewrite email addresses already recorded in public Git history.

## Review and push changes

From the repository root, review all staged files before committing:

```powershell
git status --short
git diff --check
git diff --cached --check
```

Do not commit zrok account tokens, `zrok2.exe`, local Caddy configuration, settings files, private screenshots, personal files or real share URLs. If a tool or build downloads `zrok2.exe`, keep it under `%LOCALAPPDATA%\DropLink\tools`, not in the repository.

After staging the intended source/documentation changes:

```powershell
git add .
git status --short
git diff --cached --check
git commit -m "Prepare DropLink 0.4.8"
git push
```

## Publish a Windows executable Release

The Windows workflow runs tests, publishes a self-contained `win-x64` build and uploads it as an Actions artifact on each push. When the tag-based release job is present on the branch, pushing a version tag triggers a GitHub Release and attaches `DropLink.exe`:

```powershell
git tag v0.4.8
git push origin v0.4.8
```

Wait for the **Windows build and tests** workflow to finish successfully, then open <https://github.com/gaprj/DropLink/releases> and verify that the release contains `DropLink.exe`. Do not call a release ready if tests or publishing fail. For a future release, bump the version in `src/DropLink/DropLink.csproj` and update this changelog before tagging.
