using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using EasyDesk.Core;
using EasyDesk.Core.Models;
using EasyDesk.Windows.NativeMethods;

namespace EasyDesk.Windows
{
    /// <summary>
    /// Windows screen capturer using GDI+ (CopyFromScreen / BitBlt).
    /// Not thread-safe — internal DC state must be guarded externally.
    /// </summary>
    public class WindowsScreenCapturer : IScreenCapturer
    {
        // 枚举显示器使用无状态复用实例，避免捕获热路径（~60fps）每次分配新对象
        private static readonly WindowsScreenCapturer EnumerationInstance = new WindowsScreenCapturer();

        public ScreenFrame CaptureScreen()
        {
            return CaptureScreen(CaptureOptions.Default);
        }

        public ScreenFrame CaptureScreen(CaptureOptions options)
        {
            if (options == null)
                options = CaptureOptions.Default;

            int x, y, width, height;
            GetCaptureBounds(options.TargetDisplay, out x, out y, out width, out height);

            var frame = CaptureRegionInternal(x, y, width, height);

            // Draw cursor if requested (simplified — full cursor blending needs per-frame capture)
            if (options.IncludeCursor)
            {
                // Cursor drawing omitted in this version; CopyFromScreen does not include cursor
                // by default. For cursor inclusion, use DXGI Desktop Duplication API.
            }

            return frame;
        }

        public ScreenFrame CaptureRegion(int x, int y, int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException("width");
            if (height <= 0) throw new ArgumentOutOfRangeException("height");

            return CaptureRegionInternal(x, y, width, height);
        }

        private ScreenFrame CaptureRegionInternal(int x, int y, int width, int height)
        {
            int stride = width * 4;
            int totalBytes = stride * height;
            IntPtr pixelBuffer = Marshal.AllocHGlobal(totalBytes);
            if (pixelBuffer == IntPtr.Zero)
                throw new OutOfMemoryException("Failed to allocate pixel buffer for screen capture.");

            try
            {
                // Get desktop DC
                IntPtr hdcScreen = User32.GetDC(IntPtr.Zero);
                if (hdcScreen == IntPtr.Zero)
                    throw new InvalidOperationException("GetDC returned null.");

                try
                {
                    // Create compatible DC and bitmap
                    IntPtr hdcMem = Gdi32.CreateCompatibleDC(hdcScreen);
                    if (hdcMem == IntPtr.Zero)
                        throw new InvalidOperationException("CreateCompatibleDC failed.");

                    try
                    {
                        IntPtr hOldBitmap = IntPtr.Zero;
                        IntPtr hBitmap = Gdi32.CreateCompatibleBitmap(hdcScreen, width, height);
                        if (hBitmap == IntPtr.Zero)
                            throw new InvalidOperationException("CreateCompatibleBitmap failed.");

                        try
                        {
                            hOldBitmap = Gdi32.SelectObject(hdcMem, hBitmap);
                            if (hOldBitmap == IntPtr.Zero)
                            {
                                var selError = Marshal.GetLastWin32Error();
                                throw new InvalidOperationException(
                                    string.Format("SelectObject failed. Win32 error: {0}", selError));
                            }

                            // BitBlt from screen to memory DC
                            uint rop = Win32Constants.SRCCOPY | Win32Constants.CAPTUREBLT;
                            bool bitbltOk = Gdi32.BitBlt(
                                hdcMem, 0, 0, width, height,
                                hdcScreen, x, y, rop);

                            if (!bitbltOk)
                            {
                                var error = Marshal.GetLastWin32Error();
                                throw new InvalidOperationException(
                                    string.Format("BitBlt failed. Win32 error: {0}", error));
                            }

                            // Get pixel data via GetDIBits
                            var bmi = new BITMAPINFO();
                            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                            bmi.bmiHeader.biWidth = width;
                            bmi.bmiHeader.biHeight = -height; // negative = top-down
                            bmi.bmiHeader.biPlanes = 1;
                            bmi.bmiHeader.biBitCount = 32;
                            bmi.bmiHeader.biCompression = Win32Constants.BI_RGB;
                            bmi.bmiHeader.biSizeImage = (uint)totalBytes;

                            int result = Gdi32.GetDIBits(
                                hdcMem, hBitmap, 0, (uint)height,
                                pixelBuffer, ref bmi, Win32Constants.DIB_RGB_COLORS);

                            if (result == 0)
                            {
                                var error = Marshal.GetLastWin32Error();
                                throw new InvalidOperationException(
                                    string.Format("GetDIBits failed. Win32 error: {0}", error));
                            }
                        }
                        finally
                        {
                            // 先还原选择，再删除位图（Win32 规则：选入 DC 的对象 DeleteObject 返回 FALSE，
                            // 异常路径若不还原会泄漏 GDI 句柄，累计达 1 万上限即全屏渲染失败）。
                            if (hOldBitmap != IntPtr.Zero)
                            {
                                try { Gdi32.SelectObject(hdcMem, hOldBitmap); } catch { }
                            }
                            Gdi32.DeleteObject(hBitmap);
                        }
                    }
                    finally
                    {
                        Gdi32.DeleteDC(hdcMem);
                    }
                }
                finally
                {
                    User32.ReleaseDC(IntPtr.Zero, hdcScreen);
                }

                return new ScreenFrame
                {
                    Scan0 = pixelBuffer,
                    Width = width,
                    Height = height,
                    Stride = stride,
                    PixelFormat = 0
                };
            }
            catch
            {
                Marshal.FreeHGlobal(pixelBuffer);
                throw;
            }
        }

