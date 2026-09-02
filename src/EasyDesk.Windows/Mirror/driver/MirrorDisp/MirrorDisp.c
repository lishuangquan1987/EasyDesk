/******************************Module*Header*******************************\
*
* Module Name: MirrorDisp.c
*
* EasyRDP XDDM mirror display driver.
* Based on the Microsoft WDK7 mirror driver sample
*   \src\video\displays\mirror\disp\
*
* The GDI desktop draw operations intersecting the mirror surface are
* dispatched to this driver's Drv* callbacks; we both (a) forward them to
* the engine (EngBitBlt/EngTextOut/...) so the pixels are really rendered
* into the mapped mirror surface file, and (b) record the affected
* rectangles into a shared ring buffer so the user-mode client
* (MirrorScreenCapturer) only processes changed regions.
*
* Platform: XDDM display driver, XP/Win7 only (< Win8).
*
\**************************************************************************/

#include "driver.h"

static DRVFN gadrvfn[] =
{
    { INDEX_DrvEnablePDEV,            (PFN) DrvEnablePDEV         },
    { INDEX_DrvCompletePDEV,          (PFN) DrvCompletePDEV       },
    { INDEX_DrvDisablePDEV,           (PFN) DrvDisablePDEV        },
    { INDEX_DrvEnableSurface,         (PFN) DrvEnableSurface      },
    { INDEX_DrvDisableSurface,        (PFN) DrvDisableSurface     },
    { INDEX_DrvAssertMode,            (PFN) DrvAssertMode         },
    { INDEX_DrvNotify,                (PFN) DrvNotify             },
    { INDEX_DrvTextOut,               (PFN) DrvTextOut            },
    { INDEX_DrvBitBlt,                (PFN) DrvBitBlt             },
    { INDEX_DrvCopyBits,              (PFN) DrvCopyBits           },
    { INDEX_DrvStrokePath,            (PFN) DrvStrokePath         },
    { INDEX_DrvLineTo,                (PFN) DrvLineTo             },
    { INDEX_DrvFillPath,              (PFN) DrvFillPath           },
    { INDEX_DrvMovePointer,           (PFN) DrvMovePointer        },
    { INDEX_DrvSetPointerShape,       (PFN) DrvSetPointerShape    },
    { INDEX_DrvEscape,                (PFN) DrvEscape             }
};

/* ---- dirty-rectangle recording ---- */

VOID
MirrorLogChange(
    PPDEV pdev,
    CONST RECTL *prcl)
{
    ULONG next;
    RECTL clipped;
    MIRROR_CHANGES_HEADER *pHead;

    if (pdev == NULL || pdev->pChanges == NULL || prcl == NULL)
        return;

    pHead = pdev->pChanges;

    clipped = *prcl;
    if (clipped.left < 0) clipped.left = 0;
    if (clipped.top < 0) clipped.top = 0;
    if (clipped.right > (LONG)pdev->cxScreen) clipped.right = pdev->cxScreen;
    if (clipped.bottom > (LONG)pdev->cyScreen) clipped.bottom = pdev->cyScreen;
    if (clipped.right <= clipped.left || clipped.bottom <= clipped.top)
        return;

    next = (pHead->WriteIndex + 1) % pHead->Capacity;
    if (next == pHead->ReadIndex)
    {
        pHead->Overflow = 1;
        return;
    }

    if (pHead->Overflow)
        pHead->Overflow = 0;

    pHead->Records[pHead->WriteIndex].Type = 0;
    pHead->Records[pHead->WriteIndex].Rect = clipped;
    pHead->WriteIndex = next;
}

/* ---- Driver enable/disable ---- */

BOOL DrvEnableDriver(
ULONG iEngineVersion,
ULONG cj,
PDRVENABLEDATA pded)
{
    iEngineVersion;
    if (cj >= sizeof(DRVENABLEDATA))
        pded->pdrvfn = gadrvfn;
    if (cj >= (sizeof(ULONG) * 2))
        pded->c = sizeof(gadrvfn) / sizeof(DRVFN);
    if (cj >= sizeof(ULONG))
        pded->iDriverVersion = DDI_DRIVER_VERSION_NT4;
    return TRUE;
}

