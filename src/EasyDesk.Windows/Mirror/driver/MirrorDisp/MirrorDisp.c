/*
 * MirrorDisp.c — XDDM 镜像显示驱动（Mirror Display Driver）
 *
 * 参考：微软 WDK7 (Version 7600) 示例 \src\video\displays\mirror\disp\，
 *       以及 dfmirage 的脏矩形（dirty-rect）协议思想。
 *
 * 作用：作为虚拟显示设备挂到 GDI 桌面绘图层之上，GDI 把与该镜像区域相交的
 *       2D 绘图操作（DrvBitBlt / DrvCopyBits / DrvTextOut / DrvStretchBlt 等）
 *       派发给本驱动。驱动在回调里把被写入的矩形记录到共享内存缓冲
 *       （changes buffer），用户态客户端（MirrorScreenCapturer）通过
 *       设备控制码读取脏矩形，从而只编码/发送变化区域。
 *
 * 平台：XDDM 镜像驱动，仅 XP / Win7（< Win8）。
 *
 * 状态：骨架实现，需在 WDK7 环境编译并补充共享缓冲的用户态映射
 *       （PMDL + MapUserPhysicalPages）后验证。当前环境未安装 WDK，
 *       代码先落盘。
 */

#include <ntddk.h>
#include <winddi.h>
#include <dderror.h>

/* ---- 与用户态客户端共享的结构定义 ---- */
/* 脏矩形记录 */
typedef struct _MIRROR_CHANGES_RECORD
{
    ULONG Type;      /* 0 = 区域, 1 = 整屏(全帧) */
    RECTL Rect;      /* 脏矩形（left/top/right/bottom） */
} MIRROR_CHANGES_RECORD;

/* 环形脏矩形缓冲头部（用户态映射共享内存后按此解析） */
typedef struct _MIRROR_CHANGES_HEADER
{
    volatile ULONG WriteIndex;   /* 驱动写入位置（环形） */
    volatile ULONG ReadIndex;    /* 用户态已读位置（环形） */
    volatile ULONG Overflow;     /* 非0 = 缓冲溢出, 用户态应回退整屏 */
    ULONG Capacity;              /* 记录容量 */
    MIRROR_CHANGES_RECORD Records[1]; /* 记录数组, 实际按 Capacity 分配 */
} MIRROR_CHANGES_HEADER;

/* 脏矩形缓冲默认容量（条数） */
#define MIRROR_DEFAULT_CAPACITY 4096

/* ---- 每设备（PDEV）状态。XDDM 镜像驱动禁止全局状态，必须 per-PDEV ---- */
typedef struct _MIRROR_DEV
{
    SIZEL ScreenSize;                 /* 屏幕尺寸 */
    MIRROR_CHANGES_HEADER *pChanges;  /* 内核映射的共享缓冲虚拟地址 */
    ULONG ChangesSize;                /* 共享缓冲字节数 */
    ULONG ChangesCapacity;            /* 记录容量 */
    PMDL MdlChanges;                  /* 共享缓冲的 MDL（供用户态映射） */
} MIRROR_DEV;

/* ---- 工具函数：向共享缓冲写入一条脏矩形记录 ---- */
static VOID
MirrorLogChange(
    MIRROR_DEV *pDev,
    CONST RECTL *prcl)
{
    ULONG next;
    RECTL clipped;
    MIRROR_CHANGES_HEADER *pHead;

    if (pDev == NULL || pDev->pChanges == NULL || prcl == NULL)
        return;

    pHead = pDev->pChanges;

    /* 裁剪到屏幕范围 */
    clipped = *prcl;
    if (clipped.left < 0) clipped.left = 0;
    if (clipped.top < 0) clipped.top = 0;
    if (clipped.right > (LONG)pDev->ScreenSize.cx) clipped.right = pDev->ScreenSize.cx;
    if (clipped.bottom > (LONG)pDev->ScreenSize.cy) clipped.bottom = pDev->ScreenSize.cy;
    if (clipped.right <= clipped.left || clipped.bottom <= clipped.top)
        return; /* 空矩形 */

    next = (pHead->WriteIndex + 1) % pHead->Capacity;
    if (next == pHead->ReadIndex)
    {
        /* 环形缓冲已满：标记溢出，用户态应回退整屏 */
        pHead->Overflow = 1;
        return;
    }

    /* 空间可用：若此前曾溢出（用户态已消费腾出空间），清除溢出标志，恢复正常增量 */
    if (pHead->Overflow)
        pHead->Overflow = 0;

    pHead->Records[pHead->WriteIndex].Type = 0; /* 区域 */
    pHead->Records[pHead->WriteIndex].Rect = clipped;
    pHead->WriteIndex = next;
}

