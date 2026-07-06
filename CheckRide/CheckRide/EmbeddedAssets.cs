using System.Reflection;

namespace CheckRide;

// Extracts embedded assets (sounds, images, refdata) to a versioned folder in LocalApplicationData
// on first run. Subsequent runs find the folder already there and skip extraction.
// This allows PublishSingleFile to bundle everything into one .exe with no loose files.
internal static class EmbeddedAssets
{
    private static string? _dir;
    public static string Dir => _dir ?? throw new InvalidOperationException("EmbeddedAssets.Extract() was not called");

    public static void Extract()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimLetsFly", "CheckRide", "assets", version);

        if (!Directory.Exists(root))
        {
            Directory.CreateDirectory(root);

            var asm = Assembly.GetExecutingAssembly();
            const string prefix = "CheckRide.";

            foreach (var name in asm.GetManifestResourceNames())
            {
                if (!name.StartsWith(prefix)) continue;
                var rel = name[prefix.Length..];

                if (!rel.StartsWith("sounds.") && !rel.StartsWith("images.") &&
                    !rel.StartsWith("refdata.") && !rel.StartsWith("samples.")) continue;

                var relPath  = ResourceNameToPath(rel);
                var fullPath = Path.Combine(root, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                using var rs = asm.GetManifestResourceStream(name)!;
                using var fs = File.Create(fullPath);
                rs.CopyTo(fs);
            }
        }

        _dir = root;
    }

    // "sounds.after_takeoff.1.wav"      →  "sounds\after_takeoff\1.wav"
    // "sounds.system_notices.lost_sim.wav" →  "sounds\system_notices\lost_sim.wav"
    // "images.icon_256x256.ico"         →  "images\icon_256x256.ico"
    // Rule: last two dot-segments are always the filename stem + extension;
    //       all segments before that form the directory path.
    private static string ResourceNameToPath(string rel)
    {
        var parts = rel.Split('.');
        if (parts.Length < 2) return rel;
        var dirs = parts[..^2];
        var file = parts[^2] + "." + parts[^1];
        return dirs.Length == 0 ? file : Path.Combine([.. dirs, file]);
    }
}
