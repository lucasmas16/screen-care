using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenCare
{
    internal sealed class CoverMotion
    {
        private readonly Point origin;
        internal CoverMotion(Point start) { origin = start; }
        internal bool Moved(Point point)
        { return Math.Abs((long)point.X - origin.X) >= 4 || Math.Abs((long)point.Y - origin.Y) >= 4; }
    }

    internal sealed class CoverLastInput
    {
        private bool known;
        private uint baseline;
        internal void Begin(bool valid, uint time) { known = valid; baseline = time; }
        internal bool Observe(uint time)
        {
            if (!known) { Begin(true, time); return false; }
            // LASTINPUTINFO can go backwards; small forward deltas also work
            // when the 32-bit Windows tick counter wraps.
            return unchecked((int)(time - baseline)) > 0;
        }
    }

    // Installed only for a black cover. Never consume input, retain keys or do
    // I/O in a native callback. Cleanup runs later on the normal UI queue.
    internal sealed class CoverEscape : IDisposable
    {
        private readonly Control dispatcher;
        private readonly Action<string> exit;
        private readonly bool ignoreInjected;
        private readonly CoverMotion motion = new CoverMotion(Cursor.Position);
        private readonly Native.HookProc keyboardProc, mouseProc;
        private IntPtr keyboard, mouse;
        private bool pending, disposed;
        internal bool Installed { get { return keyboard != IntPtr.Zero && mouse != IntPtr.Zero; } }
        internal CoverEscape(Control target, Action<string> onExit, bool ignoreSimulated)
        {
            dispatcher = target; exit = onExit; ignoreInjected = ignoreSimulated;
            keyboardProc = Keyboard; mouseProc = Mouse;
            try
            {
                IntPtr module = Native.GetModuleHandle(null);
                keyboard = Native.SetWindowsHookEx(13, keyboardProc, module, 0);
                if (keyboard == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                mouse = Native.SetWindowsHookEx(14, mouseProc, module, 0);
                if (mouse == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            catch { Dispose(); throw; }
        }
        private IntPtr Keyboard(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && data != IntPtr.Zero && (!ignoreInjected || (Marshal.ReadInt32(data, 8) & 0x10) == 0))
                Queue("cover-keyboard");
            return Native.CallNextHookEx(IntPtr.Zero, code, message, data);
        }
        private IntPtr Mouse(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && data != IntPtr.Zero && (!ignoreInjected || (Marshal.ReadInt32(data, 12) & 1) == 0))
            {
                int kind = message.ToInt32();
                if (kind != 0x0200 || motion.Moved(new Point(Marshal.ReadInt32(data), Marshal.ReadInt32(data, 4))))
                    Queue("cover-mouse");
            }
            return Native.CallNextHookEx(IntPtr.Zero, code, message, data);
        }
        private void Queue(string source)
        {
            if (pending || disposed || dispatcher.IsDisposed || !dispatcher.IsHandleCreated) return;
            pending = true;
            try { dispatcher.BeginInvoke((Action)delegate { if (!disposed) exit(source); }); }
            catch (InvalidOperationException) { pending = false; }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (keyboard != IntPtr.Zero) { Native.UnhookWindowsHookEx(keyboard); keyboard = IntPtr.Zero; }
            if (mouse != IntPtr.Zero) { Native.UnhookWindowsHookEx(mouse); mouse = IntPtr.Zero; }
        }
    }
}
