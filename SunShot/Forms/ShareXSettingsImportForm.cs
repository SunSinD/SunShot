// Copyright (c) 2026 SunSinD
// SPDX-License-Identifier: GPL-3.0-or-later

using ShareX.HelpersLib;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace ShareX
{
    public sealed class ShareXSettingsImportForm : Form
    {
        private readonly TextBox sourcePath;
        private readonly Button importButton;
        private readonly bool french = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";

        private string Localize(string english, string frenchText) => french ? frenchText : english;

        public ShareXSettingsImportForm()
        {
            Text = Localize("SunShot - Import ShareX settings", "SunShot - Importer les paramètres ShareX");
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(640, 470);
            MinimumSize = new Size(560, 470);

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 2,
                RowCount = 5
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label introduction = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 14),
                Text = Localize(
                    "Copy settings from a ShareX 21.0 profile. ShareX files and Windows integration remain untouched. Close ShareX before importing so its settings are up to date.",
                    "Copiez les paramètres d’un profil ShareX 21.0. Les fichiers et l’intégration Windows de ShareX restent intacts. Fermez ShareX avant l’importation pour enregistrer ses derniers paramètres.")
            };
            layout.Controls.Add(introduction, 0, 0);
            layout.SetColumnSpan(introduction, 2);
            layout.Controls.Add(new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6),
                Text = Localize("ShareX profile folder (suggested location):", "Dossier du profil ShareX (emplacement suggéré) :")
            }, 0, 1);
            sourcePath = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = ShareXSettingsImportService.SuggestedSourceFolder,
                AccessibleName = Localize("ShareX profile folder", "Dossier du profil ShareX"),
                Margin = new Padding(0, 0, 8, 14)
            };
            layout.Controls.Add(sourcePath, 0, 2);
            Button browse = new Button { AutoSize = true, Text = Localize("&Browse…", "&Parcourir…"), Margin = new Padding(0, 0, 0, 14) };
            browse.Click += (_, _) =>
            {
                using FolderBrowserDialog dialog = new FolderBrowserDialog
                {
                    Description = Localize("Select the folder containing ApplicationConfig.json", "Sélectionnez le dossier contenant ApplicationConfig.json"),
                    ShowNewFolderButton = false,
                    SelectedPath = sourcePath.Text
                };
                if (dialog.ShowDialog(this) == DialogResult.OK) sourcePath.Text = dialog.SelectedPath;
            };
            layout.Controls.Add(browse, 1, 2);

            Label explanation = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Text = Localize(
                    "Imports capture preferences, hotkey definitions, workflows, and upload destinations, including saved credentials. Missing hotkey or uploader files use SunShot defaults. External config paths are not followed.\r\n\r\n" +
                    "Hotkeys and uploading are disabled after import. Automatic upload, external actions, deletion, AI tasks, and watch folders are disabled. Screenshot and config paths use SunShot’s own folders. History, screenshots, PersonalPath.cfg, startup settings, and file associations are excluded.\r\n\r\n" +
                    "Current SunShot settings are backed up under its Backup folder. The backup may contain credentials. SunShot restarts after importing. Nothing in ShareX is read until you confirm below.",
                    "Importe les préférences de capture, les raccourcis clavier, les tâches et les destinations d’envoi, y compris les identifiants enregistrés. Les fichiers de raccourcis ou de destinations absents utilisent les paramètres SunShot par défaut. Les chemins de configuration externes ne sont pas suivis.\r\n\r\n" +
                    "Les raccourcis et l’envoi sont désactivés après l’importation. L’envoi automatique, les actions externes, la suppression, les tâches IA et les dossiers surveillés sont désactivés. Les captures et les paramètres utilisent les dossiers SunShot. L’historique, les images, PersonalPath.cfg, le démarrage et les associations de fichiers sont exclus.\r\n\r\n" +
                    "Les paramètres SunShot actuels sont sauvegardés dans son dossier Backup. La sauvegarde peut contenir des identifiants. SunShot redémarre après l’importation. Aucun fichier ShareX n’est lu avant votre confirmation.")
            };
            layout.Controls.Add(explanation, 0, 3);
            layout.SetColumnSpan(explanation, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 14, 0, 0) };
            Button cancel = new Button { AutoSize = true, Text = Localize("Cancel", "Annuler"), DialogResult = DialogResult.Cancel };
            importButton = new Button { AutoSize = true, Text = Localize("&Import and restart…", "&Importer et redémarrer…") };
            importButton.Click += ImportButton_Click;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(importButton);
            layout.Controls.Add(buttons, 0, 4);
            layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);
            CancelButton = cancel;
        }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            Process[] instances = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Application.ExecutablePath));
            bool otherInstance = instances.Any(x => x.Id != Environment.ProcessId);
            foreach (Process instance in instances) instance.Dispose();
            if (Program.Sandbox || Program.MultiInstance || otherInstance || TaskManager.IsBusy || ScreenRecordManager.IsRecording)
            {
                MessageBox.Show(this, Localize(
                    "Finish active tasks and close other SunShot instances first. Import is unavailable in sandbox mode or with the multi-instance flag.",
                    "Terminez les tâches et fermez les autres instances SunShot. L’importation est indisponible en mode sandbox ou avec l’option multi-instance."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, Localize(
                "Read the selected ShareX profile, replace the SunShot settings, and restart SunShot? A backup of your SunShot settings will be kept.",
                "Lire le profil ShareX sélectionné, remplacer les paramètres SunShot et redémarrer SunShot ? Une sauvegarde de vos paramètres SunShot sera conservée."),
                Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.OK) return;

            importButton.Enabled = false;
            bool suspended = false;
            bool watchersSuspended = false;
            try
            {
                ShareXSettingsImportService.ImportPlan plan = ShareXSettingsImportService.Prepare(sourcePath.Text, Program.PersonalFolder);
                if (TaskManager.IsBusy || ScreenRecordManager.IsRecording)
                    throw new InvalidDataException(Localize("Finish recording and active tasks before importing.", "Terminez l’enregistrement et les tâches avant l’importation."));
                watchersSuspended = Program.WatchFolderManager != null;
                Program.WatchFolderManager?.Dispose();
                SettingManager.SuspendSavesForImport();
                suspended = true;
                string backup = ShareXSettingsImportService.Apply(plan);
                string result = Localize(
                    $"Imported ShareX {plan.SourceVersion} settings and {plan.HotkeyCount} hotkey definitions. Upload destinations: {(plan.HasUploaders ? "imported" : "defaults (file absent)")}.\r\n\r\nReview your settings first. To enable hotkeys, choose Enable hotkeys in the tray menu. To enable uploading, clear DisableUpload in Application settings > Advanced.\r\n\r\nBackup: {backup}",
                    $"Paramètres ShareX {plan.SourceVersion} et {plan.HotkeyCount} raccourcis importés. Destinations : {(plan.HasUploaders ? "importées" : "par défaut (fichier absent)")}.\r\n\r\nVérifiez vos paramètres. Pour activer les raccourcis, choisissez Activer les raccourcis clavier dans le menu de notification. Pour activer l’envoi, décochez DisableUpload dans Paramètres de l’application > Avancé.\r\n\r\nSauvegarde : {backup}");
                if (plan.UnreadableCredentials > 0)
                    result += Localize($"\r\n\r\n{plan.UnreadableCredentials} credentials could not be decrypted for this Windows account and were cleared. Reconnect those accounts.",
                        $"\r\n\r\n{plan.UnreadableCredentials} identifiants n’ont pas pu être déchiffrés pour ce compte Windows et ont été effacés. Reconnectez ces comptes.");
                MessageBox.Show(this, result, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
                Program.Restart();
            }
            catch (Exception error)
            {
                // Do not log exception data from a settings parser: it could contain credentials.
                bool needsRecovery = ShareXSettingsImportService.HasPendingImport(Program.PersonalFolder);
                if (!needsRecovery)
                {
                    if (suspended) SettingManager.ResumeSavesAfterFailedImport();
                    if (watchersSuspended) Program.WatchFolderManager?.UpdateWatchFolders();
                }
                string message = Localize("Import failed. ", "L’importation a échoué. ") +
                    (error is InvalidDataException || error is DirectoryNotFoundException ? error.Message : Localize("Check the folder and file permissions, then try again.", "Vérifiez le dossier et les permissions des fichiers, puis réessayez."));
                if (needsRecovery) message += Localize(
                    "\r\nSunShot must close to protect your settings. Close any program locking the profile files before reopening SunShot. The pending import will be rolled back at startup.",
                    "\r\nSunShot doit se fermer pour protéger vos paramètres. Fermez les programmes qui bloquent les fichiers avant de rouvrir SunShot. L’importation sera annulée au prochain démarrage.");
                MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (needsRecovery) { Close(); Application.Exit(); }
                else importButton.Enabled = true;
            }
        }
    }
}
