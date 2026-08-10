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

AircraftDb.Load(Path.Combine(EmbeddedAssets.Dir, "refdata", "faa_aircraft_data.csv"));
AircraftVSpeeds.Load(Path.Combine(EmbeddedAssets.Dir, "refdata", "aircraft.json"));

var session = SessionStore.Load();

if (session is not null)
{
    // A cached session skips LoginForm entirely, which is also the only place that
    // called the version gate -- without this, anyone who's ever logged in successfully
    // could keep running a blocked, out-of-date exe forever. Any failure here (version
    // block, or the session/refresh token having genuinely expired) falls through to
    // LoginForm, which already knows how to display it (including the clickable
    // download link for a version-block message).
    try { await new SupabaseClient(session).VerifyClientAsync(); }
    catch { session = null; }
}

if (session is null)
{
    using var login = new LoginForm();
    if (login.ShowDialog() != DialogResult.OK || login.Session is null)
        return;

    session = login.Session;
}

Application.Run(new TrayApp(session));
