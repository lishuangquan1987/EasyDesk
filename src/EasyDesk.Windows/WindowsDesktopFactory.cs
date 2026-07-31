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
            // Win8+ (6.2+) 优先使用 DXGI Desktop Duplication（net40/netstandard2.0 均启用）。
            // DXGI 直接从 GPU 读桌面，1-5ms/帧；GDI BitBlt 在 Aero 下 30-50ms/帧，
            // 是远程桌面延迟的主要来源之一。XP 无 DXGI，回退 BitBlt。
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
            return new WindowsScreenCapturer();
        }

        /// <summary>
        /// 创建最佳的视频编码器。
        /// Win8+ → H.264 MediaFoundation 硬件编码（Win7 无 MF H.264 MFT）；回退 → Baseline
        /// </summary>
        public IVideoEncoder CreateVideoEncoder()
        {
#if NET40
            if (Environment.OSVersion.Version.Major > 6 ||
                (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 2))
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
