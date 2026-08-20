# Live local browser host

The plain `docs/` folder is safe to host as a UI preview, but a browser must
not receive a Claude credential. This tiny Node host keeps the credential on
the local machine and connects the button to a real Claude request.

## Requirements

1. Node.js 18 or newer.
2. Anthropic's official `ant` CLI on `PATH`, or an `ANTHROPIC_API_KEY` in the
   host environment.

Install the CLI using the instructions in the official
[Claude CLI quickstart](https://platform.claude.com/docs/en/cli-sdks-libraries/cli/quickstart).

## Run it

From the repository root:

```powershell
node examples/LocalHost/server.mjs
```

Open <http://127.0.0.1:4173>. Click `Continue with Claude`, finish the browser
login, then send a message. The server calls `ant auth print-credentials` for
the short-lived bearer token and sends the request to the Claude Messages API;
the token is never sent to the browser or returned by a local endpoint.

For API-key mode, set the key only in the host process and choose a model:

```powershell
$env:ANTHROPIC_API_KEY = "..."
$env:ANTHROPIC_MODEL = "claude-sonnet-5"
node examples/LocalHost/server.mjs
```

Do not commit the key or put it in `docs/app.js`.
