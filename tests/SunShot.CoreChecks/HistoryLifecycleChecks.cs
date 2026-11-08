using ShareX;
using ShareX.HistoryLib;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class HistoryLifecycleChecks
{
    internal static void RunUi(Action<bool, string> check)
    {
        const BindingFlags programFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        Type program = typeof(TaskSettings).Assembly.GetType("ShareX.Program");
        PropertyInfo historyProperty = program.GetProperty("HistoryManager", programFlags);
        object originalHistory = historyProperty.GetValue(null);
        try
        {
            historyProperty.SetValue(null, null);
            int forms = Application.OpenForms.Count;
            Type helpers = typeof(TaskSettings).Assembly.GetType("ShareX.TaskHelpers");
            helpers.GetMethod("OpenHistory", programFlags).Invoke(null, null);
            helpers.GetMethod("OpenImageHistory", programFlags).Invoke(null, null);
            check(Application.OpenForms.Count == forms, "history views stay closed while the database is unavailable during import");
        }
        finally { historyProperty.SetValue(null, originalHistory); }
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            foreach (string language in new[] { "en-CA", "fr-CA" })
            foreach (int width in new[] { 660, 580 })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                using var form = new ShareXSettingsImportForm { ClientSize = new Size(width, 560) };
                void Layout(Control control)
                {
                    _ = control.Handle;
                    control.PerformLayout();
                    foreach (Control child in control.Controls) Layout(child);
                }
                Layout(form);
                IEnumerable<Control> Descendants(Control control)
                {
                    foreach (Control child in control.Controls)
                    {
                        yield return child;
                        foreach (Control nested in Descendants(child)) yield return nested;
                    }
                }
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var history = (CheckBox)typeof(ShareXSettingsImportForm).GetField("includeHistory", flags).GetValue(form);
                var reuse = (CheckBox)typeof(ShareXSettingsImportForm).GetField("reuseCaptureFolder", flags).GetValue(form);
                var import = (Button)typeof(ShareXSettingsImportForm).GetField("importButton", flags).GetValue(form);
                check(history.Checked && !reuse.Checked, "history is offered and capture-folder writes require opt-in: " + language);
                check(new Control[] { history, reuse, import }.All(control =>
                    form.ClientRectangle.Contains(form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle)))),
                    "history migration choices and import button fit: " + language + " " + width);
                check(Descendants(form).OfType<Label>().All(label =>
                    label.Height >= label.GetPreferredSize(new Size(label.Width, 0)).Height &&
                    form.ClientRectangle.Contains(form.RectangleToClient(label.RectangleToScreen(label.ClientRectangle)))),
                    "history migration explanation fits: " + language + " " + width);
            }
        }
        finally { CultureInfo.CurrentUICulture = original; }
        CheckImportedViews(check);
    }

    private static void CheckImportedViews(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "sunshot-history-views-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "ShareX"), destination = Path.Combine(root, "SunShot");
        Directory.CreateDirectory(source);
        try
        {
            string image = Path.Combine(source, "old-capture.png");
            using (var bitmap = new Bitmap(24, 24))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.DarkGray);
                bitmap.Save(image, System.Drawing.Imaging.ImageFormat.Png);
            }
            byte[] original = File.ReadAllBytes(image);
            File.WriteAllText(Path.Combine(source, "ApplicationConfig.json"), "{\"ApplicationVersion\":\"22.4.0\",\"DefaultTaskSettings\":{}}");
            using (var history = new HistoryManagerSQLite(Path.Combine(source, "History.db")))
                history.AppendHistoryItem(new HistoryItem { FileName = "old-capture.png", FilePath = image, Type = "Image", DateTime = new DateTime(2020, 1, 1) });
            ImportService.Apply(ImportService.PrepareMigration(source, destination, true, false));
            using var imported = new HistoryManagerSQLite(Path.Combine(destination, "History.db"));
            using var list = new HistoryForm(imported, new HistorySettings());
            using var images = new ImageHistoryForm(imported, new ImageHistorySettings());
            foreach (Form view in new Form[] { list, images })
            {
                _ = view.Handle;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var refresh = (System.Threading.Tasks.Task)view.GetType().GetMethod("RefreshHistoryItems", flags).Invoke(view, new object[] { true });
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (!refresh.IsCompleted && timer.ElapsedMilliseconds < 10000)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(10);
                }
                check(refresh.IsCompletedSuccessfully, view.GetType().Name + " loads imported history through its production refresh path");
                var loaded = (List<HistoryItem>)view.GetType().GetField("allHistoryItems", flags).GetValue(view);
                check(loaded.Count == 1 && loaded[0].FilePath == image, view.GetType().Name + " retains the original capture path");
            }
            using (var reopened = Image.FromFile(imported.GetHistoryItems()[0].FilePath))
                check(reopened.Width == 24 && reopened.Height == 24 && File.ReadAllBytes(image).SequenceEqual(original),
                    "old imported capture opens from its original folder with unchanged bytes");
        }
        finally { Directory.Delete(root, true); }
    }

    internal static void Run(string root, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        Type program = typeof(TaskSettings).Assembly.GetType("ShareX.Program");
        Type settings = typeof(TaskSettings).Assembly.GetType("ShareX.SettingManager");
        PropertyInfo historyProperty = program.GetProperty("HistoryManager", flags);
        PropertyInfo settingsProperty = program.GetProperty("Settings", flags);
        PropertyInfo pathProperty = program.GetProperty("CustomPersonalPath", flags);
        object previousHistory = historyProperty.GetValue(null);
        object previousSettings = settingsProperty.GetValue(null);
        object previousPath = pathProperty.GetValue(null);
        MethodInfo append = typeof(TaskManager).GetMethod("AppendHistoryItemAsync", flags);
        MethodInfo suspend = typeof(TaskManager).GetMethod("SuspendHistoryAppendsForImport", flags);
        MethodInfo resume = typeof(TaskManager).GetMethod("ResumeHistoryAppendsAfterFailedImport", flags);
        string directory = Path.Combine(root, "history-lifecycle");
        Directory.CreateDirectory(directory);
        try
        {
            pathProperty.SetValue(null, directory);
            var config = new ApplicationConfig();
            settingsProperty.SetValue(null, config);
            MethodInfo validate = settings.GetMethod("ValidateImportDestination", flags);
            foreach (bool hotkeys in new[] { true, false })
            {
                config.CustomHotkeysConfigPath = hotkeys ? directory : "";
                config.CustomUploadersConfigPath = hotkeys ? "" : directory;
                bool rejected = false;
                try { validate.Invoke(null, null); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidDataException) { rejected = true; }
                check(rejected, "external " + (hotkeys ? "hotkey" : "uploader") + " config is rejected before import saves");
            }
            config.CustomHotkeysConfigPath = config.CustomUploadersConfigPath = "";
            bool accepted = false;
            try { validate.Invoke(null, null); accepted = true; }
            catch (TargetInvocationException) { }
            check(accepted, "independent regular profile passes the pre-save guard");

            string source = Path.Combine(root, "history-presave-source");
            Directory.CreateDirectory(source);
            string original = Path.Combine(source, "ApplicationConfig.json");
            File.WriteAllText(original, "unchanged-source");
            string temporary = Path.Combine(directory, "ApplicationConfig.json.temp");
            void Link(string kind, string path, string target)
            {
                var start = new System.Diagnostics.ProcessStartInfo("powershell.exe")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-Command");
                start.ArgumentList.Add("New-Item -ItemType " + kind + " -Path '" + path.Replace("'", "''") + "' -Target '" + target.Replace("'", "''") + "' | Out-Null");
                using var process = System.Diagnostics.Process.Start(start);
                process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd(); process.WaitForExit();
                check(process.ExitCode == 0, "synthetic pre-save " + kind + " created");
            }
            bool Rejected()
            {
                try { validate.Invoke(null, null); return false; }
                catch (TargetInvocationException error) when (error.InnerException is InvalidDataException) { return true; }
            }
            Link("HardLink", temporary, original);
            try { check(Rejected() && File.ReadAllText(original) == "unchanged-source", "pre-save guard rejects a temp hardlink without touching its source"); }
            finally { File.Delete(temporary); }
            string backupFolder = Path.Combine(directory, "Backup");
            Link("Junction", backupFolder, source);
            try { check(Rejected() && File.ReadAllText(original) == "unchanged-source", "pre-save guard rejects a backup junction without touching source settings"); }
            finally { Directory.Delete(backupFolder); }

            using var history = new HistoryManagerSQLite(Path.Combine(directory, "History.db"));
            historyProperty.SetValue(null, history);
            DateTime timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 32; i++)
                append.Invoke(null, new object[] { new HistoryItem {
                    FileName = $"{i}.png", FilePath = Path.Combine(directory, $"{i}.png"),
                    DateTime = timestamp.AddDays(32 - i), Type = "Image"
                } });
            suspend.Invoke(null, null);
            check(history.GetHistoryItems().Count == 32, "import suspension drains all pending production history appends");
            check(history.AppendHistoryItems(new[] { 3, 2, 1 }.Select(day => new HistoryItem {
                FileName = $"old-{day}.png", FilePath = Path.Combine(directory, $"old-{day}.png"),
                DateTime = new DateTime(2024, 1, day, 0, 0, 0, DateTimeKind.Utc), Type = "Image"
            })), "descending capture dates are inserted in deterministic order");
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = history.FilePath }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO History (FileName, FilePath, DateTime, Type) VALUES ('offset.png', @offsetPath, '2024-12-31T23:59:00-05:00', 'Image'), ('utc.png', @utcPath, '2025-01-01T04:58:00Z', 'Image');";
                command.Parameters.AddWithValue("@offsetPath", Path.Combine(directory, "offset.png"));
                command.Parameters.AddWithValue("@utcPath", Path.Combine(directory, "utc.png"));
                command.ExecuteNonQuery();
            }
            var items = history.GetHistoryItems();
            check(items.Select(x => x.DateTime).SequenceEqual(items.Select(x => x.DateTime).OrderBy(x => x)) &&
                items.FindIndex(x => x.FileName == "offset.png") > items.FindIndex(x => x.FileName == "utc.png"),
                "history loader orders old captures by date instead of insertion time");
            append.Invoke(null, new object[] { new HistoryItem {
                FileName = "blocked.png", FilePath = Path.Combine(directory, "blocked.png"),
                DateTime = timestamp, Type = "Image"
            } });
            check(history.GetHistoryItems().Count == 37, "history appends remain suspended during replacement");
            resume.Invoke(null, null);
            append.Invoke(null, new object[] { new HistoryItem {
                FileName = "resumed.png", FilePath = Path.Combine(directory, "resumed.png"),
                DateTime = timestamp, Type = "Image"
            } });
            suspend.Invoke(null, null);
            check(history.GetHistoryItems().Count == 38, "failed import can resume history appends");
        }
        finally
        {
            resume.Invoke(null, null);
            historyProperty.SetValue(null, previousHistory);
            settingsProperty.SetValue(null, previousSettings);
            pathProperty.SetValue(null, previousPath);
        }
    }
}
