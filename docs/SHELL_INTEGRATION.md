# Shell Integration Documentation

## Overview

This document describes the C++ COM Shell extension implementation that adds a "SortBySchlong" submenu to the Windows desktop context menu. The extension launches the ConsoleHarness.exe to arrange desktop icons.

## Architecture

### Component Overview

```
┌─────────────────────────────────────────┐
│   Windows Explorer (explorer.exe)       │
│                                         │
│  Right-click Desktop → Context Menu    │
│    └─ SortBySchlong → Penis            │
│         └─ (Click)                      │
│              └─ Launch                  │
│                  SortBySchlong.         │
│                  ConsoleHarness.exe     │
│                  --shape=penis          │
└─────────────────────────────────────────┘
```

### COM Extension (C++)

The shell extension is a C++ Win32 DLL (`SortBySchlong.Shell.dll`) implementing:

- **IContextMenu**: Adds menu items to context menu
- **IShellExtInit**: Initializes extension with desktop background PIDL
- **IUnknown**: COM interface requirements
- **IClassFactory**: Creates extension instances

## Project Structure

### Actual Structure

```
SortBySchlong.Shell/
├── DllMain.cpp                      # COM entry points (DllMain, DllGetClassObject, etc.)
├── Guids.h                          # CLSID definitions
├── ClassFactory.h/.cpp              # IClassFactory implementation
├── SortBySchlongExtension.h/.cpp    # Main extension class
├── MenuConstants.h                   # Menu text constants and command enum
├── MenuBuilder.h/.cpp                # Helper for building context menu
├── ProcessLauncher.h/.cpp           # Helper for launching ConsoleHarness.exe
├── SortBySchlong.Shell.def          # DLL export definitions
└── SortBySchlong.Shell.vcxproj      # Visual Studio project file
```

## Implementation Details

### COM Class Structure

The main extension class `CSortBySchlongExtension` implements:

- `IUnknown` - Reference counting
- `IShellExtInit` - Initialization with desktop background PIDL
- `IContextMenu` - Menu item injection and command handling

### Menu Integration

The extension:

1. Detects desktop background context in `Initialize()`
2. Adds a top-level "SortBySchlong" submenu to the context menu (via `MenuBuilder::AddSortBySchlongMenu()`)
3. Populates the submenu with available shapes (currently "Penis")
4. On click, launches `SortBySchlong.ConsoleHarness.exe --shape=penis`

**Important**: The extension does NOT modify the system-owned "Sort by" submenu. Instead, it adds its own branded submenu at the top level of the context menu. This approach is safer, more stable, and follows Windows shell extension best practices.

### Process Launching

The `ProcessLauncher` helper class:

- Gets DLL directory via `GetModuleFileNameW`
- Locates `SortBySchlong.ConsoleHarness.exe` in the same directory
- Launches process with `CreateProcessW` using `CREATE_NO_WINDOW` flag
- Passes `--shape=penis` as command line argument
- Handles errors silently (logs via `OutputDebugStringW`)

## Build Requirements

### Visual Studio

- Visual Studio 2019 or later (with C++ Desktop Development workload)
- Windows 10+ SDK
- Platform Toolset: v143 or later

### Project Configuration

- **Platform**: x64 only (matches 64-bit Explorer on Windows 10+)
- **Configuration**: Debug and Release
- **Runtime Library**: `/MD` (Multi-threaded DLL)
- **Character Set**: Unicode
- **Language Standard**: C++17
- **Warning Level**: `/W4`

### Dependencies

- `shlwapi.lib` - Shell helper APIs
- `ole32.lib` - COM support
- `oleaut32.lib` - OLE automation

### Build Instructions

1. Open `Sort by Schlong.sln` in Visual Studio
2. Select "Release" configuration and "x64" platform
3. Build the solution (or just the `SortBySchlong.Shell` project)
4. DLL will be output to `x64\Release\SortBySchlong.Shell.dll`

## Registration

### Registry Entries

The extension registers in two places:

#### Per-User Context Menu Handler (Recommended for dev/testing)

