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
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ShareX
{
    // Windows 11 (and Windows 10 when turned on) opens its own screen capture on a bare Print Screen press.
    // RegisterHotKey still succeeds in that case but never receives the key, and turning the Windows setting off
    // only applies after the next sign in. So SunShot catches bare Print Screen with a low level keyboard hook
    // before Windows does. The Windows setting itself is never changed.
    // Ctrl, Alt and Shift + Print Screen are not taken by Windows and keep using normal hotkeys.
    public sealed class PrintScreenKeyHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105, WM_QUIT = 0x0012;
        private const int VK_SNAPSHOT = 0x2C, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
        private const uint PM_NOREMOVE = 0;

        // One snapshot of what a bare Print Screen press should do, swapped as a whole from the UI thread.
        private sealed class Targets
        {
            public static readonly Targets None = new Targets(0, null, false);

            public readonly ushort HotkeyID;
            public readonly Action Editor;
            public readonly bool Suspended;

            public Targets(ushort hotkeyID, Action editor, bool suspended)
            {
                HotkeyID = hotkeyID;
                Editor = editor;
                Suspended = suspended;
            }
        }

        private readonly Control owner;
        private readonly Action<ushort> hotkeyPressed;
        private readonly Func<bool> isBlocked;
        private readonly uint processId = (uint)Environment.ProcessId;
        private readonly HookProc hookProc;
        private readonly ManualResetEventSlim started = new ManualResetEventSlim();
        private readonly Thread thread;
        private volatile uint threadId;
        private volatile bool running, stopping;
        private volatile Targets targets = Targets.None;

        // Only touched on the hook thread.
        private bool printDown, swallowedDown;

        public bool IsRunning => running;

        // isBlocked runs on the hook thread and must stay fast. When it returns true the press goes to Windows.
        public PrintScreenKeyHook(Control owner, Action<ushort> hotkeyPressed, Func<bool> isBlocked)
        {
            this.owner = owner;
            this.hotkeyPressed = hotkeyPressed;
            this.isBlocked = isBlocked;
            hookProc = HookCallback;

            thread = new Thread(Run) { IsBackground = true, Name = "Print Screen key" };
            thread.Start();
            started.Wait(2000);
        }

        public void SetTargets(ushort hotkeyID, Action editor, bool suspended)
        {
            targets = new Targets(hotkeyID, editor, suspended);
        }

        private void Run()
        {
            IntPtr hookHandle = IntPtr.Zero;

            try
            {
                // Make sure this thread has a message queue before anyone can post WM_QUIT to it.
                PeekMessage(out MSG _, IntPtr.Zero, 0, 0, PM_NOREMOVE);
                threadId = GetCurrentThreadId();

                // Low level hooks need a message loop on the installing thread. A dedicated thread keeps key presses
                // fast even while the main window is busy, so Windows never drops the hook for being slow.
                if (!stopping)
                {
                    hookHandle = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, NativeMethods.GetModuleHandle(null), 0);
                    running = hookHandle != IntPtr.Zero;

                    if (!running)
                    {
                        DebugHelper.WriteLine("Print Screen key hook could not be installed.");
                    }
                }
            }
            finally
            {
                started.Set();
            }

            if (hookHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                while (!stopping && GetMessage(out MSG _, IntPtr.Zero, 0, 0) > 0)
                {
                }
            }
            finally
            {
                running = false;
                NativeMethods.UnhookWindowsHookEx(hookHandle);
                GC.KeepAlive(hookProc);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && Marshal.ReadInt32(lParam) == VK_SNAPSHOT)
            {
                int message = (int)wParam;

                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    if (!printDown)
                    {
                        printDown = true;
                        swallowedDown = false;

                        if (!IsModifierDown())
                        {
                            Targets current = targets;

                            if (current.Editor != null)
                            {
                                // A waiting hotkey box only gets the key while SunShot is the active app.
                                if (IsOwnWindowActive())
                                {
                                    swallowedDown = true;
                                    Post(current.Editor);
                                }
                            }
                            else if (!current.Suspended && current.HotkeyID != 0 && !IsBlockedSafe())
                            {
                                swallowedDown = true;
                                ushort id = current.HotkeyID;
                                Post(() => hotkeyPressed(id));
                            }
                        }
                    }

                    // Key repeat follows whatever the first press did.
                    if (swallowedDown)
                    {
                        return (IntPtr)1;
                    }
                }
                else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                {
                    bool swallowUp = swallowedDown;
                    printDown = false;
                    swallowedDown = false;

                    if (swallowUp)
                    {
                        return (IntPtr)1;
                    }
                }
            }

            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private bool IsOwnWindowActive()
        {
            IntPtr window = GetForegroundWindow();
            return window != IntPtr.Zero && GetWindowThreadProcessId(window, out uint id) != 0 && id == processId;
        }

        private bool IsBlockedSafe()
        {
            try
            {
                return isBlocked != null && isBlocked();
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
                return false;
            }
        }

        private static bool IsModifierDown()
        {
            return IsKeyDown(VK_SHIFT) || IsKeyDown(VK_CONTROL) || IsKeyDown(VK_MENU) || IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN);
        }

        private static bool IsKeyDown(int key)
        {
            return (GetAsyncKeyState(key) & 0x8000) != 0;
        }

        private void Post(Action action)
        {
            try
            {
                if (owner != null && !owner.IsDisposed && owner.IsHandleCreated)
                {
                    owner.BeginInvoke(action);
                }
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
            }
        }

        public void Dispose()
        {
            stopping = true;
            targets = Targets.None;
            started.Wait(2000);

            for (int i = 0; i < 20 && thread.IsAlive; i++)
            {
                if (threadId != 0 && PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero) && thread.Join(500))
                {
                    break;
                }

                thread.Join(50);
            }

            if (thread.IsAlive)
            {
                DebugHelper.WriteLine("Print Screen key hook thread did not stop.");
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostThreadMessage(uint idThread, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
