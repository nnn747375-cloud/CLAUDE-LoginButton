using System.Text.Json;
using ClaudeLoginButton;

internal static class Program
{
    private const string Session = "12345678-1234-1234-1234-123456789abc";
    private static int _passed;
    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label); _passed++;
    }
    private static async Task Throws<T>(Func<Task> run, string label) where T : Exception
    {
        try { await run(); } catch (T) { Check(true, label); return; }
        throw new Exception("FAIL: " + label);
    }
    private static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        if (args.Contains("--help")) { Console.WriteLine("--restricted --safe-mode --resume"); return 0; }
        if (args.Contains("auth") || args.Contains("-p")) return await FakeCli(args);
        if (args.Contains("--live")) return await Live();
        var exe = Environment.ProcessPath!;
        var workspace = Path.Combine(Path.GetTempPath(), "ClaudeLoginButton-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        Environment.SetEnvironmentVariable("CLAUDE_TEST_WORKSPACE", workspace);
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-not-a-key");
            var progress = new Collector();
            var client = new ClaudeCodeClient { ExecutablePath = exe, WorkspacePath = workspace };
            var result = await client.SendTurnAsync("quotes \" ; & | % ! ü\nsecond line", progress: progress);
            Check(result.Text == "safe prompt received" && result.SessionId == Session, "UTF-8 stdin, structured result and session ID");
            Check(progress.Items.Any(x => x.Kind == "tool" && x.Label == "Read") && progress.Items.Any(x => x.Kind == "tool-done"), "real stream tool lifecycle parsing");
            await client.SendTurnAsync("continue", result.SessionId);
            Check(true, "resume ID passed explicitly");
            Environment.SetEnvironmentVariable("CLAUDE_TEST_CASE", "no-tools");
            await new ClaudeCodeClient { ExecutablePath = exe }.SendTurnAsync("plain chat");
            Check(true, "no folder means no tools");
            Environment.SetEnvironmentVariable("CLAUDE_TEST_CASE", "error-result");
            await Throws<InvalidOperationException>(() => client.SendTurnAsync("error"), "is_error result rejects exit-zero false success");
            Environment.SetEnvironmentVariable("CLAUDE_TEST_CASE", "missing-result");
            await Throws<InvalidOperationException>(() => client.SendTurnAsync("empty"), "missing final result is rejected");
            Environment.SetEnvironmentVariable("CLAUDE_TEST_CASE", "wait");
            using (var cancel = new CancellationTokenSource(700))
                await Throws<OperationCanceledException>(() => client.SendTurnAsync("stop", cancellationToken: cancel.Token), "cancellation terminates an active process");
            await Throws<TimeoutException>(() => new ClaudeCodeClient { ExecutablePath = exe, RequestTimeout = TimeSpan.FromMilliseconds(500) }.SendTurnAsync("timeout"), "timeout differs from cancellation");
            Environment.SetEnvironmentVariable("CLAUDE_TEST_CASE", "auth");
            var auth = new ClaudeCodeCliAuthProvider { ExecutablePath = exe };
            await auth.SignInAsync();
            Check(File.Exists(Path.Combine(workspace, "fake-auth-complete")), "signed-out state launches official login command and rechecks");
            await auth.SignInAsync();
            Check(File.ReadAllText(Path.Combine(workspace, "fake-auth-complete")) == "1", "existing login is reused without logging in again");
            await auth.SignOutAsync();
            Check(File.Exists(Path.Combine(workspace, "fake-auth-complete")), "disconnect does not log out the CLI");
            await Throws<ArgumentException>(() => client.SendTurnAsync("x", "bad;id"), "invalid session ID rejected before launch");
            Console.WriteLine($"{_passed} checks passed. These are protocol tests, not live service proof.");
            return 0;
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
            Environment.SetEnvironmentVariable("CLAUDE_TEST_WORKSPACE", null);
            Environment.SetEnvironmentVariable("CLAUDE_TEST_CASE", null);
            Directory.Delete(workspace, true);
        }
    }
    private static async Task<int> FakeCli(string[] args)
    {
        var mode = Environment.GetEnvironmentVariable("CLAUDE_TEST_CASE");
        var workspace = Environment.GetEnvironmentVariable("CLAUDE_TEST_WORKSPACE")!;
        if (args.Contains("auth"))
        {
            var marker = Path.Combine(workspace, "fake-auth-complete");
            if (args.Contains("login"))
            {
                if (!args.Contains("--claudeai")) return 8;
                File.WriteAllText(marker, File.Exists(marker) ? "2" : "1"); return 0;
            }
            var ready = File.Exists(marker);
            Console.WriteLine(JsonSerializer.Serialize(new { loggedIn = ready, authMethod = "claude.ai" }));
            return ready ? 0 : 1;
        }
        var stdin = await Console.In.ReadToEndAsync();
        if (mode == "wait") { await Task.Delay(30000); return 0; }
        if (mode == "missing-result") { Console.WriteLine("noise"); return 0; }
        if (Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is not null) return 10;
        if (!args.Contains("--restricted") || !args.Contains("--safe-mode") || !args.Contains("--strict-mcp-config") || args.Contains("--dangerously-skip-permissions")) return 11;
        var tools = args[Array.IndexOf(args, "--tools") + 1];
        if (mode == "no-tools" ? tools != "" : tools != "Read,Glob,Grep" || Environment.CurrentDirectory != workspace) return 12;
        if (stdin == "continue" && (!args.Contains("--resume") || args[Array.IndexOf(args, "--resume") + 1] != Session)) return 13;
        if (stdin.StartsWith("quotes") && stdin != "quotes \" ; & | % ! ü\nsecond line") return 14;
        Console.WriteLine(JsonSerializer.Serialize(new { type = "system", subtype = "init", model = "fixture-model", session_id = Session }));
        Console.WriteLine("{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"Read\",\"input\":{\"file_path\":\"notes.txt\"}}]}}");
        Console.WriteLine("{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"t1\",\"content\":\"fixture\"}]}}");
        Console.WriteLine(JsonSerializer.Serialize(new { type = "result", subtype = mode == "error-result" ? "error_during_execution" : "success", is_error = mode == "error-result", result = "safe prompt received", session_id = Session }));
        return 0;
    }
    private static async Task<int> Live()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ClaudeLoginButton-live-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var marker = "maple-" + Guid.NewGuid().ToString("N")[..8];
        File.WriteAllText(Path.Combine(folder, "demo.txt"), "The verification word is " + marker + ".");
        try
        {
            var account = await new ClaudeCodeCliAuthProvider().SignInAsync();
            Check(account is not null, "live official CLI authentication status");
            var events = new Collector();
            var client = new ClaudeCodeClient { WorkspacePath = folder };
            var first = await client.SendTurnAsync("Read demo.txt using the Read tool. Reply with only the verification word from the file.", progress: events);
            Check(first.Text.Contains(marker), "live inference reads actual fixture content");
            Check(events.Items.Any(x => x.Kind == "tool" && x.Label == "Read"), "live Read event observed");
            Check(!string.IsNullOrEmpty(first.SessionId), "live session ID received");
            var second = await client.SendTurnAsync("Repeat the verification word from the previous turn, without reading the file again.", first.SessionId);
            Check(second.Text.Contains(marker) && second.SessionId == first.SessionId, "live conversation continuation retains context");
            Console.WriteLine($"{_passed} LIVE checks passed using the actual Claude service.");
            return 0;
        }
        finally { Directory.Delete(folder, true); }
    }
    private sealed class Collector : IProgress<ClaudeActivity>
    {
        public List<ClaudeActivity> Items { get; } = [];
        public void Report(ClaudeActivity item) { lock (Items) Items.Add(item); }
    }
}
