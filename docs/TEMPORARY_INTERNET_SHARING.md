# Temporary Internet sharing with zrok2

DropLink can expose its local HTTP server through a temporary public HTTPS URL using the official `zrok2` client. This works behind many NAT/CGNAT configurations without router port forwarding. It is a tunnel, not a guaranteed direct peer-to-peer connection: public traffic passes through zrok and may add latency or throughput overhead.

## First use

1. Start DropLink and click **Start temporary Internet link**.
2. If the client is missing, DropLink downloads the official Windows x64 archive from the `openziti/zrok` GitHub release and verifies the SHA-256 checksum before extracting `zrok2.exe` to `%LOCALAPPDATA%\DropLink\tools`.
3. If the local zrok environment is not authorized, DropLink opens a native setup dialog. Create or sign in to your own account at <https://myzrok.io/>, copy your personal account token and paste it into the dialog. DropLink passes it to `zrok2 enable`; it does not store the token in its own settings or source files. zrok stores its configuration under the current Windows user profile.
4. DropLink resumes starting the tunnel and reads the HTTPS endpoint reported by zrok. Existing share cards are updated to display public links. Copy the updated link and send it to the recipient.
5. Keep DropLink open while the recipient downloads the file. Stop the tunnel or close DropLink to end public access.

Each user must authorize their own account. Embedding a shared token in a public repository would expose a service credential and make installations share the same account and quota.

## Limits and privacy

- Check the provider's current plan and terms at <https://zrok.io/pricing/> before relying on large transfers. Quotas and interstitial behavior can change.
- The file remains on the sender's PC, but public requests pass through zrok. Throughput is not guaranteed to match a direct LAN connection.
- A share URL is a bearer link: anyone who has it can access the associated file while the share and tunnel are active. Revoke the share when it is no longer needed.
- Public links stop working when the tunnel or DropLink stops.

## Troubleshooting

- **The client cannot be downloaded:** check Internet access and whether GitHub downloads are blocked, then retry activation. The executable is stored in `%LOCALAPPDATA%\DropLink\tools`.
- **The setup dialog appears:** this is expected on first use or when the local zrok identity is not configured. Use your own account token.
- **The token is rejected:** verify that your account is active and copy the full token from <https://myzrok.io/>. Never paste a password or another person's token.
- **No public URL appears:** keep DropLink open while zrok connects and check the displayed error for account, service or network details.
- **The link stops working:** confirm that DropLink and the tunnel are active. Restart the tunnel and resend the newly generated public link if needed.

## Alternatives

- **LAN link:** direct and generally fastest, but only available to devices that can reach the PC on the local network.
- **Custom domain with Caddy:** can use a direct inbound path where DNS, firewall, router and ISP allow it; requires configuration and does not solve CGNAT by itself.
- **zrok temporary tunnel:** avoids port forwarding and works behind many NAT configurations, but public traffic uses the provider's network and is subject to its terms and limits.
