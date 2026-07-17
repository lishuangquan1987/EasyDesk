using System;

namespace EasyDesk.Core
{
    /// <summary>
    /// 视频编码器接口——将原始帧编码为压缩视频数据。
    /// 长期目标是接入 H.264/H.265 硬件编码，当前用 Zlib/JPEG 基线实现。
    /// </summary>
    public interface IVideoEncoder : IDisposable
    {
        /// <summary>初始化编码器。返回 true 表示正常，false 表示不支持回退到基线。</summary>
        bool Initialize(int width, int height, int framerate);

        /// <summary>编码一帧。返回编码后的帧数据（可直接传输）。</summary>
        EncodedFrame EncodeFrame(byte[] bgraPixels, int width, int height, bool forceKeyframe);

        /// <summary>编码器名称（用于日志/诊断）</summary>
        string Name { get; }
    }

    /// <summary>
    /// 编码后的视频帧。
    /// </summary>
    public struct EncodedFrame
    {
        /// <summary>是否为关键帧（I 帧）</summary>
        public bool IsKeyframe;

        /// <summary>编码后的数据</summary>
        public byte[] Data;

        /// <summary>原始帧序号</summary>
        public int FrameIndex;
    }
}
