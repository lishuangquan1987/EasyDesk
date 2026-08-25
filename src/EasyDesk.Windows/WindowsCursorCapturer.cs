using System;
using System.Runtime.InteropServices;
using EasyDesk.Core;
using EasyDesk.Core.Models;
using EasyDesk.Windows.NativeMethods;

namespace EasyDesk.Windows
{
    /// <summary>
    /// Windows cursor capturer using GetCursorInfo / GetIconInfo / DrawIconEx.
    ///
    /// 性能设计（弱机 XP 单核关键路径）：
    /// 光标形状按光标句柄（hCursor）缓存 —— 60Hz 轮询只做一次 GetCursorInfo（极轻），
    /// 仅当 hCursor 变化（形状切换/动画帧）时才执行一次 DrawIconEx 渲染。
    /// 旧实现每次轮询都 GetIconInfo + 2×GetDIBits + 逐字节 Marshal.ReadByte
    /// （32×32 光标约 6000 次 P/Invoke），单核 XP 虚拟机上实测把整机 CPU 吃满、
    /// 并因 GDI 串行化拖垮屏幕捕获（捕获仅 ~1.7 FPS）。
    ///
    /// 正确性设计（修复“光标矩形黑框”）：
    /// 光标形状改用 DrawIconEx 渲染到清零的 32bpp DIB section，GDI 按光标自身
    /// 掩码/alpha 合成出正确的 BGRA（含透明度）。旧实现直接 GetDIBits 读取
    /// hbmColor DDB —— DDB 无 alpha 通道，虚拟机显示驱动回读的 alpha 字节是
    /// 未定义垃圾值（常见 0xFF），客户端把透明背景涂成不透明黑 → 黑框。
    /// 渲染结果按 alpha==0 推导出等价的 1bpp AND 掩码，保持 [AND|XOR] 线格式
    /// 与客户端合成逻辑完全兼容。
    ///
    /// 线程安全：GetCursorInfo 内部有锁（形状缓存与 DIB 复用）。调用方通常
    /// 是单轮询线程，锁开销可忽略。
    /// </summary>
    public class WindowsCursorCapturer : ICursorCapturer
    {
        // ── 形状缓存（key = 光标句柄）──
        private readonly object _cacheLock = new object();
        private IntPtr _cachedHandle;
        private int _cachedWidth;
        private int _cachedHeight;
        private int _cachedHotX;
        private int _cachedHotY;
        private byte[] _cachedImageData;

        // ── DrawIconEx 渲染用的缓存 32bpp DIB section ──
        private IntPtr _dibDc;
        private IntPtr _dibBitmap;
        private IntPtr _dibBuffer;
        private IntPtr _dibOldObject;
        private int _dibW;
        private int _dibH;
        private byte[] _dibZeroes;
        private bool _dibReady;

        /// <summary>
        /// 把 GetIconInfo 返回的掩码位图全部释放（调用方职责）。
        /// </summary>
        private static void DestroyIconBitmaps(ICONINFO ii)
        {
            if (ii.hbmMask != IntPtr.Zero)
            {
                Gdi32.DeleteObject(ii.hbmMask);
                ii.hbmMask = IntPtr.Zero;
            }
            if (ii.hbmColor != IntPtr.Zero)
            {
                Gdi32.DeleteObject(ii.hbmColor);
                ii.hbmColor = IntPtr.Zero;
            }
        }

        public void GetCursorPosition(out int x, out int y)
        {
            POINT pt;
            if (!User32.GetCursorPos(out pt))
            {
                x = 0;
                y = 0;
                return;
            }
            x = pt.x;
            y = pt.y;
        }

        public CursorInfo GetCursorInfo()
        {
            var ci = new CURSORINFO();
            ci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));

            if (!User32.GetCursorInfo(ref ci))
            {
                // Cursor info unavailable, return default
                return new CursorInfo
                {
                    X = 0, Y = 0,
                    HotspotX = 0, HotspotY = 0,
                    Width = 0, Height = 0,
                    ImageData = new byte[0]
                };
            }

            int x = ci.ptScreenPos.x;
            int y = ci.ptScreenPos.y;

