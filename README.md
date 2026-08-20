# CLAUDE-LoginButton

<p align="center">
  <strong>Real Claude sign-in for Windows Forms — using the local Claude Code CLI.</strong><br />
  <sub>Community-maintained · independent · not affiliated with Anthropic</sub>
</p>

> [!WARNING]
> ## READ THIS BEFORE YOU RUN IT
>
> This demo needs **Windows 10+, .NET 9, Node.js 18+ with `npm`, Claude Code,
> and a Claude.ai Pro or Max account** for the no-API-key login path.
> Install Claude Code, run `claude` once, finish the browser login, and then
> start the demo. **Do not paste an API key into this project.**
>
> Claude Code may use Windows with Git for Windows/Git Bash or WSL depending on
> your setup. The account's plan, model access and usage limits still apply.
> This project launches the real `claude` CLI already installed on your PC;
> it never copies tokens, cookies or Claude auth files into the app.

## What this repository actually contains

- `ClaudeLoginButton`: reusable, keyboard-accessible WinForms control.
- A real local Claude Code demo: browser login, connection check, model label and chat.
- A source-only Windows Forms demo that sends turns through `claude -p`.

The demo does not simulate a successful login or generate a fake answer. The
button becomes connected only after Claude Code is found and a real, restricted
CLI request succeeds.

## Run the demo

Install [Node.js 18+](https://nodejs.org/) first. Then install the official
Claude Code CLI:

```powershell
npm install -g @anthropic-ai/claude-code
```

Start Claude Code once and finish the Claude.ai browser login:

```powershell
claude
```

Then run the real source demo:

```powershell
dotnet run --project examples/WinFormsDemo/WinFormsDemo.csproj
```

The demo invokes the local CLI with `claude -p --output-format json` and uses
the existing Claude Code account session. Disconnecting the button only clears
this demo's state; it does not log you out of Claude Code on Windows.

## Use the control in another WinForms app

Reference `src/ClaudeLoginButton/ClaudeLoginButton.csproj`:

```xml
<ProjectReference Include="path/to/ClaudeLoginButton.csproj" />
```

The control owns only presentation and events. Your host decides how the real
Claude Code connection is handled:

```csharp
var button = new ClaudeLoginButton
{
    Dock = DockStyle.Top,
};

var auth = new ClaudeCodeCliAuthProvider();

button.LoginRequested += async (_, _) =>
{
    button.SetSigningIn();
    try
    {
        var session = await auth.SignInAsync();
        button.SetConnected(session.AccountLabel);
    }
    catch (OperationCanceledException)
    {
        button.SetSignedOut();
    }
    catch (Exception error)
    {
        button.SetError(error.Message);
    }
};

button.LogoutRequested += async (_, _) =>
{
    await auth.SignOutAsync();
    button.SetSignedOut();
};

Controls.Add(button);
```

## Security boundary

- This project never asks the user to paste an API key.
- The official Claude Code CLI owns browser authentication and local credentials.
- The host only starts `claude -p` with restricted, non-interactive options.
- The project removes API-key and cloud-provider environment variables before launching the CLI.
- Never commit Claude auth files, tokens, cookies, screenshots or callback URLs.
- Use your own account and follow Anthropic's terms and usage limits.

See [SECURITY.md](SECURITY.md) for the short reporting policy.

## Build and test

```powershell
dotnet build src/ClaudeLoginButton/ClaudeLoginButton.csproj --configuration Release
dotnet build examples/WinFormsDemo/WinFormsDemo.csproj --configuration Release
```

The live request requires a signed-in Claude Code account and the `claude`
command on `PATH`. A source build alone cannot prove the account path; the demo
must be connected and used.

## Official references

- [Claude Code setup](https://docs.anthropic.com/en/docs/claude-code/getting-started)
- [Claude Code CLI reference](https://docs.anthropic.com/en/docs/claude-code/cli-usage)

## License

MIT. Claude and Anthropic are trademarks of Anthropic. This project is
independent and is not endorsed by or affiliated with Anthropic.
