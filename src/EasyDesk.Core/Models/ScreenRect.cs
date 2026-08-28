namespace EasyDesk.Core.Models
{
    /// <summary>
    /// 屏幕矩形区域（像素坐标，相对于所在帧/屏幕的左上角）。
    /// 用于镜像驱动等支持"脏矩形增量"的捕获后端：描述需要更新的画面区域，
    /// 调用方只对该区域重新编码/渲染，避免整帧处理。
    /// </summary>
    public struct ScreenRect
    {
        /// <summary>矩形左上角 X 坐标（像素）。</summary>
        public int X;

        /// <summary>矩形左上角 Y 坐标（像素）。</summary>
        public int Y;

        /// <summary>矩形宽度（像素）。</summary>
        public int Width;

        /// <summary>矩形高度（像素）。</summary>
        public int Height;
    }
}
