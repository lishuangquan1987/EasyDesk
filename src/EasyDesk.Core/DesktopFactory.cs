namespace EasyDesk.Core
{
    /// <summary>
    /// Abstract factory for creating platform-specific desktop I/O implementations.
    /// Each platform provides its own factory (e.g. WindowsDesktopFactory).
    /// </summary>
    public interface DesktopFactory
    {
        IInputSimulator CreateInputSimulator();
        IScreenCapturer CreateScreenCapturer();
        ICursorCapturer CreateCursorCapturer();
        IClipboardService CreateClipboardService();
        IDesktopInfo CreateDesktopInfo();
    }
}
