#ifndef HIE_MMC_PLUGIN_H
#define HIE_MMC_PLUGIN_H

#include <windows.h>
#include <objbase.h>
#include <shlobj.h>
#include <adsiid.h>
#include <activeds.h>

// {E5A2B3C4-D5E6-F7A8-B9C0-D1E2F3A4B5C6}
DEFINE_GUID(CLSID_HieMmcPlugin, 
0xe5a2b3c4, 0xd5e6, 0xf7a8, 0xb9, 0xc0, 0xd1, 0xe2, 0xf3, 0xa4, 0xb5, 0xc6);

#define HIE_QUOTA_SEND_PROP    L"HIEprohibitsendquota"
#define HIE_QUOTA_RECEIVE_PROP L"HIEprohibitreceivequota"
#define HIE_QUOTA_STORAGE_PROP L"HIEstoragequotalimit"

#define EX_QUOTA_SEND_PROP     L"mDBOverQuotaLimit"
#define EX_QUOTA_RECEIVE_PROP  L"mDBOverHardQuotaLimit"
#define EX_QUOTA_STORAGE_PROP  L"mDBStorageQuota"

#define IDT_QUOTA_SEND         101
#define IDT_QUOTA_RECEIVE      102
#define IDT_QUOTA_STORAGE      103

#define IDT_EX_SEND            104
#define IDT_EX_RECEIVE         105
#define IDT_EX_STORAGE         106

typedef struct CHieMmcPlugin {
    const IShellExtInitVtbl *lpVtblIShellExtInit;
    const IShellPropSheetExtVtbl *lpVtblIShellPropSheetExt;
    LONG m_cRef;
    BSTR m_bstrADsPath;
    IDirectoryObject *m_pDirObj;
} CHieMmcPlugin;

CHieMmcPlugin* CHieMmcPlugin_Create();
void CHieMmcPlugin_Destroy(CHieMmcPlugin* pThis);

#endif // HIE_MMC_PLUGIN_H
