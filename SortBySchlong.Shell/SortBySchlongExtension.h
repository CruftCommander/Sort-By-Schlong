#pragma once

#include <windows.h>
#include <shlobj.h>
#include <shlguid.h>
#include <string>

/// <summary>
/// Shell extension that adds "Sort by -> Penis" menu item to desktop context menu.
/// </summary>
class CSortBySchlongExtension : public IShellExtInit, public IContextMenu
{
public:
    CSortBySchlongExtension();
    virtual ~CSortBySchlongExtension();

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    // IShellExtInit
    IFACEMETHODIMP Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* pdtobj, HKEY hkeyProgID) override;

    // IContextMenu
    IFACEMETHODIMP QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags) override;
    IFACEMETHODIMP InvokeCommand(LPCMINVOKECOMMANDINFO pici) override;
    IFACEMETHODIMP GetCommandString(UINT_PTR idCmd, UINT uFlags, UINT* pwReserved, LPSTR pszName, UINT cchMax) override;

    // Non-copyable, non-movable
    CSortBySchlongExtension(const CSortBySchlongExtension&) = delete;
    CSortBySchlongExtension& operator=(const CSortBySchlongExtension&) = delete;
    CSortBySchlongExtension(CSortBySchlongExtension&&) = delete;
    CSortBySchlongExtension& operator=(CSortBySchlongExtension&&) = delete;

private:
    LONG m_cRef;
    UINT m_sortByPenisId;
    bool m_isDesktopBackground;

    void LogDebug(const std::wstring& message) const;
    int FindSortByMenuIndex(HMENU hmenu) const;
};

