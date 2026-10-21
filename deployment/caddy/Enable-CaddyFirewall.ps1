[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Apri PowerShell come amministratore e riesegui questo script.'
}

$caddyCommand = Get-Command 'caddy.exe' -ErrorAction SilentlyContinue
if (-not $caddyCommand) { $caddyCommand = Get-Command 'caddy' -ErrorAction SilentlyContinue }
if ($caddyCommand) {
    $caddyPath = $caddyCommand.Source
} else {
    $localCaddy = Join-Path $PSScriptRoot 'caddy.exe'
    if (Test-Path -LiteralPath $localCaddy) { $caddyPath = $localCaddy } else {
        throw "Caddy non trovato. Installa Caddy e rendilo disponibile nel PATH oppure copia caddy.exe in '$PSScriptRoot'."
    }
}

foreach ($port in @(80, 443)) {
    $name = "DropLink-Caddy-TCP-$port"
    if (Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue) {
        Set-NetFirewallRule -Name $name -Enabled True -Action Allow
        Write-Host "Regola aggiornata: TCP $port"
    } else {
        New-NetFirewallRule -Name $name -DisplayName "DropLink Caddy TCP $port" -Direction Inbound -Program $caddyPath -Protocol TCP -LocalPort $port -Action Allow -Profile Domain,Private,Public | Out-Null
        Write-Host "Regola creata: TCP $port"
    }
}
Write-Host 'Firewall configurato solo per Caddy sulle porte TCP 80 e 443.' -ForegroundColor Green
Write-Host 'Non è stata aperta la porta interna del server DropLink.'
