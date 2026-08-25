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

        /// <summary>
        /// Copies a rectangle from the source DC to the destination DC with scaling.
        /// Used to capture the screen directly at the encode resolution, avoiding a
        /// full-resolution pixel buffer plus a slow managed downscale.
        /// </summary>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool StretchBlt(
            IntPtr hdcDest,
            int nXOriginDest, int nYOriginDest,
            int nWidthDest, int nHeightDest,
            IntPtr hdcSrc,
            int nXOriginSrc, int nYOriginSrc,
            int nWidthSrc, int nHeightSrc,
            uint dwRop);

        /// <summary>Sets the stretching mode of a DC (COLORONCOLOR = fastest).</summary>
        [DllImport(DllName)]
        public static extern int SetStretchBltMode(IntPtr hdc, int iStretchMode);

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

        /// <summary>
        /// Creates a 32bpp top-down DIB section in system memory. GDI renders
        /// directly into system memory (no video-memory round trip), and the caller
        /// reads ppvBits directly — far faster than CreateCompatibleBitmap + GetDIBits
        /// on virtualized/software-rendered GDI (e.g. VMware SVGA on XP).
        /// </summary>
        [DllImport(DllName)]
        public static extern IntPtr CreateDIBSection(
            IntPtr hdc,
            ref BITMAPINFO pbmi,
            uint usage,
            out IntPtr ppvBits,
            IntPtr hSection,
            uint offset);

        /// <summary>
        /// Flushes the GDI batch so pending drawing (e.g. StretchBlt) is visible
        /// when reading DIB section memory directly.
        /// </summary>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GdiFlush();
    }
}
