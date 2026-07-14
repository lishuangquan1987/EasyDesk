using System;
using System.Runtime.InteropServices;
using Xunit;
using EasyDesk.Core.Models;
using EasyDesk.Windows;

namespace EasyDesk.Windows.Tests
{
    /// <summary>
    /// Tests for WindowsScreenCapturer.
    /// </summary>
    public class ScreenCapturerTests
    {
        private readonly WindowsDesktopFactory _factory;

        public ScreenCapturerTests()
        {
            _factory = new WindowsDesktopFactory();
        }

        [Fact]
        public void CaptureScreen_ShouldReturnValidFrame()
        {
            var capturer = _factory.CreateScreenCapturer();
            ScreenFrame frame = null;
            try
            {
                frame = capturer.CaptureScreen();
                Assert.NotNull(frame);
                Assert.NotEqual(IntPtr.Zero, frame.Scan0);
                Assert.True(frame.Width > 0);
                Assert.True(frame.Height > 0);
                Assert.Equal(frame.Width * 4, frame.Stride);
                Assert.Equal(0, frame.PixelFormat); // BGRA32
            }
            finally
            {
                if (frame != null && frame.Scan0 != IntPtr.Zero)
                    Marshal.FreeHGlobal(frame.Scan0);
            }
        }

        [Fact]
        public void CaptureScreen_WithDefaultOptions_ShouldReturnValidFrame()
        {
            var capturer = _factory.CreateScreenCapturer();
            ScreenFrame frame = null;
            try
            {
                frame = capturer.CaptureScreen(CaptureOptions.Default);
                Assert.NotNull(frame);
                Assert.True(frame.Width > 0);
            }
            finally
            {
                if (frame != null && frame.Scan0 != IntPtr.Zero)
                    Marshal.FreeHGlobal(frame.Scan0);
            }
        }

        [Fact]
        public void CaptureRegion_ShouldMatchRequestedSize()
        {
            var capturer = _factory.CreateScreenCapturer();
            ScreenFrame frame = null;
            try
            {
                frame = capturer.CaptureRegion(0, 0, 100, 50);
                Assert.Equal(100, frame.Width);
                Assert.Equal(50, frame.Height);
                Assert.Equal(400, frame.Stride); // 100 * 4
            }
            finally
            {
                if (frame != null && frame.Scan0 != IntPtr.Zero)
                    Marshal.FreeHGlobal(frame.Scan0);
            }
        }

        [Fact]
        public void CaptureRegion_InvalidSize_ShouldThrow()
        {
            var capturer = _factory.CreateScreenCapturer();
            Assert.Throws<ArgumentOutOfRangeException>(() => capturer.CaptureRegion(0, 0, 0, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => capturer.CaptureRegion(0, 0, 100, 0));
        }

        [Fact]
        public void GetPrimaryScreen_ShouldReturnValidBounds()
        {
            var capturer = _factory.CreateScreenCapturer();
            var bounds = capturer.GetPrimaryScreen();
            Assert.True(bounds.Width > 0);
            Assert.True(bounds.Height > 0);
            Assert.True(bounds.IsPrimary);
            Assert.Equal(0, bounds.X);
            Assert.Equal(0, bounds.Y);
        }

        [Fact]
        public void GetAllScreens_ShouldReturnAtLeastOne()
        {
            var capturer = _factory.CreateScreenCapturer();
            var screens = capturer.GetAllScreens();
            Assert.NotNull(screens);
            Assert.True(screens.Length >= 1);
            Assert.True(screens[0].Width > 0);
        }

        [Fact]
        public void ScreenFrame_PixelData_ShouldBeReadable()
        {
            var capturer = _factory.CreateScreenCapturer();
            ScreenFrame frame = null;
            try
            {
                frame = capturer.CaptureRegion(0, 0, 10, 10);
                // Read first pixel to verify memory is accessible
                byte b = Marshal.ReadByte(frame.Scan0, 0); // Blue
                byte g = Marshal.ReadByte(frame.Scan0, 1); // Green
                byte r = Marshal.ReadByte(frame.Scan0, 2); // Red
                byte a = Marshal.ReadByte(frame.Scan0, 3); // Alpha
                // Values should be 0-255 (no assertion on actual color)
                Assert.True(b >= 0 && b <= 255);
                Assert.True(r >= 0 && r <= 255);
            }
            finally
            {
                if (frame != null && frame.Scan0 != IntPtr.Zero)
                    Marshal.FreeHGlobal(frame.Scan0);
            }
        }
    }
}
