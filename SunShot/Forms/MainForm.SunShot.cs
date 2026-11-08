#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    SunShot is a fork of ShareX. Modifications copyright (c) 2026 SunSinD.

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
using ShareX.UploadersLib;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShareX
{
    public partial class MainForm
    {
        private static bool skipUploadConfirmation;

        private ToolStrip tsSunShot;
        private ToolStripButton tsbSunRegion, tsbSunRecord, tsbSunCopy, tsbSunSave, tsbSunEdit, tsbSunUpload, tsbImportShareXSettings;
        private ToolStripLabel tslSunAutoUpload;
        private Font sunBoldMenuFont;
        private bool sunShotLayoutUpdating;

        // Called right after InitializeComponent, before the theme is applied.
        private void InitializeSunShotControls()
        {
            tsSunShot = new ToolStrip()
            {
                Name = "tsSunShot",
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                CanOverflow = true,
                ShowItemToolTips = true,
                TabStop = true,
                Padding = new Padding(4, 2, 4, 2),
                AccessibleName = "SunShot actions",
                AccessibleRole = AccessibleRole.ToolBar
            };

            tsbSunRegion = CreateSunShotButton("tsbSunRegion", tsmiRectangle.Text, tsmiRectangle.Image, SunShotUI.Text("RegionTip"), tsmiRectangle_Click);
            tsbSunRecord = CreateSunShotButton("tsbSunRecord", tsmiScreenRecordingFFmpeg.Text, tsmiScreenRecordingFFmpeg.Image, SunShotUI.Text("RecordTip"), tsmiScreenRecordingFFmpeg_Click);
            tsbSunCopy = CreateSunShotButton("tsbSunCopy", tsmiCopy.Text, tsmiCopy.Image, SunShotUI.Text("CopyTip"), (s, e) => CopySelectedForSunShot());
            tsbSunSave = CreateSunShotButton("tsbSunSave", SunShotUI.Text("SaveAs"), Properties.Resources.disk_black, SunShotUI.Text("SaveAsTip"), (s, e) => SaveSelectedFileAs());
            tsbSunEdit = CreateSunShotButton("tsbSunEdit", tsmiEditSelectedFile.Text, tsmiEditSelectedFile.Image, SunShotUI.Text("EditTip"), tsmiEditSelectedFile_Click);

            string uploadText = tsmiUploadSelectedFile.Text;
            if (!uploadText.EndsWith("...")) uploadText += "...";
            tsbSunUpload = CreateSunShotButton("tsbSunUpload", uploadText, tsmiUploadSelectedFile.Image, SunShotUI.Text("UploadTip"), tsmiUploadSelectedFile_Click);

            tslSunAutoUpload = new ToolStripLabel()
            {
                Name = "tslSunAutoUpload",
                Alignment = ToolStripItemAlignment.Right,
                ToolTipText = SunShotUI.Text("AutoUploadTip"),
                AccessibleRole = AccessibleRole.StaticText
            };

            tsSunShot.Items.AddRange(new ToolStripItem[]
            {
                tsbSunRegion, tsbSunRecord, new ToolStripSeparator(), tsbSunCopy, tsbSunSave, tsbSunEdit, tsbSunUpload, tslSunAutoUpload
            });

            pMain.Controls.Add(tsSunShot);
            tsSunShot.SendToBack(); // Dock before the Fill controls so it stays on top of the list
            tsSunShot.SizeChanged += (s, e) => UpdateSunShotCompactMode();

            tsbImportShareXSettings = new ToolStripButton(SunShotUI.Text("Import"))
            {
                Name = "tsbImportShareXSettings",
                Image = Properties.Resources.drive_download,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleLeft
            };
            tsbImportShareXSettings.Click += tsbImportShareXSettings_Click;
            tsMain.Items.Insert(tsMain.Items.IndexOf(tssMain3), tsbImportShareXSettings);
        }

        private ToolStripButton CreateSunShotButton(string name, string text, Image image, string toolTip, EventHandler onClick)
        {
            ToolStripButton tsb = new ToolStripButton(text)
            {
                Name = name,
                Image = image,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                ToolTipText = toolTip,
                Tag = toolTip,
                AutoToolTip = false,
                Margin = new Padding(0, 1, 2, 2)
            };
            tsb.Click += onClick;
            return tsb;
        }

        // Called once all menus are built.
        private void ApplySunShotMenuSimplification()
        {
            foreach (ToolStripItem item in new ToolStripItem[]
            {
                // AI analysis
                tsmiAI, tsmiTrayAI, tsmiAnalyzeImage,
                // Utilities unrelated to capturing
                tsmiHashChecker, tsmiIndexFolder, tssTools4, tsmiClipboardViewer, tsmiBorderlessWindow, tsmiInspectWindow, tsmiMonitorTest,
                tsmiTrayHashChecker, tsmiTrayIndexFolder, tssTrayTools4, tsmiTrayClipboardViewer, tsmiTrayBorderlessWindow, tsmiTrayInspectWindow, tsmiTrayMonitorTest,
                // Links to the upstream ShareX project accounts (credit stays in About)
                tsbDonate, tsbX, tsbDiscord
            })
            {
                item.Visible = false;
            }

            if (SystemOptions.DisableUpload)
            {
                tsbSunUpload.Visible = false;
                tslSunAutoUpload.Visible = false;
            }
        }

        private void UpdateSunShotTheme()
        {
            if (tsSunShot == null) return;

            tsSunShot.Font = ShareXResources.Theme.MenuFont;
            tsSunShot.Renderer = new SunShotToolStripRenderer();
            tsSunShot.BackColor = ShareXResources.Theme.BackgroundColor;
            tsSunShot.ForeColor = ShareXResources.Theme.TextColor;

            // Old font is not disposed because the label may still reference it until the next state update.
            sunBoldMenuFont = new Font(ShareXResources.Theme.MenuFont, FontStyle.Bold);
            UpdateSunShotAfterCaptureState();
        }

        // When the window is narrow (or the language has long labels), buttons fall back to icon only instead of
        // dropping into the overflow menu. The label stays in the tooltip and in the accessible name.
        private void UpdateSunShotCompactMode()
        {
            if (tsSunShot == null || sunShotLayoutUpdating) return;

            sunShotLayoutUpdating = true;

            try
            {
                ToolStripButton[] captureButtons = new[] { tsbSunRegion, tsbSunRecord };
                ToolStripButton[] fileButtons = new[] { tsbSunCopy, tsbSunSave, tsbSunEdit, tsbSunUpload };
                int available = tsSunShot.DisplayRectangle.Width;

                SetSunShotIconOnly(captureButtons, false);
                SetSunShotIconOnly(fileButtons, false);

                if (GetSunShotItemsWidth() > available)
                {
                    SetSunShotIconOnly(captureButtons, true);

                    if (GetSunShotItemsWidth() > available)
                    {
                        SetSunShotIconOnly(fileButtons, true);
                    }
                }
            }
            finally
            {
                sunShotLayoutUpdating = false;
            }
        }

        private int GetSunShotItemsWidth()
        {
            return tsSunShot.Items.Cast<ToolStripItem>().Where(x => x.Available).Sum(x => x.GetPreferredSize(Size.Empty).Width + x.Margin.Horizontal);
        }

        private static void SetSunShotIconOnly(ToolStripButton[] buttons, bool iconOnly)
        {
            foreach (ToolStripButton tsb in buttons)
            {
                tsb.DisplayStyle = iconOnly ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.ImageAndText;
                string tip = tsb.Tag as string;
                tsb.ToolTipText = iconOnly ? tsb.Text.Replace("&", "") + " - " + tip : tip;
            }
        }

        private void UpdateSunShotAfterCaptureState()
        {
            if (tslSunAutoUpload == null) return;

            SunShotUI.UpdateAfterCaptureItems(tsddbAfterCaptureTasks.DropDownItems, Program.DefaultTaskSettings.AfterCaptureJob);
            SunShotUI.UpdateAfterCaptureItems(tsmiTrayAfterCaptureTasks.DropDownItems, Program.DefaultTaskSettings.AfterCaptureJob);

            bool autoUpload = Program.DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost);
            tslSunAutoUpload.Text = SunShotUI.Text(autoUpload ? "AutoUploadOn" : "AutoUploadOff");
            tslSunAutoUpload.Font = autoUpload && sunBoldMenuFont != null ? sunBoldMenuFont : ShareXResources.Theme.MenuFont;

            UpdateSunShotCompactMode();
        }

        // Called at the end of UpdateInfoManager, uim selection is up to date.
        private void UpdateSunShotSelectionActions()
        {
            if (tsSunShot == null) return;

            UploadInfoStatus item = uim.IsItemSelected ? uim.SelectedItem : null;
            bool ready = item != null && uim.SelectedItems != null && !uim.SelectedItems.Any(x => x.Task.IsWorking);

            tsbSunCopy.Enabled = ready && (item.IsImageFile || item.IsFileExist || item.IsURLExist);
            tsbSunSave.Enabled = ready && item.IsFileExist;
            tsbSunEdit.Enabled = ready && item.IsImageFile;
            tsbSunUpload.Enabled = ready && item.IsFileExist && !SystemOptions.DisableUpload && !Program.Settings.DisableUpload;
        }

        private void CopySelectedForSunShot()
        {
            if (!uim.IsItemSelected) return;

            UploadInfoStatus item = uim.SelectedItem;

            if (item.IsImageFile)
            {
                uim.CopyImage();
            }
            else if (item.IsFileExist)
            {
                uim.CopyFile();
            }
            else if (item.IsURLExist)
            {
                uim.CopyURL();
            }
        }

        private void SaveSelectedFileAs()
        {
            if (!uim.IsItemSelected || !uim.SelectedItem.IsFileExist) return;

            string sourcePath = uim.SelectedItem.Info.FilePath;
            string ext = Path.GetExtension(sourcePath);

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = Path.GetFileName(sourcePath);
                sfd.InitialDirectory = Path.GetDirectoryName(sourcePath);
                sfd.Filter = string.IsNullOrEmpty(ext) ? "*.*|*.*" : $"*{ext}|*{ext}|*.*|*.*";
                sfd.DefaultExt = ext.TrimStart('.');
                sfd.OverwritePrompt = true;

                if (sfd.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(sfd.FileName)) return;

                if (string.Equals(Path.GetFullPath(sfd.FileName), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase)) return;

                try
                {
                    File.Copy(sourcePath, sfd.FileName, true);
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex);
                    MessageBox.Show(this, SunShotUI.Text("SaveFailed") + Environment.NewLine + Environment.NewLine + ex.Message, Program.TitleShort,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        // Toolbar, context-menu and Ctrl+U uploads of a selected file share the same confirmation.
        private void UploadSelectedWithConfirmation()
        {
            if (!uim.IsItemSelected || !uim.SelectedItem.IsFileExist) return;

            UploadFileWithConfirmation(uim.SelectedItem.Info.FilePath);
        }

        internal void UploadFileWithConfirmation(string filePath)
        {
            if (!File.Exists(filePath) || !TaskHelpers.IsUploadAllowed()) return;

            WorkerTask task = WorkerTask.CreateFileUploaderTask(filePath, TaskSettings.GetDefaultTaskSettings());
            if (task == null) return;
            task.UploadConfirmation = (service, uploadFileName) =>
            {
                if (IsDisposed || Disposing || !IsHandleCreated) return false;
                try
                {
                    return (bool)Invoke(new Func<bool>(() => ConfirmUpload(uploadFileName, service)));
                }
                catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated)
                {
                    return false;
                }
            };
            TaskManager.Start(task);
        }

        private bool ConfirmUpload(string filePath, IGenericUploaderService destination)
        {
            if (SystemOptions.DisableUpload || Program.Settings.DisableUpload) return false;
            string destinationName = destination.ServiceName;

            if (!IsUploadDestinationSetUp(destination))
            {
                if (MessageBox.Show(this, string.Format(SunShotUI.Text("DestinationNotSetUp"), destinationName),
                    Program.TitleShort + " - " + SunShotUI.Text("UploadConfirmTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    TaskHelpers.OpenUploadersConfigWindow();
                }

                return false;
            }

            if (skipUploadConfirmation) return true;

            string text = string.Format(SunShotUI.Text("UploadConfirm"), Path.GetFileName(filePath), destinationName);

            using (MyMessageBox msgbox = new MyMessageBox(text, Program.TitleShort + " - " + SunShotUI.Text("UploadConfirmTitle"),
                MessageBoxButtons.YesNo, SunShotUI.Text("DontAskSession")))
            {
                msgbox.ShowDialog(this);

                bool confirmed = msgbox.DialogResult == DialogResult.Yes;

                if (confirmed && msgbox.IsChecked)
                {
                    skipUploadConfirmation = true;
                }

                return confirmed;
            }
        }

        private static bool IsUploadDestinationSetUp(IGenericUploaderService destination)
        {
            SettingManager.WaitUploadersConfig();

            UploadersConfig config = Program.UploadersConfig;

            if (config == null) return false;

            if (destination is ImageUploaderService image && image.EnumValue == ImageDestination.Imgur &&
                string.IsNullOrEmpty(config.ImgurOAuth2Info?.Client_ID)) return false;
            return destination.CheckConfig(config);
        }

        private void tsbImportShareXSettings_Click(object sender, EventArgs e)
        {
            using (ShareXSettingsImportForm form = new ShareXSettingsImportForm())
            {
                form.ShowDialog(this);
            }
        }

        private class SunShotToolStripRenderer : ToolStripCustomRenderer
        {
            public SunShotToolStripRenderer() : base(new DarkColorTable())
            {
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                Color text = ShareXResources.Theme.TextColor;

                if (!e.Item.Enabled)
                {
                    Color back = ShareXResources.Theme.BackgroundColor;
                    text = Color.FromArgb((text.R + back.R) / 2, (text.G + back.G) / 2, (text.B + back.B) / 2);
                }

                e.TextColor = text;

                base.OnRenderItemText(e);
            }
        }
    }
}
