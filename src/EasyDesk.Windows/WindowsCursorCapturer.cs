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
                int width = 0;
                int height = 0;
                int andStride = 0;

                // 光标类型判定（Win32 ICONINFO 约定）：
                // - 颜色光标（现代 Windows 桌面默认箭头带阴影即属此类）：hbmColor=颜色 XOR
                //   位图（高度=光标高），hbmMask=仅 AND 掩码（高度=光标高）。
                // - 黑白光标：hbmColor=NULL，hbmMask=AND+XOR 双倍高（上半 AND、下半 XOR）。
                // 旧实现无条件对高度 /2 且只读 hbmMask，颜色光标被当成黑白光标处理，
                // 抓出来的是半高 + 错位像素（默认箭头即垃圾数据）。
                bool hasColor = ii.hbmColor != IntPtr.Zero;
                IntPtr xorBitmap = hasColor ? ii.hbmColor : ii.hbmMask;

                if (xorBitmap != IntPtr.Zero)
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
                        // 查询位图尺寸（XOR 源位图）
                        var bmiQuery = new BITMAPINFO();
                        bmiQuery.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                        bmiQuery.bmiHeader.biBitCount = 0;

                        Gdi32.GetDIBits(
                            hdc, xorBitmap, 0, 0, IntPtr.Zero, ref bmiQuery, Win32Constants.DIB_RGB_COLORS);

                        width = Math.Abs(bmiQuery.bmiHeader.biWidth);
                        int fullHeight = Math.Abs(bmiQuery.bmiHeader.biHeight);
                        // 颜色光标：hbmColor 高度即光标高；黑白光标：hbmMask 双倍高
                        height = hasColor ? fullHeight : fullHeight / 2;
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

                        // 读 XOR 掩码（32bpp top-down DIB，行 0 在顶）
                        var bmiXor = new BITMAPINFO();
                        bmiXor.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                        bmiXor.bmiHeader.biWidth = width;
                        bmiXor.bmiHeader.biHeight = -height;
                        bmiXor.bmiHeader.biPlanes = 1;
                        bmiXor.bmiHeader.biBitCount = 32;
                        bmiXor.bmiHeader.biCompression = Win32Constants.BI_RGB;

                        int xorSize = width * 4 * height;
                        IntPtr xorBuffer = Marshal.AllocHGlobal(xorSize);
                        try
                        {
                            int xorLines = Gdi32.GetDIBits(hdc, xorBitmap, 0, (uint)height,
                                xorBuffer, ref bmiXor, Win32Constants.DIB_RGB_COLORS);

                            if (xorLines == 0)
                            {
                                return new CursorInfo
                                {
                                    X = x, Y = y,
                                    HotspotX = hotspotX, HotspotY = hotspotY,
                                    Width = 0, Height = 0,
                                    ImageData = new byte[0]
                                };
                            }

                            // XOR 掩码 → imageData 后半段（每行 andStride 之后的 xorStride 字节）
                            for (int row = 0; row < height; row++)
                            {
                                int destOffset = (andStride * height) + row * xorStride;
                                int srcOffset = row * width * 4;
                                for (int col = 0; col < width * 4; col++)
                                    imageData[destOffset + col] =
                                        Marshal.ReadByte(xorBuffer, srcOffset + col);
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(xorBuffer);
                        }

                        // 读 AND 掩码：颜色光标读 hbmMask（height 行）；黑白光标读 hbmMask 上半
                        var bmiAnd = new BITMAPINFO();
                        bmiAnd.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                        bmiAnd.bmiHeader.biWidth = width;
                        bmiAnd.bmiHeader.biHeight = -height;
                        bmiAnd.bmiHeader.biPlanes = 1;
                        bmiAnd.bmiHeader.biBitCount = 32;
                        bmiAnd.bmiHeader.biCompression = Win32Constants.BI_RGB;

                        int andSize = width * 4 * height;
                        IntPtr andBuffer = Marshal.AllocHGlobal(andSize);
                        try
                        {
                            int andLines = Gdi32.GetDIBits(hdc, ii.hbmMask, 0, (uint)height,
                                andBuffer, ref bmiAnd, Win32Constants.DIB_RGB_COLORS);

                            if (andLines == 0)
                            {
                                return new CursorInfo
                                {
                                    X = x, Y = y,
                                    HotspotX = hotspotX, HotspotY = hotspotY,
                                    Width = 0, Height = 0,
                                    ImageData = new byte[0]
                                };
                            }

                            // AND 掩码 → imageData 前半段（BGRA32 → 1bpp，AND=1 → 透明）
                            for (int row = 0; row < height; row++)
                            {
                                int destOffset = row * andStride;
                                for (int col = 0; col < width; col++)
                                {
                                    int srcOffset = row * width * 4 + col * 4;
                                    byte pixel = Marshal.ReadByte(andBuffer, srcOffset);
                                    if (pixel != 0) // AND=1 → transparent
                                    {
                                        int byteIndex = col / 8;
                                        int bitInByte = 7 - (col % 8);
                                        imageData[destOffset + byteIndex] |= (byte)(1 << bitInByte);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(andBuffer);
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

                // hbmMask 与 hbmColor 均为 null — 无光标图像可用
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
