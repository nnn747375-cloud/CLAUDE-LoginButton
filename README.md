# CLAUDE-LoginButton

<div style="border:5px solid #b42318; padding:22px; background:#fff1f0; color:#641b16;">
<h1>⚠️ WARNING — REAL CLAUDE CODE REQUIRED</h1>
<h2>NO API KEY. NO FAKE CHAT. CLAUDE CODE MUST BE INSTALLED AND LOGGED IN.</h2>
<p>This demo uses the official <code>claude</code> CLI on your own Windows PC. For the subscription flow, you need a Claude.ai account with at least a Pro or Max plan. The EXE does not contain Claude Code or your login.</p>
<pre>npm install -g @anthropic-ai/claude-code
claude</pre>
<p>Run <code>claude</code> once and finish the browser login. Then start the EXE.</p>
</div>

An independent WinForms button and a tiny real chat host. Every reply comes from `claude -p` through the CLI installed on your PC. This project is not affiliated with or endorsed by Anthropic.

## Start

1. Install [Node.js 18+](https://nodejs.org/) and Claude Code:

   ```powershell
   npm install -g @anthropic-ai/claude-code
   ```

2. Log in once:

   ```powershell
   claude
   ```

3. Download `dist/ClaudeLoginButton.exe`, click **Continue with Claude**, and chat.

The app only clears its own local connection state when you click disconnect. It does not log you out of Claude Code.

## Build the EXE

```powershell
dotnet publish examples/WinFormsDemo/WinFormsDemo.csproj `
  --configuration Release --runtime win-x64 --self-contained true --output dist
```

The output is one self-contained .NET file: `dist/ClaudeLoginButton.exe`. Claude Code remains a required external dependency because it owns the account login.

## What is inside

- `src/ClaudeLoginButton` — reusable button control and local CLI integration
- `examples/WinFormsDemo` — real chat demo
- `scripts/install-claude-code.ps1` — optional Node/npm setup helper
- `SECURITY.md` — credential handling notes

The demo uses `claude -p --output-format json` with the conversation sent through stdin. It does not read `ANTHROPIC_API_KEY`, copy tokens, or ship a simulated response.

Official setup: [Claude Code](https://docs.anthropic.com/en/docs/claude-code/getting-started) · [CLI reference](https://docs.anthropic.com/en/docs/claude-code/cli-usage)

MIT licensed. See [LICENSE](LICENSE).
