using System.Diagnostics;
using ClaudeLoginButton;

namespace ClaudeLoginButton.Demo;

internal sealed class DemoForm : Form
{
    private readonly IClaudeAuthProvider _authProvider = new ClaudeCodeCliAuthProvider();
    private readonly ClaudeLoginButton _button = new();
    private readonly Label _status = new();
    private readonly Label _modelStatus = new();
    private readonly FlowLayoutPanel _messages = new();
    private readonly TextBox _composer = new();
    private readonly Button _send = new();
    private readonly List<ClaudeMessage> _history = [];
    private readonly ClaudeCodeClient _claudeClient = new();
    private ClaudeAuthSession? _session;
    private bool _requestInFlight;

    public DemoForm()
    {
        Text = "CLAUDE-LoginButton · real Claude Code demo";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 780);
        MinimumSize = new Size(980, 780);
        BackColor = Color.FromArgb(24, 26, 25);

        var warningCard = new Panel
        {
            BackColor = Color.FromArgb(78, 32, 25),
            Location = new Point(24, 20),
            Size = new Size(932, 122),
            Padding = new Padding(22, 15, 22, 12),
        };
        var warningTitle = MakeLabel("⚠  WARNING", 23f, FontStyle.Bold, Color.FromArgb(255, 224, 207), "Georgia");
        warningTitle.Location = new Point(22, 13);
        warningTitle.Size = new Size(250, 36);

        var warningCopy = MakeLabel(
            "THIS IS A REAL CLAUDE CODE CLIENT. No fake replies and no API-key field. Your messages go through the official claude CLI already logged into your account.",
            10f,
            FontStyle.Bold,
            Color.FromArgb(255, 246, 238));
        warningCopy.Location = new Point(24, 52);
        warningCopy.Size = new Size(870, 24);

        var warningCommand = MakeLabel(
            "FIRST SETUP:  npm install -g @anthropic-ai/claude-code   ·   then run: claude",
            9f,
            FontStyle.Bold,
            Color.FromArgb(255, 185, 153),
            "Consolas");
        warningCommand.Location = new Point(24, 86);
        warningCommand.Size = new Size(870, 22);

        var warningFootnote = MakeLabel(
            "Minimum subscription login: Claude.ai Pro (or Max) · account limits apply · unofficial project",
            8f,
            FontStyle.Regular,
            Color.FromArgb(218, 170, 151));
        warningFootnote.Location = new Point(24, 105);
        warningFootnote.Size = new Size(870, 18);
        warningCard.Controls.AddRange([warningTitle, warningCopy, warningCommand, warningFootnote]);

        var authCard = new Panel
        {
            BackColor = Color.FromArgb(247, 243, 237),
            Location = new Point(24, 162),
            Size = new Size(318, 592),
        };
        var eyebrow = MakeLabel("LOCAL CLAUDE CODE", 9f, FontStyle.Bold, Color.FromArgb(122, 46, 18));
        eyebrow.Location = new Point(24, 24);
        eyebrow.AutoSize = true;

        var title = MakeLabel("A softer way in.", 27f, FontStyle.Regular, Color.FromArgb(41, 38, 36), "Georgia");
        title.Location = new Point(24, 70);
        title.Size = new Size(270, 46);

        var copy = MakeLabel(
            "This button checks the real Claude Code CLI on your PC. It reuses the login Claude Code already stores for your account.",
            10f,
            FontStyle.Regular,
            Color.FromArgb(111, 101, 93));
        copy.Location = new Point(26, 132);
        copy.Size = new Size(266, 86);

        _button.Location = new Point(24, 252);
        _button.Size = new Size(270, 58);
        _button.LoginRequested += async (_, _) => await SignInAsync();
        _button.LogoutRequested += async (_, _) => await SignOutAsync();

