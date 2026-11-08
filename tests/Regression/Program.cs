using Microsoft.Data.Sqlite;
using ShareX;
using System.Reflection;

static class Regression
{
    static int checks;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
        Console.WriteLine("PASS: " + name);
    }

    static async Task Watch(string root, bool slow, bool empty, bool preallocated = false)
    {
        string directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var watcher = new WatchFolder
        {
            Settings = new WatchFolderSettings { FolderPath = directory },
            TaskSettings = new TaskSettings()
        };
        var triggered = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        watcher.FileWatcherTrigger += path =>
        {
            Interlocked.Increment(ref count);
            triggered.TrySetResult(new FileInfo(path).Length);
        };
        watcher.Enable();
        await using (var writer = new FileStream(Path.Combine(directory, "test.bin"), FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            if (preallocated)
            {
                writer.SetLength(4096);
                await Task.Delay(6500);
            }
            for (int i = 0; i < (slow ? 32 : empty ? 0 : 1); i++)
            {
                await writer.WriteAsync(new byte[4096]);
                await writer.FlushAsync();
                if (slow) await Task.Delay(250);
            }
            Check(!triggered.Task.IsCompleted, "watcher waits until writer closes");
        }
        Task result = await Task.WhenAny(triggered.Task, Task.Delay(empty ? 7000 : 5000));
        Check((result == triggered.Task) != empty, empty ? "empty file is skipped" : "completed file is delivered");
        if (!empty) Check(await triggered.Task == (slow ? 32 : 1) * 4096 && count == 1, "full file is delivered once");
    }

    sealed class QueuedContext : SynchronizationContext
    {
        public readonly TaskCompletionSource<(SendOrPostCallback Callback, object? State)> Posted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void Post(SendOrPostCallback callback, object? state) => Posted.TrySetResult((callback, state));
    }

    static async Task LockedBatch(string root)
    {
        string directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var watcher = new WatchFolder { Settings = new WatchFolderSettings { FolderPath = directory }, TaskSettings = new TaskSettings() };
        var writers = new List<FileStream>();
        ThreadPool.GetMinThreads(out int minimumWorkers, out int minimumIo);
        Check(ThreadPool.SetMinThreads(Math.Max(minimumWorkers, 32), minimumIo), "locked-file test worker capacity configured");
        int count = 0;
        watcher.FileWatcherTrigger += _ => Interlocked.Increment(ref count);
        try
        {
            for (int i = 0; i < 16; i++)
            {
                var writer = new FileStream(Path.Combine(directory, $"locked-{i}.bin"), FileMode.Create, FileAccess.Write, FileShare.None);
                writer.SetLength(4096);
                writers.Add(writer);
            }
            watcher.Enable();
            object sender = typeof(WatchFolder).GetField("fileWatcher", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(watcher)!;
            MethodInfo created = typeof(WatchFolder).GetMethod("fileWatcher_Created", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ThreadPool.GetAvailableThreads(out int workersBefore, out _);
            for (int i = 0; i < writers.Count; i++)
                created.Invoke(watcher, [sender, new FileSystemEventArgs(WatcherChangeTypes.Created, directory, $"locked-{i}.bin")]);
            await Task.Delay(2500);
            ThreadPool.GetAvailableThreads(out int workersAfter, out _);
            Check(workersBefore - workersAfter < 8, "sixteen locked watched files do not occupy one worker each");
            Check(count == 0, "locked batch is not delivered");
            watcher.Dispose();
            foreach (FileStream writer in writers) writer.Dispose();
            await Task.Delay(1500);
            Check(count == 0, "disposed watcher drops pending locked files after they close");
        }
        finally
        {
            watcher.Dispose();
            foreach (FileStream writer in writers) writer.Dispose();
            ThreadPool.SetMinThreads(minimumWorkers, minimumIo);
        }
    }

    static async Task WatchedDirectory(string root)
    {
        string directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        string child = Path.Combine(directory, "child");
        Directory.CreateDirectory(child);
        using var watcher = new WatchFolder { Settings = new WatchFolderSettings { FolderPath = directory }, TaskSettings = new TaskSettings() };
        int count = 0;
        watcher.FileWatcherTrigger += _ => Interlocked.Increment(ref count);
        watcher.Enable();
        object sender = typeof(WatchFolder).GetField("fileWatcher", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(watcher)!;
        MethodInfo created = typeof(WatchFolder).GetMethod("fileWatcher_Created", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var errors = new QueuedContext();
        SynchronizationContext? original = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(errors);
        try { created.Invoke(watcher, [sender, new FileSystemEventArgs(WatcherChangeTypes.Created, directory, "child")]); }
        finally { SynchronizationContext.SetSynchronizationContext(original); }
        await Task.Delay(2500);
        Check(!errors.Posted.Task.IsCompleted, "watched directory does not raise an unhandled access error");
        Check(count == 0, "watched directory is not delivered as a file");
    }

    static async Task PendingCallback(string root, bool reenable)
    {
        string directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var watcher = new WatchFolder { Settings = new WatchFolderSettings { FolderPath = directory }, TaskSettings = new TaskSettings() };
        var queue = new QueuedContext();
        SynchronizationContext? original = SynchronizationContext.Current;
        int count = 0;
        watcher.FileWatcherTrigger += _ => count++;
        SynchronizationContext.SetSynchronizationContext(queue);
        try { watcher.Enable(); }
        finally { SynchronizationContext.SetSynchronizationContext(original); }
        File.WriteAllBytes(Path.Combine(directory, "test.bin"), new byte[4096]);
        Check(await Task.WhenAny(queue.Posted.Task, Task.Delay(6000)) == queue.Posted.Task, "watch callback queued");
        if (reenable) watcher.Enable(); else watcher.Dispose();
        var pending = await queue.Posted.Task;
        pending.Callback(pending.State);
        Check(count == 0, reenable ? "new watcher rejects old queued work" : "disposed watcher rejects queued work");
    }

    static void Database(string root)
    {
        string path = Path.Combine(root, "history-test.db");
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE captures (id INTEGER PRIMARY KEY, path TEXT NOT NULL); INSERT INTO captures(path) VALUES ('synthetic.png'); SELECT sqlite_version();";
            Check(!string.IsNullOrEmpty((string?)command.ExecuteScalar()), "native SQLite loads and writes a database");
        }
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT path FROM captures WHERE id = 1";
            Check((string?)command.ExecuteScalar() == "synthetic.png", "SQLite data survives reopening");
        }
    }

    static void FFmpeg(string root, string? archive)
    {
        Type installer = typeof(TaskSettings).Assembly.GetType("ShareX.SunShotFFmpeg", true)!;
        MethodInfo method = installer.GetMethod("InstallVerifiedArchive", BindingFlags.NonPublic | BindingFlags.Static)!;
        string invalid = Path.Combine(root, "invalid.zip");
        File.WriteAllText(invalid, "invalid");
        string target = Path.Combine(root, "tools", "ffmpeg.exe");
        bool rejected = false;
        try { method.Invoke(null, [invalid, target]); }
        catch (TargetInvocationException error) when (error.InnerException is InvalidDataException) { rejected = true; }
        Check(rejected && !File.Exists(target), "FFmpeg checksum rejects invalid download before installation");
        if (archive != null)
        {
            method.Invoke(null, [archive, target]);
            Check(File.Exists(target) && new FileInfo(target).Length == 202259968, "verified FFmpeg archive installs completely");
        }
    }

    static async Task Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--cleanup-child")
        {
            TempCleanupChecks.Child(args[1]);
            return;
        }
        string root = Path.Combine(Path.GetTempPath(), "sunshot-regression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            UploadConsentChecks.Run(root, Check);
            TempCleanupChecks.Run(root, Check);
            Database(root);
            FFmpeg(root, args.FirstOrDefault());
            await Watch(root, false, false);
            await Watch(root, true, false);
            await Watch(root, false, false, true);
            await Watch(root, false, true);
            await LockedBatch(root);
            await WatchedDirectory(root);
            await PendingCallback(root, false);
            await PendingCallback(root, true);
            Console.WriteLine($"{checks} assertions passed.");
        }
        finally
        {
            string temporary = Path.GetFullPath(Path.GetTempPath());
            string resolved = Path.GetFullPath(root);
            if (resolved.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("sunshot-regression-", StringComparison.Ordinal))
                Directory.Delete(resolved, true);
        }
    }
}
