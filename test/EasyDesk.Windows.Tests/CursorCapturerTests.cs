using Xunit;
using EasyDesk.Windows;

namespace EasyDesk.Windows.Tests
{
    /// <summary>
    /// Tests for WindowsCursorCapturer.
    /// </summary>
    public class CursorCapturerTests
    {
        private readonly WindowsDesktopFactory _factory;

        public CursorCapturerTests()
        {
            _factory = new WindowsDesktopFactory();
        }

        [Fact]
        public void GetCursorPosition_ShouldReturnValues()
        {
            var capturer = _factory.CreateCursorCapturer();
            int x, y;
            capturer.GetCursorPosition(out x, out y);

            // Cursor should be somewhere on the virtual desktop
            // (bounds are system-dependent, just verify they're not default zeros
            //  on a properly initialized system)
            Assert.True(x != 0 || y != 0,
                string.Format("Cursor position should not be (0,0) unless cursor is at origin. Got ({0},{1})", x, y));
        }

        [Fact]
        public void GetCursorInfo_ShouldReturnValidInfo()
        {
            var capturer = _factory.CreateCursorCapturer();
            var info = capturer.GetCursorInfo();

            Assert.NotNull(info);
            Assert.NotNull(info.ImageData);
            // ImageData may be empty if cursor is hidden, but the info object must exist
        }

        [Fact]
        public void GetCursorInfo_Hotspot_ShouldBeReasonable()
        {
            var capturer = _factory.CreateCursorCapturer();
            var info = capturer.GetCursorInfo();

            if (info.Width > 0 && info.Height > 0)
            {
                // Hotspot must be within cursor bounds (or exactly at edge for some cursors)
                Assert.True(info.HotspotX >= 0 && info.HotspotX <= info.Width,
                    string.Format("HotspotX {0} out of range [0,{1}]", info.HotspotX, info.Width));
                Assert.True(info.HotspotY >= 0 && info.HotspotY <= info.Height,
                    string.Format("HotspotY {0} out of range [0,{1}]", info.HotspotY, info.Height));
            }
        }
    }
}
