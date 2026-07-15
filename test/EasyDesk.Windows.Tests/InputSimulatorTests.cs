using System;
using Xunit;
using EasyDesk.Core.Models;
using EasyDesk.Windows;

namespace EasyDesk.Windows.Tests
{
    /// <summary>
    /// Tests for WindowsInputSimulator.
    /// WARNING: These tests actually move your mouse and press keys.
    /// Do not use the machine while tests are running.
    /// </summary>
    public class InputSimulatorTests
    {
        private readonly WindowsDesktopFactory _factory;

        public InputSimulatorTests()
        {
            _factory = new WindowsDesktopFactory();
        }

        [Fact]
        public void SendMouseMove_Absolute_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            // Move to center of virtual screen (32767, 32767 in 0-65535 space)
            var ex = Record.Exception(() => input.SendMouseMove(32767, 32767, true));
            Assert.Null(ex);
        }

        [Fact]
        public void SendMouseMove_Relative_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            // Move 1 pixel right and down
            var ex = Record.Exception(() => input.SendMouseMove(1, 1, false));
            Assert.Null(ex);
        }

        [Fact]
        public void SendMouseButton_LeftClick_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex1 = Record.Exception(() => input.SendMouseButton(MouseButton.Left, true));
            Assert.Null(ex1);

            System.Threading.Thread.Sleep(10);

            var ex2 = Record.Exception(() => input.SendMouseButton(MouseButton.Left, false));
            Assert.Null(ex2);
        }

        [Fact]
        public void SendMouseButton_RightClick_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex1 = Record.Exception(() => input.SendMouseButton(MouseButton.Right, true));
            Assert.Null(ex1);

            System.Threading.Thread.Sleep(10);

            var ex2 = Record.Exception(() => input.SendMouseButton(MouseButton.Right, false));
            Assert.Null(ex2);
        }

        [Fact]
        public void SendMouseWheel_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex = Record.Exception(() => input.SendMouseWheel(120));
            Assert.Null(ex);
        }

        [Fact]
        public void SendKey_Alpha_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex1 = Record.Exception(() => input.SendKeyDown(VirtualKeyCode.VK_A));
            Assert.Null(ex1);

            System.Threading.Thread.Sleep(10);

            var ex2 = Record.Exception(() => input.SendKeyUp(VirtualKeyCode.VK_A));
            Assert.Null(ex2);
        }

        [Fact]
        public void SendKey_Modifier_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            // Ctrl key
            var ex1 = Record.Exception(() => input.SendKeyDown(VirtualKeyCode.VK_CONTROL));
            Assert.Null(ex1);
            var ex2 = Record.Exception(() => input.SendKeyUp(VirtualKeyCode.VK_CONTROL));
            Assert.Null(ex2);

            // Extended keys (Right Alt)
            var ex3 = Record.Exception(() => input.SendKeyDown(VirtualKeyCode.VK_RMENU));
            Assert.Null(ex3);
            var ex4 = Record.Exception(() => input.SendKeyUp(VirtualKeyCode.VK_RMENU));
            Assert.Null(ex4);
        }

        [Fact]
        public void SendText_Ascii_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex = Record.Exception(() => input.SendText("Hello World!"));
            Assert.Null(ex);
        }

        [Fact]
        public void SendText_Unicode_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex = Record.Exception(() => input.SendText("\u4F60\u597D")); // "你好"
            Assert.Null(ex);
        }

        [Fact]
        public void SendText_Empty_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex = Record.Exception(() => input.SendText(""));
            Assert.Null(ex);
        }

        [Fact]
        public void SendText_Null_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex = Record.Exception(() => input.SendText(null));
            Assert.Null(ex);
        }

        [Fact]
        public void SendMouseButton_MiddleClick_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex1 = Record.Exception(() => input.SendMouseButton(MouseButton.Middle, true));
            Assert.Null(ex1);
            System.Threading.Thread.Sleep(10);
            var ex2 = Record.Exception(() => input.SendMouseButton(MouseButton.Middle, false));
            Assert.Null(ex2);
        }

        [Fact]
        public void SendMouseButton_XButton1_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex1 = Record.Exception(() => input.SendMouseButton(MouseButton.XButton1, true));
            Assert.Null(ex1);
            System.Threading.Thread.Sleep(10);
            var ex2 = Record.Exception(() => input.SendMouseButton(MouseButton.XButton1, false));
            Assert.Null(ex2);
        }

        [Fact]
        public void SendMouseButton_XButton2_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex1 = Record.Exception(() => input.SendMouseButton(MouseButton.XButton2, true));
            Assert.Null(ex1);
            System.Threading.Thread.Sleep(10);
            var ex2 = Record.Exception(() => input.SendMouseButton(MouseButton.XButton2, false));
            Assert.Null(ex2);
        }

        [Fact]
        public void SendKey_ExtendedKeys_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var extendedKeys = new[]
            {
                VirtualKeyCode.VK_RCONTROL,
                VirtualKeyCode.VK_INSERT,
                VirtualKeyCode.VK_DELETE,
                VirtualKeyCode.VK_HOME,
                VirtualKeyCode.VK_END,
                VirtualKeyCode.VK_PRIOR,
                VirtualKeyCode.VK_NEXT,
                VirtualKeyCode.VK_LEFT,
                VirtualKeyCode.VK_RIGHT,
                VirtualKeyCode.VK_UP,
                VirtualKeyCode.VK_DOWN,
                VirtualKeyCode.VK_NUMLOCK,
                VirtualKeyCode.VK_RWIN,
                VirtualKeyCode.VK_APPS,
                VirtualKeyCode.VK_DIVIDE,
            };

            foreach (var key in extendedKeys)
            {
                var exDown = Record.Exception(() => input.SendKeyDown(key));
                Assert.Null(exDown);
                System.Threading.Thread.Sleep(5);
                var exUp = Record.Exception(() => input.SendKeyUp(key));
                Assert.Null(exUp);
                System.Threading.Thread.Sleep(5);
            }
        }

        [Fact]
        public void SendText_SurrogatePair_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            // U+1F680 (rocket emoji) = surrogate pair: 0xD83D 0xDE80
            var ex = Record.Exception(() => input.SendText("\U0001F680"));
            Assert.Null(ex);
        }

        [Fact]
        public void SendMouseWheel_NegativeDelta_ShouldNotThrow()
        {
            var input = _factory.CreateInputSimulator();
            var ex = Record.Exception(() => input.SendMouseWheel(-120));
            Assert.Null(ex);
        }
    }
}
