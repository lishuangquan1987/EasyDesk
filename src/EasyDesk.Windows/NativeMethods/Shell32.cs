using System;
using System.Runtime.InteropServices;

namespace EasyDesk.Windows.NativeMethods
{
    /// <summary>
    /// Shell32 P/Invoke declarations for file drag/drop and clipboard CF_HDROP operations.
    /// </summary>
    internal static class Shell32
    {
        private const string DllName = "shell32.dll";

        /// <summary>
        /// Retrieve file names from CF_HDROP handle.
        /// If iFile = 0xFFFFFFFF, returns the count of files; otherwise copies the file name to lpszFile.
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint DragQueryFile(IntPtr hDrop, uint iFile, System.Text.StringBuilder lpszFile, uint cch);

        /// <summary>
        /// Release the CF_HDROP handle allocated by the system.
        /// </summary>
        [DllImport(DllName)]
        public static extern void DragFinish(IntPtr hDrop);
    }
}
