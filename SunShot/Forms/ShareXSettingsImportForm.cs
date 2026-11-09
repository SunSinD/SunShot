// Copyright (c) 2026 SunSinD
// SPDX-License-Identifier: GPL-3.0-or-later

using ShareX.HelpersLib;
using ShareX.HistoryLib;
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
        private readonly CheckBox includeHistory;
        private readonly CheckBox reuseCaptureFolder;
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
            ClientSize = new Size(660, 560);
            MinimumSize = SizeFromClientSize(new Size(580, 560));

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 2,
                RowCount = 6
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
                    "Copy settings from a ShareX 21.0 or 22.0–22.4 profile. Close ShareX first.",
                    "Copiez les paramètres d’un profil ShareX 21.0 ou 22.0–22.4. Fermez ShareX avant de commencer.")
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

            FlowLayoutPanel options = new FlowLayoutPanel
            {
                AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, Margin = new Padding(0, 0, 0, 14)
            };
            includeHistory = new CheckBox {
                AutoSize = true, Checked = true,
                Text = Localize("Import capture history (files stay where they are)", "Importer l’historique (les fichiers restent en place)")
            };
            reuseCaptureFolder = new CheckBox {
                AutoSize = true,
                Text = Localize("Save new captures in the existing ShareX screenshot folder", "Enregistrer les nouvelles captures dans le dossier ShareX existant")
            };
            options.Controls.Add(includeHistory);
            options.Controls.Add(reuseCaptureFolder);
            layout.Controls.Add(options, 0, 3);
            layout.SetColumnSpan(options, 2);

            Label explanation = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Text = Localize(
                    "Imports your settings, hotkeys and upload destinations. Hotkeys, uploads, automatic actions and watch folders start off. SunShot saves a backup, which can contain saved logins, then restarts.",
                    "Importe vos paramètres, raccourcis et destinations d’envoi. Les raccourcis, les envois, les actions automatiques et les dossiers surveillés sont désactivés au départ. SunShot fait une sauvegarde, qui peut contenir des identifiants, puis redémarre.")
            };
            layout.Controls.Add(explanation, 0, 4);
            layout.SetColumnSpan(explanation, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 14, 0, 0) };
            Button cancel = new Button { AutoSize = true, Text = Localize("Cancel", "Annuler"), DialogResult = DialogResult.Cancel };
            importButton = new Button { AutoSize = true, Text = Localize("&Import and restart…", "&Importer et redémarrer…") };
            importButton.Click += ImportButton_Click;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(importButton);
            layout.Controls.Add(buttons, 0, 5);
            layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);
            CancelButton = cancel;
        }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            Process[] instances = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Application.ExecutablePath));
            bool otherInstance = instances.Any(x => x.Id != Environment.ProcessId);
            foreach (Process instance in instances) instance.Dispose();
            if (Program.Sandbox || Program.MultiInstance || otherInstance || TaskManager.IsBusy || ScreenRecordManager.IsRecording ||
                (includeHistory.Checked && HasOpenHistoryViews()))
            {
                MessageBox.Show(this, Localize(
                    "Close active tasks, history windows and other SunShot instances before importing.",
                    "Terminez les tâches en cours et fermez les fenêtres d’historique et les autres instances de SunShot avant l’importation."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, Localize(
                "Read the selected ShareX profile and prepare the import preview?",
                "Lire le profil ShareX sélectionné et préparer l’aperçu de l’importation ?"),
                Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.OK) return;

            importButton.Enabled = false;
            bool suspended = false;
            bool watchersSuspended = false;
            bool historySuspended = false;
            bool historyClosed = false;
            HotkeyManager hotkeys = Program.HotkeyManager;
            bool originalIgnoreHotkeys = hotkeys?.IgnoreHotkeys ?? false;
            bool hotkeysSuspended = false;
            try
            {
                SettingManager.ValidateImportDestination();
                ShareXSettingsImportService.ImportPlan plan = ShareXSettingsImportService.PrepareMigration(
                    sourcePath.Text, Program.PersonalFolder, includeHistory.Checked, reuseCaptureFolder.Checked);
                if (!ConfirmPreparedImport(plan)) { importButton.Enabled = true; return; }
                if (TaskManager.IsBusy || ScreenRecordManager.IsRecording || (plan.HasHistory && HasOpenHistoryViews()))
                    throw new InvalidDataException(Localize("Close active tasks and history windows before importing.", "Fermez les tâches actives et les fenêtres d’historique avant l’importation."));
                SettingManager.ValidateImportDestination();
                if (hotkeys != null)
                {
                    hotkeys.IgnoreHotkeys = true;
                    hotkeysSuspended = true;
                }
                watchersSuspended = Program.WatchFolderManager != null;
                Program.WatchFolderManager?.Dispose();
                SettingManager.SuspendSavesForImport();
                suspended = true;
                if (plan.HasHistory)
                {
                    TaskManager.SuspendHistoryAppendsForImport();
                    historySuspended = true;
                    historyClosed = true;
                    SettingManager.HistoryClose();
                }
                string backup = ShareXSettingsImportService.Apply(plan);
                string result = Localize(
                    $"Imported ShareX {plan.SourceVersion} settings and {plan.HotkeyCount} hotkeys.\r\n\r\nTo turn hotkeys on, choose Enable hotkeys in the tray menu. To upload, clear DisableUpload in Application settings > Advanced.\r\n\r\nBackup: {backup}",
                    $"Paramètres ShareX {plan.SourceVersion} et {plan.HotkeyCount} raccourcis importés.\r\n\r\nPour activer les raccourcis, choisissez Activer les raccourcis clavier dans le menu de notification. Pour envoyer, décochez DisableUpload dans Paramètres de l’application > Avancé.\r\n\r\nSauvegarde : {backup}");
                if (plan.HasHistory)
                    result += Localize("\r\n\r\nCapture history merged. Duplicates were skipped.",
                        "\r\n\r\nHistorique fusionné. Les doublons ont été ignorés.");
                else if (plan.HistoryRequested)
                    result += Localize("\r\n\r\nNo capture history was imported.",
                        "\r\n\r\nAucun historique n’a été importé.");
                if (plan.ReusesCaptureFolder)
                    result += Localize($"\r\n\r\nNew captures will use: {plan.CaptureFolder}", $"\r\n\r\nDossier des nouvelles captures : {plan.CaptureFolder}");
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
                bool historyUnavailable = false;
                if (!needsRecovery)
                {
                    if (historyClosed)
                    {
                        try { SettingManager.HistoryConnect(); }
                        catch { historyUnavailable = true; }
                    }
                    if (historySuspended && !historyUnavailable) TaskManager.ResumeHistoryAppendsAfterFailedImport();
                    if (suspended) SettingManager.ResumeSavesAfterFailedImport();
                    if (watchersSuspended) Program.WatchFolderManager?.UpdateWatchFolders();
                    if (hotkeysSuspended && !historyUnavailable) hotkeys.IgnoreHotkeys = originalIgnoreHotkeys;
                }
                string message = Localize("Import failed. ", "L’importation a échoué. ") +
                    (error is InvalidDataException || error is DirectoryNotFoundException ? error.Message : Localize("Check the folder and file permissions, then try again.", "Vérifiez le dossier et les permissions des fichiers, puis réessayez."));
                if (needsRecovery) message += Localize(
                    "\r\nSunShot must close. Close programs using the profile files, then reopen it. The import will be rolled back.",
                    "\r\nSunShot doit se fermer. Fermez les programmes qui utilisent le profil, puis rouvrez-le. L’importation sera annulée.");
                if (historyUnavailable) message += Localize(
                    "\r\nSunShot could not reopen its history. Close programs using the history file, then reopen it.",
                    "\r\nSunShot n’a pas pu rouvrir son historique. Fermez les programmes qui utilisent le fichier, puis rouvrez-le.");
                MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (needsRecovery || historyUnavailable) { Close(); Application.Exit(); }
                else importButton.Enabled = true;
            }
        }

        private static bool HasOpenHistoryViews() => Application.OpenForms.Cast<Form>().Any(form => form is HistoryForm || form is ImageHistoryForm);

        private bool ConfirmPreparedImport(ShareXSettingsImportService.ImportPlan plan)
        {
            using Form review = new Form {
                Text = Localize("Review ShareX import", "Vérifier l’importation ShareX"),
                StartPosition = FormStartPosition.CenterParent, Font = Font,
                ClientSize = new Size(740, 500), MinimumSize = new Size(560, 400),
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false
            };
            string summary = Localize(
                $"ShareX {plan.SourceVersion}: {plan.HotkeyCount} hotkeys ({(plan.HasHotkeys ? "imported" : "defaults")}). Upload destinations: {(plan.HasUploaders ? "imported" : "defaults")}. Hotkeys, uploads, automatic actions and watch folders will be off.\r\n\r\n",
                $"ShareX {plan.SourceVersion} : {plan.HotkeyCount} raccourcis ({(plan.HasHotkeys ? "importés" : "par défaut")}). Destinations : {(plan.HasUploaders ? "importées" : "par défaut")}. Les raccourcis, envois, actions automatiques et dossiers surveillés seront désactivés.\r\n\r\n");
            summary += plan.HasHistory ? Localize(
                $"History: {plan.HistoryCount} entries will be merged. Duplicates are skipped and images stay where they are.\r\n\r\n",
                $"Historique : {plan.HistoryCount} entrées seront fusionnées. Les doublons sont ignorés et les images restent en place.\r\n\r\n") : Localize(
                "Capture history is unchanged.\r\n\r\n", "L’historique reste inchangé.\r\n\r\n");
            summary += plan.ReusesCaptureFolder ? Localize(
                $"New captures: {plan.CaptureFolder}\r\n\r\n",
                $"Nouvelles captures : {plan.CaptureFolder}\r\n\r\n") : Localize(
                "New captures will use SunShot's screenshots folder.\r\n\r\n", "Les nouvelles captures utiliseront le dossier SunShot.\r\n\r\n");
            if (plan.UnreadableCredentials > 0) summary += Localize(
                $"{plan.UnreadableCredentials} credentials could not be read and will be cleared. Reconnect those accounts.\r\n\r\n",
                $"{plan.UnreadableCredentials} identifiants n’ont pas pu être lus et seront effacés. Reconnectez ces comptes.\r\n\r\n");
            summary += plan.Warnings.Count == 0 ? Localize("No other compatibility notes.", "Aucune autre remarque de compatibilité.") :
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
