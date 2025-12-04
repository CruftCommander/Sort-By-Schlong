# Shell Integration Documentation

## Overview

This document describes the planned C++ COM Shell extension integration for adding a context menu item to the Windows desktop that triggers icon arrangement.

## Architecture

### Component Overview

```
┌─────────────────────────────────────────┐
│   Windows Explorer (explorer.exe)       │
│                                         │
│  Right-click Desktop → Context Menu    │
│    └─ Sort by → Penis                   │
│         └─ (Click)                      │
│              └─ Launch                  │
│                  ConsoleHarness.exe     │
│                  --shape=penis          │
└─────────────────────────────────────────┘
```

### COM Extension (C++)

A C++ DLL implementing Windows Shell extension interfaces:

- **IContextMenu**: Adds menu items to context menu
- **IShellExtInit**: Initializes extension with selected items
- **IUnknown**: COM interface requirements

## Project Structure

### Proposed Structure

```
SortBySchlong.Shell/
├── IconArrangerShellExt.h
├── IconArrangerShellExt.cpp
├── IconArrangerShellExt.def
├── IconArrangerShellExt.rc
├── resource.h
└── SortBySchlong.Shell.vcxproj
```

## Implementation Details

### COM Interface Implementation

```cpp
// IconArrangerShellExt.h
class IconArrangerShellExt : public IContextMenu, public IShellExtInit, public IUnknown
{
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void **ppv);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // IShellExtInit
    IFACEMETHODIMP Initialize(PCIDLIST_ABSOLUTE pidlFolder, 
                              IDataObject *pdtobj, 
                              HKEY hkeyProgID);

    // IContextMenu
    IFACEMETHODIMP QueryContextMenu(HMENU hmenu, 
                                    UINT indexMenu, 
                                    UINT idCmdFirst, 
                                    UINT idCmdLast, 
                                    UINT uFlags);
    IFACEMETHODIMP InvokeCommand(LPCMINVOKECOMMANDINFO pici);
    IFACEMETHODIMP GetCommandString(UINT_PTR idCmd, 
                                    UINT uFlags, 
                                    UINT *pwReserved, 
                                    LPSTR pszName, 
                                    UINT cchMax);

private:
    ULONG m_cRef;
    bool m_bIsDesktop;
};
```

### Menu Integration

The extension will:

1. Detect if context menu is being shown on desktop
2. Add menu item under "Sort by" submenu
3. On click, launch `ConsoleHarness.exe` with appropriate shape parameter

### Process Launch

```cpp
void LaunchConsoleHarness(const std::string& shapeKey)
{
    // Get path to ConsoleHarness.exe
    wchar_t exePath[MAX_PATH];
    GetModuleFileNameW(NULL, exePath, MAX_PATH);
    // ... construct path to ConsoleHarness.exe
    
    // Build command line
    std::wstring cmdLine = L"\"";
    cmdLine += exePath;
    cmdLine += L"\" --shape=";
    cmdLine += shapeKeyWide;
    
    // Launch process
    STARTUPINFOW si = { sizeof(si) };
    PROCESS_INFORMATION pi;
    CreateProcessW(NULL, cmdLine.data(), NULL, NULL, FALSE, 
                   0, NULL, NULL, &si, &pi);
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);
}
```

## Registration

### Registry Entries

The extension must be registered in Windows Registry:

```
HKEY_CLASSES_ROOT
└── DesktopBackground
    └── ShellEx
        └── ContextMenuHandlers
            └── IconArranger
                (Default) = {CLSID}
```

### CLSID Registration

```
HKEY_CLASSES_ROOT
└── CLSID
    └── {CLSID}
        ├── InprocServer32
        │   ├── (Default) = path\to\IconArranger.Shell.dll
        │   └── ThreadingModel = Apartment
```

### Registration Methods

#### Manual Registration

```batch
regsvr32 IconArranger.Shell.dll
```

#### Unregistration

```batch
regsvr32 /u IconArranger.Shell.dll
```

#### Automated Registration

Create installer or setup script that:
1. Copies DLL to appropriate location (e.g., Program Files)
2. Copies ConsoleHarness.exe to same location
3. Registers DLL via regsvr32 or direct registry writes
4. Handles uninstallation

## Integration with C# Codebase

### Executable Location

The C++ extension needs to locate the C# ConsoleHarness.exe:

**Option 1**: Install both to same directory
- DLL and EXE in same folder
- Use relative path or GetModuleFileName

**Option 2**: Registry-based path
- Store installation path in registry during install
- Read from registry at runtime

**Option 3**: Environment variable
- Set environment variable during install
- Read at runtime

### Communication

- **No direct communication needed**: Extension launches EXE as separate process
- **Parameters**: Pass shape key via command line arguments
- **Return codes**: Extension can check process exit code if needed

## Debugging

### Attach to Explorer

1. Start Visual Studio as Administrator
2. Debug → Attach to Process
3. Select `explorer.exe`
4. Set breakpoints in extension code
5. Right-click desktop to trigger breakpoints

### Logging

- Use OutputDebugString for debug output
- View in DebugView (Sysinternals) or Visual Studio Output window
- Consider file logging for production debugging

### Testing

1. Build DLL in Debug configuration
2. Register DLL
3. Restart Explorer (or log off/on)
4. Right-click desktop
5. Verify menu item appears
6. Click and verify ConsoleHarness launches

## Build Requirements

### Visual Studio

- Visual Studio 2019 or later
- Windows SDK (latest)
- C++ Desktop Development workload

### Project Configuration

- Platform: x64 (match C# application)
- Configuration: Release for deployment
- Runtime: Static linking recommended (or include redistributables)

### Dependencies

- Windows Shell API headers (shlobj.h, shlguid.h)
- COM interfaces (objbase.h)

## Deployment Considerations

### 32-bit vs 64-bit

- Windows 10+ uses 64-bit Explorer
- Extension must match Explorer architecture (64-bit)
- C# application should also be 64-bit

### Versioning

- Version DLL appropriately
- Handle updates (unregister old, register new)
- Consider side-by-side installation for testing

### Security

- Code signing recommended for production
- Handle elevation requirements if needed
- Validate paths before launching executables

## Future Enhancements

### Dynamic Shape Selection

- Submenu with all available shapes
- Query ConsoleHarness for available shapes (via stdout or config file)

### Progress Feedback

- Show progress dialog during arrangement
- Use COM interfaces for status updates

### Configuration

- Allow user to configure default shape
- Store preferences in registry or config file

## References

- [Microsoft: Creating Shell Extension Handlers](https://docs.microsoft.com/en-us/windows/win32/shell/context-menu-handlers)
- [CodeProject: Windows Shell Extension Tutorial](https://www.codeproject.com/Articles/840/How-to-implement-IContextMenu-interface)
