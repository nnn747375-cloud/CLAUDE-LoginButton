using ClaudeLoginButton;

namespace ClaudeLoginButton.Demo;

internal sealed class DemoForm : Form
{
    private readonly ClaudeLoginButton _button = new();
    private readonly Label _status = new();

    public DemoForm()
    {
        Text = "CLAUDE-LoginButton · demo";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 280);
        BackColor = Color.FromArgb(247, 243, 237);

        var title = new Label
        {
            AutoSize = true,
            Text = "A button, not an auth backend.",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(41, 38, 36),
            Location = new Point(42, 36),
        };
        var copy = new Label
        {
            AutoSize = false,
            Size = new Size(420, 48),
            Text = "The host application owns OAuth, callbacks and secure session storage.",
            ForeColor = Color.FromArgb(92, 81, 73),
            Location = new Point(44, 82),
        };
        _button.Location = new Point(42, 152);
        _button.LoginRequested += async (_, _) => await SimulateLoginAsync();
        _button.LogoutRequested += (_, _) =>
        {
            _button.SetSignedOut();
            _status.Text = "Signed out (demo state).";
        };
        _status.AutoSize = true;
        _status.Text = "Ready for your auth service.";
        _status.ForeColor = Color.FromArgb(92, 81, 73);
        _status.Location = new Point(42, 222);

        Controls.AddRange([title, copy, _button, _status]);
    }

    private async Task SimulateLoginAsync()
    {
        _button.SetSigningIn();
        _status.Text = "Demo login in progress…";
        await Task.Delay(650);
        _button.SetConnected("Claude connected");
        _status.Text = "Replace this demo with your validated auth result.";
    }
}
