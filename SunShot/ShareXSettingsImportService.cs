// Copyright (c) 2026 SunSinD
// SPDX-License-Identifier: GPL-3.0-or-later

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ShareX.HelpersLib;
using ShareX.HistoryLib;
using ShareX.ImageEffectsLib;
using ShareX.UploadersLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ShareX
{
    internal static class ShareXSettingsImportService
    {
        private const long MaximumFileSize = 16 * 1024 * 1024;
        private const string JournalName = ".SunShot-import-pending.json";
        private static readonly string[] ConfigNames = { "ApplicationConfig.json", "HotkeysConfig.json", "UploadersConfig.json" };

        // Merely opening the form does not inspect ShareX files or its registry.
        public static string SuggestedSourceFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShareX");

        public static bool HasPendingImport(string destinationFolder) => File.Exists(Path.Combine(destinationFolder, JournalName));

        internal sealed class ImportPlan
        {
            internal readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
            internal string Destination;
            public int HotkeyCount { get; internal set; }
            public bool HasUploaders { get; internal set; }
            public bool HasHotkeys { get; internal set; }
            public int UnreadableCredentials { get; internal set; }
            public string SourceVersion { get; internal set; }
            public List<string> Warnings { get; } = new List<string>();
        }

        private static JsonSerializer CreateSerializer() => new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead,
            SerializationBinder = new ImportImageEffectsBinder(),
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            ContractResolver = new WritablePropertiesOnlyResolver(),
            Converters = { new StringEnumConverter() },
            MaxDepth = 64
        };

        public static ImportPlan Prepare(string sourceFolder, string destinationFolder)
        {
            string source = NormalizeFolder(sourceFolder);
            string destination = NormalizeFolder(destinationFolder);
            if (IsWithin(source, destination) || IsWithin(destination, source))
                throw new InvalidDataException("The ShareX and SunShot profile folders must be separate and cannot contain one another.");
            EnsureNoLinks(source);
            EnsureNoLinks(destination);
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException("The selected ShareX profile folder does not exist.");

            JObject appJson = ReadConfig(Path.Combine(source, ConfigNames[0]));
            if (appJson["DefaultTaskSettings"] is not JObject)
                throw new InvalidDataException("ApplicationConfig.json must contain ShareX task settings.");
            ImportPlan plan = new ImportPlan { Destination = destination, SourceVersion = (string)appJson["ApplicationVersion"] };
            foreach (string field in new[] { "CustomHotkeysConfigPath", "CustomUploadersConfigPath" })
                if (appJson[field]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)appJson[field]))
                    plan.Warnings.Add(field + ": external config path was not followed. Only files in the selected profile folder are imported; an absent file uses SunShot defaults.");
            ShareX22SettingsMigration migration = new ShareX22SettingsMigration(plan.Warnings, CreateSerializer());
            migration.Convert(appJson, typeof(ApplicationConfig), ConfigNames[0]);

            string hotkeyPath = Path.Combine(source, ConfigNames[1]);
            JObject hotkeyJson = null;
            if (File.Exists(hotkeyPath))
            {
                JObject json = ReadConfig(hotkeyPath);
                if (json["Hotkeys"] is not JArray) throw new InvalidDataException("HotkeysConfig.json must contain a Hotkeys list.");
                CheckFileVersion(json, plan, ConfigNames[1]);
                migration.Convert(json, typeof(HotkeysConfig), ConfigNames[1]);
                hotkeyJson = json;
                plan.HasHotkeys = true;
                foreach (JObject entry in ((JArray)json["Hotkeys"]).OfType<JObject>())
                {
                    if (entry["TaskSettings"] is not JObject task) continue;
                    // ShareX omits inherited task sections by saving them as null.
                    foreach (string section in new[] { "GeneralSettings", "ImageSettings", "CaptureSettings", "UploadSettings", "ToolsSettings", "AdvancedSettings", "ExternalPrograms" })
                    {
                        string inherited = section == "ExternalPrograms" ? "UseDefaultActions" : "UseDefault" + section;
                        if (task[section]?.Type == JTokenType.Null && (bool?)(task[inherited]) != false) task.Remove(section);
                    }
                    ValidateContainers(task, JObject.FromObject(new TaskSettings(), CreateSerializer()));
                }
            }
            string uploaderName = ConfigNames[2];
            if ((bool?)appJson["UseMachineSpecificUploadersConfig"] == true)
            {
                string machineName = FileHelpers.SanitizeFileName(Environment.MachineName.ToLowerInvariant());
                string machinePath = Path.Combine(source, $"UploadersConfig-{machineName}.json");
                if (File.Exists(machinePath)) uploaderName = Path.GetFileName(machinePath);
            }
            string uploaderPath = Path.Combine(source, uploaderName);
            UploadersConfig uploaders = new UploadersConfig();
            if (File.Exists(uploaderPath))
            {
                JObject json = ReadConfig(uploaderPath);
                CheckFileVersion(json, plan, uploaderName);
                bool version22 = Version.Parse((string)json["ApplicationVersion"]).Major == 22;
                if (!version22 && !json.Properties().Any(x => x.Name != "ApplicationVersion" &&
                    typeof(UploadersConfig).GetProperty(x.Name) != null))
                    throw new InvalidDataException("UploadersConfig.json does not contain recognized uploader settings.");
                foreach (JValue value in json.Descendants().OfType<JValue>().Where(x => x.Type == JTokenType.String))
                {
                    string text = (string)value;
                    const string tag = "$DPAPIEncrypted$";
                    if (text.StartsWith(tag, StringComparison.Ordinal))
                    {
                        try { DPAPI.Decrypt(text.Substring(tag.Length)); }
                        catch { value.Value = ""; plan.UnreadableCredentials++; }
                    }
                }
                migration.Convert(json, typeof(UploadersConfig), uploaderName);
                uploaders = Deserialize<UploadersConfig>(json);
                ValidateContainers(json, JObject.FromObject(new UploadersConfig(), CreateSerializer()));
                plan.HasUploaders = true;
            }

            migration.Finish(uploaders);
            ApplicationConfig app = Deserialize<ApplicationConfig>(appJson);
            ValidateContainers(appJson, JObject.FromObject(new ApplicationConfig(), CreateSerializer()));
            HotkeysConfig hotkeys = hotkeyJson == null ? new HotkeysConfig() : Deserialize<HotkeysConfig>(hotkeyJson);
            if (hotkeys.Hotkeys == null || hotkeys.Hotkeys.Any(x => x?.TaskSettings == null || x.HotkeyInfo == null))
                throw new InvalidDataException("The hotkey configuration contains an incomplete entry.");
            plan.HotkeyCount = hotkeys.Hotkeys.Count;
            migration.BlockInheritedDestinations(app, hotkeys);
            // Secondary destinations are unavailable in 22 and must never become a
            // fallback for a blocked destination, including a mixed 21/22 profile.
            if (plan.Warnings.Count > 0) app.UseSecondaryUploaders = false;
            ResetSourceFFmpeg(app.DefaultTaskSettings, source, plan, "DefaultTaskSettings");
            for (int i = 0; i < hotkeys.Hotkeys.Count; i++) ResetSourceFFmpeg(hotkeys.Hotkeys[i].TaskSettings, source, plan, $"Hotkeys[{i + 1}]");
            SanitizeApplication(app);
            SanitizeTask(app.DefaultTaskSettings);
            foreach (HotkeySettings hotkey in hotkeys.Hotkeys) SanitizeTask(hotkey.TaskSettings);
            plan.Files[ConfigNames[0]] = Serialize(app);
            plan.Files[ConfigNames[1]] = Serialize(hotkeys);
            plan.Files[ConfigNames[2]] = Serialize(uploaders);
            return plan;
        }

        private static void ResetSourceFFmpeg(TaskSettings task, string source, ImportPlan plan, string location)
        {
            var options = task?.CaptureSettings?.FFmpegOptions;
            if (options == null || !options.OverrideCLIPath || string.IsNullOrWhiteSpace(options.CLIPath)) return;
            try
            {
                string path = Path.GetFullPath(options.CLIPath, source);
                if (!IsWithin(path, source)) return;
            }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException)
            {
                // Invalid overrides are also unusable in the destination profile.
            }
            options.OverrideCLIPath = false;
            options.CLIPath = "";
            plan.Warnings.Add(location + ".CaptureSettings.FFmpegOptions: FFmpeg override within the source profile (or invalid path) cleared; SunShot's own recorder path will be used.");
        }

        private static JObject ReadConfig(string path)
        {
            EnsureNoLinks(path);
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length == 0 || stream.Length > MaximumFileSize)
                throw new InvalidDataException($"{Path.GetFileName(path)} is empty or larger than 16 MB.");
            using StreamReader input = new StreamReader(stream, Encoding.UTF8, true);
            using JsonTextReader reader = new JsonTextReader(input) { MaxDepth = 64, DateParseHandling = DateParseHandling.None };
            JObject json = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("A settings file contains extra data after its JSON object.");
            if (!Version.TryParse((string)json["ApplicationVersion"], out Version version) || version.Build > 0 || version.Revision > 0 ||
                !((version.Major == 21 && version.Minor == 0) || (version.Major == 22 && version.Minor >= 0 && version.Minor <= 4)))
                throw new InvalidDataException("This importer supports ShareX 21.0 and 22.0 through 22.4 release settings only. Other versions must first be checked for compatibility.");
            ImportImageEffectsBinder effects = new ImportImageEffectsBinder();
            foreach (JProperty metadata in json.Descendants().OfType<JProperty>().Where(x => x.Name.StartsWith("$", StringComparison.Ordinal)))
            {
                bool effectEntry = metadata.Parent is JObject effect && effect.Parent is JArray list &&
                    list.Parent is JProperty property && property.Name == "Effects" &&
                    property.Parent is JObject preset && preset.Parent is JArray presets &&
                    presets.Parent is JProperty presetProperty && presetProperty.Name == "ImageEffectPresets";
                if (metadata.Name != "$type" || !effectEntry || metadata.Value.Type != JTokenType.String ||
                    !effects.IsKnown((string)metadata.Value))
                    throw new InvalidDataException("Settings contain unsupported JSON type or reference metadata.");
            }
            return json;
        }

        private static void CheckFileVersion(JObject json, ImportPlan plan, string file)
        {
            Version version = Version.Parse((string)json["ApplicationVersion"]), source = Version.Parse(plan.SourceVersion);
            if (version.Major != source.Major || version.Minor != source.Minor)
                plan.Warnings.Add(file + ": version differs from ApplicationConfig.json; each file is converted using its own supported version. Review the combined workflows and destinations.");
        }

        private sealed class ImportImageEffectsBinder : ISerializationBinder
        {
            private static readonly Type[] Allowed = typeof(ImageEffect).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(ImageEffect).IsAssignableFrom(type)).ToArray();

            public bool IsKnown(string qualifiedName)
            {
                if (string.IsNullOrEmpty(qualifiedName)) return false;
                int comma = qualifiedName.IndexOf(',');
                return BindToType(comma < 0 ? null : qualifiedName.Substring(comma + 1).Trim(),
                    (comma < 0 ? qualifiedName : qualifiedName.Substring(0, comma)).Trim()) != null;
            }

            public Type BindToType(string assemblyName, string typeName)
            {
                if (assemblyName != null)
                {
                    try
                    {
                        if (new System.Reflection.AssemblyName(assemblyName).Name != typeof(ImageEffect).Assembly.GetName().Name) return null;
                    }
                    catch { return null; }
                }
                return Allowed.FirstOrDefault(type => type.FullName == typeName || type.Name == typeName);
            }

            public void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                if (!Allowed.Contains(serializedType)) throw new JsonSerializationException("Unsupported image effect type.");
                assemblyName = serializedType.Assembly.GetName().Name;
                typeName = serializedType.FullName;
            }
        }

        private static T Deserialize<T>(JObject json)
        {
            try { return json.ToObject<T>(CreateSerializer()); }
            catch (JsonException) { throw new InvalidDataException($"{typeof(T).Name} contains a setting with an invalid type or value."); }
        }

        private static void ValidateContainers(JObject settings, JObject defaults)
        {
            foreach (JProperty property in defaults.Properties())
            {
                JToken value = settings[property.Name];
                if (value == null) continue;
                if ((property.Value is JObject || property.Value is JArray) && value.Type == JTokenType.Null)
                    throw new InvalidDataException("A required settings object or list is null.");
                if (value is JObject obj && property.Value is JObject defaultObject) ValidateContainers(obj, defaultObject);
                if (value is JArray array && property.Value is JArray && array.Any(x => x.Type == JTokenType.Null))
                    throw new InvalidDataException("A settings list contains an incomplete entry.");
            }
        }

        private static byte[] Serialize<T>(T value) where T : SettingsBase<T>, new()
        {
            value.ApplicationVersion = Application.ProductVersion;
            StringBuilder text = new StringBuilder();
            using (StringWriter output = new StringWriter(text))
            using (JsonTextWriter writer = new JsonTextWriter(output) { Formatting = Formatting.Indented })
                CreateSerializer().Serialize(writer, value);
            return new UTF8Encoding(false).GetBytes(text.ToString());
        }

        private static void SanitizeApplication(ApplicationConfig app)
        {
            if (app.DefaultTaskSettings == null) throw new InvalidDataException("Default task settings are missing.");
            app.AutoCheckUpdate = false;
            app.UpdateChannel = UpdateChannel.Release;
            app.CheckPreReleaseUpdates = false;
            app.CustomUploadersConfigPath = "";
            app.CustomHotkeysConfigPath = "";
            app.UseMachineSpecificUploadersConfig = false;
            app.UseCustomScreenshotsPath = false;
            app.CustomScreenshotsPath = "";
            app.CustomScreenshotsPath2 = "";
            app.SaveImageSubFolderPattern = "%y-%mo";
            app.SaveImageSubFolderPatternWindow = "";
            app.FileUploadDefaultDirectory = "";
            app.RecentTasks = null;
            app.RecentTasksSave = false;
            app.HistorySettings = new HistorySettings();
            app.ImageHistorySettings = new ImageHistorySettings();
            app.ActionsToolbarRunAtStartup = false;
            app.DisableHotkeys = true;
            app.DisableUpload = true;
            app.SilentRun = false;
            app.ShowTray = true;
            app.TrayLeftClickAction = HotkeyType.RectangleRegion;
            app.TrayLeftDoubleClickAction = HotkeyType.OpenMainWindow;
            app.TrayMiddleClickAction = HotkeyType.None;
            app.QuickTaskPresets ??= new List<QuickTaskInfo>();
            app.QuickTaskPresets.RemoveAll(x => x == null);
            app.QuickTaskPresets.RemoveAll(preset =>
            {
                AfterCaptureTasks original = preset.AfterCaptureTasks;
                preset.AfterCaptureTasks = SafeCaptureTasks(original);
                bool wasSeparator = original == AfterCaptureTasks.None && string.IsNullOrEmpty(preset.Name);
                if (original != preset.AfterCaptureTasks) preset.Name = null;
                return !wasSeparator && preset.AfterCaptureTasks == AfterCaptureTasks.None;
            });
            app.ActionsToolbarList?.RemoveAll(x => x == HotkeyType.AnalyzeImage);
        }

        private static AfterCaptureTasks SafeCaptureTasks(AfterCaptureTasks tasks) => tasks &
            ~(AfterCaptureTasks.UploadImageToHost | AfterCaptureTasks.AnalyzeImage | AfterCaptureTasks.PerformActions | AfterCaptureTasks.DeleteFile);

        private static void SanitizeTask(TaskSettings task)
        {
            task.AfterCaptureJob = SafeCaptureTasks(task.AfterCaptureJob);
            if (task.Job == HotkeyType.AnalyzeImage) task.Job = HotkeyType.None;
            task.OverrideScreenshotsFolder = false;
            task.ScreenshotsFolder = "";
            task.WatchFolderEnabled = false;
            task.WatchFolderList = new List<WatchFolderSettings>();
            task.GeneralSettings ??= new TaskSettingsGeneral();
            task.ImageSettings ??= new TaskSettingsImage();
            task.CaptureSettings ??= new TaskSettingsCapture();
            task.UploadSettings ??= new TaskSettingsUpload();
            task.ToolsSettings ??= new TaskSettingsTools();
            task.AdvancedSettings ??= new TaskSettingsAdvanced();
            task.ExternalPrograms ??= new List<ExternalProgram>();
            task.ToolsSettings.AIOptions = new AIOptions();
        }

        public static string Apply(ImportPlan plan)
        {
            string destination = plan.Destination;
            EnsureNoLinks(destination);
            Directory.CreateDirectory(destination);
            RecoverPendingImport(destination);
            string backupName = "ShareX-import-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
            string backup = Path.Combine(destination, "Backup", backupName);
            EnsureNoLinks(backup);
            Directory.CreateDirectory(backup);
            foreach (string name in ConfigNames)
            {
                string target = Path.Combine(destination, name);
                EnsureNoLinks(target);
                if (File.Exists(target)) File.Copy(target, Path.Combine(backup, name + ".original"));
                else File.WriteAllText(Path.Combine(backup, name + ".absent"), "");
                WriteDurable(Path.Combine(backup, name + ".new"), plan.Files[name]);
            }
            string journal = Path.Combine(destination, JournalName);
            WriteDurable(journal, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(backupName)));
            try
            {
                foreach (string name in ConfigNames)
                    ReplaceFile(Path.Combine(backup, name + ".new"), Path.Combine(destination, name));
                File.Delete(journal);
            }
            catch
            {
                RecoverPendingImport(destination);
                throw;
            }
            return backup;
        }

        // A power loss between file replacements is rolled back before any settings are loaded.
        public static void RecoverPendingImport(string destinationFolder)
        {
            string destination = NormalizeFolder(destinationFolder);
            string journal = Path.Combine(destination, JournalName);
            if (!File.Exists(journal)) return;
            EnsureNoLinks(journal);
            string backupName = JsonConvert.DeserializeObject<string>(File.ReadAllText(journal));
            if (string.IsNullOrEmpty(backupName) || !backupName.StartsWith("ShareX-import-", StringComparison.Ordinal) ||
                backupName != Path.GetFileName(backupName) || backupName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("SunShot import recovery information is invalid.");
            string backup = Path.Combine(destination, "Backup", backupName);
            EnsureNoLinks(backup);
            // Check the whole backup before touching any destination files.
            foreach (string name in ConfigNames)
            {
                EnsureNoLinks(Path.Combine(destination, name));
                EnsureNoLinks(Path.Combine(backup, name + ".original"));
                EnsureNoLinks(Path.Combine(backup, name + ".absent"));
                if (!File.Exists(Path.Combine(backup, name + ".original")) && !File.Exists(Path.Combine(backup, name + ".absent")))
                    throw new InvalidDataException("An import backup is incomplete. Restore the settings from its Backup folder before opening SunShot.");
            }
            foreach (string name in ConfigNames)
            {
                string original = Path.Combine(backup, name + ".original");
                string target = Path.Combine(destination, name);
                if (File.Exists(original))
                {
                    string recovery = Path.Combine(backup, name + ".restore");
                    EnsureNoLinks(recovery);
                    File.Copy(original, recovery, true);
                    ReplaceFile(recovery, target);
                }
                else File.Delete(target);
            }
            File.Delete(journal);
        }

        private static void ReplaceFile(string staged, string target)
        {
            if (File.Exists(target)) File.Replace(staged, target, null);
            else File.Move(staged, target);
        }

        private static void WriteDurable(string path, byte[] bytes)
        {
            using FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            output.Write(bytes);
            output.Flush(true);
        }

        private static string NormalizeFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("Select a profile folder.");
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }

        private static bool IsWithin(string child, string parent) => child.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
            child.StartsWith(Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static void EnsureNoLinks(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try
                {
                    FileAttributes attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        FileSystemInfo entry = (attributes & FileAttributes.Directory) != 0
                            ? new DirectoryInfo(current) : new FileInfo(current);
                        if (entry.LinkTarget != null)
                            throw new InvalidDataException("Profile files and folders cannot be symbolic links or junctions. Select a regular local profile folder.");
                    }
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }
    }
}
