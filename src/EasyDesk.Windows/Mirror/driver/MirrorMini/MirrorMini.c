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

#include <ntddk.h>
#include <video.h>
#include <ntddvdeo.h>

/* ---- DriverEntry：注册 HwVid 回调 ---- */

NTSTATUS
DriverEntry(
    IN PDRIVER_OBJECT DriverObject,
    IN PUNICODE_STRING RegistryPath)
{
    VIDEO_HW_INITIALIZATION_DATA HwInitData;

    RtlZeroMemory(&HwInitData, sizeof(VIDEO_HW_INITIALIZATION_DATA));

    HwInitData.HwInitDataSize = sizeof(VIDEO_HW_INITIALIZATION_DATA);
    /* 镜像 miniport 不需要真正访问硬件；以下回调在 WDK7 示例中多为空或占位。
     * 关键：设置 HwFindAdapter 以被系统枚举到。 */
    /* HwInitData.HwFindAdapter = MirrorHwFindAdapter;
     * HwInitData.HwInitialize = MirrorHwInitialize;
     * HwInitData.HwStartIO = MirrorHwStartIO;
     * ... */

    return VideoPortInitialize(DriverObject, RegistryPath, &HwInitData, NULL);
}
