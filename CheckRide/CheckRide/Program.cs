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

var session = SessionStore.Load();

if (session is null)
{
    using var login = new LoginForm();
    if (login.ShowDialog() != DialogResult.OK || login.Session is null)
        return;

    session = login.Session;
}

Application.Run(new TrayApp(session));
