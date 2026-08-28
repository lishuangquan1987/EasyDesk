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

typedef struct _PDEV
{
    HANDLE  hDriver;                    // Handle to \Device\Screen
    HDEV    hdevEng;                    // Engine's handle to PDEV
    HSURF   hsurfEng;                   // Engine's handle to surface

    ULONG   cxScreen;                   // Visible screen width
    ULONG   cyScreen;                   // Visible screen height

    MIRROR_CHANGES_HEADER *pChanges;    // shared dirty-rect buffer
    ULONG   ChangesSize;                // shared buffer size in bytes
    ULONG   ChangesCapacity;            // record capacity

} PDEV, *PPDEV;

/* ---- prototypes ---- */

BOOL bInitPDEV(PPDEV, PDEVMODEW, GDIINFO *, DEVINFO *);
VOID vDisablePDEV(PPDEV);
VOID MirrorLogChange(PPDEV, CONST RECTL *);

#define DLL_NAME                L"mirror"     // Name of the DLL in UNICODE
#define STANDARD_DEBUG_PREFIX   "MIRROR: "    // All debug output is prefixed
#define ALLOC_TAG               'oDDM'        // Four byte tag for memory allocation
