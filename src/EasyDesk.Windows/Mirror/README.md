# EasyDesk 镜像驱动（Mirror Driver）

XP/Win7 上摆脱 BitBlt 截屏瓶颈的镜像显示驱动 + EasyDesk 用户态捕获客户端。

> 关联设计：`EasyRDP/docs/EasyRDP-Mirror-Driver-Design.md`（D10 落地）
> 技术路线：方案 B（WDK7 示例驱动为底座）+ 参考 dfmirage 脏矩形协议 + 方案 X（可选脏矩形接口）

## 目录结构

```
Mirror/
├── driver/                       # 内核驱动工程（独立于 EasyDesk C# 库）
│   ├── MirrorDisp/MirrorDisp.c   # 镜像显示驱动（XDDM，记录脏矩形）
│   ├── MirrorMini/MirrorMini.c   # 最小化 miniport
│   └── inf/MirrorDriver.inf      # 驱动安装文件（Attach.ToDesktop=1）
└── client/                       # EasyDesk C# 用户态捕获客户端
    ├── MirrorScreenCapturer.cs   # IScreenCapturer + ICaptureChangesReader 实现
    └── MirrorNative.cs           # 驱动访问 P/Invoke（IOCTL/ExtEscape/共享缓冲映射）
```

## 编译（内核驱动）

需要 **WDK7（Version 7600）** 或 XP DDK。当前开发环境未安装 WDK，驱动代码为骨架，安装 WDK 后按以下步骤编译：

1. 安装 WDK7（Version 7600）
2. 复制 `Mirror/driver/MirrorDisp`、`MirrorMini` 到 WDK 示例目录或独立驱动工程
3. 用 WDK build 环境编译（`setenv.bat` + `build`），或挂到 VS2008 驱动工程
4. 生成的 `mirror.sys` / `mirror_m.sys` 通过 `inf/MirrorDriver.inf` 安装

> ⚠️ 驱动签名：XP 32 位免签；Win7 需测试签名（`bcdedit /set testsigning on`）或 WHQL 签名。

## 编译（EasyDesk 客户端）

客户端是普通 C#，随 EasyDesk.sln 编译即可：

```bash
dotnet build EasyDesk.sln
```

## 接入方式

`WindowsDesktopFactory.CreateScreenCapturer()` 在 XP/Win7 上探测镜像驱动是否安装：
- 已装 → `new MirrorScreenCapturer()`
- 未装/加载失败 → 回退 `new WindowsScreenCapturer()`（BitBlt）

`CaptureService` 用 `capturer as ICaptureChangesReader` 检测脏矩形能力，走增量路径。

## 状态

- [x] 方案 X 接口（`ICaptureChangesReader` + `ScreenRect`）
- [x] 驱动源码（display / miniport / inf）
- [x] **WDK7 编译验证**（WDK 7.1 + `build.exe`，`chk WXP`，产物 `mirror.dll` + `mirror_m.sys`）
  - 命令：`setenv.bat C:\WinDDK\7600.16385.1 chk WXP && cd MirrorDisp && build`
  - 已解决的关键问题：`TARGETTYPE=GDI_DRIVER`（非 DRIVER）、`winddi.h` 单头包含、DDI 签名须与 winddi.h 严格一致、sources 需纯 ASCII（nmake 逐字节解析，中文注释致解析失败）
- [ ] 用户态共享缓冲映射 + IOCTL 对接（客户端骨架，待驱动验证后细化）
