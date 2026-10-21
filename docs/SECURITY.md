# Security notes

## Threat model

DropLink is a small personal file server. The user explicitly selects a file, and a random token grants access to that one file while the application remains open. The source computer is the data source.

## Implemented controls

- 16-byte token from the operating system cryptographic random generator, encoded as 32 lowercase hex characters.
- Strict token validation; recipient endpoints never accept a filesystem path.
- One share record maps to one selected file; no directory listing or arbitrary filesystem route exists.
- HTML escaping for names and Unicode-safe `Content-Disposition`.
- `HEAD` and single byte-range handling; streaming with a 64 KiB buffer and concurrent download limit.
- Browser security headers and no-store caching.
- Revocation removes the share from the token map and the streaming loop rechecks whether a share remains active.
- No telemetry and no cloud file upload.

## Public Internet usage

- The embedded DropLink server uses HTTP on its local port. For public sharing, use Caddy as the HTTPS reverse proxy. Caddy listens publicly on TCP 80/443 and proxies to the local DropLink server; only use the public `https://` links generated from the configured domain.
- Configure DNS/DDNS and router forwarding to the PC. Do not forward DropLink's internal port directly from the router. Keep Windows Firewall enabled.
- Caddy needs a valid public hostname and inbound TCP 80/443 (or a separately configured DNS challenge) to obtain a publicly trusted certificate. If Caddy is not running or cannot obtain a certificate, public links will not work securely.
- A random share token is a bearer capability, not complete authentication. Anyone who receives a link can download that file until the share is revoked or the app exits.
- No password protection, expiry, download count limit, persistent transfer history or per-download audit log is implemented yet.
- Public reachability and CGNAT are not conclusively detected by the app. The DNS check only resolves the configured host; use the network diagnostic script and test from mobile data.
- The local HTTP URL is unencrypted. Do not use it for confidential content on untrusted networks.
- File size is checked before service and before streaming. File contents can still change without changing size; avoid editing a file while it is shared.

## Reporting

For a private security report, do not attach confidential files. Include the application version, Windows version, steps to reproduce and relevant logs with tokens and personal paths removed.
