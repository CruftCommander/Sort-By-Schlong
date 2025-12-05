#include "SortBySchlongExtension.h"
#include "ProcessLauncher.h"
#include "MenuBuilder.h"
#include "MenuConstants.h"
#include <shlwapi.h>
#include <vector>
#include <string>

#pragma comment(lib, "shlwapi.lib")

CSortBySchlongExtension::CSortBySchlongExtension()
    : m_cRef(1)
    , m_commandIdFirst(0)
    , m_commandIdCount(0)
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
    m_commandIdFirst = 0;
    m_commandIdCount = 0;
    
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

    // Only show menu on desktop background
    if (!m_isDesktopBackground)
    {
        return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
    }

    // Reset command tracking
    m_commandIdFirst = 0;
    m_commandIdCount = 0;

    try
    {
        LogDebug(L"QueryContextMenu called");
        
        // Validate inputs
        if (hmenu == nullptr || !IsMenu(hmenu))
        {
            LogDebug(L"QueryContextMenu: Invalid menu handle");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        // Validate command ID is within valid range (must be < 0x8000)
        if (idCmdFirst >= 0x8000)
        {
            LogDebug(L"QueryContextMenu: Command ID out of range");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Create submenu
        HMENU hSubMenu = CreatePopupMenu();
        if (hSubMenu == nullptr || !IsMenu(hSubMenu))
        {
            LogDebug(L"QueryContextMenu: Failed to create submenu");
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Add "Penis" item to submenu with explicit string copy
        UINT currentId = idCmdFirst;
        const wchar_t* penisText = SortBySchlongUI::MenuPenisText;
        if (penisText == nullptr || wcslen(penisText) == 0)
        {
            LogDebug(L"QueryContextMenu: Invalid menu text");
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        if (!AppendMenuW(hSubMenu, MF_STRING, currentId, penisText))
        {
            DWORD error = GetLastError();
            wchar_t errorMsg[256];
            swprintf_s(errorMsg, L"QueryContextMenu: Failed to append menu item, error %lu", error);
            LogDebug(errorMsg);
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Verify submenu has the item
        int itemCount = GetMenuItemCount(hSubMenu);
        if (itemCount != 1)
        {
            LogDebug(L"QueryContextMenu: Submenu item count mismatch");
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }

        // Add submenu to main context menu with explicit string copy
        const wchar_t* rootText = SortBySchlongUI::MenuRootText;
        if (rootText == nullptr || wcslen(rootText) == 0)
        {
            LogDebug(L"QueryContextMenu: Invalid root menu text");
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        if (!AppendMenuW(hmenu, MF_STRING | MF_POPUP, reinterpret_cast<UINT_PTR>(hSubMenu), rootText))
        {
            DWORD error = GetLastError();
            wchar_t errorMsg[256];
            swprintf_s(errorMsg, L"QueryContextMenu: Failed to append submenu, error %lu", error);
            LogDebug(errorMsg);
            DestroyMenu(hSubMenu);
            return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 0);
        }
        
        // Windows now owns hSubMenu - don't destroy it

        // Store command ID range (only the items in the submenu, not the submenu itself)
        m_commandIdFirst = idCmdFirst;
        m_commandIdCount = 1;
        
        LogDebug(L"QueryContextMenu: Successfully added menu with 1 command(s)");

        // Return number of command IDs consumed (items in submenu)
        return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, 1);
    }
    catch (...)
    {
        LogDebug(L"QueryContextMenu: Exception caught");
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
        // We only handle numeric command IDs (not string verbs)
        if (HIWORD(pici->lpVerb) != 0)
        {
            // Command is invoked by verb string - we don't use verbs, so fail
            return E_FAIL;
        }

        // Get command ID
        UINT commandId = LOWORD(pici->lpVerb);

        // Validate command ID is within our reserved range
        if (m_commandIdCount == 0 || commandId < m_commandIdFirst)
        {
            return E_FAIL;
        }

        // Calculate relative offset from our first command ID
        UINT relativeId = commandId - m_commandIdFirst;
        
        // Validate relative ID is within our command count
        if (relativeId >= m_commandIdCount)
        {
            return E_FAIL;
        }

        // Map relative command ID to action using enum
        SortBySchlongUI::SortBySchlongCommand command = static_cast<SortBySchlongUI::SortBySchlongCommand>(relativeId);

        // Dispatch to appropriate handler
        switch (command)
        {
        case SortBySchlongUI::SortBySchlongCommand::PenisLayout:
            HandlePenisLayout();
            return S_OK;

        default:
            // Unknown command - fail gracefully
            LogDebug(L"InvokeCommand: Unknown command ID " + std::to_wstring(relativeId));
            return E_FAIL;
        }
    }
    catch (...)
    {
        return E_FAIL;
    }
}

IFACEMETHODIMP CSortBySchlongExtension::GetCommandString(UINT_PTR idCmd, UINT uFlags, UINT* /*pwReserved*/, LPSTR pszName, UINT cchMax)
{
    // Wrap everything in try-catch to prevent any crashes
    try
    {
        // Validate buffer first - return early if invalid
        if (pszName == nullptr || cchMax == 0)
        {
            return E_INVALIDARG;
        }

        // Validate command ID is within our reserved range
        if (m_commandIdCount == 0)
        {
            // Don't log here - might be called during menu cleanup
            return E_INVALIDARG;
        }

        UINT commandId = static_cast<UINT>(idCmd);
        if (commandId < m_commandIdFirst || commandId >= m_commandIdFirst + m_commandIdCount)
        {
            // Command ID not ours - return invalid (don't log to avoid noise)
            return E_INVALIDARG;
        }

        // Calculate relative command ID
        UINT relativeId = commandId - m_commandIdFirst;
        
        // Validate relative ID is within enum range
        if (relativeId >= static_cast<UINT>(SortBySchlongUI::CommandCount))
        {
            return E_INVALIDARG;
        }
        
        SortBySchlongUI::SortBySchlongCommand command = static_cast<SortBySchlongUI::SortBySchlongCommand>(relativeId);
        switch (uFlags)
        {
        case GCS_VERBA:
            {
                // Only support verb for PenisLayout command
                if (command != SortBySchlongUI::SortBySchlongCommand::PenisLayout)
                {
                    return E_INVALIDARG;
                }
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
                // Only support verb for PenisLayout command
                if (command != SortBySchlongUI::SortBySchlongCommand::PenisLayout)
                {
                    return E_INVALIDARG;
                }
                LPWSTR pszWide = reinterpret_cast<LPWSTR>(pszName);
                if (wcslen(SortBySchlongUI::MenuPenisText) >= cchMax)
                {
                    return E_INVALIDARG;
                }
                wcscpy_s(pszWide, cchMax, SortBySchlongUI::MenuPenisText);
                return S_OK;
            }

        case GCS_HELPTEXTA:
            {
                // Only support help text for PenisLayout command
                if (command != SortBySchlongUI::SortBySchlongCommand::PenisLayout)
                {
                    return E_INVALIDARG;
                }
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
                // Only support help text for PenisLayout command
                if (command != SortBySchlongUI::SortBySchlongCommand::PenisLayout)
                {
                    return E_INVALIDARG;
                }
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
        // Catch all exceptions to prevent Explorer crashes
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
    std::wstring fullMessage = L"[CSortBySchlongExtension] ";
    fullMessage += message;
    fullMessage += L"\r\n";
    OutputDebugStringW(fullMessage.c_str());
}

