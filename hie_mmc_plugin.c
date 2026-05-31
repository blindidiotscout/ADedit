#include "hie_mmc_plugin.h"
#include <stdio.h>
#include <strsafe.h>
#include <stddef.h>

#ifndef CFSTR_ADSPATH
#define CFSTR_ADSPATH L"AdsPath"
#endif

extern HINSTANCE g_hInst;

// Forward declarations for vtable methods
static HRESULT WINAPI HieMmcPlugin_ExtInit_QueryInterface(IShellExtInit* iface, REFIID riid, void **ppv);
static ULONG WINAPI HieMmcPlugin_ExtInit_AddRef(IShellExtInit* iface);
static ULONG WINAPI HieMmcPlugin_ExtInit_Release(IShellExtInit* iface);
static HRESULT WINAPI HieMmcPlugin_Initialize(IShellExtInit* iface, LPCITEMIDLIST pidlFolder, IDataObject *pdtobj, HKEY hkeyProgID);

static HRESULT WINAPI HieMmcPlugin_PropSheet_QueryInterface(IShellPropSheetExt* iface, REFIID riid, void **ppv);
static ULONG WINAPI HieMmcPlugin_PropSheet_AddRef(IShellPropSheetExt* iface);
static ULONG WINAPI HieMmcPlugin_PropSheet_Release(IShellPropSheetExt* iface);
static HRESULT WINAPI HieMmcPlugin_AddPages(IShellPropSheetExt* iface, LPFNADDPROPSHEETPAGE lpfnAddPage, LPARAM lParam);
static HRESULT WINAPI HieMmcPlugin_ReplacePage(IShellPropSheetExt* iface, EXPPS uPageID, LPFNADDPROPSHEETPAGE lpfnReplacePage, LPARAM lParam);

static HRESULT HieMmcPlugin_LoadAdsiData(CHieMmcPlugin* pThis, HWND hWnd);
static HRESULT HieMmcPlugin_SaveAdsiData(CHieMmcPlugin* pThis, HWND hWnd);

// Vtables
static const IShellExtInitVtbl vtbl_IShellExtInit = {
    HieMmcPlugin_ExtInit_QueryInterface,
    HieMmcPlugin_ExtInit_AddRef,
    HieMmcPlugin_ExtInit_Release,
    HieMmcPlugin_Initialize
};

static const IShellPropSheetExtVtbl vtbl_IShellPropSheetExt = {
    HieMmcPlugin_PropSheet_QueryInterface,
    HieMmcPlugin_PropSheet_AddRef,
    HieMmcPlugin_PropSheet_Release,
    HieMmcPlugin_AddPages,
    HieMmcPlugin_ReplacePage
};

// Constructor / Destructor
CHieMmcPlugin* CHieMmcPlugin_Create() {
    CHieMmcPlugin* pThis = (CHieMmcPlugin*)CoTaskMemAlloc(sizeof(CHieMmcPlugin));
    if (!pThis) return NULL;
    pThis->lpVtblIShellExtInit = &vtbl_IShellExtInit;
    pThis->lpVtblIShellPropSheetExt = &vtbl_IShellPropSheetExt;
    pThis->m_cRef = 1;
    pThis->m_bstrADsPath = NULL;
    pThis->m_pDirObj = NULL;
    return pThis;
}

void CHieMmcPlugin_Destroy(CHieMmcPlugin* pThis) {
    if (pThis->m_bstrADsPath) SysFreeString(pThis->m_bstrADsPath);
    if (pThis->m_pDirObj) pThis->m_pDirObj->lpVtbl->Release(pThis->m_pDirObj);
    CoTaskMemFree(pThis);
}

// Helper to get CHieMmcPlugin* from interface pointers
static inline CHieMmcPlugin* impl_from_IShellExtInit(IShellExtInit* iface) {
    return (CHieMmcPlugin*)((BYTE*)iface - offsetof(CHieMmcPlugin, lpVtblIShellExtInit));
}
static inline CHieMmcPlugin* impl_from_IShellPropSheetExt(IShellPropSheetExt* iface) {
    return (CHieMmcPlugin*)((BYTE*)iface - offsetof(CHieMmcPlugin, lpVtblIShellPropSheetExt));
}

