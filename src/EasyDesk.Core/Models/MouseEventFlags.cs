using System;

namespace EasyDesk.Core.Models
{
    /// <summary>
    /// Mouse event flags — maps directly to MOUSEINPUT.dwFlags.
    /// These can be combined with bitwise OR (e.g. Move | Absolute).
    /// </summary>
    [Flags]
    public enum MouseEventFlags : uint
    {
        /// <summary>Movement occurred.</summary>
        Move = 0x0001,

        /// <summary>Left button went down.</summary>
        LeftDown = 0x0002,

        /// <summary>Left button went up.</summary>
        LeftUp = 0x0004,

        /// <summary>Right button went down.</summary>
        RightDown = 0x0008,

        /// <summary>Right button went up.</summary>
        RightUp = 0x0010,

        /// <summary>Middle button went down.</summary>
        MiddleDown = 0x0020,

        /// <summary>Middle button went up.</summary>
        MiddleUp = 0x0040,

        /// <summary>X button went down.</summary>
        XDown = 0x0080,

        /// <summary>X button went up.</summary>
        XUp = 0x0100,

        /// <summary>Vertical wheel moved.</summary>
        Wheel = 0x0800,

        /// <summary>Horizontal wheel moved.</summary>
        HWheel = 0x1000,

        /// <summary>Absolute position (0-65535 mapped to virtual desktop).</summary>
        Absolute = 0x8000,

        /// <summary>Coordinates map to the entire virtual desktop (all monitors).</summary>
        VirtualDesk = 0x4000
    }
}
