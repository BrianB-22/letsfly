namespace CheckRide;

public class EventLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly DateTime _sessionStart = DateTime.Now;

    public EventLogger(string path)
    {
        _writer = new StreamWriter(path, append: false) { AutoFlush = true };
        var appVersion = System.Reflection.Assembly.GetExecutingAssembly()
                             .GetName().Version?.ToString() ?? "unknown";
        Log($"CheckRide session started — {_sessionStart:yyyy-MM-dd HH:mm:ss}");
        Log($"App version: {appVersion}  Scoring: {CheckRide.Models.CheckRideReport.ScoringVersionConst}");
    }

    public void Log(string message)
    {
        var elapsed = (DateTime.Now - _sessionStart).TotalSeconds;
        _writer.WriteLine($"[{elapsed,7:F1}s] {message}");
    }

    public void Close() => _writer.Close();

    public void Dispose() => _writer.Dispose();
}