/* ---- DDI 回调：把被绘制的区域记入脏矩形 ---- */

static BOOL
MirrorDrvCopyBits(
    SURFOBJ *pso,
    SURFOBJ *psoDst,
    SURFOBJ *psoSrc,
    CLIPOBJ *pco,
    XLATEOBJ *pxlo,
    RECTL *prclDst,
    POINTL *pptlSrc)
{
    if (prclDst != NULL)
        MirrorLogChange((MIRROR_DEV *)(pso->dhpdev), prclDst);
    return TRUE;
}

static BOOL
MirrorDrvBitBlt(
    SURFOBJ *psoTrg,
    SURFOBJ *psoSrc,
    SURFOBJ *psoMask,
    CLIPOBJ *pco,
    XLATEOBJ *pxlo,
    RECTL *prclTrg,
    POINTL *pptlSrc,
    POINTL *pptlMask,
    BRUSHOBJ *pbo,
    POINTL *pptlBrush,
    ROP4 rop)
{
    if (prclTrg != NULL)
        MirrorLogChange((MIRROR_DEV *)(psoTrg->dhpdev), prclTrg);
    return TRUE;
}

static BOOL
MirrorDrvTextOut(
    SURFOBJ *pso,
    STROBJ *pstro,
    FONTOBJ *pfo,
    CLIPOBJ *pco,
    RECTL *prclExtra,
    RECTL *prclOpaque,
    BRUSHOBJ *pboFore,
    BRUSHOBJ *pboOpaque,
    POINTL *pptlOrg,
    MIX mix)
{
    if (prclOpaque != NULL)
        MirrorLogChange((MIRROR_DEV *)(pso->dhpdev), prclOpaque);
    return TRUE;
}

static BOOL
MirrorDrvStretchBlt(
    SURFOBJ *psoDst,
    SURFOBJ *psoSrc,
    SURFOBJ *psoMask,
    CLIPOBJ *pco,
    XLATEOBJ *pxlo,
    COLORADJUSTMENT *pca,
    POINTL *pptlHTOrg,
    RECTL *prclDst,
    RECTL *prclSrc,
    POINTL *pptlMask,
    ULONG iMode)
{
    if (prclDst != NULL)
        MirrorLogChange((MIRROR_DEV *)(psoDst->dhpdev), prclDst);
    return TRUE;
}

/* 其余 DDI 回调：仅需桩以保证链接通过（镜像驱动不真正绘制表面）。 */

static ULONG
MirrorDrvDitherColor(
    DHPDEV dhpdev,
    ULONG iMode,
    ULONG rgb,
    ULONG *pul)
{
    return 0;
}

static BOOL
MirrorDrvRealizeBrush(
    BRUSHOBJ *pbo,
    SURFOBJ *psoTarget,
    SURFOBJ *psoPattern,
    SURFOBJ *psoMask,
    XLATEOBJ *pxlo,
    ULONG iHatch)
{
    return TRUE;
}

static BOOL
MirrorDrvSetPointerShape(
    SURFOBJ *pso,
    SURFOBJ *psoMask,
    SURFOBJ *psoColor,
    XLATEOBJ *pxlo,
    RECTL *prcl,
    RECTL *prclEx,
    POINTL *pptlHot,
    RECTL *prclMask,
    FLONG fl)
{
    return TRUE;
}