DHPDEV
DrvEnablePDEV(
    DEVMODEW   *pDevmode,
    PWSTR       pwszLogAddress,
    ULONG       cPatterns,
    HSURF      *ahsurfPatterns,
    ULONG       cjGdiInfo,
    ULONG      *pGdiInfo,
    ULONG       cjDevInfo,
    DEVINFO    *pDevInfo,
    HDEV        hdev,
    PWSTR       pwszDeviceName,
    HANDLE      hDriver)
{
    GDIINFO GdiInfo;
    DEVINFO DevInfo;
    PPDEV   ppdev;
    ULONG   changesSize;

    UNREFERENCED_PARAMETER(pwszLogAddress);
    UNREFERENCED_PARAMETER(cPatterns);
    UNREFERENCED_PARAMETER(ahsurfPatterns);
    UNREFERENCED_PARAMETER(hdev);
    UNREFERENCED_PARAMETER(pwszDeviceName);

    ppdev = (PPDEV) EngAllocMem(FL_ZERO_MEMORY, sizeof(PDEV), ALLOC_TAG);
    if (ppdev == NULL)
        return (DHPDEV)0;

    ppdev->hDriver = hDriver;

    if (!bInitPDEV(ppdev, pDevmode, &GdiInfo, &DevInfo))
    {
        EngFreeMem(ppdev);
        return (DHPDEV)0;
    }

    /* Allocate the shared dirty-rect ring buffer. */
    ppdev->ChangesCapacity = MIRROR_DEFAULT_CAPACITY;
    changesSize = sizeof(MIRROR_CHANGES_HEADER)
                  + (MIRROR_DEFAULT_CAPACITY - 1) * sizeof(MIRROR_CHANGES_RECORD);
    ppdev->ChangesSize = changesSize;
    ppdev->pChanges = (MIRROR_CHANGES_HEADER *)EngAllocMem(
        FL_ZERO_MEMORY, changesSize, ALLOC_TAG);
    if (ppdev->pChanges == NULL)
    {
        EngFreeMem(ppdev);
        return (DHPDEV)0;
    }
    ppdev->pChanges->Capacity = MIRROR_DEFAULT_CAPACITY;

    if (sizeof(DEVINFO) > cjDevInfo)
    {
        EngFreeMem(ppdev->pChanges);
        EngFreeMem(ppdev);
        return (DHPDEV)0;
    }
    RtlCopyMemory(pDevInfo, &DevInfo, sizeof(DEVINFO));

    if (sizeof(GDIINFO) > cjGdiInfo)
    {
        EngFreeMem(ppdev->pChanges);
        EngFreeMem(ppdev);
        return (DHPDEV)0;
    }
    RtlCopyMemory(pGdiInfo, &GdiInfo, sizeof(GDIINFO));

    return (DHPDEV)ppdev;
}

VOID DrvCompletePDEV(
DHPDEV dhpdev,
HDEV  hdev)
{
    ((PPDEV)dhpdev)->hdevEng = hdev;
}

VOID DrvDisablePDEV(
DHPDEV dhpdev)
{
    PPDEV ppdev = (PPDEV)dhpdev;

    if (ppdev->hpalDefault)
        EngDeletePalette(ppdev->hpalDefault);
    if (ppdev->pChanges)
        EngFreeMem(ppdev->pChanges);
    if (ppdev->pMappedFile)
        EngUnmapFile(ppdev->pMappedFile);
    if (ppdev->pvTmpBuffer)
        EngDeleteFile(MIRROR_SURFACE_FILE);
    EngFreeMem(dhpdev);
}

HSURF DrvEnableSurface(
DHPDEV dhpdev)
{
    PPDEV ppdev = (PPDEV)dhpdev;
    HSURF hsurf;
    SIZEL sizl;
    ULONG ulBitmapType;
    FLONG flHooks;
    ULONG mirrorsize;

    ppdev->ptlOrg.x = 0;
    ppdev->ptlOrg.y = 0;

    sizl.cx = ppdev->cxScreen;
    sizl.cy = ppdev->cyScreen;

    ulBitmapType = BMF_32BPP;
    flHooks = HOOKS_BMF32BPP;
    flHooks |= flGlobalHooks;

    mirrorsize = (ULONG)(ppdev->cxScreen * ppdev->cyScreen * 4);
    ppdev->lDeltaScreen = ppdev->cxScreen * 4;

    ppdev->pvTmpBuffer = EngMapFile(MIRROR_SURFACE_FILE,
                    mirrorsize,
                    &ppdev->pMappedFile);
    if (ppdev->pvTmpBuffer == NULL)
        return FALSE;

    hsurf = (HSURF) EngCreateBitmap(sizl,
                                        ppdev->lDeltaScreen,
                                        ulBitmapType,
                                        0,
                                        (PVOID)(ppdev->pvTmpBuffer));
    if (hsurf == (HSURF)0)
    {
        return FALSE;
    }

    if (!EngAssociateSurface(hsurf, ppdev->hdevEng, flHooks))
    {
        EngDeleteSurface(hsurf);
        return FALSE;
    }

    ppdev->hsurfEng = hsurf;
    return hsurf;
}

VOID DrvDisableSurface(
DHPDEV dhpdev)
{
    PPDEV ppdev = (PPDEV)dhpdev;
    if (ppdev->hsurfEng)
        EngDeleteSurface(ppdev->hsurfEng);
    ppdev->hsurfEng = NULL;
}

BOOL DrvAssertMode(
DHPDEV dhpdev,
BOOL bEnable)
{
    UNREFERENCED_PARAMETER(dhpdev);
    UNREFERENCED_PARAMETER(bEnable);
    return TRUE;
}

