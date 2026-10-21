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

Write-Host '=== DropLink network diagnostic ===' -ForegroundColor Green
Write-Host "PC: $env:COMPUTERNAME"
Write-Host ''
Write-Host 'Local IPv4 addresses and gateways:'
try {
    Get-NetIPConfiguration | ForEach-Object {
        $ips = ($_.IPv4Address | ForEach-Object { $_.IPAddress }) -join ', '
        $gateways = ($_.IPv4DefaultGateway | ForEach-Object { $_.NextHop }) -join ', '
        if ($ips -or $gateways) { [PSCustomObject]@{ Interface=$_.InterfaceAlias; IPv4=$ips; Gateway=$gateways } }
    } | Format-Table -AutoSize
} catch { Write-Warning "Unable to read IP configuration: $($_.Exception.Message)" }

$publicIP = $null
try {
    $publicIP = (Invoke-RestMethod -Uri 'https://api4.ipify.org' -TimeoutSec 10).ToString().Trim()
    Write-Host "Public IPv4 detected by an external service: $publicIP" -ForegroundColor Cyan
} catch {
    Write-Warning "Unable to detect the public IPv4 address via api4.ipify.org: $($_.Exception.Message)"
}

if ($RouterWanIPv4) {
    Write-Host "Router WAN IPv4 (provided by you): $RouterWanIPv4"
    if ($publicIP -and $RouterWanIPv4 -eq $publicIP) {
        Write-Host 'The router WAN IPv4 matches the externally detected IPv4. This is a good sign, but it does not prove that inbound ports are reachable.' -ForegroundColor Green
    } elseif (Test-PrivateOrSharedIPv4 $RouterWanIPv4) {
        Write-Warning 'The router WAN IPv4 is private or within the shared 100.64.0.0/10 range. Double NAT or CGNAT is likely, and ordinary port forwarding may not be sufficient.'
    } else {
        Write-Warning 'The router WAN IPv4 differs from the externally detected IP. There may be upstream NAT; check with your Internet provider.'
    }
} else {
    Write-Host 'To assess CGNAT, compare this IPv4 address with the WAN/Internet IPv4 shown in the router status page.'
    Write-Host 'If the WAN address is in 100.64.x.x–100.127.x.x or a private range (10.x, 172.16–31.x, 192.168.x), normal port forwarding may not work.'
}

if ($Domain) {
    Write-Host ''
    Write-Host "DNS records for $Domain :"
    try {
        $resolved = Resolve-DnsName -Name $Domain -ErrorAction Stop | Where-Object { $_.Type -in @('A','AAAA') } | Select-Object -ExpandProperty IPAddress -Unique
        $resolved | ForEach-Object { Write-Host "  $_" }
        if ($publicIP -and $resolved -contains $publicIP) {
            Write-Host 'DNS includes the detected public IPv4 address.' -ForegroundColor Green
        } elseif ($resolved) {
            Write-Warning 'DNS resolves, but does not include the detected public IPv4. Check the configuration if you use IPv6 or a DNS proxy.'
        } else {
            Write-Warning 'No A/AAAA records were found.'
        }
        Write-Host 'TCP 443 test (results from the same LAN may be misleading if the router lacks NAT loopback):'
        Test-NetConnection -ComputerName $Domain -Port 443 -InformationLevel Detailed
    } catch { Write-Warning "DNS resolution or connectivity test failed: $($_.Exception.Message)" }
}

Write-Host ''
Write-Host 'Note: this PC-side diagnostic cannot conclusively prove external reachability. After configuring Caddy and the router, test the link from a phone with Wi-Fi disabled.' -ForegroundColor Yellow
