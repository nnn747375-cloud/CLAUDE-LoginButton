using System.Diagnostics;
using ClaudeLoginButton;

namespace ClaudeLoginButton.Demo;

internal sealed class DemoForm : Form
{
    private static readonly Color Ink = Color.FromArgb(32, 42, 52), Muted = Color.FromArgb(109, 119, 128),
        Paper = Color.FromArgb(247, 249, 250), Line = Color.FromArgb(225, 231, 234), Accent = Color.FromArgb(172, 91, 60);
    private readonly ClaudeLoginButton _login = new();
    private readonly Label _account = Label("Dein Claude-Konto, direkt verbunden.", 9, Muted);
    private readonly Label _folder = Label("Kein Ordner ausgewählt", 10, Ink);
    private readonly Label _status = Label("Bereit, wenn du es bist.", 9, Muted);
    private readonly Label _mode = Label("CHAT · OHNE DATEIZUGRIFF", 8, Muted, FontStyle.Bold);
    private readonly RichTextBox _conversation = new();
    private readonly FlowLayoutPanel _activity = new();
    private readonly TextBox _composer = new();
    private readonly ComboBox _model = new();
    private readonly Button _send = Button("Senden  ↗", true), _stop = Button("Stoppen"),
        _choose = Button("Ordner auswählen…"), _clearFolder = Button("Ohne Ordner"), _new = Button("+  Neues Gespräch");
    private readonly Button _summarize = Button("Projekt überblicken"), _explain = Button("Dateien finden");
    private readonly ToolTip _tips = new();
    private ClaudeAuthSession? _accountSession;
    private string? _sessionId, _workspace;
    private CancellationTokenSource? _operation;
    private bool _busy, _hasMessages;

