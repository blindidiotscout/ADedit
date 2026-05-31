#include "hie_mmc_plugin.h"
#include <stdio.h>
#include <strsafe.h>

#ifndef CFSTR_ADSPATH
#define CFSTR_ADSPATH L"AdsPath"
#endif

extern HINSTANCE g_hInst;

// Konstruktor
CHieMmcPlugin::CHieMmcPlugin() : m_cRef(1), m_bstrADsPath(NULL), m_pDirObj(NULL) {
}

// Destruktor
CHieMmcPlugin::~CHieMmcPlugin() {
    if (m_bstrADsPath) SysFreeString(m_bstrADsPath);
    if (m_pDirObj) m_pDirObj->Release();
}

// Dialog Proc für unsere MMC Eigenschaftenseite
INT_PTR CALLBACK HieQuotaDlgProc(HWND hDlg, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    CHieMmcPlugin *pThis = (CHieMmcPlugin *)GetWindowLongPtr(hDlg, GWLP_USERDATA);

    switch (uMsg) {
        case WM_INITDIALOG: {
            LPPROPSHEETPAGE ppsp = (LPPROPSHEETPAGE)lParam;
            pThis = (CHieMmcPlugin *)ppsp->lParam;
            SetWindowLongPtr(hDlg, GWLP_USERDATA, (LONG_PTR)pThis);
            pThis->LoadAdsiData(hDlg);
            return TRUE;
        }
        case WM_COMMAND:
            // In einem Property Sheet werden OK/Cancel vom Framework behandelt.
            // Speichern erfolgt ausschließlich über PSN_APPLY.
            return TRUE;
        case WM_NOTIFY: {
            NMHDR* pnmh = (NMHDR*)lParam;
            switch (pnmh->code) {
                case PSN_APPLY:
                    // Wird aufgerufen, wenn der User OK oder Übernehmen klickt
                    if (pThis) pThis->SaveAdsiData(hDlg);
                    SetWindowLongPtr(hDlg, DWLP_MSGRESULT, PSNRET_NOERROR);
                    return TRUE;
            }
            break;
        }
    }
    return FALSE;
}

// --- IUnknown Implementation ---
STDMETHODIMP CHieMmcPlugin::QueryInterface(REFIID riid, void **ppv) {
    if (riid == IID_IUnknown || riid == IID_IShellExtInit) {
        *ppv = static_cast<IShellExtInit*>(this);
    } else if (riid == IID_IShellPropSheetExt) {
        *ppv = static_cast<IShellPropSheetExt*>(this);
    } else {
        *ppv = NULL;
        return E_NOINTERFACE;
    }
    AddRef();
    return S_OK;
}

STDMETHODIMP_(ULONG) CHieMmcPlugin::AddRef() {
    return InterlockedIncrement(&m_cRef);
}

STDMETHODIMP_(ULONG) CHieMmcPlugin::Release() {
    LONG cRef = InterlockedDecrement(&m_cRef);
    if (cRef == 0) delete this;
    return cRef;
}

// --- IShellExtInit Implementation ---
STDMETHODIMP CHieMmcPlugin::Initialize(LPCITEMIDLIST pidlFolder, IDataObject *pdtobj, HKEY hkeyProgID) {
    if (!pdtobj) return E_INVALIDARG;

    // Vorherige Verbindungen trennen
    if (m_pDirObj) {
        m_pDirObj->Release();
        m_pDirObj = NULL;
    }
    if (m_bstrADsPath) {
        SysFreeString(m_bstrADsPath);
        m_bstrADsPath = NULL;
    }

    // ADsPath aus dem IDataObject extrahieren (wird von ADUC bereitgestellt)
    FORMATETC fmt = { 0 };
    fmt.cfFormat = RegisterClipboardFormat(CFSTR_ADSPATH);
    fmt.ptd = NULL;
    fmt.dwAspect = DVASPECT_CONTENT;
    fmt.lindex = -1;
    fmt.tymed = TYMED_HGLOBAL;

    STGMEDIUM stg = { 0 };
    stg.tymed = TYMED_HGLOBAL;

    HRESULT hr = pdtobj->GetData(&fmt, &stg);
    if (FAILED(hr)) {
        // Fallback auf DistinguishedName, falls AdsPath nicht verfügbar
        fmt.cfFormat = RegisterClipboardFormat(L"DistinguishedName");
        hr = pdtobj->GetData(&fmt, &stg);
        if (FAILED(hr)) {
            return E_FAIL;
        }
    }

    LPOLESTR pwszPath = (LPOLESTR)GlobalLock(stg.hGlobal);
    if (!pwszPath) {
        ReleaseStgMedium(&stg);
        return E_FAIL;
    }

    m_bstrADsPath = SysAllocString(pwszPath);
    GlobalUnlock(stg.hGlobal);
    ReleaseStgMedium(&stg);

    if (!m_bstrADsPath) return E_OUTOFMEMORY;

    // Mit dem ADSI-Objekt verbinden
    hr = ADsGetObject(m_bstrADsPath, IID_IDirectoryObject, (void**)&m_pDirObj);
    if (FAILED(hr)) {
        SysFreeString(m_bstrADsPath);
        m_bstrADsPath = NULL;
        return hr;
    }

    return S_OK;
}

