# Security boundary

`CLAUDE-LoginButton` is a UI control plus optional host-side helpers. The
control never receives or stores credentials. `ClaudeCliAuthProvider` delegates
the browser flow to Anthropic's official `ant` CLI; the host asks the CLI for a
short-lived token only when it needs to make an API request.

For a desktop flow, use the provider-supported authorization method and apply
current OAuth guidance: fresh state and PKCE values per attempt, exact
redirect validation, one-time callback handling, secure credential storage,
minimal permissions and redacted diagnostics.

Never commit client secrets, access tokens, refresh tokens, cookies,
authorization codes, private keys or real account screenshots.

## Browser demo boundary

The interactive page in `docs/` is safe to publish as a UI demo. It can open
the official Claude sign-in page, but it never reads that page's cookies or
receives a token. The included `examples/LocalHost/server.mjs` is the real
local host: it starts `ant auth login`, keeps the credential server-side, and
proxies only sanitized chat requests to the Messages API. A custom host may
provide `window.CLAUDE_AUTH_URL`, `window.CLAUDE_AUTH_STATUS_URL`,
`window.CLAUDE_AUTH_LOGOUT_URL` and `window.CLAUDE_CHAT_ENDPOINT`; those
endpoints must implement the same boundary.

The Claude API documentation currently describes Console API keys or
short-lived workload-identity tokens for programmatic access. Never put either
credential in this static page.
