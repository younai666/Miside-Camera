using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Miside.Camera.Desktop;

internal static class HostLocator
{
    internal static bool IsSupported(string path) => Path.GetFileName(path).Equals("NeuroMita.exe", StringComparison.OrdinalIgnoreCase);
    private static bool IsGame(string path) => IsSupported(path) || Path.GetFileName(path).Equals("MiSide.exe", StringComparison.OrdinalIgnoreCase)
        || Path.GetFileName(path).Equals("MiSideFull.exe", StringComparison.OrdinalIgnoreCase);

    public static string? Find() => Find(SearchRoots());

    internal static string? Find(IEnumerable<string> roots)
    {
        string? neuroMita = null, miside = null;
        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase)) {
            foreach (string candidate in Candidates(root, 3)) {
                if (IsSupported(candidate)) {
                    if (File.Exists(Path.Combine(Path.GetDirectoryName(candidate)!, "BepInEx", "interop", "Assembly-CSharp.dll"))) return candidate;
                    neuroMita ??= candidate;
                } else miside ??= candidate;
            }
        }
        return neuroMita ?? miside;
    }

    private static IEnumerable<string> Candidates(string root, int depth, bool insideHost = false)
    {
        if (!Directory.Exists(root)) yield break;
        string[] files, directories;
        try {
            files = Directory.GetFiles(root, "*.exe");
            directories = depth > 0 ? Directory.GetDirectories(root).Where(path => {
                string name = Path.GetFileName(path);
                string parentName = Path.GetFileName(root);
                return insideHost || parentName.Contains("neuromita", StringComparison.OrdinalIgnoreCase) || parentName.Contains("miside", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("neuromita", StringComparison.OrdinalIgnoreCase) || name.Contains("miside", StringComparison.OrdinalIgnoreCase);
            }).ToArray() : Array.Empty<string>();
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { yield break; }
        foreach (string file in files.Where(IsGame)) yield return file;
        foreach (string directory in directories)
            foreach (string file in Candidates(directory, depth - 1, true)) yield return file;
    }

    private static IEnumerable<string> SearchRoots()
    {
        foreach (string name in new[] { "NeuroMita", "MiSide", "MiSideFull" }) {
            foreach (var process in Process.GetProcessesByName(name)) {
                string? path = null;
                using (process) {
                    try { path = process.MainModule?.FileName; }
                    catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
                }
                if (path != null) yield return Path.GetDirectoryName(path)!;
            }
        }
        // A portable Desktop may be placed inside or beside the game installation.
        var parent = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; parent != null && i < 3; i++, parent = parent.Parent) yield return parent.FullName;
        var steamRoots = new List<string>();
        foreach (string registry in new[] { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam" }) {
            string? root = Registry.GetValue(registry, "SteamPath", null) as string ?? Registry.GetValue(registry, "InstallPath", null) as string;
            if (root != null) steamRoots.Add(root);
        }
        steamRoots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            steamRoots.Add(Path.Combine(drive.RootDirectory.FullName, "SteamLibrary"));
        foreach (string root in steamRoots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()) {
            string libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile)) continue;
            foreach (Match match in Regex.Matches(ReadManifest(libraryFile), "\"path\"\\s*\"([^\"]+)\""))
                steamRoots.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
        }
        foreach (string root in steamRoots.Distinct(StringComparer.OrdinalIgnoreCase)) {
            yield return Path.Combine(root, "steamapps", "common");
            string manifest = Path.Combine(root, "steamapps", "appmanifest_2527500.acf");
            if (!File.Exists(manifest)) continue;
            var match = Regex.Match(ReadManifest(manifest), "\"installdir\"\\s*\"([^\"]+)\"");
            if (match.Success) yield return Path.Combine(root, "steamapps", "common", match.Groups[1].Value);
        }
    }

    private static string ReadManifest(string path)
    {
        try { return File.ReadAllText(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }
}
