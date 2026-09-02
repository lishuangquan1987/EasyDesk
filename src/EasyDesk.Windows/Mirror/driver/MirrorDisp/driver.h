/******************************Module*Header*******************************\
*
*                           * GDI SAMPLE CODE *
*                           *******************
*
* Module Name: driver.h
*
* contains prototypes for the EasyRDP mirror display driver.
*
* Copyright (c) 1992-1998 Microsoft Corporation
* (based on WDK7 mirror driver sample; adapted for EasyRDP)
\**************************************************************************/

#define DBG 1

#include "stddef.h"

#include <stdarg.h>

#pragma warning(push)
#pragma warning(disable: 4200 4201 4214)

#include "windef.h"
#include "wingdi.h"
#include "winddi.h"
#include "devioctl.h"
#include "ntddvdeo.h"

#pragma warning(pop)    // C4200/C4201/C4214 warnings suppressed
                        // (zero-size array, nameless struct, bitfield types)

/* ---- EasyRDP mirror driver PDEV state ---- */

/* Dirty-rect record shared with the user-mode client */
typedef struct _MIRROR_CHANGES_RECORD
{
    ULONG Type;      /* 0 = region, 1 = full screen */
    RECTL Rect;      /* dirty rectangle (left/top/right/bottom) */
} MIRROR_CHANGES_RECORD;

/* Ring-buffer header shared with the user-mode client */
typedef struct _MIRROR_CHANGES_HEADER
{
    volatile ULONG WriteIndex;   /* driver write position (ring) */
    volatile ULONG ReadIndex;    /* client consumed position (ring) */
    volatile ULONG Overflow;     /* nonzero = overflow, client must full-screen */
    ULONG Capacity;              /* record capacity */
    MIRROR_CHANGES_RECORD Records[1]; /* record array, sized by Capacity */
} MIRROR_CHANGES_HEADER;

#define MIRROR_DEFAULT_CAPACITY 4096

/*
 * Mirror surface pixel buffer: the driver renders the desktop into this
 * mapped file, and the user-mode client reads it (like the WDK sample's
 * c:\video.dat). EasyRDP uses \??\c:\easyrdp-mirror.bin.
 */
#define MIRROR_SURFACE_FILE  L"\\??\\c:\\easyrdp-mirror.bin"

typedef struct  _PDEV
{
    HANDLE  hDriver;                    // Handle to \Device\Screen
    HDEV    hdevEng;                    // Engine's handle to PDEV
    HSURF   hsurfEng;                   // Engine's handle to surface
    HPALETTE hpalDefault;               // Handle to the default palette
    PBYTE   pjScreen;                   // pointer to base screen address
    ULONG   cxScreen;                   // Visible screen width
    ULONG   cyScreen;                   // Visible screen height
    POINTL  ptlOrg;                     // Where this display is anchored
    ULONG   ulMode;                     // Mode the mini-port driver is in
    LONG    lDeltaScreen;               // Distance from one scan to the next
    ULONG   cScreenSize;                // size of video memory
    FLONG   flRed;                      // Red mask
    FLONG   flGreen;                    // Green mask
    FLONG   flBlue;                     // Blue mask
    ULONG   ulBitCount;                 // bits per pel (32)

    PVOID   pvTmpBuffer;                // ptr to MIRRSURF bits for screen surface
    ULONG_PTR pMappedFile;              // handle of the mapped surface file

    MIRROR_CHANGES_HEADER *pChanges;    // shared dirty-rect buffer
    ULONG   ChangesSize;                // shared buffer size in bytes
    ULONG   ChangesCapacity;            // record capacity

} PDEV, *PPDEV;

/* ---- prototypes ---- */

BOOL bInitPDEV(PPDEV, PDEVMODEW, GDIINFO *, DEVINFO *);
VOID MirrorLogChange(PPDEV, CONST RECTL *);

#define DLL_NAME                L"mirror"     // Name of the DLL in UNICODE
#define STANDARD_DEBUG_PREFIX   "MIRROR: "    // All debug output is prefixed
#define ALLOC_TAG               'oDDM'        // Four byte tag for memory allocation

/* Always hook these to be called for our surfaces (from the WDK sample). */
#define flGlobalHooks   HOOK_FILLPATH | HOOK_STROKEPATH | HOOK_LINETO | HOOK_TEXTOUT | HOOK_BITBLT | HOOK_COPYBITS
#define HOOKS_BMF32BPP  0
