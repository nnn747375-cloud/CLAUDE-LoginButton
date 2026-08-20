using System.Diagnostics;
using ClaudeLoginButton;

namespace ClaudeLoginButton.Demo;

internal sealed class DemoForm : Form
{
    private readonly IClaudeAuthProvider _authProvider;
    private readonly ClaudeLoginButton _button = new();
    private readonly Label _status = new();
    private readonly FlowLayoutPanel _messages = new();
    private readonly TextBox _composer = new();
    private readonly Button _send = new();
    private readonly List<ClaudeMessage> _history = [];
    private readonly ClaudeMessagesClient _claudeClient;
    private ClaudeAuthSession? _session;
    private bool _requestInFlight;

    public DemoForm()
    {
        _authProvider = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"))
            ? new ClaudeCliAuthProvider()
            : new ClaudeApiKeyAuthProvider();

        _claudeClient = new ClaudeMessagesClient(GetCredentialAsync)
        {
            Model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-5",
            MaxTokens = 1024,
        };

        Text = "CLAUDE-LoginButton · live demo";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(860, 620);
        BackColor = Color.FromArgb(23, 25, 24);

        var authCard = new Panel
        {
            BackColor = Color.FromArgb(244, 239, 231),
            Location = new Point(28, 28),
            Size = new Size(300, 564),
        };
        var eyebrow = MakeLabel("LIVE AUTHENTICATION", 9f, FontStyle.Bold, Color.FromArgb(156, 77, 56));
        eyebrow.Location = new Point(24, 24);
        eyebrow.AutoSize = true;

        var title = MakeLabel("A softer way in.", 26f, FontStyle.Regular, Color.FromArgb(23, 25, 24), "Georgia");
        title.Location = new Point(24, 90);
        title.Size = new Size(245, 75);

        var copy = MakeLabel(
            "This sample runs the official Claude CLI OAuth flow, then sends real Messages API requests from the host process.",
            10f,
            FontStyle.Regular,
            Color.FromArgb(111, 101, 93));
        copy.Location = new Point(26, 174);
        copy.Size = new Size(244, 100);

        _button.Location = new Point(24, 300);
        _button.Size = new Size(252, 56);
        _button.LoginRequested += async (_, _) => await SignInAsync();
        _button.LogoutRequested += async (_, _) => await SignOutAsync();

        var openClaude = new LinkLabel
        {
            AutoSize = true,
            Text = "Open Claude Console ↗",
            LinkColor = Color.FromArgb(156, 77, 56),
            ActiveLinkColor = Color.FromArgb(48, 93, 70),
            Location = new Point(26, 390),
        };
        openClaude.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("https://console.anthropic.com/") { UseShellExecute = true });

        _status.AutoSize = false;
        _status.Text = _authProvider is ClaudeApiKeyAuthProvider
            ? "ANTHROPIC_API_KEY found. Click to connect."
            : "Install the official 'ant' CLI, then click the button.";
        _status.ForeColor = Color.FromArgb(111, 101, 93);
        _status.Location = new Point(26, 452);
        _status.Size = new Size(244, 70);
        _status.Font = new Font("Segoe UI", 9f);

        authCard.Controls.AddRange([eyebrow, title, copy, _button, openClaude, _status]);

        var chatCard = new Panel
        {
            BackColor = Color.FromArgb(37, 40, 38),
            Location = new Point(346, 28),
            Size = new Size(486, 564),
        };
        var chatEyebrow = MakeLabel("CLAUDE · LIVE CHAT", 9f, FontStyle.Bold, Color.FromArgb(217, 119, 87));
        chatEyebrow.Location = new Point(24, 24);
        chatEyebrow.AutoSize = true;

        var chatTitle = MakeLabel("A small room for a real conversation.", 22f, FontStyle.Regular, Color.FromArgb(244, 239, 231), "Georgia");
        chatTitle.Location = new Point(24, 57);
        chatTitle.Size = new Size(425, 64);

        _messages.Location = new Point(24, 136);
        _messages.Size = new Size(438, 300);
        _messages.AutoScroll = true;
        _messages.FlowDirection = FlowDirection.TopDown;
        _messages.WrapContents = false;
        _messages.BackColor = Color.FromArgb(37, 40, 38);
        _messages.Padding = new Padding(0, 0, 4, 0);

        _composer.Location = new Point(24, 460);
        _composer.Size = new Size(350, 40);
        _composer.BorderStyle = BorderStyle.FixedSingle;
        _composer.BackColor = Color.FromArgb(28, 30, 28);
        _composer.ForeColor = Color.FromArgb(244, 239, 231);
        _composer.Font = new Font("Segoe UI", 10f);
        _composer.PlaceholderText = "Connect Claude to chat…";
        _composer.Enabled = false;
        _composer.KeyDown += ComposerKeyDown;

