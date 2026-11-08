#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using ShareX.HistoryLib;
using ShareX.Properties;
using ShareX.ScreenCaptureLib;
using ShareX.UploadersLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShareX
{
    internal static class SettingManager
    {
        private const string ApplicationConfigFileName = "ApplicationConfig.json";

        private static string ApplicationConfigFilePath
        {
            get
            {
                if (Program.Sandbox) return null;

                return Path.Combine(Program.PersonalFolder, ApplicationConfigFileName);
            }
        }

        private const string UploadersConfigFileNamePrefix = "UploadersConfig";
        private const string UploadersConfigFileNameExtension = "json";
        private const string UploadersConfigFileName = UploadersConfigFileNamePrefix + "." + UploadersConfigFileNameExtension;

        private static string UploadersConfigFilePath
        {
            get
            {
                if (Program.Sandbox) return null;

                string uploadersConfigFolder;

                if (Settings != null && !string.IsNullOrEmpty(Settings.CustomUploadersConfigPath))
                {
                    uploadersConfigFolder = FileHelpers.ExpandFolderVariables(Settings.CustomUploadersConfigPath);
                }
                else
                {
                    uploadersConfigFolder = Program.PersonalFolder;
                }

                string uploadersConfigFileName = GetUploadersConfigFileName(uploadersConfigFolder);

                return Path.Combine(uploadersConfigFolder, uploadersConfigFileName);
            }
        }

        private const string HotkeysConfigFileName = "HotkeysConfig.json";

        private static string HotkeysConfigFilePath
        {
            get
            {
                if (Program.Sandbox) return null;

                string hotkeysConfigFolder;

                if (Settings != null && !string.IsNullOrEmpty(Settings.CustomHotkeysConfigPath))
                {
                    hotkeysConfigFolder = FileHelpers.ExpandFolderVariables(Settings.CustomHotkeysConfigPath);
                }
                else
                {
                    hotkeysConfigFolder = Program.PersonalFolder;
                }

                return Path.Combine(hotkeysConfigFolder, HotkeysConfigFileName);
            }
        }

        public static string BackupFolder => Path.Combine(Program.PersonalFolder, "Backup");

        private static ApplicationConfig Settings { get => Program.Settings; set => Program.Settings = value; }
        private static TaskSettings DefaultTaskSettings { get => Program.DefaultTaskSettings; set => Program.DefaultTaskSettings = value; }
        private static UploadersConfig UploadersConfig { get => Program.UploadersConfig; set => Program.UploadersConfig = value; }
        private static HotkeysConfig HotkeysConfig { get => Program.HotkeysConfig; set => Program.HotkeysConfig = value; }

        private static ManualResetEvent uploadersConfigResetEvent = new ManualResetEvent(false);
        private static ManualResetEvent hotkeysConfigResetEvent = new ManualResetEvent(false);

        public static void LoadInitialSettings()
        {
            LoadApplicationConfig();

            Task.Run(() =>
            {
                LoadUploadersConfig();
                uploadersConfigResetEvent.Set();

                LoadHotkeysConfig();
                hotkeysConfigResetEvent.Set();
            });
        }

        public static void WaitUploadersConfig()
        {
            if (UploadersConfig == null)
            {
                uploadersConfigResetEvent.WaitOne();
            }
        }

        public static void WaitHotkeysConfig()
        {
            if (HotkeysConfig == null)
            {
                hotkeysConfigResetEvent.WaitOne();
            }
        }

        public static void LoadApplicationConfig(bool fallbackSupport = true)
        {
            Settings = ApplicationConfig.Load(ApplicationConfigFilePath, BackupFolder, fallbackSupport);
            Settings.CreateBackup = true;
            Settings.CreateWeeklyBackup = true;
            Settings.SettingsSaveFailed += Settings_SettingsSaveFailed;
            DefaultTaskSettings = Settings.DefaultTaskSettings;
            ApplicationConfigBackwardCompatibilityTasks();
            MigrateHistoryFile();
            HistoryConnect();
        }

        private static void Settings_SettingsSaveFailed(Exception e)
        {
            string message;

            if (e is UnauthorizedAccessException || e is FileNotFoundException)
            {
                message = Resources.YourAntiVirusSoftwareOrTheControlledFolderAccessFeatureInWindowsCouldBeBlockingShareX;
            }
            else
            {
                message = e.Message;
            }

            TaskHelpers.ShowNotificationTip(message, Program.AppName + " - " + Resources.FailedToSaveSettings, 5000);
        }

        public static void LoadUploadersConfig(bool fallbackSupport = true)
        {
            UploadersConfig = UploadersConfig.Load(UploadersConfigFilePath, BackupFolder, fallbackSupport);
            UploadersConfig.CreateBackup = true;
            UploadersConfig.CreateWeeklyBackup = true;
            UploadersConfig.SupportDPAPIEncryption = true;
            UploadersConfigBackwardCompatibilityTasks();
        }

        public static void LoadHotkeysConfig(bool fallbackSupport = true)
        {
            HotkeysConfig = HotkeysConfig.Load(HotkeysConfigFilePath, BackupFolder, fallbackSupport);
            HotkeysConfig.CreateBackup = true;
            HotkeysConfig.CreateWeeklyBackup = true;
            HotkeysConfigBackwardCompatibilityTasks();
        }

        public static void LoadAllSettings()
        {
            LoadApplicationConfig();
            LoadUploadersConfig();
            LoadHotkeysConfig();
        }

        private static string GetUploadersConfigFileName(string destinationFolder)
        {
            if (string.IsNullOrEmpty(destinationFolder))
            {
                return UploadersConfigFileName;
            }

            if (Settings != null && Settings.UseMachineSpecificUploadersConfig)
            {
                string sanitizedMachineName = FileHelpers.SanitizeFileName(Environment.MachineName.ToLowerInvariant());

                if (!string.IsNullOrEmpty(sanitizedMachineName))
                {
                    string machineSpecificFileName = $"{UploadersConfigFileNamePrefix}-{sanitizedMachineName}.{UploadersConfigFileNameExtension}";
                    string machineSpecificPath = Path.Combine(destinationFolder, machineSpecificFileName);

                    if (!File.Exists(machineSpecificPath))
                    {
                        string defaultFilePath = Path.Combine(destinationFolder, UploadersConfigFileName);

                        if (File.Exists(defaultFilePath))
                        {
                            try
                            {
                                File.Copy(defaultFilePath, machineSpecificPath, false);
                            }
                            catch (IOException)
                            {
                                // Ignore copy issues; file may have been created in the meantime.
                            }
                        }
                    }

                    return machineSpecificFileName;
                }
            }

            return UploadersConfigFileName;
        }

        private static void ApplicationConfigBackwardCompatibilityTasks()
        {
            if (SystemOptions.DisableUpload)
            {
                DefaultTaskSettings.AfterCaptureJob = DefaultTaskSettings.AfterCaptureJob.Remove(AfterCaptureTasks.UploadImageToHost);
            }

        }

        public static void HistoryConnect()
        {
            HistoryClose();
            Program.HistoryManager = new HistoryManagerSQLite(Program.HistoryFilePath);
        }

        public static void HistoryClose()
        {
            if (Program.HistoryManager != null)
            {
                Program.HistoryManager.Dispose();
                Program.HistoryManager = null;
            }
        }

        private static void MigrateHistoryFile()
        {
            if (File.Exists(Program.HistoryFilePathOld))
            {
                try
                {
                    if (!File.Exists(Program.HistoryFilePath))
                    {
                        DebugHelper.WriteLine($"Migrating JSON history file \"{Program.HistoryFilePathOld}\" to SQLite history file \"{Program.HistoryFilePath}\"");

                        using (HistoryManagerSQLite historyManager = new HistoryManagerSQLite(Program.HistoryFilePath))
                        {
                            historyManager.MigrateFromJSON(Program.HistoryFilePathOld);
                        }
                    }

                    FileHelpers.MoveFile(Program.HistoryFilePathOld, BackupFolder);
                }
                catch (Exception e)
                {
                    DebugHelper.WriteException(e);
                    e.ShowError();
                }
            }
        }

        private static void UploadersConfigBackwardCompatibilityTasks()
        {
            if (UploadersConfig.CustomUploadersList != null)
            {
                foreach (CustomUploaderItem cui in UploadersConfig.CustomUploadersList)
                {
                    try
                    {
                        cui.CheckBackwardCompatibility();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void HotkeysConfigBackwardCompatibilityTasks()
        {
            if (SystemOptions.DisableUpload)
            {
                foreach (TaskSettings taskSettings in HotkeysConfig.Hotkeys.Select(x => x.TaskSettings))
                {
                    if (taskSettings != null)
                    {
                        taskSettings.AfterCaptureJob = taskSettings.AfterCaptureJob.Remove(AfterCaptureTasks.UploadImageToHost);
                    }
                }
            }

        }

        public static void CleanupHotkeysConfig()
        {
            foreach (TaskSettings taskSettings in HotkeysConfig.Hotkeys.Select(x => x.TaskSettings))
            {
                taskSettings.Cleanup();
            }
        }

        private static readonly object saveLock = new object();
        private static bool savesSuspended;

        public static void SaveAllSettings()
        {
            lock (saveLock)
            {
                if (savesSuspended) return;
                SaveApplicationConfig();
                SaveUploadersConfig();
                SaveHotkeysConfig();
            }
        }

        private static void SaveApplicationConfig()
        {
            lock (saveLock)
            {
                if (!savesSuspended && Settings != null) Settings.Save(ApplicationConfigFilePath);
            }
        }

        private static void SaveUploadersConfig()
        {
            lock (saveLock)
            {
                if (!savesSuspended && UploadersConfig != null) UploadersConfig.Save(UploadersConfigFilePath);
            }
        }

        private static void SaveHotkeysConfig()
        {
            lock (saveLock)
            {
                if (!savesSuspended && HotkeysConfig != null)
                {
                    CleanupHotkeysConfig();
                    HotkeysConfig.Save(HotkeysConfigFilePath);
                }
            }
        }

        public static void SaveApplicationConfigAsync() => Task.Run(SaveApplicationConfig);
        public static void SaveUploadersConfigAsync() => Task.Run(SaveUploadersConfig);
        public static void SaveHotkeysConfigAsync() => Task.Run(SaveHotkeysConfig);
        public static void SaveAllSettingsAsync() => Task.Run(SaveAllSettings);

        public static void ValidateImportDestination()
        {
            lock (saveLock) ValidateImportDestinationCore();
        }

        private static void ValidateImportDestinationCore()
        {
            if (!string.IsNullOrWhiteSpace(Settings.CustomHotkeysConfigPath) || !string.IsNullOrWhiteSpace(Settings.CustomUploadersConfigPath))
                throw new InvalidDataException("SunShot currently uses external hotkey or uploader settings. Reset those config folders to SunShot's own profile in Application settings before importing, so the source files cannot be overwritten.");
            string root = Path.GetFullPath(Program.PersonalFolder);
            foreach (string path in new[] { root, BackupFolder, ApplicationConfigFilePath, HotkeysConfigFilePath, UploadersConfigFilePath })
            {
                for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                {
                    if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        FileSystemInfo entry = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                        if (entry.LinkTarget != null)
                            throw new InvalidDataException("SunShot profile files cannot be symbolic links or junctions during an import. Use a separate regular profile folder.");
                    }
                }
            }
            foreach (string path in new[] { ApplicationConfigFilePath, HotkeysConfigFilePath, UploadersConfigFilePath })
            {
                ShareXSettingsImportService.ValidateRegularTarget(path);
                ShareXSettingsImportService.ValidateRegularTarget(Path.Combine(BackupFolder, Path.GetFileName(path)));
                string temporary = path + ".temp";
                ShareXSettingsImportService.ValidateRegularTarget(temporary);
                if (File.Exists(temporary))
                    throw new InvalidDataException("A temporary SunShot settings file already exists. Close SunShot and move the .temp settings file out of its profile before retrying the import.");
            }
        }

        public static void SuspendSavesForImport()
        {
            WaitUploadersConfig();
            WaitHotkeysConfig();
            lock (saveLock)
            {
                // Drain pending saves and persist the current profile before making its backup.
                ValidateImportDestinationCore();
                SaveAllSettings();
                savesSuspended = true;
            }
        }

        public static void ResumeSavesAfterFailedImport()
        {
            lock (saveLock) savesSuspended = false;
        }

        public static void ResetSettings()
        {
            if (File.Exists(ApplicationConfigFilePath)) File.Delete(ApplicationConfigFilePath);
            LoadApplicationConfig(false);

            if (File.Exists(UploadersConfigFilePath)) File.Delete(UploadersConfigFilePath);
            LoadUploadersConfig(false);

            if (File.Exists(HotkeysConfigFilePath)) File.Delete(HotkeysConfigFilePath);
            LoadHotkeysConfig(false);
        }

        public static bool Export(string archivePath, bool settings, bool history)
        {
            MemoryStream msApplicationConfig = null, msUploadersConfig = null, msHotkeysConfig = null;

            try
            {
                List<ZipEntryInfo> entries = new List<ZipEntryInfo>();

                if (settings)
                {
                    msApplicationConfig = Settings.SaveToMemoryStream(false);
                    entries.Add(new ZipEntryInfo(msApplicationConfig, ApplicationConfigFileName));

                    msUploadersConfig = UploadersConfig.SaveToMemoryStream(false);
                    entries.Add(new ZipEntryInfo(msUploadersConfig, UploadersConfigFileName));

                    msHotkeysConfig = HotkeysConfig.SaveToMemoryStream(false);
                    entries.Add(new ZipEntryInfo(msHotkeysConfig, HotkeysConfigFileName));
                }

                if (history)
                {
                    entries.Add(new ZipEntryInfo(Program.HistoryFilePath));
                    HistoryClose();
                }

                ZipManager.Compress(archivePath, entries);
                return true;
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
                MessageBox.Show("Error while exporting backup:\r\n" + e, Program.AppName + " - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                msApplicationConfig?.Dispose();
                msUploadersConfig?.Dispose();
                msHotkeysConfig?.Dispose();

                if (history)
                {
                    HistoryConnect();
                }
            }

            return false;
        }

    }
}
