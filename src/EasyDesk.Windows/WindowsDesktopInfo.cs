using System;
using EasyDesk.Core;
using EasyDesk.Core.Models;
using EasyDesk.Windows.NativeMethods;

namespace EasyDesk.Windows
{
    /// <summary>
    /// Windows desktop geometry information.
    /// Thread-safe — read-only queries, no mutable state.
    /// </summary>
    public class WindowsDesktopInfo : IDesktopInfo
    {
        public DesktopBounds GetPrimaryBounds()
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

        public DesktopBounds[] GetAllBounds()
        {
            // Reuse the screen capturer's monitor enumeration
            var capturer = new WindowsScreenCapturer();
            return capturer.GetAllScreens();
        }

        public DesktopBounds GetVirtualScreenBounds()
        {
            int x = User32.GetSystemMetrics(Win32Constants.SM_XVIRTUALSCREEN);
            int y = User32.GetSystemMetrics(Win32Constants.SM_YVIRTUALSCREEN);
            int width = User32.GetSystemMetrics(Win32Constants.SM_CXVIRTUALSCREEN);
            int height = User32.GetSystemMetrics(Win32Constants.SM_CYVIRTUALSCREEN);

            return new DesktopBounds
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                IsPrimary = false
            };
        }
    }
}
