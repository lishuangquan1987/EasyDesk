/*
 * MirrorMini.c — 最小化 miniport（MiniPort Driver）
 *
 * 参考：微软 WDK7 (Version 7600) 示例 \src\video\miniport\mirror\mini\
 *
 * XDDM 镜像驱动需要一对：display driver（MirrorDisp）在系统侧 + 一个 miniport
 * 驱动（本文件）提供硬件抽象层。镜像 miniport 不真正操作硬件，只提供注册的
 * 显示设备，使系统认为存在一个虚拟显示适配器，从而把 GDI 绘图派发给镜像
 * display driver。
 *
 * 平台：XP / Win7（< Win8）。
 *
 * 状态：骨架，需在 WDK7 编译验证。
 */

#include <miniport.h>
#include <ntddvdeo.h>
#include <video.h>
#include <string.h>

/* 注意：视频 miniport 驱动正确 include：
 * miniport.h（内核基础）→ ntddvdeo.h（提供 PVIDEO_POWER_MANAGEMENT、
 * PVIDEO_HW_INITIALIZATION_DATA 等类型）→ video.h（依赖这些类型）。
 * 不要包含 ntddk.h（与 miniport.h 冲突），顺序不可颠倒。 */

/* ---- DriverEntry：注册 HwVid 回调 ---- */

ULONG
DriverEntry(
    IN PVOID Context1,
    IN PVOID Context2)
{
    VIDEO_HW_INITIALIZATION_DATA HwInitData;

    memset(&HwInitData, 0, sizeof(VIDEO_HW_INITIALIZATION_DATA));
    HwInitData.HwInitDataSize = sizeof(VIDEO_HW_INITIALIZATION_DATA);
    /* 镜像 miniport 不需要真正访问硬件；以下回调在 WDK7 示例中多为空或占位。
     * 关键：设置 HwFindAdapter 以被系统枚举到。 */
    /* HwInitData.HwFindAdapter = MirrorHwFindAdapter;
     * HwInitData.HwInitialize = MirrorHwInitialize;
     * HwInitData.HwStartIO = MirrorHwStartIO;
     * ... */

    return (ULONG)VideoPortInitialize(Context1, Context2, &HwInitData, NULL);
}
