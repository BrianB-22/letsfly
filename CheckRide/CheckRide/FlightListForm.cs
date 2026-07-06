using System.Media;
using CheckRide.Models;

namespace CheckRide;

internal class FlightListForm : Form
{
    private static readonly Color _bg      = Color.FromArgb(14, 19, 24);
    private static readonly Color _bg2     = Color.FromArgb(19, 25, 32);
    private static readonly Color _panel   = Color.FromArgb(17, 23, 32);
    private static readonly Color _border  = Color.FromArgb(30, 45, 61);
    private static readonly Color _accent  = Color.FromArgb(0, 180, 216);
    private static readonly Color _text    = Color.FromArgb(200, 216, 232);
    private static readonly Color _text2   = Color.FromArgb(122, 146, 168);
    private static readonly Color _text3   = Color.FromArgb(74, 96, 112);
    private static readonly Color _green   = Color.FromArgb(82, 183, 136);
    private static readonly Color _amber   = Color.FromArgb(244, 162, 97);
    private static readonly Color _red     = Color.FromArgb(231, 111, 81);

    private enum AppState { Idle, WaitingXP12, Recording, Uploading, Done }

    private readonly SupabaseClient _client;
    private List<SavedFlight>                         _flights = new();
    private Dictionary<string, (int Score, string Grade, string ResultId)> _scores = new();

    // UI controls
    private readonly DataGridView _grid       = new();
    private readonly Button  _btnTake         = new();
    private readonly Button  _btnCancel       = new();
    private readonly Button  _btnRetry        = new();
    private readonly Button  _btnDebug        = new();
    private readonly Button  _btnOpenFlight   = new();
    private readonly Button  _btnRefresh      = new();
    private readonly Button  _btnHelp         = new();
    private readonly Button  _btnSignOut      = new();

    // Held after a failed upload so the Retry button can re-attempt
    private (string FlightId, CheckRideReport Report, string? LogPath)? _pendingUpload;
    private bool _engineStartAnnounced;
    private readonly Label   _lblStatus       = new();
    private readonly Label   _lblUser         = new();
    private readonly Label   _lblSimValue     = new();

    // Session state
    private AppState     _state = AppState.Idle;
    private SavedFlight? _activeFlight;
    private string       _sessionTimestamp = "";
    private string       _sessionLogPath   = "";

    // Monitoring
    private XP12Connector? _connector;
    private FlightMonitor?  _monitor;
    private EventLogger?    _logger;
    private readonly System.Windows.Forms.Timer _watchTimer = new() { Interval = 5000 };

