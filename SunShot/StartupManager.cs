﻿#region License Information (GPL v3)

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

using Microsoft.Win32;
using ShareX.HelpersLib;
using System;
using System.IO;
using System.Windows.Forms;

#if MicrosoftStore
using Windows.ApplicationModel;
#endif

namespace ShareX
{
    public static class StartupManager
    {
#if MicrosoftStore
        private const int StartupTargetIndex = 0;
        private static readonly StartupTask packageTask = StartupTask.GetForCurrentPackageAsync().GetAwaiter().GetResult()[StartupTargetIndex];
#else
        private const string StartupApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
        private const string DefaultAppliedValue = "StartupDefaultApplied";

        // Portable copies get their own name so they never replace or remove the installed copy's startup entry.
        private static string ShortcutFileName => Program.Portable ? Program.AppName + " Portable.lnk" : Program.AppName + ".lnk";
        private static string ShortcutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutFileName);

        private static string StartupArguments => Program.PortableFromCommandLine ? "-silent -portable" : "-silent";
#endif

        public static string StartupTargetPath
        {
            get
            {
#if STEAM
                return FileHelpers.GetAbsolutePath("../SunShot_Launcher.exe");
#else
                return Application.ExecutablePath;
#endif
            }
        }

        public static StartupState State
        {
            get
            {
#if MicrosoftStore
                return (StartupState)packageTask.State;
#else
                if (!ShortcutHelpers.CheckShortcut(ShortcutPath, StartupTargetPath))
                {
                    return StartupState.Disabled;
                }

                return IsDisabledInWindows() ? StartupState.DisabledByUser : StartupState.Enabled;
#endif
            }
            set
            {
#if MicrosoftStore
                if (value == StartupState.Enabled)
                {
                    packageTask.RequestEnableAsync().GetAwaiter().GetResult();
                }
                else if (value == StartupState.Disabled)
                {
                    packageTask.Disable();
                }
                else
                {
                    throw new NotSupportedException();
                }
#else
                if (value == StartupState.Enabled)
                {
                    if (!File.Exists(ShortcutPath) && IsDisabledInWindows())
                    {
                        // A leftover "disabled" entry from a deleted shortcut would silently block the new one.
                        RemoveWindowsApproval();
                    }

                    ShortcutHelpers.SetShortcut(true, ShortcutPath, StartupTargetPath, StartupArguments);

                    if (!ShortcutHelpers.CheckShortcut(ShortcutPath, StartupTargetPath))
                    {
                        throw new IOException("Could not create the startup shortcut: " + ShortcutPath);
                    }
                }
                else if (value == StartupState.Disabled)
                {
                    // Only remove a shortcut that starts this copy.
                    if (ShortcutHelpers.CheckShortcut(ShortcutPath, StartupTargetPath))
                    {
                        ShortcutHelpers.SetShortcut(false, ShortcutPath, StartupTargetPath);
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }
#endif
            }
        }

        /// <summary>
        /// Turns on start with Windows once for a new installed (non portable) user. Upgrades and later choices are left alone.
        /// </summary>
        public static void ApplyFirstRunDefault()
        {
#if !STEAM && !MicrosoftStore
            if (Program.Portable && !Program.Sandbox)
            {
                MigratePortableShortcut();
            }

            if (Program.Portable || Program.Sandbox || Program.MultiInstance)
            {
                return;
            }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Program.StateRegistryPath))
                {
                    if (key.GetValue(DefaultAppliedValue) != null)
                    {
                        return;
                    }

                    key.SetValue(DefaultAppliedValue, 1, RegistryValueKind.DWord);
                }

                if (Program.Settings.IsFirstTimeRun && !File.Exists(ShortcutPath) && !IsDisabledInWindows())
                {
                    State = StartupState.Enabled;
                    DebugHelper.WriteLine("Start with Windows turned on for first run.");
                }
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
            }
#endif
        }

#if !STEAM && !MicrosoftStore
        // Portable 0.1.2 and older used the same shortcut name as the installed copy. Move that shortcut, and the
        // Windows enabled or disabled state that goes with it, to the portable name when it starts this exe.
        private static void MigratePortableShortcut()
        {
            try
            {
                string legacyName = Program.AppName + ".lnk";
                string legacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), legacyName);

                if (File.Exists(ShortcutPath) || !ShortcutHelpers.CheckShortcut(legacyPath, StartupTargetPath))
                {
                    return;
                }

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(StartupApprovedPath, true))
                {
                    object previous = key.GetValue(ShortcutFileName);

                    // Copy the state first (or clear a stale one), then move the file, so a failure leaves the old setup working.
                    if (key.GetValue(legacyName) is byte[] status)
                    {
                        key.SetValue(ShortcutFileName, status, RegistryValueKind.Binary);
                    }
                    else
                    {
                        key.DeleteValue(ShortcutFileName, false);
                    }

                    try
                    {
                        File.Move(legacyPath, ShortcutPath);
                    }
                    catch
                    {
                        if (previous is byte[] previousStatus)
                        {
                            key.SetValue(ShortcutFileName, previousStatus, RegistryValueKind.Binary);
                        }
                        else
                        {
                            key.DeleteValue(ShortcutFileName, false);
                        }

                        throw;
                    }

                    key.DeleteValue(legacyName, false);
                }

                DebugHelper.WriteLine("Moved the portable startup shortcut to " + ShortcutFileName);
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
            }
        }
#endif

#if !MicrosoftStore
        private static bool IsDisabledInWindows()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupApprovedPath))
            {
                // Byte 0: even = enabled, odd = disabled in Task Manager or Settings > Apps > Startup.
                return key?.GetValue(ShortcutFileName) is byte[] status && status.Length > 0 && (status[0] & 1) == 1;
            }
        }

        private static void RemoveWindowsApproval()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupApprovedPath, true))
            {
                key?.DeleteValue(ShortcutFileName, false);
            }
        }
#endif
    }
}
