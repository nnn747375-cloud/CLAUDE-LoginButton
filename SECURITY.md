# Security

The demo calls the official `claude` CLI installed on the user's own PC. It
does not read, copy, or store API keys, OAuth tokens, cookies, or browser data.
Claude Code owns authentication and its local credential storage.

Never commit `.env` files, credentials, tokens, account screenshots, or
private chat content. Disconnecting the demo only clears this app's session;
it deliberately does not log out Claude Code system-wide.

If you find a security issue, use a private GitHub security advisory. Do not
publish tokens or personal data in an issue.
