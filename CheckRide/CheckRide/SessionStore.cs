using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CheckRide.Models;

namespace CheckRide;

// Session tokens are encrypted at rest with Windows DPAPI (per-user scope).
// Load() transparently migrates plaintext files from older versions.
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
            var raw = File.ReadAllBytes(StorePath);

            string json;
            try
            {
                var decrypted = ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser);
                json = Encoding.UTF8.GetString(decrypted);
            }
            catch (CryptographicException)
            {
                // Plaintext file from an older version — migrate to encrypted on next Save
                json = Encoding.UTF8.GetString(raw);
            }

            return JsonSerializer.Deserialize<SupabaseSession>(json);
        }
        catch { return null; }
    }

    public static void Save(SupabaseSession session)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(StorePath)!);
            var json      = JsonSerializer.Serialize(session);
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(StorePath, encrypted);
        }
        catch { }
    }

    public static void Clear()
    {
        try { if (File.Exists(StorePath)) File.Delete(StorePath); }
        catch { }
    }
}
