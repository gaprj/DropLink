# Internet access, direct transfers and HTTPS

DropLink serves file bytes from the sender's Windows PC. It does not upload files to a cloud or relay. The desktop app's own server speaks HTTP on its local port (default `43821`); **public links use HTTPS through Caddy**, which terminates TLS and proxies requests to DropLink on the same PC.

## Public links: required setup

1. **Use a domain you control.** Create a DNS `A` record for the subdomain (for example `share.example.com`) pointing to the public IPv4 of your home connection. If your public IP changes, use DDNS. Do not enter `https://` or a path in DropLink's domain field.
2. **Check whether inbound access is possible.** Open the router status page and compare its WAN/Internet IPv4 with the IPv4 printed by `tools/Test-DropLinkNetwork.ps1`. If the router WAN address is in `100.64.0.0/10` or another private range, or it differs from the public address, the connection may be behind CGNAT/double NAT. This comparison is a clue, not a definitive diagnosis; ask the ISP whether it can provide a public IPv4 or inbound IPv6.
3. **Install Caddy.** Obtain the Windows binary from the official download page: <https://caddyserver.com/download>. Put `caddy.exe` in `PATH` or beside `Start-PublicHttps.ps1` in `deployment/caddy/`.
4. **Allow Caddy through Windows Firewall.** Open PowerShell as Administrator from the project root and run:

   ```powershell
   .\deployment\caddy\Enable-CaddyFirewall.ps1
   ```

   This creates rules only for Caddy on TCP 80 and 443. It does not open DropLink's internal port.
5. **Configure the router.** Reserve a stable LAN IP for the PC. Forward external TCP `80` to this PC's TCP `80`, and external TCP `443` to this PC's TCP `443`. These rules are for Caddy, not DropLink's port. Do **not** forward TCP `43821` (or the internal port you configured in DropLink) directly to the Internet.
6. **Start DropLink**, then start Caddy from the project root (PowerShell can be a normal user for this step):

   ```powershell
   .\deployment\caddy\Start-PublicHttps.ps1 -Domain "share.example.com" -ServerPort 43821
   ```

   Replace the example hostname with your real DNS name and use the same local port as DropLink. Keep the Caddy console running. Caddy validates the configuration and automatically requests/renews a public TLS certificate when DNS and external ports 80/443 are correct. See [Caddy automatic HTTPS](https://caddyserver.com/docs/automatic-https).
7. In DropLink **Settings**, enter that same host name and save. New share links will be `https://share.example.com/s/<token>`. Create new shares after configuring the host; existing in-memory share links are not rewritten when settings change.
8. Test from a phone with Wi-Fi **disabled** (mobile data). The DNS check inside DropLink only checks name resolution; it does not prove router forwarding or public reachability. Testing from inside the same Wi-Fi may be misleading if the router does not support NAT loopback.

## What the network path looks like

`Recipient browser → public TCP 443 → Caddy (HTTPS) → 127.0.0.1:43821 (DropLink) → file on sender's PC`

Only Caddy's TCP ports 80 and 443 need to be reachable from the Internet. The content is proxied from the same Windows PC; it is not sent through a VPS or file relay. Keep the PC, DropLink, and Caddy running while a share needs to work.

## If it still does not work

- If DNS does not resolve to your current public IP, fix the DNS/DDNS record first.
- If Caddy cannot obtain a certificate, confirm that the domain is publicly resolvable and TCP ports 80 and 443 reach this PC; read the Caddy output for the ACME error.
- If Caddy works on the PC but a mobile-data client cannot connect, inspect router forwards, Windows Firewall, any second router/modem, and the ISP's CGNAT policy.
- If the ISP uses CGNAT and cannot supply a reachable public address, conventional port forwarding won't make direct sharing reachable. A VPS tunnel or relay may help but changes the path; it must be treated as a different, non-direct mode.

## Local-only links

If the public domain field is empty, DropLink keeps generating `http://<LAN-IP>:<port>/s/<token>` URLs for use on the same local network. Those local HTTP links are not encrypted, so do not use them for confidential transfers on untrusted networks.

## CGNAT alternative: temporary zrok2 tunnel

For users who cannot receive inbound connections, DropLink 0.4.1 can launch a temporary public HTTPS share with `zrok2`. It requires a one-time user-managed setup (install the CLI and run `zrok2 enable <TOKEN>`), then the app manages the `zrok2 share public --headless http://127.0.0.1:<port>` process. No router ports are opened. See `TEMPORARY_INTERNET_SHARING.md` for exact steps and provider limitations. Public traffic uses the tunnel service, so it is not equivalent to a direct LAN transfer and has service-dependent throughput/quota.

