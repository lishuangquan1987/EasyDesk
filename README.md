# EasyDesk

> **零依赖、跨平台 .NET 桌面 I/O 抽象库**
>
> 提供屏幕捕获、鼠标键盘模拟、光标获取、剪贴板操作、桌面信息查询五类能力。
> 双目标 `net40` + `netstandard2.0`，纯 P/Invoke，无需安装任何第三方 NuGet 包。

---

## 平台支持

| 平台 | 屏幕捕获 | 输入模拟 | 光标 | 剪贴板 | 桌面信息 |
|---|---|---|---|---|---|
| **Windows** (net40 / netstandard2.0) | ✅ BitBlt + CopyFromScreen | ✅ SendInput | ✅ GetCursorInfo | ✅ OpenClipboard API | ✅ GetSystemMetrics |
| Linux (X11) | ⏳ 计划中 (XGetImage) | ⏳ 计划中 (XTest) | ⏳ 计划中 (XFixes) | ⏳ 计划中 | ⏳ 计划中 |
| macOS | ⏳ 计划中 (CGDisplay) | ⏳ 计划中 (CGEvent) | ⏳ 计划中 (NSCursor) | ⏳ 计划中 | ⏳ 计划中 |

---

## 架构

```
EasyDesk.Core (net40;netstandard2.0)
  接口 + 模型，零依赖
  IInputSimulator / IScreenCapturer / ICursorCapturer
  IClipboardService / IDesktopInfo / DesktopFactory

        ↓ 引用

EasyDesk.Windows (net40;netstandard2.0)
  P/Invoke 实现，零依赖
  ┌───────────────────────────────────┐
  │ user32.dll → SendInput            │  鼠标/键盘注入
  │            → GetCursorInfo         │  光标捕获
  │            → GetSystemMetrics      │  屏幕尺寸
  │            → EnumDisplayMonitors   │  多显示器
  │            → OpenClipboard...      │  剪贴板
  ├───────────────────────────────────┤
  │ gdi32.dll  → BitBlt               │  屏幕捕获
  │            → CreateCompatibleDC    │
  ├───────────────────────────────────┤
  │ kernel32.dll → RtlMoveMemory      │  内存拷贝
  │              → GlobalAlloc/Lock    │  剪贴板内存
  └───────────────────────────────────┘
```

---

## API 总览

### IInputSimulator — 输入模拟

| 方法 | 说明 |
|---|---|
| `void SendMouseMove(int x, int y, bool absolute)` | 移动鼠标。absolute=true 时坐标映射 0-65535（绝对坐标），false 时为相对像素偏移 |
| `void SendMouseButton(MouseButton button, bool down)` | 按下/释放鼠标按钮。支持 Left/Right/Middle/XButton1/XButton2 |
| `void SendMouseWheel(int delta)` | 滚轮。正值向上(WHEEL_DELTA=120)，负值向下 |
| `void SendKeyDown(VirtualKeyCode key)` | 按下键盘按键，使用 Windows VK 码 |
| `void SendKeyUp(VirtualKeyCode key)` | 释放键盘按键 |
| `void SendText(string text)` | 直接发送 Unicode 文本（通过 KEYEVENTF_UNICODE），不依赖键盘布局 |

### IScreenCapturer — 屏幕捕获

| 方法 | 说明 |
|---|---|
| `ScreenFrame CaptureScreen()` | 捕获完整虚拟桌面（所有显示器），返回原始 BGRA32 像素指针 |
| `ScreenFrame CaptureScreen(CaptureOptions options)` | 带选项捕获：可排除光标、指定目标显示器 |
| `ScreenFrame CaptureRegion(int x, int y, int width, int height)` | 捕获指定矩形区域 |
| `DesktopBounds GetPrimaryScreen()` | 获取主显示器边界 |
| `DesktopBounds[] GetAllScreens()` | 获取所有显示器边界（按 EnumDisplayMonitors 顺序） |

### ICursorCapturer — 光标捕获

| 方法 | 说明 |
|---|---|
| `CursorInfo GetCursorInfo()` | 获取完整光标信息：屏幕坐标、热区、AND/XOR 掩码位图 |
| `void GetCursorPosition(out int x, out int y)` | 仅获取光标屏幕坐标 |

