// Copyright (c) 2026 SunSinD
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

internal static class HistoryImportChecks
{
    private const string DatabaseSchema = @"CREATE TABLE History (
Id INTEGER PRIMARY KEY AUTOINCREMENT, FileName TEXT, FilePath TEXT, DateTime TEXT,
Type TEXT, Host TEXT, URL TEXT, ThumbnailURL TEXT, DeletionURL TEXT, ShortenedURL TEXT, Tags TEXT);";
    private static readonly string[] Fields = { "FileName", "FilePath", "DateTime", "Type", "Host", "URL", "ThumbnailURL", "DeletionURL", "ShortenedURL", "Tags" };

    public static void Run(string root, Action<bool, string> check, Action<Action, string> reject)
    {
        string area = Path.Combine(root, "HistoryImport");
        var merge = Profile(area, "merge");
        JObject first = Record();
        JObject equivalent = (JObject)first.DeepClone();
        equivalent["DateTime"] = "2025-03-01T12:00:00+00:00";
        equivalent["Tags"] = "{\"Tag\":\"kept\",\"Favorite\":null,\"WindowTitle\":\"window\"}";
        JObject later = (JObject)first.DeepClone(); later["DateTime"] = "2025-03-01T12:01:00.0000000Z";
        CreateDatabase(Path.Combine(merge.Source, "History.db"), new[] { first, equivalent, later }, 900);
        Directory.CreateDirectory(merge.Destination);
        JObject existing = Record(); existing["FileName"] = "existing.png"; existing["FilePath"] = "relative-existing.png";
        CreateDatabase(Path.Combine(merge.Destination, "History.db"), new[] { existing }, 100);
        File.WriteAllText(Path.Combine(merge.Destination, "ApplicationConfig.json"), "original-app");
        var originalSource = Snapshot(merge.Source);
        var originalDestination = Snapshot(merge.Destination);
        var settingsOnly = ImportService.Prepare(merge.Source, merge.Destination);
        check(!settingsOnly.HistoryRequested && !settingsOnly.HasHistory && settingsOnly.HistoryCount == 0 && !settingsOnly.ReusesCaptureFolder,
            "two-argument Prepare remains settings-only");
        var plan = ImportService.PrepareMigration(merge.Source, merge.Destination, true, false);
        check(plan.HistoryRequested && plan.HasHistory && plan.HistoryCount == 3 && plan.Files.Count == 3,
            "history preview reports valid incoming rows independently of settings files");
        check(Unchanged(merge.Source, originalSource) && Unchanged(merge.Destination, originalDestination), "history preview preserves source bytes, entries and destination");
        using (SqliteConnection connection = Connect(Path.Combine(merge.Destination, "History.db"))) Insert(connection, Record("added-after-preview.png"), 101);
        string backup = ImportService.Apply(plan);
        var rows = ReadDatabase(Path.Combine(merge.Destination, "History.db"));
        check(rows.Count == 4 && rows.Any(r => (long)r["Id"] == 100 && (string)r["FilePath"] == "relative-existing.png"),
            "history merge preserves existing IDs and relative destination paths");
        check(rows.Any(r => (long)r["Id"] == 101 && (string)r["FileName"] == "added-after-preview.png"), "destination history is snapshotted at apply time after preview");
        check(rows.Where(r => (long)r["Id"] > 101).All(r => (long)r["Id"] < 900), "incoming history gets new destination IDs");
        check(rows.Count(r => (string)r["FilePath"] == (string)first["FilePath"]) == 2 && rows.Count(r => ((string)r["Tags"]).Contains("Favorite")) == 4,
            "history retains missing absolute files, favorites and distinct upload times");
        check(rows.Where(r => (long)r["Id"] > 101).All(r => Fields.Where(f => f.EndsWith("URL", StringComparison.Ordinal)).All(f => (string)r[f] == (string)first[f])),
            "all history URL fields survive merge");
        check(File.Exists(Path.Combine(backup, "History.db.original")) && !ImportService.HasPendingImport(merge.Destination), "history uses fixed backup marker and finishes transaction");
        ImportService.Apply(plan);
        var repeated = ReadDatabase(Path.Combine(merge.Destination, "History.db"));
        check(JToken.DeepEquals(new JArray(rows), new JArray(repeated)), "repeat history import is idempotent including tags and normalized dates");
        check(Unchanged(merge.Source, originalSource), "history apply leaves every source byte and entry unchanged");

        var frozen = Profile(area, "frozen-preview");
        CreateDatabase(Path.Combine(frozen.Source, "History.db"), new[] { Record() });
        var frozenPlan = ImportService.PrepareMigration(frozen.Source, frozen.Destination, true, false);
        using (SqliteConnection connection = Connect(Path.Combine(frozen.Source, "History.db"))) Insert(connection, Record("added-after-source-preview.png"), 2);
        ImportService.Apply(frozenPlan);
        check(ReadDatabase(Path.Combine(frozen.Destination, "History.db")).Count == 1, "incoming history payload stays frozen after preview");

        var allFields = Profile(area, "all-fields");
        List<JObject> events = new List<JObject> { Record() };
        foreach (string field in Fields)
        {
            JObject item = Record();
            item[field] = field == "DateTime" ? "2025-03-02T12:00:00.0000000Z" :
                field == "Tags" ? "{\"Favorite\":null,\"Tag\":\"different\"}" :
                field == "FilePath" ? @"C:\synthetic\different.png" :
                field.EndsWith("URL", StringComparison.Ordinal) ? "https://example.invalid/different" : "different";
            events.Add(item);
        }
        CreateDatabase(Path.Combine(allFields.Source, "History.db"), events);
        ImportService.Apply(ImportService.PrepareMigration(allFields.Source, allFields.Destination, true, false));
        check(ReadDatabase(Path.Combine(allFields.Destination, "History.db")).Count == 11, "dedup compares every meaningful history field");

        var invalid = Profile(area, "invalid-records");
        List<JObject> invalidRows = new List<JObject>();
        foreach (string path in new[] { "relative.png", @"\\server\share\image.png", @"\\?\C:\image.png", @"C:\NUL" })
        { JObject item = Record(); item["FilePath"] = path; invalidRows.Add(item); }
        JObject badDate = Record(); badDate["DateTime"] = "not-a-date"; invalidRows.Add(badDate);
        JObject badTags = Record(); badTags["Tags"] = "{\"$type\":\"arbitrary\"}"; invalidRows.Add(badTags);
        JObject invalidUrl = Record(); invalidUrl["URL"] = "file:///C:/private"; invalidRows.Add(invalidUrl);
        CreateDatabase(Path.Combine(invalid.Source, "History.db"), invalidRows);
        var invalidPlan = ImportService.PrepareMigration(invalid.Source, invalid.Destination, true, false);
        check(invalidPlan.HistoryCount == 5 && invalidPlan.Warnings.Any(w => w.Contains("skipped 2")) && invalidPlan.Warnings.Any(w => w.Contains("4 records")),
            "invalid dates and tag dictionaries are skipped and unsupported path counts reported");
        check(!invalidPlan.Warnings.Any(w => w.Contains("private") || w.Contains("server") || w.Contains("arbitrary") || w.Contains("not-a-date")),
            "history warnings do not expose stored paths or row contents");
        ImportService.Apply(invalidPlan);
        var sanitizedRows = ReadDatabase(Path.Combine(invalid.Destination, "History.db"));
        check(sanitizedRows.Any(r => (string)r["FilePath"] == "" && (string)r["URL"] == (string)first["URL"]) &&
            sanitizedRows.Any(r => (string)r["URL"] == "" && (string)r["ThumbnailURL"] == (string)first["ThumbnailURL"]),
            "unsupported paths and URLs clear individually while valid URLs survive");

        var missing = Profile(area, "missing");
        var missingPlan = ImportService.PrepareMigration(missing.Source, missing.Destination, true, false);
        check(missingPlan.HistoryRequested && !missingPlan.HasHistory && missingPlan.HistoryCount == 0 && !Directory.Exists(missing.Destination),
            "missing history is explicit and preview creates no destination");
        File.WriteAllText(Path.Combine(missing.Source, "History.json"), LegacyRecord().ToString(Formatting.None) + ",\n" + LegacyRecord().ToString(Formatting.None));
        var legacyPlan = ImportService.PrepareMigration(missing.Source, missing.Destination, true, false);
        ImportService.Apply(legacyPlan);
        check(legacyPlan.HasHistory && legacyPlan.HistoryCount == 2 && ReadDatabase(Path.Combine(missing.Destination, "History.db")).Count == 1,
            "legacy comma-separated JSON history imports and deduplicates without type handling");
        File.WriteAllText(Path.Combine(missing.Source, "History.db"), "corrupt database");
        reject(() => ImportService.PrepareMigration(missing.Source, missing.Destination, true, false), "corrupt database refuses import rather than falling back to valid legacy JSON");
        File.Delete(Path.Combine(missing.Source, "History.db"));
        File.WriteAllText(Path.Combine(missing.Source, "History.json"), "{\"$type\":\"unsafe\",\"DateTime\":\"2025-03-01\"}");
        reject(() => ImportService.PrepareMigration(missing.Source, missing.Destination, true, false), "legacy JSON metadata rejected");
        File.WriteAllText(Path.Combine(missing.Source, "History.json"), "{}{}");
        reject(() => ImportService.PrepareMigration(missing.Source, missing.Destination, true, false), "malformed legacy JSON rejected");
        var schema = Profile(area, "schema");
        using (SqliteConnection connection = Connect(Path.Combine(schema.Source, "History.db"))) Execute(connection, "CREATE TABLE History(Id INTEGER PRIMARY KEY, FileName TEXT);");
        reject(() => ImportService.PrepareMigration(schema.Source, schema.Destination, true, false), "incomplete SQLite schema rejected");
        using (var locked = new FileStream(Path.Combine(merge.Source, "History.db"), FileMode.Open, FileAccess.Read, FileShare.None))
            ExpectSourceError(() => ImportService.PrepareMigration(merge.Source, merge.Destination, true, false), check, "locked source database fails with constant friendly InvalidDataException");
        using (var writer = new FileStream(Path.Combine(merge.Source, "History.db"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            ExpectSourceError(() => ImportService.PrepareMigration(merge.Source, merge.Destination, true, false), check, "source snapshot refuses an existing shared write handle");
        File.WriteAllText(Path.Combine(merge.Source, "History.db-journal"), "synthetic journal");
        ExpectSourceError(() => ImportService.PrepareMigration(merge.Source, merge.Destination, true, false), check, "source rollback journal refuses snapshot");
        File.Delete(Path.Combine(merge.Source, "History.db-journal"));
        var oversized = Profile(area, "oversized");
        using (var large = new FileStream(Path.Combine(oversized.Source, "History.db"), FileMode.CreateNew)) large.SetLength(128L * 1024 * 1024 + 1);
        reject(() => ImportService.PrepareMigration(oversized.Source, oversized.Destination, true, false), "oversized SQLite input rejected before loading");
        var fieldBound = Profile(area, "field-bound");
        JObject oversizedField = Record(); oversizedField["Host"] = new string('x', 65537);
        CreateDatabase(Path.Combine(fieldBound.Source, "History.db"), new[] { oversizedField });
        reject(() => ImportService.PrepareMigration(fieldBound.Source, fieldBound.Destination, true, false), "oversized history field rejected before materializing the record");

        CheckWal(area, check, reject);
        CheckRecovery(area, check, reject);
        CheckCaptureFolders(area, check, reject);
    }

    private static void CheckWal(string area, Action<bool, string> check, Action<Action, string> reject)
    {
        var wal = Profile(area, "wal");
        string maker = Path.Combine(area, "wal-maker.db");
        using (SqliteConnection connection = Connect(maker))
        {
            Execute(connection, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0;" + DatabaseSchema);
            Insert(connection, Record(), 800);
            File.Copy(maker, Path.Combine(wal.Source, "History.db"));
            File.Copy(maker + "-wal", Path.Combine(wal.Source, "History.db-wal"));
        }
        var before = Snapshot(wal.Source);
        var plan = ImportService.PrepareMigration(wal.Source, wal.Destination, true, false);
        check(plan.HistoryCount == 1 && Unchanged(wal.Source, before) && !File.Exists(Path.Combine(wal.Source, "History.db-shm")),
            "owned WAL snapshot includes uncheckpointed records without creating source SHM or changing directory entries");
        ImportService.Apply(plan);
        check(ReadDatabase(Path.Combine(wal.Destination, "History.db")).Count == 1 && Unchanged(wal.Source, before), "WAL history applies while source remains byte-identical");
        using (var locked = new FileStream(Path.Combine(wal.Source, "History.db-wal"), FileMode.Open, FileAccess.Read, FileShare.None))
            ExpectSourceError(() => ImportService.PrepareMigration(wal.Source, wal.Destination, true, false), check, "locked WAL never silently omitted");
        using (var corrupt = new FileStream(Path.Combine(wal.Source, "History.db-wal"), FileMode.Append)) corrupt.WriteByte(0);
        ExpectSourceError(() => ImportService.PrepareMigration(wal.Source, wal.Destination, true, false), check, "incomplete WAL rejected rather than silently ignored by SQLite");
    }

    private static void CheckRecovery(string area, Action<bool, string> check, Action<Action, string> reject)
    {
        var pair = Profile(area, "rollback");
        CreateDatabase(Path.Combine(pair.Source, "History.db"), new[] { Record() });
        Directory.CreateDirectory(pair.Destination);
        CreateDatabase(Path.Combine(pair.Destination, "History.db"), new[] { Record("prior.png") }, 44);
        foreach (string name in new[] { "ApplicationConfig.json", "HotkeysConfig.json", "UploadersConfig.json" }) File.WriteAllText(Path.Combine(pair.Destination, name), "prior-" + name);
        var before = Snapshot(pair.Destination);
        var plan = ImportService.PrepareMigration(pair.Source, pair.Destination, true, false);
        using (var locked = new FileStream(Path.Combine(pair.Destination, "HotkeysConfig.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            reject(() => ImportService.Apply(plan), "replacement failure enters recoverable settings and history rollback");
        ImportService.RecoverPendingImport(pair.Destination);
        check(before.All(p => File.ReadAllBytes(Path.Combine(pair.Destination, p.Key)).SequenceEqual(p.Value)) && !ImportService.HasPendingImport(pair.Destination),
            "failed multi-file import restores settings and history together");
        foreach (string suffix in new[] { "-wal", "-shm", "-journal" })
        {
            string sidecar = Path.Combine(pair.Destination, "History.db" + suffix);
            File.WriteAllText(sidecar, "sidecar");
            reject(() => ImportService.Apply(plan), "destination " + suffix + " blocks import without sidecar deletion");
            check(File.ReadAllText(sidecar) == "sidecar" && before.All(p => File.ReadAllBytes(Path.Combine(pair.Destination, p.Key)).SequenceEqual(p.Value)), "sidecar refusal preserves live profile");
            File.Delete(sidecar);
        }
        string backup = ImportService.Apply(plan);
        string historyBackup = Path.Combine(backup, "History.db.original");
        byte[] validBackup = File.ReadAllBytes(historyBackup);
        var liveBeforeRecovery = new[] { "ApplicationConfig.json", "HotkeysConfig.json", "UploadersConfig.json", "History.db" }
            .ToDictionary(name => name, name => File.ReadAllBytes(Path.Combine(pair.Destination, name)));
        File.WriteAllText(historyBackup, "truncated database");
        WriteJournal(pair.Destination, backup);
        byte[] pendingJournal = File.ReadAllBytes(Path.Combine(pair.Destination, ".SunShot-import-pending.json"));
        reject(() => ImportService.RecoverPendingImport(pair.Destination), "corrupt history backup rejects recovery before restoring any files");
        check(liveBeforeRecovery.All(p => File.ReadAllBytes(Path.Combine(pair.Destination, p.Key)).SequenceEqual(p.Value)) &&
            File.ReadAllBytes(Path.Combine(pair.Destination, ".SunShot-import-pending.json")).SequenceEqual(pendingJournal),
            "corrupt backup refusal preserves all live files and pending journal");
        File.WriteAllBytes(historyBackup, validBackup);
        File.WriteAllText(Path.Combine(pair.Destination, "ApplicationConfig.json"), "partial");
        WriteJournal(pair.Destination, backup);
        File.WriteAllText(Path.Combine(pair.Destination, "History.db-wal"), "stale WAL");
        reject(() => ImportService.RecoverPendingImport(pair.Destination), "history recovery refuses stale destination WAL before restoring any settings");
        check(File.ReadAllText(Path.Combine(pair.Destination, "ApplicationConfig.json")) == "partial" && ImportService.HasPendingImport(pair.Destination), "blocked recovery retains journal for safe retry");
        File.Delete(Path.Combine(pair.Destination, "History.db-wal"));
        ImportService.RecoverPendingImport(pair.Destination);
        check(before.All(p => File.ReadAllBytes(Path.Combine(pair.Destination, p.Key)).SequenceEqual(p.Value)), "history marker restores original database alongside original settings");

        var absent = Profile(area, "absent-recovery");
        CreateDatabase(Path.Combine(absent.Source, "History.db"), new[] { Record() });
        var absentPlan = ImportService.PrepareMigration(absent.Source, absent.Destination, true, false);
        string absentBackup = ImportService.Apply(absentPlan);
        check(File.Exists(Path.Combine(absentBackup, "History.db.absent")), "previously absent history receives absent marker");
        WriteJournal(absent.Destination, absentBackup);
        ImportService.RecoverPendingImport(absent.Destination);
        check(!File.Exists(Path.Combine(absent.Destination, "History.db")) && absentPlan.Files.Keys.All(n => !File.Exists(Path.Combine(absent.Destination, n))),
            "history and settings absence restored after simulated interruption");
        ImportService.Apply(absentPlan);
        string legacyBackup = Path.Combine(absent.Destination, "Backup", "ShareX-import-legacy-settings-only");
        Directory.CreateDirectory(legacyBackup);
        foreach (string name in absentPlan.Files.Keys) File.Copy(Path.Combine(absent.Destination, name), Path.Combine(legacyBackup, name + ".original"));
        byte[] originalHistory = File.ReadAllBytes(Path.Combine(absent.Destination, "History.db"));
        WriteJournal(absent.Destination, legacyBackup);
        ImportService.RecoverPendingImport(absent.Destination);
        check(File.ReadAllBytes(Path.Combine(absent.Destination, "History.db")).SequenceEqual(originalHistory), "old string journal without history markers remains settings-only");
        File.WriteAllText(Path.Combine(legacyBackup, "History.db.original"), "unsafe duplicate marker");
        File.WriteAllText(Path.Combine(legacyBackup, "History.db.absent"), "");
        WriteJournal(absent.Destination, legacyBackup);
        reject(() => ImportService.RecoverPendingImport(absent.Destination), "conflicting history markers reject recovery");
    }

    private static void CheckCaptureFolders(string area, Action<bool, string> check, Action<Action, string> reject)
    {
        var capture = Profile(area, "capture");
        string folder = Path.Combine(capture.Source, "Screenshots");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "existing.png"), new byte[] { 1, 2, 3 });
        JObject config = JObject.Parse(File.ReadAllText(Path.Combine(capture.Source, "ApplicationConfig.json")));
        config["DefaultTaskSettings"] = new JObject { ["OverrideScreenshotsFolder"] = true, ["ScreenshotsFolder"] = "unsafe", ["ImageSettings"] = new JObject { ["FileExistAction"] = "Overwrite" } };
        WriteConfig(capture.Source, config);
        JObject hotkeys = new JObject { ["ApplicationVersion"] = "22.4.0", ["Hotkeys"] = new JArray(new JObject {
            ["HotkeyInfo"] = new JObject { ["Hotkey"] = "PrintScreen" }, ["TaskSettings"] = new JObject { ["ImageSettings"] = new JObject { ["FileExistAction"] = "Overwrite" }, ["OverrideScreenshotsFolder"] = true, ["ScreenshotsFolder"] = "unsafe" }
        }) };
        File.WriteAllText(Path.Combine(capture.Source, "HotkeysConfig.json"), hotkeys.ToString());
        var before = Snapshot(capture.Source);
        var defaultPlan = ImportService.PrepareMigration(capture.Source, capture.Destination, false, false);
        check(!defaultPlan.ReusesCaptureFolder && defaultPlan.CaptureFolder == null, "capture reuse remains explicitly off by default");
        var reuse = ImportService.PrepareMigration(capture.Source, capture.Destination, false, true);
        JObject app = App(reuse);
        JObject keys = JObject.Parse(Encoding.UTF8.GetString(reuse.Files["HotkeysConfig.json"]));
        check(reuse.ReusesCaptureFolder && reuse.CaptureFolder == folder && (bool)app["UseCustomScreenshotsPath"] && (string)app["CustomScreenshotsPath"] == folder && (string)app["CustomScreenshotsPath2"] == "",
            "capture preview approves literal default source folder and clears fallback");
        check((string)app["DefaultTaskSettings"]["ImageSettings"]["FileExistAction"] == "UniqueName" && (string)keys["Hotkeys"][0]["TaskSettings"]["ImageSettings"]["FileExistAction"] == "UniqueName" &&
            !(bool)app["DefaultTaskSettings"]["OverrideScreenshotsFolder"] && !(bool)keys["Hotkeys"][0]["TaskSettings"]["OverrideScreenshotsFolder"] && (string)app["SaveImageSubFolderPattern"] == "%y-%mo",
            "reuse forces unique filenames and safe subfolders and clears every task override");
        ImportService.Apply(reuse);
        check(Unchanged(capture.Source, before), "capture reuse neither moves nor changes existing images or source settings");
        string environmentName = "SUNSHOT_SYNTHETIC_CAPTURE_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(environmentName, folder);
        try
        {
            config["UseCustomScreenshotsPath"] = true;
            config["CustomScreenshotsPath"] = "%" + environmentName + "%";
            config["CustomScreenshotsPath2"] = "";
            WriteConfig(capture.Source, config);
            check(ImportService.PrepareMigration(capture.Source, capture.Destination, false, true).CaptureFolder == folder, "capture folder environment variable expands to approved literal");
        }
        finally { Environment.SetEnvironmentVariable(environmentName, null); }
        config["CustomScreenshotsPath"] = Path.Combine(capture.Source, "missing-primary"); config["CustomScreenshotsPath2"] = folder;
        WriteConfig(capture.Source, config);
        check(ImportService.PrepareMigration(capture.Source, capture.Destination, false, true).CaptureFolder == folder, "effective capture folder honors existing fallback");
        config["CustomScreenshotsPath"] = folder; config["CustomScreenshotsPath2"] = @"\\server\unused-fallback";
        WriteConfig(capture.Source, config);
        check(ImportService.PrepareMigration(capture.Source, capture.Destination, false, true).CaptureFolder == folder, "effective primary capture folder is reused without following unused fallback");
        config["CustomScreenshotsPath"] = "Screenshots"; config["CustomScreenshotsPath2"] = "";
        WriteConfig(capture.Source, config);
        check(ImportService.PrepareMigration(capture.Source, capture.Destination, false, true).CaptureFolder == folder, "relative capture setting resolves beneath selected source profile");
        foreach (string path in new[] { @"\\server\share\captures", @"\\?\C:\captures", "%unsupported-template%", Path.Combine(capture.Source, "missing"), capture.Destination, area })
        {
            config["CustomScreenshotsPath"] = path;
            WriteConfig(capture.Source, config);
            var skipped = ImportService.PrepareMigration(capture.Source, capture.Destination, false, true);
            check(!skipped.ReusesCaptureFolder && !(bool)App(skipped)["UseCustomScreenshotsPath"] && skipped.Warnings.Any(w => w.Contains("reuse was skipped")), "unsafe, missing or overlapping capture folder rejected");
        }
        string junction = Path.Combine(capture.Source, "capture-link");
        var command = new System.Diagnostics.ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        command.ArgumentList.Add("-NoProfile"); command.ArgumentList.Add("-NonInteractive"); command.ArgumentList.Add("-Command");
        command.ArgumentList.Add("New-Item -ItemType Junction -Path '" + junction.Replace("'", "''") + "' -Target '" + folder.Replace("'", "''") + "' | Out-Null");
        using (var process = System.Diagnostics.Process.Start(command))
        { process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd(); process.WaitForExit(); check(process.ExitCode == 0, "synthetic capture junction created"); }
        config["CustomScreenshotsPath"] = junction; WriteConfig(capture.Source, config);
        check(!ImportService.PrepareMigration(capture.Source, capture.Destination, false, true).ReusesCaptureFolder, "linked capture folder rejected without following it");
        reject(() => ImportService.PrepareMigration(junction, capture.Destination, true, true), "source profile aliases require selecting real profile");
        ImportService.RecoverPendingImport(junction);
        check(!ImportService.HasPendingImport(junction), "startup without a pending import allows a profile reached through a junction");
        config["CustomScreenshotsPath"] = folder; WriteConfig(capture.Source, config);
        var revalidate = ImportService.PrepareMigration(capture.Source, capture.Destination, false, true);
        Directory.Move(folder, folder + "-temporarily-unavailable");
        reject(() => ImportService.Apply(revalidate), "approved capture folder is revalidated before import writes");
        Directory.Move(folder + "-temporarily-unavailable", folder);
    }

    private static (string Source, string Destination) Profile(string area, string name)
    {
        string source = Path.Combine(area, name, "ShareX");
        string destination = Path.Combine(area, name, "SunShot");
        Directory.CreateDirectory(source);
        WriteConfig(source, new JObject { ["ApplicationVersion"] = "22.4.0", ["DefaultTaskSettings"] = new JObject() });
        return (source, destination);
    }

    private static void WriteConfig(string source, JObject config) => File.WriteAllText(Path.Combine(source, "ApplicationConfig.json"), config.ToString());
    private static JObject App(ImportService.Plan plan) => JObject.Parse(Encoding.UTF8.GetString(plan.Files["ApplicationConfig.json"]));
    private static Dictionary<string, byte[]> Snapshot(string folder) => Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(folder, path), File.ReadAllBytes);
    private static bool Unchanged(string folder, Dictionary<string, byte[]> snapshot)
    {
        var current = Snapshot(folder);
        return current.Keys.OrderBy(k => k).SequenceEqual(snapshot.Keys.OrderBy(k => k)) && snapshot.All(p => current[p.Key].SequenceEqual(p.Value));
    }
    private static JObject Record(string name = "capture.png") => new JObject
    {
        ["FileName"] = name, ["FilePath"] = @"C:\synthetic-missing\" + name,
        ["DateTime"] = "2025-03-01T12:00:00.0000000Z", ["Type"] = "Image", ["Host"] = "synthetic",
        ["URL"] = "https://example.invalid/image", ["ThumbnailURL"] = "https://example.invalid/thumb",
        ["DeletionURL"] = "https://example.invalid/delete", ["ShortenedURL"] = "https://example.invalid/short",
        ["Tags"] = "{\"WindowTitle\":\"window\",\"Favorite\":null,\"Tag\":\"kept\"}"
    };
    private static JObject LegacyRecord()
    {
        JObject record = Record(); record["Tags"] = JObject.Parse((string)record["Tags"]); return record;
    }
    private static SqliteConnection Connect(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open(); return connection;
    }
    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
    private static void CreateDatabase(string path, IEnumerable<JObject> records, long start = 1)
    {
        using SqliteConnection connection = Connect(path);
        Execute(connection, DatabaseSchema);
        foreach (JObject record in records) Insert(connection, record, start++);
    }
    private static void Insert(SqliteConnection connection, JObject record, long id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO History(Id," + string.Join(",", Fields.Select(f => "\"" + f + "\"")) + ") VALUES(@id," + string.Join(",", Fields.Select((f, i) => "@p" + i)) + ");";
        command.Parameters.AddWithValue("@id", id);
        for (int i = 0; i < Fields.Length; i++) command.Parameters.AddWithValue("@p" + i, (string)record[Fields[i]] ?? (object)DBNull.Value);
        command.ExecuteNonQuery();
    }
    private static List<JObject> ReadDatabase(string path)
    {
        using SqliteConnection connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT * FROM History ORDER BY Id;";
        using SqliteDataReader reader = command.ExecuteReader();
        List<JObject> rows = new List<JObject>();
        while (reader.Read())
        {
            JObject row = new JObject { ["Id"] = reader.GetInt64(0) };
            for (int i = 0; i < Fields.Length; i++) row[Fields[i]] = reader.IsDBNull(i + 1) ? "" : reader.GetString(i + 1);
            rows.Add(row);
        }
        return rows;
    }
    private static void WriteJournal(string destination, string backup) => File.WriteAllText(Path.Combine(destination, ".SunShot-import-pending.json"), JsonConvert.SerializeObject(Path.GetFileName(backup)));
    private static void ExpectSourceError(Action action, Action<bool, string> check, string label)
    {
        Exception caught = null; try { action(); } catch (Exception error) { caught = error; }
        check(caught is InvalidDataException && caught.Message.StartsWith("ShareX history could not be safely read.", StringComparison.Ordinal) && caught.InnerException == null, label);
    }
}
