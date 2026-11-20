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
using ShareX.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ShareX
{
    public class HotkeyManager
    {
        public List<HotkeySettings> Hotkeys { get; private set; }
        private bool ignoreHotkeys;

        public bool IgnoreHotkeys
        {
            get => ignoreHotkeys;
            set
            {
                ignoreHotkeys = value;
                RefreshPrintScreenHotkey();
            }
        }

        public delegate void HotkeyTriggerEventHandler(HotkeySettings hotkeySetting);
        public delegate void HotkeysToggledEventHandler(bool hotkeysEnabled);

        public HotkeyTriggerEventHandler HotkeyTrigger;
        public HotkeysToggledEventHandler HotkeysToggledTrigger;

        private HotkeyForm hotkeyForm;
        private PrintScreenKeyHook printScreenKeyHook;
        private Action printScreenEditing;

        // Set while a hotkey box is waiting for a key, so a bare Print Screen press reaches it instead of Windows.
        public Action PrintScreenEditing
        {
            get => printScreenEditing;
            set
            {
                printScreenEditing = value;
                RefreshPrintScreenHotkey();
            }
        }

        public bool IsPrintScreenKeyHooked => printScreenKeyHook != null && printScreenKeyHook.IsRunning;

        public bool HasBarePrintScreenHotkey => GetBarePrintScreenHotkey() != null;

        public HotkeyManager(HotkeyForm form)
        {
            hotkeyForm = form;
            hotkeyForm.HotkeyPress += HotkeyForm_HotkeyPress;
            hotkeyForm.FormClosed += HotkeyForm_FormClosed;
        }

        private void PrintScreenKeyHook_Pressed(ushort id)
        {
            HotkeySettings hotkeySetting = GetBarePrintScreenHotkey();

            // The press was queued on the hook thread, so make sure the same binding is still active.
            if (hotkeySetting != null && hotkeySetting.HotkeyInfo.ID == id && hotkeyForm.CheckRepeatLimitTime())
            {
                HotkeyForm_HotkeyPress(id, Keys.PrintScreen, Modifiers.None);
            }
        }

        private void HotkeyForm_HotkeyPress(ushort id, Keys key, Modifiers modifier)
        {
            if (!IgnoreHotkeys && (!Program.Settings.DisableHotkeysOnFullscreen || !CaptureHelpers.IsActiveWindowFullscreen()))
            {
                HotkeySettings hotkeySetting = Hotkeys.Find(x => x.HotkeyInfo.ID == id);

                if (hotkeySetting != null)
                {
                    OnHotkeyTrigger(hotkeySetting);
                }
            }
        }

        private void HotkeyForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (hotkeyForm != null && !hotkeyForm.IsDisposed)
            {
                UnregisterAllHotkeys(false);
            }

            StopPrintScreenKeyHook();
        }

        public void UpdateHotkeys(List<HotkeySettings> hotkeys, bool showFailedHotkeys)
        {
            if (Hotkeys != null)
            {
                UnregisterAllHotkeys();
            }

            Hotkeys = hotkeys;

            RegisterAllHotkeys();
            UpdatePrintScreenKey();

            if (showFailedHotkeys)
            {
                ShowFailedHotkeys();
            }
        }

        protected void OnHotkeyTrigger(HotkeySettings hotkeySetting)
        {
            HotkeyTrigger?.Invoke(hotkeySetting);
        }

        public void RegisterHotkey(HotkeySettings hotkeySetting)
        {
            if (!Program.Settings.DisableHotkeys || hotkeySetting.TaskSettings.Job == HotkeyType.DisableHotkeys)
            {
                UnregisterHotkey(hotkeySetting, false);

                if (hotkeySetting.HotkeyInfo.Status != HotkeyStatus.Registered && hotkeySetting.HotkeyInfo.IsValidHotkey)
                {
                    hotkeyForm.RegisterHotkey(hotkeySetting.HotkeyInfo);

                    if (hotkeySetting.HotkeyInfo.Status == HotkeyStatus.Registered)
                    {
                        DebugHelper.WriteLine("Hotkey registered: " + hotkeySetting);
                    }
                    else if (hotkeySetting.HotkeyInfo.Status == HotkeyStatus.Failed)
                    {
                        DebugHelper.WriteLine("Hotkey register failed: " + hotkeySetting);
                    }
                }
                else
                {
                    hotkeySetting.HotkeyInfo.Status = HotkeyStatus.NotConfigured;
                }
            }

            if (!Hotkeys.Contains(hotkeySetting))
            {
                Hotkeys.Add(hotkeySetting);
            }

            RefreshPrintScreenHotkey();
        }

        public void RegisterAllHotkeys()
        {
            foreach (HotkeySettings hotkeySetting in Hotkeys.ToArray())
            {
                RegisterHotkey(hotkeySetting);
            }
        }

        public void RegisterFailedHotkeys()
        {
            foreach (HotkeySettings hotkeySetting in Hotkeys.Where(x => x.HotkeyInfo.Status == HotkeyStatus.Failed))
            {
                RegisterHotkey(hotkeySetting);
            }
        }

        public void UnregisterHotkey(HotkeySettings hotkeySetting, bool removeFromList = true)
        {
            if (hotkeySetting.HotkeyInfo.Status == HotkeyStatus.Registered)
            {
                hotkeyForm.UnregisterHotkey(hotkeySetting.HotkeyInfo);

                if (hotkeySetting.HotkeyInfo.Status == HotkeyStatus.NotConfigured)
                {
                    DebugHelper.WriteLine("Hotkey unregistered: " + hotkeySetting);
                }
                else if (hotkeySetting.HotkeyInfo.Status == HotkeyStatus.Failed)
                {
                    DebugHelper.WriteLine("Hotkey unregister failed: " + hotkeySetting);
                }
            }

            if (removeFromList)
            {
                Hotkeys.Remove(hotkeySetting);
            }

            RefreshPrintScreenHotkey();
        }

        public void UnregisterAllHotkeys(bool removeFromList = true, bool temporary = false)
        {
            if (Hotkeys != null)
            {
                foreach (HotkeySettings hotkeySetting in Hotkeys.ToArray())
                {
                    if (!temporary || hotkeySetting.TaskSettings.Job != HotkeyType.DisableHotkeys)
                    {
                        UnregisterHotkey(hotkeySetting, removeFromList);
                    }
                }
            }
        }

        public void ToggleHotkeys(bool hotkeysDisabled)
        {
            if (!hotkeysDisabled)
            {
                RegisterAllHotkeys();
            }
            else
            {
                UnregisterAllHotkeys(false, true);
            }

            UpdatePrintScreenKey();
            HotkeysToggledTrigger?.Invoke(hotkeysDisabled);
        }

        /// <summary>
        /// Starts or stops catching bare Print Screen before the Windows screen capture shortcut gets it.
        /// The registry can say the Windows shortcut is off before that applies (it needs a sign in), so the hook
        /// follows the SunShot option and only takes the key while a hotkey uses Print Screen alone.
        /// </summary>
        public void UpdatePrintScreenKey()
        {
            bool needed = Program.Settings.UsePrintScreenKey && !Program.Sandbox && hotkeyForm != null && !hotkeyForm.IsDisposed;

            if (needed && printScreenKeyHook == null)
            {
                printScreenKeyHook = new PrintScreenKeyHook(hotkeyForm, PrintScreenKeyHook_Pressed,
                    () => Program.Settings.DisableHotkeysOnFullscreen && CaptureHelpers.IsActiveWindowFullscreen());

                if (printScreenKeyHook.IsRunning)
                {
                    DebugHelper.WriteLine("Print Screen key hook started.");
                }
            }
            else if (!needed)
            {
                StopPrintScreenKeyHook();
            }

            RefreshPrintScreenHotkey();
        }

        private HotkeySettings GetBarePrintScreenHotkey()
        {
            return Hotkeys?.FirstOrDefault(x => x.HotkeyInfo.Status == HotkeyStatus.Registered &&
                x.HotkeyInfo.KeyCode == Keys.PrintScreen && x.HotkeyInfo.ModifiersEnum == Modifiers.None);
        }

        private void RefreshPrintScreenHotkey()
        {
            printScreenKeyHook?.SetTargets(GetBarePrintScreenHotkey()?.HotkeyInfo.ID ?? 0, printScreenEditing, ignoreHotkeys);
        }

        private void StopPrintScreenKeyHook()
        {
            if (printScreenKeyHook != null)
            {
                printScreenKeyHook.Dispose();
                printScreenKeyHook = null;
                DebugHelper.WriteLine("Print Screen key hook stopped.");
            }
        }

        public void ShowFailedHotkeys()
        {
            List<HotkeySettings> failedHotkeysList = Hotkeys.Where(x => x.HotkeyInfo.Status == HotkeyStatus.Failed).ToList();

            if (failedHotkeysList.Count > 0)
            {
                string failedHotkeys = string.Join("\r\n", failedHotkeysList.Select(x => $"[{x.HotkeyInfo}] {x.TaskSettings}"));
                string hotkeyText = failedHotkeysList.Count > 1 ? Resources.HotkeyManager_ShowFailedHotkeys_hotkeys : Resources.HotkeyManager_ShowFailedHotkeys_hotkey;
                string text = string.Format(Resources.HotkeyManager_ShowFailedHotkeys_Unable_to_register_hotkey, hotkeyText, failedHotkeys);

                MessageBox.Show(text, Program.AppName + " - " + Resources.HotkeyManager_ShowFailedHotkeys_Hotkey_registration_failed, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void ResetHotkeys()
        {
            UnregisterAllHotkeys();
            Hotkeys.AddRange(GetDefaultHotkeyList());
            RegisterAllHotkeys();

            if (Program.Settings.DisableHotkeys)
            {
                TaskHelpers.ToggleHotkeys();
            }
        }

        public static List<HotkeySettings> GetDefaultHotkeyList()
        {
            return new List<HotkeySettings>
            {
                new HotkeySettings(HotkeyType.RectangleRegion, Keys.Control | Keys.PrintScreen),
                new HotkeySettings(HotkeyType.PrintScreen, Keys.PrintScreen),
                new HotkeySettings(HotkeyType.ActiveWindow, Keys.Alt | Keys.PrintScreen),
                new HotkeySettings(HotkeyType.ScreenRecorder, Keys.Shift | Keys.PrintScreen),
                new HotkeySettings(HotkeyType.ScreenRecorderGIF, Keys.Control | Keys.Shift | Keys.PrintScreen)
            };
        }
    }
}