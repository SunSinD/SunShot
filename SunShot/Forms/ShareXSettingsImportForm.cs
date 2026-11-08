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
                    "Copy settings from a ShareX 21.0 or 22.0–22.4 profile. ShareX files and Windows integration remain untouched. Close ShareX before importing so its settings are up to date.",
                    "Copiez les paramètres d’un profil ShareX 21.0 ou 22.0–22.4. Les fichiers et l’intégration Windows de ShareX restent intacts. Fermez ShareX avant l’importation pour enregistrer ses derniers paramètres.")
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
                    "ShareX 22 themes, native recording and newer tools have limitations. You can review all reported changes before applying. Current SunShot settings are backed up under Backup; the backup may contain credentials. SunShot restarts after importing. Nothing is read until you confirm below.",
                    "Importe les préférences de capture, les raccourcis clavier, les tâches et les destinations d’envoi, y compris les identifiants enregistrés. Les fichiers de raccourcis ou de destinations absents utilisent les paramètres SunShot par défaut. Les chemins de configuration externes ne sont pas suivis.\r\n\r\n" +
                    "Les raccourcis et l’envoi sont désactivés après l’importation. L’envoi automatique, les actions externes, la suppression, les tâches IA et les dossiers surveillés sont désactivés. Les captures et les paramètres utilisent les dossiers SunShot. L’historique, les images, PersonalPath.cfg, le démarrage et les associations de fichiers sont exclus.\r\n\r\n" +
                    "Les thèmes, l’enregistrement natif et les nouveaux outils de ShareX 22 ont des limites. Les changements signalés seront affichés avant l’importation. Les paramètres actuels sont sauvegardés dans Backup; la sauvegarde peut contenir des identifiants. SunShot redémarre après l’importation. Aucun fichier n’est lu avant votre confirmation.")
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
                "Read the selected ShareX profile and prepare an import preview? You will review the limitations before replacing any SunShot settings.",
                "Lire le profil ShareX sélectionné et préparer un aperçu ? Vous pourrez vérifier les limites avant de remplacer les paramètres SunShot."),
                Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.OK) return;

            importButton.Enabled = false;
            bool suspended = false;
            bool watchersSuspended = false;
            try
            {
                ShareXSettingsImportService.ImportPlan plan = ShareXSettingsImportService.Prepare(sourcePath.Text, Program.PersonalFolder);
                if (!ConfirmPreparedImport(plan)) { importButton.Enabled = true; return; }
                if (TaskManager.IsBusy || ScreenRecordManager.IsRecording)
                    throw new InvalidDataException(Localize("Finish recording and active tasks before importing.", "Terminez l’enregistrement et les tâches avant l’importation."));
                watchersSuspended = Program.WatchFolderManager != null;
                Program.WatchFolderManager?.Dispose();
                SettingManager.SuspendSavesForImport();
                suspended = true;
                string backup = ShareXSettingsImportService.Apply(plan);
                string result = Localize(
                    $"Imported ShareX {plan.SourceVersion} settings and {plan.HotkeyCount} hotkey definitions ({(plan.HasHotkeys ? "imported" : "defaults, file absent")}). Upload destinations: {(plan.HasUploaders ? "imported" : "defaults (file absent)")}.\r\n\r\nReview your settings first. To enable hotkeys, choose Enable hotkeys in the tray menu. To enable uploading, clear DisableUpload in Application settings > Advanced.\r\n\r\nBackup: {backup}",
                    $"Paramètres ShareX {plan.SourceVersion} et {plan.HotkeyCount} raccourcis ({(plan.HasHotkeys ? "importés" : "par défaut, fichier absent")}). Destinations : {(plan.HasUploaders ? "importées" : "par défaut (fichier absent)")}.\r\n\r\nVérifiez vos paramètres. Pour activer les raccourcis, choisissez Activer les raccourcis clavier dans le menu de notification. Pour activer l’envoi, décochez DisableUpload dans Paramètres de l’application > Avancé.\r\n\r\nSauvegarde : {backup}");
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

        private bool ConfirmPreparedImport(ShareXSettingsImportService.ImportPlan plan)
        {
            using Form review = new Form {
                Text = Localize("Review ShareX import", "Vérifier l’importation ShareX"),
                StartPosition = FormStartPosition.CenterParent, Font = Font,
                ClientSize = new Size(740, 500), MinimumSize = new Size(560, 400),
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false
            };
            string summary = Localize(
                $"ShareX {plan.SourceVersion}; {plan.HotkeyCount} hotkey definitions ({(plan.HasHotkeys ? "imported" : "defaults, file absent")}). Upload destinations: {(plan.HasUploaders ? "imported" : "defaults (file absent)")}.\r\n\r\nHotkeys, uploads, automatic upload/actions/deletion/AI and watch folders will be disabled. SunShot paths will be used. A backup will be kept.\r\n\r\n",
                $"ShareX {plan.SourceVersion}; {plan.HotkeyCount} raccourcis ({(plan.HasHotkeys ? "importés" : "par défaut, fichier absent")}). Destinations : {(plan.HasUploaders ? "importées" : "par défaut (fichier absent)")}.\r\n\r\nLes raccourcis, l’envoi, les actions automatiques, la suppression, l’IA et les dossiers surveillés seront désactivés. Les dossiers SunShot seront utilisés. Une sauvegarde sera conservée.\r\n\r\n");
            if (plan.UnreadableCredentials > 0) summary += Localize(
                $"{plan.UnreadableCredentials} unreadable credentials will be cleared. Reconnect those accounts.\r\n\r\n",
                $"{plan.UnreadableCredentials} identifiants illisibles seront effacés. Reconnectez ces comptes.\r\n\r\n");
            summary += plan.Warnings.Count == 0 ? Localize("No additional compatibility limitations reported.", "Aucune autre limite de compatibilité signalée.") :
                Localize($"Compatibility report ({plan.Warnings.Count} notices):\r\n", $"Rapport de compatibilité ({plan.Warnings.Count} avis; détails techniques en anglais) :\r\n") + string.Join("\r\n\r\n", plan.Warnings);
            TextBox report = new TextBox {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, Text = summary,
                AccessibleName = Localize("Import compatibility report", "Rapport de compatibilité")
            };
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8), FlowDirection = FlowDirection.RightToLeft };
            Button cancel = new Button { Text = Localize("Cancel", "Annuler"), AutoSize = true, DialogResult = DialogResult.Cancel };
            Button apply = new Button { Text = Localize("Import and restart", "Importer et redémarrer"), AutoSize = true, DialogResult = DialogResult.OK };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(apply);
            review.Controls.Add(report);
            review.Controls.Add(buttons);
            review.CancelButton = cancel;
            review.ActiveControl = cancel;
            return review.ShowDialog(this) == DialogResult.OK;
        }
    }
}
