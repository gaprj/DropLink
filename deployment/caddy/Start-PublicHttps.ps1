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
    throw 'Inserisci un nome DNS pubblico valido, ad esempio share.tuodominio.it. Non includere https:// o percorsi.'
}

$caddyCommand = Get-Command 'caddy.exe' -ErrorAction SilentlyContinue
if (-not $caddyCommand) { $caddyCommand = Get-Command 'caddy' -ErrorAction SilentlyContinue }
if ($caddyCommand) {
    $caddyPath = $caddyCommand.Source
} else {
    $localCaddy = Join-Path $PSScriptRoot 'caddy.exe'
    if (Test-Path -LiteralPath $localCaddy) { $caddyPath = $localCaddy } else {
        throw "Caddy non trovato. Installa il binario ufficiale da https://caddyserver.com/download e rendilo disponibile nel PATH oppure copia caddy.exe in '$PSScriptRoot'."
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
Write-Host 'DropLink - avvio HTTPS pubblico' -ForegroundColor Green
Write-Host "Dominio:      $Domain"
Write-Host "Server locale: 127.0.0.1:$ServerPort"
Write-Host "Caddyfile:    $caddyfile"
Write-Host ''
Write-Host 'Prerequisiti:' -ForegroundColor Yellow
Write-Host '  1. DropLink deve essere avviato sulla porta locale indicata.'
Write-Host '  2. Il record DNS A/AAAA del dominio deve puntare alla tua connessione pubblica.'
Write-Host '  3. Il router deve inoltrare TCP 80 -> PC:80 e TCP 443 -> PC:443.'
Write-Host '  4. Windows Firewall deve consentire Caddy su TCP 80 e 443.'
Write-Host '  5. NON inoltrare sul router la porta interna di DropLink (predefinita 43821).'
Write-Host '  6. Con CGNAT, il normale port forwarding potrebbe non funzionare.'
Write-Host ''
Write-Host 'Caddy usa certificati pubblici automatici; la prima emissione richiede DNS e porte raggiungibili.'
Write-Host 'Premi Ctrl+C per arrestare Caddy.'
Write-Host ''

& $caddyPath validate --config $caddyfile --adapter caddyfile
if ($LASTEXITCODE -ne 0) { throw "La validazione del Caddyfile è fallita (codice $LASTEXITCODE)." }
& $caddyPath run --config $caddyfile --adapter caddyfile
if ($LASTEXITCODE -ne 0) { throw "Caddy è terminato con codice $LASTEXITCODE." }
