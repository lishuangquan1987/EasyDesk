using System;
using System.Runtime.InteropServices;

namespace EasyDesk.Windows.NativeMethods
{
    internal static class Gdi32
    {
        private const string DllName = "gdi32.dll";

        [DllImport(DllName)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport(DllName)]
        public static extern IntPtr CreateCompatibleBitmap(
            IntPtr hdc, int nWidth, int nHeight);

        [DllImport(DllName)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BitBlt(
            IntPtr hdcDest,
            int nXDest, int nYDest,
            int nWidth, int nHeight,
            IntPtr hdcSrc,
            int nXSrc, int nYSrc,
            uint dwRop);

        [DllImport(DllName)]
        public static extern int GetDIBits(
            IntPtr hdc,
            IntPtr hbmp,
            uint uStartScan,
            uint cScanLines,
            byte[] lpvBits,
            ref BITMAPINFO lpbi,
            uint uUsage);

        [DllImport(DllName)]
        public static extern int GetDIBits(
            IntPtr hdc,
            IntPtr hbmp,
            uint uStartScan,
            uint cScanLines,
            IntPtr lpvBits,
            ref BITMAPINFO lpbi,
            uint uUsage);
    }
}
