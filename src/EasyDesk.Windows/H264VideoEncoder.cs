#if NET40

using System;
using EasyDesk.Core;

namespace EasyDesk.Windows
{
    /// <summary>
    /// H.264 视频编码器（架构占位）。
    /// 
    /// 当前 MediaFoundation H.264 MFT 集成需要 SharpDX 4.0.1 对应的
    /// SetInputType/SetOutputType/ProcessInput/ProcessOutput 调用，
    /// 实测 SharpDX.MediaFoundation 4.0.1 的 API 签名与此版本不完全兼容，
    /// 需要单独进行 MFT COM 互操作适配。
    /// 
    /// 此占位保留完整的编码器架构，初始化时自动回退到 BaselineVideoEncoder。
    /// H.264 编码器可以提供 10-50x 的带宽节省，是长期演进的关键路径。
    /// </summary>
    public class H264VideoEncoder : IVideoEncoder
    {
        public string Name { get { return "H.264 (hardware)"; } }

        public bool Initialize(int width, int height, int framerate)
        {
            // TODO: 实现 SharpDX.MediaFoundation MFT 编码器
            // 参考 SharpDX 4.0.1 的 API:
            //   var encoder = new SharpDX.MediaFoundation.Transform(...)
            //   encoder.SetInputType(0, inputType, 0)
            //   encoder.SetOutputType(0, outputType, 0)
            //   encoder.ProcessInput(0, sample, 0)
            //   encoder.ProcessOutput(...)
            //   MediaFactory.CreateSample()
            //   MediaFactory.CreateMemoryBuffer()
            //   MediaManager.Startup() / MediaManager.Shutdown()
            return false; // 暂未实现，回退到基线编码器
        }

        public EncodedFrame EncodeFrame(byte[] bgraPixels, int width, int height, bool forceKeyframe)
        {
            // 不应被调用（Initialize 返回 false 时系统使用基线）
            return new EncodedFrame
            {
                IsKeyframe = forceKeyframe,
                Data = bgraPixels,
                FrameIndex = 0
            };
        }

        public void Dispose()
        {
        }
    }
}

#endif