VOID DrvNotify(
SURFOBJ *pso,
ULONG iType,
PVOID pvData)
{
    UNREFERENCED_PARAMETER(pso);
    UNREFERENCED_PARAMETER(iType);
    UNREFERENCED_PARAMETER(pvData);
}

/* ---- draw callbacks: forward to engine AND record dirty rects ---- */

BOOL DrvCopyBits(
   SURFOBJ *psoDst,
   SURFOBJ *psoSrc,
   CLIPOBJ *pco,
   XLATEOBJ *pxlo,
   RECTL *prclDst,
   POINTL *pptlSrc)
{
    if (prclDst != NULL)
        MirrorLogChange((PPDEV)psoDst->dhpdev, prclDst);
    return EngCopyBits(psoDst, psoSrc, pco, pxlo, prclDst, pptlSrc);
}

BOOL DrvBitBlt(
   SURFOBJ *psoDst,
   SURFOBJ *psoSrc,
   SURFOBJ *psoMask,
   CLIPOBJ *pco,
   XLATEOBJ *pxlo,
   RECTL *prclDst,
   POINTL *pptlSrc,
   POINTL *pptlMask,
   BRUSHOBJ *pbo,
   POINTL *pptlBrush,
   ROP4 rop4)
{
    if (prclDst != NULL)
        MirrorLogChange((PPDEV)psoDst->dhpdev, prclDst);
    return EngBitBlt(psoDst, psoSrc, psoMask, pco, pxlo,
                     prclDst, pptlSrc, pptlMask, pbo, pptlBrush, rop4);
}

BOOL DrvTextOut(
   SURFOBJ *psoDst,
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
        MirrorLogChange((PPDEV)psoDst->dhpdev, prclOpaque);
    return EngTextOut(psoDst, pstro, pfo, pco, prclExtra,
                      prclOpaque, pboFore, pboOpaque, pptlOrg, mix);
}

BOOL DrvStrokePath(
SURFOBJ*   pso,
PATHOBJ*   ppo,
CLIPOBJ*   pco,
XFORMOBJ*  pxo,
BRUSHOBJ*  pbo,
POINTL*    pptlBrush,
LINEATTRS* pLineAttrs,
MIX        mix)
{
    return EngStrokePath(pso, ppo, pco, pxo, pbo, pptlBrush, pLineAttrs, mix);
}

BOOL DrvLineTo(
SURFOBJ   *pso,
CLIPOBJ   *pco,
BRUSHOBJ  *pbo,
LONG       x1,
LONG       y1,
LONG       x2,
LONG       y2,
RECTL     *prclBounds,
MIX        mix)
{
    if (prclBounds != NULL)
        MirrorLogChange((PPDEV)pso->dhpdev, prclBounds);
    return EngLineTo(pso, pco, pbo, x1, y1, x2, y2, prclBounds, mix);
}

BOOL DrvFillPath(
SURFOBJ  *pso,
PATHOBJ  *ppo,
CLIPOBJ  *pco,
BRUSHOBJ *pbo,
PPOINTL   pptlBrushOrg,
MIX       mix,
FLONG     flOptions)
{
    return EngFillPath(pso, ppo, pco, pbo, pptlBrushOrg, mix, flOptions);
}

ULONG DrvEscape(
SURFOBJ *pso,
ULONG iEsc,
ULONG cjIn,
PVOID pvIn,
ULONG cjOut,
PVOID pvOut)
{
    UNREFERENCED_PARAMETER(cjIn);
    UNREFERENCED_PARAMETER(pvIn);
    UNREFERENCED_PARAMETER(cjOut);
    UNREFERENCED_PARAMETER(pvOut);
    UNREFERENCED_PARAMETER(pso);
    UNREFERENCED_PARAMETER(iEsc);
    return 0;
}

void DrvMovePointer(SURFOBJ *pso, LONG x, LONG y, RECTL *prcl)
{
    UNREFERENCED_PARAMETER(pso);
    UNREFERENCED_PARAMETER(x);
    UNREFERENCED_PARAMETER(y);
    UNREFERENCED_PARAMETER(prcl);
}

ULONG DrvSetPointerShape(SURFOBJ *pso, SURFOBJ *psoMask, SURFOBJ *psoColor,
                         XLATEOBJ *pxlo, LONG xHot, LONG yHot,
                         LONG x, LONG y, RECTL *prcl, FLONG fl)
{
    UNREFERENCED_PARAMETER(pso);
    UNREFERENCED_PARAMETER(psoMask);
    UNREFERENCED_PARAMETER(psoColor);
    UNREFERENCED_PARAMETER(pxlo);
    UNREFERENCED_PARAMETER(xHot);
    UNREFERENCED_PARAMETER(yHot);
    UNREFERENCED_PARAMETER(x);
    UNREFERENCED_PARAMETER(y);
    UNREFERENCED_PARAMETER(prcl);
    UNREFERENCED_PARAMETER(fl);
    return SPS_ACCEPT_NOEXCLUDE;
}