static BOOL
MirrorDrvMovePointer(
    SURFOBJ *pso,
    LONG x,
    LONG y,
    RECTL *prcl)
{
    return TRUE;
}

static BOOL
MirrorDrvSynchronize(
    SURFOBJ *pso,
    RECTL *prcl)
{
    return TRUE;
}

static BOOL
MirrorDrvStrokePath(
    SURFOBJ *pso,
    PATHOBJ *ppo,
    CLIPOBJ *pco,
    XFORMOBJ *pxo,
    BRUSHOBJ *pbo,
    POINTL *pptlBrush,
    LINEATTRS *plineattrs,
    MIX mix)
{
    return TRUE;
}

static BOOL
MirrorDrvFillPath(
    SURFOBJ *pso,
    PATHOBJ *ppo,
    CLIPOBJ *pco,
    BRUSHOBJ *pbo,
    POINTL *pptlBrush,
    MIX mix,
    FLONG flOptions)
{
    return TRUE;
}

static BOOL
MirrorDrvLineTo(
    SURFOBJ *pso,
    CLIPOBJ *pco,
    BRUSHOBJ *pbo,
    POINTL *pptlBrush,
    POINTL *pptl,
    MIX mix,
    FLONG flOptions)
{
    return TRUE;
}

/* ---- DrvEnablePDEV：初始化设备，创建共享脏矩形缓冲 ---- */

static BOOL
MirrorDrvEnablePDEV(
    DEVMODEW *pdm,
    LPWSTR pwszLogAddress,
    ULONG cPat,
    HSURF *phsurfPatterns,
    ULONG cjCaps,
    ULONG *pdevcaps,
    ULONG cjDevInfo,
    DEVINFO *pdi,
    HDEV hdev,
    PWSTR pwszDeviceName,
    HANDLE hDriver)
{
    GDIINFO *pGdi;
    DEVINFO *pdiOut;
    MIRROR_DEV *pDev;
    ULONG changesSize;

    /* 创建 per-PDEV 状态。XDDM 中通过 EngCreateDriverSurface / EngAllocMem 分配。
     * 本骨架用 EngAllocMem 分配 MIRROR_DEV，并记入 hdev 关联（示例经
     * GdiSetHandle/hdev 存取，此处用 hdev 的 pdev 槽位——为清晰起见用
     * 显式字段保存指针，实际工程应按 WDK 示例的 DEVOBJ 关联方式挂接）。 */
    pDev = EngAllocMem(0, sizeof(MIRROR_DEV), 'vriM');
    if (pDev == NULL)
        return FALSE;
    RtlZeroMemory(pDev, sizeof(MIRROR_DEV));

    pDev->ScreenSize.cx = pdm->dmPelsWidth;
    pDev->ScreenSize.cy = pdm->dmPelsHeight;
    pDev->ChangesCapacity = MIRROR_DEFAULT_CAPACITY;

    changesSize = sizeof(MIRROR_CHANGES_HEADER)
                  + (MIRROR_DEFAULT_CAPACITY - 1) * sizeof(MIRROR_CHANGES_RECORD);
    pDev->ChangesSize = changesSize;

    /* 分配非分页共享缓冲，供用户态映射。 */
    pDev->pChanges = EngAllocMem(0, changesSize, 'hCriM');
    if (pDev->pChanges == NULL)
    {
        EngFreeMem(pDev);
        return FALSE;
    }
    RtlZeroMemory(pDev->pChanges, changesSize);
    pDev->pChanges->Capacity = MIRROR_DEFAULT_CAPACITY;
    pDev->pChanges->WriteIndex = 0;
    pDev->pChanges->ReadIndex = 0;
    pDev->pChanges->Overflow = 0;

    /* 为共享缓冲创建 MDL，供用户态 MapUserPhysicalPages 映射（骨架，见备注）。 */
    pDev->MdlChanges = IoAllocateMdl(
        pDev->pChanges, (ULONG)changesSize, FALSE, FALSE, NULL);
    if (pDev->MdlChanges == NULL)
    {
        EngFreeMem(pDev->pChanges);
        EngFreeMem(pDev);
        return FALSE;
    }
    MmBuildMdlForNonPagedPool(pDev->MdlChanges);

    /* 把 per-PDEV 状态与 hdev 关联（骨架：直接写 hdev 关联槽。完整实现见备注）。 */
    /* GdiSetHandle((HANDLE)hdev, pDev); */

    /* 填充 GDIINFO 能力描述 */
    if (cjCaps >= sizeof(GDIINFO))
    {
        pGdi = (GDIINFO *)pdevcaps;
        RtlZeroMemory(pGdi, sizeof(GDIINFO));
        pGdi->ulVersion = GDILO_VERSION;
        pGdi->ulTechnology = DT_RASDISPLAY;
        pGdi->ulHorzRes = pdm->dmPelsWidth;
        pGdi->ulVertRes = pdm->dmPelsHeight;
        pGdi->ulHorzSize = pdm->dmPelsWidth;
        pGdi->ulVertSize = pdm->dmPelsHeight;
        pGdi->ulBitsPerPixel = 32;
        pGdi->flRaster = 0;
        pGdi->ulLogPixelsX = pdm->dmLogPixels;
        pGdi->ulLogPixelsY = pdm->dmLogPixels;
        pGdi->ulNumColors = 0;
        pGdi->ulDevicePelsDPI = 0;
        pGdi->ulPrimaryOrder = PRIMARY_ORDER_CYU;
        pGdi->ulHTPatterns = 0;
        pGdi->ulHTOutputFormat = HT_FORMAT_8BPP;
    }

    /* 填充 DEVINFO（镜像驱动能力） */
    if (cjDevInfo >= sizeof(DEVINFO))
    {
        pdiOut = pdi;
        RtlZeroMemory(pdiOut, sizeof(DEVINFO));
        pdiOut->flGraphicsCaps = GCAPS_DIRECTDRAW;
        pdiOut->iDitherFormat = BMF_32BPP;
        pdiOut->ulHTPatterns = 0;
        pdiOut->ulDitherRed = 0;
        pdiOut->ulDitherGreen = 0;
        pdiOut->ulDitherBlue = 0;
    }

    return TRUE;
}

