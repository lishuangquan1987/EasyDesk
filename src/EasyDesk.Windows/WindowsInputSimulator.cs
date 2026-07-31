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
        private static readonly int InputStructSize = Marshal.SizeOf(typeof(INPUT));
        // SendInput 进程级共享，多会话并发调用非线程安全；串行化所有注入。
        private static readonly object SendLock = new object();

        /// <summary>
        /// Send a single INPUT and throw if it fails.
        /// </summary>
        private static void SendInputChecked(INPUT input, string context)
        {
            var inputs = new INPUT[1];
            inputs[0] = input;
            uint result;
            lock (SendLock)
            {
                result = User32.SendInput(1, inputs, InputStructSize);
            }
            if (result == 0)
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("SendInput ({0}) failed. Win32 error: {1}", context, error));
            }
        }

        public void SendMouseMove(int x, int y, bool absolute)
        {
            var input = new INPUT();
            input.type = Win32Constants.INPUT_MOUSE;
            input.mkhi.mi.mouseData = 0;
            input.mkhi.mi.dwFlags = (uint)MouseEventFlags.Move;
            if (absolute)
            {
                // SendInput 的绝对坐标范围是 0~65535。旧实现只用主显示器（SM_CXSCREEN）
                // 且未设 VIRTUALDESK：副屏坐标不可达。改用虚拟桌面范围 + 原点偏移，
                // 并用 MOUSEEVENTF_VIRTUALDESK 让映射覆盖所有显示器。
                int virtualX = User32.GetSystemMetrics(Win32Constants.SM_XVIRTUALSCREEN);
                int virtualY = User32.GetSystemMetrics(Win32Constants.SM_YVIRTUALSCREEN);
                int virtualW = User32.GetSystemMetrics(Win32Constants.SM_CXVIRTUALSCREEN);
                int virtualH = User32.GetSystemMetrics(Win32Constants.SM_CYVIRTUALSCREEN);
                // 用 (范围-1) 做除数让最右/最下像素可达（dx=65535）
                input.mkhi.mi.dx = ((x - virtualX) * 65535) / Math.Max(virtualW - 1, 1);
                input.mkhi.mi.dy = ((y - virtualY) * 65535) / Math.Max(virtualH - 1, 1);
                input.mkhi.mi.dwFlags |= (uint)MouseEventFlags.Absolute;
                input.mkhi.mi.dwFlags |= (uint)MouseEventFlags.VirtualDesk;
            }
            else
            {
                input.mkhi.mi.dx = x;
                input.mkhi.mi.dy = y;
            }
            input.mkhi.mi.time = 0;
            input.mkhi.mi.dwExtraInfo = IntPtr.Zero;

            SendInputChecked(input, "mouse move");
        }

        public void SendMouseButton(MouseButton button, bool down)
        {
            uint flags = GetMouseButtonFlags(button, down);
            if (flags == 0) return;

            var input = new INPUT();
            input.type = Win32Constants.INPUT_MOUSE;
            input.mkhi.mi.dx = 0;
            input.mkhi.mi.dy = 0;
            input.mkhi.mi.mouseData = GetMouseButtonData(button);
            input.mkhi.mi.dwFlags = flags;
            input.mkhi.mi.time = 0;
            input.mkhi.mi.dwExtraInfo = IntPtr.Zero;

            SendInputChecked(input, "mouse button");
        }

        public void SendMouseWheel(int delta)
        {
            var input = new INPUT();
            input.type = Win32Constants.INPUT_MOUSE;
            input.mkhi.mi.dx = 0;
            input.mkhi.mi.dy = 0;
            input.mkhi.mi.mouseData = (uint)delta;
            input.mkhi.mi.dwFlags = (uint)MouseEventFlags.Wheel;
            input.mkhi.mi.time = 0;
            input.mkhi.mi.dwExtraInfo = IntPtr.Zero;

            SendInputChecked(input, "mouse wheel");
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
            var input = new INPUT();
            input.type = Win32Constants.INPUT_KEYBOARD;
            input.mkhi.ki.wVk = (ushort)key;
            input.mkhi.ki.wScan = 0;
            input.mkhi.ki.dwFlags = down
                ? (uint)KeyEventFlags.KeyDown
                : (uint)KeyEventFlags.KeyUp;
            input.mkhi.ki.time = 0;
            input.mkhi.ki.dwExtraInfo = IntPtr.Zero;

            if (IsExtendedKey(key))
            {
                input.mkhi.ki.dwFlags |= (uint)KeyEventFlags.ExtendedKey;
            }

            SendInputChecked(input, string.Format("key event VK={0}", (int)key));
        }

        public void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (char c in text)
            {
                // Send KEYDOWN
                var inputDown = new INPUT();
                inputDown.type = Win32Constants.INPUT_KEYBOARD;
                inputDown.mkhi.ki.wVk = 0;        // Must be 0 for Unicode
                inputDown.mkhi.ki.wScan = c;       // UTF-16 character
                inputDown.mkhi.ki.dwFlags = (uint)(KeyEventFlags.Unicode | KeyEventFlags.KeyDown);
                inputDown.mkhi.ki.time = 0;
                inputDown.mkhi.ki.dwExtraInfo = IntPtr.Zero;

                SendInputChecked(inputDown, string.Format("Unicode down '{0}'", c));

                // Send KEYUP (failure is non-fatal — key may already be up)
                var inputUp = new INPUT();
                inputUp.type = Win32Constants.INPUT_KEYBOARD;
                inputUp.mkhi.ki.wVk = 0;
                inputUp.mkhi.ki.wScan = c;
                inputUp.mkhi.ki.dwFlags = (uint)(KeyEventFlags.Unicode | KeyEventFlags.KeyUp);
                inputUp.mkhi.ki.time = 0;
                inputUp.mkhi.ki.dwExtraInfo = IntPtr.Zero;

                var inputsUp = new INPUT[1];
                inputsUp[0] = inputUp;
                uint resultUp;
                lock (SendLock)
                {
                    resultUp = User32.SendInput(1, inputsUp, InputStructSize);
                }
                if (resultUp == 0)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "SendInput (Unicode up '{0}') failed. Win32 error: {1}",
                        c, Marshal.GetLastWin32Error());
                }
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
