using System.Linq;
using Xunit;
using EasyDesk.Windows;

namespace EasyDesk.Windows.Tests
{
    /// <summary>
    /// Tests for WindowsDesktopInfo.
    /// </summary>
    public class DesktopInfoTests
    {
        private readonly WindowsDesktopFactory _factory;

        public DesktopInfoTests()
        {
            _factory = new WindowsDesktopFactory();
        }

        [Fact]
        public void GetPrimaryBounds_ShouldReturnValidBounds()
        {
            var info = _factory.CreateDesktopInfo();
            var bounds = info.GetPrimaryBounds();

            Assert.True(bounds.Width > 0);
            Assert.True(bounds.Height > 0);
            Assert.True(bounds.IsPrimary);

            // Verify primary bounds match the primary entry in GetAllBounds
            var allBounds = info.GetAllBounds();
            var primaryFromAll = allBounds.First(s => s.IsPrimary);
            Assert.Equal(primaryFromAll.X, bounds.X);
            Assert.Equal(primaryFromAll.Y, bounds.Y);
            Assert.Equal(primaryFromAll.Width, bounds.Width);
            Assert.Equal(primaryFromAll.Height, bounds.Height);
        }

        [Fact]
        public void GetAllBounds_ShouldReturnAtLeastOneMonitor()
        {
            var info = _factory.CreateDesktopInfo();
            var screens = info.GetAllBounds();

            Assert.NotNull(screens);
            Assert.True(screens.Length >= 1);
            foreach (var s in screens)
            {
                Assert.True(s.Width > 0);
                Assert.True(s.Height > 0);
            }
        }

        [Fact]
        public void GetAllBounds_ShouldHaveExactlyOnePrimary()
        {
            var info = _factory.CreateDesktopInfo();
            var screens = info.GetAllBounds();

            int primaryCount = 0;
            foreach (var s in screens)
            {
                if (s.IsPrimary) primaryCount++;
            }
            Assert.Equal(1, primaryCount);
        }

        [Fact]
        public void GetVirtualScreenBounds_ShouldEncloseAllMonitors()
        {
            var info = _factory.CreateDesktopInfo();
            var virtualBounds = info.GetVirtualScreenBounds();

            Assert.True(virtualBounds.Width > 0);
            Assert.True(virtualBounds.Height > 0);

            // Virtual bounds should cover at least the primary monitor
            var primary = info.GetPrimaryBounds();
            Assert.True(virtualBounds.Width >= primary.Width);
            Assert.True(virtualBounds.Height >= primary.Height);
        }

        [Fact]
        public void GetVirtualScreenBounds_ShouldMatchPrimaryOnSingleMonitor()
        {
            var info = _factory.CreateDesktopInfo();
            var screens = info.GetAllBounds();
            if (screens.Length > 1)
            {
                // Multi-monitor — virtual bounds differ from primary; skip this check
                return;
            }

            var primary = info.GetPrimaryBounds();
            var virtualBounds = info.GetVirtualScreenBounds();
            Assert.Equal(primary.Width, virtualBounds.Width);
            Assert.Equal(primary.Height, virtualBounds.Height);
        }
    }
}
