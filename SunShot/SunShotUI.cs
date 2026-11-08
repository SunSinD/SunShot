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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ShareX
{
    // SunShot keeps every ShareX feature in the code and in existing configs, but hides
    // AI analysis and tools unrelated to capturing from the normal menus and pickers.
    // An item that is already in use (checked / selected) is never hidden, so users can still see and turn it off.
    public static class SunShotUI
    {
        public static readonly HotkeyType[] HiddenHotkeyTypes = new HotkeyType[]
        {
            HotkeyType.AnalyzeImage,
            HotkeyType.HashCheck,
            HotkeyType.IndexFolder,
            HotkeyType.ClipboardViewer,
            HotkeyType.BorderlessWindow,
            HotkeyType.ActiveWindowBorderless,
            HotkeyType.ActiveWindowTopMost,
            HotkeyType.InspectWindow,
            HotkeyType.MonitorTest
        };

        public static readonly AfterCaptureTasks[] HiddenAfterCaptureTasks = new AfterCaptureTasks[]
        {
            AfterCaptureTasks.AnalyzeImage
        };

        public static bool IsHidden(HotkeyType hotkeyType)
        {
            return HiddenHotkeyTypes.Contains(hotkeyType);
        }

        public static bool IsHidden(AfterCaptureTasks task)
        {
            return HiddenAfterCaptureTasks.Contains(task);
        }

        // For menus built from AfterCaptureTasks where item index i maps to flag 1 << i (None skipped).
        // Items are hidden instead of removed so index based check/toggle logic keeps working.
        // A hidden task stays visible while it is enabled, so it can still be turned off.
        public static void UpdateAfterCaptureItems(ToolStripItemCollection items, AfterCaptureTasks current)
        {
            for (int i = 0; i < items.Count; i++)
            {
                AfterCaptureTasks task = items[i].Tag is AfterCaptureTasks tag ? tag : (AfterCaptureTasks)(1 << i);

                if (IsHidden(task))
                {
                    items[i].Visible = current.HasFlag(task);
                }
            }
        }

        // For HotkeyType pickers whose items carry an EnumInfo tag, optionally grouped in category sub menus.
        public static void HideHotkeyItems(ToolStripItemCollection items, HotkeyType current)
        {
            foreach (ToolStripItem item in items)
            {
                if (item.Tag is EnumInfo enumInfo && enumInfo.Value is HotkeyType hotkeyType)
                {
                    if (IsHidden(hotkeyType) && hotkeyType != current)
                    {
                        item.Visible = false;
                    }
                }
                else if (item is ToolStripDropDownItem dropDownItem && dropDownItem.DropDownItems.Count > 0)
                {
                    HideHotkeyItems(dropDownItem.DropDownItems, current);
                }
            }
        }

        public static bool IsFrench => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";

        // Small string table for the few strings SunShot adds. Everything else reuses existing ShareX resources.
        public static string Text(string key)
        {
            if (Strings.TryGetValue(key, out string[] values))
            {
                return IsFrench ? values[1] : values[0];
            }

            return key;
        }

        private static readonly Dictionary<string, string[]> Strings = new Dictionary<string, string[]>()
        {
            ["SaveAs"] = new[] { "&Save as...", "&Enregistrer sous..." },
            ["SaveAsTip"] = new[] { "Save a copy of the selected file", "Enregistrer une copie du fichier sélectionné" },
            ["CopyTip"] = new[] { "Copy the selected image or file", "Copier l'image ou le fichier sélectionné" },
            ["EditTip"] = new[] { "Annotate the selected image (Ctrl+E)", "Annoter l'image sélectionnée (Ctrl+E)" },
            ["UploadTip"] = new[] { "Upload the selected file (asks first)", "Téléverser le fichier sélectionné (demande avant)" },
            ["RegionTip"] = new[] { "Capture a region of the screen", "Capturer une région de l'écran" },
            ["RecordTip"] = new[] { "Record a region of the screen", "Enregistrer une région de l'écran" },
            ["Import"] = new[] { "Import ShareX settings...", "Importer les paramètres ShareX..." },
            ["AutoUploadOn"] = new[] { "Auto upload: On", "Téléversement auto : activé" },
            ["AutoUploadOff"] = new[] { "Auto upload: Off", "Téléversement auto : désactivé" },
            ["AutoUploadTip"] = new[] { "Whether captures are uploaded automatically. Change it in After capture tasks.",
                "Indique si les captures sont téléversées automatiquement. Modifiable dans Tâches après capture." },
            ["UploadConfirmTitle"] = new[] { "Upload file", "Téléverser le fichier" },
            ["UploadConfirm"] = new[] { "Upload \"{0}\" to {1}?\r\n\r\nThe file leaves your computer. Depending on the destination, anyone with the link may be able to see it.",
                "Téléverser « {0} » vers {1} ?\r\n\r\nLe fichier quitte votre ordinateur. Selon la destination, toute personne ayant le lien pourrait le voir." },
            ["DestinationNotSetUp"] = new[] { "The upload destination \"{0}\" is not set up yet, so nothing was uploaded.\r\n\r\nOpen destination settings now?",
                "La destination de téléversement « {0} » n'est pas encore configurée, donc rien n'a été téléversé.\r\n\r\nOuvrir les paramètres de destination maintenant ?" },
            ["DontAskSession"] = new[] { "Don't ask again until SunShot is restarted", "Ne plus demander jusqu'au redémarrage de SunShot" },
            ["SaveFailed"] = new[] { "The file could not be saved.", "Le fichier n'a pas pu être enregistré." },
            ["BackupExported"] = new[] { "Backup saved. This .sxb file is a ZIP archive. SunShot does not restore it automatically. To restore it manually, close SunShot, keep a copy of your current profile, then extract the backup files into your SunShot profile folder. The ShareX importer does not accept .sxb files.",
                "Sauvegarde enregistrée. Ce fichier .sxb est une archive ZIP. SunShot ne la restaure pas automatiquement. Pour la restaurer manuellement, fermez SunShot, gardez une copie de votre profil actuel, puis extrayez les fichiers dans votre dossier de profil SunShot. L'importateur ShareX n'accepte pas les fichiers .sxb." }
        };
    }
}
