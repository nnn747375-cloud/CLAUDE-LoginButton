# Contributing

Keep changes focused on the reusable control and its documentation. Do not
add provider credentials, browser cookies, personal account data or files from
another application.

Before opening a pull request:

```bash
dotnet build src/ClaudeLoginButton/ClaudeLoginButton.csproj -c Release
dotnet build examples/WinFormsDemo/WinFormsDemo.csproj -c Release
```

Describe accessibility, keyboard and state changes in the pull request.
