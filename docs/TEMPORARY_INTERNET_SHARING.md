# Temporary Internet sharing with zrok2

DropLink 0.4.2 publishes its local HTTP server as a temporary public HTTPS URL through the official `zrok2` client. It works behind many NAT/CGNAT configurations without inbound router ports. It is a tunnel, not a guarantee of direct peer-to-peer traffic: the public network path is managed by zrok and may add latency or throughput overhead.

## First use

1. Start DropLink and click **Attiva link Internet temporaneo / Start temporary Internet link**.
2. If `zrok2.exe` is not already present in a known location or `PATH`, DropLink downloads the Windows x64 archive from the official `openziti/zrok` GitHub latest release, checks the SHA-256 hash listed by that release, and extracts `zrok2.exe` to `%LOCALAPPDATA%\DropLink\tools`. No manual installer or PowerShell is required.
3. Because the hosted service requires each environment to be authorized, DropLink opens a native setup dialog if the local zrok profile is not enabled. Create or sign in to your own account at <https://myzrok.io/>, copy your account token, and paste it into that dialog. DropLink passes the token to `zrok2 enable`; it does not write it to its own settings or repository. zrok stores its identity/configuration under the current Windows user profile.
4. DropLink resumes starting the tunnel automatically and reads the HTTPS endpoint reported by zrok. Existing share cards are updated to show public links; copy the updated link and send it.
5. Keep DropLink open while the recipient downloads the file. Stop the tunnel or close DropLink to terminate the public link.

This one-time account authorization cannot be removed by embedding one shared token in the public GitHub repository: that would expose a service credential and make all installations share the same account/quota.

## Limits and privacy

- Check the provider's current plan and terms at <https://zrok.io/pricing/> before relying on large transfers. Free-tier quotas can change and unverified free accounts may show an interstitial page.
- The file remains on the sender's PC, but public requests pass through zrok; throughput is not guaranteed to match a LAN/direct connection.
- A share URL is a bearer link: anyone who has it can access the associated file while the share and tunnel are active. Revoke the share when no longer needed.
- The tunnel runs only while DropLink and the zrok process remain alive. Existing public links stop working when the tunnel stops.

## Troubleshooting

- **The app says it cannot download zrok:** check Internet access and whether GitHub downloads are blocked; retry the activation. The executable is stored in `%LOCALAPPDATA%\DropLink\tools`.
- **The setup dialog appears:** this is expected on the first use or when zrok's local identity is not configured. Use your own account token.
- **The token is rejected:** verify the account is active and copy the full current token from <https://myzrok.io/>. Do not paste passwords or a token from another user.
- **No public URL appears:** leave DropLink open while zrok connects; check the error message for account, service availability, or network issues.
- **The link stops working:** check that DropLink is open and the tunnel status is active. Stop/restart the tunnel and resend the new public link if needed.

## Alternatives

- LAN link: fastest and direct, only available on networks that can reach the PC.
- Custom domain + Caddy: direct incoming access for networks that support it, but requires public reachability, DNS and firewall/router configuration.
- zrok public tunnel: automatic executable setup and a guided one-time account authorization; avoids port forwarding, but adds a service-mediated network path and provider-specific quotas.
