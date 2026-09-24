$ErrorActionPreference = 'Stop'
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'Install the .NET 9 SDK from https://dotnet.microsoft.com/download/dotnet/9.0, then launch again.'
    }
    $repoDirectory = Split-Path -Parent $PSScriptRoot
    $projectFile = Join-Path $repoDirectory 'examples\WinFormsDemo\WinFormsDemo.csproj'
    Write-Host 'Building Claude Studio...'
    & dotnet build $projectFile --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'The build failed. See the output above.' }
    $demoFile = Join-Path $repoDirectory 'examples\WinFormsDemo\bin\Release\net9.0-windows\WinFormsDemo.exe'
    Start-Process -FilePath $demoFile -WorkingDirectory $repoDirectory
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    Read-Host 'Press Enter to close'
    exit 1
}
