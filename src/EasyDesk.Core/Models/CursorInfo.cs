namespace EasyDesk.Core.Models
{
    /// <summary>
    /// Full cursor information: screen position, hotspot, and raw AND/XOR mask bitmap.
    /// </summary>
    public class CursorInfo
    {
        /// <summary>Cursor screen X coordinate.</summary>
        public int X;

        /// <summary>Cursor screen Y coordinate.</summary>
        public int Y;

        /// <summary>Hotspot X position relative to cursor image top-left.</summary>
        public int HotspotX;

        /// <summary>Hotspot Y position relative to cursor image top-left.</summary>
        public int HotspotY;

        /// <summary>Cursor image width in pixels.</summary>
        public int Width;

        /// <summary>Cursor image height in pixels.</summary>
        public int Height;

        /// <summary>
        /// Raw cursor image data in Windows cursor format:
        /// [AND mask bytes] + [XOR mask bytes].
        /// AND mask: 1 bit per pixel (monochrome), rows padded to 2-byte boundary.
        /// XOR mask: 32 bits per pixel (BGRA), same row stride as AND mask.
        /// Total length = (AND stride * Height) + (XOR stride * Height).
        /// </summary>
        public byte[] ImageData;
    }
}
