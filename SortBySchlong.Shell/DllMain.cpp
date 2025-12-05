#include <windows.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <ole2.h>
#include "Guids.h"
#include "ClassFactory.h"
#include <string>
#include <vector>

#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "oleaut32.lib")
#pragma comment(lib, "shlwapi.lib")

// Forward declarations
STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv);
STDAPI DllCanUnloadNow(void);
STDAPI DllRegisterServer(void);
STDAPI DllUnregisterServer(void);

HINSTANCE g_hInst = nullptr;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD dwReason, LPVOID lpReserved)
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        g_hInst = hModule;
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    *ppv = nullptr;

    if (IsEqualCLSID(rclsid, CLSID_SortBySchlongExtension))
    {
        CClassFactory* pFactory = new (std::nothrow) CClassFactory();
        if (pFactory == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        HRESULT hr = pFactory->QueryInterface(riid, ppv);
        pFactory->Release();
        return hr;
    }

    return CLASS_E_CLASSNOTAVAILABLE;
}

STDAPI DllCanUnloadNow(void)
{
    // Can unload if no objects are active and no locks are held
    if (CClassFactory::GetObjectCount() == 0 && CClassFactory::GetLockCount() == 0)
    {
        return S_OK;
    }
    return S_FALSE;
}

STDAPI DllRegisterServer(void)
{
    wchar_t szModulePath[MAX_PATH] = {};
    DWORD pathLen = GetModuleFileNameW(g_hInst, szModulePath, MAX_PATH);
    if (pathLen == 0 || pathLen >= MAX_PATH)
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    // Convert CLSID to string
    wchar_t szCLSID[64] = {};
    StringFromGUID2(CLSID_SortBySchlongExtension, szCLSID, _countof(szCLSID));

    // Build registry paths
    std::wstring clsidKey = L"CLSID\\";
    clsidKey += szCLSID;

    std::wstring inprocKey = clsidKey + L"\\InprocServer32";
    
    std::wstring handlerKey = L"Directory\\Background\\shellex\\ContextMenuHandlers\\SortBySchlong";

    // Register CLSID
    HKEY hKey = nullptr;
    LONG lResult = RegCreateKeyExW(HKEY_CLASSES_ROOT, clsidKey.c_str(), 0, nullptr,
        REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr, &hKey, nullptr);
    if (lResult == ERROR_SUCCESS)
    {
        const wchar_t* szDescription = L"SortBySchlong Shell Extension";
        RegSetValueExW(hKey, nullptr, 0, REG_SZ,
            reinterpret_cast<const BYTE*>(szDescription),
            static_cast<DWORD>((wcslen(szDescription) + 1) * sizeof(wchar_t)));
        RegCloseKey(hKey);
    }

    // Register InprocServer32
    hKey = nullptr;
    lResult = RegCreateKeyExW(HKEY_CLASSES_ROOT, inprocKey.c_str(), 0, nullptr,
        REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr, &hKey, nullptr);
    if (lResult == ERROR_SUCCESS)
    {
        RegSetValueExW(hKey, nullptr, 0, REG_SZ,
            reinterpret_cast<const BYTE*>(szModulePath),
            static_cast<DWORD>((wcslen(szModulePath) + 1) * sizeof(wchar_t)));

        const wchar_t* szThreadingModel = L"Apartment";
        RegSetValueExW(hKey, L"ThreadingModel", 0, REG_SZ,
            reinterpret_cast<const BYTE*>(szThreadingModel),
            static_cast<DWORD>((wcslen(szThreadingModel) + 1) * sizeof(wchar_t)));
        RegCloseKey(hKey);
    }

    // Register per-user context menu handler (recommended for dev/testing)
    // Use HKEY_CURRENT_USER\Software\Classes instead of HKEY_CLASSES_ROOT
    std::wstring userHandlerKey = L"Software\\Classes\\Directory\\Background\\shellex\\ContextMenuHandlers\\SortBySchlong";
    hKey = nullptr;
    lResult = RegCreateKeyExW(HKEY_CURRENT_USER, userHandlerKey.c_str(), 0, nullptr,
        REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr, &hKey, nullptr);
    if (lResult == ERROR_SUCCESS)
    {
        RegSetValueExW(hKey, nullptr, 0, REG_SZ,
            reinterpret_cast<const BYTE*>(szCLSID),
            static_cast<DWORD>((wcslen(szCLSID) + 1) * sizeof(wchar_t)));
        RegCloseKey(hKey);
    }

    return S_OK;
}

STDAPI DllUnregisterServer(void)
{
    // Convert CLSID to string
    wchar_t szCLSID[64] = {};
    StringFromGUID2(CLSID_SortBySchlongExtension, szCLSID, _countof(szCLSID));

    // Build registry paths
    std::wstring clsidKey = L"CLSID\\";
    clsidKey += szCLSID;

    std::wstring userHandlerKey = L"Software\\Classes\\Directory\\Background\\shellex\\ContextMenuHandlers\\SortBySchlong";

    // Remove per-user context menu handler
    RegDeleteTreeW(HKEY_CURRENT_USER, userHandlerKey.c_str());
    // Ignore errors if key doesn't exist

    // Remove CLSID key (which will also remove InprocServer32)
    SHDeleteKeyW(HKEY_CLASSES_ROOT, clsidKey.c_str());
    // Ignore errors if key doesn't exist

    return S_OK;
}