// --- IUnknown / IShellExtInit Implementation ---
static HRESULT WINAPI HieMmcPlugin_ExtInit_QueryInterface(IShellExtInit* iface, REFIID riid, void **ppv) {
    CHieMmcPlugin *pThis = impl_from_IShellExtInit(iface);
    if (IsEqualIID(riid, &IID_IUnknown) || IsEqualIID(riid, &IID_IShellExtInit)) {
        *ppv = &pThis->lpVtblIShellExtInit;
    } else if (IsEqualIID(riid, &IID_IShellPropSheetExt)) {
        *ppv = &pThis->lpVtblIShellPropSheetExt;
    } else {
        *ppv = NULL;
        return E_NOINTERFACE;
    }
    pThis->lpVtblIShellExtInit->AddRef(iface);
    return S_OK;
}

static ULONG WINAPI HieMmcPlugin_ExtInit_AddRef(IShellExtInit* iface) {
    CHieMmcPlugin *pThis = impl_from_IShellExtInit(iface);
    return InterlockedIncrement(&pThis->m_cRef);
}

static ULONG WINAPI HieMmcPlugin_ExtInit_Release(IShellExtInit* iface) {
    CHieMmcPlugin *pThis = impl_from_IShellExtInit(iface);
    LONG cRef = InterlockedDecrement(&pThis->m_cRef);
    if (cRef == 0) CHieMmcPlugin_Destroy(pThis);
    return cRef;
}

static HRESULT WINAPI HieMmcPlugin_Initialize(IShellExtInit* iface, LPCITEMIDLIST pidlFolder, IDataObject *pdtobj, HKEY hkeyProgID) {
    CHieMmcPlugin *pThis = impl_from_IShellExtInit(iface);
    if (!pdtobj) return E_INVALIDARG;

    if (pThis->m_pDirObj) {
        pThis->m_pDirObj->lpVtbl->Release(pThis->m_pDirObj);
        pThis->m_pDirObj = NULL;
    }
    if (pThis->m_bstrADsPath) {
        SysFreeString(pThis->m_bstrADsPath);
        pThis->m_bstrADsPath = NULL;
    }

    FORMATETC fmt = { 0 };
    fmt.cfFormat = RegisterClipboardFormat(CFSTR_ADSPATH);
    fmt.ptd = NULL;
    fmt.dwAspect = DVASPECT_CONTENT;
    fmt.lindex = -1;
    fmt.tymed = TYMED_HGLOBAL;

    STGMEDIUM stg = { 0 };
    stg.tymed = TYMED_HGLOBAL;

    HRESULT hr = pdtobj->lpVtbl->GetData(pdtobj, &fmt, &stg);
    if (FAILED(hr)) {
        fmt.cfFormat = RegisterClipboardFormat(L"DistinguishedName");
        hr = pdtobj->lpVtbl->GetData(pdtobj, &fmt, &stg);
        if (FAILED(hr)) {
            return E_FAIL;
        }
    }

    LPOLESTR pwszPath = (LPOLESTR)GlobalLock(stg.hGlobal);
    if (!pwszPath) {
        ReleaseStgMedium(&stg);
        return E_FAIL;
    }

    pThis->m_bstrADsPath = SysAllocString(pwszPath);
    GlobalUnlock(stg.hGlobal);
    ReleaseStgMedium(&stg);

    if (!pThis->m_bstrADsPath) return E_OUTOFMEMORY;

    hr = ADsGetObject(pThis->m_bstrADsPath, &IID_IDirectoryObject, (void**)&pThis->m_pDirObj);
    if (FAILED(hr)) {
        SysFreeString(pThis->m_bstrADsPath);
        pThis->m_bstrADsPath = NULL;
        return hr;
    }

    return S_OK;
}

// --- IUnknown / IShellPropSheetExt Implementation ---
static HRESULT WINAPI HieMmcPlugin_PropSheet_QueryInterface(IShellPropSheetExt* iface, REFIID riid, void **ppv) {
    CHieMmcPlugin *pThis = impl_from_IShellPropSheetExt(iface);
    return pThis->lpVtblIShellExtInit->QueryInterface(&pThis->lpVtblIShellExtInit, riid, ppv);
}

