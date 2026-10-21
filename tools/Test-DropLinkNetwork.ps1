[CmdletBinding()]
param(
    [string] $Domain,
    [string] $RouterWanIPv4
)

$ErrorActionPreference = 'Continue'

function Test-PrivateOrSharedIPv4([string] $Address) {
    try { $b = [System.Net.IPAddress]::Parse($Address).GetAddressBytes() } catch { return $false }
    if ($b.Length -ne 4) { return $false }
    return ($b[0] -eq 10) -or
           ($b[0] -eq 172 -and $b[1] -ge 16 -and $b[1] -le 31) -or
           ($b[0] -eq 192 -and $b[1] -eq 168) -or
           ($b[0] -eq 100 -and $b[1] -ge 64 -and $b[1] -le 127) -or
           ($b[0] -eq 169 -and $b[1] -eq 254)
}

Write-Host '=== Diagnostica rete DropLink ===' -ForegroundColor Green
Write-Host "PC: $env:COMPUTERNAME"
Write-Host ''
Write-Host 'Indirizzi IPv4 e gateway locale:'
try {
    Get-NetIPConfiguration | ForEach-Object {
        $ips = ($_.IPv4Address | ForEach-Object { $_.IPAddress }) -join ', '
        $gateways = ($_.IPv4DefaultGateway | ForEach-Object { $_.NextHop }) -join ', '
        if ($ips -or $gateways) { [PSCustomObject]@{ Interfaccia=$_.InterfaceAlias; IPv4=$ips; Gateway=$gateways } }
    } | Format-Table -AutoSize
} catch { Write-Warning "Impossibile leggere la configurazione IP: $($_.Exception.Message)" }

$publicIP = $null
try {
    $publicIP = (Invoke-RestMethod -Uri 'https://api4.ipify.org' -TimeoutSec 10).ToString().Trim()
    Write-Host "IPv4 visto da Internet: $publicIP" -ForegroundColor Cyan
} catch {
    Write-Warning "Non riesco a rilevare l'IPv4 pubblico da api.ipify.org: $($_.Exception.Message)"
}

if ($RouterWanIPv4) {
    Write-Host "IPv4 WAN del router (inserito da te): $RouterWanIPv4"
    if ($publicIP -and $RouterWanIPv4 -eq $publicIP) {
        Write-Host 'Il WAN IPv4 coincide con quello visto da Internet: è un buon segno, ma non prova che le porte siano raggiungibili.' -ForegroundColor Green
    } elseif (Test-PrivateOrSharedIPv4 $RouterWanIPv4) {
        Write-Warning 'Il WAN IPv4 del router è privato o nell’intervallo condiviso 100.64.0.0/10: è probabile un doppio NAT o CGNAT. Il port forwarding normale potrebbe non bastare.'
    } else {
        Write-Warning 'Il WAN IPv4 del router non coincide con l’IP visto da Internet. Potrebbe esserci NAT a monte; verifica con il provider.'
    }
} else {
    Write-Host 'Per valutare CGNAT, confronta questo IPv4 con l’indirizzo WAN/Internet mostrato nella pagina di stato del router.'
    Write-Host 'Se il WAN è 100.64.x.x–100.127.x.x o un indirizzo privato (10.x, 172.16–31.x, 192.168.x), il port forwarding potrebbe non funzionare.'
}

if ($Domain) {
    Write-Host ''
    Write-Host "DNS per $Domain :"
    try {
        $resolved = Resolve-DnsName -Name $Domain -ErrorAction Stop | Where-Object { $_.Type -in @('A','AAAA') } | Select-Object -ExpandProperty IPAddress -Unique
        $resolved | ForEach-Object { Write-Host "  $_" }
        if ($publicIP -and $resolved -contains $publicIP) {
            Write-Host 'Il DNS include l’IPv4 pubblico rilevato.' -ForegroundColor Green
        } elseif ($resolved) {
            Write-Warning 'Il DNS risolve, ma non include l’IPv4 pubblico rilevato. Se usi IPv6 o un proxy DNS, controlla la configurazione.'
        } else {
            Write-Warning 'Nessun record A/AAAA trovato.'
        }
        Write-Host 'Test TCP 443 (il risultato può essere fuorviante dalla stessa LAN per il NAT loopback):'
        Test-NetConnection -ComputerName $Domain -Port 443 -InformationLevel Detailed
    } catch { Write-Warning "Risoluzione DNS/test fallito: $($_.Exception.Message)" }
}

Write-Host ''
Write-Host 'Nota: il test da questo PC non prova in modo conclusivo l’accesso dall’esterno. Dopo aver configurato Caddy e il router, prova il link da un telefono con Wi-Fi disattivato.' -ForegroundColor Yellow