        _send.Location = new Point(384, 460);
        _send.Size = new Size(78, 40);
        _send.Text = "Send ↗";
        _send.FlatStyle = FlatStyle.Flat;
        _send.FlatAppearance.BorderSize = 0;
        _send.BackColor = Color.FromArgb(217, 119, 87);
        _send.ForeColor = Color.FromArgb(23, 25, 24);
        _send.Enabled = false;
        _send.Click += async (_, _) => await SendMessageAsync();

        var chatNote = MakeLabel("Live requests stay in this host process and use your active Claude CLI credential.", 8f, FontStyle.Regular, Color.FromArgb(143, 134, 126));
        chatNote.Location = new Point(24, 518);
        chatNote.Size = new Size(438, 28);

        chatCard.Controls.AddRange([chatEyebrow, chatTitle, _messages, _composer, _send, chatNote]);
        Controls.AddRange([authCard, chatCard]);

        AddMessage("Claude", "Install the official ant CLI, connect above, then ask me something.", false);
        AddMessage("Claude", "This demo no longer fakes a connected state or a reply.", false);
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
            Width = 414,
            Height = user ? 68 : 82,
            Margin = new Padding(user ? 40 : 0, 0, 0, 10),
            Padding = new Padding(13, 9, 13, 8),
            BackColor = user ? Color.FromArgb(59, 62, 58) : Color.FromArgb(244, 239, 231),
        };
        var meta = MakeLabel(author.ToUpperInvariant(), 8f, FontStyle.Bold, user ? Color.FromArgb(209, 184, 172) : Color.FromArgb(156, 77, 56));
        meta.Dock = DockStyle.Top;
        meta.Height = 17;
        var body = MakeLabel(text, 9f, FontStyle.Regular, user ? Color.FromArgb(255, 250, 245) : Color.FromArgb(23, 25, 24));
        body.Dock = DockStyle.Fill;
        message.Controls.Add(body);
        message.Controls.Add(meta);
        _messages.Controls.Add(message);
        _messages.ScrollControlIntoView(message);
    }

    private Task<ClaudeCredential> GetCredentialAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _session is null
            ? Task.FromException<ClaudeCredential>(new InvalidOperationException("Connect Claude before sending a message."))
            : Task.FromResult(_session.Credential);
    }

    private async Task SignInAsync()
    {
        _button.SetSigningIn();
        SetChatEnabled(false);
        _status.Text = "Opening the official Claude Console OAuth flow…";

        try
        {
            _session = await _authProvider.SignInAsync();
            _button.SetConnected(_session.AccountLabel);
            _status.Text = "Connected. Messages now use the live Claude API.";
            SetChatEnabled(true);
            _history.Clear();
            _history.Add(new ClaudeMessage("assistant", "Connected through your local Claude Console session. Ask me something."));
            AddMessage("Claude", "Connected through your local Claude Console session. Ask me something.", false);
            _composer.Focus();
        }
        catch (OperationCanceledException)
        {
            _session = null;
            _button.SetSignedOut();
            _status.Text = "Login canceled.";
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
        if (_session is null)
        {
            _button.SetSignedOut();
            return;
        }

        _send.Enabled = false;
        _composer.Enabled = false;
        try
        {
            await _authProvider.SignOutAsync();
            _status.Text = "Signed out. The stored Claude CLI credential was removed.";
            AddMessage("Claude", "Signed out. Connect again when you are ready.", false);
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
            AddMessage("System", exception.Message, false);
        }
        finally
        {
            _session = null;
            _history.Clear();
            _button.SetSignedOut();
            SetChatEnabled(false);
        }
    }

    private void SetChatEnabled(bool enabled)
    {
        _composer.Enabled = enabled && !_requestInFlight;
        _send.Enabled = enabled && !_requestInFlight;
        _composer.PlaceholderText = enabled ? "Ask Claude something…" : "Connect Claude to chat…";
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
            var reply = await _claudeClient.SendAsync(_history);
            _history.Add(new ClaudeMessage("assistant", reply));
            AddMessage("Claude", reply, false);
            _status.Text = "Live Claude response received.";
        }
        catch (Exception exception)
        {
            AddMessage("System", exception.Message, false);
            _status.Text = "The live Claude request failed.";
        }
        finally
        {
            _requestInFlight = false;
            SetChatEnabled(_session is not null);
            _composer.Focus();
        }
    }
}
