using System.Text.Json;
using System.Text.Json.Serialization;
using CheckRide.Models;

namespace CheckRide;

public static class ReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Write(CheckRideReport report, string path)
    {
        var json = JsonSerializer.Serialize(report, Options);
        File.WriteAllText(path, json);
    }
}
