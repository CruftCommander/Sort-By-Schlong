#include "SortBySchlongExtension.h"
#include "ProcessLauncher.h"
#include "MenuBuilder.h"
#include "MenuConstants.h"
#include <shlwapi.h>
#include <vector>
#include <string>

#pragma comment(lib, "shlwapi.lib")

// Compile-time flag to test simplified menu (direct item instead of submenu)
// Set to 1 to test if submenu is causing the crash
// Set to 0 to use normal submenu behavior
#ifndef SIMPLE_MENU_TEST
#define SIMPLE_MENU_TEST 1
#endif

CSortBySchlongExtension::CSortBySchlongExtension()
    : m_cRef(1)
    , m_commandIdFirst(0)
    , m_commandIdCount(0)
    , m_isDesktopBackground(false)
{
}

CSortBySchlongExtension::~CSortBySchlongExtension()
{
    // Log immediately using direct OutputDebugString
    wchar_t immediateLog[256];
    swprintf_s(immediateLog, L"[TID:%lu] ~CSortBySchlongExtension DESTRUCTOR CALLED: this=%p, m_cRef=%ld\r\n", 
               GetCurrentThreadId(), this, m_cRef);
    OutputDebugStringW(immediateLog);
    
    try
    {
        wchar_t debugMsg[256];
        swprintf_s(debugMsg, L"~CSortBySchlongExtension: Destructor called, m_cRef=%ld, m_commandIdFirst=%u, m_commandIdCount=%u, m_isDesktopBackground=%d",
                   m_cRef, m_commandIdFirst, m_commandIdCount, m_isDesktopBackground ? 1 : 0);
        LogDebug(debugMsg);
    }
    catch (...)
    {
        // Don't log in destructor if logging fails - might cause issues
        OutputDebugStringW(L"[CSortBySchlongExtension] Destructor: Exception in logging\r\n");
    }
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
    ULONG cRef = InterlockedIncrement(&m_cRef);
    wchar_t logMsg[128];
    swprintf_s(logMsg, L"[TID:%lu] AddRef: this=%p, m_cRef=%lu\r\n", GetCurrentThreadId(), this, cRef);
    OutputDebugStringW(logMsg);
    return cRef;
}

IFACEMETHODIMP_(ULONG) CSortBySchlongExtension::Release()
{
    ULONG cRef = InterlockedDecrement(&m_cRef);
    wchar_t logMsg[128];
    swprintf_s(logMsg, L"[TID:%lu] Release: this=%p, m_cRef=%lu\r\n", GetCurrentThreadId(), this, cRef);
    OutputDebugStringW(logMsg);
    if (cRef == 0)
    {
        OutputDebugStringW(L"Release: Reference count reached 0, deleting object\r\n");
        delete this;
    }
    return cRef;
}

IFACEMETHODIMP CSortBySchlongExtension::Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* /*pdtobj*/, HKEY /*hkeyProgID*/)
{
    try
    {
        // Reset state
        m_isDesktopBackground = false;
        m_commandIdFirst = 0;
        m_commandIdCount = 0;
        
        wchar_t debugMsg[512];
        swprintf_s(debugMsg, L"Initialize: ENTRY pidlFolder=%p, m_cRef=%ld", 
                   pidlFolder, m_cRef);
        LogDebug(debugMsg);

        // We only handle desktop background context
        // pidlFolder will be the desktop background PIDL when invoked on desktop
        if (pidlFolder != nullptr)
        {
            // For desktop background, we expect a valid PIDL
            // In practice, this is called when right-clicking the desktop
            m_isDesktopBackground = true;
            LogDebug(L"Initialize: Desktop background detected");
        }
        else
        {
            LogDebug(L"Initialize: pidlFolder is null - not desktop background");
        }

        LogDebug(L"Initialize: EXIT returning S_OK");
        return S_OK;
    }
    catch (...)
    {
        LogDebug(L"Initialize: Exception caught");
        return E_FAIL;
    }
}

