[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Open PowerShell as Administrator and run this script again.'
}

$caddyCommand = Get-Command 'caddy.exe' -ErrorAction SilentlyContinue
if (-not $caddyCommand) { $caddyCommand = Get-Command 'caddy' -ErrorAction SilentlyContinue }
if ($caddyCommand) {
    $caddyPath = $caddyCommand.Source
} else {
    $localCaddy = Join-Path $PSScriptRoot 'caddy.exe'
    if (Test-Path -LiteralPath $localCaddy) { $caddyPath = $localCaddy } else {
        throw "Caddy was not found. Install Caddy and add it to PATH or copy caddy.exe to '$PSScriptRoot'."
    }
}

foreach ($port in @(80, 443)) {
    $name = "DropLink-Caddy-TCP-$port"
    if (Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue) {
        Set-NetFirewallRule -Name $name -Enabled True -Action Allow
        Write-Host "Updated firewall rule: TCP $port"
    } else {
        New-NetFirewallRule -Name $name -DisplayName "DropLink Caddy TCP $port" -Direction Inbound -Program $caddyPath -Protocol TCP -LocalPort $port -Action Allow -Profile Domain,Private,Public | Out-Null
        Write-Host "Created firewall rule: TCP $port"
    }
}
Write-Host 'Firewall configured for Caddy on TCP ports 80 and 443 only.' -ForegroundColor Green
Write-Host "DropLink's internal server port has not been opened."
