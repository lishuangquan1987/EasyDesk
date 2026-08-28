/*
 * MirrorDisp.c - EasyRDP XDDM mirror display driver
 *
 * Based on the Microsoft WDK7 mirror driver sample
 *   \src\video\displays\mirror\disp\
 * Adapted for EasyRDP dirty-rectangle capture on XP/Win7.
 *
 * GDI desktop draw operations intersecting the mirror surface are dispatched
 * to this driver's Drv* callbacks; we record the affected rectangles into a
 * shared ring buffer so the user-mode client (MirrorScreenCapturer) only
 * processes changed regions.
 *
 * All DDI callback signatures must match winddi.h exactly (they are declared
 * there). Platform: XDDM display driver, XP/Win7 only (< Win8).
 */

#include "driver.h"

/* ---- DDI callbacks (signatures from winddi.h) ---- */

DHPDEV APIENTRY
DrvEnablePDEV(
    DEVMODEW *pdm,
    LPWSTR pwszLogAddress,
    ULONG cPat,
    HSURF *phsurfPatterns,
    ULONG cjCaps,
    ULONG *pdevcaps,
    ULONG cjDevInfo,
    DEVINFO *pdi,
    HDEV hdev,
    LPWSTR pwszDeviceName,
    HANDLE hDriver)
{
    PPDEV pdev;
    ULONG changesSize;
    GDIINFO *pGdi;
    PDEVINFO pdiOut;

    pdev = (PPDEV)EngAllocMem(FL_ZERO_MEMORY, sizeof(PDEV), ALLOC_TAG);
    if (pdev == NULL)
        return NULL;

    pdev->cxScreen = pdm->dmPelsWidth;
    pdev->cyScreen = pdm->dmPelsHeight;
    pdev->ChangesCapacity = MIRROR_DEFAULT_CAPACITY;

    changesSize = sizeof(MIRROR_CHANGES_HEADER)
                  + (MIRROR_DEFAULT_CAPACITY - 1) * sizeof(MIRROR_CHANGES_RECORD);
    pdev->ChangesSize = changesSize;

    pdev->pChanges = (MIRROR_CHANGES_HEADER *)EngAllocMem(
        FL_ZERO_MEMORY, changesSize, ALLOC_TAG);
    if (pdev->pChanges == NULL)
    {
        EngFreeMem(pdev);
        return NULL;
    }
    pdev->pChanges->Capacity = MIRROR_DEFAULT_CAPACITY;

    /* Fill GDIINFO */
    if (cjCaps >= sizeof(GDIINFO))
    {
        pGdi = (GDIINFO *)pdevcaps;
        RtlZeroMemory(pGdi, sizeof(GDIINFO));
        pGdi->ulVersion = 0;
        pGdi->ulHorzRes = pdm->dmPelsWidth;
        pGdi->ulVertRes = pdm->dmPelsHeight;
        pGdi->ulHorzSize = pdm->dmPelsWidth;
        pGdi->ulVertSize = pdm->dmPelsHeight;
        pGdi->ulLogPixelsX = pdm->dmLogPixels;
        pGdi->ulLogPixelsY = pdm->dmLogPixels;
    }

    /* Fill DEVINFO */
    if (cjDevInfo >= sizeof(DEVINFO))
    {
        pdiOut = (PDEVINFO)pdi;
        RtlZeroMemory(pdiOut, sizeof(DEVINFO));
        pdiOut->flGraphicsCaps = GCAPS_DIRECTDRAW;
        pdiOut->iDitherFormat = BMF_32BPP;
    }

    return (DHPDEV)pdev;
}

VOID APIENTRY
DrvCompletePDEV(
    DHPDEV dhpdev,
    HDEV hdev)
{
}

VOID APIENTRY
DrvDisablePDEV(
    DHPDEV dhpdev)
{
    PPDEV pdev = (PPDEV)dhpdev;
    if (pdev != NULL)
    {
        if (pdev->pChanges != NULL)
            EngFreeMem(pdev->pChanges);
        EngFreeMem(pdev);
    }
}

