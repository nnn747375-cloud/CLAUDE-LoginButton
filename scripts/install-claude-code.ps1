$ErrorActionPreference = 'Stop'
if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    throw 'Install Claude Code from https://code.claude.com/docs/en/setup, then run Launch.cmd.'
}
Write-Host 'Installing the official native Claude Code package...'
& winget install --id Anthropic.ClaudeCode --exact --source winget
if ($LASTEXITCODE -ne 0) { throw "Claude Code installation exited with code $LASTEXITCODE." }
Write-Host 'Open Launch.cmd and click Continue with Claude to sign in.'
