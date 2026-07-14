using EasyDesk.Core.Models;

namespace EasyDesk.Core
{
    /// <summary>
    /// Mouse and keyboard input simulation.
    /// Thread-safe — SendInput is an atomic Win32 API.
    /// </summary>
    public interface IInputSimulator
    {
        /// <summary>
        /// Move the mouse cursor.
        /// </summary>
        /// <param name="x">X coordinate. When absolute=true, mapped to 0-65535 virtual desktop.</param>
        /// <param name="y">Y coordinate. When absolute=true, mapped to 0-65535 virtual desktop.</param>
        /// <param name="absolute">true for absolute positioning, false for relative delta.</param>
        void SendMouseMove(int x, int y, bool absolute);

        /// <summary>
        /// Press or release a mouse button.
        /// </summary>
        /// <param name="button">Which button (Left, Right, Middle, X1, X2).</param>
        /// <param name="down">true to press, false to release.</param>
        void SendMouseButton(MouseButton button, bool down);

        /// <summary>
        /// Scroll the mouse wheel.
        /// </summary>
        /// <param name="delta">Positive = up (WHEEL_DELTA=120 per notch), negative = down.</param>
        void SendMouseWheel(int delta);

        /// <summary>
        /// Press a keyboard key.
        /// </summary>
        /// <param name="key">Windows Virtual-Key code.</param>
        void SendKeyDown(VirtualKeyCode key);

        /// <summary>
        /// Release a keyboard key.
        /// </summary>
        /// <param name="key">Windows Virtual-Key code.</param>
        void SendKeyUp(VirtualKeyCode key);

        /// <summary>
        /// Send Unicode text as keystrokes (KEYEVENTF_UNICODE).
        /// Does not depend on current keyboard layout or IME.
        /// </summary>
        /// <param name="text">The text to type.</param>
        void SendText(string text);
    }
}
