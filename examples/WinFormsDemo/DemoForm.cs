using System.Diagnostics;
using ClaudeLoginButton;

namespace ClaudeLoginButton.Demo;

internal sealed class DemoForm : Form
{
    private readonly ClaudeLoginButton _button = new();
    private readonly Label _status = new();
    private readonly FlowLayoutPanel _messages = new();
    private readonly TextBox _composer = new();
    private readonly Button _send = new();

    public DemoForm()
    {
        Text = "CLAUDE-LoginButton · demo";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(860, 620);
        BackColor = Color.FromArgb(23, 25, 24);

        var authCard = new Panel
        {
            BackColor = Color.FromArgb(244, 239, 231),
            Location = new Point(28, 28),
            Size = new Size(300, 564),
        };
        var eyebrow = MakeLabel("AUTHENTICATION SURFACE", 9f, FontStyle.Bold, Color.FromArgb(156, 77, 56));
        eyebrow.Location = new Point(24, 24);
        eyebrow.AutoSize = true;

        var title = MakeLabel("A softer way in.", 26f, FontStyle.Regular, Color.FromArgb(23, 25, 24), "Georgia");
        title.Location = new Point(24, 90);
        title.Size = new Size(245, 75);

        var copy = MakeLabel(
            "A real control with a small conversation room beside it. The host application owns OAuth, callbacks and secure session storage.",
            10f,
            FontStyle.Regular,
            Color.FromArgb(111, 101, 93));
        copy.Location = new Point(26, 174);
        copy.Size = new Size(244, 82);

        _button.Location = new Point(24, 300);
        _button.Size = new Size(252, 56);
        _button.LoginRequested += async (_, _) => await SimulateLoginAsync();
        _button.LogoutRequested += (_, _) =>
        {
            _button.SetSignedOut();
            SetChatEnabled(false);
            _status.Text = "Signed out. Ready for a fresh host flow.";
            AddMessage("Claude", "Signed out. The chat is waiting for the next connection.", false);
        };

        var openClaude = new LinkLabel
        {
            AutoSize = true,
            Text = "Open official Claude sign-in ↗",
            LinkColor = Color.FromArgb(156, 77, 56),
            ActiveLinkColor = Color.FromArgb(48, 93, 70),
            Location = new Point(26, 390),
        };
        openClaude.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("https://claude.ai/login") { UseShellExecute = true });

        _status.AutoSize = true;
        _status.Text = "Preview host: simulated auth state.";
        _status.ForeColor = Color.FromArgb(111, 101, 93);
        _status.Location = new Point(26, 452);
        _status.Font = new Font("Segoe UI", 9f);

        authCard.Controls.AddRange([eyebrow, title, copy, _button, openClaude, _status]);

        var chatCard = new Panel
        {
            BackColor = Color.FromArgb(37, 40, 38),
            Location = new Point(346, 28),
            Size = new Size(486, 564),
        };
        var chatEyebrow = MakeLabel("CLAUDE · MINI CHAT", 9f, FontStyle.Bold, Color.FromArgb(217, 119, 87));
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

        _send.Location = new Point(382, 460);
        _send.Size = new Size(80, 40);
        _send.Text = "Send ↗";
        _send.FlatStyle = FlatStyle.Flat;
        _send.FlatAppearance.BorderSize = 0;
        _send.BackColor = Color.FromArgb(244, 239, 231);
        _send.ForeColor = Color.FromArgb(23, 25, 24);
        _send.Enabled = false;
        _send.Click += async (_, _) => await SendMessageAsync();

        var chatNote = MakeLabel("Preview replies are local until your host supplies the real chat client.", 8f, FontStyle.Regular, Color.FromArgb(143, 134, 126));
        chatNote.Location = new Point(24, 518);
        chatNote.Size = new Size(438, 28);

        chatCard.Controls.AddRange([chatEyebrow, chatTitle, _messages, _composer, _send, chatNote]);
        Controls.AddRange([authCard, chatCard]);

        AddMessage("Claude", "Hi. Connect the button, then ask me something.", false);
        AddMessage("You", "What does this button own?", true);
        AddMessage("Claude", "Focus, loading, connected and error states. Your host owns auth, tokens and the session.", false);
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

    private async Task SimulateLoginAsync()
    {
        _button.SetSigningIn();
        _status.Text = "Waiting for a simulated callback…";
        await Task.Delay(650);
        _button.SetConnected("Claude connected");
        _status.Text = "Connected preview. Replace with your validated auth result.";
        SetChatEnabled(true);
        AddMessage("Claude", "Connected. The control is ready, and this tiny room is listening.", false);
        _composer.Focus();
    }

    private void SetChatEnabled(bool enabled)
    {
        _composer.Enabled = enabled;
        _send.Enabled = enabled;
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
        if (string.IsNullOrWhiteSpace(text) || !_composer.Enabled)
        {
            return;
        }

        _composer.Clear();
        _send.Enabled = false;
        AddMessage("You", text, true);
        await Task.Delay(350);
        AddMessage("Claude", "This is a local demo reply. Wire your host's validated Claude client here for live responses.", false);
        _send.Enabled = true;
        _composer.Focus();
    }
}
