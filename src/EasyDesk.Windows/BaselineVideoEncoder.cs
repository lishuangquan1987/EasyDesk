using System;
using EasyDesk.Core;

namespace EasyDesk.Windows
{
    /// <summary>
    /// 基线视频编码器——使用 Zlib/JPEG 逐帧编码，作为 H.264 不可用时的回退方案。
    /// </summary>
    public class BaselineVideoEncoder : IVideoEncoder
    {
        private int _frameIndex;
        private int _keyframeInterval = 30;

        public string Name { get { return "Baseline (Zlib/JPEG)"; } }

        public bool Initialize(int width, int height, int framerate)
        {
            _frameIndex = 0;
            return true; // 永远可用
        }

        public EncodedFrame EncodeFrame(byte[] bgraPixels, int width, int height, bool forceKeyframe)
        {
            bool isKey = forceKeyframe || (_frameIndex % _keyframeInterval == 0);
            int idx = _frameIndex++;

            // 用当前已有的压缩逻辑（通过静态引用）
            // 这里直接返回原始数据，CaptureEngine 会处理实际压缩
            return new EncodedFrame
            {
                IsKeyframe = isKey,
                Data = bgraPixels,
                FrameIndex = idx
            };
        }

        public void Dispose()
        {
        }
    }
}
