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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ShareX
{
    public class WatchFolder : IDisposable
    {
        public WatchFolderSettings Settings { get; set; }
        public TaskSettings TaskSettings { get; set; }

        public delegate void FileWatcherTriggerEventHandler(string path);

        public event FileWatcherTriggerEventHandler FileWatcherTrigger;

        private SynchronizationContext context;
        private FileSystemWatcher fileWatcher;
        private List<WatchFolderDuplicateEventTimer> timers = new List<WatchFolderDuplicateEventTimer>();

        public virtual void Enable()
        {
            Dispose();

            string folderPath = FileHelpers.ExpandFolderVariables(Settings.FolderPath);

            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                context = SynchronizationContext.Current ?? new SynchronizationContext();

                fileWatcher = new FileSystemWatcher(folderPath);
                if (!string.IsNullOrEmpty(Settings.Filter)) fileWatcher.Filter = Settings.Filter;
                fileWatcher.IncludeSubdirectories = Settings.IncludeSubdirectories;
                fileWatcher.Created += fileWatcher_Created;
                fileWatcher.EnableRaisingEvents = true;
            }
        }

        protected void OnFileWatcherTrigger(string path)
        {
            FileWatcherTrigger?.Invoke(path);
        }

        private async void fileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            CleanElapsedTimers();

            string path = e.FullPath;

            foreach (WatchFolderDuplicateEventTimer timer in timers)
            {
                if (timer.IsDuplicateEvent(path))
                {
                    return;
                }
            }

            timers.Add(new WatchFolderDuplicateEventTimer(path));

            int successCount = 0;
            long previousSize = -1;
            long lastObservedSize = -1;
            Stopwatch idleTimer = Stopwatch.StartNew();
            bool timedOut = false;

            await Helpers.WaitWhileAsync(() =>
            {
                if (!ReferenceEquals(sender, fileWatcher))
                {
                    return false;
                }

                // Only give up after the file size stopped changing for a while, so files that take long to write are not skipped.
                long observedSize = FileHelpers.GetFileSize(path);

                if (observedSize != lastObservedSize)
                {
                    lastObservedSize = observedSize;
                    idleTimer.Restart();
                }
                else if (idleTimer.ElapsedMilliseconds >= 5000)
                {
                    timedOut = true;
                    return false;
                }

                if (!FileHelpers.IsFileLocked(path))
                {
                    long currentSize = FileHelpers.GetFileSize(path);

                    if (currentSize > 0 && currentSize == previousSize)
                    {
                        successCount++;
                    }
                    else
                    {
                        successCount = 0;
                    }

                    previousSize = currentSize;
                    return successCount < 4;
                }

                previousSize = -1;
                successCount = 0;
                return true;
            }, 250, -1, () =>
            {
                if (!ReferenceEquals(sender, fileWatcher))
                {
                    return;
                }

                if (timedOut)
                {
                    DebugHelper.WriteLine($"Watch folder skipped file because it is still locked or empty: {path}");
                }
                else
                {
                    context.Post(state =>
                    {
                        if (ReferenceEquals(sender, fileWatcher))
                        {
                            OnFileWatcherTrigger(path);
                        }
                    }, null);
                }
            }, 1000);
        }

        protected void CleanElapsedTimers()
        {
            for (int i = 0; i < timers.Count; i++)
            {
                if (timers[i].IsElapsed)
                {
                    timers.Remove(timers[i]);
                }
            }
        }

        public void Dispose()
        {
            FileSystemWatcher watcher = fileWatcher;
            fileWatcher = null;
            watcher?.Dispose();
        }
    }

    public class WatchFolderDuplicateEventTimer
    {
        private const int expireTime = 1000;

        private Stopwatch timer;
        private string path;

        public bool IsElapsed
        {
            get
            {
                return timer.ElapsedMilliseconds >= expireTime;
            }
        }

        public WatchFolderDuplicateEventTimer(string path)
        {
            timer = Stopwatch.StartNew();
            this.path = path;
        }

        public bool IsDuplicateEvent(string path)
        {
            bool result = path == this.path && !IsElapsed;
            if (result)
            {
                timer = Stopwatch.StartNew();
            }
            return result;
        }
    }
}