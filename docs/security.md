# Security boundary

`CLAUDE-LoginButton` is a UI control. It does not receive, store, refresh or
forward credentials. The host application owns the authentication flow.

For a desktop flow, use the provider-supported authorization method and apply
current OAuth guidance: fresh state and PKCE values per attempt, exact
redirect validation, one-time callback handling, secure credential storage,
minimal permissions and redacted diagnostics.

Never commit client secrets, access tokens, refresh tokens, cookies,
authorization codes, private keys or real account screenshots.