        public DesktopBounds GetPrimaryScreen()
        {
            return new DesktopBounds
            {
                X = 0,
                Y = 0,
                Width = User32.GetSystemMetrics(Win32Constants.SM_CXSCREEN),
                Height = User32.GetSystemMetrics(Win32Constants.SM_CYSCREEN),
                IsPrimary = true
            };
        }

        public DesktopBounds[] GetAllScreens()
        {
            var bounds = new List<DesktopBounds>();

            GCHandle handle = GCHandle.Alloc(bounds);
            try
            {
                User32.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdc, ref RECT rc, IntPtr lParam) =>
                {
                    var list = (List<DesktopBounds>)GCHandle.FromIntPtr(lParam).Target;
                    var mi = new MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                    if (User32.GetMonitorInfo(hMonitor, ref mi))
                    {
                        list.Add(new DesktopBounds
                        {
                            X = mi.rcMonitor.left,
                            Y = mi.rcMonitor.top,
                            Width = mi.rcMonitor.right - mi.rcMonitor.left,
                            Height = mi.rcMonitor.bottom - mi.rcMonitor.top,
                            IsPrimary = (mi.dwFlags & Win32Constants.MONITORINFOF_PRIMARY) != 0
                        });
                    }
                    return true;
                };

                User32.EnumDisplayMonitors(
                    IntPtr.Zero, IntPtr.Zero, callback, GCHandle.ToIntPtr(handle));
            }
            finally
            {
                handle.Free();
            }

            return bounds.ToArray();
        }

        private static void GetCaptureBounds(
            int targetDisplay, out int x, out int y, out int width, out int height)
        {
            if (targetDisplay < 0)
            {
                // Entire virtual desktop
                x = User32.GetSystemMetrics(Win32Constants.SM_XVIRTUALSCREEN);
                y = User32.GetSystemMetrics(Win32Constants.SM_YVIRTUALSCREEN);
                width = User32.GetSystemMetrics(Win32Constants.SM_CXVIRTUALSCREEN);
                height = User32.GetSystemMetrics(Win32Constants.SM_CYVIRTUALSCREEN);
                return;
            }

            DesktopBounds[] screens = EnumerationInstance.GetAllScreens();

            // 0 = primary monitor：EnumDisplayMonitors 的枚举顺序不保证主屏在前，
            // 必须按 MONITORINFOF_PRIMARY 显式查找，否则多显示器时捕获到副屏。
            if (targetDisplay == 0)
            {
                foreach (DesktopBounds s in screens)
                {
                    if (s.IsPrimary)
                    {
                        x = s.X;
                        y = s.Y;
                        width = s.Width;
                        height = s.Height;
                        return;
                    }
                }
                throw new ArgumentOutOfRangeException("Primary monitor not found");
            }

            // 1, 2, ... = 第 (targetDisplay-1) 个副屏（按枚举顺序，主屏不计入）
            int secondarySeen = 0;
            int secondaryIndex = targetDisplay - 1;
            foreach (DesktopBounds s in screens)
            {
                if (!s.IsPrimary)
                {
                    if (secondarySeen == secondaryIndex)
                    {
                        x = s.X;
                        y = s.Y;
                        width = s.Width;
                        height = s.Height;
                        return;
                    }
                    secondarySeen++;
                }
            }
            throw new ArgumentOutOfRangeException(
                string.Format("Monitor index {0} out of range (found {1} secondary monitors).",
                    targetDisplay, secondarySeen));
        }
    }
}
