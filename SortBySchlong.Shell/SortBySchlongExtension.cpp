#include "SortBySchlongExtension.h"
#include "ProcessLauncher.h"
#include <shlwapi.h>
#include <vector>
#include <string>

#pragma comment(lib, "shlwapi.lib")

// Menu item text - English only for v1
constexpr const wchar_t* SORT_BY_MENU_TEXT = L"Sort by";
constexpr const wchar_t* PENIS_MENU_TEXT = L"Penis";

CSortBySchlongExtension::CSortBySchlongExtension()
    : m_cRef(1)
    , m_sortByPenisId(0)
    , m_isDesktopBackground(false)
{
}

CSortBySchlongExtension::~CSortBySchlongExtension()
{
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

IFACEMETHODIMP CSortBySchlongExtension::Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* pdtobj, HKEY hkeyProgID)
{
    // Reset state
    m_isDesktopBackground = false;
    m_sortByPenisId = 0;

    // We only handle desktop background context
    // pidlFolder will be the desktop background PIDL when invoked on desktop
    if (pidlFolder != nullptr)
    {
        // For desktop background, we expect a valid PIDL
        // In practice, this is called when right-clicking the desktop
        m_isDesktopBackground = true;
    }

    return S_OK;
}

IFACEMETHODIMP CSortBySchlongExtension::QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags)
{
    // Early return if default only (double-click)
    if (uFlags & CMF_DEFAULTONLY)
    {
        return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
    }

    try
    {
        // Find "Sort by" menu item
        int sortByIndex = FindSortByMenuIndex(hmenu);
        if (sortByIndex == -1)
        {
            // "Sort by" menu not found - fail gracefully
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Get the "Sort by" submenu
        HMENU hSubMenu = GetSubMenu(hmenu, sortByIndex);
        if (hSubMenu == nullptr)
        {
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Insert "Penis" menu item
        MENUITEMINFOW mii = {};
        mii.cbSize = sizeof(mii);
        mii.fMask = MIIM_STRING | MIIM_ID;
        mii.wID = idCmdFirst;
        mii.dwTypeData = const_cast<LPWSTR>(PENIS_MENU_TEXT);
        mii.cch = static_cast<UINT>(wcslen(PENIS_MENU_TEXT));

        BOOL inserted = InsertMenuItemW(hSubMenu, 0, TRUE, &mii);
        if (!inserted)
        {
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Store the command ID
        m_sortByPenisId = idCmdFirst;

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

IFACEMETHODIMP CSortBySchlongExtension::GetCommandString(UINT_PTR idCmd, UINT uFlags, UINT* pwReserved, LPSTR pszName, UINT cchMax)
{
    if (idCmd != m_sortByPenisId)
    {
        return E_INVALIDARG;
    }

    try
    {
        switch (uFlags)
        {
        case GCS_VERBA:
            if (pszName != nullptr && cchMax > 0)
            {
                strcpy_s(static_cast<char*>(pszName), cchMax, "penis");
                return S_OK;
            }
            break;

        case GCS_VERBW:
            if (pszName != nullptr && cchMax > 0)
            {
                wcscpy_s(static_cast<LPWSTR>(pszName), cchMax, PENIS_MENU_TEXT);
                return S_OK;
            }
            break;

        case GCS_HELPTEXTA:
            if (pszName != nullptr && cchMax > 0)
            {
                strcpy_s(static_cast<char*>(pszName), cchMax, "Arrange desktop icons in a penis shape");
                return S_OK;
            }
            break;

        case GCS_HELPTEXTW:
            if (pszName != nullptr && cchMax > 0)
            {
                const wchar_t* helpText = L"Arrange desktop icons in a penis shape";
                wcscpy_s(static_cast<LPWSTR>(pszName), cchMax, helpText);
                return S_OK;
            }
            break;

        case GCS_VALIDATEA:
        case GCS_VALIDATEW:
            return S_OK;
        }

        return E_INVALIDARG;
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
        return -1;
    }

    int itemCount = GetMenuItemCount(hmenu);
    if (itemCount == -1)
    {
        return -1;
    }

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
            if (mii.hSubMenu != nullptr && _wcsicmp(buffer, SORT_BY_MENU_TEXT) == 0)
            {
                return i;
            }
        }
    }

    return -1;
}

void CSortBySchlongExtension::LogDebug(const std::wstring& message) const
{
    std::wstring fullMessage = L"[CSortBySchlongExtension] ";
    fullMessage += message;
    fullMessage += L"\r\n";
    OutputDebugStringW(fullMessage.c_str());
}

