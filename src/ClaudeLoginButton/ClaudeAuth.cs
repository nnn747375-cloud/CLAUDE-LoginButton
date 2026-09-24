using System.Text.Json;
namespace ClaudeLoginButton;
public sealed record ClaudeAuthSession(string AccountLabel);
public interface IClaudeAuthProvider
{
    Task<ClaudeAuthSession> SignInAsync(CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
}
/// <summary>Official CLI browser sign-in, with no token handling in the host.</summary>
public class ClaudeCodeCliAuthProvider : IClaudeAuthProvider
{
    public string ExecutablePath { get; init; } = "claude";
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public IProgress<string>? Progress { get; init; }
    public async Task<ClaudeAuthSession> SignInAsync(CancellationToken cancellationToken = default)
    {
        Progress?.Report("Checking Claude Code…");
        await ClaudeCliProcess.CheckCapabilitiesAsync(ExecutablePath, cancellationToken).ConfigureAwait(false);
        if (await IsSignedInAsync(cancellationToken).ConfigureAwait(false)) return new("Claude account signed in");
        Progress?.Report("Finish signing in in your browser. This window will reconnect automatically.");
        var result = await ClaudeCliProcess.RunAsync(ExecutablePath, ["auth", "login", "--claudeai"], null,
            ConnectionTimeout, cancellationToken).ConfigureAwait(false);
        // Do not display raw OAuth output or callback URLs in the host.
        if (result.ExitCode != 0) throw new InvalidOperationException("Browser sign-in did not finish. Try again, or run 'claude auth login --claudeai' in a terminal.");
        if (!await IsSignedInAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Claude Code has no signed-in subscription account yet. Finish browser sign-in and try again.");
        return new("Claude account signed in");
    }
    private async Task<bool> IsSignedInAsync(CancellationToken token)
    {
        var result = await ClaudeCliProcess.RunAsync(ExecutablePath, ["auth", "status", "--json"], null,
            TimeSpan.FromSeconds(20), token).ConfigureAwait(false);
        if (result.ExitCode == 1) return false;
        if (result.ExitCode != 0) throw new InvalidOperationException("Claude could not check your account. Run 'claude auth status' in a terminal to diagnose it.");
        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            return root.TryGetProperty("loggedIn", out var loggedIn) && loggedIn.ValueKind == JsonValueKind.True &&
                root.TryGetProperty("authMethod", out var method) && method.GetString() == "claude.ai";
        }
        catch (JsonException ex) { throw new InvalidOperationException("Claude returned an unreadable account status. Update Claude Code and try again.", ex); }
    }
    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
public sealed class ClaudeCliAuthProvider : ClaudeCodeCliAuthProvider { }