// --- IShellPropSheetExt Implementation ---
STDMETHODIMP CHieMmcPlugin::AddPages(LPFNADDPROPSHEETPAGE lpfnAddPage, LPARAM lParam) {
    PROPSHEETPAGE psp = {0}; // Wichtig: Struktur mit 0 initialisieren (vermeidet Garbage in Unions)
    HPROPSHEETPAGE hPage;

    psp.dwSize = sizeof(PROPSHEETPAGE);
    psp.dwFlags = PSP_USETITLE; // PSP_USEREFPARENT und PSP_USECALLBACK entfernt, da nicht initialisiert
    psp.hInstance = g_hInst;
    psp.pszTemplate = MAKEINTRESOURCE(1001); // IDD_HIEQUOTA_DIALOG aus .rc
    psp.pszTitle = L"HIE Quota";
    psp.pfnDlgProc = HieQuotaDlgProc;
    psp.lParam = (LPARAM)this;

    AddRef(); // Für die Seite referenzieren

    hPage = CreatePropertySheetPage(&psp);
    if (hPage) {
        if (!lpfnAddPage(hPage, lParam)) {
            DestroyPropertySheetPage(hPage);
            Release();
        }
    }
    return S_OK;
}

STDMETHODIMP CHieMmcPlugin::ReplacePage(EXPPS uPageID, LPFNADDPROPSHEETPAGE lpfnReplacePage, LPARAM lParam) {
    return E_NOTIMPL;
}

// --- Helper Methods ---
STDMETHODIMP CHieMmcPlugin::LoadAdsiData(HWND hWnd) {
    if (!m_pDirObj) return E_FAIL;

    HRESULT hr;
    PADS_ATTR_INFO pAttrInfo = NULL;
    DWORD dwReturned = 0;
    LPWSTR pAttrNames[] = { HIE_QUOTA_SEND_PROP, HIE_QUOTA_RECEIVE_PROP, HIE_QUOTA_STORAGE_PROP, EX_QUOTA_SEND_PROP, EX_QUOTA_RECEIVE_PROP, EX_QUOTA_STORAGE_PROP };
    
    hr = m_pDirObj->GetObjectAttributes(pAttrNames, 6, &pAttrInfo, &dwReturned);
    
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
        // Fehlerbehandlung für fehlende Schema-Erweiterung
        MessageBox(hWnd, L"HIE-Attribute konnten nicht gelesen werden. Fehlt die Schema-Erweiterung?", L"Fehler", MB_ICONERROR);
    }
    return S_OK;
}

STDMETHODIMP CHieMmcPlugin::SaveAdsiData(HWND hWnd) {
    if (!m_pDirObj) return E_FAIL;

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
    m_pDirObj->SetObjectAttributes(attrInfo, 3, &dwModified);
    
    return S_OK;
}

// --- Class Factory & DLL Exports (Standard COM Boilerplate) ---
class CHieMmcPluginClassFactory : public IClassFactory {
private:
    LONG m_cRef;
public:
    CHieMmcPluginClassFactory() : m_cRef(1) {}
    ~CHieMmcPluginClassFactory() {}

    STDMETHOD(QueryInterface)(REFIID riid, void **ppv) {
        if (riid == IID_IUnknown || riid == IID_IClassFactory) {
            *ppv = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }
        *ppv = NULL;
        return E_NOINTERFACE;
    }
    STDMETHOD_(ULONG, AddRef)() { return InterlockedIncrement(&m_cRef); }
    STDMETHOD_(ULONG, Release)() {
        LONG cRef = InterlockedDecrement(&m_cRef);
        if (cRef == 0) delete this;
        return cRef;
    }
    STDMETHOD(CreateInstance)(IUnknown* pUnkOuter, REFIID riid, void** ppv) {
        if (pUnkOuter) return CLASS_E_NOAGGREGATION;
        CHieMmcPlugin *pObj = new CHieMmcPlugin();
        if (!pObj) return E_OUTOFMEMORY;
        HRESULT hr = pObj->QueryInterface(riid, ppv);
        pObj->Release();
        return hr;
    }
    STDMETHOD(LockServer)(BOOL fLock) { return S_OK; }
};

HINSTANCE g_hInst;

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void **ppv) {
    if (rclsid == CLSID_HieMmcPlugin) {
        CHieMmcPluginClassFactory *pFactory = new CHieMmcPluginClassFactory();
        if (!pFactory) return E_OUTOFMEMORY;
        HRESULT hr = pFactory->QueryInterface(riid, ppv);
        pFactory->Release(); // Lokale Referenz freigeben
        return hr;
    }
    return CLASS_E_CLASSNOTAVAILABLE;
}

STDAPI DllCanUnloadNow() { return S_FALSE; }
STDAPI DllRegisterServer() { return S_OK; } // Vereinfacht: Registry-Einträge müssten hier gesetzt werden
STDAPI DllUnregisterServer() { return S_OK; }

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    if (ul_reason_for_call == DLL_PROCESS_ATTACH) {
        g_hInst = hModule;
    }
    return TRUE;
}
