using System;
using System.Runtime.InteropServices;
using EasyDesk.Core;
using EasyDesk.Windows.NativeMethods;

namespace EasyDesk.Windows
{
    /// <summary>
    /// Windows clipboard service using raw clipboard API (zero WinForms dependency).
    /// NOT thread-safe. Must be called from a STA thread.
    /// </summary>
    public class WindowsClipboardService : IClipboardService
    {
        public string GetText()
        {
            if (!IsClipboardFormatAvailable(Win32Constants.CF_UNICODETEXT))
                return null;

            if (!User32.OpenClipboard(IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("OpenClipboard failed. Win32 error: {0}", error));
            }

            try
            {
                IntPtr hData = User32.GetClipboardData(Win32Constants.CF_UNICODETEXT);
                if (hData == IntPtr.Zero)
                    return null;

                IntPtr pData = Kernel32.GlobalLock(hData);
                if (pData == IntPtr.Zero)
                    return null;

                try
                {
                    return Marshal.PtrToStringUni(pData);
                }
                finally
                {
                    Kernel32.GlobalUnlock(hData);
                }
            }
            finally
            {
                User32.CloseClipboard();
            }
        }

        public void SetText(string text)
        {
            if (text == null) throw new ArgumentNullException("text");

            if (!User32.OpenClipboard(IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("OpenClipboard failed. Win32 error: {0}", error));
            }

            try
            {
                if (!User32.EmptyClipboard())
                {
                    var error = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException(
                        string.Format("EmptyClipboard failed. Win32 error: {0}", error));
                }

                // Allocate global memory for Unicode string + null terminator
                int byteCount = (text.Length + 1) * 2; // UTF-16, each char = 2 bytes
                IntPtr hGlobal = Kernel32.GlobalAlloc(Win32Constants.GHND, (UIntPtr)(uint)byteCount);
                if (hGlobal == IntPtr.Zero)
                    throw new OutOfMemoryException("GlobalAlloc failed for clipboard text.");

                IntPtr pGlobal = Kernel32.GlobalLock(hGlobal);
                if (pGlobal == IntPtr.Zero)
                {
                    Kernel32.GlobalFree(hGlobal);
                    throw new InvalidOperationException("GlobalLock failed for clipboard text.");
                }

                try
                {
                    Marshal.Copy(text.ToCharArray(), 0, pGlobal, text.Length);
                    // Null terminator already zero-initialized by GMEM_ZEROINIT
                }
                finally
                {
                    Kernel32.GlobalUnlock(hGlobal);
                }

                IntPtr result = User32.SetClipboardData(Win32Constants.CF_UNICODETEXT, hGlobal);
                if (result == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    Kernel32.GlobalFree(hGlobal); // We still own it if SetClipboardData fails
                    throw new InvalidOperationException(
                        string.Format("SetClipboardData failed. Win32 error: {0}", error));
                }
                // On success, clipboard now owns hGlobal — do NOT free it
            }
            finally
            {
                User32.CloseClipboard();
            }
        }

        public bool ContainsText()
        {
            return IsClipboardFormatAvailable(Win32Constants.CF_UNICODETEXT);
        }

        private static bool IsClipboardFormatAvailable(uint format)
        {
            // Try up to 10 times with 50ms delay — clipboard may be locked by another app
            for (int i = 0; i < 10; i++)
            {
                if (User32.IsClipboardFormatAvailable(format))
                    return true;
                System.Threading.Thread.Sleep(50);
            }
            return false;
        }
    }
}
