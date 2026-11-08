using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using ShareX;
using ShareX.HelpersLib;
using ShareX.UploadersLib;
using Newtonsoft.Json.Linq;

internal static class ShareX22Checks
{
    internal static void Run(string root, Action<bool, string> check, Action<Action, string> reject)
    {
        string fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        foreach (string version in new[] { "22.0.0", "22.1.0", "22.2.0", "22.3.0", "22.4.0" })
        {
            string source = Path.Combine(root, "ShareX-" + version);
            string destination = Path.Combine(root, "SunShot-" + version);
            Directory.CreateDirectory(source);
            foreach (string file in new[] { "ApplicationConfig.json", "HotkeysConfig.json", "UploadersConfig.json" }) File.Copy(Path.Combine(fixtures, version, file), Path.Combine(source, file));
            JObject schema = JObject.Parse(File.ReadAllText(Path.Combine(fixtures, version, "Schema.json")));
            JObject fixtureApp = JObject.Parse(File.ReadAllText(Path.Combine(source, "ApplicationConfig.json")));
            JObject fixtureUploaders = JObject.Parse(File.ReadAllText(Path.Combine(source, "UploadersConfig.json")));
            check(((JArray)schema["ApplicationConfig"]).Values<string>().Append("ApplicationVersion").Order().SequenceEqual(fixtureApp.Properties().Select(x=>x.Name).Order()) &&
                ((JArray)schema["UploadersConfig"]).Values<string>().Append("ApplicationVersion").Order().SequenceEqual(fixtureUploaders.Properties().Select(x=>x.Name).Order()), version + " fixture includes complete source-checked application and uploader field inventories");
            check(((JArray)schema["TaskSettings"]).Values<string>().Order().SequenceEqual(((JObject)fixtureApp["DefaultTaskSettings"]).Properties().Select(x=>x.Name).Order()), version + " fixture includes complete task field inventory");
            foreach (var section in new[] { ("GeneralSettings", "TaskSettingsGeneral"), ("ImageSettings", "TaskSettingsImage"), ("CaptureSettings", "TaskSettingsCapture"), ("UploadSettings", "TaskSettingsUpload"), ("ToolsSettings", "TaskSettingsTools"), ("AdvancedSettings", "TaskSettingsAdvanced") })
                check(((JArray)schema[section.Item2]).Values<string>().Order().SequenceEqual(((JObject)fixtureApp["DefaultTaskSettings"][section.Item1]).Properties().Select(x=>x.Name).Order()), version + " fixture includes source-checked section: " + section.Item1);
            check(fixtureApp["DefaultTaskSettings"]["CaptureSettings"]["SurfaceOptions"] == null &&
                fixtureApp["DefaultTaskSettings"]["CaptureSettings"]["RegionCaptureOptions"] is JObject &&
                fixtureApp["DefaultTaskSettings"]["ToolsSettings"]["ScreenColorPickerOptions"] is JObject && fixtureApp["ThemeOptions"] is JObject,
                version + " fixture uses actual 22 renamed structures");
            check(((JObject)schema["InputKeys"]).Properties().All(key => Enum.TryParse(key.Name, out System.Windows.Forms.Keys parsed) && (int)parsed == (int)key.Value), version + " every audited InputKey name and value matches Windows Keys");
            var original = Directory.GetFiles(source).ToDictionary(Path.GetFileName, File.ReadAllBytes);
            var plan = ImportService.Prepare(source, destination);
            JObject app = Read(plan, "ApplicationConfig.json");
            JObject capture = (JObject)app["DefaultTaskSettings"]["CaptureSettings"];
            JObject region = (JObject)capture["SurfaceOptions"];
            check(!(bool)region["QuickCrop"] && !(bool)region["DetectWindows"] && (int)region["BackgroundDimStrength"] == 37 && (bool)region["ShowCrosshair"], version + " region capture mappings retained");
            check((int)region["MagnifierPixelSize"] == 12 && (int)region["MagnifierPixelCount"] == 15, version + " magnifier pixel size converted");
            check((string)app["DefaultTaskSettings"]["ToolsSettings"]["ScreenColorPickerFormat"] == "$r255/$g255/$b255" &&
                !(bool)region["ColorPickerOptions"]["RecentColorsSelected"], version + " color picker formats and recent colors retained");
            check(!(bool)capture["ScreenRecordAutoStart"] && (float)capture["ScreenRecordStartDelay"] == 0, version + " recording requires manual start and disabled delay is honored");
            JArray hotkeys = (JArray)Read(plan, "HotkeysConfig.json")["Hotkeys"];
            check(hotkeys.Count == 5 && (string)hotkeys[0]["TaskSettings"]["Job"] == "RectangleRegion" &&
                ((string)hotkeys[0]["HotkeyInfo"]["Hotkey"]).Contains("Control") && (bool)hotkeys[0]["HotkeyInfo"]["Win"], version + " ordinary hotkeys and Win modifier retained");
            check((string)hotkeys[1]["TaskSettings"]["Job"] == "None" && (string)hotkeys[1]["TaskSettings"]["Description"] == "unsupported tool", version + " unsupported job disabled per entry without aborting import");
            check((string)hotkeys[2]["TaskSettings"]["FileDestination"] == "CustomFileUploader" && !(bool)hotkeys[2]["TaskSettings"]["UseDefaultDestinations"], version + " unsupported provider cannot default to a public host");
            JObject uploaders = Read(plan, "UploadersConfig.json");
            int blocked = (int)hotkeys[2]["TaskSettings"]["CustomUploaderIndex"];
            check((string)uploaders["CustomUploadersList"][blocked]["RequestURL"] == "" && blocked == 1 &&
                (string)uploaders["CustomUploadersList"][0]["RequestURL"] == "https://example.invalid/upload" && !(bool)app["UseSecondaryUploaders"], version + " unsupported destinations use dedicated empty uploader rather than existing custom or secondary uploader");
            check((string)hotkeys[3]["TaskSettings"]["FileDestination"] == "CustomFileUploader", version + " unsupported provider filter blocks workflow destinations");
            check((string)hotkeys[4]["TaskSettings"]["FileDestination"] == "AmazonS3", version + " compatible provider preserved by name");
            string actions = (string)app["DefaultTaskSettings"]["AfterCaptureJob"];
            check(actions.Contains("CopyImageToClipboard") && actions.Contains("SaveImageToFile") && !actions.Contains("Upload") && !actions.Contains("PerformActions") && !actions.Contains("DeleteFile") &&
                !(bool)app["DefaultTaskSettings"]["WatchFolderEnabled"] && (bool)app["DisableHotkeys"] && (bool)app["DisableUpload"], version + " safe local capture retained and automatic actions disabled");
            check(plan.Warnings.Any(x => x.Contains("ThemeOptions")) && plan.Warnings.Any(x => x.Contains("ScreenRecordSystemAudio")) &&
                plan.Warnings.Any(x => x.Contains("ImageGIFQuality")) && plan.Warnings.Any(x => x.Contains("Mega")) && plan.Warnings.Any(x => x.Contains("MouseHighlighter")), version + " theme native recording image encoder uploader credentials and unsupported job limitations reported");
            check(!plan.Warnings.Any(x => x.Contains("fixture-secret")) && !plan.Warnings.Any(x => x.Contains("example.invalid")), version + " compatibility report contains no settings values or credentials");
            check(original.All(x => File.ReadAllBytes(Path.Combine(source, x.Key)).SequenceEqual(x.Value)) && !Directory.Exists(destination), version + " prepare leaves source byte-identical and destination untouched");
            ImportService.Apply(plan);
            var loadedApp = ShareX.ApplicationConfig.Load(Path.Combine(destination, "ApplicationConfig.json"), fallbackSupport:false);
            var loadedKeys = ShareX.HotkeysConfig.Load(Path.Combine(destination, "HotkeysConfig.json"), fallbackSupport:false);
            var loadedUploaders = ShareX.UploadersLib.UploadersConfig.Load(Path.Combine(destination, "UploadersConfig.json"), fallbackSupport:false);
            check(!loadedApp.DefaultTaskSettings.CaptureSettings.SurfaceOptions.QuickCrop && loadedKeys.Hotkeys.Count == 5 &&
                loadedUploaders.CustomUploadersList.Count == 2 && loadedApp.DisableUpload, version + " prepared output loads through real production SettingsBase.Load");
            reject(() => loadedUploaders.CustomUploadersList[blocked].GetRequestURL(null), version + " blocked uploader fails locally before any request can be sent");
            if (Version.Parse(version).Minor >= 2)
                check(plan.Warnings.Any(x => x.Contains("AfterCaptureJob") && x.Contains("unsupported")), version + " OpenInPhotoEditor flag explicitly reported as unsupported");

            JObject input = JObject.Parse(File.ReadAllText(Path.Combine(source, "HotkeysConfig.json")));
            foreach (string key in new[] { "Return", "Enter", "Capital", "CapsLock", "Snapshot", "PrintScreen", "Prior", "PageUp", "Next", "PageDown", "HanguelMode", "OemSemicolon", "Oem1", "Control, Shift, F12" })
            {
                input["Hotkeys"][0]["HotkeyInfo"]["Hotkey"] = key;
                File.WriteAllText(Path.Combine(source, "HotkeysConfig.json"), input.ToString());
                var aliases = ImportService.Prepare(source, destination);
                check(!aliases.Warnings.Any(x => x.Contains("HotkeyInfo.Hotkey")), version + " InputKey alias accepted: " + key);
            }
            input["Hotkeys"][0]["HotkeyInfo"]["Hotkey"] = 131116;
            input["Hotkeys"][0]["TaskSettings"]["Job"] = 8;
            File.WriteAllText(Path.Combine(source, "HotkeysConfig.json"), input.ToString());
            var numeric = ImportService.Prepare(source, destination);
            check((string)Read(numeric, "HotkeysConfig.json")["Hotkeys"][0]["TaskSettings"]["Job"] == "None" &&
                !numeric.Warnings.Any(x => x.Contains("HotkeyInfo.Hotkey")), version + " numeric jobs disabled while native virtual-key codes retained");
            input["ApplicationVersion"] = "21.0.0";
            input["Hotkeys"] = new JArray(new JObject { ["HotkeyInfo"] = new JObject { ["Hotkey"] = "PrintScreen" }, ["TaskSettings"] = new JObject { ["Job"] = "RectangleRegion" } });
            File.WriteAllText(Path.Combine(source, "HotkeysConfig.json"), input.ToString());
            var mixed = ImportService.Prepare(source, destination);
            check(mixed.Warnings.Any(x => x.Contains("version differs")) && mixed.HotkeyCount == 1, version + " mixed supported versions converted independently and reported");
            input["ApplicationVersion"] = "22.5.0";
            File.WriteAllText(Path.Combine(source, "HotkeysConfig.json"), input.ToString());
            reject(() => ImportService.Prepare(source, destination), version + " future optional file version rejected");
            File.WriteAllText(Path.Combine(source, "HotkeysConfig.json"), "{ malformed");
            reject(() => ImportService.Prepare(source, destination), version + " malformed optional JSON rejected");
            File.Delete(Path.Combine(source, "HotkeysConfig.json"));
            File.WriteAllText(Path.Combine(source, "UploadersConfig.json"), new JObject { ["ApplicationVersion"] = version, ["MegaPassword"] = "fixture-secret-only" }.ToString());
            var onlyUnsupported = ImportService.Prepare(source, destination);
            check(onlyUnsupported.HasUploaders && onlyUnsupported.Warnings.Any(x => x.Contains("MegaPassword")), version + " unsupported-only provider settings nonfatal and clearly reported");
            fixtureApp["DefaultTaskSettings"]["ImageSettings"]["ImageFormat"] = "WEBP";
            File.WriteAllText(Path.Combine(source, "ApplicationConfig.json"), fixtureApp.ToString());
            var format = ImportService.Prepare(source, destination);
            check((string)Read(format, "ApplicationConfig.json")["DefaultTaskSettings"]["ImageSettings"]["ImageFormat"] == "PNG" && format.Warnings.Any(x => x.Contains("ImageFormat")), version + " unsupported image format resets with explicit warning");
            fixtureApp["DefaultTaskSettings"]["ImageSettings"]["ImageEffectPresets"][0]["Effects"][0]["$type"] = "System.Windows.Data.ObjectDataProvider, PresentationFramework";
            File.WriteAllText(Path.Combine(source, "ApplicationConfig.json"), fixtureApp.ToString());
            reject(() => ImportService.Prepare(source, destination), version + " strict JSON type allowlist remains enforced for 22");
        }
        RuntimeBlocking(root, check);
    }