### IClipboardService — 剪贴板

| 方法 | 说明 |
|---|---|
| `string GetText()` | 读取文本剪贴板内容（CF_UNICODETEXT） |
| `void SetText(string text)` | 写入文本到剪贴板 |
| `bool ContainsText()` | 剪贴板当前是否包含文本 |

### IDesktopInfo — 桌面信息

| 方法 | 说明 |
|---|---|
| `DesktopBounds GetPrimaryBounds()` | 主显示器坐标和尺寸 |
| `DesktopBounds[] GetAllBounds()` | 所有显示器坐标和尺寸列表 |
| `DesktopBounds GetVirtualScreenBounds()` | 虚拟桌面包围盒（所有显示器的最小包围矩形） |

### DesktopFactory — 工厂接口

| 方法 | 说明 |
|---|---|
| `IInputSimulator CreateInputSimulator()` | 创建输入模拟器 |
| `IScreenCapturer CreateScreenCapturer()` | 创建屏幕捕获器 |
| `ICursorCapturer CreateCursorCapturer()` | 创建光标捕获器 |
| `IClipboardService CreateClipboardService()` | 创建剪贴板服务 |
| `IDesktopInfo CreateDesktopInfo()` | 创建桌面信息服务 |

### WindowsDesktopFactory — Windows 实现入口

```csharp
// 一行创建所有实现
var factory = new WindowsDesktopFactory();
var input = factory.CreateInputSimulator();
var capturer = factory.CreateScreenCapturer();

// 截个图
var frame = capturer.CaptureScreen();
// 模拟 Ctrl+C
input.SendKeyDown(VirtualKeyCode.VK_CONTROL);
input.SendKeyDown(VirtualKeyCode.VK_C);
input.SendKeyUp(VirtualKeyCode.VK_C);
input.SendKeyUp(VirtualKeyCode.VK_CONTROL);
```

---

## 数据模型

### ScreenFrame

```csharp
public class ScreenFrame
{
    public IntPtr Scan0;       // 像素数据指针 (BGRA32 格式)
    public int Width;          // 宽度（像素）
    public int Height;         // 高度（像素）
    public int Stride;         // 每行字节数 (= Width * 4)
    public int PixelFormat;    // 像素格式，当前固定为 0 (BGRA32)
}
```

**生命周期约定**：调用者负责释放 `Scan0` 指向的内存。使用完后调用 `Marshal.FreeHGlobal(frame.Scan0)`。EasyDesk 不持有 Bitmap 对象，只返回元数据+指针。

### CursorInfo

```csharp
public class CursorInfo
{
    public int X;              // 屏幕 X 坐标
    public int Y;              // 屏幕 Y 坐标
    public int HotspotX;       // 热区 X（相对于光标图像左上角）
    public int HotspotY;       // 热区 Y
    public int Width;          // 光标图像宽度
    public int Height;         // 光标图像高度
    public byte[] ImageData;   // AND 掩码 + XOR 掩码（标准 Windows 游标格式）
}
```

### DesktopBounds

```csharp
public class DesktopBounds
{
    public int X;              // 左上角 X（相对于虚拟桌面）
    public int Y;              // 左上角 Y
    public int Width;          // 宽度
    public int Height;         // 高度
    public bool IsPrimary;     // 是否为主显示器
}
```

### CaptureOptions

```csharp
public class CaptureOptions
{
    public bool IncludeCursor;      // 截图中是否包含光标（默认 true）
    public int TargetDisplay;       // 目标显示器索引：-1=全部虚拟桌面, 0=主屏, 1=第二屏...
}
```

### VirtualKeyCode

完整的 Windows Virtual-Key Code 枚举（250+ 值），从 `VK_LBUTTON = 0x01` 到 `VK_OEM_CLEAR = 0xFE`。