HSURF APIENTRY
DrvEnableSurface(
    DHPDEV dhpdev)
{
    SIZEL sizl;
    HSURF hsurf;

    sizl.cx = 1;
    sizl.cy = 1;
    hsurf = EngCreateBitmap(sizl, 4, BMF_32BPP, BMF_TOPDOWN, NULL);
    if (hsurf == NULL)
        return NULL;
    EngAssociateSurface(hsurf, ((PPDEV)dhpdev)->hdevEng, 0);
    return hsurf;
}

VOID APIENTRY
DrvDisableSurface(
    DHPDEV dhpdev)
{
}

BOOL APIENTRY
DrvAssertMode(
    DHPDEV dhpdev,
    BOOL bEnable)
{
    return TRUE;
}

BOOL APIENTRY
DrvResetPDEV(
    DHPDEV dhpdevOld,
    DHPDEV dhpdevNew)
{
    return TRUE;
}

VOID APIENTRY
DrvNotify(
    SURFOBJ *pso,
    ULONG iType,
    PVOID pvData)
{
}

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

/* ---- Drv* draw callbacks ---- */

BOOL APIENTRY
DrvCopyBits(
    SURFOBJ *psoDest,
    SURFOBJ *psoSrc,
    CLIPOBJ *pco,
    XLATEOBJ *pxlo,
    RECTL *prclDest,
    POINTL *pptlSrc)
{
    if (prclDest != NULL)
        MirrorLogChange((PPDEV)psoDest->dhpdev, prclDest);
    return TRUE;
}

BOOL APIENTRY
DrvBitBlt(
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
    ROP4 rop4)
{
    if (prclTrg != NULL)
        MirrorLogChange((PPDEV)psoTrg->dhpdev, prclTrg);
    return TRUE;
}

BOOL APIENTRY
DrvTextOut(
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
        MirrorLogChange((PPDEV)pso->dhpdev, prclOpaque);
    return TRUE;
}

BOOL APIENTRY
DrvStretchBlt(
    SURFOBJ *psoDest,
    SURFOBJ *psoSrc,
    SURFOBJ *psoMask,
    CLIPOBJ *pco,
    XLATEOBJ *pxlo,
    COLORADJUSTMENT *pca,
    POINTL *pptlHTOrg,
    RECTL *prclDest,
    RECTL *prclSrc,
    POINTL *pptlMask,
    ULONG iMode)
{
    if (prclDest != NULL)
        MirrorLogChange((PPDEV)psoDest->dhpdev, prclDest);
    return TRUE;
}

/* ---- remaining DDI stubs (mirror drivers don't render) ---- */

BOOL APIENTRY
DrvStrokePath(
    SURFOBJ *pso,
    PATHOBJ *ppo,
    CLIPOBJ *pco,
    XFORMOBJ *pxo,
    BRUSHOBJ *pbo,
    POINTL *pptlBrushOrg,
    LINEATTRS *plineattrs,
    MIX mix)
{
    return TRUE;
}

BOOL APIENTRY
DrvFillPath(
    SURFOBJ *pso,
    PATHOBJ *ppo,
    CLIPOBJ *pco,
    BRUSHOBJ *pbo,
    POINTL *pptlBrushOrg,
    MIX mix,
    FLONG flOptions)
{
    return TRUE;
}

BOOL APIENTRY
DrvLineTo(
    SURFOBJ *pso,
    CLIPOBJ *pco,
    BRUSHOBJ *pbo,
    LONG x1,
    LONG y1,
    LONG x2,
    LONG y2,
    RECTL *prclBounds,
    MIX mix)
{
    return TRUE;
}

ULONG APIENTRY
DrvDitherColor(
    DHPDEV dhpdev,
    ULONG iMode,
    ULONG rgb,
    ULONG *pul)
{
    return 0;
}

BOOL APIENTRY
DrvRealizeBrush(
    BRUSHOBJ *pbo,
    SURFOBJ *psoTarget,
    SURFOBJ *psoPattern,
    SURFOBJ *psoMask,
    XLATEOBJ *pxlo,
    ULONG iHatch)
{
    return TRUE;
}