    private static void RuntimeBlocking(string root, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        Type program = typeof(TaskSettings).Assembly.GetType("ShareX.Program", true);
        string[] names = { "Settings", "DefaultTaskSettings", "UploadersConfig" };
        var previous = names.ToDictionary(x => x, x => program.GetProperty(x, flags).GetValue(null));
        try
        {
            foreach (string scenario in new[] { "default-destination", "default-filter", "mixed-inherited", "local-inherited-filters", "mixed-local-filters", "own-destination-default-filter", "own-destination-default-destination" })
            {
                string source = Path.Combine(root, "runtime-" + scenario);
                string destination = Path.Combine(root, "runtime-output-" + scenario);
                Directory.CreateDirectory(source);
                foreach (string name in new[] { "ApplicationConfig.json", "HotkeysConfig.json", "UploadersConfig.json" })
                    File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "22.4.0", name), Path.Combine(source, name));
                JObject app = JObject.Parse(File.ReadAllText(Path.Combine(source, "ApplicationConfig.json")));
                JObject task = (JObject)app["DefaultTaskSettings"];
                string filter = UploaderFactory.FileUploaderServices[FileDestination.SharedFolder].ServiceIdentifier;
                task["UploadSettings"]["UploaderFilters"] = new JArray(new JObject { ["Uploader"] = filter, ["Extensions"] = new JArray("bin") });
                if (scenario == "default-filter" || scenario == "own-destination-default-filter") task["UploadSettings"]["UploaderFilters"][0]["Uploader"] = "Unsupported22Provider";
                else if (scenario != "local-inherited-filters") task["FileDestination"] = "Mega";
                JObject keys = new JObject {
                    ["ApplicationVersion"] = scenario.StartsWith("mixed-") ? "21.0.0" : "22.4.0",
                    ["Hotkeys"] = new JArray(new JObject {
                        ["HotkeyInfo"] = new JObject { ["Hotkey"] = "PrintScreen" },
                        ["TaskSettings"] = new JObject {
                            ["Job"] = "RectangleRegion", ["UseDefaultDestinations"] = scenario != "local-inherited-filters" && !scenario.StartsWith("own-destination-"),
                            ["UseDefaultUploadSettings"] = true, ["UploadSettings"] = JValue.CreateNull(),
                            ["FileDestination"] = scenario == "local-inherited-filters" ? "Mega" : "Dropbox"
                        }
                    })
                };
                if (scenario == "mixed-local-filters")
                {
                    keys["Hotkeys"][0]["TaskSettings"]["UseDefaultUploadSettings"] = false;
                    keys["Hotkeys"][0]["TaskSettings"]["UploadSettings"] = new JObject { ["UploaderFilters"] = new JArray(new JObject { ["Uploader"] = filter, ["Extensions"] = new JArray("bin") }) };
                }
                File.WriteAllText(Path.Combine(source, "ApplicationConfig.json"), app.ToString());
                File.WriteAllText(Path.Combine(source, "HotkeysConfig.json"), keys.ToString());
                var plan = ImportService.Prepare(source, destination);
                ImportService.Apply(plan);
                var loadedApp = ApplicationConfig.Load(Path.Combine(destination, "ApplicationConfig.json"), fallbackSupport:false);
                var loadedKeys = HotkeysConfig.Load(Path.Combine(destination, "HotkeysConfig.json"), fallbackSupport:false);
                var uploaders = UploadersConfig.Load(Path.Combine(destination, "UploadersConfig.json"), fallbackSupport:false);
                loadedApp.DisableUpload = false;
                uploaders.CustomFileUploaderSelected = 0;
                program.GetProperty("Settings", flags).SetValue(null, loadedApp);
                program.GetProperty("DefaultTaskSettings", flags).SetValue(null, loadedApp.DefaultTaskSettings);
                program.GetProperty("UploadersConfig", flags).SetValue(null, uploaders);
                var tasks = scenario == "local-inherited-filters"
                    ? new[] { TaskSettings.GetSafeTaskSettings(loadedKeys.Hotkeys[0].TaskSettings) }
                    : new[] { TaskSettings.GetDefaultTaskSettings(), TaskSettings.GetSafeTaskSettings(loadedKeys.Hotkeys[0].TaskSettings), TaskSettings.GetSafeTaskSettings(new TaskSettings { UseDefaultGeneralSettings = false }) };
                foreach (TaskSettings runtime in tasks)
                {
                    check(runtime.OverrideCustomUploader && runtime.CustomUploaderIndex == 1 && runtime.UploadSettings.UploaderFilters.Count == 0,
                        scenario + " runtime task preserves placeholder and clears inherited filters");
                    using var worker = WorkerTask.CreateDataUploaderTask(EDataType.File, new MemoryStream(new byte[] { 1 }), "synthetic.bin", runtime);
                    bool selected = false;
                    worker.UploadConfirmation = (service, _) => { selected = service.ServiceIdentifier == UploaderFactory.FileUploaderServices[FileDestination.CustomFileUploader].ServiceIdentifier; return false; };
                    typeof(WorkerTask).GetMethod("CreateTaskReferenceHelper", flags).Invoke(worker, null);
                    object reference = typeof(WorkerTask).GetField("taskReferenceHelper", flags).GetValue(worker);
                    var actual = UploaderFactory.FileUploaderServices[FileDestination.CustomFileUploader].CreateUploader(uploaders, (TaskReferenceHelper)reference);
                    object item = actual.GetType().GetField("uploader", flags).GetValue(actual);
                    check(ReferenceEquals(item, uploaders.CustomUploadersList[1]), scenario + " actual custom uploader resolves placeholder instead of selected existing uploader");
                    typeof(WorkerTask).GetMethod("DoUpload", flags).Invoke(worker, new object[] { worker.Data, worker.Info.FileName, 0 });
                    check(selected && worker.StopRequested, scenario + " actual upload execution selects blocked service without filter fallback or network");
                }
            }
        }
        finally { foreach (string name in names) program.GetProperty(name, flags).SetValue(null, previous[name]); }
    }

    private static JObject Read(ImportService.Plan plan, string file) => JObject.Parse(Encoding.UTF8.GetString(plan.Files[file]));
}
