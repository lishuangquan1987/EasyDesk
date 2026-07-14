using EasyDesk.Core;

namespace EasyDesk.Windows
{
    /// <summary>
    /// Windows platform factory — creates all desktop I/O implementations.
    /// Use this as the single entry point: new WindowsDesktopFactory().CreateScreenCapturer()
    /// </summary>
    public class WindowsDesktopFactory : DesktopFactory
    {
        public IInputSimulator CreateInputSimulator()
        {
            return new WindowsInputSimulator();
        }

        public IScreenCapturer CreateScreenCapturer()
        {
            return new WindowsScreenCapturer();
        }

        public ICursorCapturer CreateCursorCapturer()
        {
            return new WindowsCursorCapturer();
        }

        public IClipboardService CreateClipboardService()
        {
            return new WindowsClipboardService();
        }

        public IDesktopInfo CreateDesktopInfo()
        {
            return new WindowsDesktopInfo();
        }
    }
}
