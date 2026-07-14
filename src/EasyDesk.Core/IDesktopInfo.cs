using EasyDesk.Core.Models;

namespace EasyDesk.Core
{
    /// <summary>
    /// Desktop geometry information. Read-only, thread-safe.
    /// </summary>
    public interface IDesktopInfo
    {
        /// <summary>
        /// Get the primary monitor bounds.
        /// </summary>
        DesktopBounds GetPrimaryBounds();

        /// <summary>
        /// Get bounds for all connected monitors.
        /// </summary>
        DesktopBounds[] GetAllBounds();

        /// <summary>
        /// Get the bounding rectangle of the entire virtual desktop (min X, min Y, max width, max height).
        /// </summary>
        DesktopBounds GetVirtualScreenBounds();
    }
}
