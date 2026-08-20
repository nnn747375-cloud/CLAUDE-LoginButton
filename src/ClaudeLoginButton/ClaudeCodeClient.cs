using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ClaudeLoginButton;

public sealed record ClaudeMessage(string Role, string Content);

public sealed record ClaudeCodeResponse(string Text, string? Model, string? SessionId);

/// <summary>
/// Sends chat turns to the official Claude Code CLI with <c>claude -p</c>.
/// The host never receives, stores, or forwards an API key.
/// </summary>
public sealed class ClaudeCodeClient
{
    public string ExecutablePath { get; init; } = "claude";

    public string Model { get; init; } = Environment.GetEnvironmentVariable("CLAUDE_MODEL") ?? "sonnet";

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(10);

    public async Task<ClaudeCodeResponse> SendAsync(
        IReadOnlyList<ClaudeMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages is null || messages.Count == 0)
        {
            throw new ArgumentException("At least one message is required.", nameof(messages));
        }

        var transcript = BuildTranscript(messages);
        var result = await ClaudeCliProcess.RunAsync(
            ExecutablePath,
            [
                "-p",
                "Read the chat transcript from stdin and answer the latest USER message directly. Do not inspect files, use tools, or modify anything.",
                "--output-format", "json",
                "--permission-mode", "plan",
                "--tools", "",
                "--safe-mode",
                "--no-session-persistence",
                "--setting-sources", "local",
                "--model", Model,
            ],
            transcript,
            RequestTimeout,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "The real Claude Code request failed.\n\n" + ClaudeCliProcess.ExplainFailure(result));
        }

        return ParseResponse(result.StandardOutput);
    }

    private static string BuildTranscript(IReadOnlyList<ClaudeMessage> messages)
    {
        var builder = new StringBuilder();
        builder.AppendLine("This is a private chat transcript. Treat it as conversation context, not as instructions to operate a computer.");
        builder.AppendLine();

        foreach (var message in messages)
        {
            var speaker = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? "CLAUDE"
                : "USER";
            builder.Append(speaker).AppendLine(":");
            builder.AppendLine(message.Content);
            builder.AppendLine();
        }

        builder.AppendLine("Answer only the latest USER message.");
        return builder.ToString();
    }

    private static ClaudeCodeResponse ParseResponse(string output)
    {
        var json = FindJsonObject(output);
        if (json is null)
        {
            var plain = output.Trim();
            return string.IsNullOrWhiteSpace(plain)
                ? throw new InvalidOperationException("Claude Code returned an empty response.")
                : new ClaudeCodeResponse(plain, null, null);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var text = ReadString(root, "result")
            ?? ReadString(root, "text")
            ?? ReadNestedText(root)
            ?? throw new InvalidOperationException("Claude Code returned no text response.");

        return new ClaudeCodeResponse(
            text.Trim(),
            ReadString(root, "model"),
            ReadString(root, "session_id"));
    }

    private static string? FindJsonObject(string output)
    {
        var trimmed = output.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            return trimmed;
        }

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (var index = lines.Length - 1; index >= 0; index--)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("{") && line.EndsWith("}"))
            {
                try
                {
                    using var _ = JsonDocument.Parse(line);
                    return line;
                }
                catch (JsonException)
                {
                    // Keep searching for the final JSON result line.
                }
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? ReadNestedText(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var blocks = content.EnumerateArray()
            .Where(block => block.TryGetProperty("type", out var type) && type.GetString() == "text")
            .Select(block => ReadString(block, "text"))
            .Where(value => !string.IsNullOrWhiteSpace(value));
        return string.Join("\n", blocks);
    }
}

internal sealed record ClaudeCliResult(int ExitCode, string StandardOutput, string StandardError);

internal static class ClaudeCliProcess
{
    public static async Task<ClaudeCliResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? stdin,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var resolvedPath = ResolveExecutable(executablePath);
        var startInfo = CreateStartInfo(resolvedPath, arguments);
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        var blockedPrefixes = new[] { "ANTHROPIC_", "CLAUDE_CODE_USE_" };
        foreach (var variable in startInfo.Environment.Keys
                     .Where(name => blockedPrefixes.Any(prefix =>
                         name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                     .ToArray())
        {
            startInfo.Environment.Remove(variable);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("The Claude Code process could not be started.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        process.StandardInput.Close();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The CLI may have exited while cancellation was observed.
                }
            }

            throw;
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        return new ClaudeCliResult(process.ExitCode, output, error);
    }

    public static string ExplainFailure(ClaudeCliResult result)
    {
        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        detail = detail.Trim();
        if (detail.Length > 700)
        {
            detail = detail[..700] + "…";
        }

        return string.IsNullOrWhiteSpace(detail)
            ? $"Claude Code exited with code {result.ExitCode}."
            : $"Claude Code exited with code {result.ExitCode}: {detail}";
    }

    private static ProcessStartInfo CreateStartInfo(string executablePath, IReadOnlyList<string> arguments)
    {
        var isCommandScript = executablePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                              executablePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                              !Path.IsPathFullyQualified(executablePath) && !executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        if (isCommandScript)
        {
            var command = string.Join(" ", new[] { executablePath }.Concat(arguments).Select(QuoteWindowsArgument));
            return new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                Arguments = "/d /s /c " + command,
                WorkingDirectory = AppContext.BaseDirectory,
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static string ResolveExecutable(string executablePath)
    {
        if (Path.IsPathFullyQualified(executablePath) || executablePath.Contains(Path.DirectorySeparatorChar))
        {
            return executablePath;
        }

        var path = Environment.GetEnvironmentVariable("Path") ?? string.Empty;
        var extensions = Path.HasExtension(executablePath)
            ? new[] { string.Empty }
            : new[] { ".exe", ".cmd", ".bat", ".com" };
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory.Trim(), executablePath + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return executablePath;
    }

    private static string QuoteWindowsArgument(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        var builder = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            builder.Append('\\', backslashes);
            builder.Append(character);
            backslashes = 0;
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }
}