```csharp
public enum VirtualKeyCode
{
    VK_LBUTTON = 0x01,
    VK_RBUTTON = 0x02,
    VK_CANCEL = 0x03,
    VK_MBUTTON = 0x04,
    VK_BACK = 0x08,
    VK_TAB = 0x09,
    VK_RETURN = 0x0D,
    VK_SHIFT = 0x10,
    VK_CONTROL = 0x11,
    VK_MENU = 0x12,        // Alt
    VK_PAUSE = 0x13,
    VK_CAPITAL = 0x14,     // Caps Lock
    VK_ESCAPE = 0x1B,
    VK_SPACE = 0x20,
    VK_PRIOR = 0x21,       // Page Up
    VK_NEXT = 0x22,        // Page Down
    VK_END = 0x23,
    VK_HOME = 0x24,
    VK_LEFT = 0x25,
    VK_UP = 0x26,
    VK_RIGHT = 0x27,
    VK_DOWN = 0x28,
    VK_SNAPSHOT = 0x2C,    // Print Screen
    VK_INSERT = 0x2D,
    VK_DELETE = 0x2E,
    // 0x30-0x39: 数字 0-9
    VK_0 = 0x30, VK_1 = 0x31, VK_2 = 0x32, VK_3 = 0x33, VK_4 = 0x34,
    VK_5 = 0x35, VK_6 = 0x36, VK_7 = 0x37, VK_8 = 0x38, VK_9 = 0x39,
    // 0x41-0x5A: A-Z
    VK_A = 0x41, VK_B = 0x42, /* ... */ VK_Z = 0x5A,
    VK_LWIN = 0x5B,
    VK_RWIN = 0x5C,
    VK_NUMPAD0 = 0x60, /* ... */ VK_NUMPAD9 = 0x69,
    VK_F1 = 0x70, /* ... */ VK_F24 = 0x87,
    VK_NUMLOCK = 0x90,
    VK_SCROLL = 0x91,
    VK_LSHIFT = 0xA0,
    VK_RSHIFT = 0xA1,
    VK_LCONTROL = 0xA2,
    VK_RCONTROL = 0xA3,
    VK_LMENU = 0xA4,
    VK_RMENU = 0xA5,
    VK_VOLUME_MUTE = 0xAD,
    VK_VOLUME_DOWN = 0xAE,
    VK_VOLUME_UP = 0xAF,
    VK_OEM_1 = 0xBA,        // ;:
    VK_OEM_PLUS = 0xBB,     // +
    VK_OEM_COMMA = 0xBC,    // ,
    VK_OEM_MINUS = 0xBD,    // -
    VK_OEM_PERIOD = 0xBE,   // .
    VK_OEM_2 = 0xBF,        // /?
    VK_OEM_3 = 0xC0,        // `~
    VK_OEM_4 = 0xDB,        // [{
    VK_OEM_5 = 0xDC,        // \|
    VK_OEM_6 = 0xDD,        // ]}
    VK_OEM_7 = 0xDE,        // '"
    VK_OEM_8 = 0xDF,
    VK_OEM_102 = 0xE2,      // <> on non-US keyboards
}
```

完整枚举定义见 `src/EasyDesk.Core/Models/VirtualKeyCode.cs`。

---

## 注意事项

### .NET 4.0 / C# 5.0 语法约束

本项目同时 target `net40`，所有代码必须兼容 C# 5.0。以下语法**禁止使用**：

| 禁止 (C# 6.0+) | 替代写法 (C# 5.0) |
|---|---|
| `$"hello {x}"` | `string.Format("hello {0}", x)` |
| `async / await` | `Task.Factory.StartNew()` 或 `BeginXXX/EndXXX` |
| `obj?.Prop` | `obj != null ? obj.Prop : default` |
| `{ get; set; } = 42` | 构造函数中赋值 |
| `int Foo() => x;` | `int Foo() { return x; }` |
| `nameof(X)` | 字符串常量 `"X"` |
| `if (x is Type v)` | `if (x is Type) { var v = (Type)x; }` |

可安全使用：`var`、Lambda (`x => ...`)、LINQ、`Task.Factory.StartNew`、对象/集合初始化器、自动属性 `{ get; set; }`。

### 线程安全

| 组件 | 线程安全性 | 说明 |
|---|---|---|
| `IInputSimulator` | ✅ 线程安全 | `SendInput` 是 Win32 原子 API |
| `IScreenCapturer` | ❌ 线程不安全 | 内部 DC/Bitmap 不是线程安全的，多线程调用需要外部加锁 |
| `ICursorCapturer` | ✅ 线程安全 | 仅读取，无共享状态 |
| `IClipboardService` | ❌ 线程不安全 | 必须在同一线程上序列化调用。内部用 `OpenClipboard`/`CloseClipboard` 配对 |

### 剪贴板：STA 线程要求

使用剪贴板 API 的线程必须是 **STA (Single-Threaded Apartment)**：

```csharp
// WinForms / WPF 主线程默认是 STA，可以直接使用
var clip = factory.CreateClipboardService();
clip.SetText("hello");

