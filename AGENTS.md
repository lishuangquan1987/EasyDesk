# EasyDesk

零依赖、跨平台 .NET 桌面 I/O 抽象库 — 屏幕捕获、键鼠模拟、光标、剪贴板、桌面信息。

## Project

- **Stack**: C# / .NET SDK-style projects, multi-target `net40;netstandard2.0` (libs), `net472` (tests)
- **Entry**: `WindowsDesktopFactory` (`src/EasyDesk.Windows/WindowsDesktopFactory.cs`) — factory for all 5 desktop I/O services
- **Zero dependencies**: no NuGet PackageReference; pure P/Invoke into user32.dll, gdi32.dll, kernel32.dll

## Commands

```bash
# Build (solution-level)
dotnet build EasyDesk.sln

# Test (single project, net472 — tests require Windows desktop session)
dotnet test test/EasyDesk.Windows.Tests/EasyDesk.Windows.Tests.csproj
```

Tests physically move mouse / press keys / capture screen. Do not interact with the machine while they run.

## Architecture

```
EasyDesk.Core/                # Interfaces + models, zero deps
  DesktopFactory.cs            # Abstract factory (5 Create* methods)
  I*.cs                        # 5 service interfaces (InputSimulator, ScreenCapturer,
                               #   CursorCapturer, ClipboardService, DesktopInfo)
  Models/                      # ScreenFrame, CursorInfo, DesktopBounds, CaptureOptions,
                               #   VirtualKeyCode, MouseButton, MouseEventFlags, KeyEventFlags

EasyDesk.Windows/              # Windows P/Invoke implementations, refs Core
  Windows*Service.cs           # 5 concrete implementations
  WindowsDesktopFactory.cs     # Wires all 5 services
  NativeMethods/               # P/Invoke wrappers (User32, Gdi32, Kernel32) + structs

test/EasyDesk.Windows.Tests/   # xUnit 2.4.1 integration tests, net472
```

### Data flow

`WindowsDesktopFactory` → creates `IInputSimulator` / `IScreenCapturer` / `ICursorCapturer` / `IClipboardService` / `IDesktopInfo`. Each interface lives in Core, each implementation in Windows. Screen capture returns raw BGRA32 pixel buffer (caller must `Marshal.FreeHGlobal`). Clipboard requires STA thread.

## Roadmap

### 短期（当前版本）

- 完善 Windows P/Invoke 实现的边界情况与错误处理
- 屏幕捕获中加入光标绘制支持（当前 `CopyFromScreen` 不含光标）

### 中期（跨平台）

- **Linux (X11)**: XGetImage 屏幕捕获、XTest 输入注入、XFixes 光标、X11 剪贴板
- **macOS**: CGDisplay 屏幕捕获、CGEvent 输入、NSCursor 光标、NSPasteboard 剪贴板
- 跨平台键盘码映射（Windows VK ↔ X11 keysym ↔ macOS kVK）

### 性能优化

- Windows Aero 开启时 BitBlt 性能下降严重（GPU→CPU 回读，~30-50ms/帧）→ 未来实现 **DXGI Desktop Duplication API** 快速路径
- Linux Wayland 环境下 X11 API 完全不可用 → 需 PipeWire + xdg-desktop-portal 路径

### 已知限制

| 限制 | 影响 | 缓解 |
|---|---|---|
| `IScreenCapturer` 线程不安全 | 多线程需外部加锁 | 文档说明 |
| `IClipboardService` 必须 STA 线程 | 后台线程直接调用静默失败 | 调用方设置 `ApartmentState.STA` |
| Win7+Aero 下 BitBlt 性能差 | ~30-50ms/帧 | 关闭 Aero 或等 DXGI 路径 |
| 仅 Windows 实现 | Linux/macOS 不可用 | 计划中 |

## Conventions

### C# 5.0 (mandatory — net40 target)

| Forbidden (C# 6+) | Use instead |
|---|---|
| `$"hello {x}"` | `string.Format("hello {0}", x)` |
| `obj?.Prop` | `obj != null ? obj.Prop : default` |
| `int Foo() => x;` | `int Foo() { return x; }` |
| `{ get; set; } = 42;` | Assign in constructor |
| `nameof(X)` | String literal `"X"` |
| `if (x is Type v)` | `if (x is Type) { var v = (Type)x; }` |

### Code style

- `using` directives **inside** `namespace` blocks
- XML doc comments on all public interfaces, classes, and methods
- P/Invoke: `internal static class` under `EasyDesk.Windows.NativeMethods`
- Error handling: `InvalidOperationException` for Win32 failures, `ArgumentOutOfRangeException` for bad args
- Resource cleanup: `Marshal.FreeHGlobal` in `finally` block (ScreenFrame.Scan0)
- No `.editorconfig` — manual consistency

### Test conventions

- xUnit `[Fact]` only; naming: `Method_Scenario_ShouldNotThrow` / `ShouldReturnValidFrame`
- Smoke tests use `Record.Exception(() => ...)`
- `ScreenFrame.Scan0` freed in `finally` inside every test
- Real integration tests — actually move mouse, press keys, touch clipboard

## Notes

- Placeholder for future quick-adds.
