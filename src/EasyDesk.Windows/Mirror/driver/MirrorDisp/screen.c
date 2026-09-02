/******************************Module*Header*******************************\
*
* Module Name: screen.c
*
* Initializes the GDIINFO and DEVINFO structures for DrvEnablePDEV.
* Based on the Microsoft WDK7 mirror driver sample \src\video\displays\mirror\disp\screen.c
*
\**************************************************************************/

#include "driver.h"

#define SYSTM_LOGFONT {16,7,0,0,700,0,0,0,ANSI_CHARSET,OUT_DEFAULT_PRECIS,CLIP_DEFAULT_PRECIS,DEFAULT_QUALITY,VARIABLE_PITCH | FF_DONTCARE,L"System"}
#define HELVE_LOGFONT {12,9,0,0,400,0,0,0,ANSI_CHARSET,OUT_DEFAULT_PRECIS,CLIP_STROKE_PRECIS,PROOF_QUALITY,VARIABLE_PITCH | FF_DONTCARE,L"MS Sans Serif"}
#define COURI_LOGFONT {12,9,0,0,400,0,0,0,ANSI_CHARSET,OUT_DEFAULT_PRECIS,CLIP_STROKE_PRECIS,PROOF_QUALITY,FIXED_PITCH | FF_DONTCARE, L"Courier"}

const DEVINFO gDevInfoFrameBuffer = {
    0,
    SYSTM_LOGFONT,
    HELVE_LOGFONT,
    COURI_LOGFONT,
    0,
    0,
    8,
    8,
    0,
    GCAPS2_SYNCTIMER |
    GCAPS2_SYNCFLUSH
};

BOOL bInitPDEV(
PPDEV ppdev,
DEVMODEW *pDevMode,
GDIINFO *pGdiInfo,
DEVINFO *pDevInfo)
{
    ppdev->ulMode = 0;
    ppdev->cxScreen = pDevMode->dmPelsWidth;
    ppdev->cyScreen = pDevMode->dmPelsHeight;
    ppdev->ulBitCount = pDevMode->dmBitsPerPel;
    ppdev->lDeltaScreen = 0;

    ppdev->flRed = 0x00FF0000;
    ppdev->flGreen = 0x0000FF00;
    ppdev->flBlue = 0x000000FF;

    pGdiInfo->ulVersion    = GDI_DRIVER_VERSION;
    pGdiInfo->ulTechnology = DT_RASDISPLAY;
    pGdiInfo->ulHorzSize   = 0;
    pGdiInfo->ulVertSize   = 0;
    pGdiInfo->ulHorzRes        = ppdev->cxScreen;
    pGdiInfo->ulVertRes        = ppdev->cyScreen;
    pGdiInfo->ulPanningHorzRes = 0;
    pGdiInfo->ulPanningVertRes = 0;
    pGdiInfo->cBitsPixel       = 8;
    pGdiInfo->cPlanes          = 1;
    pGdiInfo->ulVRefresh       = 1;
    pGdiInfo->ulBltAlignment   = 1;
    pGdiInfo->ulLogPixelsX = pDevMode->dmLogPixels;
    pGdiInfo->ulLogPixelsY = pDevMode->dmLogPixels;
    pGdiInfo->flTextCaps = TC_RA_ABLE;
    pGdiInfo->flRaster = 0;
    pGdiInfo->ulDACRed   = 8;
    pGdiInfo->ulDACGreen = 8;
    pGdiInfo->ulDACBlue  = 8;
    pGdiInfo->ulAspectX    = 0x24;
    pGdiInfo->ulAspectY    = 0x24;
    pGdiInfo->ulAspectXY   = 0x33;
    pGdiInfo->xStyleStep   = 1;
    pGdiInfo->yStyleStep   = 1;
    pGdiInfo->denStyleStep = 3;
    pGdiInfo->ptlPhysOffset.x = 0;
    pGdiInfo->ptlPhysOffset.y = 0;
    pGdiInfo->szlPhysSize.cx  = 0;
    pGdiInfo->szlPhysSize.cy  = 0;
    pGdiInfo->ciDevice.Red.x = 6700;
    pGdiInfo->ciDevice.Red.y = 3300;
    pGdiInfo->ciDevice.Red.Y = 0;
    pGdiInfo->ciDevice.Green.x = 2100;
    pGdiInfo->ciDevice.Green.y = 7100;
    pGdiInfo->ciDevice.Green.Y = 0;
    pGdiInfo->ciDevice.Blue.x = 1400;
    pGdiInfo->ciDevice.Blue.y = 800;
    pGdiInfo->ciDevice.Blue.Y = 0;
    pGdiInfo->ciDevice.AlignmentWhite.x = 3127;
    pGdiInfo->ciDevice.AlignmentWhite.y = 3290;
    pGdiInfo->ciDevice.AlignmentWhite.Y = 0;
    pGdiInfo->ciDevice.RedGamma = 20000;
    pGdiInfo->ciDevice.GreenGamma = 20000;
    pGdiInfo->ciDevice.BlueGamma = 20000;
    pGdiInfo->ciDevice.Cyan.x = 1750;
    pGdiInfo->ciDevice.Cyan.y = 3950;
    pGdiInfo->ciDevice.Cyan.Y = 0;
    pGdiInfo->ciDevice.Magenta.x = 4050;
    pGdiInfo->ciDevice.Magenta.y = 2050;
    pGdiInfo->ciDevice.Magenta.Y = 0;
    pGdiInfo->ciDevice.Yellow.x = 4400;
    pGdiInfo->ciDevice.Yellow.y = 5200;
    pGdiInfo->ciDevice.Yellow.Y = 0;
    pGdiInfo->ciDevice.MagentaInCyanDye = 0;
    pGdiInfo->ciDevice.YellowInCyanDye = 0;
    pGdiInfo->ciDevice.CyanInMagentaDye = 0;
    pGdiInfo->ciDevice.YellowInMagentaDye = 0;
    pGdiInfo->ciDevice.CyanInYellowDye = 0;
    pGdiInfo->ciDevice.MagentaInYellowDye = 0;
    pGdiInfo->ulDevicePelsDPI = 0;
    pGdiInfo->ulPrimaryOrder = PRIMARY_ORDER_CBA;
    pGdiInfo->ulHTPatternSize = HT_PATSIZE_4x4_M;
    pGdiInfo->flHTFlags = HT_FLAG_ADDITIVE_PRIMS;

    *pDevInfo = gDevInfoFrameBuffer;

    pGdiInfo->ulNumColors = 20;
    pGdiInfo->ulNumPalReg = 256;
    pGdiInfo->ulHTOutputFormat = HT_FORMAT_32BPP;
    pDevInfo->iDitherFormat = BMF_32BPP;

    pDevInfo->hpalDefault = EngCreatePalette(PAL_BITFIELDS, 0, NULL, 0xFF0000, 0xFF00, 0xFF);

    return TRUE;
}
