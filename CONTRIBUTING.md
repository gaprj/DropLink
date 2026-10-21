# Contributing

1. Use a current .NET 10 SDK on Windows.
2. Keep the WPF desktop application independent of browser-rendered UI. The recipient-facing download page is a separate, minimal HTML response from the HTTP server.
3. Do not add a file relay or cloud upload to the direct-transfer mode.
4. Add automated tests for security-sensitive behavior (tokens, URL validation, revocation, path handling, HTTP ranges and errors).
5. Do not claim public Internet reachability or HTTPS until tested with evidence.
6. Never commit access tokens, private certificates, DDNS credentials, personal files or local share links.
