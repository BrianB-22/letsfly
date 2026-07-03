using System.Text.Json;
using CheckRide.Models;

namespace CheckRide;

internal static class SessionStore
{
    private static string StorePath =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimLetsFly", "CheckRide", "session.json");

    public static SupabaseSession? Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return null;
            return JsonSerializer.Deserialize<SupabaseSession>(File.ReadAllText(StorePath));
        }
        catch { return null; }
    }

    public static void Save(SupabaseSession session)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(session));
        }
        catch { }
    }

    public static void Clear()
    {
        try { if (File.Exists(StorePath)) File.Delete(StorePath); }
        catch { }
    }
}
