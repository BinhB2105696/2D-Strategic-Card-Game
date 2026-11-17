#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

// Keep the entire old Player, including its embedded catalog, before BuildPlayer replaces it.
public static class SimulationRunnerArchive
{
    public static void Preserve(string directory)
    {
        string project = Path.GetFullPath(".");
        string source = Path.GetFullPath(directory);
        string expected = Path.Combine(project, "Build", "Simulation");
        if (!string.Equals(source, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the project's simulation Player may be archived.");
        if (!File.Exists(Path.Combine(source, "CardGameSimulation.exe"))) return;
        var files = new List<string>();
        Collect(source, files);
        files.Sort(StringComparer.Ordinal);
        string destination = Path.Combine(project, "SimulationResults", "Runners",
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(destination);
        var entries = new List<object>();
        foreach (string file in files)
        {
            string relative = file.Substring(source.Length + 1);
            string copy = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            string hash = Hash(file);
            File.Copy(file, copy, false);
            if (Hash(copy) != hash) throw new IOException("Runner archive verification failed: " + relative);
            entries.Add(SimulationJson.Object("path", relative.Replace('\\', '/'), "bytes", new FileInfo(file).Length, "sha256", hash));
        }
        SimulationJson.Atomic(Path.Combine(destination, "archive-manifest.json"), SimulationJson.Object(
            "schema", 1, "createdUtc", DateTime.UtcNow.ToString("o"), "sourceDirectory", source,
            "operation", "copied, originals retained; all copied files hash-verified", "files", entries,
            "compatibility", "Preserves the old binary/catalog, not a claim that it matches every historical trace. Use -RunnerDirectory and check the replay report."));
        UnityEngine.Debug.Log("Previous simulation runner preserved: " + destination);
    }

    private static void Collect(string directory, List<string> files)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Runner archive does not follow directory links: " + directory);
        foreach (string file in Directory.GetFiles(directory))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Runner archive does not follow file links: " + file);
            files.Add(file);
        }
        foreach (string child in Directory.GetDirectories(directory)) Collect(child, files);
    }

    private static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
    }
}
#endif
