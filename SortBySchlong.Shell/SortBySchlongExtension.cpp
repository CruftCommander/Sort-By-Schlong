#include "SortBySchlongExtension.h"
#include "ProcessLauncher.h"
#include <shlwapi.h>
#include <vector>
#include <string>

#pragma comment(lib, "shlwapi.lib")

// Menu item text - English only for v1
// Note: Windows menus use & for keyboard accelerators, so "Sort by" appears as "S&ort by"
constexpr const wchar_t* SORT_BY_MENU_TEXT = L"S&ort by";
constexpr const wchar_t* PENIS_MENU_TEXT = L"Penis";

CSortBySchlongExtension::CSortBySchlongExtension()
    : m_cRef(1)
    , m_sortByPenisId(0)
    , m_isDesktopBackground(false)
{
}

CSortBySchlongExtension::~CSortBySchlongExtension()
{
    LogDebug(L"~CSortBySchlongExtension: Destructor called");
}

IFACEMETHODIMP CSortBySchlongExtension::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] =
    {
        QITABENT(CSortBySchlongExtension, IShellExtInit),
        QITABENT(CSortBySchlongExtension, IContextMenu),
        { nullptr, 0 }
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG) CSortBySchlongExtension::AddRef()
{
    return InterlockedIncrement(&m_cRef);
}

IFACEMETHODIMP_(ULONG) CSortBySchlongExtension::Release()
{
    ULONG cRef = InterlockedDecrement(&m_cRef);
    if (cRef == 0)
    {
        delete this;
    }
    return cRef;
}

IFACEMETHODIMP CSortBySchlongExtension::Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* /*pdtobj*/, HKEY /*hkeyProgID*/)
{
    // Reset state
    m_isDesktopBackground = false;
    m_sortByPenisId = 0;
    
    LogDebug(L"Initialize called");

    // We only handle desktop background context
    // pidlFolder will be the desktop background PIDL when invoked on desktop
    if (pidlFolder != nullptr)
    {
        // For desktop background, we expect a valid PIDL
        // In practice, this is called when right-clicking the desktop
        m_isDesktopBackground = true;
        LogDebug(L"Initialize: Desktop background detected");
    }

    return S_OK;
}

