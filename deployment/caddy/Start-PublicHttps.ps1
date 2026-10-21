[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Domain,

    [ValidateRange(1024, 65535)]
    [int] $ServerPort = 43821
)

$ErrorActionPreference = 'Stop'
$Domain = $Domain.Trim().TrimEnd('.').ToLowerInvariant()

if ($Domain -match '[/\\:@\s]' -or $Domain -notmatch '^(?=.{1,253}$)([a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$') {
    throw 'Enter a valid public DNS hostname, e.g. share.example.com. Do not include https:// or a path.'
}

$caddyCommand = Get-Command 'caddy.exe' -ErrorAction SilentlyContinue
if (-not $caddyCommand) { $caddyCommand = Get-Command 'caddy' -ErrorAction SilentlyContinue }
if ($caddyCommand) {
    $caddyPath = $caddyCommand.Source
} else {
    $localCaddy = Join-Path $PSScriptRoot 'caddy.exe'
    if (Test-Path -LiteralPath $localCaddy) { $caddyPath = $localCaddy } else {
        throw "Caddy was not found. Install the official binary from https://caddyserver.com/download and add it to PATH or copy caddy.exe to '$PSScriptRoot'."
    }
}

$caddyfile = Join-Path $PSScriptRoot 'Caddyfile'
$config = @"
$Domain {
    encode zstd gzip
    reverse_proxy 127.0.0.1:$ServerPort
}
"@
[System.IO.File]::WriteAllText($caddyfile, $config, [System.Text.UTF8Encoding]::new($false))

Write-Host ''
Write-Host 'DropLink - starting public HTTPS' -ForegroundColor Green
Write-Host "Domain:        $Domain"
Write-Host "Local server:  127.0.0.1:$ServerPort"
Write-Host "Caddyfile:     $caddyfile"
Write-Host ''
Write-Host 'Prerequisites:' -ForegroundColor Yellow
Write-Host '  1. DropLink must be running on the specified local port.'
Write-Host '  2. The domain DNS A/AAAA record must point to your public connection.'
Write-Host '  3. The router must forward TCP 80 -> PC:80 and TCP 443 -> PC:443.'
Write-Host '  4. Windows Firewall must allow Caddy on TCP 80 and 443.'
Write-Host '  5. Do NOT forward DropLink internal port (default 43821) on the router.'
Write-Host '  6. With CGNAT, ordinary port forwarding may not work.'
Write-Host ''
Write-Host 'Caddy automatically obtains public certificates when DNS and the required ports are reachable.'
Write-Host 'Press Ctrl+C to stop Caddy.'
Write-Host ''

& $caddyPath validate --config $caddyfile --adapter caddyfile
if ($LASTEXITCODE -ne 0) { throw "Caddyfile validation failed (exit code $LASTEXITCODE)." }
& $caddyPath run --config $caddyfile --adapter caddyfile
if ($LASTEXITCODE -ne 0) { throw "Caddy exited with code $LASTEXITCODE." }
