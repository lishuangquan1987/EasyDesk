using System;
using System.Runtime.InteropServices;

namespace EasyDesk.Windows.NativeMethods
{
    internal static class Kernel32
    {
        private const string DllName = "kernel32.dll";

        [DllImport(DllName, EntryPoint = "RtlMoveMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        [DllImport(DllName, SetLastError = true)]
        public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport(DllName, SetLastError = true)]
        public static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport(DllName, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport(DllName, SetLastError = true)]
        public static extern UIntPtr GlobalSize(IntPtr hMem);

        [DllImport(DllName, SetLastError = true)]
        public static extern IntPtr GlobalFree(IntPtr hMem);
    }
}
