using System;
using System.Runtime.InteropServices;

namespace EasyDesk.Windows.NativeMethods
{
    // ── INPUT structures (for SendInput) ──

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;        // INPUT_MOUSE=0, INPUT_KEYBOARD=1, INPUT_HARDWARE=2
        public MOUSEKEYBDHARDWAREINPUT mkhi;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct MOUSEKEYBDHARDWAREINPUT
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    // ── Cursor structures ──

    [StructLayout(LayoutKind.Sequential)]
    internal struct CURSORINFO
    {
        public int cbSize;       // sizeof(CURSORINFO)
        public uint flags;       // CURSOR_SHOWING=1
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ICONINFO
    {
        public bool fIcon;       // true=icon, false=cursor
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;   // AND mask (monochrome)
        public IntPtr hbmColor;  // XOR mask (color)
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x;
        public int y;
    }

    // ── Display structures ──

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public int Width { get { return right - left; } }
        public int Height { get { return bottom - top; } }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        public int cbSize;       // sizeof(MONITORINFO)
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;     // MONITORINFOF_PRIMARY=1
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    // ── Bitmap structures ──

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;     // positive = bottom-up, negative = top-down
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        // RGBQUAD bmiColors[1] follows — not used for 32bpp
    }

    // ── Constants ──

    internal static class Win32Constants
    {
        public const uint INPUT_MOUSE = 0;
        public const uint INPUT_KEYBOARD = 1;
        public const uint INPUT_HARDWARE = 2;

        public const uint CURSOR_SHOWING = 0x00000001;

        public const uint MONITORINFOF_PRIMARY = 0x00000001;
        public const uint MONITOR_DEFAULTTONULL = 0x00000000;
        public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // GetSystemMetrics indices
        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;
        public const int SM_XVIRTUALSCREEN = 76;
        public const int SM_YVIRTUALSCREEN = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;

        // BitBlt raster operations
        public const uint SRCCOPY = 0x00CC0020;
        public const uint CAPTUREBLT = 0x40000000;

        // DIB colors
        public const uint DIB_RGB_COLORS = 0;
        public const uint BI_RGB = 0;

        // Clipboard formats
        public const uint CF_UNICODETEXT = 13;
        /// <summary>文件列表剪贴板格式（右键复制文件时使用）。</summary>
        public const uint CF_HDROP = 15;
        /// <summary>设备无关位图剪贴板格式（截图/复制图片时使用）。</summary>
        public const uint CF_DIB = 8;

        // Global memory flags
        public const uint GMEM_MOVEABLE = 0x0002;
        public const uint GMEM_ZEROINIT = 0x0040;
        public const uint GHND = GMEM_MOVEABLE | GMEM_ZEROINIT;
    }
}
