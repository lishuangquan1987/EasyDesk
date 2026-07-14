namespace EasyDesk.Core.Models
{
    /// <summary>
    /// Options for screen capture.
    /// </summary>
    public class CaptureOptions
    {
        /// <summary>Whether to include the mouse cursor in the captured image. Default: true.</summary>
        public bool IncludeCursor;

        /// <summary>
        /// Target monitor index for capture.
        /// -1 = entire virtual desktop (default).
        /// 0 = primary monitor.
        /// 1, 2, ... = secondary monitors in EnumDisplayMonitors order.
        /// </summary>
        public int TargetDisplay;

        /// <summary>
        /// Creates default capture options: include cursor, entire virtual desktop.
        /// </summary>
        public static CaptureOptions Default
        {
            get
            {
                return new CaptureOptions
                {
                    IncludeCursor = true,
                    TargetDisplay = -1
                };
            }
        }
    }
}
