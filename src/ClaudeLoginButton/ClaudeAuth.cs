using System.ComponentModel;

namespace ClaudeLoginButton;

public sealed record ClaudeAuthSession(string AccountLabel);

public interface IClaudeAuthProvider
{
    Task<ClaudeAuthSession> SignInAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Connects through the Claude Code CLI already installed and authenticated on
/// the user's computer. No API key is read or stored by this library.
/// </summary>
public class ClaudeCodeCliAuthProvider : IClaudeAuthProvider
{
    public string ExecutablePath { get; init; } = "claude";

    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromMinutes(5);

    public async Task<ClaudeAuthSession> SignInAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ClaudeCliProcess.RunAsync(
                ExecutablePath,
                [
                    "-p",
                    "Reply with exactly CLAUDE_LOGIN_OK. Do not inspect files and do not use tools.",
                    "--output-format", "json",
                    "--permission-mode", "plan",
                    "--tools", "",
                    "--safe-mode",
                    "--no-session-persistence",
                    "--setting-sources", "local",
                ],
                stdin: null,
                ConnectionTimeout,
                cancellationToken).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Claude Code could not connect. Run 'claude' once in PowerShell to finish the browser login, then click Continue with Claude again.\n\n" +
                    ClaudeCliProcess.ExplainFailure(result));
            }

            return new ClaudeAuthSession("Claude Code connected");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "Claude Code was not found. Install it with: npm install -g @anthropic-ai/claude-code",
                exception);
        }
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Disconnecting this host must not log the user out of Claude Code on
        // the whole computer. The local app session is cleared by the host.
        return Task.CompletedTask;
    }
}

// Kept as a small compatibility alias for hosts that already used the original
// name. Both names use Claude Code directly; neither handles API keys.
public sealed class ClaudeCliAuthProvider : ClaudeCodeCliAuthProvider
{
}