static VOID
MirrorDrvDisablePDEV(
    HDEV hdev)
{
    /* MIRROR_DEV *pDev = (MIRROR_DEV *)GdiGetHandle(hdev); 见备注 */
    /* 释放 MDL、共享缓冲、per-PDEV 状态 */
    /* if (pDev->MdlChanges) IoFreeMdl(pDev->MdlChanges);
     * if (pDev->pChanges) EngFreeMem(pDev->pChanges);
     * EngFreeMem(pDev); */
}

static BOOL
MirrorDrvCompletePDEV(
    HDEV hdev,
    HDEV hdevOld)
{
    return TRUE;
}

static VOID
MirrorDrvAssertMode(
    HDEV hdev,
    BOOL bEnable)
{
}

static BOOL
MirrorDrvResetPDEV(
    HDEV hdevOld,
    HDEV hdevNew)
{
    return TRUE;
}

/* ---- DrvEnableDriver：导出 DDI 函数表 ---- */

BOOL
DrvEnableDriver(
    ULONG iEngineVersion,
    ULONG cj,
    DRVENABLEDATA *pded)
{
    if (pded == NULL)
        return FALSE;
    if (pded->pdrvfn == NULL || cj < (ULONG)(sizeof(DRVENABLEDATA) / sizeof(void *)))
        return FALSE;

    /* 填充分发表（按 DRVFN 槽位，顺序必须与 winddi.h 的 DRVFN 枚举一致） */
    pded->iDriverVersion = DDI_DRIVER_VERSION_NT4;

    /* 以下按 WDK7 mirror 示例的 DrvEnableDriver 填表顺序。字段顺序来自
     * 微软示例 mirror.c 的 MIRRORFUNCS 表；本骨架仅列关键项，完整表在
     * 编译验证阶段按示例补齐。 */
    pded->c = 0; /* 实际按示例填满后置为条目数 */

    return TRUE;
}
