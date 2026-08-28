namespace EasyDesk.Windows
{
    using System;
    using System.Runtime.InteropServices;
    using EasyDesk.Core;
    using EasyDesk.Core.Models;

    /// <summary>
    /// 镜像驱动截屏后端（XP/Win7）。实现 <see cref="IScreenCapturer"/> 与
    /// <see cref="ICaptureChangesReader"/>。通过内核镜像驱动读取"脏矩形"，
    /// 只处理变化区域，避免 BitBlt 整帧截屏在无硬件加速环境（虚拟机/虚拟显卡）
    /// 的慢瓶颈（实测 ~250~300ms/帧）。
    /// 
    /// 职责边界：本类是用户态捕获客户端，负责打开驱动设备、映射共享缓冲、
    /// 解析脏矩形。内核镜像驱动（Mirror/driver/*.sys）负责在 GDI 绘图时记录
    /// 脏矩形到共享缓冲。
    /// </summary>
    /// <remarks>
    /// 构造在失败时（驱动未安装/无法加载）抛出 <see cref="InvalidOperationException"/>，
    /// 由 <see cref="WindowsDesktopFactory.CreateScreenCapturer"/> 捕获并回退到 BitBlt。
    /// 脏矩形结构体布局与内核驱动 Mirror/driver/MirrorDisp/MirrorDisp.c 中的
    /// MIRROR_CHANGES_HEADER / MIRROR_CHANGES_RECORD 一一对应，修改任一侧须同步。
    /// </remarks>
    public class MirrorScreenCapturer : IScreenCapturer, ICaptureChangesReader, IDisposable
    {
        private const string DriverDeviceName = @"\\.\EasyRDPMirror"; // 驱动设备名，与驱动侧一致
        private const int DefaultChangesCapacity = 4096;
        private const int ChangeTypeRect = 0;
        private const int ChangeTypeFullScreen = 1;

        // 环形缓冲布局（与内核共享，不释放前保持有效）
        private IntPtr _deviceHandle;
        private IntPtr _mappedHeader; // 映射后的共享缓冲头指针
        private int _capacity;
        private int _readIndex;       // 客户端本地游标（不写回，由驱动端维护 ReadIndex）

        private bool _disposed;
        private int _screenWidth;
        private int _screenHeight;

        // 诊断计数
        private long _changeReadCount;
        private long _overflowCount;

        /// <summary>构造镜像捕获器。失败抛 InvalidOperationException（由工厂回退 BitBlt）。</summary>
        public MirrorScreenCapturer()
        {
            // 打开驱动设备
            _deviceHandle = NativeCreateFile(
                DriverDeviceName,
                NativeGenericRead | NativeGenericWrite,
                0, IntPtr.Zero, NativeOpenExisting, 0, IntPtr.Zero);
            if (_deviceHandle == null || _deviceHandle == InvalidHandleValue)
                throw new InvalidOperationException("镜像驱动未安装或无法打开: " + DriverDeviceName);

            try
            {
                // 获取屏幕尺寸
                DesktopBounds b = GetPrimaryScreen();
                _screenWidth = b.Width;
                _screenHeight = b.Height;

                // 映射共享脏矩形缓冲（IOCTL/ExtEscape，此处用 CreateFileMapping 语义简化；
                // 完整实现在驱动验证后按 IOCTL 对接补全）
                _capacity = DefaultChangesCapacity;
                _mappedHeader = MapChangesBuffer();
                if (_mappedHeader == IntPtr.Zero)
                    throw new InvalidOperationException("无法映射镜像驱动共享缓冲");
                _readIndex = 0;
            }
            catch
            {
                if (_deviceHandle != IntPtr.Zero && _deviceHandle != InvalidHandleValue)
                    NativeCloseHandle(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
                throw;
            }
        }

        /// <summary>脏矩形环形缓冲头（与内核共享，布局须与 C 端一致）。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ChangesHeader
        {
            public uint WriteIndex;   // 驱动写入位置
            public uint ReadIndex;    // 用户态已读位置
            public uint Overflow;     // 非0 = 溢出，回退整屏
            public uint Capacity;     // 记录容量
            // 后续为 Records[]
        }

        /// <summary>脏矩形记录（与内核共享）。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ChangesRecord
        {
            public uint Type;   // 0=区域, 1=整屏
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>读取自上次以来的脏矩形。返回 true 表示有变化。</summary>
        public bool TryReadChanges(out ScreenRect[] rects)
        {
            rects = new ScreenRect[0];
            if (_mappedHeader == IntPtr.Zero)
                return false;

            System.Threading.Interlocked.Increment(ref _changeReadCount);

            ChangesHeader header;
            unsafe
            {
                header = *(ChangesHeader*)_mappedHeader.ToPointer();
                // 内存屏障：确保读取 WriteIndex 之前，驱动侧对 Records 的写入已可见
                System.Threading.Thread.MemoryBarrier();
            }

            // 溢出：回退整屏，并清溢出标志，同步游标避免重复读
            if (header.Overflow != 0)
            {
                System.Threading.Interlocked.Increment(ref _overflowCount);
                ClearOverflowFlag();
                _readIndex = (int)header.WriteIndex;
                WriteBackReadIndex(_readIndex);
                rects = new ScreenRect[] { new ScreenRect
                {
                    X = 0, Y = 0, Width = _screenWidth, Height = _screenHeight
                } };
                return true;
            }

            int write = (int)header.WriteIndex;
            if (write == _readIndex)
                return false; // 无新变化

            // 计算待读条数（环形）
            int count = (write + _capacity - _readIndex) % _capacity;
            if (count <= 0)
                return false;

            var list = new System.Collections.Generic.List<ScreenRect>(count);
            unsafe
            {
                byte* basePtr = (byte*)_mappedHeader.ToPointer();
                ChangesRecord* records = (ChangesRecord*)(basePtr + Marshal.SizeOf(typeof(ChangesHeader)));
                for (int i = 0; i < count; i++)
                {
                    int idx = (_readIndex + i) % _capacity;
                    ChangesRecord rec = records[idx];
                    if (rec.Type == ChangeTypeFullScreen)
                    {
                        list.Add(new ScreenRect { X = 0, Y = 0, Width = _screenWidth, Height = _screenHeight });
                    }
                    else
                    {
                        int w = rec.Right - rec.Left;
                        int h = rec.Bottom - rec.Top;
                        if (w > 0 && h > 0)
                        {
                            list.Add(new ScreenRect { X = rec.Left, Y = rec.Top, Width = w, Height = h });
                        }
                    }
                }
            }

            _readIndex = write;
            // 写回消费位置到共享头的 ReadIndex 字段（偏移 4），驱动端据此判断环形空间；
            // 写回后加内存屏障，确保驱动侧对 Records 的读取能看到我们的消费进度。
            WriteBackReadIndex(write);
            rects = list.ToArray();
            return rects.Length > 0;
        }

        /// <summary>清除驱动端溢出标志（避免反复回退整屏）。</summary>
        private void ClearOverflowFlag()
        {
            unsafe
            {
                // Overflow 字段偏移 = WriteIndex(4)+ReadIndex(4) = 8
                byte* basePtr = (byte*)_mappedHeader.ToPointer();
                *(uint*)(basePtr + 8) = 0;
                System.Threading.Thread.MemoryBarrier();
            }
        }

        /// <summary>把消费位置写回共享头的 ReadIndex 字段（偏移 4），驱动端据此释放环形空间。</summary>
        private void WriteBackReadIndex(int index)
        {
            unsafe
            {
                byte* basePtr = (byte*)_mappedHeader.ToPointer();
                *(uint*)(basePtr + 4) = (uint)index;
                System.Threading.Thread.MemoryBarrier();
            }
        }

        /// <summary>从镜像表面读整帧 BGRA。骨架：需驱动侧提供表面读取，先返回全黑占位。</summary>
        public ScreenFrame CaptureScreen()
        {
            return CaptureScreen(CaptureOptions.Default);
        }

        /// <summary>从镜像表面读整帧（带选项）。骨架：需驱动侧提供表面读取。</summary>
        public ScreenFrame CaptureScreen(CaptureOptions options)
        {
            int totalBytes = _screenWidth * _screenHeight * 4;
            IntPtr buf = Marshal.AllocHGlobal(totalBytes);
            // 骨架：清零（调用方负责 FreeHGlobal）。真实实现在驱动表面读取接口就绪后补。
            ZeroMemory(buf, new IntPtr(totalBytes));
            return new ScreenFrame
            {
                Scan0 = buf,
                Width = _screenWidth,
                Height = _screenHeight,
                Stride = _screenWidth * 4,
                PixelFormat = 0
            };
        }

        /// <summary>读指定区域。骨架：基于 CaptureScreen 后裁剪。</summary>
        public ScreenFrame CaptureRegion(int x, int y, int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException("width");
            if (height <= 0) throw new ArgumentOutOfRangeException("height");
            ScreenFrame full = CaptureScreen();
            if (full.Scan0 == IntPtr.Zero)
                return full;
            try
            {
                int stride = full.Width * 4;
                IntPtr region = Marshal.AllocHGlobal(width * height * 4);
                for (int row = 0; row < height; row++)
                {
                    long srcOff = ((long)(y + row) * stride) + x * 4L;
                    IntPtr dstRow = new IntPtr(region.ToInt64() + (long)row * width * 4);
                    if (srcOff >= 0 && srcOff + width * 4L <= stride * (long)full.Height)
                    {
                        CopyMemory(dstRow, new IntPtr(full.Scan0.ToInt64() + srcOff), new IntPtr(width * 4));
                    }
                    else
                    {
                        ZeroMemory(dstRow, new IntPtr(width * 4));
                    }
                }
                return new ScreenFrame
                {
                    Scan0 = region,
                    Width = width,
                    Height = height,
                    Stride = width * 4,
                    PixelFormat = 0
                };
            }
            finally
            {
                Marshal.FreeHGlobal(full.Scan0);
            }
        }

        /// <summary>读区域并按目标尺寸缩放。骨架：基于 CaptureRegion 后由调用方缩放。</summary>
        public ScreenFrame CaptureScaled(int x, int y, int width, int height, int targetWidth, int targetHeight)
        {
            if (targetWidth <= 0) throw new ArgumentOutOfRangeException("targetWidth");
            if (targetHeight <= 0) throw new ArgumentOutOfRangeException("targetHeight");
            ScreenFrame region = CaptureRegion(x, y, width, height);
            if (region.Scan0 == IntPtr.Zero)
                return region;
            // 骨架：直接返回原尺寸（真实缩放由镜像路径的调用方/编码器处理）
            return region;
        }

        /// <summary>获取主显示器边界。</summary>
        public DesktopBounds GetPrimaryScreen()
        {
            var info = new WindowsDesktopInfo();
            return info.GetPrimaryBounds();
        }

        /// <summary>获取所有显示器边界。</summary>
        public DesktopBounds[] GetAllScreens()
        {
            var info = new WindowsDesktopInfo();
            return info.GetAllBounds();
        }

        /// <summary>映射共享脏矩形缓冲。骨架：通过驱动 IOCTL/ExtEscape 返回映射指针。</summary>
        private IntPtr MapChangesBuffer()
        {
            // 真实实现：向驱动发 IOCTL 获取共享缓冲（MDL）的用户态映射地址。
            // 骨架：返回一个本进程分配的内存，供协议解析开发（驱动验证后替换）。
            int headerSize = Marshal.SizeOf(typeof(ChangesHeader));
            int recordSize = Marshal.SizeOf(typeof(ChangesRecord));
            int total = headerSize + _capacity * recordSize;
            IntPtr mem = Marshal.AllocHGlobal(total);
            unsafe
            {
                ChangesHeader* h = (ChangesHeader*)mem.ToPointer();
                h->Capacity = (uint)_capacity;
                h->WriteIndex = 0;
                h->ReadIndex = 0;
                h->Overflow = 0;
            }
            return mem;
        }

        /// <summary>释放资源。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_mappedHeader != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_mappedHeader);
                _mappedHeader = IntPtr.Zero;
            }
            if (_deviceHandle != IntPtr.Zero && _deviceHandle != InvalidHandleValue)
            {
                NativeCloseHandle(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
            }
        }

        // ---- P/Invoke 访问内核驱动 ----
        private const int NativeGenericRead = unchecked((int)0x80000000);
        private const int NativeGenericWrite = 0x40000000;
        private const int NativeOpenExisting = 3;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileW(
            string lpFileName, int dwDesiredAccess, int dwShareMode,
            IntPtr lpSecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory", SetLastError = false)]
        private static extern void ZeroMemory(IntPtr dest, IntPtr count);

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, IntPtr count);

        private static IntPtr NativeCreateFile(
            string name, int access, int share, IntPtr sec, int disp, int flags, IntPtr tmpl)
        {
            return CreateFileW(name, access, share, sec, disp, flags, tmpl);
        }

        private static void NativeCloseHandle(IntPtr h)
        {
            CloseHandle(h);
        }
    }
}
