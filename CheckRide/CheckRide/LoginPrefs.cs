using System.Text.Json;

namespace CheckRide;

// Remembers the last successfully used email address for "Remember Me" on the login
// screen. Just an email, not a session/token, so no DPAPI encryption like SessionStore --
// this is a convenience pre-fill, not something sensitive.
internal static class LoginPrefs
{
    private static string StorePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimLetsFly", "CheckRide", "login_prefs.json");

    private record Prefs(string? Email, bool RememberMe);

    public static (string? Email, bool RememberMe) Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return (null, false);
            var prefs = JsonSerializer.Deserialize<Prefs>(File.ReadAllText(StorePath));
            return prefs is null ? (null, false) : (prefs.Email, prefs.RememberMe);
        }
        catch { return (null, false); }
    }

    // Called after every successful login. Stores the email only when rememberMe is
    // checked, so unchecking it on a later login forgets the previously saved address.
    public static void Save(string email, bool rememberMe)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            var prefs = new Prefs(rememberMe ? email : null, rememberMe);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(prefs));
        }
        catch { }
    }
}
