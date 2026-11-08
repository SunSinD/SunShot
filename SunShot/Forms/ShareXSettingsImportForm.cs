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
                    "Imports capture settings, workflows, hotkeys and upload destinations, including credentials. Missing optional files use defaults; external config paths are skipped. Windows integration stays separate.\r\n\r\n" +
                    "History is merged without moving images or removing existing entries. Reusing the screenshot folder allows new captures to be saved there.\r\n\r\n" +
                    "Hotkeys, uploads, automatic actions, deletion, AI and watch folders start disabled. ShareX 22 themes, recording and newer tools have limitations. Review the report before applying.\r\n\r\n" +
                    "SunShot backs up current settings and history under Backup, then restarts. Backups may contain credentials. Nothing is read until you confirm.",
                    "Importe captures, tâches, raccourcis et destinations, avec les identifiants. Les fichiers facultatifs absents utilisent les valeurs par défaut. Les chemins externes et l’intégration Windows sont exclus.\r\n\r\n" +
                    "L’historique est fusionné sans déplacer les images ni supprimer les entrées existantes. Réutiliser le dossier permet d’y enregistrer de nouvelles captures.\r\n\r\n" +
                    "Raccourcis, envois, actions automatiques, suppression, IA et dossiers surveillés sont désactivés. Les thèmes, l’enregistrement et les nouveaux outils de ShareX 22 ont des limites. Vérifiez le rapport avant d’appliquer.\r\n\r\n" +
                    "SunShot sauvegarde les paramètres et l’historique dans Backup, puis redémarre. Les sauvegardes peuvent contenir des identifiants. Aucun fichier n’est lu avant confirmation.")
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
                    "Finish active tasks and close SunShot's History and Image history windows and other instances first. Import is unavailable in sandbox mode or with the multi-instance flag.",
                    "Terminez les tâches et fermez les fenêtres Historique et Historique des images ainsi que les autres instances SunShot. L’importation est indisponible en mode sandbox ou avec l’option multi-instance."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, Localize(
                "Read the selected ShareX profile and prepare an import preview? You will review the limitations before replacing any SunShot settings.",
                "Lire le profil ShareX sélectionné et préparer un aperçu ? Vous pourrez vérifier les limites avant de remplacer les paramètres SunShot."),
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
                    throw new InvalidDataException(Localize("Finish active tasks and close the history windows before importing.", "Terminez les tâches et fermez les fenêtres d’historique avant l’importation."));
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
                    $"Imported ShareX {plan.SourceVersion} settings and {plan.HotkeyCount} hotkey definitions ({(plan.HasHotkeys ? "imported" : "defaults, file absent")}). Upload destinations: {(plan.HasUploaders ? "imported" : "defaults (file absent)")}.\r\n\r\nReview your settings first. To enable hotkeys, choose Enable hotkeys in the tray menu. To enable uploading, clear DisableUpload in Application settings > Advanced.\r\n\r\nBackup: {backup}",
                    $"Paramètres ShareX {plan.SourceVersion} et {plan.HotkeyCount} raccourcis ({(plan.HasHotkeys ? "importés" : "par défaut, fichier absent")}). Destinations : {(plan.HasUploaders ? "importées" : "par défaut (fichier absent)")}.\r\n\r\nVérifiez vos paramètres. Pour activer les raccourcis, choisissez Activer les raccourcis clavier dans le menu de notification. Pour activer l’envoi, décochez DisableUpload dans Paramètres de l’application > Avancé.\r\n\r\nSauvegarde : {backup}");
                if (plan.HasHistory)
                    result += Localize("\r\n\r\nCapture history merged. Existing entries were kept and duplicates skipped. Original images stay in place.",
                        "\r\n\r\nHistorique fusionné. Les entrées existantes sont conservées et les doublons ignorés. Les images d’origine restent en place.");
                else if (plan.HistoryRequested)
                    result += Localize("\r\n\r\nNo capture history was imported. Your existing SunShot history is unchanged.",
                        "\r\n\r\nAucun historique n’a été importé. Votre historique SunShot existant reste intact.");
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
                    "\r\nSunShot must close to protect your settings. Close any program locking the profile files before reopening SunShot. The pending import will be rolled back at startup.",
                    "\r\nSunShot doit se fermer pour protéger vos paramètres. Fermez les programmes qui bloquent les fichiers avant de rouvrir SunShot. L’importation sera annulée au prochain démarrage.");
                if (historyUnavailable) message += Localize(
                    "\r\nSunShot could not reopen its history and must close. Close any program locking the history file, then reopen SunShot.",
                    "\r\nSunShot n’a pas pu rouvrir son historique et doit se fermer. Fermez les programmes qui bloquent ce fichier, puis rouvrez SunShot.");
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
                $"ShareX {plan.SourceVersion}; {plan.HotkeyCount} hotkey definitions ({(plan.HasHotkeys ? "imported" : "defaults, file absent")}). Upload destinations: {(plan.HasUploaders ? "imported" : "defaults (file absent)")}.\r\n\r\nHotkeys, uploads, automatic actions/deletion/AI and watch folders will be disabled. Settings stay in SunShot's own profile. A backup will be kept.\r\n\r\n",
                $"ShareX {plan.SourceVersion}; {plan.HotkeyCount} raccourcis ({(plan.HasHotkeys ? "importés" : "par défaut, fichier absent")}). Destinations : {(plan.HasUploaders ? "importées" : "par défaut (fichier absent)")}.\r\n\r\nLes raccourcis, l’envoi, les actions automatiques, la suppression, l’IA et les dossiers surveillés seront désactivés. Les paramètres restent dans le profil SunShot. Une sauvegarde sera conservée.\r\n\r\n");
            summary += plan.HasHistory ? Localize(
                $"History: {plan.HistoryCount} valid source entries will be merged with your SunShot history. Duplicates are skipped. File paths and URLs are preserved; images are not moved or copied. Editing or deleting an original file through history still affects that file.\r\n\r\n",
                $"Historique : {plan.HistoryCount} entrées valides seront fusionnées avec l’historique SunShot. Les doublons sont ignorés. Les chemins et les URL sont conservés; les images ne sont ni déplacées ni copiées. Modifier ou supprimer un fichier depuis l’historique agit toujours sur l’original.\r\n\r\n") : Localize(
                "Capture history will not change.\r\n\r\n", "L’historique ne sera pas modifié.\r\n\r\n");
            summary += plan.ReusesCaptureFolder ? Localize(
                $"New captures will be written to: {plan.CaptureFolder}\r\nExisting captures stay in place. Unique filenames prevent overwriting them.\r\n\r\n",
                $"Les nouvelles captures seront enregistrées dans : {plan.CaptureFolder}\r\nLes captures existantes restent en place. Des noms uniques évitent de les remplacer.\r\n\r\n") : Localize(
                "New captures will use SunShot's screenshots folder.\r\n\r\n", "Les nouvelles captures utiliseront le dossier SunShot.\r\n\r\n");
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