            if (ci.hCursor == IntPtr.Zero || (ci.flags & Win32Constants.CURSOR_SHOWING) == 0)
            {
                return new CursorInfo
                {
                    X = x, Y = y,
                    HotspotX = 0, HotspotY = 0,
                    Width = 0, Height = 0,
                    ImageData = new byte[0]
                };
            }

            lock (_cacheLock)
            {
                // 缓存命中：光标句柄未变，直接复用形状（仅位置更新）。
                // 这是 60Hz 轮询的热路径 —— 一次 GetCursorInfo API 即返回。
                if (ci.hCursor == _cachedHandle && _cachedImageData != null)
                {
                    return new CursorInfo
                    {
                        X = x, Y = y,
                        HotspotX = _cachedHotX, HotspotY = _cachedHotY,
                        Width = _cachedWidth, Height = _cachedHeight,
                        ImageData = _cachedImageData
                    };
                }

                // 形状变化（或首次）：执行一次重渲染
                ICONINFO ii;
                if (!User32.GetIconInfo(ci.hCursor, out ii))
                {
                    return new CursorInfo
                    {
                        X = x, Y = y,
                        HotspotX = 0, HotspotY = 0,
                        Width = 0, Height = 0,
                        ImageData = new byte[0]
                    };
                }

                try
                {
                    int width;
                    int height;
                    byte[] imageData;
                    if (!TryRenderCursorShape(ci.hCursor, ii, out imageData, out width, out height))
                    {
                        return new CursorInfo
                        {
                            X = x, Y = y,
                            HotspotX = 0, HotspotY = 0,
                            Width = 0, Height = 0,
                            ImageData = new byte[0]
                        };
                    }

                    _cachedHandle = ci.hCursor;
                    _cachedHotX = ii.xHotspot;
                    _cachedHotY = ii.yHotspot;
                    _cachedWidth = width;
                    _cachedHeight = height;
                    _cachedImageData = imageData;

                    return new CursorInfo
                    {
                        X = x, Y = y,
                        HotspotX = ii.xHotspot, HotspotY = ii.yHotspot,
                        Width = width, Height = height,
                        ImageData = imageData
                    };
                }
                finally
                {
                    DestroyIconBitmaps(ii);
                }
            }
        }

        /// <summary>
        /// 用 DrawIconEx 把光标渲染到缓存的 32bpp DIB section，
        /// 合成 [AND 1bpp | XOR BGRA] 复合格式（AND=1 表示透明，由渲染结果的
        /// alpha==0 推导）。渲染结果为 GDI 按光标自身掩码/alpha 合成后的
        /// 正确图像，不再依赖 DDB 无意义的 alpha 字节。
        /// </summary>
        private bool TryRenderCursorShape(IntPtr hCursor, ICONINFO ii,
            out byte[] imageData, out int width, out int height)
        {
            imageData = null;
            width = 0;
            height = 0;

            try
            {
                // 尺寸探测位图：颜色光标用 hbmColor；单色光标 hbmMask 为双倍高（取半）
                IntPtr probeBitmap = ii.hbmColor != IntPtr.Zero ? ii.hbmColor : ii.hbmMask;
                if (probeBitmap == IntPtr.Zero)
                    return false;

                IntPtr hdc = User32.GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero)
                    return false;

                try
                {
                    var bmiQuery = new BITMAPINFO();
                    bmiQuery.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                    bmiQuery.bmiHeader.biBitCount = 0;

                    Gdi32.GetDIBits(hdc, probeBitmap, 0, 0, IntPtr.Zero,
                        ref bmiQuery, Win32Constants.DIB_RGB_COLORS);

                    width = Math.Abs(bmiQuery.bmiHeader.biWidth);
                    int fullHeight = Math.Abs(bmiQuery.bmiHeader.biHeight);
                    bool hasColor = ii.hbmColor != IntPtr.Zero;
                    height = hasColor ? fullHeight : fullHeight / 2;

                    // 防御异常光标尺寸（防止恶意/损坏光标导致 OOM）
                    if (width <= 0 || height <= 0 || width > 512 || height > 512)
                        return false;

                    if (!EnsureDib(hdc, width, height))
                        return false;

                    // 清零 DIB（全透明背景），再让 GDI 合成光标
                    Marshal.Copy(_dibZeroes, 0, _dibBuffer, _dibZeroes.Length);

                    bool ok = User32.DrawIconEx(_dibDc, 0, 0, hCursor, width, height,
                        0, IntPtr.Zero, Win32Constants.DI_NORMAL);
                    if (!ok)
                        return false;

                    // GDI 批处理可能尚未落地：直接读 DIB 内存前必须 flush
                    Gdi32.GdiFlush();

                    // 渲染结果 → 托管数组（每形状变化仅一次，逐字节拷贝可接受）
                    int dibBytes = width * height * 4;
                    byte[] rendered = new byte[dibBytes];
                    Marshal.Copy(_dibBuffer, rendered, 0, dibBytes);

                    // 合成 [AND 1bpp | XOR BGRA]：alpha==0 → AND=1（透明）
                    int andStride = ((width + 15) / 16) * 2;
                    int xorStride = width * 4;
                    imageData = new byte[(andStride + xorStride) * height];
                    int xorBase = andStride * height;

                    for (int row = 0; row < height; row++)
                    {
                        int srcRow = row * width * 4;
                        int dstRow = xorBase + row * xorStride;
                        int andRow = row * andStride;
                        for (int col = 0; col < width; col++)
                        {
                            int s = srcRow + col * 4;
                            byte b = rendered[s];
                            byte g = rendered[s + 1];
                            byte r = rendered[s + 2];
                            byte a = rendered[s + 3];
                            int d = dstRow + col * 4;
                            imageData[d] = b;
                            imageData[d + 1] = g;
                            imageData[d + 2] = r;
                            imageData[d + 3] = a;
                            if (a == 0)
                            {
                                int byteIndex = col / 8;
                                int bitInByte = 7 - (col % 8);
                                imageData[andRow + byteIndex] |= (byte)(1 << bitInByte);
                            }
                        }
                    }
                    return true;
                }
                finally
                {
                    User32.ReleaseDC(IntPtr.Zero, hdc);
                }
            }
            catch (Exception)
            {
                imageData = null;
                width = 0;
                height = 0;
                return false;
            }
        }

        /// <summary>
        /// 确保缓存 DIB section 就绪且尺寸匹配（尺寸变化时销毁重建）。
        /// DIB section 在系统内存中，DrawIconEx 渲染结果可直接读内存，
        /// 无需 GetDIBits 视频内存回读（虚拟机 GDI 回读极慢）。
        /// </summary>
        private bool EnsureDib(IntPtr refDc, int width, int height)
        {
            if (_dibReady && _dibW == width && _dibH == height)
                return true;

            DestroyDib();

            _dibDc = Gdi32.CreateCompatibleDC(refDc);
            if (_dibDc == IntPtr.Zero)
                return false;

            var bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // top-down：行 0 在缓冲区顶部
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = Win32Constants.BI_RGB;

            _dibBitmap = Gdi32.CreateDIBSection(_dibDc, ref bmi,
                Win32Constants.DIB_RGB_COLORS, out _dibBuffer, IntPtr.Zero, 0);
            if (_dibBitmap == IntPtr.Zero || _dibBuffer == IntPtr.Zero)
            {
                DestroyDib();
                return false;
            }

            _dibOldObject = Gdi32.SelectObject(_dibDc, _dibBitmap);
            _dibW = width;
            _dibH = height;
            _dibZeroes = new byte[width * height * 4];
            _dibReady = true;
            return true;
        }

        /// <summary>释放缓存 DIB section 与内存 DC。</summary>
        private void DestroyDib()
        {
            if (_dibDc != IntPtr.Zero)
            {
                if (_dibBitmap != IntPtr.Zero)
                {
                    if (_dibOldObject != IntPtr.Zero)
                        Gdi32.SelectObject(_dibDc, _dibOldObject);
                    Gdi32.DeleteObject(_dibBitmap);
                    _dibBitmap = IntPtr.Zero;
                    _dibOldObject = IntPtr.Zero;
                }
                Gdi32.DeleteDC(_dibDc);
                _dibDc = IntPtr.Zero;
            }
            _dibBuffer = IntPtr.Zero;
            _dibZeroes = null;
            _dibReady = false;
        }
    }
}