ULONG APIENTRY
DrvSetPointerShape(
    SURFOBJ *pso,
    SURFOBJ *psoMask,
    SURFOBJ *psoColor,
    XLATEOBJ *pxlo,
    LONG xHot,
    LONG yHot,
    LONG x,
    LONG y,
    RECTL *prcl,
    FLONG fl)
{
    return SPS_DECLINE;
}

VOID APIENTRY
DrvMovePointer(
    SURFOBJ *pso,
    LONG x,
    LONG y,
    RECTL *prcl)
{
}

VOID APIENTRY
DrvSynchronize(
    DHPDEV dhpdev,
    RECTL *prcl)
{
}

BOOL APIENTRY
DrvSetPalette(
    DHPDEV dhpdev,
    PALOBJ *ppalo,
    FLONG fl,
    ULONG iStart,
    ULONG cColors)
{
    return TRUE;
}

BOOL APIENTRY
DrvSetPixelFormat(
    SURFOBJ *pso,
    LONG iPixelFormat,
    HWND hwnd)
{
    return TRUE;
}

HBITMAP APIENTRY
DrvCreateDeviceBitmap(
    DHPDEV dhpdev,
    SIZEL sizl,
    ULONG iFormat)
{
    return NULL;
}

VOID APIENTRY
DrvDeleteDeviceBitmap(
    DHSURF dhsurf)
{
}

/* ---- DDI function table + DrvEnableDriver ---- */

BOOL APIENTRY
DrvEnableDriver(
    ULONG iEngineVersion,
    ULONG cj,
    DRVENABLEDATA *pded)
{
    static DRVFN gadrvfn[] =
    {
        { INDEX_DrvEnablePDEV,        (PFN)DrvEnablePDEV        },
        { INDEX_DrvCompletePDEV,      (PFN)DrvCompletePDEV      },
        { INDEX_DrvDisablePDEV,       (PFN)DrvDisablePDEV       },
        { INDEX_DrvEnableSurface,     (PFN)DrvEnableSurface     },
        { INDEX_DrvDisableSurface,    (PFN)DrvDisableSurface    },
        { INDEX_DrvAssertMode,        (PFN)DrvAssertMode        },
        { INDEX_DrvTextOut,           (PFN)DrvTextOut           },
        { INDEX_DrvBitBlt,            (PFN)DrvBitBlt            },
        { INDEX_DrvCopyBits,          (PFN)DrvCopyBits          },
        { INDEX_DrvStretchBlt,        (PFN)DrvStretchBlt        },
        { INDEX_DrvStrokePath,        (PFN)DrvStrokePath        },
        { INDEX_DrvFillPath,          (PFN)DrvFillPath          },
        { INDEX_DrvLineTo,            (PFN)DrvLineTo            },
        { INDEX_DrvDitherColor,       (PFN)DrvDitherColor       },
        { INDEX_DrvRealizeBrush,      (PFN)DrvRealizeBrush      },
        { INDEX_DrvSetPalette,        (PFN)DrvSetPalette        },
        { INDEX_DrvSetPixelFormat,    (PFN)DrvSetPixelFormat    },
        { INDEX_DrvCreateDeviceBitmap,(PFN)DrvCreateDeviceBitmap},
        { INDEX_DrvDeleteDeviceBitmap,(PFN)DrvDeleteDeviceBitmap},
        { INDEX_DrvSetPointerShape,   (PFN)DrvSetPointerShape   },
        { INDEX_DrvMovePointer,       (PFN)DrvMovePointer       },
        { INDEX_DrvSynchronize,       (PFN)DrvSynchronize       },
        { INDEX_DrvResetPDEV,         (PFN)DrvResetPDEV         },
        { INDEX_DrvNotify,            (PFN)DrvNotify            },
    };

    if (pded == NULL)
        return FALSE;

    pded->pdrvfn = gadrvfn;
    pded->c = sizeof(gadrvfn) / sizeof(gadrvfn[0]);
    pded->iDriverVersion = DDI_DRIVER_VERSION_NT4;

    return TRUE;
}
