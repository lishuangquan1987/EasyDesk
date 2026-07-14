using System;

namespace EasyDesk.Core.Models
{
    /// <summary>
    /// Keyboard event flags — maps directly to KEYBDINPUT.dwFlags.
    /// These can be combined with bitwise OR (e.g. KeyDown | ExtendedKey).
    /// </summary>
    [Flags]
    public enum KeyEventFlags : uint
    {
        /// <summary>Key is being pressed (if not set, key is being released).</summary>
        KeyDown = 0x0000,

        /// <summary>Key is being released (if KeyDown is also set, this means nothing).</summary>
        KeyUp = 0x0002,

        /// <summary>Extended key (right Alt, right Ctrl, Ins, Del, arrows, etc.).</summary>
        ExtendedKey = 0x0001,

        /// <summary>Use scan code instead of virtual key code.</summary>
        ScanCode = 0x0008,

        /// <summary>Send as Unicode character (wVk must be 0, wScan is UTF-16 char).</summary>
        Unicode = 0x0004
    }
}
