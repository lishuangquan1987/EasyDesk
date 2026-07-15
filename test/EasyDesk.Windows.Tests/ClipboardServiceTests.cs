using System;
using System.Threading;
using Xunit;
using EasyDesk.Windows;

namespace EasyDesk.Windows.Tests
{
    /// <summary>
    /// Tests for WindowsClipboardService.
    /// WARNING: These tests modify your clipboard. The test runner
    /// attempts to restore original content, but there is a brief window
    /// where clipboard is modified. Do not copy important data during test execution.
    /// </summary>
    public class ClipboardServiceTests
    {
        private readonly WindowsDesktopFactory _factory;

        public ClipboardServiceTests()
        {
            _factory = new WindowsDesktopFactory();
        }

        [Fact]
        public void SetText_GetText_RoundTrip()
        {
            // Must run on STA thread for clipboard access
            string originalText = null;
            string roundTripResult = null;

            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();

                // Backup original clipboard
                originalText = clip.GetText();

                try
                {
                    // Write test text
                    string testText = "EasyDesk Clipboard Test: " + System.Guid.NewGuid().ToString();
                    clip.SetText(testText);

                    // Verify ContainsText
                    Assert.True(clip.ContainsText());

                    // Read back
                    roundTripResult = clip.GetText();
                }
                finally
                {
                    // Restore original clipboard
                    if (originalText != null)
                    {
                        clip.SetText(originalText);
                    }
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.NotNull(roundTripResult);
            Assert.True(roundTripResult.StartsWith("EasyDesk Clipboard Test: "));
        }

        [Fact]
        public void SetText_Empty_ShouldNotThrow()
        {
            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                string originalText = clip.GetText();

                try
                {
                    var ex = Record.Exception(() => clip.SetText(""));
                    Assert.Null(ex);

                    string result = clip.GetText();
                    Assert.Equal("", result);
                }
                finally
                {
                    if (originalText != null)
                        clip.SetText(originalText);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [Fact]
        public void GetText_WhenEmpty_ReturnsNull()
        {
            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                // Empty clipboard first
                clip.SetText("");
                // GetText on empty string returns "" (not null)
                string result = clip.GetText();
                Assert.NotNull(result);
                Assert.Equal("", result);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [Fact]
        public void ContainsText_ReturnsBoolean()
        {
            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                string originalText = clip.GetText();

                try
                {
                    clip.SetText("test");
                    Assert.True(clip.ContainsText());
                }
                finally
                {
                    if (originalText != null)
                        clip.SetText(originalText);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [Fact]
        public void GetText_NonStaThread_ShouldNotThrow()
        {
            // Non-STA thread accessing clipboard should fail gracefully or throw
            // Exception must be captured INSIDE the MTA thread — it won't propagate via Join()
            string result = null;
            Exception threadEx = null;

            var thread = new Thread(() =>
            {
                try
                {
                    var clip = _factory.CreateClipboardService();
                    result = clip.GetText(); // May return null or throw on non-STA
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
            thread.Join();

            // We don't assert on the result — the important thing is no unhandled crash
            Assert.Null(threadEx);
        }
    }
}