        var openClaude = new LinkLabel
        {
            AutoSize = true,
            Text = "Claude Code setup ↗",
            LinkColor = Color.FromArgb(122, 46, 18),
            ActiveLinkColor = Color.FromArgb(48, 93, 70),
            Location = new Point(26, 344),
        };
        openClaude.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("https://docs.anthropic.com/en/docs/claude-code/getting-started") { UseShellExecute = true });

        _status.AutoSize = false;
        _status.Text = "Install Claude Code, run 'claude' once to log in, then click the button.";
        _status.ForeColor = Color.FromArgb(111, 101, 93);
        _status.Location = new Point(26, 404);
        _status.Size = new Size(266, 92);
        _status.Font = new Font("Segoe UI", 9f);

        var localNote = MakeLabel(
            "Disconnect only clears this demo session. It does not log you out of Claude Code on Windows.",
            8f,
            FontStyle.Regular,
            Color.FromArgb(143, 126, 116));
        localNote.Location = new Point(26, 530);
        localNote.Size = new Size(266, 42);

        authCard.Controls.AddRange([eyebrow, title, copy, _button, openClaude, _status, localNote]);

        var chatCard = new Panel
        {
            BackColor = Color.FromArgb(41, 38, 36),
            Location = new Point(360, 162),
            Size = new Size(596, 592),
        };
        var chatEyebrow = MakeLabel("CLAUDE CODE · LIVE CHAT", 9f, FontStyle.Bold, Color.FromArgb(217, 119, 87));
        chatEyebrow.Location = new Point(24, 24);
        chatEyebrow.AutoSize = true;

        var chatTitle = MakeLabel("A small room for a real conversation.", 23f, FontStyle.Regular, Color.FromArgb(247, 243, 237), "Georgia");
        chatTitle.Location = new Point(24, 57);
        chatTitle.Size = new Size(520, 42);

        _modelStatus.Text = $"MODEL VIA CLAUDE CODE  ·  {_claudeClient.Model}".ToUpperInvariant();
        _modelStatus.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
        _modelStatus.ForeColor = Color.FromArgb(201, 169, 149);
        _modelStatus.Location = new Point(26, 105);
        _modelStatus.Size = new Size(520, 20);

        _messages.Location = new Point(24, 140);
        _messages.Size = new Size(548, 302);
        _messages.AutoScroll = true;
        _messages.FlowDirection = FlowDirection.TopDown;
        _messages.WrapContents = false;
        _messages.BackColor = Color.FromArgb(41, 38, 36);
        _messages.Padding = new Padding(0, 0, 4, 0);

        _composer.Location = new Point(24, 462);
        _composer.Size = new Size(438, 42);
        _composer.BorderStyle = BorderStyle.FixedSingle;
        _composer.BackColor = Color.FromArgb(29, 28, 27);
        _composer.ForeColor = Color.FromArgb(247, 243, 237);
        _composer.Font = new Font("Segoe UI", 10f);
        _composer.PlaceholderText = "Connect Claude Code to chat…";
        _composer.Enabled = false;
        _composer.KeyDown += ComposerKeyDown;

        _send.Location = new Point(476, 462);
        _send.Size = new Size(96, 42);
        _send.Text = "Send  ↗";
        _send.FlatStyle = FlatStyle.Flat;
        _send.FlatAppearance.BorderSize = 0;
        _send.BackColor = Color.FromArgb(217, 119, 87);
        _send.ForeColor = Color.FromArgb(41, 38, 36);
        _send.Enabled = false;
        _send.Click += async (_, _) => await SendMessageAsync();

        var chatNote = MakeLabel(
            "REAL PATH: claude -p  ·  no browser layer  ·  no simulated answer",
            8f,
            FontStyle.Bold,
            Color.FromArgb(169, 150, 139),
            "Consolas");
        chatNote.Location = new Point(24, 530);
        chatNote.Size = new Size(548, 28);

        chatCard.Controls.AddRange([chatEyebrow, chatTitle, _modelStatus, _messages, _composer, _send, chatNote]);
        Controls.AddRange([warningCard, authCard, chatCard]);

        AddMessage("Claude", "Run 'claude' once in PowerShell if you still need to finish the browser login, then connect here.", false);
        AddMessage("System", "This demo sends real turns through Claude Code. It never invents a connected state or a reply.", false);
    }

    private static Label MakeLabel(string text, float size, FontStyle style, Color color, string family = "Segoe UI")
        => new()
        {
            Text = text,
            Font = new Font(family, size, style),
            ForeColor = color,
            AutoSize = false,
        };

    private void AddMessage(string author, string text, bool user)
    {
        var message = new Panel
        {
            Width = 524,
            Height = user ? 72 : 88,
            Margin = new Padding(user ? 42 : 0, 0, 0, 10),
            Padding = new Padding(13, 9, 13, 8),
            BackColor = user ? Color.FromArgb(78, 58, 51) : Color.FromArgb(247, 243, 237),
        };
        var meta = MakeLabel(author.ToUpperInvariant(), 8f, FontStyle.Bold, user ? Color.FromArgb(235, 190, 173) : Color.FromArgb(122, 46, 18));
        meta.Dock = DockStyle.Top;
        meta.Height = 17;
        var body = MakeLabel(text, 9f, FontStyle.Regular, user ? Color.FromArgb(255, 250, 245) : Color.FromArgb(41, 38, 36));
        body.Dock = DockStyle.Fill;
        message.Controls.Add(body);
        message.Controls.Add(meta);
        _messages.Controls.Add(message);
        _messages.ScrollControlIntoView(message);
    }

    private async Task SignInAsync()
    {
        _button.SetSigningIn();
        SetChatEnabled(false);
        _status.Text = "Checking your existing Claude Code login with a real request…";

        try
        {
            _session = await _authProvider.SignInAsync();
            _button.SetConnected(_session.AccountLabel);
            _status.Text = "Connected. Chat now runs through your local Claude Code account.";
            SetChatEnabled(true);
            _history.Clear();
            AddMessage("Claude", "Connected through Claude Code. Ask me something.", false);
            _composer.Focus();
        }
        catch (OperationCanceledException)
        {
            _session = null;
            _button.SetSignedOut();
            _status.Text = "Connection canceled.";
        }
        catch (Exception exception)
        {
            _session = null;
            _button.SetError();
            _status.Text = exception.Message;
            AddMessage("System", exception.Message, false);
        }
    }

    private async Task SignOutAsync()
    {
        await _authProvider.SignOutAsync();
        _session = null;
        _history.Clear();
        _button.SetSignedOut();
        SetChatEnabled(false);
        _status.Text = "Disconnected from this demo. Claude Code remains logged in on this PC.";
        AddMessage("System", "Local demo session cleared. Your Claude Code login was left untouched.", false);
    }

    private void SetChatEnabled(bool enabled)
    {
        _composer.Enabled = enabled && !_requestInFlight;
        _send.Enabled = enabled && !_requestInFlight;
        _composer.PlaceholderText = enabled ? "Ask Claude something…" : "Connect Claude Code to chat…";
    }

    private void ComposerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.SuppressKeyPress = true;
            _ = SendMessageAsync();
        }
    }

    private async Task SendMessageAsync()
    {
        var text = _composer.Text.Trim();
        if (string.IsNullOrWhiteSpace(text) || _session is null || _requestInFlight)
        {
            return;
        }

        _requestInFlight = true;
        SetChatEnabled(true);
        _composer.Clear();
        AddMessage("You", text, true);
        _history.Add(new ClaudeMessage("user", text));

        try
        {
            var response = await _claudeClient.SendAsync(_history);
            _history.Add(new ClaudeMessage("assistant", response.Text));
            AddMessage("Claude", response.Text, false);
            _status.Text = $"Live Claude Code response received · {response.Model ?? _claudeClient.Model}.";
        }
        catch (Exception exception)
        {
            AddMessage("System", exception.Message, false);
            _status.Text = "The real Claude Code request failed.";
        }
        finally
        {
            _requestInFlight = false;
            SetChatEnabled(_session is not null);
            _composer.Focus();
        }
    }
}