static ULONG WINAPI HieMmcPlugin_PropSheet_AddRef(IShellPropSheetExt* iface) {
    CHieMmcPlugin *pThis = impl_from_IShellPropSheetExt(iface);
    return InterlockedIncrement(&pThis->m_cRef);
}

static ULONG WINAPI HieMmcPlugin_PropSheet_Release(IShellPropSheetExt* iface) {
    CHieMmcPlugin *pThis = impl_from_IShellPropSheetExt(iface);
    LONG cRef = InterlockedDecrement(&pThis->m_cRef);
    if (cRef == 0) CHieMmcPlugin_Destroy(pThis);
    return cRef;
}

static HRESULT WINAPI HieMmcPlugin_AddPages(IShellPropSheetExt* iface, LPFNADDPROPSHEETPAGE lpfnAddPage, LPARAM lParam) {
    CHieMmcPlugin *pThis = impl_from_IShellPropSheetExt(iface);
    PROPSHEETPAGE psp = {0};
    HPROPSHEETPAGE hPage;

    psp.dwSize = sizeof(PROPSHEETPAGE);
    psp.dwFlags = PSP_USETITLE;
    psp.hInstance = g_hInst;
    psp.pszTemplate = MAKEINTRESOURCE(1001);
    psp.pszTitle = L"HIE Quota";
    psp.pfnDlgProc = HieQuotaDlgProc;
    psp.lParam = (LPARAM)pThis;

    pThis->lpVtblIShellExtInit->AddRef(&pThis->lpVtblIShellExtInit);

    hPage = CreatePropertySheetPage(&psp);
    if (hPage) {
        if (!lpfnAddPage(hPage, lParam)) {
            DestroyPropertySheetPage(hPage);
            pThis->lpVtblIShellExtInit->Release(&pThis->lpVtblIShellExtInit);
        }
    }
    return S_OK;
}

static HRESULT WINAPI HieMmcPlugin_ReplacePage(IShellPropSheetExt* iface, EXPPS uPageID, LPFNADDPROPSHEETPAGE lpfnReplacePage, LPARAM lParam) {
    return E_NOTIMPL;
}

// --- Dialog Proc ---
INT_PTR CALLBACK HieQuotaDlgProc(HWND hDlg, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    CHieMmcPlugin *pThis = (CHieMmcPlugin *)GetWindowLongPtr(hDlg, GWLP_USERDATA);

    switch (uMsg) {
        case WM_INITDIALOG: {
            LPPROPSHEETPAGE ppsp = (LPPROPSHEETPAGE)lParam;
            pThis = (CHieMmcPlugin *)ppsp->lParam;
            SetWindowLongPtr(hDlg, GWLP_USERDATA, (LONG_PTR)pThis);
            HieMmcPlugin_LoadAdsiData(pThis, hDlg);
            return TRUE;
        }
        case WM_COMMAND:
            if (LOWORD(wParam) == IDOK) {
                if (pThis) HieMmcPlugin_SaveAdsiData(pThis, hDlg);
            }
            return TRUE;
        case WM_NOTIFY: {
            NMHDR* pnmh = (NMHDR*)lParam;
            switch (pnmh->code) {
                case PSN_APPLY:
                    if (pThis) HieMmcPlugin_SaveAdsiData(pThis, hDlg);
                    SetWindowLongPtr(hDlg, DWLP_MSGRESULT, PSNRET_NOERROR);
                    return TRUE;
            }
            break;
        }
    }
    return FALSE;
}

