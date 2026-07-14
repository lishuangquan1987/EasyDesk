namespace EasyDesk.Core.Models
{
    /// <summary>
    /// Desktop monitor bounds in virtual screen coordinates.
    /// </summary>
    public class DesktopBounds
    {
        /// <summary>Top-left X in virtual desktop coordinates.</summary>
        public int X;

        /// <summary>Top-left Y in virtual desktop coordinates.</summary>
        public int Y;

        /// <summary>Monitor width in pixels.</summary>
        public int Width;

        /// <summary>Monitor height in pixels.</summary>
        public int Height;

        /// <summary>Whether this is the primary display.</summary>
        public bool IsPrimary;
    }
}
