using EasyDesk.Core.Models;

namespace EasyDesk.Core
{
    /// <summary>
    /// 可选能力接口：捕获后端是否支持"脏矩形增量"读取。
    /// 镜像驱动（XDDM）在绘图事件时记录脏矩形，本接口让调用方只处理变化区域，
    /// 避免 BitBlt/DXGI 的整帧处理开销。
    /// 
    /// 设计说明（方案 X）：为保持 `IScreenCapturer` 向后兼容（net40 无接口默认方法），
    /// 不把本方法直接加入 `IScreenCapturer`，而是作为独立可选接口。调用方用
    /// `capturer as ICaptureChangesReader` 检测：支持则走增量路径，否则回退整帧。
    /// BitBlt / DXGI 实现不实现本接口，返回 null；镜像驱动实现之。
    /// </summary>
    public interface ICaptureChangesReader
    {
        /// <summary>
        /// 读取自上次调用以来画面发生变化的矩形区域。
        /// </summary>
        /// <param name="rects">变化区域数组；无变化时为空数组（非 null）。</param>
        /// <returns>true = 有变化（rects 至少含一个区域）；false = 无变化。</returns>
        /// <remarks>
        /// 实现约定：本方法应支持"无变化帧"语义——调用方可能以固定节奏轮询，
        /// 若画面未变化应返回 false 且 rects 为空，供调用方跳过编码。内部需用
        /// 环形缓冲累积脏矩形；若缓冲溢出（变化过快）可合并为全屏矩形或在
        /// rects 中返回一个覆盖全帧的矩形。
        /// </remarks>
        bool TryReadChanges(out ScreenRect[] rects);
    }
}