// --- Helper Methods ---
static HRESULT HieMmcPlugin_LoadAdsiData(CHieMmcPlugin* pThis, HWND hWnd) {
    if (!pThis->m_pDirObj) return E_FAIL;

    HRESULT hr;
    PADS_ATTR_INFO pAttrInfo = NULL;
    DWORD dwReturned = 0;
    LPWSTR pAttrNames[] = { HIE_QUOTA_SEND_PROP, HIE_QUOTA_RECEIVE_PROP, HIE_QUOTA_STORAGE_PROP, EX_QUOTA_SEND_PROP, EX_QUOTA_RECEIVE_PROP, EX_QUOTA_STORAGE_PROP };
    
    hr = pThis->m_pDirObj->lpVtbl->GetObjectAttributes(pThis->m_pDirObj, pAttrNames, 6, &pAttrInfo, &dwReturned);
    
    if (SUCCEEDED(hr)) {
        for (DWORD i = 0; i < dwReturned; i++) {
            int nCtrlID = 0;
            BOOL bReadOnly = FALSE;
            
            if (wcscmp(pAttrInfo[i].pszAttrName, HIE_QUOTA_SEND_PROP) == 0) { nCtrlID = IDT_QUOTA_SEND; }
            else if (wcscmp(pAttrInfo[i].pszAttrName, HIE_QUOTA_RECEIVE_PROP) == 0) { nCtrlID = IDT_QUOTA_RECEIVE; }
            else if (wcscmp(pAttrInfo[i].pszAttrName, HIE_QUOTA_STORAGE_PROP) == 0) { nCtrlID = IDT_QUOTA_STORAGE; }
            else if (wcscmp(pAttrInfo[i].pszAttrName, EX_QUOTA_SEND_PROP) == 0) { nCtrlID = IDT_EX_SEND; bReadOnly = TRUE; }
            else if (wcscmp(pAttrInfo[i].pszAttrName, EX_QUOTA_RECEIVE_PROP) == 0) { nCtrlID = IDT_EX_RECEIVE; bReadOnly = TRUE; }
            else if (wcscmp(pAttrInfo[i].pszAttrName, EX_QUOTA_STORAGE_PROP) == 0) { nCtrlID = IDT_EX_STORAGE; bReadOnly = TRUE; }

            if (nCtrlID != 0 && pAttrInfo[i].dwNumValues > 0) {
                SetDlgItemText(hWnd, nCtrlID, pAttrInfo[i].pADsValues->CaseIgnoreString);
                if (bReadOnly) {
                    HWND hCtrl = GetDlgItem(hWnd, nCtrlID);
                    SendMessage(hCtrl, EM_SETREADONLY, TRUE, 0);
                }
            }
        }
        FreeADsMem(pAttrInfo);
    } else {
        MessageBox(hWnd, L"HIE-Attribute konnten nicht gelesen werden. Fehlt die Schema-Erweiterung?", L"Fehler", MB_ICONERROR);
    }
    return S_OK;
}

static HRESULT HieMmcPlugin_SaveAdsiData(CHieMmcPlugin* pThis, HWND hWnd) {
    if (!pThis->m_pDirObj) return E_FAIL;

    WCHAR szSend[32], szReceive[32], szStorage[32];
    GetDlgItemText(hWnd, IDT_QUOTA_SEND, szSend, 32);
    GetDlgItemText(hWnd, IDT_QUOTA_RECEIVE, szReceive, 32);
    GetDlgItemText(hWnd, IDT_QUOTA_STORAGE, szStorage, 32);

    ADSVALUE adsValSend, adsValReceive, adsValStorage;
    adsValSend.dwType = ADSTYPE_CASE_IGNORE_STRING;
    adsValSend.CaseIgnoreString = szSend;
    adsValReceive.dwType = ADSTYPE_CASE_IGNORE_STRING;
    adsValReceive.CaseIgnoreString = szReceive;
    adsValStorage.dwType = ADSTYPE_CASE_IGNORE_STRING;
    adsValStorage.CaseIgnoreString = szStorage;

    ADS_ATTR_INFO attrInfo[3];
    attrInfo[0].pszAttrName = HIE_QUOTA_SEND_PROP;
    attrInfo[0].dwControlCode = ADS_ATTR_UPDATE;
    attrInfo[0].dwADsType = ADSTYPE_CASE_IGNORE_STRING;
    attrInfo[0].pADsValues = &adsValSend;
    attrInfo[0].dwNumValues = 1;

    attrInfo[1].pszAttrName = HIE_QUOTA_RECEIVE_PROP;
    attrInfo[1].dwControlCode = ADS_ATTR_UPDATE;
    attrInfo[1].dwADsType = ADSTYPE_CASE_IGNORE_STRING;
    attrInfo[1].pADsValues = &adsValReceive;
    attrInfo[1].dwNumValues = 1;

    attrInfo[2].pszAttrName = HIE_QUOTA_STORAGE_PROP;
    attrInfo[2].dwControlCode = ADS_ATTR_UPDATE;
    attrInfo[2].dwADsType = ADSTYPE_CASE_IGNORE_STRING;
    attrInfo[2].pADsValues = &adsValStorage;
    attrInfo[2].dwNumValues = 1;

    DWORD dwModified = 0;
    pThis->m_pDirObj->lpVtbl->SetObjectAttributes(pThis->m_pDirObj, attrInfo, 3, &dwModified);
    
    return S_OK;
}

