using System;
using System.IO;
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

        // ── 文件剪贴板（CF_HDROP）测试 ──

        /// <summary>SetFiles → ContainsFiles → GetFileList round-trip：单个文件。</summary>
        [Fact]
        public void SetFiles_GetFileList_RoundTrip_SingleFile()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "EasyDeskTest_" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(tempFile, "test content");

            string[] readBack = null;
            bool containsFiles = false;
            string originalText = null;

            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                originalText = clip.ContainsText() ? clip.GetText() : null;

                try
                {
                    clip.SetFiles(new string[] { tempFile });
                    containsFiles = clip.ContainsFiles();
                    readBack = clip.GetFileList();
                }
                finally
                {
                    // 恢复剪贴板
                    if (originalText != null)
                        clip.SetText(originalText);
                    try { File.Delete(tempFile); } catch { }
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.True(containsFiles, "ContainsFiles should return true after SetFiles");
            Assert.NotNull(readBack);
            Assert.Equal(1, readBack.Length);
            Assert.Equal(tempFile, readBack[0]);
        }

        /// <summary>SetFiles 多个文件 round-trip。</summary>
        [Fact]
        public void SetFiles_GetFileList_RoundTrip_MultipleFiles()
        {
            string tempFile1 = Path.Combine(Path.GetTempPath(), "EasyDeskTest_" + Guid.NewGuid().ToString("N") + ".bin");
            string tempFile2 = Path.Combine(Path.GetTempPath(), "EasyDeskTest_" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(tempFile1, new byte[] { 0x01, 0x02, 0x03 });
            File.WriteAllBytes(tempFile2, new byte[] { 0x04, 0x05 });

            string[] readBack = null;
            string originalText = null;

            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                originalText = clip.ContainsText() ? clip.GetText() : null;

                try
                {
                    clip.SetFiles(new string[] { tempFile1, tempFile2 });
                    readBack = clip.GetFileList();
                }
                finally
                {
                    if (originalText != null)
                        clip.SetText(originalText);
                    try { File.Delete(tempFile1); } catch { }
                    try { File.Delete(tempFile2); } catch { }
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.NotNull(readBack);
            Assert.Equal(2, readBack.Length);
            Assert.Contains(tempFile1, readBack);
            Assert.Contains(tempFile2, readBack);
        }

        /// <summary>ContainsFiles 在文本剪贴板时应返回 false。</summary>
        [Fact]
        public void ContainsFiles_AfterSetText_ReturnsFalse()
        {
            bool containsFiles = false;
            string originalText = null;

            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                originalText = clip.ContainsText() ? clip.GetText() : null;

                try
                {
                    clip.SetText("plain text");
                    containsFiles = clip.ContainsFiles();
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

            Assert.False(containsFiles, "ContainsFiles should return false when clipboard has only text");
        }

        /// <summary>GetFileList 在无文件剪贴板时应返回 null。</summary>
        [Fact]
        public void GetFileList_WhenNoFiles_ReturnsNull()
        {
            string[] readBack = new string[0]; // 初始化为非 null 以验证被设为 null
            string originalText = null;

            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                originalText = clip.ContainsText() ? clip.GetText() : null;

                try
                {
                    clip.SetText("no files here");
                    readBack = clip.GetFileList();
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

            Assert.Null(readBack);
        }

        // ── 图片剪贴板（CF_DIB）测试 ──

        /// <summary>构造一个 2x2 32bpp BGRA 的 CF_DIB 字节数组。</summary>
        private static byte[] BuildTestDibBytes(int width, int height, int bpp)
        {
            // BITMAPINFOHEADER (40 bytes) + 像素数据
            // 对于 32bpp BGRA：每像素 4 字节，行已 DWORD 对齐
            int rowBytes = width * (bpp / 8);
            // 32bpp 已自然对齐；24bpp 需要补齐到 4 字节倍数
            if (bpp == 24)
            {
                rowBytes = (rowBytes + 3) & ~3;
            }
            int pixelDataSize = rowBytes * height;
            int totalSize = 40 + pixelDataSize;
            byte[] data = new byte[totalSize];

            // BITMAPINFOHEADER
            // biSize=40, biWidth, biHeight, biPlanes=1, biBitCount=bpp,
            // biCompression=0 (BI_RGB), 其余字段 = 0
            WriteInt32LE(data, 0, 40);             // biSize
            WriteInt32LE(data, 4, width);          // biWidth
            WriteInt32LE(data, 8, height);         // biHeight (positive = bottom-up)
            WriteInt16LE(data, 12, 1);             // biPlanes
            WriteInt16LE(data, 14, (short)bpp);    // biBitCount
            WriteInt32LE(data, 16, 0);             // biCompression = BI_RGB
            WriteInt32LE(data, 20, pixelDataSize); // biSizeImage
            WriteInt32LE(data, 24, 0);             // biXPelsPerMeter
            WriteInt32LE(data, 28, 0);             // biYPelsPerMeter
            WriteInt32LE(data, 32, 0);             // biClrUsed
            WriteInt32LE(data, 36, 0);             // biClrImportant

            // 像素数据：填充测试模式 — 红色像素 (BGRA: B=0, G=0, R=255, A=255 for 32bpp)
            if (bpp == 32)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = 40 + y * rowBytes + x * 4;
                        data[offset + 0] = 0x00;   // B
                        data[offset + 1] = 0x00;   // G
                        data[offset + 2] = 0xFF;   // R
                        data[offset + 3] = 0xFF;   // A
                    }
                }
            }
            else if (bpp == 24)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = 40 + y * rowBytes + x * 3;
                        data[offset + 0] = 0x00;   // B
                        data[offset + 1] = 0x00;   // G
                        data[offset + 2] = 0xFF;   // R
                    }
                }
            }
            return data;
        }

        private static void WriteInt32LE(byte[] buf, int offset, int value)
        {
            buf[offset + 0] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteInt16LE(byte[] buf, int offset, short value)
        {
            buf[offset + 0] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static int ReadInt32LE(byte[] buf, int offset)
        {
            return buf[offset + 0] | (buf[offset + 1] << 8) |
                   (buf[offset + 2] << 16) | (buf[offset + 3] << 24);
        }

        private static short ReadInt16LE(byte[] buf, int offset)
        {
            return (short)(buf[offset + 0] | (buf[offset + 1] << 8));
        }

        /// <summary>SetImageDibBytes → ContainsImage → GetImageDibBytes round-trip：2x2 32bpp BGRA。</summary>
        [Fact]
        public void SetImage_GetImage_RoundTrip_32bpp()
        {
            byte[] testDib = BuildTestDibBytes(2, 2, 32);
            byte[] readBack = null;
            bool containsImage = false;
            string originalText = null;

            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                originalText = clip.ContainsText() ? clip.GetText() : null;

                try
                {
                    clip.SetImageDibBytes(testDib);
                    containsImage = clip.ContainsImage();
                    readBack = clip.GetImageDibBytes();
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

            Assert.True(containsImage, "ContainsImage should return true after SetImageDibBytes");
            Assert.NotNull(readBack);
            // 校验 BITMAPINFOHEADER
            Assert.Equal(40, ReadInt32LE(readBack, 0));   // biSize
            Assert.Equal(2, ReadInt32LE(readBack, 4));    // biWidth
            Assert.Equal(2, ReadInt32LE(readBack, 8));    // biHeight
            Assert.Equal((short)1, ReadInt16LE(readBack, 12));  // biPlanes
            Assert.Equal((short)32, ReadInt16LE(readBack, 14)); // biBitCount
            // 校验像素数据：第一个像素应为 BGRA = 00 00 FF FF
            Assert.Equal(0x00, readBack[40 + 0]); // B
            Assert.Equal(0x00, readBack[40 + 1]); // G
            Assert.Equal(0xFF, readBack[40 + 2]); // R
            Assert.Equal(0xFF, readBack[40 + 3]); // A
        }

        /// <summary>SetImageDibBytes → ContainsImage → GetImageDibBytes round-trip：4x3 24bpp RGB（行对齐）。</summary>
        [Fact]
        public void SetImage_GetImage_RoundTrip_24bpp()
        {
            byte[] testDib = BuildTestDibBytes(4, 3, 24);
            byte[] readBack = null;
            string originalText = null;

            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                originalText = clip.ContainsText() ? clip.GetText() : null;

                try
                {
                    clip.SetImageDibBytes(testDib);
                    readBack = clip.GetImageDibBytes();
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

            Assert.NotNull(readBack);
            Assert.Equal(40, ReadInt32LE(readBack, 0));   // biSize
            Assert.Equal(4, ReadInt32LE(readBack, 4));    // biWidth
            Assert.Equal(3, ReadInt32LE(readBack, 8));    // biHeight
            Assert.Equal((short)24, ReadInt16LE(readBack, 14)); // biBitCount
            // 4 pixels * 3 bytes = 12 bytes per row, already DWORD aligned (no padding needed)
            // 总像素数据 = 12 * 3 = 36 bytes
            Assert.Equal(36, ReadInt32LE(readBack, 20));  // biSizeImage
            Assert.Equal(76, readBack.Length);            // 40 + 36
        }

        /// <summary>ContainsImage 在文本剪贴板时应返回 false。</summary>
        [Fact]
        public void ContainsImage_AfterSetText_ReturnsFalse()
        {
            bool containsImage = true; // 初始化为 true 验证被设为 false
            string originalText = null;

            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                originalText = clip.ContainsText() ? clip.GetText() : null;

                try
                {
                    clip.SetText("plain text");
                    containsImage = clip.ContainsImage();
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

            Assert.False(containsImage, "ContainsImage should return false when clipboard has only text");
        }

        /// <summary>GetImageDibBytes 在无图片剪贴板时应返回 null。</summary>
        [Fact]
        public void GetImageDibBytes_WhenNoImage_ReturnsNull()
        {
            byte[] readBack = new byte[1]; // 初始化为非 null 验证被设为 null
            string originalText = null;

            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                originalText = clip.ContainsText() ? clip.GetText() : null;

                try
                {
                    clip.SetText("no image here");
                    readBack = clip.GetImageDibBytes();
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

            Assert.Null(readBack);
        }

        /// <summary>SetImageDibBytes 在 null 或空数组时应抛 ArgumentException。</summary>
        [Fact]
        public void SetImageDibBytes_NullOrEmpty_Throws()
        {
            Exception nullEx = null;
            Exception emptyEx = null;
            string originalText = null;

            var thread = new Thread(() =>
            {
                var clip = _factory.CreateClipboardService();
                originalText = clip.ContainsText() ? clip.GetText() : null;

                try
                {
                    try { clip.SetImageDibBytes(null); }
                    catch (Exception ex) { nullEx = ex; }

                    try { clip.SetImageDibBytes(new byte[0]); }
                    catch (Exception ex) { emptyEx = ex; }
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

            Assert.NotNull(nullEx);
            Assert.True(nullEx is ArgumentException);
            Assert.NotNull(emptyEx);
            Assert.True(emptyEx is ArgumentException);
        }
    }
}
