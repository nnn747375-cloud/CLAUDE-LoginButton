using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ClaudeLoginButton;

public enum ClaudeLoginState
{
    SignedOut,
    SigningIn,
    Connected,
    Error,
}

/// <summary>
/// A warm, accessible WinForms button for starting an application-defined
/// Claude authorization flow. It does not implement authentication itself.
/// </summary>
[DefaultEvent(nameof(LoginRequested))]
public sealed class ClaudeLoginButton : Control
{
    private ClaudeLoginState _state = ClaudeLoginState.SignedOut;
    private string? _accountLabel;
    private bool _hovered;

    public ClaudeLoginButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = "Continue with Claude";
        Cursor = Cursors.Hand;
        Font = new Font("Georgia", 10f, FontStyle.Regular);
        MinimumSize = new Size(220, 48);
        Size = new Size(280, 54);
        TabStop = true;
    }

    public event EventHandler? LoginRequested;
    public event EventHandler? LogoutRequested;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ClaudeLoginState State => _state;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? AccountLabel => _accountLabel;

    public void SetSigningIn()
        => SetState(ClaudeLoginState.SigningIn, null);

    public void SetConnected(string? accountLabel = null)
        => SetState(ClaudeLoginState.Connected, accountLabel);

    public void SetSignedOut()
        => SetState(ClaudeLoginState.SignedOut, null);

    public void SetError(string? message = null)
        => SetState(ClaudeLoginState.Error, message);

    public void PerformLogin()
    {
        if (_state != ClaudeLoginState.SigningIn)
        {
            LoginRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void PerformLogout()
    {
        if (_state == ClaudeLoginState.Connected)
        {
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (_state == ClaudeLoginState.Connected)
        {
            PerformLogout();
        }
        else
        {
            PerformLogin();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            PerformClickFromKeyboard();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(1, 1, Math.Max(0, Width - 3), Math.Max(0, Height - 3));
        var radius = Math.Min(14, Math.Max(6, bounds.Height / 3));
        using var path = RoundedRectangle(bounds, radius);

        var palette = PaletteForState();
        using var fill = new SolidBrush(palette.Fill);
        using var border = new Pen(palette.Border, Focused ? 2f : 1f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        var text = CurrentText();
        var markSize = Math.Min(18, Math.Max(14, bounds.Height - 25));
        const int markGap = 9;
        var textSize = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        var groupWidth = markSize + markGap + textSize.Width;
        var groupX = Math.Max(bounds.X + 12, bounds.X + ((bounds.Width - groupWidth) / 2));
        var markBounds = new Rectangle(groupX, bounds.Y + ((bounds.Height - markSize) / 2), markSize, markSize);
        DrawClaudeMark(e.Graphics, markBounds, palette.Text);

        using var textBrush = new SolidBrush(palette.Text);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        var textBounds = new Rectangle(groupX + markSize + markGap, bounds.Y, Math.Max(1, bounds.Right - groupX - markSize - markGap), bounds.Height);
        e.Graphics.DrawString(text, Font, textBrush, textBounds, format);

        if (Focused)
        {
            var focusBounds = Rectangle.Inflate(bounds, -5, -5);
            using var focusPath = RoundedRectangle(focusBounds, Math.Max(4, radius - 3));
            using var focusPen = new Pen(Color.FromArgb(140, palette.Focus), 1f) { DashStyle = DashStyle.Dot };
            e.Graphics.DrawPath(focusPen, focusPath);
        }
    }

    private void SetState(ClaudeLoginState state, string? accountLabel)
    {
        if (IsHandleCreated && InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => SetState(state, accountLabel)));
            }
            catch (InvalidOperationException)
            {
                // The host is closing; no UI update is needed.
            }

            return;
        }

        _state = state;
        _accountLabel = state == ClaudeLoginState.Connected ? accountLabel : null;
        Enabled = state != ClaudeLoginState.SigningIn;
        AccessibleName = CurrentText();
        Cursor = state == ClaudeLoginState.SigningIn ? Cursors.WaitCursor : Cursors.Hand;
        Invalidate();
    }

    private string CurrentText()
        => _state switch
        {
            ClaudeLoginState.SigningIn => "Signing in…",
            ClaudeLoginState.Connected => string.IsNullOrWhiteSpace(_accountLabel) ? "Claude connected" : _accountLabel,
            ClaudeLoginState.Error => "Try Claude login again",
            _ => "Continue with Claude",
        };

    private void PerformClickFromKeyboard()
    {
        if (Enabled)
        {
            OnClick(EventArgs.Empty);
        }
    }

    private (Color Fill, Color Border, Color Text, Color Focus) PaletteForState()
    {
        if (!Enabled)
        {
            return (Color.FromArgb(235, 229, 222), Color.FromArgb(190, 177, 165), Color.FromArgb(125, 113, 103), Color.FromArgb(120, 96, 80));
        }

        if (_state == ClaudeLoginState.Connected)
        {
            return (_hovered ? Color.FromArgb(236, 247, 239) : Color.FromArgb(246, 251, 247), Color.FromArgb(67, 126, 88), Color.FromArgb(39, 93, 57), Color.FromArgb(39, 93, 57));
        }

        if (_state == ClaudeLoginState.Error)
        {
            return (Color.FromArgb(255, 242, 237), Color.FromArgb(180, 68, 43), Color.FromArgb(126, 44, 27), Color.FromArgb(126, 44, 27));
        }

        return (_hovered ? Color.FromArgb(255, 238, 226) : Color.FromArgb(255, 247, 239), Color.FromArgb(181, 78, 50), Color.FromArgb(108, 48, 34), Color.FromArgb(181, 78, 50));
    }

    private static void DrawClaudeMark(Graphics graphics, Rectangle bounds, Color color)
    {
        var center = new PointF(bounds.Left + (bounds.Width / 2f), bounds.Top + (bounds.Height / 2f));
        var radius = Math.Max(3f, bounds.Width / 2f);
        using var pen = new Pen(color, Math.Max(1.2f, bounds.Width / 8f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        for (var index = 0; index < 6; index++)
        {
            var angle = (Math.PI / 3d * index) - (Math.PI / 2d);
            var end = new PointF(
                center.X + (float)(Math.Cos(angle) * radius),
                center.Y + (float)(Math.Sin(angle) * radius));
            graphics.DrawLine(pen, center, end);
        }

        using var dot = new SolidBrush(color);
        graphics.FillEllipse(dot, center.X - 1.5f, center.Y - 1.5f, 3f, 3f);
    }

    private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
