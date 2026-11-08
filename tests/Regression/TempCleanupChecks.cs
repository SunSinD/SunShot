using System.Diagnostics;
using System.Reflection;
using ShareX;

static class TempCleanupChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        string synthetic = Path.Combine(root, "private-temp");
        string sharex = Path.Combine(synthetic, "ShareX", "nested", "keep.txt");
        string sunshot = Path.Combine(synthetic, "SunShot", "nested", "remove.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(sharex)!);
        Directory.CreateDirectory(Path.GetDirectoryName(sunshot)!);
        File.WriteAllText(sharex, "ShareX synthetic fixture must survive unchanged");
        File.WriteAllText(sunshot, "SunShot synthetic temporary file");
        byte[] original = File.ReadAllBytes(sharex);
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--cleanup-child");
        start.ArgumentList.Add(synthetic);
        start.Environment["TEMP"] = synthetic;
        start.Environment["TMP"] = synthetic;
        using var child = Process.Start(start)!;
        string output = child.StandardOutput.ReadToEnd(), error = child.StandardError.ReadToEnd();
        child.WaitForExit();
        check(child.ExitCode == 0, "private-temp cleanup runs only in isolated child process: " + output + error);
        check(File.ReadAllBytes(sharex).SequenceEqual(original), "cleanup leaves sibling ShareX directory and bytes untouched");
        check(!Directory.Exists(Path.Combine(synthetic, "SunShot")), "cleanup removes SunShot private temp directory recursively");
    }

    public static void Child(string synthetic)
    {
        string expected = Path.GetFullPath(synthetic);
        string actual = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(expected) != "private-temp" ||
            !(Path.GetFileName(Path.GetDirectoryName(expected))?.StartsWith("sunshot-regression-", StringComparison.Ordinal) ?? false))
            throw new InvalidOperationException("Refusing cleanup outside synthetic test TEMP.");
        typeof(CleanupManager).GetMethod("CleanupAppTempFolder", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null);
        Console.WriteLine("isolated TEMP verified: " + actual);
    }
}