IFACEMETHODIMP CSortBySchlongExtension::QueryContextMenu(HMENU hmenu, UINT /*indexMenu*/, UINT idCmdFirst, UINT /*idCmdLast*/, UINT uFlags)
{
    // Early return if default only (double-click)
    if (uFlags & CMF_DEFAULTONLY)
    {
        return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
    }

    try
    {
        LogDebug(L"QueryContextMenu called");
        
        // Find "Sort by" menu item
        int sortByIndex = FindSortByMenuIndex(hmenu);
        if (sortByIndex == -1)
        {
            // "Sort by" menu not found - fail gracefully
            LogDebug(L"QueryContextMenu: Sort by menu not found");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        LogDebug(L"QueryContextMenu: Found Sort by menu at index " + std::to_wstring(sortByIndex));

        // Get the "Sort by" submenu
        HMENU hSubMenu = GetSubMenu(hmenu, sortByIndex);
        if (hSubMenu == nullptr)
        {
            LogDebug(L"QueryContextMenu: GetSubMenu returned null");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Validate command ID is within valid range (must be < 0x8000)
        if (idCmdFirst >= 0x8000)
        {
            LogDebug(L"QueryContextMenu: Command ID out of range");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Validate the submenu handle
        if (!IsMenu(hSubMenu))
        {
            LogDebug(L"QueryContextMenu: Invalid submenu handle");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Get count of items in submenu to append at the end
        int itemCount = GetMenuItemCount(hSubMenu);
        if (itemCount < 0)
        {
            LogDebug(L"QueryContextMenu: GetMenuItemCount failed");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Prepare menu text in a local buffer
        // Windows will copy the string when InsertMenuItem is called
        wchar_t menuTextBuffer[64] = {};
        size_t textLen = wcslen(PENIS_MENU_TEXT);
        if (textLen >= _countof(menuTextBuffer))
        {
            LogDebug(L"QueryContextMenu: Menu text too long");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        wcscpy_s(menuTextBuffer, _countof(menuTextBuffer), PENIS_MENU_TEXT);

        // Prepare menu item structure with minimal required fields
        MENUITEMINFOW mii = {};
        mii.cbSize = sizeof(MENUITEMINFOW);
        mii.fMask = MIIM_STRING | MIIM_ID;
        mii.wID = idCmdFirst;
        mii.dwTypeData = menuTextBuffer;
        // Note: cch is not needed when using dwTypeData with null-terminated string

        // Insert at the end of the submenu
        BOOL inserted = InsertMenuItemW(hSubMenu, static_cast<UINT>(itemCount), TRUE, &mii);
        if (!inserted)
        {
            DWORD error = GetLastError();
            wchar_t errorMsg[256];
            swprintf_s(errorMsg, L"QueryContextMenu: InsertMenuItemW failed with error %lu", error);
            LogDebug(errorMsg);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        // Store the command ID only after successful insertion
        m_sortByPenisId = idCmdFirst;
        LogDebug(L"QueryContextMenu: Menu item inserted successfully with ID " + std::to_wstring(idCmdFirst));
        
        LogDebug(L"QueryContextMenu: Successfully inserted menu item with ID " + std::to_wstring(idCmdFirst));

        // Return number of items added
        return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
    }
    catch (...)
    {
        return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
    }
}

IFACEMETHODIMP CSortBySchlongExtension::InvokeCommand(LPCMINVOKECOMMANDINFO pici)
{
    if (pici == nullptr)
    {
        return E_INVALIDARG;
    }

    try
    {
        // Check if command is invoked by index or verb
        UINT commandId = 0;
        bool isByIndex = false;

        if (HIWORD(pici->lpVerb) == 0)
        {
            // Command is invoked by index
            commandId = LOWORD(pici->lpVerb);
            isByIndex = true;
        }
        else
        {
            // Command is invoked by verb string - we don't use verbs, so fail
            return E_FAIL;
        }

        // Check if this is our command (only if invoked by index)
        if (!isByIndex || commandId != static_cast<UINT>(m_sortByPenisId))
        {
            return E_FAIL;
        }

        // Launch the console harness
        bool success = ProcessLauncher::LaunchConsoleHarness(L"penis");
        if (!success)
        {
            LogDebug(L"InvokeCommand: Failed to launch ConsoleHarness");
            // Fail silently - don't show error to user
        }

        return S_OK;
    }
    catch (...)
    {
        return E_FAIL;
    }
}

IFACEMETHODIMP CSortBySchlongExtension::GetCommandString(UINT_PTR idCmd, UINT uFlags, UINT* /*pwReserved*/, LPSTR pszName, UINT cchMax)
{
    // Early return if command ID doesn't match or isn't set
    // Don't log here to avoid potential issues during menu cleanup
    if (m_sortByPenisId == 0 || idCmd != static_cast<UINT_PTR>(m_sortByPenisId))
    {
        return E_INVALIDARG;
    }

    // Validate buffer - return early if invalid
    if (pszName == nullptr || cchMax == 0)
    {
        return E_INVALIDARG;
    }

    try
    {
        switch (uFlags)
        {
        case GCS_VERBA:
            {
                const char* verb = "penis";
                if (strlen(verb) >= cchMax)
                {
                    return E_INVALIDARG;
                }
                strcpy_s(pszName, cchMax, verb);
                return S_OK;
            }

        case GCS_VERBW:
            {
                LPWSTR pszWide = reinterpret_cast<LPWSTR>(pszName);
                if (wcslen(PENIS_MENU_TEXT) >= cchMax)
                {
                    return E_INVALIDARG;
                }
                wcscpy_s(pszWide, cchMax, PENIS_MENU_TEXT);
                return S_OK;
            }

        case GCS_HELPTEXTA:
            {
                const char* helpText = "Arrange desktop icons in a penis shape";
                if (strlen(helpText) >= cchMax)
                {
                    return E_INVALIDARG;
                }
                strcpy_s(pszName, cchMax, helpText);
                return S_OK;
            }

        case GCS_HELPTEXTW:
            {
                LPWSTR pszWide = reinterpret_cast<LPWSTR>(pszName);
                const wchar_t* helpText = L"Arrange desktop icons in a penis shape";
                if (wcslen(helpText) >= cchMax)
                {
                    return E_INVALIDARG;
                }
                wcscpy_s(pszWide, cchMax, helpText);
                return S_OK;
            }

        case GCS_VALIDATEA:
        case GCS_VALIDATEW:
            return S_OK;

        default:
            return E_INVALIDARG;
        }
    }
    catch (...)
    {
        return E_FAIL;
    }
}

int CSortBySchlongExtension::FindSortByMenuIndex(HMENU hmenu) const
{
    if (hmenu == nullptr)
    {
        LogDebug(L"FindSortByMenuIndex: hmenu is null");
        return -1;
    }

    int itemCount = GetMenuItemCount(hmenu);
    if (itemCount == -1)
    {
        LogDebug(L"FindSortByMenuIndex: GetMenuItemCount failed");
        return -1;
    }

    LogDebug(L"FindSortByMenuIndex: Searching through " + std::to_wstring(itemCount) + L" menu items");

    // Search for "Sort by" menu item
    for (int i = 0; i < itemCount; i++)
    {
        MENUITEMINFOW mii = {};
        mii.cbSize = sizeof(mii);
        mii.fMask = MIIM_STRING | MIIM_SUBMENU;

        wchar_t buffer[256] = {};
        mii.dwTypeData = buffer;
        mii.cch = _countof(buffer);

        if (GetMenuItemInfoW(hmenu, i, TRUE, &mii))
        {
            std::wstring menuText = buffer;
            std::wstring debugMsg = L"FindSortByMenuIndex: Item " + std::to_wstring(i) + L" = \"" + menuText + L"\"";
            if (mii.hSubMenu != nullptr)
            {
                debugMsg += L" (has submenu)";
            }
            LogDebug(debugMsg);

            if (mii.hSubMenu != nullptr && _wcsicmp(buffer, SORT_BY_MENU_TEXT) == 0)
            {
                LogDebug(L"FindSortByMenuIndex: Found \"Sort by\" at index " + std::to_wstring(i));
                return i;
            }
        }
    }

    LogDebug(L"FindSortByMenuIndex: \"Sort by\" menu not found");
    return -1;
}

void CSortBySchlongExtension::LogDebug(const std::wstring& message) const
{
    std::wstring fullMessage = L"[CSortBySchlongExtension] ";
    fullMessage += message;
    fullMessage += L"\r\n";
    OutputDebugStringW(fullMessage.c_str());
}

