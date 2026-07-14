using System;

namespace EasyDesk.Core.Models
{
    /// <summary>
    /// Captured screen frame. Contains a raw BGRA32 pixel buffer pointer and metadata.
    /// </summary>
    /// <remarks>
    /// LIFECYCLE: The caller MUST free Scan0 by calling Marshal.FreeHGlobal(Scan0)
    /// when done. Failing to do so causes a memory leak of Width * Height * 4 bytes.
    /// EasyDesk does NOT own or track the pixel buffer lifetime.
    /// </remarks>
    public class ScreenFrame
    {
        /// <summary>Pointer to the pixel data (BGRA32 format). Must be freed by caller.</summary>
        public IntPtr Scan0;

        /// <summary>Frame width in pixels.</summary>
        public int Width;

        /// <summary>Frame height in pixels.</summary>
        public int Height;

        /// <summary>Number of bytes per row (= Width * 4).</summary>
        public int Stride;

        /// <summary>Pixel format identifier. Currently fixed at 0 (= BGRA32, 32bpp).</summary>
        public int PixelFormat;
    }
}
