# CLAUDE-LoginButton

<p align="center">
  <a href="https://nnn747375-cloud.github.io/CLAUDE-LoginButton/">
    <img src="docs/preview.svg" alt="Open the interactive Claude login button and mini chat demo" width="820" />
  </a>
</p>

<p align="center">
  <strong>A Claude-styled, reusable WinForms login surface with an interactive mini-chat demo.</strong><br />
  <sub>Independent · host-owned auth · not affiliated with Anthropic</sub>
</p>

<p align="center">
  <a href="https://github.com/nnn747375-cloud/CLAUDE-LoginButton/actions/workflows/ci.yml"><img src="https://github.com/nnn747375-cloud/CLAUDE-LoginButton/actions/workflows/ci.yml/badge.svg" alt="Build status" /></a>
  <img src="https://img.shields.io/badge/.NET-9.0-512BD4" alt=".NET 9" />
  <img src="https://img.shields.io/badge/WinForms-supported-7A2E12" alt="WinForms" />
  <img src="https://img.shields.io/badge/license-MIT-176B5B" alt="MIT license" />
</p>

## Open the interactive demo

Use the [interactive Claude demo](https://nnn747375-cloud.github.io/CLAUDE-LoginButton/) to try the actual button states and mini chat. The page is responsive, keyboard-friendly and uses no credentials in the browser.

The `Open Claude sign-in` action opens the official Claude page in a separate tab. A static GitHub page cannot read Claude.ai cookies or turn that browser session into API access. For real account authorization, connect the button to an approved host-side auth flow.

## The important boundary

This repository contains a UI control and integration guidance. The button
does **not** authenticate users by itself, issue tokens, store credentials or
grant access to Claude services. Your application supplies the auth service.

That separation keeps the control reusable and prevents credentials from
leaking into a UI library.

## What is included

- `ClaudeLoginButton`: signed-out, signing-in, connected and error states
- keyboard activation, visible focus and accessible naming
- Claude-inspired mark, typography and state styling with no provider credential handling
- an interactive browser demo with a small local mini chat
- a WinForms demo with the same login surface and mini-chat layout
- source project, documentation and CI build

## Requirements

- Windows 10 or newer
- .NET 9 Windows Forms
- your own provider-supported authorization flow
- an exact, configured callback/redirect URI
- secure session storage owned by the host application

## Install from source

```bash
git clone https://github.com/nnn747375-cloud/CLAUDE-LoginButton.git
dotnet build CLAUDE-LoginButton/src/ClaudeLoginButton/ClaudeLoginButton.csproj
```

Or add the project reference to your WinForms application:

```xml
<ProjectReference Include="path/to/ClaudeLoginButton.csproj" />
```

## Five-minute integration

```csharp
using ClaudeLoginButton;

var button = new ClaudeLoginButton
{
    Dock = DockStyle.Top,
};

button.LoginRequested += async (_, _) =>
{
    button.SetSigningIn();

    try
    {
        // Your app starts and validates the provider-supported auth flow.
        var session = await claudeAuth.StartAuthorizationAsync();
        button.SetConnected(session.DisplayName ?? "Claude connected");
    }
    catch (OperationCanceledException)
    {
        button.SetSignedOut();
    }
    catch (Exception)
    {
        button.SetError();
        // Log only redacted diagnostics.
    }
};

button.LogoutRequested += async (_, _) =>
{
    await claudeAuth.SignOutAsync();
    button.SetSignedOut();
};

Controls.Add(button);
```

Use `await` through the auth flow. Do not call `.Wait()` or `.Result` on the
WinForms UI thread.

## Expected flow

1. The user activates the button.
2. Your application creates a fresh authorization request.
3. The provider page opens in the browser.
4. Your callback handler validates the returned data.
5. Your application creates a secure session.
6. Only then does the host call `SetConnected(...)`.

A button click is never proof of authentication.

## Security checklist

- generate fresh `state` and PKCE values for every attempt
- validate the callback exactly once
- register exact redirect URIs; do not accept arbitrary destinations
- never embed a client secret in a desktop executable
- never put codes or tokens in logs, URLs, screenshots or analytics
- store credentials in an appropriate secure store
- request only the permissions your host actually needs
- treat cancellation, expiry and denial as separate outcomes

Read the full [security boundary](docs/security.md).

## Demos

The browser demo in `docs/` has real interactive button states and a small
local mini chat. It does not contact a provider or contain credentials. Run
it locally with:

```bash
python -m http.server 4173 --directory docs
```

Then open `http://localhost:4173`.

The WinForms demo also intentionally simulates a successful result. It does
not contact a provider and does not contain credentials:

```bash
dotnet run --project examples/WinFormsDemo/WinFormsDemo.csproj
```

For a host-owned live flow, configure the browser adapter before loading the
page:

```html
<script>
  window.CLAUDE_AUTH_URL = "/auth/start";
  window.CLAUDE_CHAT_ENDPOINT = "/api/chat";
</script>
```

Those endpoints must run server-side. Do not put an Anthropic API key in a
static page; see the [Claude API authentication docs](https://platform.claude.com/docs/en/api/overview#authentication).

## Troubleshooting

**Clicking does nothing:** confirm the control is in the active form and that
`LoginRequested` is subscribed.

**The callback is not received:** verify scheme, host, port, path and trailing
slash against the registered redirect URI. Check firewall, proxy and port use.

**The UI freezes:** remove `.Wait()`/`.Result` from the UI thread and await the
auth service from the event boundary.

**The app remains disconnected:** call `SetConnected(...)` only after your app
has validated issuer, audience, expiry, permissions and session state.

## Repository boundary

This repo deliberately excludes the local ClaudeLocal desktop app, chat
history, account data, browser profiles, tokens, cookies and any auth backend.

## License

MIT. Claude and Anthropic are trademarks of Anthropic PBC. This project is
independent and is not endorsed by or affiliated with Anthropic.
