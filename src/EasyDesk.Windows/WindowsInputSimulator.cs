using System;
using System.Runtime.InteropServices;
using EasyDesk.Core;
using EasyDesk.Core.Models;
using EasyDesk.Windows.NativeMethods;

namespace EasyDesk.Windows
{
    /// <summary>
    /// Windows input simulator using SendInput API.
    /// Thread-safe.
    /// </summary>
    public class WindowsInputSimulator : IInputSimulator
    {
        private const int InputStructSize = 28; // sizeof(INPUT) on 32-bit

        public void SendMouseMove(int x, int y, bool absolute)
        {
            var inputs = new INPUT[1];
            inputs[0].type = Win32Constants.INPUT_MOUSE;
            inputs[0].mkhi.mi.dx = x;
            inputs[0].mkhi.mi.dy = y;
            inputs[0].mkhi.mi.mouseData = 0;
            inputs[0].mkhi.mi.dwFlags = (uint)MouseEventFlags.Move;
            if (absolute)
                inputs[0].mkhi.mi.dwFlags |= (uint)MouseEventFlags.Absolute;
            inputs[0].mkhi.mi.time = 0;
            inputs[0].mkhi.mi.dwExtraInfo = IntPtr.Zero;

            var result = User32.SendInput(1, inputs, InputStructSize);
            if (result == 0)
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("SendInput (mouse move) failed. Win32 error: {0}", error));
            }
        }

        public void SendMouseButton(MouseButton button, bool down)
        {
            uint flags = GetMouseButtonFlags(button, down);
            if (flags == 0) return;

            var inputs = new INPUT[1];
            inputs[0].type = Win32Constants.INPUT_MOUSE;
            inputs[0].mkhi.mi.dx = 0;
            inputs[0].mkhi.mi.dy = 0;
            inputs[0].mkhi.mi.mouseData = GetMouseButtonData(button);
            inputs[0].mkhi.mi.dwFlags = flags;
            inputs[0].mkhi.mi.time = 0;
            inputs[0].mkhi.mi.dwExtraInfo = IntPtr.Zero;

            var result = User32.SendInput(1, inputs, InputStructSize);
            if (result == 0)
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("SendInput (mouse button) failed. Win32 error: {0}", error));
            }
        }

        public void SendMouseWheel(int delta)
        {
            var inputs = new INPUT[1];
            inputs[0].type = Win32Constants.INPUT_MOUSE;
            inputs[0].mkhi.mi.dx = 0;
            inputs[0].mkhi.mi.dy = 0;
            inputs[0].mkhi.mi.mouseData = (uint)delta;
            inputs[0].mkhi.mi.dwFlags = (uint)MouseEventFlags.Wheel;
            inputs[0].mkhi.mi.time = 0;
            inputs[0].mkhi.mi.dwExtraInfo = IntPtr.Zero;

            var result = User32.SendInput(1, inputs, InputStructSize);
            if (result == 0)
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("SendInput (mouse wheel) failed. Win32 error: {0}", error));
            }
        }

        public void SendKeyDown(VirtualKeyCode key)
        {
            SendKeyEvent(key, true);
        }

        public void SendKeyUp(VirtualKeyCode key)
        {
            SendKeyEvent(key, false);
        }

        private void SendKeyEvent(VirtualKeyCode key, bool down)
        {
            var inputs = new INPUT[1];
            inputs[0].type = Win32Constants.INPUT_KEYBOARD;
            inputs[0].mkhi.ki.wVk = (ushort)key;
            inputs[0].mkhi.ki.wScan = 0;
            inputs[0].mkhi.ki.dwFlags = down
                ? (uint)KeyEventFlags.KeyDown
                : (uint)KeyEventFlags.KeyUp;
            inputs[0].mkhi.ki.time = 0;
            inputs[0].mkhi.ki.dwExtraInfo = IntPtr.Zero;

            // Set ExtendedKey flag for specific keys
            if (IsExtendedKey(key))
            {
                inputs[0].mkhi.ki.dwFlags |= (uint)KeyEventFlags.ExtendedKey;
            }

            var result = User32.SendInput(1, inputs, InputStructSize);
            if (result == 0)
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("SendInput (key event) failed. VK={0}, Win32 error: {1}",
                        (int)key, error));
            }
        }

        public void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (char c in text)
            {
                // Send KEYDOWN
                var inputsDown = new INPUT[1];
                inputsDown[0].type = Win32Constants.INPUT_KEYBOARD;
                inputsDown[0].mkhi.ki.wVk = 0;        // Must be 0 for Unicode
                inputsDown[0].mkhi.ki.wScan = c;       // UTF-16 character
                inputsDown[0].mkhi.ki.dwFlags = (uint)(KeyEventFlags.Unicode | KeyEventFlags.KeyDown);
                inputsDown[0].mkhi.ki.time = 0;
                inputsDown[0].mkhi.ki.dwExtraInfo = IntPtr.Zero;

                var resultDown = User32.SendInput(1, inputsDown, InputStructSize);
                if (resultDown == 0)
                {
                    var error = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException(
                        string.Format("SendInput (Unicode down) failed. Char='{0}', Win32 error: {1}",
                            c, error));
                }

                // Send KEYUP
                var inputsUp = new INPUT[1];
                inputsUp[0].type = Win32Constants.INPUT_KEYBOARD;
                inputsUp[0].mkhi.ki.wVk = 0;
                inputsUp[0].mkhi.ki.wScan = c;
                inputsUp[0].mkhi.ki.dwFlags = (uint)(KeyEventFlags.Unicode | KeyEventFlags.KeyUp);
                inputsUp[0].mkhi.ki.time = 0;
                inputsUp[0].mkhi.ki.dwExtraInfo = IntPtr.Zero;

                User32.SendInput(1, inputsUp, InputStructSize);
                // KEYUP failures are ignored (key may already be up)
            }
        }

        private static uint GetMouseButtonFlags(MouseButton button, bool down)
        {
            switch (button)
            {
                case MouseButton.Left:
                    return down ? (uint)MouseEventFlags.LeftDown : (uint)MouseEventFlags.LeftUp;
                case MouseButton.Right:
                    return down ? (uint)MouseEventFlags.RightDown : (uint)MouseEventFlags.RightUp;
                case MouseButton.Middle:
                    return down ? (uint)MouseEventFlags.MiddleDown : (uint)MouseEventFlags.MiddleUp;
                case MouseButton.XButton1:
                case MouseButton.XButton2:
                    return down ? (uint)MouseEventFlags.XDown : (uint)MouseEventFlags.XUp;
                default:
                    return 0;
            }
        }

        private static uint GetMouseButtonData(MouseButton button)
        {
            if (button == MouseButton.XButton1) return 0x0001;  // XBUTTON1
            if (button == MouseButton.XButton2) return 0x0002;  // XBUTTON2
            return 0;
        }

        private static bool IsExtendedKey(VirtualKeyCode key)
        {
            switch (key)
            {
                case VirtualKeyCode.VK_RMENU:
                case VirtualKeyCode.VK_RCONTROL:
                case VirtualKeyCode.VK_INSERT:
                case VirtualKeyCode.VK_DELETE:
                case VirtualKeyCode.VK_HOME:
                case VirtualKeyCode.VK_END:
                case VirtualKeyCode.VK_PRIOR:   // Page Up
                case VirtualKeyCode.VK_NEXT:     // Page Down
                case VirtualKeyCode.VK_LEFT:
                case VirtualKeyCode.VK_RIGHT:
                case VirtualKeyCode.VK_UP:
                case VirtualKeyCode.VK_DOWN:
                case VirtualKeyCode.VK_NUMLOCK:
                case VirtualKeyCode.VK_SNAPSHOT: // Print Screen
                case VirtualKeyCode.VK_CANCEL:   // Ctrl+Break
                case VirtualKeyCode.VK_RWIN:
                case VirtualKeyCode.VK_APPS:     // Menu key
                case VirtualKeyCode.VK_DIVIDE:   // Numpad /
                    return true;
                default:
                    return false;
            }
        }
    }
}
