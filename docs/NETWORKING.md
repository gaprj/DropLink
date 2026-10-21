# Networking, direct transfers and HTTPS

DropLink serves files from the sender's Windows PC. In LAN mode, devices download directly from that PC. For temporary public links, requests pass through the zrok tunnel. For a custom-domain setup, Caddy can terminate HTTPS and reverse-proxy to DropLink on the same PC.

## Temporary Internet links (recommended for CGNAT)

The **Start temporary Internet link** button downloads and verifies the official `zrok2` client when needed. At first use, DropLink asks for a one-time personal zrok account authorization in a native window. No router port forwarding is required. Follow [Temporary Internet sharing](TEMPORARY_INTERNET_SHARING.md) for account setup, limits and troubleshooting.

## Custom-domain public HTTPS

DropLink's embedded server uses HTTP on its local port (default `43821`). Caddy can terminate TLS on TCP 443, redirect HTTP on TCP 80 to HTTPS, and proxy requests to the local DropLink server.

1. Use a public domain/subdomain you control. Point its DNS `A`/`AAAA` record or DDNS hostname to the connection's public IP. Do not enter `https://` or a path in DropLink's domain field.
2. Check inbound connectivity. Run `tools/Test-DropLinkNetwork.ps1` and compare the detected public IPv4 with the router's WAN IPv4. An address in `100.64.0.0/10`, another private range, or a mismatch may indicate CGNAT or double NAT; this comparison is not conclusive.
3. Install Caddy from <https://caddyserver.com/download>. Put `caddy.exe` in `PATH` or in `deployment/caddy/`.
4. Open PowerShell as Administrator from the repository root and run:

   ```powershell
   .\deployment\caddy\Enable-CaddyFirewall.ps1
   ```

   This creates firewall rules for Caddy on TCP 80 and 443 only.
5. Reserve a stable LAN IP for the PC. Forward external TCP 80 to the PC's TCP 80 and external TCP 443 to the PC's TCP 443. **Do not forward DropLink's internal port (`43821` by default) directly to the Internet.**
6. Start DropLink, then run from the repository root:

   ```powershell
   .\deployment\caddy\Start-PublicHttps.ps1 -Domain "share.example.com" -ServerPort 43821
   ```

   Replace the sample domain and keep Caddy running. Caddy can obtain and renew a trusted certificate when DNS and external ports are reachable. See <https://caddyserver.com/docs/automatic-https>.
7. In DropLink Settings, enter the same hostname and save. Create new shares to generate links such as `https://share.example.com/s/<token>`. Existing links keep their original URLs.
8. Test from a phone with Wi-Fi disabled. DNS resolution alone does not prove that forwarding, firewall rules and ISP routing are correct.

## Network path

For custom-domain sharing:

`Recipient browser → public TCP 443 → Caddy (HTTPS) → 127.0.0.1:43821 (DropLink) → file on sender's PC`

Only Caddy's TCP ports 80 and 443 need to be reachable from the Internet. Keep the PC, DropLink and Caddy running while shares need to work.

## Troubleshooting

- If DNS does not resolve to the current public IP, fix the DNS/DDNS record first.
- If Caddy cannot obtain a certificate, confirm public DNS and inbound TCP ports 80/443; inspect Caddy's ACME error output.
- If local testing succeeds but mobile-data access fails, inspect router forwards, Windows Firewall, any upstream router/modem and the ISP's CGNAT policy.
- If the ISP uses CGNAT and cannot provide a reachable public address, conventional port forwarding will not enable direct incoming access. Use the temporary zrok tunnel instead, understanding that public traffic then passes through the provider's network.

## Local-only links

If no public hostname is configured, DropLink generates `http://<LAN-IP>:<port>/s/<token>` links for devices on the same reachable local network. Local HTTP links are not encrypted; do not use them for confidential files on untrusted networks.
