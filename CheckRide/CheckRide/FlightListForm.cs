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

    // Shared font scale — reuse these instead of ad hoc `new Font(...)` sizes
    private static readonly Font _fontTitle     = new("Segoe UI", 16f, FontStyle.Bold); // "CHECKRIDE" wordmark
    private static readonly Font _fontBase      = new("Segoe UI", 10f);                 // body text (form default, status, inputs)
    private static readonly Font _fontBaseBold  = new("Segoe UI", 10f, FontStyle.Bold);
    private static readonly Font _fontLabel     = new("Segoe UI", 9f);                   // secondary labels/buttons
    private static readonly Font _fontLabelBold = new("Segoe UI", 9f, FontStyle.Bold);
    private static readonly Font _fontSmall     = new("Segoe UI", 8.5f);                 // tertiary/info text
    private static readonly Font _fontSmallBold = new("Segoe UI", 8.5f, FontStyle.Bold);
    private static readonly Font _fontIcon      = new("Segoe UI", 14f);                  // icon-only buttons (↻)
    private static readonly Font _fontIconBold  = new("Segoe UI", 13f, FontStyle.Bold);  // icon-only buttons (?)

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
        Font            = _fontBase;
        StartPosition   = FormStartPosition.CenterScreen;
        MinimumSize     = new Size(700, 480);
        ShowInTaskbar   = true;

        BuildGrid();
        BuildAircraftBar();
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
            Font      = _fontTitle,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize  = true,
            Location  = new Point(16, 13),
        };

        // Sim selector (placeholder — only XP12 for now)
        var lblSim = new Label
        {
            Text      = "Simulator:",
            ForeColor = _text3,
            Font      = _fontLabel,
            AutoSize  = true,
            Location  = new Point(0, 0), // positioned below
        };
        _lblSimValue.Text      = "X-Plane 12";
        _lblSimValue.ForeColor = _accent;
        _lblSimValue.Font      = _fontLabelBold;
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
        _lblUser.Font      = _fontLabel;
        _lblUser.AutoSize  = true;
        _lblUser.Text      = _client.Session.Email;

        _btnSignOut.Text             = "Sign Out";
        _btnSignOut.ForeColor        = _text3;
        _btnSignOut.BackColor        = Color.Transparent;
        _btnSignOut.FlatStyle        = FlatStyle.Flat;
        _btnSignOut.FlatAppearance.BorderColor = _border;
        _btnSignOut.FlatAppearance.BorderSize  = 1;
        _btnSignOut.Font             = _fontLabel;
        _btnSignOut.AutoSize         = true;
        _btnSignOut.Cursor           = Cursors.Hand;
        _btnSignOut.Click           += OnSignOut;

        var btnMyFlights = new Button
        {
            Text      = "My Flights ↗",
            ForeColor = _accent,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Font      = _fontLabel,
            AutoSize  = true,
            Cursor    = Cursors.Hand,
        };
        btnMyFlights.FlatAppearance.BorderSize = 0;
        btnMyFlights.Click += (s, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://simletsfly.com/flights.html") { UseShellExecute = true }); } catch { }
        };

        _btnOpenFlight.Text      = "Open Flight ↗";
        _btnOpenFlight.ForeColor = _accent;
        _btnOpenFlight.BackColor = Color.Transparent;
        _btnOpenFlight.FlatStyle = FlatStyle.Flat;
        _btnOpenFlight.FlatAppearance.BorderSize = 0;
        _btnOpenFlight.Font      = _fontLabel;
        _btnOpenFlight.AutoSize  = true;
        _btnOpenFlight.Enabled   = false;
        _btnOpenFlight.Cursor    = Cursors.Hand;
        _btnOpenFlight.Click    += OnOpenFlight;

        _btnRefresh.Text      = "↻";
        _btnRefresh.ForeColor = _text3;
        _btnRefresh.BackColor = Color.Transparent;
        _btnRefresh.FlatStyle = FlatStyle.Flat;
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Font      = _fontIcon;
        _btnRefresh.AutoSize  = true;
        _btnRefresh.Cursor    = Cursors.Hand;
        _btnRefresh.Click    += async (s, e) => await LoadDataAsync();

        top.Controls.AddRange(new Control[] { lblTitle, simPanel, _lblUser, _btnRefresh, _btnOpenFlight, btnMyFlights, _btnSignOut });

        top.Resize += (s, e) =>
        {
            _btnSignOut.Location    = new Point(top.Width - _btnSignOut.Width - 12, (top.Height - _btnSignOut.Height) / 2);
            btnMyFlights.Location   = new Point(_btnSignOut.Left - btnMyFlights.Width - 10, (top.Height - btnMyFlights.Height) / 2);
            _btnOpenFlight.Location = new Point(btnMyFlights.Left - _btnOpenFlight.Width - 14, (top.Height - _btnOpenFlight.Height) / 2);
            _btnRefresh.Location    = new Point(_btnOpenFlight.Left - _btnRefresh.Width - 10, (top.Height - _btnRefresh.Height) / 2);
            _lblUser.Location       = new Point(_btnRefresh.Left - _lblUser.Width - 16, (top.Height - _lblUser.Height) / 2);
            simPanel.Location       = new Point(_lblUser.Left - simPanel.Width - 20, (top.Height - simPanel.Height) / 2);
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
        _btnHelp.Font             = _fontIconBold;
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
        _btnDebug.Font             = _fontLabel;
        _btnDebug.Size             = new Size(140, 36);
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
        _btnRetry.Font             = _fontLabel;
        _btnRetry.Size             = new Size(116, 36);
        _btnRetry.Visible          = false;
        _btnRetry.Cursor           = Cursors.Hand;
        _btnRetry.Click           += OnRetryUpload;

        _btnCancel.Text             = "Cancel";
        _btnCancel.BackColor        = Color.Transparent;
        _btnCancel.ForeColor        = _red;
        _btnCancel.FlatStyle        = FlatStyle.Flat;
        _btnCancel.FlatAppearance.BorderColor = _red;
        _btnCancel.FlatAppearance.BorderSize  = 1;
        _btnCancel.Font             = _fontLabel;
        _btnCancel.Size             = new Size(96, 36);
        _btnCancel.Visible          = false;
        _btnCancel.Cursor           = Cursors.Hand;
        _btnCancel.Click           += OnCancel;

        _btnTake.Text             = "TAKE CHECKRIDE";
        _btnTake.BackColor        = _accent;
        _btnTake.ForeColor        = Color.FromArgb(10, 13, 16);
        _btnTake.FlatStyle        = FlatStyle.Flat;
        _btnTake.FlatAppearance.BorderSize = 0;
        _btnTake.Font             = _fontBaseBold;
        _btnTake.Size             = new Size(182, 36);
        _btnTake.Enabled          = false;
        _btnTake.Cursor           = Cursors.Hand;
        _btnTake.Click           += OnTakeCheckRide;

        _lblStatus.ForeColor = _text3;
        _lblStatus.Font      = _fontBase;
        _lblStatus.AutoSize  = false;
        _lblStatus.AutoEllipsis = true;
        _lblStatus.Location  = new Point(16, 58);
        _lblStatus.Height    = 22;
        _lblStatus.Text      = "Select your flight for a CheckRide";

        bottom.Controls.AddRange(new Control[] { _btnHelp, _btnDebug, _btnRetry, _btnCancel, _btnTake, _lblStatus });

        bottom.Resize += (s, e) =>
        {
            _btnTake.Location       = new Point(bottom.Width - _btnTake.Width - 16, 18);
            _btnCancel.Location     = new Point(_btnTake.Left - _btnCancel.Width - 8, 18);
            _btnRetry.Location      = new Point(_btnTake.Left - _btnCancel.Width - _btnRetry.Width - 16, 18);
            _lblStatus.Width        = bottom.Width - 32;
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
        _grid.ColumnHeadersDefaultCellStyle.Font               = _fontLabelBold;
        _grid.ColumnHeadersBorderStyle                         = DataGridViewHeaderBorderStyle.Single;
        _grid.ColumnHeadersHeight    = 34;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowTemplate.Height     = 40;

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
            var flightsTask      = _client.GetFlightsAsync();
            var scoresTask       = _client.GetLastScoresAsync();
            var lastAcTask       = _selectedAircraft == null && !_aircraftIsGeneric
                                   ? _client.GetLastAircraftIcaoAsync()
                                   : Task.FromResult<string?>(null);
            var lastTransAltTask = !_transitionAltUserChanged
                                   ? _client.GetLastTransitionAltitudeAsync()
                                   : Task.FromResult<int?>(null);
            await Task.WhenAll(flightsTask, scoresTask, lastAcTask, lastTransAltTask);
            _flights = flightsTask.Result;
            _scores  = scoresTask.Result;
            PopulateGrid();

            var lastIcao = lastAcTask.Result;
            if (lastIcao != null && _selectedAircraft == null && !_aircraftIsGeneric)
            {
                var match = lastIcao == "OTHER"
                    ? AircraftType.Other
                    : AircraftDb.Search(lastIcao)
                        .FirstOrDefault(a => a.IcaoCode.Equals(lastIcao, StringComparison.OrdinalIgnoreCase));
                if (match != null) SelectAircraft(match);
            }

            var lastTransAlt = lastTransAltTask.Result;
            if (lastTransAlt != null && !_transitionAltUserChanged)
            {
                var idx = Array.FindIndex(TransitionAltOptions, o => o.Ft == lastTransAlt.Value);
                if (idx >= 0)
                {
                    _suppressTransAltChange = true;
                    _cmbTransitionAlt.SelectedIndex = idx;
                    _suppressTransAltChange = false;
                }
            }

            if (_state == AppState.Idle)
                SetState(AppState.Idle);
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
        UpdateTakeButton();
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
        report.Grade    = score >= 90 ? "S" : score >= 80 ? "A" : score >= 70 ? "B" : score >= 55 ? "C" : score >= 40 ? "D" : "F";
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
                _activeFlight!.ArrId, _activeFlight!.ArrLat, _activeFlight!.ArrLon,
                SelectedTransitionAltFt);
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
            report.AircraftIcao = _selectedAircraft?.IcaoCode ?? (_aircraftIsGeneric ? "OTHER" : "");
            if (_selectedAircraft is { } ac)
            {
                report.AircraftManufacturer = ac.Manufacturer;
                report.AircraftModel        = ac.Model;
                report.AircraftEngineClass  = ac.EngineClass;
                report.AircraftWeightClass  = ac.WeightClass;
                report.AircraftWtc          = ac.WakeTurbulenceCategory;
                report.AircraftVrefKt       = ac.EffectiveVrefKt;
            }
            report.TransitionAltitudeFt = SelectedTransitionAltFt;
            _logger!.Log($"Session ended — Score: {report.Score}  Grade: {report.Grade}  Aircraft: {report.AircraftIcao}");
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
        bool hasAc = _selectedAircraft != null || _aircraftIsGeneric;
        _btnTake.Enabled         = idle && _grid.SelectedRows.Count > 0 && hasAc;
        _btnTake.BackColor       = idle ? _accent : _border;
        _btnTake.ForeColor       = idle ? Color.FromArgb(10, 13, 16) : _text3;
        _btnDebug.Enabled        = idle && _grid.SelectedRows.Count > 0 && hasAc;
        _btnOpenFlight.Enabled   = (idle && _grid.SelectedRows.Count > 0) || _activeFlight is not null;
        _btnRefresh.Enabled      = idle;
        _btnCancel.Visible       = active;
        _grid.Enabled            = idle;
        _btnSignOut.Enabled      = idle;
        _txtAircraftSearch.Enabled = idle;
        _btnAircraftClear.Enabled  = idle;
        _cmbTransitionAlt.Enabled  = idle;

        // Status messages per state
        (string msg, Color col) = state switch
        {
            AppState.WaitingXP12 => ("Waiting for XP12 to be available…", _amber),
            AppState.Recording   => ("Flight in Progress", _green),
            AppState.Uploading   => ("Flight Complete — uploading…", _accent),
            AppState.Done        => uploadError
                                    ? ("Error connecting to server to upload", _red)
                                    : ("CheckRide successfully uploaded", _green),
            _                    => ("", _text3)
        };
        if (state == AppState.Idle) UpdateIdleStatus();
        else { _lblStatus.Text = msg; _lblStatus.ForeColor = col; }
    }

    // ── Aircraft picker ───────────────────────────────────────────────────────

    private void BuildAircraftBar()
    {
        var bar = new Panel { BackColor = _panel, Height = 60, Dock = DockStyle.Bottom };
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(_border);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var lblType = new Label
        {
            Text      = "AIRCRAFT:",
            Font      = _fontLabelBold,
            ForeColor = _text3,
            AutoSize  = true,
            Location  = new Point(16, 14),
        };

        _txtAircraftSearch.PlaceholderText = "Search ICAO, manufacturer, or model…";
        _txtAircraftSearch.BackColor       = _bg2;
        _txtAircraftSearch.ForeColor       = _text;
        _txtAircraftSearch.BorderStyle     = BorderStyle.FixedSingle;
        _txtAircraftSearch.Font            = _fontBase;
        _txtAircraftSearch.Height          = 28;
        _txtAircraftSearch.Location        = new Point(86, 10);
        _txtAircraftSearch.TextChanged    += OnAircraftSearchChanged;
        _txtAircraftSearch.GotFocus       += (s, e) =>
        {
            if (!_suppressDropdown && _selectedAircraft == null && !_aircraftIsGeneric)
                UpdateAircraftList();
        };
        _txtAircraftSearch.Leave += (s, e) =>
            BeginInvoke(() => { if (!_lstAircraft.Focused) _lstAircraft.Visible = false; });

        _btnAircraftClear.Text             = "✕";
        _btnAircraftClear.FlatStyle        = FlatStyle.Flat;
        _btnAircraftClear.FlatAppearance.BorderSize = 0;
        _btnAircraftClear.Font             = _fontBase;
        _btnAircraftClear.ForeColor        = _text3;
        _btnAircraftClear.BackColor        = Color.Transparent;
        _btnAircraftClear.Size             = new Size(28, 28);
        _btnAircraftClear.Location         = new Point(0, 10); // x set in Resize
        _btnAircraftClear.Cursor           = Cursors.Hand;
        _btnAircraftClear.Visible          = false;
        _btnAircraftClear.Click           += (s, e) => ClearAircraft();

        _lblAircraftInfo.AutoSize  = false;
        _lblAircraftInfo.Height    = 18;
        _lblAircraftInfo.Font      = _fontSmallBold;
        _lblAircraftInfo.ForeColor = _text2;
        _lblAircraftInfo.Location  = new Point(86, 40);
        _lblAircraftInfo.Visible   = false;

        _lblTransAlt.Text      = "TRANS ALT:";
        _lblTransAlt.Font      = _fontLabelBold;
        _lblTransAlt.ForeColor = _text3;
        _lblTransAlt.AutoSize  = true;

        _cmbTransitionAlt.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbTransitionAlt.BackColor     = _bg2;
        _cmbTransitionAlt.ForeColor     = _text;
        _cmbTransitionAlt.FlatStyle     = FlatStyle.Flat;
        _cmbTransitionAlt.Font          = _fontSmall;
        _cmbTransitionAlt.Width         = 64;
        _cmbTransitionAlt.Height        = 24;
        _cmbTransitionAlt.Items.AddRange(TransitionAltOptions.Cast<object>().ToArray());
        _cmbTransitionAlt.SelectedIndex = 0; // FL180 default
        _cmbTransitionAlt.SelectedIndexChanged += (s, e) =>
        {
            if (!_suppressTransAltChange) _transitionAltUserChanged = true;
        };
        var transAltTip = new ToolTip();
        transAltTip.SetToolTip(_cmbTransitionAlt, "Altitude above which the altimeter should be set to 29.92\" / 1013 hPa");

        bar.Controls.AddRange(new Control[]
        {
            lblType, _txtAircraftSearch, _btnAircraftClear, _lblAircraftInfo, _lblTransAlt, _cmbTransitionAlt
        });

        bar.Resize += (s, e) =>
        {
            _cmbTransitionAlt.Location = new Point(bar.Width - _cmbTransitionAlt.Width - 16, 12);
            _lblTransAlt.Location      = new Point(_cmbTransitionAlt.Left - _lblTransAlt.Width - 6, 16);
            // Reserve room for the clear ("✕") button between the search box and the trans-alt
            // group — it's invisible until an aircraft is selected, so this space must be
            // reserved up front or the button overlaps the label once it appears.
            _txtAircraftSearch.Width   = _lblTransAlt.Left - 8 - 34 - 86;
            _btnAircraftClear.Location = new Point(_txtAircraftSearch.Right + 4, 10);
            _lblAircraftInfo.Width     = _txtAircraftSearch.Width;
        };

        // ListBox lives on the form for z-order overlay
        _lstAircraft.BackColor   = _bg2;
        _lstAircraft.ForeColor   = _text;
        _lstAircraft.Font        = _fontBase;
        _lstAircraft.BorderStyle = BorderStyle.FixedSingle;
        _lstAircraft.ItemHeight  = 22;
        _lstAircraft.Visible     = false;
        _lstAircraft.Click      += OnAircraftListClick;
        _lstAircraft.Leave      += (s, e) => _lstAircraft.Visible = false;
        _lstAircraft.KeyDown    += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) { _lstAircraft.Visible = false; _txtAircraftSearch.Focus(); }
            if (e.KeyCode == Keys.Enter)  OnAircraftListClick(s, EventArgs.Empty);
        };
        Controls.Add(_lstAircraft);
        Controls.Add(bar);
    }

    private void UpdateAircraftList()
    {
        var results = AircraftDb.Search(_txtAircraftSearch.Text).ToList();
        _lstAircraft.Items.Clear();
        foreach (var a in results) _lstAircraft.Items.Add(a);
        _lstAircraft.Items.Add(AircraftType.Other);

        var pt = _txtAircraftSearch.PointToScreen(Point.Empty);
        var fp = PointToClient(pt);
        int maxRows = Math.Max(1, (fp.Y - 8) / _lstAircraft.ItemHeight);
        int rows    = Math.Min(_lstAircraft.Items.Count, Math.Min(12, maxRows));
        int h       = rows * _lstAircraft.ItemHeight + 4;
        _lstAircraft.SetBounds(fp.X, fp.Y - h, _txtAircraftSearch.Width, h);
        _lstAircraft.Visible = true;
        _lstAircraft.BringToFront();
    }

    private void OnAircraftSearchChanged(object? sender, EventArgs e)
    {
        if (_suppressDropdown) return;
        if (_selectedAircraft != null || _aircraftIsGeneric)
        {
            _selectedAircraft  = null;
            _aircraftIsGeneric = false;
            _lblAircraftInfo.Visible  = false;
            _btnAircraftClear.Visible = false;
            UpdateTakeButton();
        }
        UpdateAircraftList();
    }

    private void OnAircraftListClick(object? sender, EventArgs e)
    {
        if (_lstAircraft.SelectedItem is not AircraftType a) return;
        SelectAircraft(a);
        _lstAircraft.Visible = false;
        _txtAircraftSearch.Focus();
    }

    private void SelectAircraft(AircraftType a)
    {
        _selectedAircraft  = a.IsOther ? null : a;
        _aircraftIsGeneric = a.IsOther;

        _suppressDropdown = true;
        _txtAircraftSearch.Text      = a.ToString();
        _txtAircraftSearch.ForeColor = a.IsOther ? _amber : _text;
        _txtAircraftSearch.Select(0, 0); // show from the start, not scrolled to the end
        _suppressDropdown = false;

        _lblAircraftInfo.Text      = a.InfoLine;
        _lblAircraftInfo.ForeColor = a.IsOther ? _amber : _text2;
        _lblAircraftInfo.Visible   = true;

        _btnAircraftClear.Visible = true;
        UpdateTakeButton();
    }

    private void ClearAircraft()
    {
        _selectedAircraft  = null;
        _aircraftIsGeneric = false;

        _suppressDropdown = true;
        _txtAircraftSearch.Text      = "";
        _txtAircraftSearch.ForeColor = _text;
        _suppressDropdown = false;

        _lblAircraftInfo.Visible  = false;
        _btnAircraftClear.Visible = false;
        _lstAircraft.Visible      = false;
        UpdateTakeButton();
    }

    private void UpdateTakeButton()
    {
        if (_state != AppState.Idle) return;
        bool hasAc     = _selectedAircraft != null || _aircraftIsGeneric;
        bool hasFlight = _grid.SelectedRows.Count > 0;
        _btnTake.Enabled  = hasFlight && hasAc;
        _btnDebug.Enabled = hasFlight && hasAc;
        UpdateIdleStatus();
    }

    private void UpdateIdleStatus()
    {
        bool hasFlight   = _grid.SelectedRows.Count > 0;
        bool hasAircraft = _selectedAircraft != null || _aircraftIsGeneric;

        var acCount = AircraftDb.Count;

        (_lblStatus.Text, _lblStatus.ForeColor) = (hasFlight, hasAircraft) switch
        {
            (false, false) => ($"Select a flight and aircraft type to begin ({acCount} aircraft available)", _text3),
            (true,  false) => ($"Select your aircraft type to continue ({acCount} available)", _text3),
            (false, true ) => ("Select a flight to begin", _text3),
            (true,  true ) when _aircraftIsGeneric =>
                ("⚠ Generic thresholds — results may be less accurate", _amber),
            _ => ("Ready — click Take CheckRide, then start X-Plane 12", _green),
        };
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
            Font            = _fontBase,
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
            Font        = _fontBase,
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
                rtb.SelectionFont  = _fontBaseBold;
                rtb.SelectionColor = _accent;
            }
        }
        rtb.SelectionStart = 0;

        var lnkMore = new LinkLabel
        {
            Text      = "More info at simletsfly.com/checkride",
            Bounds    = new Rectangle(28, 404, 424, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            Font      = _fontSmall,
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
            Font      = _fontLabel,
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

    // Aircraft picker
    private readonly TextBox  _txtAircraftSearch = new();
    private readonly Button   _btnAircraftClear  = new();
    private readonly Label    _lblAircraftInfo   = new();
    private readonly ListBox  _lstAircraft       = new();
    private AircraftType?     _selectedAircraft;
    private bool              _aircraftIsGeneric;
    private bool              _suppressDropdown;

    // Transition altitude picker
    private readonly ComboBox _cmbTransitionAlt       = new();
    private readonly Label    _lblTransAlt            = new();
    private bool              _suppressTransAltChange;
    private bool              _transitionAltUserChanged;

    private readonly record struct TransitionAltOption(string Label, int Ft)
    {
        public override string ToString() => Label;
    }

    private static readonly TransitionAltOption[] TransitionAltOptions =
    {
        new("FL180", 18000),
        new("FL100", 10000),
        new("FL050",  5000),
    };

    private int SelectedTransitionAltFt =>
        _cmbTransitionAlt.SelectedItem is TransitionAltOption opt ? opt.Ft : 18000;

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
