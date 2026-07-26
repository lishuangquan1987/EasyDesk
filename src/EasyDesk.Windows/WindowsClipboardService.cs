using System;
using System.Runtime.InteropServices;
using System.Text;
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

            if (!OpenClipboardWithRetry(IntPtr.Zero))
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

            if (!OpenClipboardWithRetry(IntPtr.Zero))
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

        // ── File clipboard (CF_HDROP) ──

        /// <summary>
        /// Check whether the clipboard contains files (CF_HDROP format).
        /// 不会阻塞：IsClipboardFormatAvailable 是即时的查询，无需打开剪贴板。
        /// </summary>
        public bool ContainsFiles()
        {
            return User32.IsClipboardFormatAvailable(Win32Constants.CF_HDROP);
        }

        /// <summary>
        /// Get the list of file paths from the clipboard (CF_HDROP format).
        /// Returns null if clipboard does not contain files or OpenClipboard fails.
        /// </summary>
        public string[] GetFileList()
        {
            if (!User32.IsClipboardFormatAvailable(Win32Constants.CF_HDROP))
                return null;

            if (!OpenClipboardWithRetry(IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("OpenClipboard failed. Win32 error: {0}", error));
            }

            try
            {
                IntPtr hDrop = User32.GetClipboardData(Win32Constants.CF_HDROP);
                if (hDrop == IntPtr.Zero)
                    return null;

                // hDrop 由剪贴板拥有，不要 free。DragFinish 会自动释放。
                // 查询文件数量：iFile = 0xFFFFFFFF
                uint fileCount = Shell32.DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                if (fileCount == 0)
                    return new string[0];

                var result = new string[fileCount];
                for (uint i = 0; i < fileCount; i++)
                {
                    // 先查长度（lpszFile=null）
                    uint len = Shell32.DragQueryFile(hDrop, i, null, 0);
                    if (len == 0)
                    {
                        result[i] = "";
                        continue;
                    }
                    var sb = new StringBuilder((int)len + 1);
                    Shell32.DragQueryFile(hDrop, i, sb, (uint)sb.Capacity);
                    result[i] = sb.ToString();
                }
                return result;
            }
            finally
            {
                User32.CloseClipboard();
            }
        }

        /// <summary>
        /// Set file paths to the clipboard (CF_HDROP format).
        /// 构造 DROPFILES 结构 + 文件路径列表（UTF-16，双 null 终止），通过 SetClipboardData 写入。
        /// </summary>
        public void SetFiles(string[] filePaths)
        {
            if (filePaths == null || filePaths.Length == 0)
                throw new ArgumentException("filePaths is null or empty");

            // 构造 DROPFILES 结构 + 文件路径（UTF-16，每个以 \0 结尾，最后额外一个 \0）
            // DROPFILES: 20 bytes (pFiles=0, pt.x=0, pt.y=0, fNC=0, fWide=1)
            const int DROPFILES_SIZE = 20;
            int totalChars = 1; // 结尾额外 \0
            for (int i = 0; i < filePaths.Length; i++)
            {
                totalChars += filePaths[i].Length + 1; // 路径 + \0
            }
            int byteCount = DROPFILES_SIZE + totalChars * 2; // UTF-16 = 2 bytes/char

            IntPtr hGlobal = Kernel32.GlobalAlloc(Win32Constants.GHND, (UIntPtr)(uint)byteCount);
            if (hGlobal == IntPtr.Zero)
                throw new OutOfMemoryException("GlobalAlloc failed for CF_HDROP");

            IntPtr pGlobal = Kernel32.GlobalLock(hGlobal);
            if (pGlobal == IntPtr.Zero)
            {
                Kernel32.GlobalFree(hGlobal);
                throw new InvalidOperationException("GlobalLock failed for CF_HDROP");
            }

            try
            {
                // 写 DROPFILES 结构（20 字节）
                // pFiles = 文件名列表相对于结构开始的偏移量 = sizeof(DROPFILES) = 20
                Marshal.WriteInt32(pGlobal, 0, DROPFILES_SIZE);  // pFiles = 20
                Marshal.WriteInt32(pGlobal, 4, 0);               // pt.x = 0
                Marshal.WriteInt32(pGlobal, 8, 0);               // pt.y = 0
                Marshal.WriteInt32(pGlobal, 12, 0);              // fNC = 0 (client coords)
                Marshal.WriteInt32(pGlobal, 16, 1);              // fWide = 1 (Unicode)

                // 写文件路径（UTF-16，每个 \0 终止，结尾额外 \0）
                int offset = DROPFILES_SIZE;
                for (int i = 0; i < filePaths.Length; i++)
                {
                    string path = filePaths[i];
                    char[] chars = path.ToCharArray();
                    Marshal.Copy(chars, 0, (IntPtr)(pGlobal.ToInt64() + offset), chars.Length);
                    offset += chars.Length * 2;
                    // 写 \0 终止符
                    Marshal.WriteInt16((IntPtr)(pGlobal.ToInt64() + offset), 0);
                    offset += 2;
                }
                // 结尾额外 \0 已由 GMEM_ZEROINIT 保证
            }
            finally
            {
                Kernel32.GlobalUnlock(hGlobal);
            }

            // 设置到剪贴板
            if (!OpenClipboardWithRetry(IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                Kernel32.GlobalFree(hGlobal);
                throw new InvalidOperationException(
                    string.Format("OpenClipboard failed. Win32 error: {0}", error));
            }

            try
            {
                if (!User32.EmptyClipboard())
                {
                    var error = Marshal.GetLastWin32Error();
                    Kernel32.GlobalFree(hGlobal);
                    throw new InvalidOperationException(
                        string.Format("EmptyClipboard failed. Win32 error: {0}", error));
                }

                IntPtr result = User32.SetClipboardData(Win32Constants.CF_HDROP, hGlobal);
                if (result == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    Kernel32.GlobalFree(hGlobal); // 失败时我们仍拥有 hGlobal
                    throw new InvalidOperationException(
                        string.Format("SetClipboardData failed for CF_HDROP. Win32 error: {0}", error));
                }
                // 成功后剪贴板拥有 hGlobal，不要 free
            }
            finally
            {
                User32.CloseClipboard();
            }
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

        /// <summary>
        /// OpenClipboard with retry logic. Win32 error 5 (ACCESS_DENIED) occurs
        /// when another process has the clipboard open. Retry up to 10 times
        /// with 50ms delay before giving up.
        /// </summary>
        private static bool OpenClipboardWithRetry(IntPtr hWndOwner)
        {
            for (int i = 0; i < 10; i++)
            {
                if (User32.OpenClipboard(hWndOwner))
                    return true;
                // Win32 error 5 = ACCESS_DENIED (clipboard locked by another process)
                // Win32 error 0 = success (shouldn't happen if return is false, but just in case)
                int error = Marshal.GetLastWin32Error();
                if (error != 5 && error != 0)
                    return false; // Other errors don't benefit from retry
                System.Threading.Thread.Sleep(50);
            }
            return false;
        }

        // ── Image clipboard (CF_DIB) ──

        /// <summary>
        /// Check whether the clipboard contains an image (CF_DIB format).
        /// 即时查询，不会阻塞。
        /// </summary>
        public bool ContainsImage()
        {
            return User32.IsClipboardFormatAvailable(Win32Constants.CF_DIB);
        }

        /// <summary>
        /// Get the raw CF_DIB bytes from the clipboard.
        /// 返回 BITMAPINFOHEADER + 可选颜色表 + 像素数据的原始字节。
        /// 失败返回 null。
        /// </summary>
        public byte[] GetImageDibBytes()
        {
            if (!User32.IsClipboardFormatAvailable(Win32Constants.CF_DIB))
                return null;

            if (!OpenClipboardWithRetry(IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("OpenClipboard failed. Win32 error: {0}", error));
            }

            try
            {
                IntPtr hGlobal = User32.GetClipboardData(Win32Constants.CF_DIB);
                if (hGlobal == IntPtr.Zero)
                    return null;

                // 查询全局内存大小
                UIntPtr sizePtr = Kernel32.GlobalSize(hGlobal);
                int size = (int)(uint)sizePtr;
                if (size <= 0)
                    return null;

                IntPtr pGlobal = Kernel32.GlobalLock(hGlobal);
                if (pGlobal == IntPtr.Zero)
                    return null;

                try
                {
                    byte[] data = new byte[size];
                    Marshal.Copy(pGlobal, data, 0, size);
                    return data;
                }
                finally
                {
                    Kernel32.GlobalUnlock(hGlobal);
                }
            }
            finally
            {
                User32.CloseClipboard();
            }
        }

        /// <summary>
        /// Set CF_DIB raw bytes to the clipboard.
        /// dibBytes 必须是有效的 CF_DIB 数据（BITMAPINFOHEADER + 像素数据）。
        /// </summary>
        public void SetImageDibBytes(byte[] dibBytes)
        {
            if (dibBytes == null || dibBytes.Length == 0)
                throw new ArgumentException("dibBytes is null or empty");

            IntPtr hGlobal = Kernel32.GlobalAlloc(Win32Constants.GHND, (UIntPtr)(uint)dibBytes.Length);
            if (hGlobal == IntPtr.Zero)
                throw new OutOfMemoryException("GlobalAlloc failed for CF_DIB");

            IntPtr pGlobal = Kernel32.GlobalLock(hGlobal);
            if (pGlobal == IntPtr.Zero)
            {
                Kernel32.GlobalFree(hGlobal);
                throw new InvalidOperationException("GlobalLock failed for CF_DIB");
            }

            try
            {
                Marshal.Copy(dibBytes, 0, pGlobal, dibBytes.Length);
            }
            finally
            {
                Kernel32.GlobalUnlock(hGlobal);
            }

            if (!OpenClipboardWithRetry(IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                Kernel32.GlobalFree(hGlobal);
                throw new InvalidOperationException(
                    string.Format("OpenClipboard failed. Win32 error: {0}", error));
            }

            try
            {
                if (!User32.EmptyClipboard())
                {
                    var error = Marshal.GetLastWin32Error();
                    Kernel32.GlobalFree(hGlobal);
                    throw new InvalidOperationException(
                        string.Format("EmptyClipboard failed. Win32 error: {0}", error));
                }

                IntPtr result = User32.SetClipboardData(Win32Constants.CF_DIB, hGlobal);
                if (result == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    Kernel32.GlobalFree(hGlobal);
                    throw new InvalidOperationException(
                        string.Format("SetClipboardData failed for CF_DIB. Win32 error: {0}", error));
                }
                // 成功后剪贴板拥有 hGlobal，不要 free
            }
            finally
            {
                User32.CloseClipboard();
            }
        }
    }
}
