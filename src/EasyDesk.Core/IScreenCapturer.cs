using EasyDesk.Core.Models;

namespace EasyDesk.Core
{
    /// <summary>
    /// Screen capture. Returns raw BGRA32 pixel data.
    /// NOT thread-safe — internal DC/Bitmap state must be serialized externally.
    /// </summary>
    public interface IScreenCapturer
    {
        /// <summary>
        /// Capture the entire virtual desktop (all monitors).
        /// The caller MUST free the returned ScreenFrame.Scan0 via Marshal.FreeHGlobal.
        /// </summary>
        ScreenFrame CaptureScreen();

        /// <summary>
        /// Capture the desktop with options (exclude cursor, target specific monitor, etc).
        /// The caller MUST free the returned ScreenFrame.Scan0 via Marshal.FreeHGlobal.
        /// </summary>
        ScreenFrame CaptureScreen(CaptureOptions options);

        /// <summary>
        /// Capture a specific rectangular region of the virtual desktop.
        /// The caller MUST free the returned ScreenFrame.Scan0 via Marshal.FreeHGlobal.
        /// </summary>
        ScreenFrame CaptureRegion(int x, int y, int width, int height);

        /// <summary>
        /// Get the primary monitor bounds.
        /// </summary>
        DesktopBounds GetPrimaryScreen();

        /// <summary>
        /// Get bounds for all connected monitors.
        /// </summary>
        DesktopBounds[] GetAllScreens();
    }
}
