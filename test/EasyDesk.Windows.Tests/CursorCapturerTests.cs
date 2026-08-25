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

        [Fact]
        public void GetCursorInfo_Shape_ShouldContainOpaqueAndTransparentPixels()
        {
            var capturer = _factory.CreateCursorCapturer();
            var info = capturer.GetCursorInfo();
            if (info.Width <= 0 || info.Height <= 0 || info.ImageData == null || info.ImageData.Length == 0)
                return; // 光标隐藏或无形状数据：跳过

            int andStride = ((info.Width + 15) / 16) * 2;
            int xorStride = info.Width * 4;
            int xorBase = andStride * info.Height;
            bool hasOpaque = false;
            bool hasTransparent = false;
            for (int row = 0; row < info.Height; row++)
            {
                for (int col = 0; col < info.Width; col++)
                {
                    byte andByte = info.ImageData[row * andStride + (col >> 3)];
                    bool andSet = ((andByte >> (7 - (col & 7))) & 1) != 0;
                    int si = xorBase + row * xorStride + col * 4;
                    byte alpha = info.ImageData[si + 3];
                    if (!andSet && alpha != 0) hasOpaque = true;
                    if (andSet || alpha == 0) hasTransparent = true;
                }
            }
            // 光标形状必须既有可见像素又有透明像素，
            // 否则客户端会出现“整块透明（看不到光标）”或“矩形黑框/色块”类渲染问题。
            Assert.True(hasOpaque,
                "cursor shape has no opaque pixels — cursor would be invisible");
            Assert.True(hasTransparent,
                "cursor shape has no transparent pixels — cursor would render as a solid rectangle");
        }

        [Fact]
        public void GetCursorInfo_SameCursor_ShouldReuseCachedShape()
        {
            var capturer = _factory.CreateCursorCapturer();
            var first = capturer.GetCursorInfo();
            var second = capturer.GetCursorInfo();
            if (first.ImageData != null && first.ImageData.Length > 0)
            {
                // 形状按光标句柄缓存：同句柄二次调用必须复用同一实例（60Hz 轮询热路径）
                Assert.Same(first.ImageData, second.ImageData);
            }
        }
    }
}