    public DemoForm()
    {
        Text = "Claude Studio · LoginButton Demo";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1260, 800);
        MinimumSize = new Size(1050, 690);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Paper;
        Font = new Font("Segoe UI", 10);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Paper };
        root.ColumnStyles.Add(new(SizeType.Absolute, 278));
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.ColumnStyles.Add(new(SizeType.Absolute, 226));
        Controls.Add(root);

        var side = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };
        root.Controls.Add(side, 0, 0);
        var brand = Label("claude / studio", 24, Ink, FontStyle.Regular, "Bahnschrift");
        brand.SetBounds(24, 34, 230, 46);
        var subtitle = Label("RAUM FÜR GUTE FRAGEN", 8, Muted, FontStyle.Bold);
        subtitle.SetBounds(26, 86, 224, 24);
        _login.SetBounds(24, 138, 230, 48);
        _login.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _login.LoginRequested += async (_, _) => await SignInAsync();
        _login.LogoutRequested += (_, _) => Disconnect();
        _account.SetBounds(26, 198, 224, 75);
        var divider = new Panel { BackColor = Line };
        divider.SetBounds(24, 285, 230, 1);
        var workspaceTitle = Label("ARBEITSORDNER", 8, Muted, FontStyle.Bold);
        workspaceTitle.SetBounds(26, 312, 220, 22);
        _folder.SetBounds(26, 344, 224, 50);
        _folder.AutoEllipsis = true;
        _choose.SetBounds(24, 406, 230, 40);
        _clearFolder.SetBounds(24, 452, 230, 34);
        _choose.Click += (_, _) => ChooseWorkspace();
        _clearFolder.Click += (_, _) => ChangeWorkspace(null);
        var note = Label("Du entscheidest, was Claude sehen darf.\nDateien werden nur gelesen.", 9, Muted);
        note.SetBounds(26, 502, 225, 48);
        _new.SetBounds(24, 571, 230, 40);
        _new.Click += (_, _) => ResetConversation();
        var setup = new LinkLabel { Text = "Claude Code einrichten  ↗", LinkColor = Muted, ActiveLinkColor = Accent, AutoSize = true, Font = new Font("Segoe UI", 9), Location = new Point(26, 641) };
        setup.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("https://code.claude.com/docs/en/quickstart") { UseShellExecute = true });
        var independent = Label("Community-Demo · unabhängig von Anthropic", 8, Muted);
        independent.SetBounds(26, 686, 225, 36);
        side.Controls.AddRange([brand, subtitle, _login, _account, divider, workspaceTitle, _folder, _choose, _clearFolder, note, _new, setup, independent]);

        var center = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(32, 29, 28, 20), RowCount = 4, ColumnCount = 1 };
        center.ColumnStyles.Add(new(SizeType.Percent, 100));
        center.RowStyles.Add(new(SizeType.Absolute, 125));
        center.RowStyles.Add(new(SizeType.Percent, 100));
        center.RowStyles.Add(new(SizeType.Absolute, 160));
        center.RowStyles.Add(new(SizeType.Absolute, 30));
        root.Controls.Add(center, 1, 0);
        var header = new Panel { Dock = DockStyle.Fill };
        _mode.SetBounds(0, 3, 430, 25);
        var heading = Label("Was möchtest du verstehen?", 27, Ink, FontStyle.Regular, "Bahnschrift");
        heading.Dock = DockStyle.Bottom; heading.Height = 88;
        header.Controls.AddRange([_mode, heading]); center.Controls.Add(header, 0, 0);
        _conversation.Dock = DockStyle.Fill; _conversation.BorderStyle = BorderStyle.None;
        _conversation.BackColor = Paper; _conversation.ForeColor = Ink;
        _conversation.Font = new Font("Segoe UI", 11); _conversation.ReadOnly = true;
        _conversation.DetectUrls = false; _conversation.AccessibleName = "Gespräch mit Claude";
        _conversation.ScrollBars = RichTextBoxScrollBars.Vertical;
        center.Controls.Add(_conversation, 0, 1);
        var compose = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 2, Margin = new Padding(0, 10, 0, 0) };
        compose.ColumnStyles.Add(new(SizeType.Percent, 100)); compose.ColumnStyles.Add(new(SizeType.Absolute, 110));
        compose.RowStyles.Add(new(SizeType.Absolute, 78)); compose.RowStyles.Add(new(SizeType.Absolute, 45)); compose.RowStyles.Add(new(SizeType.Absolute, 22));
        _composer.Dock = DockStyle.Fill; _composer.Multiline = true; _composer.ScrollBars = ScrollBars.Vertical;
        _composer.BorderStyle = BorderStyle.FixedSingle; _composer.Font = new Font("Segoe UI", 11);
        _composer.PlaceholderText = "Mit Claude anmelden und loslegen…"; _composer.AccessibleName = "Nachricht an Claude";
        _composer.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; await SendAsync(); } };
        compose.Controls.Add(_composer, 0, 0); compose.SetColumnSpan(_composer, 2);
        _send.Dock = DockStyle.Fill; _stop.Dock = DockStyle.Right; _stop.Width = 100;
        _send.EnabledChanged += (_, _) => _send.BackColor = _send.Enabled ? Ink : Line;
        _send.Click += async (_, _) => await SendAsync();
        _stop.Click += (_, _) => { _status.Text = "Wird gestoppt…"; _operation?.Cancel(); };
        compose.Controls.Add(_stop, 0, 1); compose.Controls.Add(_send, 1, 1);
        var keys = Label("Enter zum Senden · Umschalt + Enter für eine neue Zeile", 8, Muted);
        keys.Dock = DockStyle.Fill; compose.Controls.Add(keys, 0, 2); compose.SetColumnSpan(keys, 2);
        center.Controls.Add(compose, 0, 2);
        _status.Dock = DockStyle.Fill; _status.AutoEllipsis = true; center.Controls.Add(_status, 0, 3);

        var rail = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(17, 36, 20, 20), ColumnCount = 1, RowCount = 7 };
        rail.ColumnStyles.Add(new(SizeType.Percent, 100));
        foreach (var h in new[] { 32, 48, 48, 45, 80, 28 }) rail.RowStyles.Add(new(SizeType.Absolute, h));
        rail.RowStyles.Add(new(SizeType.Percent, 100)); root.Controls.Add(rail, 2, 0);
        var modelTitle = Label("MODELL", 8, Muted, FontStyle.Bold); modelTitle.Dock = DockStyle.Fill;
        rail.Controls.Add(modelTitle, 0, 0);
        _model.Items.AddRange(["sonnet", "opus", "haiku"]); _model.SelectedIndex = 0;
        _model.DropDownStyle = ComboBoxStyle.DropDownList; _model.Dock = DockStyle.Top; _model.AccessibleName = "Claude-Modell";
        _model.SelectedIndexChanged += (_, _) => ResetConversation(); rail.Controls.Add(_model, 0, 1);
        _summarize.Dock = DockStyle.Fill; _explain.Dock = DockStyle.Fill;
        _summarize.Click += (_, _) => { _composer.Text = "Gib mir einen kurzen Überblick über dieses Projekt. Lies zuerst passende Dateien und nenne ihre Pfade."; _composer.Focus(); };
        _explain.Click += (_, _) => { _composer.Text = "Finde die wichtigsten Einstiegsdateien in diesem Ordner. Erkläre kurz, wofür sie da sind."; _composer.Focus(); };
        rail.Controls.Add(_summarize, 0, 2); rail.Controls.Add(_explain, 0, 3);
        var permissions = Label("MIT ORDNER VERFÜGBAR\nDateien lesen · suchen · auflisten", 8, Muted);
        permissions.Padding = new Padding(0, 18, 0, 0); permissions.Dock = DockStyle.Fill; rail.Controls.Add(permissions, 0, 4);
        var activityTitle = Label("AKTIVITÄT", 8, Accent, FontStyle.Bold); activityTitle.Dock = DockStyle.Fill; rail.Controls.Add(activityTitle, 0, 5);
        _activity.Dock = DockStyle.Fill; _activity.AutoScroll = true; _activity.FlowDirection = FlowDirection.TopDown; _activity.WrapContents = false;
        rail.Controls.Add(_activity, 0, 6);
        _tips.SetToolTip(_login, "Anmelden oder diese Demo trennen. Andere Claude-Sitzungen bleiben bestehen.");
        FormClosing += (_, _) => _operation?.Cancel();
        FormClosed += (_, _) => { _operation?.Cancel(); _tips.Dispose(); };
        ResetConversation();
    }

    private static Label Label(string text, float size, Color color, FontStyle style = FontStyle.Regular, string family = "Segoe UI") => new()
    { Text = text, Font = new Font(family, size, style), ForeColor = color, BackColor = Color.Transparent };
    private static Button Button(string text, bool primary = false)
    {
        var button = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = primary ? Ink : Color.White,
            ForeColor = primary ? Color.White : Ink, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9), Margin = new Padding(0, 3, 0, 3) };
        button.FlatAppearance.BorderColor = Line; button.FlatAppearance.BorderSize = primary ? 0 : 1;
        return button;
    }
    private void RefreshEnabled()
    {
        if (IsDisposed) return;
        _login.Enabled = !_busy; _choose.Enabled = !_busy; _clearFolder.Enabled = !_busy && _workspace is not null;
        _new.Enabled = !_busy; _model.Enabled = !_busy;
        _send.Enabled = !_busy && _accountSession is not null; _composer.Enabled = !_busy && _accountSession is not null;
        _stop.Visible = _busy; _stop.Enabled = _busy;
        _summarize.Enabled = _explain.Enabled = !_busy && _workspace is not null && _accountSession is not null;
        _composer.PlaceholderText = _accountSession is null ? "Mit Claude anmelden und loslegen…" : "Frag Claude…";
    }
    private void ResetConversation()
    {
        if (_busy) return;
        _sessionId = null; _hasMessages = false; _conversation.Clear(); ClearActivity(); _composer.Clear();
        _conversation.Text = "Eine Frage genügt.\n\nMelde dich mit deinem Claude-Konto an. Wähle bei Bedarf einen Ordner, um Dateien zu lesen und dein Projekt zu erkunden.";
        _status.Text = "Neues Gespräch · " + (_workspace is null ? "ohne Dateizugriff" : "Ordner nur lesen");
        RefreshEnabled();
    }
    private void ChooseWorkspace()
    {
        using var dialog = new FolderBrowserDialog { Description = "Wähle den Ordner, den Claude lesen darf.", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) ChangeWorkspace(dialog.SelectedPath);
    }
    private void ChangeWorkspace(string? path)
    {
        if (_busy) return;
        _workspace = path;
        _folder.Text = path is null ? "Kein Ordner ausgewählt" : Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        _tips.SetToolTip(_folder, path ?? "Kein Dateizugriff");
        _mode.Text = path is null ? "CHAT · OHNE DATEIZUGRIFF" : "PROJEKT · NUR LESEN"; ResetConversation();
    }
    private async Task SignInAsync()
    {
        if (_busy) return;
        _busy = true; _operation = new(); _login.SetSigningIn(); RefreshEnabled();
        var auth = new ClaudeCodeCliAuthProvider { Progress = new Progress<string>(s => { if (!IsDisposed) _account.Text = s; }) };
        try
        {
            var account = await auth.SignInAsync(_operation.Token);
            if (IsDisposed) return;
            _accountSession = account; _login.SetConnected("Claude trennen");
            _account.Text = "Angemeldet. Deine nächste Nachricht wird an Claude gesendet.";
            _status.Text = "Angemeldet · bereit für deine Nachricht";
        }
        catch (OperationCanceledException) { if (!IsDisposed) { _login.SetSignedOut(); _account.Text = "Anmeldung abgebrochen. Du kannst es erneut versuchen."; } }
        catch (Exception ex) { if (!IsDisposed) { _login.SetError(); _account.Text = "Anmeldung nicht möglich. Details im Gespräch."; Append("HINWEIS", ex.Message, Muted); } }
        finally { _busy = false; _operation?.Dispose(); _operation = null; RefreshEnabled(); }
    }
    private void Disconnect()
    {
        if (_busy) return;
        _accountSession = null; _login.SetSignedOut(); ResetConversation();
        _account.Text = "Demo getrennt. Claude Code bleibt auf diesem PC angemeldet.";
    }
    private void Append(string author, string text, Color color)
    {
        if (!_hasMessages) { _conversation.Clear(); _hasMessages = true; }
        _conversation.SelectionStart = _conversation.TextLength;
        using var authorFont = new Font("Segoe UI", 8, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 11);
        _conversation.SelectionFont = authorFont; _conversation.SelectionColor = color; _conversation.AppendText(author + "\n");
        _conversation.SelectionFont = bodyFont; _conversation.SelectionColor = Ink; _conversation.AppendText(text + "\n\n");
        _conversation.SelectionStart = _conversation.TextLength; _conversation.ScrollToCaret();
    }
    private void Activity(ClaudeActivity activity)
    {
        if (IsDisposed || !_busy) return;
        if (activity.Kind == "status") { _status.Text = activity.Label; return; }
        var text = (activity.Kind == "tool" ? "•  " : activity.Kind == "tool-error" ? "!  " : "✓  ") + activity.Label;
        if (!string.IsNullOrWhiteSpace(activity.Detail)) text += "\n" + activity.Detail;
        var label = Label(text, 8.5f, activity.Kind == "tool-error" ? Color.Firebrick : Muted);
        label.Width = 165; label.AutoSize = true; label.MaximumSize = new Size(165, 0); label.Margin = new Padding(0, 0, 0, 16);
        _activity.Controls.Add(label); _activity.ScrollControlIntoView(label);
        _status.Text = activity.Kind == "tool" ? "Claude verwendet " + activity.Label + "…" : "Claude verarbeitet das Ergebnis…";
    }
    private void ClearActivity()
    {
        foreach (Control child in _activity.Controls.Cast<Control>().ToArray()) child.Dispose();
    }
    private async Task SendAsync()
    {
        var prompt = _composer.Text.Trim();
        if (_busy || _accountSession is null || prompt.Length == 0) return;
        _busy = true; _operation = new(); ClearActivity(); RefreshEnabled();
        _composer.Clear(); Append("DU", prompt, Muted);
        try
        {
            var client = new ClaudeCodeClient { WorkspacePath = _workspace, Model = _model.Text };
            var response = await client.SendTurnAsync(prompt, _sessionId, new Progress<ClaudeActivity>(Activity), _operation.Token);
            if (IsDisposed) return;
            _sessionId = response.SessionId; Append("CLAUDE", response.Text, Accent);
            _status.Text = "Antwort erhalten · " + (response.Model ?? client.Model);
        }
        catch (Exception ex)
        {
            if (IsDisposed) return;
            _sessionId = null; _composer.Text = prompt;
            var canceled = ex is OperationCanceledException;
            Append("HINWEIS", (canceled ? "Anfrage gestoppt." : ex.Message) + "\nDie nächste Nachricht beginnt ein neues Gespräch.", Muted);
            _status.Text = canceled ? "Gestoppt · Nachricht zum erneuten Senden bereit" : "Anfrage fehlgeschlagen · Nachricht zum erneuten Senden bereit";
        }
        finally { _busy = false; _operation?.Dispose(); _operation = null; RefreshEnabled(); if (!IsDisposed) _composer.Focus(); }
    }
}
