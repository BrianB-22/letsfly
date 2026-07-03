using System.Media;

namespace CheckRide;

public class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _startItem;
    private readonly ToolStripMenuItem _endItem;

    private XP12Connector? _connector;
    private FlightMonitor? _monitor;
    private EventLogger? _logger;
    private string _sessionTimestamp = "";

    private readonly System.Windows.Forms.Timer _watchTimer;

    public TrayApp()
    {
        _startItem = new ToolStripMenuItem("Start Recording", null, OnStartRecording);
        _endItem   = new ToolStripMenuItem("End Recording",   null, OnEndRecording) { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_startItem);
        menu.Items.Add(_endItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, OnExit));

        _tray = new NotifyIcon
        {
            Icon             = SystemIcons.Application,
            Text             = "CheckRide — Watching…",
            ContextMenuStrip = menu,
            Visible          = true
        };

        _watchTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _watchTimer.Tick += OnWatchTick;
        _watchTimer.Start();
    }

    private async void OnWatchTick(object? sender, EventArgs e)
    {
        if (_connector is not null) return; // already recording
        var live = await XP12Connector.ProbeAsync();
        if (!live) return;

        _watchTimer.Stop();
        OnStartRecording(null, EventArgs.Empty);

        _tray.ShowBalloonTip(4000, "CheckRide", "Flight detected — recording started automatically.", ToolTipIcon.Info);
    }

    private static void PlaySound(string fileName)
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds", fileName);
            if (File.Exists(path)) new SoundPlayer(path).Play();
        }
        catch { }
    }

    private void OnStartRecording(object? sender, EventArgs e)
    {
        try
        {
            _sessionTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            var dir = OutputDir();
            Directory.CreateDirectory(dir);

            _logger    = new EventLogger(Path.Combine(dir, $"checkride_{_sessionTimestamp}.log"));
            _monitor   = new FlightMonitor();
            _connector = new XP12Connector();

            _connector.Log       = msg => _logger.Log($"[XP12] {msg}");
            _connector.Connected = () => PlaySound("start.wav");
            _connector.FlightDataReceived += snap => _monitor.OnSnapshot(snap, _logger);

            _ = _connector.StartAsync();

            SetRecording(true);
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(5000, "CheckRide — Start Failed", ex.Message, ToolTipIcon.Error);
            _connector = null; _monitor = null; _logger = null;
        }
    }

    private async void OnEndRecording(object? sender, EventArgs e)
    {
        if (_connector is null || _monitor is null || _logger is null) return;

        SetRecording(false);

        try
        {
            await _connector.StopAsync();

            var report = _monitor.BuildReport();
            _logger.Log($"Session ended — Score: {report.Score}  Grade: {report.Grade}");
            _logger.Close();

            PlaySound("stop.wav");

            var reportPath = Path.Combine(OutputDir(), $"checkride_{_sessionTimestamp}.json");
            ReportWriter.Write(report, reportPath);

            _tray.ShowBalloonTip(
                6000,
                "CheckRide Complete",
                $"Score: {report.Score}  Grade: {report.Grade}\n{reportPath}",
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(3000, "CheckRide Error", ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            _connector    = null;
            _monitor      = null;
            _logger       = null;
            _watchTimer.Start();
            _tray.Text = "CheckRide — Watching…";
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _tray.Visible = false;
        Application.Exit();
    }

    private void SetRecording(bool recording)
    {
        _startItem.Enabled = !recording;
        _endItem.Enabled   = recording;
        _tray.Text         = recording ? "CheckRide — Recording…" : "CheckRide — Watching…";
        if (recording) _watchTimer.Stop();
    }

    private static string OutputDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CheckRide");

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tray.Dispose(); _watchTimer.Dispose(); }
        base.Dispose(disposing);
    }
}