IFACEMETHODIMP CSortBySchlongExtension::QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags)
{
    try
    {
        wchar_t debugMsg[512];
        swprintf_s(debugMsg, L"QueryContextMenu: ENTRY hmenu=%p, indexMenu=%u, idCmdFirst=%u, idCmdLast=%u, uFlags=0x%X, m_isDesktopBackground=%d, m_cRef=%ld",
                   hmenu, indexMenu, idCmdFirst, idCmdLast, uFlags, m_isDesktopBackground ? 1 : 0, m_cRef);
        LogDebug(debugMsg);
        
        // Early return if default only (double-click)
        if (uFlags & CMF_DEFAULTONLY)
        {
            LogDebug(L"QueryContextMenu: CMF_DEFAULTONLY flag set - returning 0");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Only show menu on desktop background
        if (!m_isDesktopBackground)
        {
            LogDebug(L"QueryContextMenu: Not desktop background - returning 0");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Reset command tracking
        m_commandIdFirst = 0;
        m_commandIdCount = 0;
        
        // Validate inputs
        if (hmenu == nullptr)
        {
            LogDebug(L"QueryContextMenu: hmenu is null");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        if (!IsMenu(hmenu))
        {
            LogDebug(L"QueryContextMenu: hmenu is not a valid menu handle");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        // Validate command ID is within valid range (must be < 0x8000)
        if (idCmdFirst >= 0x8000)
        {
            swprintf_s(debugMsg, L"QueryContextMenu: Command ID %u out of range (>= 0x8000)", idCmdFirst);
            LogDebug(debugMsg);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

#if SIMPLE_MENU_TEST
        // SIMPLIFIED TEST VERSION: Direct menu item (no submenu)
        // This helps isolate if the crash is submenu-specific
        LogDebug(L"QueryContextMenu: SIMPLE_MENU_TEST enabled - using direct menu item");
        
        // Use InsertMenuItem instead of AppendMenu for more control
        // Create menu text in a static buffer to ensure it persists
        static wchar_t combinedText[256] = {};
        if (combinedText[0] == L'\0')
        {
            // Initialize once
            swprintf_s(combinedText, L"%s - %s", SortBySchlongUI::MenuRootText, SortBySchlongUI::MenuPenisText);
        }
        
        swprintf_s(debugMsg, L"QueryContextMenu: Inserting direct menu item, id=%u, text='%s'", idCmdFirst, combinedText);
        LogDebug(debugMsg);
        
        MENUITEMINFOW mii = {};
        mii.cbSize = sizeof(MENUITEMINFOW);
        mii.fMask = MIIM_STRING | MIIM_ID;
        mii.wID = idCmdFirst;
        mii.dwTypeData = combinedText;
        
        // Insert at the end of the menu
        int menuItemCount = GetMenuItemCount(hmenu);
        if (menuItemCount < 0)
        {
            LogDebug(L"QueryContextMenu: GetMenuItemCount failed");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        swprintf_s(debugMsg, L"QueryContextMenu: Current menu has %d items, inserting at position %d", menuItemCount, menuItemCount);
        LogDebug(debugMsg);
        
        if (!InsertMenuItemW(hmenu, static_cast<UINT>(menuItemCount), TRUE, &mii))
        {
            DWORD error = GetLastError();
            swprintf_s(debugMsg, L"QueryContextMenu: InsertMenuItemW failed for direct menu item, error %lu", error);
            LogDebug(debugMsg);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        // Verify the item was inserted
        int newCount = GetMenuItemCount(hmenu);
        swprintf_s(debugMsg, L"QueryContextMenu: Menu now has %d items", newCount);
        LogDebug(debugMsg);
        
        // Store command ID range
        m_commandIdFirst = idCmdFirst;
        m_commandIdCount = 1;
        
        swprintf_s(debugMsg, L"QueryContextMenu: EXIT (SIMPLE) - Successfully added direct menu item, m_commandIdFirst=%u, m_commandIdCount=%u, returning 1", 
                   m_commandIdFirst, m_commandIdCount);
        LogDebug(debugMsg);
        
        HRESULT hr = MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
        
        // Log after creating return value to ensure we see this
        wchar_t postReturnLog[256];
        swprintf_s(postReturnLog, L"[TID:%lu] QueryContextMenu ABOUT TO RETURN: hr=0x%08X\r\n", GetCurrentThreadId(), hr);
        OutputDebugStringW(postReturnLog);
        
        return hr;
#else
        // NORMAL VERSION: Submenu with items
        LogDebug(L"QueryContextMenu: Creating submenu...");
        
        // Create submenu
        HMENU hSubMenu = CreatePopupMenu();
        if (hSubMenu == nullptr)
        {
            DWORD error = GetLastError();
            swprintf_s(debugMsg, L"QueryContextMenu: CreatePopupMenu failed, error %lu", error);
            LogDebug(debugMsg);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        if (!IsMenu(hSubMenu))
        {
            LogDebug(L"QueryContextMenu: Created submenu handle is invalid");
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        swprintf_s(debugMsg, L"QueryContextMenu: Submenu created successfully, hSubMenu=%p", hSubMenu);
        LogDebug(debugMsg);

        // Add "Penis" item to submenu with explicit string copy
        UINT currentId = idCmdFirst;
        const wchar_t* penisText = SortBySchlongUI::MenuPenisText;
        if (penisText == nullptr)
        {
            LogDebug(L"QueryContextMenu: MenuPenisText is null");
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        size_t penisTextLen = wcslen(penisText);
        if (penisTextLen == 0)
        {
            LogDebug(L"QueryContextMenu: MenuPenisText is empty");
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        swprintf_s(debugMsg, L"QueryContextMenu: Appending menu item, id=%u, text='%s'", currentId, penisText);
        LogDebug(debugMsg);
        
        if (!AppendMenuW(hSubMenu, MF_STRING, currentId, penisText))
        {
            DWORD error = GetLastError();
            swprintf_s(debugMsg, L"QueryContextMenu: AppendMenuW failed for submenu item, error %lu", error);
            LogDebug(debugMsg);
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Verify submenu has the item
        int itemCount = GetMenuItemCount(hSubMenu);
        swprintf_s(debugMsg, L"QueryContextMenu: Submenu item count=%d", itemCount);
        LogDebug(debugMsg);
        
        if (itemCount != 1)
        {
            swprintf_s(debugMsg, L"QueryContextMenu: Submenu item count mismatch, expected 1, got %d", itemCount);
            LogDebug(debugMsg);
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Add submenu to main context menu with explicit string copy
        const wchar_t* rootText = SortBySchlongUI::MenuRootText;
        if (rootText == nullptr)
        {
            LogDebug(L"QueryContextMenu: MenuRootText is null");
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        size_t rootTextLen = wcslen(rootText);
        if (rootTextLen == 0)
        {
            LogDebug(L"QueryContextMenu: MenuRootText is empty");
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        swprintf_s(debugMsg, L"QueryContextMenu: Appending submenu to main menu, text='%s', hSubMenu=%p", rootText, hSubMenu);
        LogDebug(debugMsg);
        
        if (!AppendMenuW(hmenu, MF_STRING | MF_POPUP, reinterpret_cast<UINT_PTR>(hSubMenu), rootText))
        {
            DWORD error = GetLastError();
            swprintf_s(debugMsg, L"QueryContextMenu: AppendMenuW failed for main menu, error %lu", error);
            LogDebug(debugMsg);
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        swprintf_s(debugMsg, L"QueryContextMenu: Submenu successfully appended to main menu, hSubMenu=%p (Windows now owns it)", hSubMenu);
        LogDebug(debugMsg);
        
        // Windows now owns hSubMenu - don't destroy it

        // Store command ID range (only the items in the submenu, not the submenu itself)
        m_commandIdFirst = idCmdFirst;
        m_commandIdCount = 1;
        
        swprintf_s(debugMsg, L"QueryContextMenu: EXIT - Successfully added menu, m_commandIdFirst=%u, m_commandIdCount=%u, returning 1", 
                   m_commandIdFirst, m_commandIdCount);
        LogDebug(debugMsg);

        HRESULT hr = MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
        
        // Log after creating return value to ensure we see this
        wchar_t postReturnLog[256];
        swprintf_s(postReturnLog, L"[TID:%lu] QueryContextMenu (SUBMENU) ABOUT TO RETURN: hr=0x%08X\r\n", GetCurrentThreadId(), hr);
        OutputDebugStringW(postReturnLog);
        
        // Return number of command IDs consumed (items in submenu)
        return hr;
#endif
    }
    catch (...)
    {
        LogDebug(L"QueryContextMenu: Exception caught");
        return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
    }
}

IFACEMETHODIMP CSortBySchlongExtension::InvokeCommand(LPCMINVOKECOMMANDINFO pici)
{
    try
    {
        wchar_t debugMsg[512];
        
        if (pici == nullptr)
        {
            LogDebug(L"InvokeCommand: ENTRY - pici is null, returning E_INVALIDARG");
            return E_INVALIDARG;
        }

        swprintf_s(debugMsg, L"InvokeCommand: ENTRY - pici=%p, cbSize=%u, fMask=0x%X, hwnd=%p, lpVerb=%p, lpParameters=%p, lpDirectory=%p, nShow=%d, dwHotKey=%lu, hIcon=%p, m_commandIdFirst=%u, m_commandIdCount=%u",
                   pici, pici->cbSize, pici->fMask, pici->hwnd, pici->lpVerb, pici->lpParameters, pici->lpDirectory, 
                   pici->nShow, pici->dwHotKey, pici->hIcon, m_commandIdFirst, m_commandIdCount);
        LogDebug(debugMsg);

        // We only handle numeric command IDs (not string verbs)
        if (HIWORD(pici->lpVerb) != 0)
        {
            LogDebug(L"InvokeCommand: String verb not supported, returning E_FAIL");
            return E_FAIL;
        }

        // Get command ID
        UINT commandId = LOWORD(pici->lpVerb);
        swprintf_s(debugMsg, L"InvokeCommand: Command ID=%u", commandId);
        LogDebug(debugMsg);

        // Validate command ID is within our reserved range
        if (m_commandIdCount == 0)
        {
            LogDebug(L"InvokeCommand: No commands registered, returning E_FAIL");
            return E_FAIL;
        }
        
        if (commandId < m_commandIdFirst)
        {
            swprintf_s(debugMsg, L"InvokeCommand: Command ID %u < m_commandIdFirst %u, returning E_FAIL", 
                       commandId, m_commandIdFirst);
            LogDebug(debugMsg);
            return E_FAIL;
        }

        // Calculate relative offset from our first command ID
        UINT relativeId = commandId - m_commandIdFirst;
        swprintf_s(debugMsg, L"InvokeCommand: Relative ID=%u", relativeId);
        LogDebug(debugMsg);
        
        // Validate relative ID is within our command count
        if (relativeId >= m_commandIdCount)
        {
            swprintf_s(debugMsg, L"InvokeCommand: Relative ID %u >= m_commandIdCount %u, returning E_FAIL", 
                       relativeId, m_commandIdCount);
            LogDebug(debugMsg);
            return E_FAIL;
        }

        // Map relative command ID to action using enum
        SortBySchlongUI::SortBySchlongCommand command = static_cast<SortBySchlongUI::SortBySchlongCommand>(relativeId);
        swprintf_s(debugMsg, L"InvokeCommand: Command enum value=%d", static_cast<int>(command));
        LogDebug(debugMsg);

        // Dispatch to appropriate handler
        switch (command)
        {
        case SortBySchlongUI::SortBySchlongCommand::PenisLayout:
            LogDebug(L"InvokeCommand: Calling HandlePenisLayout");
            HandlePenisLayout();
            LogDebug(L"InvokeCommand: HandlePenisLayout completed, returning S_OK");
            return S_OK;

        default:
            // Unknown command - fail gracefully
            swprintf_s(debugMsg, L"InvokeCommand: Unknown command ID %u, returning E_FAIL", relativeId);
            LogDebug(debugMsg);
            return E_FAIL;
        }
    }
    catch (...)
    {
        LogDebug(L"InvokeCommand: Exception caught, returning E_FAIL");
        return E_FAIL;
    }
}

IFACEMETHODIMP CSortBySchlongExtension::GetCommandString(UINT_PTR idCmd, UINT uFlags, UINT* /*pwReserved*/, LPSTR pszName, UINT cchMax)
{
    // Log immediately using direct OutputDebugString to catch calls even if object is in bad state
    wchar_t immediateLog[256];
    swprintf_s(immediateLog, L"[TID:%lu] GetCommandString CALLED: idCmd=%llu, uFlags=0x%X\r\n", 
               GetCurrentThreadId(), idCmd, uFlags);
    OutputDebugStringW(immediateLog);
    
    // Wrap everything in try-catch to prevent any crashes
    try
    {
        wchar_t debugMsg[512];
        swprintf_s(debugMsg, L"GetCommandString: ENTRY - idCmd=%llu, uFlags=0x%X, pszName=%p, cchMax=%u, m_commandIdFirst=%u, m_commandIdCount=%u, this=%p",
                   idCmd, uFlags, pszName, cchMax, m_commandIdFirst, m_commandIdCount, this);
        LogDebug(debugMsg);
        
        // Validate buffer first - return early if invalid
        if (pszName == nullptr)
        {
            LogDebug(L"GetCommandString: pszName is null, returning E_INVALIDARG");
            return E_INVALIDARG;
        }
        
        if (cchMax == 0)
        {
            LogDebug(L"GetCommandString: cchMax is 0, returning E_INVALIDARG");
            return E_INVALIDARG;
        }

        // Early return if no commands registered
        if (m_commandIdCount == 0)
        {
            LogDebug(L"GetCommandString: No commands registered, returning E_INVALIDARG");
            return E_INVALIDARG;
        }
        
        // Check if idCmd is a pointer value (submenu handle) - these are typically > 0x10000
        // When using MF_POPUP, Explorer might query the submenu handle itself, not the item command ID
        if (idCmd > 0x10000)
        {
            swprintf_s(debugMsg, L"GetCommandString: idCmd %llu looks like a pointer/submenu handle, returning E_INVALIDARG", idCmd);
            LogDebug(debugMsg);
            return E_INVALIDARG;
        }
        
        // Validate command ID is within our reserved range - do this very defensively
        UINT commandId = static_cast<UINT>(idCmd);
        swprintf_s(debugMsg, L"GetCommandString: Command ID=%u, m_commandIdCount=%u, m_commandIdFirst=%u", 
                   commandId, m_commandIdCount, m_commandIdFirst);
        LogDebug(debugMsg);
        
        // Early return if command ID doesn't match exactly (defensive check)
        // Only accept our exact command ID to avoid any edge cases
        if (commandId != m_commandIdFirst)
        {
            swprintf_s(debugMsg, L"GetCommandString: Command ID %u != m_commandIdFirst %u, returning E_INVALIDARG", 
                       commandId, m_commandIdFirst);
            LogDebug(debugMsg);
            return E_INVALIDARG;
        }

        // Since we only have one command and we've verified commandId == m_commandIdFirst,
        // we know this is PenisLayout (relativeId = 0)
        SortBySchlongUI::SortBySchlongCommand command = SortBySchlongUI::SortBySchlongCommand::PenisLayout;
        swprintf_s(debugMsg, L"GetCommandString: Command is PenisLayout, uFlags=0x%X", uFlags);
        LogDebug(debugMsg);
        switch (uFlags)
        {
        case GCS_VERBA:
            {
                LogDebug(L"GetCommandString: GCS_VERBA requested");
                // Only support verb for PenisLayout command
                if (command != SortBySchlongUI::SortBySchlongCommand::PenisLayout)
                {
                    LogDebug(L"GetCommandString: Command is not PenisLayout, returning E_INVALIDARG");
                    return E_INVALIDARG;
                }
                const char* verb = "penis";
                if (strlen(verb) >= cchMax)
                {
                    swprintf_s(debugMsg, L"GetCommandString: Verb length %zu >= cchMax %u", strlen(verb), cchMax);
                    LogDebug(debugMsg);
                    return E_INVALIDARG;
                }
                strcpy_s(pszName, cchMax, verb);
                LogDebug(L"GetCommandString: GCS_VERBA returning S_OK");
                return S_OK;
            }

        case GCS_VERBW:
            {
                LogDebug(L"GetCommandString: GCS_VERBW requested");
                // Only support verb for PenisLayout command
                if (command != SortBySchlongUI::SortBySchlongCommand::PenisLayout)
                {
                    LogDebug(L"GetCommandString: Command is not PenisLayout, returning E_INVALIDARG");
                    return E_INVALIDARG;
                }
                LPWSTR pszWide = reinterpret_cast<LPWSTR>(pszName);
                size_t textLen = wcslen(SortBySchlongUI::MenuPenisText);
                if (textLen >= cchMax)
                {
                    swprintf_s(debugMsg, L"GetCommandString: MenuPenisText length %zu >= cchMax %u", textLen, cchMax);
                    LogDebug(debugMsg);
                    return E_INVALIDARG;
                }
                wcscpy_s(pszWide, cchMax, SortBySchlongUI::MenuPenisText);
                LogDebug(L"GetCommandString: GCS_VERBW returning S_OK");
                return S_OK;
            }

        case GCS_HELPTEXTA:
            {
                LogDebug(L"GetCommandString: GCS_HELPTEXTA requested");
                // Only support help text for PenisLayout command
                if (command != SortBySchlongUI::SortBySchlongCommand::PenisLayout)
                {
                    LogDebug(L"GetCommandString: Command is not PenisLayout, returning E_INVALIDARG");
                    return E_INVALIDARG;
                }
                const char* helpText = "Arrange desktop icons in a penis shape";
                if (strlen(helpText) >= cchMax)
                {
                    swprintf_s(debugMsg, L"GetCommandString: Help text length %zu >= cchMax %u", strlen(helpText), cchMax);
                    LogDebug(debugMsg);
                    return E_INVALIDARG;
                }
                strcpy_s(pszName, cchMax, helpText);
                LogDebug(L"GetCommandString: GCS_HELPTEXTA returning S_OK");
                return S_OK;
            }

        case GCS_HELPTEXTW:
            {
                LogDebug(L"GetCommandString: GCS_HELPTEXTW requested");
                // Only support help text for PenisLayout command
                if (command != SortBySchlongUI::SortBySchlongCommand::PenisLayout)
                {
                    LogDebug(L"GetCommandString: Command is not PenisLayout, returning E_INVALIDARG");
                    return E_INVALIDARG;
                }
                LPWSTR pszWide = reinterpret_cast<LPWSTR>(pszName);
                const wchar_t* helpText = L"Arrange desktop icons in a penis shape";
                size_t textLen = wcslen(helpText);
                if (textLen >= cchMax)
                {
                    swprintf_s(debugMsg, L"GetCommandString: Help text length %zu >= cchMax %u", textLen, cchMax);
                    LogDebug(debugMsg);
                    return E_INVALIDARG;
                }
                wcscpy_s(pszWide, cchMax, helpText);
                LogDebug(L"GetCommandString: GCS_HELPTEXTW returning S_OK");
                return S_OK;
            }

        case GCS_VALIDATEA:
        case GCS_VALIDATEW:
            LogDebug(L"GetCommandString: GCS_VALIDATE requested, returning S_OK");
            return S_OK;

        default:
            swprintf_s(debugMsg, L"GetCommandString: Unknown uFlags 0x%X, returning E_INVALIDARG", uFlags);
            LogDebug(debugMsg);
            return E_INVALIDARG;
        }
    }
    catch (...)
    {
        // Catch all exceptions to prevent Explorer crashes
        wchar_t exceptionLog[256];
        swprintf_s(exceptionLog, L"[TID:%lu] GetCommandString EXCEPTION caught\r\n", GetCurrentThreadId());
        OutputDebugStringW(exceptionLog);
        LogDebug(L"GetCommandString: Exception caught, returning E_FAIL");
        return E_FAIL;
    }
}

void CSortBySchlongExtension::HandlePenisLayout() const
{
    // Launch the console harness with penis shape
    bool success = ProcessLauncher::LaunchConsoleHarness(L"penis");
    if (!success)
    {
        LogDebug(L"HandlePenisLayout: Failed to launch ConsoleHarness");
        // Fail silently - don't show error to user
    }
}

void CSortBySchlongExtension::LogDebug(const std::wstring& message) const
{
    try
    {
        DWORD threadId = GetCurrentThreadId();
        std::wstring fullMessage = L"[TID:" + std::to_wstring(threadId) + L"] [CSortBySchlongExtension] ";
        fullMessage += message;
        fullMessage += L"\r\n";
        OutputDebugStringW(fullMessage.c_str());
    }
    catch (...)
    {
        // If logging fails, don't crash
        OutputDebugStringW(L"[CSortBySchlongExtension] LogDebug: Exception in logging\r\n");
    }
}

