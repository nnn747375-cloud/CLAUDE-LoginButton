# Security boundary

`CLAUDE-LoginButton` is a UI control. It does not receive, store, refresh or
forward credentials. The host application owns the authentication flow.

For a desktop flow, use the provider-supported authorization method and apply
current OAuth guidance: fresh state and PKCE values per attempt, exact
redirect validation, one-time callback handling, secure credential storage,
minimal permissions and redacted diagnostics.

Never commit client secrets, access tokens, refresh tokens, cookies,
authorization codes, private keys or real account screenshots.

## Browser demo boundary

The interactive page in `docs/` is safe to publish as a UI demo. It can open
the official Claude sign-in page, but it never reads that page's cookies,
captures a callback, or pretends that a click proves authentication. A live
host may provide `window.CLAUDE_AUTH_URL` and
`window.CLAUDE_CHAT_ENDPOINT`; those endpoints must implement and validate
the provider-supported flow on the server.

The Claude API documentation currently describes Console API keys or
short-lived workload-identity tokens for programmatic access. Never put either
credential in this static page.