// --- Class Factory & DLL Exports ---
typedef struct CHieMmcPluginClassFactory {
    const IClassFactoryVtbl *lpVtbl;
    LONG m_cRef;
} CHieMmcPluginClassFactory;

static HRESULT WINAPI HieMmcPluginClassFactory_QueryInterface(IClassFactory* iface, REFIID riid, void **ppv) {
    if (IsEqualIID(riid, &IID_IUnknown) || IsEqualIID(riid, &IID_IClassFactory)) {
        *ppv = iface;
        iface->lpVtbl->AddRef(iface);
        return S_OK;
    }
    *ppv = NULL;
    return E_NOINTERFACE;
}

static ULONG WINAPI HieMmcPluginClassFactory_AddRef(IClassFactory* iface) {
    CHieMmcPluginClassFactory *pThis = (CHieMmcPluginClassFactory*)iface;
    return InterlockedIncrement(&pThis->m_cRef);
}

static ULONG WINAPI HieMmcPluginClassFactory_Release(IClassFactory* iface) {
    CHieMmcPluginClassFactory *pThis = (CHieMmcPluginClassFactory*)iface;
    LONG cRef = InterlockedDecrement(&pThis->m_cRef);
    if (cRef == 0) CoTaskMemFree(pThis);
    return cRef;
}

static HRESULT WINAPI HieMmcPluginClassFactory_CreateInstance(IClassFactory* iface, IUnknown* pUnkOuter, REFIID riid, void** ppv) {
    if (pUnkOuter) return CLASS_E_NOAGGREGATION;
    CHieMmcPlugin *pObj = CHieMmcPlugin_Create();
    if (!pObj) return E_OUTOFMEMORY;
    HRESULT hr = pObj->lpVtblIShellExtInit->QueryInterface(&pObj->lpVtblIShellExtInit, riid, ppv);
    pObj->lpVtblIShellExtInit->Release(&pObj->lpVtblIShellExtInit);
    return hr;
}

static HRESULT WINAPI HieMmcPluginClassFactory_LockServer(IClassFactory* iface, BOOL fLock) { return S_OK; }

static const IClassFactoryVtbl vtbl_IClassFactory = {
    HieMmcPluginClassFactory_QueryInterface,
    HieMmcPluginClassFactory_AddRef,
    HieMmcPluginClassFactory_Release,
    HieMmcPluginClassFactory_CreateInstance,
    HieMmcPluginClassFactory_LockServer
};

HINSTANCE g_hInst;

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void **ppv) {
    if (IsEqualCLSID(rclsid, &CLSID_HieMmcPlugin)) {
        CHieMmcPluginClassFactory *pFactory = (CHieMmcPluginClassFactory*)CoTaskMemAlloc(sizeof(CHieMmcPluginClassFactory));
        if (!pFactory) return E_OUTOFMEMORY;
        pFactory->lpVtbl = &vtbl_IClassFactory;
        pFactory->m_cRef = 1;
        
        HRESULT hr = pFactory->lpVtbl->QueryInterface(&pFactory->lpVtbl, riid, ppv);
        pFactory->lpVtbl->Release(&pFactory->lpVtbl);
        return hr;
    }
    return CLASS_E_CLASSNOTAVAILABLE;
}

STDAPI DllCanUnloadNow() { return S_FALSE; }
STDAPI DllRegisterServer() { return S_OK; }
STDAPI DllUnregisterServer() { return S_OK; }

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    if (ul_reason_for_call == DLL_PROCESS_ATTACH) {
        g_hInst = hModule;
    }
    return TRUE;
}