    public FlightListForm(SupabaseClient client)
    {
        _client = client;

        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var verStr = ver is null ? "" : $" v{ver.Major}.{ver.Minor}.{ver.Build}";
        Text            = $"CheckRide for SimLetsFly{verStr}";
        var icoPath = Path.Combine(EmbeddedAssets.Dir, "images", "icon_256x256.ico");
        if (File.Exists(icoPath)) try { Icon = new Icon(icoPath); } catch { }
        ClientSize      = new Size(880, 560);
        BackColor       = _bg;
        ForeColor       = _text;
        Font            = new Font("Segoe UI", 9f);
        StartPosition   = FormStartPosition.CenterScreen;
        MinimumSize     = new Size(700, 480);
        ShowInTaskbar   = true;

        BuildGrid();
        BuildBottomBar();
        BuildTopBar();

        _watchTimer.Tick += OnWatchTick;

        Load += async (s, e) =>
        {
            // Trigger resize handlers so right-side controls position on first show
            foreach (Control c in Controls)
                c.PerformLayout();
            await LoadDataAsync();
        };
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    private void BuildTopBar()
    {
        var top = new Panel { BackColor = _panel, Height = 50, Dock = DockStyle.Top };
        top.Paint += (s, e) =>
        {
            using var pen = new Pen(_border);
            e.Graphics.DrawLine(pen, 0, top.Height - 1, top.Width, top.Height - 1);
        };

        var lblTitle = new Label
        {
            Text      = "CHECKRIDE",
            ForeColor = _accent,
            Font      = new Font("Segoe UI", 14f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize  = true,
            Location  = new Point(16, 13),
        };

        // Sim selector (placeholder — only XP12 for now)
        var lblSim = new Label
        {
            Text      = "Simulator:",
            ForeColor = _text3,
            Font      = new Font("Segoe UI", 8.5f),
            AutoSize  = true,
            Location  = new Point(0, 0), // positioned below
        };
        _lblSimValue.Text      = "X-Plane 12";
        _lblSimValue.ForeColor = _accent;
        _lblSimValue.Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _lblSimValue.AutoSize  = true;

        var simPanel = new FlowLayoutPanel
        {
            AutoSize     = true,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor    = Color.Transparent,
        };
        simPanel.Controls.Add(lblSim);
        simPanel.Controls.Add(_lblSimValue);

        _lblUser.ForeColor = _text2;
        _lblUser.Font      = new Font("Segoe UI", 8.5f);
        _lblUser.AutoSize  = true;
        _lblUser.Text      = _client.Session.Email;

        _btnSignOut.Text             = "Sign Out";
        _btnSignOut.ForeColor        = _text3;
        _btnSignOut.BackColor        = Color.Transparent;
        _btnSignOut.FlatStyle        = FlatStyle.Flat;
        _btnSignOut.FlatAppearance.BorderColor = _border;
        _btnSignOut.FlatAppearance.BorderSize  = 1;
        _btnSignOut.Font             = new Font("Segoe UI", 8f);
        _btnSignOut.AutoSize         = true;
        _btnSignOut.Cursor           = Cursors.Hand;
        _btnSignOut.Click           += OnSignOut;

        var btnMyFlights = new Button
        {
            Text      = "My Flights ↗",
            ForeColor = _accent,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 8f),
            AutoSize  = true,
            Cursor    = Cursors.Hand,
        };
        btnMyFlights.FlatAppearance.BorderSize = 0;
        btnMyFlights.Click += (s, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://simletsfly.com/flights.html") { UseShellExecute = true }); } catch { }
        };

        top.Controls.AddRange(new Control[] { lblTitle, simPanel, _lblUser, btnMyFlights, _btnSignOut });

        top.Resize += (s, e) =>
        {
            _btnSignOut.Location  = new Point(top.Width - _btnSignOut.Width - 12, (top.Height - _btnSignOut.Height) / 2);
            btnMyFlights.Location = new Point(_btnSignOut.Left - btnMyFlights.Width - 10, (top.Height - btnMyFlights.Height) / 2);
            _lblUser.Location     = new Point(btnMyFlights.Left - _lblUser.Width - 10, (top.Height - _lblUser.Height) / 2);
            simPanel.Location     = new Point(_lblUser.Left - simPanel.Width - 20, (top.Height - simPanel.Height) / 2);
        };

        Controls.Add(top);
    }

    private void BuildBottomBar()
    {
        var bottom = new Panel { BackColor = _panel, Height = 88, Dock = DockStyle.Bottom };
        bottom.Paint += (s, e) =>
        {
            using var pen = new Pen(_border);
            e.Graphics.DrawLine(pen, 0, 0, bottom.Width, 0);
        };

        _btnHelp.Text             = "?";
        _btnHelp.BackColor        = Color.Transparent;
        _btnHelp.ForeColor        = _text3;
        _btnHelp.FlatStyle        = FlatStyle.Flat;
        _btnHelp.FlatAppearance.BorderColor = _border;
        _btnHelp.FlatAppearance.BorderSize  = 1;
        _btnHelp.Font             = new Font("Segoe UI", 11f, FontStyle.Bold);
        _btnHelp.Size             = new Size(36, 36);
        _btnHelp.Location         = new Point(16, 18);
        _btnHelp.Cursor           = Cursors.Hand;
        _btnHelp.Click           += (s, e) => ShowHelp();

        _btnDebug.Text             = "Simulate Upload";
        _btnDebug.BackColor        = Color.Transparent;
        _btnDebug.ForeColor        = _amber;
        _btnDebug.FlatStyle        = FlatStyle.Flat;
        _btnDebug.FlatAppearance.BorderColor = _amber;
        _btnDebug.FlatAppearance.BorderSize  = 1;
        _btnDebug.Font             = new Font("Segoe UI", 8.5f);
        _btnDebug.Size             = new Size(130, 36);
        _btnDebug.Enabled          = false;
        _btnDebug.Visible          = false;
        _btnDebug.Cursor           = Cursors.Hand;
        _btnDebug.Click           += OnDebugUpload;

        _btnRetry.Text             = "Retry Upload";
        _btnRetry.BackColor        = Color.Transparent;
        _btnRetry.ForeColor        = _amber;
        _btnRetry.FlatStyle        = FlatStyle.Flat;
        _btnRetry.FlatAppearance.BorderColor = _amber;
        _btnRetry.FlatAppearance.BorderSize  = 1;
        _btnRetry.Font             = new Font("Segoe UI", 9f);
        _btnRetry.Size             = new Size(110, 36);
        _btnRetry.Visible          = false;
        _btnRetry.Cursor           = Cursors.Hand;
        _btnRetry.Click           += OnRetryUpload;

        _btnCancel.Text             = "Cancel";
        _btnCancel.BackColor        = Color.Transparent;
        _btnCancel.ForeColor        = _red;
        _btnCancel.FlatStyle        = FlatStyle.Flat;
        _btnCancel.FlatAppearance.BorderColor = _red;
        _btnCancel.FlatAppearance.BorderSize  = 1;
        _btnCancel.Font             = new Font("Segoe UI", 9f);
        _btnCancel.Size             = new Size(90, 36);
        _btnCancel.Visible          = false;
        _btnCancel.Cursor           = Cursors.Hand;
        _btnCancel.Click           += OnCancel;

        _btnTake.Text             = "TAKE CHECKRIDE";
        _btnTake.BackColor        = _accent;
        _btnTake.ForeColor        = Color.FromArgb(10, 13, 16);
        _btnTake.FlatStyle        = FlatStyle.Flat;
        _btnTake.FlatAppearance.BorderSize = 0;
        _btnTake.Font             = new Font("Segoe UI", 9f, FontStyle.Bold);
        _btnTake.Size             = new Size(170, 36);
        _btnTake.Enabled          = false;
        _btnTake.Cursor           = Cursors.Hand;
        _btnTake.Click           += OnTakeCheckRide;

        _lblStatus.ForeColor = _text3;
        _lblStatus.Font      = new Font("Segoe UI", 9f);
        _lblStatus.AutoSize  = false;
        _lblStatus.Location  = new Point(16, 58);
        _lblStatus.Height    = 20;
        _lblStatus.Text      = "Select your flight for a CheckRide";

        _btnRefresh.Text             = "↻";
        _btnRefresh.BackColor        = Color.Transparent;
        _btnRefresh.ForeColor        = _text3;
        _btnRefresh.FlatStyle        = FlatStyle.Flat;
        _btnRefresh.FlatAppearance.BorderColor = _border;
        _btnRefresh.FlatAppearance.BorderSize  = 1;
        _btnRefresh.Font             = new Font("Segoe UI", 12f);
        _btnRefresh.Size             = new Size(36, 36);
        _btnRefresh.Cursor           = Cursors.Hand;
        _btnRefresh.Click           += async (s, e) => await LoadDataAsync();

        _btnOpenFlight.Text             = "Open in SimLetsFly";
        _btnOpenFlight.BackColor        = Color.Transparent;
        _btnOpenFlight.ForeColor        = _accent;
        _btnOpenFlight.FlatStyle        = FlatStyle.Flat;
        _btnOpenFlight.FlatAppearance.BorderColor = _accent;
        _btnOpenFlight.FlatAppearance.BorderSize  = 1;
        _btnOpenFlight.Font             = new Font("Segoe UI", 9f);
        _btnOpenFlight.Size             = new Size(150, 36);
        _btnOpenFlight.Enabled          = false;
        _btnOpenFlight.Cursor           = Cursors.Hand;
        _btnOpenFlight.Click           += OnOpenFlight;

        bottom.Controls.AddRange(new Control[] { _btnHelp, _btnRefresh, _btnDebug, _btnOpenFlight, _btnRetry, _btnCancel, _btnTake, _lblStatus });

        bottom.Resize += (s, e) =>
        {
            _btnTake.Location       = new Point(bottom.Width - _btnTake.Width - 16, 18);
            _btnCancel.Location     = new Point(_btnTake.Left - _btnCancel.Width - 8, 18);
            _btnRetry.Location      = new Point(_btnTake.Left - _btnCancel.Width - _btnRetry.Width - 16, 18);
            _btnOpenFlight.Location = new Point(_btnTake.Left - _btnCancel.Width - _btnRetry.Width - _btnOpenFlight.Width - 24, 18);
            _btnRefresh.Location    = new Point(_btnTake.Left - _btnCancel.Width - _btnRetry.Width - _btnOpenFlight.Width - _btnRefresh.Width - 32, 18);
            _lblStatus.Width        = Math.Max(120, _btnRefresh.Left - 32);
        };

        Controls.Add(bottom);
    }

    private void BuildGrid()
    {
        _grid.Dock                   = DockStyle.Fill;
        _grid.BackgroundColor        = _bg;
        _grid.GridColor              = _border;
        _grid.BorderStyle            = BorderStyle.None;
        _grid.CellBorderStyle        = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.RowHeadersVisible      = false;
        _grid.AllowUserToAddRows     = false;
        _grid.AllowUserToDeleteRows  = false;
        _grid.AllowUserToResizeRows  = false;
        _grid.MultiSelect            = false;
        _grid.SelectionMode          = DataGridViewSelectionMode.FullRowSelect;
        _grid.ReadOnly               = true;
        _grid.EnableHeadersVisualStyles = false;

        _grid.DefaultCellStyle.BackColor          = _bg;
        _grid.DefaultCellStyle.ForeColor          = _text;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 55, 75);
        _grid.DefaultCellStyle.SelectionForeColor = _accent;
        _grid.DefaultCellStyle.Padding            = new Padding(4, 0, 4, 0);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = _bg2;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 55, 75);