```
HKEY_CURRENT_USER\Software\Classes\Directory\Background\shellex\ContextMenuHandlers\SortBySchlong
    (Default) = {A8B3C4D5-E6F7-4A8B-9C0D-1E2F3A4B5C6D}
```

#### CLSID Registration

```
HKEY_CLASSES_ROOT\CLSID\{A8B3C4D5-E6F7-4A8B-9C0D-1E2F3A4B5C6D}
    (Default) = "SortBySchlong Shell Extension"
    InprocServer32
        (Default) = "<full DLL path>"
        ThreadingModel = "Apartment"
```

### Registration Methods

#### Automated Registration Scripts

Use the provided scripts in the `tools/` directory:

**Register:**
```batch
tools\register_shell.cmd
```

**Unregister:**
```batch
tools\unregister_shell.cmd
```

These scripts:
- Locate the DLL in `x64\Release\`
- Call `regsvr32` to register/unregister
- Provide error handling

#### Manual Registration

**Register:**
```batch
regsvr32 "x64\Release\SortBySchlong.Shell.dll"
```

**Unregister:**
```batch
regsvr32 /u "x64\Release\SortBySchlong.Shell.dll"
```

### Registration Requirements

- **Per-User**: No elevation required (recommended for development)
- **Per-Machine**: Requires administrator privileges (for system-wide installation)

**Note**: The current implementation uses per-user registration. To switch to per-machine, modify `DllRegisterServer()` in `DllMain.cpp`.

### After Registration

After registering, you must:

1. **Restart Explorer** (or log off/on) for changes to take effect
2. Alternatively, restart Explorer manually:
   - Open Task Manager
   - Find "Windows Explorer"
   - Right-click → Restart

## Deployment

### File Layout

For deployment, place files as follows:

```
<Installation Directory>/
├── SortBySchlong.Shell.dll
└── SortBySchlong.ConsoleHarness.exe
```

**Important**: Both files must be in the same directory. The DLL locates the EXE relative to its own path.

### Installation Steps

1. Build the solution in Release x64 configuration
2. Copy `SortBySchlong.Shell.dll` to installation directory
3. Copy `SortBySchlong.ConsoleHarness.exe` to the same directory
4. Register the DLL using `regsvr32` or the registration script
5. Restart Explorer or log off/on

## Testing

### Testing Checklist

1. **Build Release x64 configuration**
   - Verify DLL builds without errors
   - Check output directory: `x64\Release\SortBySchlong.Shell.dll`

2. **Copy ConsoleHarness.exe**
   - Ensure `SortBySchlong.ConsoleHarness.exe` is in the same directory as the DLL
   - For testing, you can copy from `SortByShlong.ConsoleHarness\bin\Release\net8.0-windows\`

3. **Register DLL**
   - Run `tools\register_shell.cmd` or use `regsvr32`
   - Verify registration succeeds

4. **Restart Explorer**
   - Log off/on, or restart Explorer from Task Manager

5. **Test Menu Appearance**
   - Right-click on desktop (empty area)
   - Look for "SortBySchlong" submenu in the context menu
   - Verify "Penis" menu item appears under "SortBySchlong"
   - Verify the menu does NOT appear in the system "Sort by" submenu

6. **Test Command Invocation**
   - Click "Penis" menu item
   - Verify ConsoleHarness.exe launches (check Task Manager)
   - Verify desktop icons are rearranged

7. **Test Error Handling**
   - Remove ConsoleHarness.exe from directory
   - Click menu item again
   - Verify no error message appears (fails silently)
   - Check DebugView for error logs

8. **Unregister**
   - Run `tools\unregister_shell.cmd` or use `regsvr32 /u`
   - Restart Explorer
   - Verify menu item no longer appears

## Debugging

### DebugView (Recommended)

1. Download [DebugView](https://docs.microsoft.com/en-us/sysinternals/downloads/debugview) from Sysinternals
2. Run DebugView as Administrator
3. Enable "Capture Global Win32" and "Capture Kernel"
4. Right-click desktop to trigger extension
5. View debug output from `OutputDebugStringW` calls

### Attach to Explorer (Visual Studio)

1. Build DLL in **Debug** configuration
2. Start Visual Studio **as Administrator**
3. Build the solution
4. Copy Debug DLL and ConsoleHarness.exe to a test directory
5. Register the DLL (pointing to Debug DLL location)
6. In Visual Studio: **Debug → Attach to Process**
7. Select `explorer.exe` (there may be multiple - choose the main one)
8. Set breakpoints in extension code
9. Right-click desktop to trigger breakpoints

**Note**: After making code changes, you must:
- Rebuild the DLL
- Unregister old DLL
- Register new DLL
- Restart Explorer or re-attach

### Common Debug Output

The extension logs debug messages via `OutputDebugStringW`:

- `[SortBySchlong.Shell] ProcessLauncher: ...` - Process launch messages
- `[CSortBySchlongExtension] ...` - Extension class messages

### Troubleshooting

#### Menu Item Doesn't Appear

1. **Verify registration:**
   - Check registry: `HKCU\Software\Classes\Directory\Background\shellex\ContextMenuHandlers\SortBySchlong`
   - Verify CLSID matches: `{A8B3C4D5-E6F7-4A8B-9C0D-1E2F3A4B5C6D}`

2. **Restart Explorer:**
   - Menu items are cached - must restart Explorer

3. **Check DLL path:**
   - Verify DLL exists at registered path
   - Check for path typos in registry

4. **Check for errors:**
   - Use DebugView to see error messages
   - Check Windows Event Viewer

#### Process Doesn't Launch

1. **Verify EXE location:**
   - ConsoleHarness.exe must be in same directory as DLL
   - Check file name matches exactly: `SortBySchlong.ConsoleHarness.exe`

2. **Check DebugView:**
   - Look for ProcessLauncher error messages
   - Verify path resolution is working

3. **Verify EXE permissions:**
   - Ensure EXE is executable
   - Check antivirus isn't blocking it

#### DLL Loading Errors

1. **Check dependencies:**
   - Use Dependency Walker or `dumpbin /dependents` to verify DLL dependencies
   - Ensure all required DLLs are available

2. **Verify architecture:**
   - DLL must be x64 (Explorer is 64-bit)
   - Check DLL wasn't built for wrong platform

3. **Check registry:**
   - Verify ThreadingModel is "Apartment"
   - Verify DLL path in registry is correct

## Technical Constraints

- **DLL Size**: Must remain small (< 100KB ideal)
- **Performance**: No blocking operations, no disk I/O in menu code
- **Reliability**: Never crash Explorer - all exceptions are caught
- **Compatibility**: Windows 10+ only (64-bit Explorer)

## Engineering Practices

- **RAII**: All handles and memory use RAII patterns
- **Unicode**: All strings are wide-character (`wchar_t`, `std::wstring`)
- **Exception Safety**: No exceptions escape COM methods
- **Defensive Programming**: All pointers validated, buffer sizes checked
- **Thread Safety**: Reference counting uses Interlocked operations

## Future Enhancements

### Additional Shapes

- Add more shapes to the SortBySchlong submenu (e.g., "Stealth Mode", "Custom Shape...")
- Shapes are defined in `MenuBuilder.cpp` using the `ShapeMenuItem` array
- Simply add entries to the array and update `CommandCount` in `MenuConstants.h`

### Dynamic Shape Selection

- Query ConsoleHarness for available shapes via `--list-shapes`
- Dynamically populate submenu based on available shapes

### Localization

- Support localized menu text
- Menu text constants are centralized in `MenuConstants.h` for easy localization

### Configuration

- Allow user to configure default shape
- Store preferences in registry

### Progress Feedback

- Show progress dialog during icon arrangement
- Use COM interfaces for status updates

## References

- [Microsoft: Creating Shell Extension Handlers](https://docs.microsoft.com/en-us/windows/win32/shell/context-menu-handlers)
- [CodeProject: Windows Shell Extension Tutorial](https://www.codeproject.com/Articles/840/How-to-implement-IContextMenu-interface)
- [Microsoft: Shell Extension Guidelines](https://docs.microsoft.com/en-us/windows/win32/shell/shell-exts-overview)
