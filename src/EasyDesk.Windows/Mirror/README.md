# EasyDesk 镜像驱动（Mirror Driver）

XP/Win7 上摆脱 BitBlt 截屏瓶颈的镜像显示驱动 + EasyDesk 用户态捕获客户端。

> 关联设计：`EasyRDP/docs/EasyRDP-Mirror-Driver-Design.md`（D10 落地）
> 技术路线：方案 B（WDK7 示例驱动为底座）+ 参考 dfmirage 脏矩形协议 + 方案 X（可选脏矩形接口）

## 目录结构

```
Mirror/
├── build-release.bat            # 一键编译 + 打包到 release/（需 WDK7.1）
├── release/                     # 集中安装包（生成目录，勿提交）：
│   │                            #   mirror.dll + mirror_m.sys + MirrorDriver.inf + install.bat
│   ├── mirror.dll               #   XDDM 镜像显示驱动
│   ├── mirror_m.sys             #   miniport
│   ├── MirrorDriver.inf         #   安装 inf
│   └── install.bat              #   一键安装脚本（推荐）
├── driver/                       # 内核驱动工程（独立于 EasyDesk C# 库）
│   ├── MirrorDisp/MirrorDisp.c   # 镜像显示驱动（XDDM，记录脏矩形）
│   ├── MirrorMini/MirrorMini.c   # 最小化 miniport
│   ├── inf/MirrorDriver.inf      # 驱动安装文件（Attach.ToDesktop=1）
│   └── install.bat               # 一键安装脚本
└── client/                       # EasyDesk C# 用户态捕获客户端
    ├── MirrorScreenCapturer.cs   # IScreenCapturer + ICaptureChangesReader 实现
    └── MirrorNative.cs           # 驱动访问 P/Invoke（IOCTL/ExtEscape/共享缓冲映射）
```

### 一键打包到 release/

```bash
build-release.bat
```

自动用 WDK7.1 编译 MirrorDisp + MirrorMini，并把 `mirror.dll`、`mirror_m.sys`、`MirrorDriver.inf`、`install.bat` 集中复制到 `Mirror/release/`。之后把整个 `release/` 文件夹拷到 XP/Win7 目标机安装即可。

> `release/` 是生成目录（已被 `.gitignore` 忽略），dll/sys 是编译产物不进 git；`driver/` 下的 inf 和 install.bat 才是权威源码。

## 编译（内核驱动）

需要 **WDK 7.1（Version 7600）**（已在本机安装于 `C:\WinDDK\7600.16385.1`）。

```bash
# 编译 MirrorDisp（XDDM 镜像显示驱动）→ 产物 mirror.dll
call C:\WinDDK\7600.16385.1\bin\setenv.bat C:\WinDDK\7600.16385.1 chk WXP
cd EasyDesk/src/EasyDesk.Windows/Mirror/driver/MirrorDisp
build

# 编译 MirrorMini（视频 miniport）→ 产物 mirror_m.sys
cd ..\MirrorMini
build

# 如需 64 位 Win7 支持，用 x64 目标重新编译：
call C:\WinDDK\7600.16385.1\bin\setenv.bat C:\WinDDK\7600.16385.1 chk x64
# (分别 cd 到 MirrorDisp / MirrorMini 后 build)
```

产物位置：
- x86：`MirrorDisp/objchk_wxp_x86/i386/mirror.dll`、`MirrorMini/objchk_wxp_x86/i386/mirror_m.sys`
- x64：`MirrorDisp/objchk_win7_amd64/amd64/mirror.dll`、`MirrorMini/objchk_win7_amd64/amd64/mirror_m.sys`

> ⚠️ 驱动签名：**XP 32 位免签**；**Win7 64 位必须开测试签名 + 用测试证书给驱动签名**，否则驱动加载失败（`sc start` 报 577「无法验证数字签名」）。流程见下。

## 安装驱动（完整流程，按序执行）

把整个 `release/` 文件夹拷到目标机。**以下全部以管理员运行**。

### 第 1 步：开测试签名（仅 64 位 Win7 需要）
```
bcdedit /set testsigning on
```
然后**重启**。

### 第 2 步：信任测试证书（仅 64 位 Win7 需要）

> 驱动已在**开发机**用 WDK 的 `sign.bat` 签好名（release 里的 dll/sys 已带签名）。**目标机不需要 WDK**，只需信任那个测试证书。

运行 `release\trust.bat`。它用系统自带的 `certutil` 把 `easyrdp-test.cer` 装进「受信任的根证书颁发机构」和当前用户 My 存储。

> 若跳过此步，`sc start mirror` 会报 **577**（无法验证数字签名）。

### 第 3 步：安装驱动
运行 `release\install.bat`。脚本自动检测位数选对应驱动，用 `sc create` + `reg add` 注册内核服务与显示配置（`InstalledDisplayDrivers=mirror`、`Attach.ToDesktop=1`）。

### 第 4 步：重启并验证
重启后 `sc query mirror` 应为 **RUNNING**。

> 卸载：`sc stop mirror` + `sc delete mirror`，删除 `%SystemRoot%\System32\drivers\mirror_m*.sys` 与 `%SystemRoot%\System32\mirror*.dll`。

### 备选：右键 `MirrorDriver.inf` → 安装
inf 已含 `[DefaultInstall]` 节。若系统接受则可用，否则请用 `install.bat`。

> 卸载：`sc stop mirror` + `sc delete mirror`（需管理员），并删除 `%SystemRoot%\System32\drivers\mirror_m.sys` 与 `%SystemRoot%\System32\mirror.dll`。

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