        _grid.ColumnHeadersDefaultCellStyle.BackColor          = Color.FromArgb(8, 28, 45);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor          = Color.FromArgb(160, 195, 220);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(8, 28, 45);
        _grid.ColumnHeadersDefaultCellStyle.Font               = new Font("Segoe UI", 8f, FontStyle.Bold);
        _grid.ColumnHeadersBorderStyle                         = DataGridViewHeaderBorderStyle.Single;
        _grid.ColumnHeadersHeight    = 30;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowTemplate.Height     = 36;

        AddCol("Date",    90,  DataGridViewContentAlignment.MiddleLeft);
        AddCol("Route",   130, DataGridViewContentAlignment.MiddleLeft);
        AddCol("NM",      55,  DataGridViewContentAlignment.MiddleRight);
        AddCol("Flight",  -1,  DataGridViewContentAlignment.MiddleLeft);  // fill
        AddCol("Grade",   60,  DataGridViewContentAlignment.MiddleCenter);
        AddCol("Score",   65,  DataGridViewContentAlignment.MiddleRight);

        var crLinkCol = new DataGridViewLinkColumn
        {
            Name              = "CR",
            HeaderText        = "",
            Width             = 30,
            AutoSizeMode      = DataGridViewAutoSizeColumnMode.None,
            LinkColor         = _accent,
            VisitedLinkColor  = _accent,
            ActiveLinkColor   = Color.White,
            TrackVisitedState = false,
            DefaultCellStyle  = { Alignment = DataGridViewContentAlignment.MiddleCenter },
        };
        _grid.Columns.Add(crLinkCol);

