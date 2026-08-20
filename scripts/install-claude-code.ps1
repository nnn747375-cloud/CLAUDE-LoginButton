$ErrorActionPreference = "Stop"

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw "Node.js 18+ is required. Install it from https://nodejs.org/ first."
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm is required. Reinstall Node.js 18+ and open a new PowerShell window."
}

Write-Host "Installing the official Claude Code package..."
& npm install -g @anthropic-ai/claude-code
if ($LASTEXITCODE -ne 0) {
    throw "Claude Code installation failed with exit code $LASTEXITCODE."
}

Write-Host "Claude Code is installed. Run 'claude' in a new PowerShell window to finish login."