// 后台线程必须显式设置
var thread = new Thread(() =>
{
    // 无效！剪贴板操作会失败
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
```

### ScreenFrame.Scan0 生命周期

`CaptureScreen()` / `CaptureRegion()` 返回的 `ScreenFrame.Scan0` 指向**新分配的堆内存**，由调用者负责释放：

```csharp
var frame = capturer.CaptureScreen();
try
{
    // 处理像素数据...
    ProcessPixels(frame.Scan0, frame.Width, frame.Height, frame.Stride);
}
finally
{
    Marshal.FreeHGlobal(frame.Scan0);  // 必须调用！
}
```

**不调用会导致内存泄漏，每次截屏泄漏 `Width × Height × 4` 字节。**

### 屏幕捕获性能：BitBlt vs Aero

| 场景 | 性能 |
|---|---|
| Windows 7 **Aero 关闭**（经典主题） | BitBlt 极快，~3ms/帧 |
| Windows 7 **Aero 开启** | BitBlt 性能急剧下降，~30-50ms/帧 |
| Windows 8+ (DWM 始终开启) | BitBlt 稳定在 ~5-15ms/帧 |

**Aero 开启时光栅操作（SRCCOPY）强制 GPU→CPU 回读，导致性能显著下降。** 如果目标环境是 Win7+Aero，建议提醒用户关闭 Aero 或在未来版本实现 DXGI Desktop Duplication API 快速路径。

### Linux Wayland 兼容性

Linux 实现（计划中）使用 X11 API（`XGetImage`、`XTest`）。在 Wayland 桌面环境下，这些 API **完全不可用**。Wayland 屏幕捕获需要 PipeWire + xdg-desktop-portal，输入注入需要 libportal。X11 实现仅覆盖：
- 传统 X11 桌面（XFCE、LXDE、i3 等）
- GNOME/KDE 的 X11 会话模式

### macOS 权限要求

macOS 10.14+（计划中）需要两项系统权限：
1. **屏幕录制权限**（System Preferences → Security & Privacy → Screen Recording）
   - 无权限时 `CGDisplayCreateImage` 返回 `nil`，不报错
   - 需在 app bundle 中声明 `NSScreenCaptureUsageDescription`
2. **辅助功能权限**（System Preferences → Security & Privacy → Accessibility）
   - 无权限时 `CGEventPost` 静默失败
   - 需在 app bundle 中声明 `NSAccessibilityUsageDescription`

使用前建议调用 `CGPreflightScreenCaptureAccess()` 和 `AXIsProcessTrusted()` 检测权限状态。

### 跨平台键盘码映射

`VirtualKeyCode` 枚举当前基于 **Windows VK 码**。未来跨平台实现时需要在平台层做映射：

| 按键 | Windows VK | X11 keysym | macOS kVK |
|---|---|---|---|
| A | `0x41` | `0x0061` | `0x00` |
| Enter | `0x0D` | `0xFF0D` | `0x24` |
| Escape | `0x1B` | `0xFF1B` | `0x35` |
| Left Arrow | `0x25` | `0xFF51` | `0x7B` |

将在 `VirtualKeyCode` 枚举上通过扩展方法或 `KeyMapper` 工具类做映射，而不是修改枚举值本身。

### 多显示器坐标空间

不同平台的坐标空间差异：

| 平台 | 原点 | 坐标空间 |
|---|---|---|
| **Windows** | 虚拟桌面左上角（可能是负坐标） | 所有显示器拼成一个大虚屏，最远显示器原点为 (0,0) 或负值 |
| **Linux X11** | XRandR 虚屏左上角 | 类似 Windows，通过 XRandR 获取各输出偏移 |
| **macOS** | 每个显示器独立坐标系 | `CGDisplayBounds` 返回的 origin 在主屏下方为负值 |

`GetAllScreens()` 返回的 `DesktopBounds` 中的 X/Y 坐标是各平台原生坐标。使用前需理解坐标空间的差异。

---

## 项目结构

```
EasyDesk/
├── EasyDesk.sln
├── README.md
├── LICENSE (MIT)
├── .gitignore
├── src/
│   ├── EasyDesk.Core/                    # TargetFrameworks: net40;netstandard2.0
│   │   ├── EasyDesk.Core.csproj          # 零 PackageReference
│   │   ├── IInputSimulator.cs
│   │   ├── IScreenCapturer.cs
│   │   ├── ICursorCapturer.cs
│   │   ├── IClipboardService.cs
│   │   ├── IDesktopInfo.cs
│   │   ├── DesktopFactory.cs
│   │   └── Models/
│   │       ├── MouseButton.cs
│   │       ├── MouseEventFlags.cs
│   │       ├── KeyEventFlags.cs
│   │       ├── VirtualKeyCode.cs
│   │       ├── ScreenFrame.cs
│   │       ├── CursorInfo.cs
│   │       ├── DesktopBounds.cs
│   │       └── CaptureOptions.cs
│   └── EasyDesk.Windows/                  # TargetFrameworks: net40;netstandard2.0
│       ├── EasyDesk.Windows.csproj        # 零 PackageReference
│       ├── WindowsInputSimulator.cs
│       ├── WindowsScreenCapturer.cs
│       ├── WindowsCursorCapturer.cs
│       ├── WindowsClipboardService.cs
│       ├── WindowsDesktopInfo.cs
│       ├── WindowsDesktopFactory.cs
│       └── NativeMethods/
│           ├── User32.cs
│           ├── Gdi32.cs
│           ├── Kernel32.cs
│           └── Win32Structs.cs
└── test/
    └── EasyDesk.Windows.Tests/            # net472, xUnit
        ├── EasyDesk.Windows.Tests.csproj
        ├── InputSimulatorTests.cs
        ├── ScreenCapturerTests.cs
        ├── CursorCapturerTests.cs
        └── ClipboardServiceTests.cs
```

---

## 使用示例

### 截屏并保存为文件

```csharp
var factory = new WindowsDesktopFactory();
var capturer = factory.CreateScreenCapturer();

var frame = capturer.CaptureScreen();
try
{
    using (var bmp = new Bitmap(frame.Width, frame.Height, frame.Stride,
        System.Drawing.Imaging.PixelFormat.Format32bppArgb, frame.Scan0))
    {
        bmp.Save(@"C:\screenshot.png", System.Drawing.Imaging.ImageFormat.Png);
    }
}
finally
{
    System.Runtime.InteropServices.Marshal.FreeHGlobal(frame.Scan0);
}
```

### 发送组合键 (Ctrl+C)

```csharp
var factory = new WindowsDesktopFactory();
var input = factory.CreateInputSimulator();

input.SendKeyDown(VirtualKeyCode.VK_CONTROL);
input.SendKeyDown(VirtualKeyCode.VK_C);
input.SendKeyUp(VirtualKeyCode.VK_C);
input.SendKeyUp(VirtualKeyCode.VK_CONTROL);
```

### 发送中文文本

```csharp
var input = factory.CreateInputSimulator();
input.SendText("你好世界！");  // 通过 KEYEVENTF_UNICODE，不依赖输入法
```

### 获取多显示器信息

```csharp
var info = factory.CreateDesktopInfo();
var screens = info.GetAllScreens();
foreach (var screen in screens)
{
    Console.WriteLine("{0}: X={1}, Y={2}, W={3}, H={4}, Primary={5}",
        Array.IndexOf(screens, screen), screen.X, screen.Y,
        screen.Width, screen.Height, screen.IsPrimary);
}
```

---

## 许可证

MIT License — 详见 [LICENSE](LICENSE) 文件。无限制商用。
