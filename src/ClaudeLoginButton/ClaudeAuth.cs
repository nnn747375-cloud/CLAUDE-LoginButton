using System.ComponentModel;
using System.Diagnostics;

namespace ClaudeLoginButton;

public enum ClaudeCredentialKind
{
    ApiKey,
    BearerToken,
}

public sealed record ClaudeCredential(string Value, ClaudeCredentialKind Kind);

public sealed record ClaudeAuthSession(string AccountLabel, ClaudeCredential Credential);

public interface IClaudeAuthProvider
{
    Task<ClaudeAuthSession> SignInAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Uses Anthropic's official <c>ant</c> CLI to run the browser OAuth flow and
/// retrieve a short-lived bearer token for the host application.
/// </summary>
public sealed class ClaudeCliAuthProvider : IClaudeAuthProvider
{
    public string ExecutablePath { get; init; } = "ant";

    public TimeSpan LoginTimeout { get; init; } = TimeSpan.FromMinutes(5);

    public async Task<ClaudeAuthSession> SignInAsync(CancellationToken cancellationToken = default)
    {
        Process? loginProcess = null;
        try
        {
            loginProcess = StartInteractive(["auth", "login"]);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(LoginTimeout);
            await loginProcess.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

            if (loginProcess.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"The Claude CLI login ended with exit code {loginProcess.ExitCode}. Try again and finish the browser flow.");
            }
        }
        catch (OperationCanceledException)
        {
            if (loginProcess is { HasExited: false })
            {
                try
                {
                    loginProcess.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The CLI may have exited while cancellation was observed.
                }
            }

            loginProcess?.Dispose();
            throw;
        }
        catch (Win32Exception exception)
        {
            loginProcess?.Dispose();
            throw new InvalidOperationException(
                $"The official Claude CLI was not found. Install 'ant' first, then try again. ({exception.Message})",
                exception);
        }
        catch
        {
            loginProcess?.Dispose();
            throw;
        }

        try
        {
            var token = await RunCapturedAsync(
                ["auth", "print-credentials", "--access-token"],
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    "The Claude CLI did not return an access token. Run 'ant auth status' to inspect the active profile.");
            }

            return new ClaudeAuthSession(
                "Claude connected",
                new ClaudeCredential(token, ClaudeCredentialKind.BearerToken));
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                $"The official Claude CLI was not found. Install 'ant' first, then try again. ({exception.Message})",
                exception);
        }
        finally
        {
            loginProcess?.Dispose();
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await RunCapturedAsync(["auth", "logout"], cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                $"The official Claude CLI was not found. Install 'ant' first, then try again. ({exception.Message})",
                exception);
        }
    }

    private Process StartInteractive(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("The Claude CLI process could not be started.");
    }

    private async Task<string> RunCapturedAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The Claude CLI process could not be started.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var output = await outputTask.ConfigureAwait(false);
        _ = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The Claude CLI command failed with exit code {process.ExitCode}. Check 'ant auth status' for details.");
        }

        return output.Trim();
    }
}

/// <summary>
/// Auth provider for a host that deliberately supplies an Anthropic API key
/// through a secret manager or environment variable.
/// </summary>
public sealed class ClaudeApiKeyAuthProvider : IClaudeAuthProvider
{
    public string EnvironmentVariableName { get; init; } = "ANTHROPIC_API_KEY";

    public Task<ClaudeAuthSession> SignInAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Set {EnvironmentVariableName} in the host environment before connecting.");
        }

        return Task.FromResult(new ClaudeAuthSession(
            "Claude API key connected",
            new ClaudeCredential(value, ClaudeCredentialKind.ApiKey)));
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