        _grid.Columns["Flight"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        _grid.CellFormatting     += OnCellFormatting;
        _grid.SelectionChanged   += OnGridSelectionChanged;
        _grid.CellDoubleClick    += OnGridDoubleClick;
        _grid.CellContentClick   += OnGridCellContentClick;

        Controls.Add(_grid);
    }

    private void AddCol(string name, int width, DataGridViewContentAlignment align)
    {
        var col = new DataGridViewTextBoxColumn
        {
            Name           = name,
            HeaderText     = name.ToUpper(),
            SortMode       = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = { Alignment = align },
        };
        if (width > 0) { col.Width = width; col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None; }
        _grid.Columns.Add(col);
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    private async Task LoadDataAsync()
    {
        _lblStatus.Text = "Loading your flights…"; _lblStatus.ForeColor = _text3;
        try
        {
            var flightsTask = _client.GetFlightsAsync();
            var scoresTask  = _client.GetLastScoresAsync();
            await Task.WhenAll(flightsTask, scoresTask);
            _flights = flightsTask.Result;
            _scores  = scoresTask.Result;
            PopulateGrid();
            if (_state == AppState.Idle)
                SetState(AppState.Idle); // resets status to "Select your flight…"
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Session expired", StringComparison.OrdinalIgnoreCase))
            {
                await _client.SignOutAsync();
                SessionStore.Clear();
                Application.Restart();
                return;
            }
            _lblStatus.Text = $"Error loading flights: {ex.Message}"; _lblStatus.ForeColor = _red;
        }
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();
        foreach (var f in _flights)
        {
            _scores.TryGetValue(f.Id, out var last);
            _grid.Rows.Add(
                f.CreatedAt.ToLocalTime(),   // DateTime — sorts correctly; formatted in CellFormatting
                f.DisplayRoute,
                f.DistanceNm,                // int — CellFormatting shows "—" when 0
                f.DisplayFlight,
                last.Grade ?? "—",
                last.Score,                  // int — CellFormatting shows "—" when 0
                last.Grade is not null ? "↗" : "");  // CR deep-link
            _grid.Rows[^1].Tag = f;
        }
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_grid.Columns[e.ColumnIndex].Name == "Date" && e.Value is DateTime dt)
        {
            e.Value = dt.ToString("MMM d, yyyy");
            e.FormattingApplied = true;
            return;
        }

        if (e.Value is int n && n == 0 &&
            _grid.Columns[e.ColumnIndex].Name is "Score" or "NM")
        {
            e.Value = "—";
            e.CellStyle.ForeColor = _text3;
            e.FormattingApplied = true;
            return;
        }

        if (_grid.Columns[e.ColumnIndex].Name != "Grade") return;
        if (e.Value is not string g || g == "—") return;

        e.CellStyle.ForeColor = g switch
        {
            "S" or "A" => _green,
            "B"        => _accent,
            "C"        => _amber,
            _          => _red
        };
        e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
        e.FormattingApplied = true;
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_state != AppState.Idle) return;
        _btnTake.Enabled = _grid.SelectedRows.Count > 0;
    }

    private SavedFlight? SelectedFlight =>
        _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].Tag as SavedFlight : null;

    // ── Actions ───────────────────────────────────────────────────────────────

    private async void OnSignOut(object? sender, EventArgs e)
    {
        await _client.SignOutAsync();
        SessionStore.Clear();
        Application.Restart();
    }

    private void OnGridDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var flight = _grid.Rows[e.RowIndex].Tag as SavedFlight;
        if (flight is null) return;
        OpenFlightInSimLetsFly(flight);
    }

    private void OnGridCellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Columns[e.ColumnIndex].Name != "CR") return;
        var flight = _grid.Rows[e.RowIndex].Tag as SavedFlight;
        if (flight is null) return;
        _scores.TryGetValue(flight.Id, out var score);
        var url = $"https://simletsfly.com/report.html?id={score.ResultId}";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private void OnOpenFlight(object? sender, EventArgs e)
    {
        var flight = _activeFlight ?? SelectedFlight;
        if (flight is null) return;
        OpenFlightInSimLetsFly(flight);
    }

    private static void OpenFlightInSimLetsFly(SavedFlight flight)
    {
        var url = $"https://simletsfly.com/index.html?dep={flight.DepId}&arr={flight.ArrId}";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private void OnTakeCheckRide(object? sender, EventArgs e)
    {
        var flight = SelectedFlight;
        if (flight is null) return;

        _activeFlight  = flight;
        _pendingUpload = null;
        _btnRetry.Visible = false;
        SetState(AppState.WaitingXP12);
        _lblStatus.Text      = $"Waiting for XP12 to be available…  Open XP12 and load your aircraft for {flight.DisplayRoute}.";
        _lblStatus.ForeColor = _amber;
        _watchTimer.Start();
    }

    private async void OnCancel(object? sender, EventArgs e)
    {
        var msg = _state == AppState.Recording
            ? "Cancel this CheckRide?\n\nThe flight session will end and no score will be uploaded."
            : "Stop waiting for X-Plane?\n\nNo score will be uploaded.";

        var result = MessageBox.Show(msg, "Cancel CheckRide",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes) return;

        _watchTimer.Stop();

        XP12Connector? conn = _connector;
        _connector = null;
        if (conn is not null)
        {
            try { await conn.StopAsync(); } catch { }
        }

        _logger?.Close();
        _monitor      = null;
        _logger       = null;
        _activeFlight = null;

        SetState(AppState.Idle);
    }

    private async void OnDebugUpload(object? sender, EventArgs e)
    {
        var flight = SelectedFlight;
        if (flight is null) return;

        var report = LoadBestSample();
        if (report is null)
        {
            _lblStatus.Text = "No sample files found in samples/ folder."; _lblStatus.ForeColor = _red;
            return;
        }

        // Randomise score so each debug run looks distinct in the list
        var rng   = new Random();
        var score = rng.Next(60, 97);
        report.Score    = score;
        report.Grade    = score >= 90 ? "A" : score >= 80 ? "B" : score >= 70 ? "C" : score >= 60 ? "D" : "F";
        report.Aircraft = report.Aircraft.Length > 0 ? report.Aircraft + " [Simulated]" : "King Air 350 [Simulated]";

        SetState(AppState.Uploading);
        try
        {
            await _client.UploadCheckRideAsync(flight.Id, report, null);
            await LoadDataAsync();
            SetState(AppState.Idle);
            _lblStatus.Text      = "CheckRide successfully uploaded";
            _lblStatus.ForeColor = _green;
        }
        catch (Exception ex)
        {
            SetState(AppState.Idle);
            _lblStatus.Text      = $"Upload error: {ex.Message}";
            _lblStatus.ForeColor = _red;
        }
    }

    private static CheckRideReport? LoadBestSample()
    {
        var samplesDir = Path.Combine(EmbeddedAssets.Dir, "samples");
        if (!Directory.Exists(samplesDir)) return null;

        // Only load files that match the current schema (have ScoringVersion field)
        var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        CheckRideReport? best = null;

        foreach (var file in Directory.GetFiles(samplesDir, "checkride_*.json")
                                      .OrderByDescending(f => f)) // newest first
        {
            try
            {
                var json   = File.ReadAllText(file);
                var report = System.Text.Json.JsonSerializer.Deserialize<CheckRideReport>(json, opts);
                if (report?.ScoringVersion is null or "") continue; // skip old-format files
                if (best is null || report.Events.Count > best.Events.Count)
                    best = report;
            }
            catch { }
        }
        return best;
    }

    // ── Monitoring ────────────────────────────────────────────────────────────

    private async void OnWatchTick(object? sender, EventArgs e)
    {
        if (_connector is not null) return;

        _lblStatus.Text      = $"Looking for XP12…  ({DateTime.Now:HH:mm:ss})";
        _lblStatus.ForeColor = _amber;

        var live = await XP12Connector.ProbeAsync();
        if (!live)
        {
            _lblStatus.Text = $"XP12 not detected — open XP12 and load your aircraft for {_activeFlight!.DisplayRoute}.";
            return;
        }

        _watchTimer.Stop();
        StartRecording();
    }

    private void StartRecording()
    {
        try
        {
            _sessionTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dir = OutputDir();
            Directory.CreateDirectory(dir);

            var flightId = _activeFlight!.Id[..8]; // first 8 chars of UUID is enough to identify
            _sessionLogPath = Path.Combine(dir, $"checkride_{flightId}_{_sessionTimestamp}.log");
            _logger    = new EventLogger(_sessionLogPath);
            _monitor   = new FlightMonitor(
                _activeFlight!.DepId, _activeFlight!.DepLat, _activeFlight!.DepLon,
                _activeFlight!.ArrId, _activeFlight!.ArrLat, _activeFlight!.ArrLon);
            _connector = new XP12Connector();

            _connector.Log       = msg => _logger.Log($"[XP12] {msg}");
            _engineStartAnnounced = false;
            _connector.Connected     = () => BeginInvoke(() =>
            {
                PlaySoundRandom("ready");
                ShowTrayBalloon?.Invoke("CheckRide is Recording", $"Flight: {_activeFlight!.DisplayRoute}");
            });
            _connector.Disconnected  = () => BeginInvoke(OnSimDisconnected);

            _connector.FlightDataReceived += snap =>
            {
                _monitor.OnSnapshot(snap, _logger!);
                if (!_engineStartAnnounced && (snap.Engine1Running || snap.Engine2Running))
                {
                    _engineStartAnnounced = true;
                    BeginInvoke(() => PlaySoundRandom("engine_start"));
                }
            };
            _monitor.AfterTakeoffCallout  += () => BeginInvoke(() => PlaySoundRandom("after_takeoff"));
            _monitor.TouchdownCallout     += q  => BeginInvoke(() => PlaySoundRandom($"landing_{q}"));
            _monitor.CalloutRain          += () => BeginInvoke(() => PlaySoundRandom("callout_rain"));
            _monitor.CalloutIcing         += () => BeginInvoke(() => PlaySoundRandom("callout_icing"));
            _monitor.CalloutTurbulence    += () => BeginInvoke(() => PlaySoundRandom("callout_turbulence"));
            _monitor.CalloutOverspeed     += () => BeginInvoke(() => PlaySoundRandom("callout_overspeed"));
            _monitor.CalloutHighBank      += () => BeginInvoke(() => PlaySoundRandom("callout_highbank"));
            _monitor.WrongDepartureDetected += () => BeginInvoke(OnWrongDepartureDetected);
            _monitor.FlightCompleted        += () => BeginInvoke(OnFlightCompleted);

            _ = _connector.StartAsync();
            SetState(AppState.Recording);
            _lblStatus.Text      = $"Flight in Progress  ·  Log: {_sessionLogPath}";
            _lblStatus.ForeColor = _green;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Failed to start: {ex.Message}"; _lblStatus.ForeColor = _red;
            SetState(AppState.Idle);
            _connector = null; _monitor = null; _logger = null;
        }
    }

    private void OnSimDisconnected()
    {
        _watchTimer.Stop();
        _ = _connector?.StopAsync();
        _logger?.Close();
        _connector = null; _monitor = null; _logger = null;
        SetState(AppState.Idle);
        _lblStatus.Text      = "Flight not complete — lost communication with sim.";
        _lblStatus.ForeColor = _amber;
        PlaySound(Path.Combine("system_notices", "lost_sim.wav"));
    }

    private async void OnWrongDepartureDetected()
    {
        PlaySound(Path.Combine("system_notices", "wrong_depart.wav"));

        _watchTimer.Stop();
        XP12Connector? conn = _connector;
        _connector = null;
        if (conn is not null)
            try { await conn.StopAsync(); } catch { }

        _logger?.Close();
        _monitor      = null;
        _logger       = null;
        _activeFlight = null;

        SetState(AppState.Idle);
        _lblStatus.Text      = "Wrong departure airport — recording cancelled, no upload.";
        _lblStatus.ForeColor = _red;
    }

    private async void OnFlightCompleted()
    {
        SetState(AppState.Uploading);

        XP12Connector? conn = _connector;
        _connector = null;

        try
        {
            if (conn is not null) await conn.StopAsync();

            var report = _monitor!.BuildReport();
            _logger!.Log($"Session ended — Score: {report.Score}  Grade: {report.Grade}");
            _logger.Close();

            if (report.Summary.Crashed)
                PlaySoundRandom("landing_crash");

            var gradeWavSuffix = report.Grade is "S" or "A" or "B" ? "excellent"
                               : report.Grade == "C"                ? "good"
                               : "poor";

            var jsonPath = Path.Combine(OutputDir(), $"checkride_{_activeFlight!.Id[..8]}_{_sessionTimestamp}.json");
            ReportWriter.Write(report, jsonPath);

            string? logPath = null;
            _pendingUpload = (_activeFlight!.Id, report, logPath);

            var uploaded = await UploadPendingAsync();
            if (uploaded)
            {
                PlaySound(Path.Combine("system_notices", "upload_success.wav"));
            }
            else
            {
                PlaySound(Path.Combine("system_notices", "upload_failed.wav"));
            }

            // Debrief plays after parking brake — crashes end the flight immediately so no debrief
            if (!report.Summary.Crashed)
            {
                await Task.Delay(4000);
                PlaySoundRandom($"parking_brake_{gradeWavSuffix}");
            }
        }
        catch (Exception ex)
        {
            PlaySound(Path.Combine("system_notices", "upload_failed.wav"));
            SetState(AppState.Idle);
            _lblStatus.Text      = $"Upload failed — flight saved locally. Click Retry when online.";
            _lblStatus.ForeColor = _red;
            _btnRetry.Visible    = true;
            _logger?.Log($"Upload error: {ex.Message}");
        }
        finally
        {
            _monitor = null; _logger = null;
        }
    }

    private async void OnRetryUpload(object? sender, EventArgs e)
    {
        if (_pendingUpload is null) return;
        _btnRetry.Visible = false;
        SetState(AppState.Uploading);
        var uploaded = await UploadPendingAsync();
        PlaySound(Path.Combine("system_notices", uploaded ? "upload_success.wav" : "upload_failed.wav"));
    }

    // Returns true when the upload succeeded (callers gate success sounds on this)
    private async Task<bool> UploadPendingAsync()
    {
        if (_pendingUpload is null) return false;
        var (flightId, report, logPath) = _pendingUpload.Value;
        try
        {
            await _client.UploadCheckRideAsync(flightId, report, logPath);
            _pendingUpload = null;
            ShowTrayBalloon?.Invoke("CheckRide Complete", $"Score: {report.Score}  Grade: {report.Grade}");
            await LoadDataAsync();
            SetState(AppState.Idle);
            _lblStatus.Text      = $"CheckRide successfully uploaded  ·  Score: {report.Score}  Grade: {report.Grade}";
            _lblStatus.ForeColor = _green;
            return true;
        }
        catch
        {
            SetState(AppState.Idle);
            _lblStatus.Text      = "Upload failed — flight saved locally. Click Retry when online.";
            _lblStatus.ForeColor = _red;
            _btnRetry.Visible    = true;
            return false;
        }
    }

    // ── State helpers ─────────────────────────────────────────────────────────

    private void SetState(AppState state, bool uploadError = false)
    {
        _state = state;
        var idle      = state == AppState.Idle;
        var active    = state is AppState.WaitingXP12 or AppState.Recording;

        _btnTake.Text    = state switch
        {
            AppState.WaitingXP12 => "Waiting for XP12…",
            AppState.Recording   => "Flight in Progress",
            AppState.Uploading   => "Uploading…",
            _                    => "TAKE CHECKRIDE"
        };
        _btnTake.Enabled         = idle && _grid.SelectedRows.Count > 0;
        _btnTake.BackColor       = idle ? _accent : _border;
        _btnTake.ForeColor       = idle ? Color.FromArgb(10, 13, 16) : _text3;
        _btnDebug.Enabled        = idle && _grid.SelectedRows.Count > 0;
        _btnOpenFlight.Enabled   = (idle && _grid.SelectedRows.Count > 0) || _activeFlight is not null;
        _btnRefresh.Enabled      = idle;
        _btnCancel.Visible       = active;
        _grid.Enabled            = idle;
        _btnSignOut.Enabled      = idle;

        // Built-in status messages per state
        (string msg, Color col) = state switch
        {
            AppState.Idle        => ("Select your flight for a CheckRide", _text3),
            AppState.WaitingXP12 => ("Waiting for XP12 to be available…", _amber),
            AppState.Recording   => ("Flight in Progress", _green),
            AppState.Uploading   => ("Flight Complete — uploading…", _accent),
            AppState.Done        => uploadError
                                    ? ("Error connecting to server to upload", _red)
                                    : ("CheckRide successfully uploaded", _green),
            _                    => ("", _text3)
        };
        _lblStatus.Text      = msg;
        _lblStatus.ForeColor = col;
    }

    // ── Help dialog ───────────────────────────────────────────────────────────

    private void ShowHelp()
    {
        using var dlg = new Form
        {
            Text            = "CheckRide — How It Works",
            ClientSize      = new Size(480, 460),
            BackColor       = _bg,
            ForeColor       = _text,
            Font            = new Font("Segoe UI", 9.5f),
            FormBorderStyle = FormBorderStyle.FixedSingle,
            MaximizeBox     = false,
            MinimizeBox     = false,
            StartPosition   = FormStartPosition.CenterParent,
        };
        var icoPath = Path.Combine(EmbeddedAssets.Dir, "images", "icon_256x256.ico");
        if (File.Exists(icoPath)) try { dlg.Icon = new Icon(icoPath); } catch { }

        const string content =
            "SCREEN COMPONENTS\r\n" +
            "\r\n" +
            "Flight list  —  Your upcoming SimLetsFly flights. Select one before clicking Take CheckRide.\r\n" +
            "\r\n" +
            "Grade / Score  —  Shown after a CheckRide is completed for that flight.\r\n" +
            "\r\n" +
            "↻ Refresh  —  Reloads your flight list from SimLetsFly.\r\n" +
            "\r\n" +
            "Open in SimLetsFly  —  Opens the selected flight's page in your browser.\r\n" +
            "\r\n" +
            "TAKE CHECKRIDE  —  Starts a live recording session for the selected flight.\r\n" +
            "\r\n" +
            "\r\n" +
            "HOW IT WORKS\r\n" +
            "\r\n" +
            "1.  Select a flight from the list above.\r\n" +
            "\r\n" +
            "2.  Click TAKE CHECKRIDE — the app waits for X-Plane 12 to be running.\r\n" +
            "\r\n" +
            "3.  Open X-Plane 12 and load your aircraft for the route.\r\n" +
            "\r\n" +
            "4.  Fly the complete route. CheckRide monitors your performance in real time.\r\n" +
            "\r\n" +
            "5.  After landing, taxi clear of the runway and set the parking brake.\r\n" +
            "\r\n" +
            "6.  Your score uploads to SimLetsFly automatically.\r\n" +
            "\r\n" +
            "\r\n" +
            "Questions or issues?  Email us at support@simletsfly.com";

        var rtb = new RichTextBox
        {
            Text        = content,
            ReadOnly    = true,
            BackColor   = _bg,
            ForeColor   = _text,
            BorderStyle = BorderStyle.None,
            Font        = new Font("Segoe UI", 9.5f),
            Dock        = DockStyle.None,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            Bounds      = new Rectangle(28, 20, 424, 380),
            TabStop     = false,
        };

        // Bold the two section headers
        foreach (var header in new[] { "SCREEN COMPONENTS", "HOW IT WORKS" })
        {
            var idx = rtb.Text.IndexOf(header, StringComparison.Ordinal);
            if (idx >= 0)
            {
                rtb.Select(idx, header.Length);
                rtb.SelectionFont  = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                rtb.SelectionColor = _accent;
            }
        }
        rtb.SelectionStart = 0;

        var lnkMore = new LinkLabel
        {
            Text      = "More info at simletsfly.com/checkride",
            Bounds    = new Rectangle(28, 404, 424, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = _text3,
        };
        lnkMore.LinkColor        = _accent;
        lnkMore.ActiveLinkColor  = Color.White;
        lnkMore.VisitedLinkColor = _accent;
        lnkMore.LinkArea         = new LinkArea(lnkMore.Text.IndexOf("simletsfly.com/checkride"), "simletsfly.com/checkride".Length);
        lnkMore.LinkClicked     += (s, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://simletsfly.com/checkride") { UseShellExecute = true }); } catch { }
        };

        var btnClose = new Button
        {
            Text      = "Close",
            Bounds    = new Rectangle(190, 430, 100, 32),
            BackColor = Color.Transparent,
            ForeColor = _text3,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f),
            Cursor    = Cursors.Hand,
        };
        btnClose.FlatAppearance.BorderColor = _border;
        btnClose.Click += (s, e) => dlg.Close();

        dlg.ClientSize = new Size(480, 474);
        dlg.Controls.AddRange(new Control[] { rtb, lnkMore, btnClose });
        dlg.ShowDialog(this);
    }

    // ── Misc ──────────────────────────────────────────────────────────────────

    private static string OutputDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CheckRide");

    private static readonly Random _soundRng = new();

    // Plays a single named file from the sounds\ root (start.wav, stop.wav, etc.)
    private static void PlaySound(string file)
    {
        try
        {
            var path = Path.Combine(EmbeddedAssets.Dir, "sounds", file);
            if (File.Exists(path)) new SoundPlayer(path).Play();
        }
        catch { }
    }

    // Picks a random WAV from sounds\<folder>\ and plays it
    private static void PlaySoundRandom(string folder)
    {
        try
        {
            var dir = Path.Combine(EmbeddedAssets.Dir, "sounds", folder);
            if (!Directory.Exists(dir)) return;
            var files = Directory.GetFiles(dir, "*.wav");
            if (files.Length == 0) return;
            new SoundPlayer(files[_soundRng.Next(files.Length)]).Play();
        }
        catch { }
    }

    // Optional hook for balloon tips (e.g. from a tray icon wrapper)
    public Action<string, string>? ShowTrayBalloon { get; set; }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && _state == AppState.Recording)
        {
            var r = MessageBox.Show(
                "A flight is currently being recorded.\nClosing will cancel the recording and lose this session.\n\nClose anyway?",
                "Recording in progress",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r == DialogResult.No) { e.Cancel = true; return; }
        }
        base.OnFormClosing(e);
    }
}
