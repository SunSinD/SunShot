// Copyright (c) 2026 SunSinD
// SPDX-License-Identifier: GPL-3.0-or-later

using ShareX.HelpersLib;
using ShareX.ScreenCaptureLib;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace ShareX
{
    internal static class SunShotFFmpeg
    {
        internal const string DownloadUrl = "https://github.com/ShareX/FFmpeg/releases/download/v8.1/ffmpeg-8.1-win-x64.zip";
        internal const string ArchiveSha256 = "1838932e32f01e0e7b65fde5ca0ebb2ab131cb6a9ff1a84025ee5b784075576c";

        private static string Text(string english, string french) =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? french : english;

        public static bool EnsureAvailable(FFmpegOptions options)
        {
            if (File.Exists(options.FFmpegPath)) return true;
            if (options.OverrideCLIPath) return false;

            string executable = Path.Combine(Program.PersonalFolder, "Tools", "ffmpeg.exe");
            if (!File.Exists(executable))
            {
                if (MessageBox.Show(Text(
                    "FFmpeg is needed for recording. Download it from ShareX's GitHub release?",
                    "L’enregistrement nécessite FFmpeg. Le télécharger depuis la version GitHub de ShareX ? Il sera enregistré dans le dossier du profil SunShot."),
                    "SunShot - FFmpeg", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return false;

                bool installed = false;
                using DownloaderForm downloader = new DownloaderForm(DownloadUrl, "sunshot-ffmpeg-8.1-x64.zip")
                {
                    InstallType = InstallType.Event,
                    RunInstallerInBackground = false
                };
                downloader.InstallRequested += archive =>
                {
                    try { InstallVerifiedArchive(archive, executable); installed = true; }
                    catch (Exception error)
                    {
                        DebugHelper.WriteException(error);
                        MessageBox.Show(Text("FFmpeg could not be installed. Check the connection and folder permissions, then try again.",
                            "Impossible d’installer FFmpeg. Vérifiez la connexion et les permissions du dossier, puis réessayez."),
                            "SunShot - FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                downloader.ShowDialog();
                if (!installed) return false;
            }

            options.CLIPath = executable;
            options.OverrideCLIPath = true;
            return true;
        }

        internal static void InstallVerifiedArchive(string archivePath, string executable)
        {
            using (FileStream stream = File.OpenRead(archivePath))
                if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("FFmpeg download checksum does not match.");

            Directory.CreateDirectory(Path.GetDirectoryName(executable));
            string temporary = executable + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(archivePath))
                {
                    ZipArchiveEntry entry = archive.Entries.Single(x => x.FullName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                    if (entry.Length != 202259968) throw new InvalidDataException("Unexpected FFmpeg executable size.");
                    entry.ExtractToFile(temporary);
                }
                File.Move(temporary, executable, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
