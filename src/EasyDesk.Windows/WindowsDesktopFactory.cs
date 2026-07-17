using System;
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
#if NET40
            // Win8+ (6.2+) 优先使用 DXGI Desktop Duplication
            if (Environment.OSVersion.Version.Major > 6 ||
                (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 2))
            {
                try
                {
                    var dxgi = new DxgiScreenCapturer();
                    return dxgi;
                }
                catch
                {
                    // DXGI 初始化失败（如无 GPU、远程桌面等），降级到 BitBlt
                }
            }
#endif
            return new WindowsScreenCapturer();
        }

        /// <summary>
        /// 创建最佳的视频编码器。
        /// Win7+ → H.264 MediaFoundation 硬件编码；回退 → Baseline (Zlib/JPEG)
        /// </summary>
        public IVideoEncoder CreateVideoEncoder()
        {
#if NET40
            if (Environment.OSVersion.Version.Major > 6 ||
                (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 1))
            {
                try
                {
                    return new H264VideoEncoder();
                }
                catch { }
            }
#endif
            return new BaselineVideoEncoder();
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
