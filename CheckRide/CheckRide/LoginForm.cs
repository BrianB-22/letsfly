using CheckRide.Models;

namespace CheckRide;

internal class LoginForm : Form
{
    // Colors matching SimLetsFly site palette
    private static readonly Color _bg      = Color.FromArgb(14, 19, 24);
    private static readonly Color _panel   = Color.FromArgb(17, 23, 32);
    private static readonly Color _border  = Color.FromArgb(30, 45, 61);
    private static readonly Color _accent  = Color.FromArgb(0, 180, 216);
    private static readonly Color _text    = Color.FromArgb(200, 216, 232);
    private static readonly Color _text2   = Color.FromArgb(122, 146, 168);
    private static readonly Color _text3   = Color.FromArgb(74, 96, 112);
    private static readonly Color _red     = Color.FromArgb(231, 111, 81);

    private readonly TextBox _txtEmail    = new();
    private readonly TextBox _txtPassword = new();
    private readonly Button  _btnLogin    = new();
    private readonly Label   _lblError    = new();

    public SupabaseSession? Session { get; private set; }

    public LoginForm()
    {
        Text            = "CheckRide for SimLetsFly — Sign In";
        var icoPath = Path.Combine(EmbeddedAssets.Dir, "images", "icon_256x256.ico");
        if (File.Exists(icoPath)) try { Icon = new Icon(icoPath); } catch { }
        ClientSize      = new Size(420, 420);
        BackColor       = _bg;
        ForeColor       = _text;
        Font            = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;

        // ── Banner ────────────────────────────────────────────────────────────
        var picBanner = new PictureBox
        {
            Bounds    = new Rectangle(0, 0, 420, 180),
            SizeMode  = PictureBoxSizeMode.Zoom,
            BackColor = _bg,
        };
        var bannerPath = Path.Combine(EmbeddedAssets.Dir, "images", "banner.png");
        if (File.Exists(bannerPath)) try { picBanner.Image = Image.FromFile(bannerPath); } catch { }

        // ── Divider ──────────────────────────────────────────────────────────
        var divider = new Panel { BackColor = _border, Bounds = new Rectangle(40, 188, 340, 1) };

        // ── Email ─────────────────────────────────────────────────────────────
        var lblEmail = MakeFieldLabel("EMAIL", 40, 202);

        _txtEmail.Bounds        = new Rectangle(40, 220, 340, 28);
        _txtEmail.BackColor     = _panel;
        _txtEmail.ForeColor     = _text;
        _txtEmail.BorderStyle   = BorderStyle.FixedSingle;
        _txtEmail.Font          = new Font("Segoe UI", 10f);
        _txtEmail.KeyDown      += OnKeyDown;

        // ── Password ──────────────────────────────────────────────────────────
        var lblPwd = MakeFieldLabel("PASSWORD", 40, 258);

        _txtPassword.Bounds       = new Rectangle(40, 276, 340, 28);
        _txtPassword.BackColor    = _panel;
        _txtPassword.ForeColor    = _text;
        _txtPassword.BorderStyle  = BorderStyle.FixedSingle;
        _txtPassword.Font         = new Font("Segoe UI", 10f);
        _txtPassword.PasswordChar = '●';
        _txtPassword.KeyDown     += OnKeyDown;

        // ── Login button ──────────────────────────────────────────────────────
        _btnLogin.Text      = "SIGN IN";
        _btnLogin.Bounds    = new Rectangle(130, 320, 160, 36);
        _btnLogin.BackColor = _accent;
        _btnLogin.ForeColor = Color.FromArgb(10, 13, 16);
        _btnLogin.FlatStyle = FlatStyle.Flat;
        _btnLogin.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
        _btnLogin.FlatAppearance.BorderSize = 0;
        _btnLogin.Cursor    = Cursors.Hand;
        _btnLogin.Click    += OnLogin;

        // ── Error label ───────────────────────────────────────────────────────
        _lblError.Bounds    = new Rectangle(40, 364, 340, 22);
        _lblError.ForeColor = _red;
        _lblError.TextAlign = ContentAlignment.MiddleCenter;
        _lblError.Font      = new Font("Segoe UI", 8.5f);

        // ── Account help link ─────────────────────────────────────────────────
        const string helpText = "To reset your password or create an account, visit simletsfly.com";
        var lnkHelp = new LinkLabel
        {
            Text      = helpText,
            Bounds    = new Rectangle(40, 390, 340, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = _text3,
            LinkArea  = new LinkArea(helpText.IndexOf("simletsfly.com"), "simletsfly.com".Length),
        };
        lnkHelp.LinkColor        = _accent;
        lnkHelp.ActiveLinkColor  = Color.White;
        lnkHelp.VisitedLinkColor = _accent;
        lnkHelp.LinkClicked     += (s, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://simletsfly.com") { UseShellExecute = true }); } catch { }
        };

        Controls.AddRange(new Control[] {
            picBanner, divider, lblEmail, _txtEmail,
            lblPwd, _txtPassword, _btnLogin, _lblError, lnkHelp
        });
    }

    private Label MakeFieldLabel(string text, int x, int y) => new()
    {
        Text      = text,
        ForeColor = _text3,
        Font      = new Font("Segoe UI", 7.5f, FontStyle.Regular),
        Bounds    = new Rectangle(x, y, 200, 16),
    };

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) OnLogin(sender, EventArgs.Empty);
    }

    private async void OnLogin(object? sender, EventArgs e)
    {
        _lblError.Text      = "";
        _btnLogin.Enabled   = false;
        _btnLogin.Text      = "Signing in…";

        try
        {
            var session = await SupabaseClient.LoginAsync(_txtEmail.Text.Trim(), _txtPassword.Text);
            await SupabaseClient.VerifyClientAsync(session.AccessToken);
            SessionStore.Save(session);
            Session      = session;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _lblError.Text    = ex.Message;
            _btnLogin.Enabled = true;
            _btnLogin.Text    = "SIGN IN";
        }
    }
}
