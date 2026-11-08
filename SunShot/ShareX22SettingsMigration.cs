// Copyright (c) 2026 SunSinD
// SPDX-License-Identifier: GPL-3.0-or-later

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ShareX.UploadersLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShareX
{
    // Audited against the local upstream v22.0.0 through v22.4.0 tags. This is a
    // conversion into the existing 21-based model, not a loader for arbitrary versions.
    internal sealed class ShareX22SettingsMigration
    {
        private readonly List<string> warnings;
        private readonly List<JObject> blockedTasks = new List<JObject>();
        private readonly JsonSerializer serializer;
        private JObject defaultTask;
        private bool defaultHadFilters;

        internal ShareX22SettingsMigration(List<string> warnings, JsonSerializer serializer)
        {
            this.warnings = warnings;
            this.serializer = serializer;
        }

        internal void Convert(JObject json, Type type, string file)
        {
            if (type == typeof(ApplicationConfig)) defaultTask = json["DefaultTaskSettings"] as JObject;
            if (Version.Parse((string)json["ApplicationVersion"]).Major != 22) return;
            if (type == typeof(ApplicationConfig))
            {
                Warn("ShareX 22: compatible common settings are imported. Native recording/audio/camera, new image encoding options, themes and newer tools may need reconfiguration. Details follow; values and credentials are never included in this report.");
                json["UseSecondaryUploaders"] = false;
                json["SecondaryImageUploaders"] = new JArray();
                json["SecondaryTextUploaders"] = new JArray();
                json["SecondaryFileUploaders"] = new JArray();
                if (json["DefaultTaskSettings"] is JObject task) MapTask(task, file + ".DefaultTaskSettings");
            }
            else if (type == typeof(HotkeysConfig) && json["Hotkeys"] is JArray hotkeys)
            {
                for (int i = 0; i < hotkeys.Count; i++)
                    if (hotkeys[i]?["TaskSettings"] is JObject task) MapTask(task, file + $".Hotkeys[{i + 1}].TaskSettings");
            }
            Normalize(json, type, file);
        }

        private void MapTask(JObject task, string path)
        {
            if (ReferenceEquals(task, defaultTask))
                defaultHadFilters = (task["UploadSettings"] as JObject)?["UploaderFilters"] is JArray defaultFilters && defaultFilters.Count > 0;
            if (task["CaptureSettings"] is JObject capture)
            {
                Rename(capture, "RegionCaptureOptions", "SurfaceOptions");
                if (capture["SurfaceOptions"] is JObject region)
                {
                    Rename(region, "QuickCapture", "QuickCrop");
                    Rename(region, "ShowScreenCrosshair", "ShowCrosshair");
                    // 22 expresses the magnifier width in pixels; 21 uses pixels per sample.
                    if (region["MagnifierSize"] != null && region["MagnifierPixelCount"] != null)
                    {
                        if (region["MagnifierSize"].Type != JTokenType.Integer || region["MagnifierPixelCount"].Type != JTokenType.Integer ||
                            !long.TryParse(region["MagnifierSize"].ToString(), out long size) || size <= 0 || size > int.MaxValue ||
                            !long.TryParse(region["MagnifierPixelCount"].ToString(), out long pixels) || pixels <= 0 || pixels > int.MaxValue)
                            throw new System.IO.InvalidDataException("Region magnifier settings have an invalid type or value.");
                        region["MagnifierPixelSize"] = Math.Clamp((int)Math.Round((double)size / pixels), 3, 30);
                        region.Remove("MagnifierSize");
                        Warn(path + ".CaptureSettings.RegionCaptureOptions.MagnifierSize: approximated using SunShot's magnifier pixel size.");
                    }
                }
                if (capture["ScreenRecordStartDelayEnabled"] != null)
                {
                    if (capture["ScreenRecordStartDelayEnabled"].Type != JTokenType.Boolean)
                        throw new System.IO.InvalidDataException("Recording start delay has an invalid type.");
                    if (!(bool)capture["ScreenRecordStartDelayEnabled"]) capture["ScreenRecordStartDelay"] = 0;
                    capture.Remove("ScreenRecordStartDelayEnabled");
                }
                capture["ScreenRecordAutoStart"] = false;
                Warn(path + ".CaptureSettings: recording requires manual start; native 22 audio/camera settings are not converted to FFmpeg devices. Review recording options.");
            }
            if (task["ToolsSettings"] is JObject tools)
            {
                if (tools["ScreenColorPickerOptions"] is JObject picker)
                {
                    Copy(picker, "Format", tools, "ScreenColorPickerFormat");
                    Copy(picker, "FormatCtrl", tools, "ScreenColorPickerFormatCtrl");
                    Copy(picker, "InfoText", tools, "ScreenColorPickerInfoText");
                    foreach (string mapped in new[] { "Format", "FormatCtrl", "InfoText" }) picker.Remove(mapped);
                    if (picker.HasValues) Warn(path + ".ToolsSettings.ScreenColorPickerOptions: display options ignored; format strings retained.");
                    tools.Remove("ScreenColorPickerOptions");
                }
                if (tools["ColorPickerOptions"] is JToken colors)
                {
                    JObject surface = (task["CaptureSettings"] as JObject)?["SurfaceOptions"] as JObject;
                    if (surface != null) { surface["ColorPickerOptions"] = colors.DeepClone(); tools.Remove("ColorPickerOptions"); }
                    else Warn(path + ".ToolsSettings.ColorPickerOptions: picker UI preferences ignored (no capture section).");
                }
            }

            bool blocked = false;
            foreach (var destination in new[] {
                ("ImageDestination", typeof(ImageDestination)), ("TextDestination", typeof(TextDestination)),
                ("ImageFileDestination", typeof(FileDestination)), ("TextFileDestination", typeof(FileDestination)), ("FileDestination", typeof(FileDestination)) })
            {
                if (task[destination.Item1] != null && !KnownEnum(task[destination.Item1], destination.Item2))
                {
                    Warn(path + "." + destination.Item1 + ": unsupported destination; this workflow's upload destinations are blocked until configured. No fallback uploader will be used.");
                    blocked = true;
                }
            }
            if (task["UploadSettings"] is JObject uploadSettings && uploadSettings["UploaderFilters"] is JArray filters)
            {
                foreach (JObject filter in filters.OfType<JObject>())
                {
                    string identifier = (string)filter["Uploader"];
                    if (identifier != null && !UploaderFactory.AllGenericUploaderServices.Any(x => x.ServiceIdentifier.Equals(identifier, StringComparison.OrdinalIgnoreCase)))
                    {
                        blocked = true;
                        Warn(path + ".UploadSettings.UploaderFilters: unsupported provider filter; this workflow's destinations are blocked until configured.");
                    }
                }
                if (blocked) filters.Clear();
            }
            if (blocked)
            {
                JObject effectiveUpload = ((bool?)task["UseDefaultUploadSettings"] != false && !ReferenceEquals(task, defaultTask)
                    ? defaultTask?["UploadSettings"] : task["UploadSettings"]) as JObject;
                task["UploadSettings"] = effectiveUpload?.DeepClone() ?? new JObject();
                task["UploadSettings"]["UploaderFilters"] = new JArray();
                task["UseDefaultUploadSettings"] = false;
                task["UseDefaultDestinations"] = false;
                task["ImageDestination"] = "CustomImageUploader";
                task["TextDestination"] = "CustomTextUploader";
                foreach (string field in new[] { "ImageFileDestination", "TextFileDestination", "FileDestination" }) task[field] = "CustomFileUploader";
                task["OverrideCustomUploader"] = true;
                blockedTasks.Add(task);
            }
        }

        internal void Finish(UploadersConfig uploaders)
        {
            if (blockedTasks.Count == 0) return;
            uploaders.CustomUploadersList ??= new List<CustomUploaderItem>();
            int index = uploaders.CustomUploadersList.Count;
            CustomUploaderItem placeholder = CustomUploaderItem.Init();
            placeholder.Name = "Unsupported ShareX 22 destination - configure before uploading";
            placeholder.RequestURL = "";
            placeholder.DestinationType = CustomUploaderDestinationType.ImageUploader | CustomUploaderDestinationType.TextUploader | CustomUploaderDestinationType.FileUploader;
            placeholder.Body = CustomUploaderBody.MultipartFormData;
            uploaders.CustomUploadersList.Add(placeholder);
            foreach (JObject task in blockedTasks) task["CustomUploaderIndex"] = index;
        }

        internal void BlockInheritedDestinations(ApplicationConfig app, HotkeysConfig hotkeys)
        {
            if (defaultTask == null || !blockedTasks.Contains(defaultTask)) return;
            foreach (TaskSettings task in hotkeys.Hotkeys.Select(x => x.TaskSettings)
                .Where(x => x.UseDefaultDestinations || (defaultHadFilters && x.UseDefaultUploadSettings)))
            {
                task.UseDefaultDestinations = false;
                task.ImageDestination = ImageDestination.CustomImageUploader;
                task.TextDestination = TextDestination.CustomTextUploader;
                task.ImageFileDestination = task.TextFileDestination = task.FileDestination = FileDestination.CustomFileUploader;
                task.OverrideCustomUploader = true;
                task.CustomUploaderIndex = app.DefaultTaskSettings.CustomUploaderIndex;
                task.UploadSettings = (task.UseDefaultUploadSettings ? app.DefaultTaskSettings.UploadSettings : task.UploadSettings) ?? new TaskSettingsUpload();
                task.UploadSettings.UploaderFilters = new List<UploaderFilter>();
                task.UseDefaultUploadSettings = false;
            }
        }

        // Check every incoming property, including list entries, so Newtonsoft's usual
        // unknown-member behavior cannot quietly lose 22-only preferences.
        private void Normalize(JToken token, Type type, string path)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (token is JObject effect && effect["$type"]?.Type == JTokenType.String)
            {
                string qualified = (string)effect["$type"];
                int comma = qualified.IndexOf(',');
                type = serializer.SerializationBinder.BindToType(comma < 0 ? null : qualified.Substring(comma + 1).Trim(),
                    (comma < 0 ? qualified : qualified.Substring(0, comma)).Trim()) ?? type;
            }
            JsonContract contract = serializer.ContractResolver.ResolveContract(type);
            if (token is JObject obj && contract is JsonObjectContract objectContract)
            {
                foreach (JProperty property in obj.Properties().ToList())
                {
                    if (property.Name == "$type") continue; // already checked by the strict image-effect binder
                    JsonProperty member = objectContract.Properties.GetClosestMatchProperty(property.Name);
                    if (member == null || member.Ignored || !member.Writable)
                    {
                        Warn(path + "." + SafeName(property.Name) + ": unsupported setting ignored (SunShot default retained where applicable).");
                        property.Remove();
                        continue;
                    }
                    Type memberType = Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType;
                    string location = path + "." + member.PropertyName;
                    if (memberType.IsEnum && !KnownEnum(property.Value, memberType))
                    {
                        if (property.Value.Type != JTokenType.String && property.Value.Type != JTokenType.Integer)
                            throw new System.IO.InvalidDataException("A settings enum has an invalid JSON type.");
                        bool flags = memberType.IsDefined(typeof(FlagsAttribute), false);
                        string[] names = property.Value.Type == JTokenType.String ? ((string)property.Value).Split(',').Select(x => x.Trim()).ToArray() : Array.Empty<string>();
                        string[] retained = flags ? names.Where(x => Enum.GetNames(memberType).Contains(x)).ToArray() : Array.Empty<string>();
                        Warn(location + ": unsupported " + (property.Value.Type == JTokenType.Integer ? "numeric enum" : "enum value") +
                            (flags ? "; unsupported actions removed." : "; reset to SunShot default."));
                        // Jobs must become None, never a constructor's capture or upload action.
                        if (memberType == typeof(HotkeyType) || memberType == typeof(System.Windows.Forms.Keys)) property.Value = "None";
                        else if (flags) property.Value = retained.Length > 0 ? string.Join(", ", retained) : "0";
                        else property.Remove();
                    }
                    else Normalize(property.Value, member.PropertyType, location);
                }
            }
            else if (token is JArray array && contract is JsonArrayContract listContract)
            {
                for (int i = array.Count - 1; i >= 0; i--)
                {
                    if (listContract.CollectionItemType.IsEnum && !KnownEnum(array[i], listContract.CollectionItemType))
                    {
                        Warn(path + $"[{i + 1}]: unsupported enum entry removed.");
                        array.RemoveAt(i);
                    }
                    else Normalize(array[i], listContract.CollectionItemType, path + $"[{i + 1}]");
                }
            }
            // Dictionary keys and string values are user data, not schema fields.
        }

        private static bool KnownEnum(JToken token, Type type)
        {
            // InputKey deliberately retains Windows virtual-key values, unlike job ordinals.
            if (type == typeof(System.Windows.Forms.Keys) && token.Type == JTokenType.Integer)
            {
                long key = (long)token;
                return key >= 0 && key <= int.MaxValue && (key & ~0x7ffffL) == 0 &&
                    Enum.IsDefined(type, (int)(key & 0xffff));
            }
            if (token.Type != JTokenType.String) return false; // enum ordinals differ across 22 releases
            string[] names = ((string)token).Split(',').Select(x => x.Trim()).ToArray();
            if (!type.IsDefined(typeof(FlagsAttribute), false) && names.Length != 1) return false;
            return names.All(x => Enum.GetNames(type).Contains(x));
        }

        private static void Rename(JObject json, string source, string target)
        {
            if (json.Property(source) == null) return;
            if (json.Property(target) != null) throw new System.IO.InvalidDataException("Settings contain conflicting old and new field names.");
            json[target] = json[source];
            json.Remove(source);
        }

        private static void Copy(JObject source, string name, JObject target, string destination)
        {
            if (source[name] != null) target[destination] = source[name].DeepClone();
        }

        private static string SafeName(string name) => name.Length <= 64 && name.All(x => char.IsAsciiLetterOrDigit(x) || x == '_') ? name : "[unrecognized field]";
        private void Warn(string message) { if (!warnings.Contains(message)) warnings.Add(message); }
    }
}
