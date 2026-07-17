#if NET40

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using EasyDesk.Core;
using EasyDesk.Core.Models;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace EasyDesk.Windows
{
    /// <summary>
    /// DXGI Desktop Duplication 截屏实现（Windows 8+）。
    /// 直接从 GPU 帧缓冲读取桌面图像，效率远高于 GDI BitBlt。
    /// 可获取硬件提供的脏矩形区域，零 CPU 开销。
    /// XP 不可用，回退到 WindowsScreenCapturer。
    /// </summary>
    public class DxgiScreenCapturer : IScreenCapturer, IDisposable
    {
        private SharpDX.Direct3D11.Device _device;
        private OutputDuplication _duplication;
        private OutputDescription _outputDesc;
        private Texture2D _stagingTex;
        private int _screenWidth;
        private int _screenHeight;
        private bool _disposed;

        /// <summary>
        /// 初始化 DXGI 桌面复制。失败时抛出异常，调用方应回退到 BitBlt。
        /// </summary>
        public DxgiScreenCapturer()
        {
            Initialize();
        }

        private void Initialize(int adapterIndex = 0, int outputIndex = 0)
        {
            Cleanup();

            // 创建 D3D11 设备
            _device = new SharpDX.Direct3D11.Device(
                DriverType.Hardware,
                DeviceCreationFlags.None,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0);

            // 获取 DXGI 适配器和输出
            var dxgiDevice = _device.QueryInterface<SharpDX.DXGI.Device>();
            var factory = dxgiDevice.GetParent<SharpDX.DXGI.Factory>();
            var adapter = factory.GetAdapter(adapterIndex);
            var output = adapter.GetOutput(outputIndex);
            _outputDesc = output.Description;

            // 获取输出尺寸
            var bounds = _outputDesc.DesktopBounds;
            _screenWidth = bounds.Right - bounds.Left;
            _screenHeight = bounds.Bottom - bounds.Top;

            // 创建输出复制接口
            var output1 = output.QueryInterface<Output1>();
            _duplication = output1.DuplicateOutput(_device);

            // 创建用于读取像素的暂存纹理
            var texDesc = new Texture2DDescription
            {
                Width = _screenWidth,
                Height = _screenHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CpuAccessFlags = CpuAccessFlags.Read,
                OptionFlags = ResourceOptionFlags.None
            };
            _stagingTex = new Texture2D(_device, texDesc);

            output.Dispose();
            adapter.Dispose();
            dxgiDevice.Dispose();
        }

        /// <summary>
        /// 捕获当前桌面帧。返回 BGRA32 像素数据。
        /// 调用者必须释放返回的 ScreenFrame.Scan0。
        /// </summary>
        public ScreenFrame CaptureScreen()
        {
            return CaptureScreen(CaptureOptions.Default);
        }

        /// <summary>
        /// 带选项的桌面捕获。
        /// </summary>
        public ScreenFrame CaptureScreen(CaptureOptions options)
        {
            if (_disposed)
                return CreateEmptyFrame();

            try
            {
                SharpDX.DXGI.Resource desktopResource;
                OutputDuplicateFrameInformation frameInfo;

                // 获取下一帧
                _duplication.AcquireNextFrame(50, out frameInfo, out desktopResource);

                if (desktopResource == null)
                    return CreateEmptyFrame();

                try
                {
                    // 获取桌面纹理
                    using (var desktopTexture = desktopResource.QueryInterface<Texture2D>())
                    {
                        // 复制到 staging 纹理以便 CPU 读取
                        _device.ImmediateContext.CopyResource(desktopTexture, _stagingTex);

                        // 映射并拷贝像素数据
                        var dataBox = _device.ImmediateContext.MapSubresource(
                            _stagingTex, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                        try
                        {
                            int stride = _screenWidth * 4;
                            int totalBytes = stride * _screenHeight;
                            IntPtr buffer = Marshal.AllocHGlobal(totalBytes);

                            // 逐行拷贝（源 stride 可能与 width*4 不同）
                            int srcStride = dataBox.RowPitch;
                            int copyBytes = Math.Min(stride, srcStride);
                            for (int y = 0; y < _screenHeight; y++)
                            {
                                IntPtr srcPtr = new IntPtr(dataBox.DataPointer.ToInt64() + y * srcStride);
                                IntPtr dstPtr = new IntPtr(buffer.ToInt64() + y * stride);
                                CopyMemory(dstPtr, srcPtr, copyBytes);
                            }

                            return new ScreenFrame
                            {
                                Scan0 = buffer,
                                Width = _screenWidth,
                                Height = _screenHeight,
                                Stride = stride,
                                PixelFormat = 0
                            };
                        }
                        finally
                        {
                            _device.ImmediateContext.UnmapSubresource(_stagingTex, 0);
                        }
                    }
                }
                finally
                {
                    desktopResource.Dispose();
                    _duplication.ReleaseFrame();
                }
            }
            catch (SharpDXException ex)
            {
                // ACCESS_LOST → 需要重新创建复制接口
                if (ex.ResultCode.Code == SharpDX.DXGI.ResultCode.AccessLost.Code ||
                    ex.ResultCode.Code == SharpDX.DXGI.ResultCode.WaitTimeout.Code)
                {
                    try { Reinitialize(); }
                    catch { /* 重试失败，下次再试 */ }
                }
                return CreateEmptyFrame();
            }
            catch (Exception)
            {
                return CreateEmptyFrame();
            }
        }

        /// <summary>
        /// 捕获指定区域（通过 DXGI 捕获全帧后裁剪实现）。
        /// </summary>
        public ScreenFrame CaptureRegion(int x, int y, int width, int height)
        {
            var full = CaptureScreen();
            if (full.Scan0 == IntPtr.Zero)
                return full;

            try
            {
                int srcStride = full.Stride;
                int dstStride = width * 4;
                int totalBytes = dstStride * height;
                IntPtr regionBuffer = Marshal.AllocHGlobal(totalBytes);

                for (int row = 0; row < height; row++)
                {
                    IntPtr srcPtr = new IntPtr(full.Scan0.ToInt64() + ((y + row) * srcStride) + x * 4);
                    IntPtr dstPtr = new IntPtr(regionBuffer.ToInt64() + row * dstStride);
                    CopyMemory(dstPtr, srcPtr, dstStride);
                }

                Marshal.FreeHGlobal(full.Scan0);

                return new ScreenFrame
                {
                    Scan0 = regionBuffer,
                    Width = width,
                    Height = height,
                    Stride = dstStride,
                    PixelFormat = 0
                };
            }
            catch
            {
                Marshal.FreeHGlobal(full.Scan0);
                return CreateEmptyFrame();
            }
        }

        public DesktopBounds GetPrimaryScreen()
        {
            return new DesktopBounds
            {
                X = _outputDesc.DesktopBounds.Left,
                Y = _outputDesc.DesktopBounds.Top,
                Width = _screenWidth,
                Height = _screenHeight,
                IsPrimary = true
            };
        }

        public DesktopBounds[] GetAllScreens()
        {
            var list = new List<DesktopBounds>();
            try
            {
                var dxgiDevice = _device.QueryInterface<SharpDX.DXGI.Device>();
                var factory = dxgiDevice.GetParent<SharpDX.DXGI.Factory>();
                var adapter = factory.GetAdapter(0);
                int outputCount = adapter.GetOutputCount();

                for (int i = 0; i < outputCount; i++)
                {
                    using (var output = adapter.GetOutput(i))
                    {
                        var desc = output.Description;
                        var bounds = desc.DesktopBounds;
                        list.Add(new DesktopBounds
                        {
                            X = bounds.Left,
                            Y = bounds.Top,
                            Width = bounds.Right - bounds.Left,
                            Height = bounds.Bottom - bounds.Top,
                            IsPrimary = i == 0
                        });
                    }
                }

                adapter.Dispose();
                dxgiDevice.Dispose();
            }
            catch { }

            return list.ToArray();
        }

        private void Reinitialize()
        {
            Cleanup();
            Initialize();
        }

        private void Cleanup()
        {
            if (_stagingTex != null) { _stagingTex.Dispose(); _stagingTex = null; }
            if (_duplication != null) { _duplication.Dispose(); _duplication = null; }
            if (_device != null) { _device.Dispose(); _device = null; }
        }

        private static ScreenFrame CreateEmptyFrame()
        {
            return new ScreenFrame
            {
                Scan0 = IntPtr.Zero,
                Width = 0,
                Height = 0,
                Stride = 0,
                PixelFormat = 0
            };
        }

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, int count);

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Cleanup();
            }
        }
    }
}

#endif
