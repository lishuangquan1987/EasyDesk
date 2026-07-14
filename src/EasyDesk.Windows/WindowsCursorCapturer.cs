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
                    // Get bitmap info for AND mask
                    var bmi = new BITMAPINFO();
                    bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                    bmi.bmiHeader.biBitCount = 0; // request only header info

                    Gdi32.GetDIBits(
                        User32.GetDC(IntPtr.Zero),
                        ii.hbmMask, 0, 0, IntPtr.Zero, ref bmi, Win32Constants.DIB_RGB_COLORS);

                    width = Math.Abs(bmi.bmiHeader.biWidth);
                    // Height is doubled: top half = XOR, bottom half = AND
                    height = Math.Abs(bmi.bmiHeader.biHeight) / 2;
                    andStride = ((width + 15) / 16) * 2; // 1bpp, 2-byte aligned rows

                    User32.ReleaseDC(IntPtr.Zero, User32.GetDC(IntPtr.Zero));
                }

                if (width == 0 || height == 0)
                {
                    return new CursorInfo
                    {
                        X = x, Y = y,
                        HotspotX = hotspotX, HotspotY = hotspotY,
                        Width = width, Height = height,
                        ImageData = new byte[0]
                    };
                }

                // Build composite image data: AND mask + XOR mask
                // Total bytes = andStride * height + (width * 4) * height
                int xorStride = width * 4;
                int totalBytes = (andStride + xorStride) * height;
                var imageData = new byte[totalBytes];

                if (ii.hbmMask != IntPtr.Zero)
                {
                    IntPtr hdcScreen = User32.GetDC(IntPtr.Zero);
                    try
                    {
                        // Get AND mask (bottom half of the combined mask bitmap)
                        var bmiFull = new BITMAPINFO();
                        bmiFull.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                        bmiFull.bmiHeader.biWidth = width;
                        bmiFull.bmiHeader.biHeight = -(height * 2); // full combined height
                        bmiFull.bmiHeader.biPlanes = 1;
                        bmiFull.bmiHeader.biBitCount = 32;
                        bmiFull.bmiHeader.biCompression = Win32Constants.BI_RGB;

                        int fullSize = width * 4 * height * 2;
                        IntPtr fullBuffer = Marshal.AllocHGlobal(fullSize);
                        try
                        {
                            Gdi32.GetDIBits(hdcScreen, ii.hbmMask, 0, (uint)(height * 2),
                                fullBuffer, ref bmiFull, Win32Constants.DIB_RGB_COLORS);

                            // Copy AND mask from bottom half (each row: width * 4 bytes)
                            for (int row = 0; row < height; row++)
                            {
                                // Source: bottom half, row by row (BGRA32 from GetDIBits)
                                // Destination: andStride bytes per row
                                IntPtr srcRow = IntPtr.Add(fullBuffer, (height + row) * width * 4);
                                int destOffset = row * andStride;

                                // Convert BGRA32 → 1bpp AND mask
                                byte andByte = 0;
                                int bitPos = 0;
                                for (int col = 0; col < width; col++)
                                {
                                    // BGRA → check if pixel is non-zero (in AND mask, 0 = opaque, 1 = transparent)
                                    // Actually in ICONINFO, hbmMask top half is AND bits per row, 
                                    // bottom half is XOR bits as BGRA.
                                    // We need the AND mask bits (1bpp).
                                    int srcOffset = (height + row) * width * 4 + col * 4;
                                    byte pixel = Marshal.ReadByte(fullBuffer, srcOffset);

                                    // AND mask: 0 = screen pixels shown, 1 = screen pixels hidden (transparent)
                                    if (pixel != 0) // non-black in AND bitmap = transparent in cursor
                                    {
                                        andByte |= (byte)(1 << (7 - bitPos));
                                    }

                                    bitPos++;
                                    if (bitPos == 8 || col == width - 1)
                                    {
                                        imageData[destOffset + (bitPos - 1) / 8] = andByte;
                                        andByte = 0;
                                        bitPos = 0;
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
                    }
                    finally
                    {
                        User32.ReleaseDC(IntPtr.Zero, hdcScreen);
                    }
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
                // Clean up icon bitmaps
                if (ii.hbmMask != IntPtr.Zero) Gdi32.DeleteObject(ii.hbmMask);
                if (ii.hbmColor != IntPtr.Zero) Gdi32.DeleteObject(ii.hbmColor);
            }
        }
    }
}
