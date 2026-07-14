using EasyDesk.Core.Models;

namespace EasyDesk.Core
{
    /// <summary>
    /// Cursor information capture.
    /// Thread-safe — read-only operations with no shared mutable state.
    /// </summary>
    public interface ICursorCapturer
    {
        /// <summary>
        /// Get the cursor screen position.
        /// </summary>
        void GetCursorPosition(out int x, out int y);

        /// <summary>
        /// Get full cursor info: position, hotspot, and AND/XOR mask bitmap.
        /// </summary>
        CursorInfo GetCursorInfo();
    }
}
