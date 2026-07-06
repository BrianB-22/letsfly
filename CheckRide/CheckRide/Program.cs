using CheckRide;

using var mutex = new Mutex(true, "SimLetsFly.CheckRide.SingleInstance", out bool isNew);
if (!isNew)
{
    MessageBox.Show(
        "CheckRide is already running.",
        "CheckRide for SimLetsFly",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);
    return;
}

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

try { EmbeddedAssets.Extract(); }
catch (Exception ex)
{
    MessageBox.Show($"Failed to initialize CheckRide:\n\n{ex.Message}",
        "CheckRide — Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    return;
}

var session = SessionStore.Load();

if (session is null)
{
    using var login = new LoginForm();
    if (login.ShowDialog() != DialogResult.OK || login.Session is null)
        return;

    session = login.Session;
}

Application.Run(new TrayApp(session));
