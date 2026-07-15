using System;
using System.Runtime.InteropServices;
using EasyDesk.Core;
using EasyDesk.Core.Models;
using EasyDesk.Windows.NativeMethods;

namespace EasyDesk.Windows
{
    /// <summary>
    /// Windows cursor capturer using GetCursorInfo / GetIconInfo.
    /// Thread-safe — read-only, no shared mutable state.
    /// </summary>
    public class WindowsCursorCapturer : ICursorCapturer
    {
        public void GetCursorPosition(out int x, out int y)
        {
            POINT pt;
            if (!User32.GetCursorPos(out pt))
            {
                x = 0;
                y = 0;
                return;
            }
            x = pt.x;
            y = pt.y;
        }

        public CursorInfo GetCursorInfo()
        {
            var ci = new CURSORINFO();
            ci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));

            if (!User32.GetCursorInfo(ref ci))
            {
                // Cursor info unavailable, return default
                return new CursorInfo
                {
                    X = 0, Y = 0,
                    HotspotX = 0, HotspotY = 0,
                    Width = 0, Height = 0,
                    ImageData = new byte[0]
                };
            }

            int x = ci.ptScreenPos.x;
            int y = ci.ptScreenPos.y;

            if (ci.hCursor == IntPtr.Zero || (ci.flags & Win32Constants.CURSOR_SHOWING) == 0)
            {
                return new CursorInfo
                {
                    X = x, Y = y,
                    HotspotX = 0, HotspotY = 0,
                    Width = 0, Height = 0,
                    ImageData = new byte[0]
                };
            }

            ICONINFO ii;
            if (!User32.GetIconInfo(ci.hCursor, out ii))
            {
                return new CursorInfo
                {
                    X = x, Y = y,
                    HotspotX = 0, HotspotY = 0,
                    Width = 0, Height = 0,
                    ImageData = new byte[0]
                };
            }

            try
            {
                int hotspotX = ii.xHotspot;
                int hotspotY = ii.yHotspot;

                // Get bitmap dimensions from AND mask
                int width = 0;
                int height = 0;
                int andStride = 0;

                if (ii.hbmMask != IntPtr.Zero)
                {
                    IntPtr hdc = User32.GetDC(IntPtr.Zero);
                    if (hdc == IntPtr.Zero)
                    {
                        return new CursorInfo
                        {
                            X = x, Y = y,
                            HotspotX = hotspotX, HotspotY = hotspotY,
                            Width = 0, Height = 0,
                            ImageData = new byte[0]
                        };
                    }
                    try
                    {
                        // Query bitmap dimensions
                        var bmiQuery = new BITMAPINFO();
                        bmiQuery.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                        bmiQuery.bmiHeader.biBitCount = 0;

                        Gdi32.GetDIBits(
                            hdc, ii.hbmMask, 0, 0, IntPtr.Zero, ref bmiQuery, Win32Constants.DIB_RGB_COLORS);

                        width = Math.Abs(bmiQuery.bmiHeader.biWidth);
                        height = Math.Abs(bmiQuery.bmiHeader.biHeight) / 2; // doubled: XOR + AND
                        andStride = ((width + 15) / 16) * 2; // 1bpp, 2-byte aligned

                        if (width == 0 || height == 0)
                        {
                            return new CursorInfo
                            {
                                X = x, Y = y,
                                HotspotX = hotspotX, HotspotY = hotspotY,
                                Width = 0, Height = 0,
                                ImageData = new byte[0]
                            };
                        }

                        // Build composite image: AND mask (1bpp) + XOR mask (BGRA32)
                        int xorStride = width * 4;
                        int totalBytes = (andStride + xorStride) * height;
                        var imageData = new byte[totalBytes];

                        // Read the combined mask as 32bpp top-down DIB
                        var bmiFull = new BITMAPINFO();
                        bmiFull.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                        bmiFull.bmiHeader.biWidth = width;
                        bmiFull.bmiHeader.biHeight = -(height * 2);
                        bmiFull.bmiHeader.biPlanes = 1;
                        bmiFull.bmiHeader.biBitCount = 32;
                        bmiFull.bmiHeader.biCompression = Win32Constants.BI_RGB;

                        int fullSize = width * 4 * height * 2;
                        IntPtr fullBuffer = Marshal.AllocHGlobal(fullSize);
                        try
                        {
                            int scanLines = Gdi32.GetDIBits(hdc, ii.hbmMask, 0, (uint)(height * 2),
                                fullBuffer, ref bmiFull, Win32Constants.DIB_RGB_COLORS);

                            if (scanLines == 0)
                            {
                                return new CursorInfo
                                {
                                    X = x, Y = y,
                                    HotspotX = hotspotX, HotspotY = hotspotY,
                                    Width = 0, Height = 0,
                                    ImageData = new byte[0]
                                };
                            }

                            // Extract AND mask from bottom half (BGRA32 → 1bpp)
                            for (int row = 0; row < height; row++)
                            {
                                int destOffset = row * andStride;
                                for (int col = 0; col < width; col++)
                                {
                                    int srcOffset = (height + row) * width * 4 + col * 4;
                                    byte pixel = Marshal.ReadByte(fullBuffer, srcOffset);
                                    if (pixel != 0) // AND=1 → transparent
                                    {
                                        int byteIndex = col / 8;
                                        int bitInByte = 7 - (col % 8);
                                        imageData[destOffset + byteIndex] |= (byte)(1 << bitInByte);
                                    }
                                }
                            }

                            // Copy XOR mask from top half
                            for (int row = 0; row < height; row++)
                            {
                                int destOffset = (andStride * height) + row * xorStride;
                                int srcOffset = row * width * 4;
                                for (int col = 0; col < width * 4; col++)
                                {
                                    imageData[destOffset + col] =
                                        Marshal.ReadByte(fullBuffer, srcOffset + col);
                                }
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(fullBuffer);
                        }

                        return new CursorInfo
                        {
                            X = x, Y = y,
                            HotspotX = hotspotX, HotspotY = hotspotY,
                            Width = width, Height = height,
                            ImageData = imageData
                        };
                    }
                    finally
                    {
                        User32.ReleaseDC(IntPtr.Zero, hdc);
                    }
                }

                // hbmMask is null — no cursor image available
                return new CursorInfo
                {
                    X = x, Y = y,
                    HotspotX = hotspotX, HotspotY = hotspotY,
                    Width = 0, Height = 0,
                    ImageData = new byte[0]
                };
            }
            finally
            {
                // Clean up icon bitmaps
                if (ii.hbmMask != IntPtr.Zero) Gdi32.DeleteObject(ii.hbmMask);
                if (ii.hbmColor != IntPtr.Zero) Gdi32.DeleteObject(ii.hbmColor);
            }
        }
    }
}
