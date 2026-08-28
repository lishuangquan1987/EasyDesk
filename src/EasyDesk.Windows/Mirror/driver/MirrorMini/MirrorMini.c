/*
 * MirrorMini.c — EasyRDP video miniport driver
 *
 * Reference: Microsoft WDK7 mirror driver sample
 *   \src\video\miniport\mirror\mini\mirror.c
 *
 * XDDM mirror driver needs a pair: the display driver (MirrorDisp, system
 * side) + a miniport driver (this file) that provides the "hardware"
 * abstraction. The mirror miniport does not touch real hardware; it just
 * registers a virtual display adapter so the system enumerates it and routes
 * GDI draw operations to the mirror display driver.
 *
 * IMPORTANT: HwFindAdapter MUST be implemented and registered in DriverEntry.
 * A miniport with only HwInitDataSize set (all callbacks NULL) fails to
 * initialize -> driver load fails with WIN32_EXIT_CODE 31 (ERROR_GEN_FAILURE).
 *
 * Platform: XP / Win7 (< Win8).
 */

#include <miniport.h>
#include <dderror.h>
#include <ntddvdeo.h>
#include <video.h>
#include <string.h>

/* ---- callback stubs (mirror miniport does not touch real hardware) ---- */

VP_STATUS
MirrorFindAdapter(
    IN PVOID HwDeviceExtension,
    IN PVOID HwContext,
    IN PWSTR ArgumentString,
    IN PVIDEO_PORT_CONFIG_INFO ConfigInfo,
    OUT PUCHAR Again)
{
    UNREFERENCED_PARAMETER(HwDeviceExtension);
    UNREFERENCED_PARAMETER(HwContext);
    UNREFERENCED_PARAMETER(ArgumentString);
    UNREFERENCED_PARAMETER(ConfigInfo);
    UNREFERENCED_PARAMETER(Again);
    return NO_ERROR;
}

BOOLEAN
MirrorInitialize(
    PVOID HwDeviceExtension)
{
    UNREFERENCED_PARAMETER(HwDeviceExtension);
    return TRUE;
}

BOOLEAN
MirrorStartIO(
    PVOID HwDeviceExtension,
    PVIDEO_REQUEST_PACKET RequestPacket)
{
    UNREFERENCED_PARAMETER(HwDeviceExtension);
    UNREFERENCED_PARAMETER(RequestPacket);
    return TRUE;
}

BOOLEAN
MirrorResetHw(
    PVOID HwDeviceExtension,
    ULONG Columns,
    ULONG Rows)
{
    UNREFERENCED_PARAMETER(HwDeviceExtension);
    UNREFERENCED_PARAMETER(Columns);
    UNREFERENCED_PARAMETER(Rows);
    return TRUE;
}

BOOLEAN
MirrorVidInterrupt(
    PVOID HwDeviceExtension)
{
    UNREFERENCED_PARAMETER(HwDeviceExtension);
    return TRUE;
}

VP_STATUS
MirrorGetPowerState(
    PVOID HwDeviceExtension,
    ULONG HwId,
    PVIDEO_POWER_MANAGEMENT VideoPowerControl)
{
    UNREFERENCED_PARAMETER(HwDeviceExtension);
    UNREFERENCED_PARAMETER(HwId);
    UNREFERENCED_PARAMETER(VideoPowerControl);
    return NO_ERROR;
}

VP_STATUS
MirrorSetPowerState(
    PVOID HwDeviceExtension,
    ULONG HwId,
    PVIDEO_POWER_MANAGEMENT VideoPowerControl)
{
    UNREFERENCED_PARAMETER(HwDeviceExtension);
    UNREFERENCED_PARAMETER(HwId);
    UNREFERENCED_PARAMETER(VideoPowerControl);
    return NO_ERROR;
}

VP_STATUS
MirrorGetChildDescriptor(
    IN PVOID HwDeviceExtension,
    IN PVIDEO_CHILD_ENUM_INFO ChildEnumInfo,
    OUT PVIDEO_CHILD_TYPE pChildType,
    OUT PVOID pChildDescriptor,
    OUT PULONG pUId,
    OUT PULONG pUnused)
{
    UNREFERENCED_PARAMETER(HwDeviceExtension);
    UNREFERENCED_PARAMETER(ChildEnumInfo);
    UNREFERENCED_PARAMETER(pChildType);
    UNREFERENCED_PARAMETER(pChildDescriptor);
    UNREFERENCED_PARAMETER(pUId);
    UNREFERENCED_PARAMETER(pUnused);
    return ERROR_NO_MORE_DEVICES;
}

/* ---- DriverEntry: register HwVid callbacks ---- */

ULONG
DriverEntry(
    IN PVOID Context1,
    IN PVOID Context2)
{
    VIDEO_HW_INITIALIZATION_DATA HwInitData;

    memset(&HwInitData, 0, sizeof(VIDEO_HW_INITIALIZATION_DATA));

    HwInitData.HwInitDataSize = sizeof(VIDEO_HW_INITIALIZATION_DATA);

    /* Set entry points. HwFindAdapter is REQUIRED for the miniport to be
     * enumerated; a NULL find-adapter causes VideoPortInitialize to fail. */
    HwInitData.HwFindAdapter             = &MirrorFindAdapter;
    HwInitData.HwInitialize              = &MirrorInitialize;
    HwInitData.HwStartIO                 = &MirrorStartIO;
    HwInitData.HwResetHw                 = &MirrorResetHw;
    HwInitData.HwInterrupt               = &MirrorVidInterrupt;
    HwInitData.HwGetPowerState           = &MirrorGetPowerState;
    HwInitData.HwSetPowerState           = &MirrorSetPowerState;
    HwInitData.HwGetVideoChildDescriptor = &MirrorGetChildDescriptor;

    return (ULONG)VideoPortInitialize(Context1, Context2, &HwInitData, NULL);
}
