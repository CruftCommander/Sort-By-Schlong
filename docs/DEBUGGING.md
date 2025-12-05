# Debugging Guide for SortBySchlong Shell Extension

This guide provides instructions for debugging Explorer crashes and other issues with the shell extension.

## Table of Contents

1. [Enhanced Logging](#enhanced-logging)
2. [WinDbg Setup](#windbg-setup)
3. [Application Verifier](#application-verifier)
4. [Process Monitor](#process-monitor)
5. [GFlags](#gflags)
6. [Simplified Test Version](#simplified-test-version)
7. [Common Issues](#common-issues)

## Enhanced Logging

The extension now includes comprehensive logging to all COM methods. All logs are sent to `OutputDebugStringW` and can be viewed with DebugView.

### Viewing Logs

1. **Download DebugView** from [Sysinternals](https://docs.microsoft.com/en-us/sysinternals/downloads/debugview)
2. **Run DebugView as Administrator**
3. **Enable capture:**
   - Check "Capture Global Win32"
   - Check "Capture Kernel" (optional, for system-level messages)
4. **Filter logs:**
   - Use "Edit → Filter/Highlight" to filter for `[CSortBySchlongExtension]`
   - Or search for "SortBySchlong" in the log

### Log Format

Logs follow this format:
```
[TID:12345] [CSortBySchlongExtension] [Method] Message
```

- **TID**: Thread ID where the log was generated
- **Method**: COM method name (Initialize, QueryContextMenu, GetCommandString, etc.)
- **Message**: Detailed information about the operation

### What Gets Logged

- **Initialize**: All parameters, desktop background detection
- **QueryContextMenu**: Menu handle validation, submenu creation, command ID tracking
- **GetCommandString**: Every call with idCmd, uFlags, buffer validation
- **InvokeCommand**: Command ID validation, handler dispatch
- **Destructor**: Object state at destruction

## WinDbg Setup

WinDbg is the Windows debugger that can attach to explorer.exe and catch crashes.

### Prerequisites

1. Install **Windows SDK** (includes WinDbg)
2. Or download **WinDbg Preview** from Microsoft Store

### Attaching to Explorer

1. **Start WinDbg as Administrator**

2. **Attach to explorer.exe:**
   ```
   File → Attach to Process
   ```
   - Find `explorer.exe` in the list
   - Click "Attach"

3. **Load Symbols:**
   ```
   .symfix
   .reload
   ```

4. **Set Breakpoints** (optional):
   ```
   bp SortBySchlong!CSortBySchlongExtension::QueryContextMenu
   bp SortBySchlong!CSortBySchlongExtension::GetCommandString
   ```

5. **Enable Exception Handling:**
   ```
   sxe av
   sxe c0000005
   ```
   - `sxe av`: Stop on access violations
   - `sxe c0000005`: Stop on specific exception code

6. **Continue execution:**
   ```
   g
   ```

7. **When crash occurs:**
   - WinDbg will break
   - Use `k` to see stack trace
   - Use `!analyze -v` for detailed analysis
   - Use `.dump /ma crash.dmp` to save crash dump

### Useful WinDbg Commands

- `k` - Stack trace
- `!analyze -v` - Analyze exception
- `dv` - Display local variables
- `dt` - Display type information
- `.dump /ma filename.dmp` - Create full memory dump
- `g` - Continue execution
- `q` - Quit debugger

## Application Verifier

Application Verifier detects heap corruption, handle leaks, and other memory issues.

### Setup

1. **Download Application Verifier** (part of Windows SDK or standalone)

2. **Run Application Verifier as Administrator**

3. **Add explorer.exe:**
   - Click "Add Application"
   - Browse to `C:\Windows\explorer.exe`
   - Click "Save"

4. **Enable Checks:**
   - **Heaps**: Detects heap corruption
   - **Handles**: Detects handle leaks
   - **Locks**: Detects deadlocks
   - **Exceptions**: Enhanced exception tracking

5. **Restart Explorer:**
   - Task Manager → Windows Explorer → Restart
   - Or log off/on

6. **View Results:**
   - Application Verifier will log issues
   - Check Event Viewer for Application Verifier logs

### Disabling

1. Open Application Verifier
2. Select explorer.exe
3. Click "Remove Application"
4. Restart Explorer

## Process Monitor

Process Monitor (ProcMon) shows file, registry, and process activity.

### Setup

1. **Download Process Monitor** from [Sysinternals](https://docs.microsoft.com/en-us/sysinternals/downloads/procmon)

2. **Run as Administrator**

3. **Set Filters:**
   - **Process Name** is `explorer.exe`
   - **Path** contains `SortBySchlong`
   - Or filter by **Operation** (RegQueryValue, CreateFile, etc.)

4. **Capture:**
   - Click the magnifying glass to start/stop capture
   - Right-click context menu to trigger extension

5. **Analyze:**
   - Look for failed operations (red entries)
   - Check registry access patterns
   - Verify DLL loading

## GFlags

GFlags enables page heap and other debugging features.

### Setup

1. **Run GFlags** (part of Debugging Tools for Windows)

2. **Select "Image File" tab**

3. **Enter image name:** `explorer.exe`

4. **Enable flags:**
   - **Page Heap**: `+hpa` (full page heap) or `+hpg` (normal page heap)
   - **Stack Traces**: Enable for heap allocations

5. **Click "Apply"**

6. **Restart Explorer**

### Disabling

1. Open GFlags
2. Select explorer.exe
3. Click "Remove"
4. Restart Explorer

**Warning**: Page heap can significantly slow down Explorer. Use only when debugging.

## Simplified Test Version

A simplified test version is available to isolate if the crash is submenu-specific.

### Enabling Simplified Version

1. **Open** `SortBySchlong.Shell/SortBySchlongExtension.cpp`

2. **Change** the `SIMPLE_MENU_TEST` definition:
   ```cpp
   #define SIMPLE_MENU_TEST 1  // Enable simplified version
   ```

3. **Rebuild** the project

4. **Register** and test

### What It Does

- **Normal version**: Creates "SortBySchlong" submenu with "Penis" item
- **Simplified version**: Creates single direct menu item "SortBySchlong - Penis"

### Interpreting Results

- **If simplified version works**: Issue is likely submenu-specific
- **If simplified version also crashes**: Issue is more fundamental (menu creation, command IDs, etc.)

## Common Issues

### Explorer Crashes on Menu Hover

**Symptoms**: Explorer crashes when hovering over "SortBySchlong" menu

**Debugging Steps**:
1. Check DebugView for `GetCommandString` calls
2. Verify command ID range is correct
3. Check if buffer validation is failing
4. Use WinDbg to catch the exact crash point

**Possible Causes**:
- Invalid command ID passed to `GetCommandString`
- Buffer overflow in string operations
- Menu handle becomes invalid
- Submenu ownership issues

### Explorer Crashes on Menu Close

**Symptoms**: Explorer crashes when closing context menu

**Debugging Steps**:
1. Check if destructor is being called
2. Verify no double-free of menu handles
3. Check for use-after-free of menu handles
4. Use Application Verifier to detect heap corruption

**Possible Causes**:
- Menu handle cleanup issues
- Object lifetime problems
- Heap corruption

### Menu Item Doesn't Appear

**Symptoms**: Context menu doesn't show "SortBySchlong"

**Debugging Steps**:
1. Check DebugView for `QueryContextMenu` logs
2. Verify `Initialize` detected desktop background
3. Check for early returns in `QueryContextMenu`
4. Verify DLL registration

**Possible Causes**:
- Not detecting desktop background
- Menu creation failing silently
- Command ID out of range
- DLL not properly registered

### Process Doesn't Launch

**Symptoms**: Clicking menu item doesn't launch ConsoleHarness

**Debugging Steps**:
1. Check DebugView for `InvokeCommand` logs
2. Verify command ID matching
3. Check ProcessLauncher logs
4. Verify ConsoleHarness.exe exists in DLL directory

**Possible Causes**:
- Command ID mismatch
- ConsoleHarness.exe not found
- Process creation failing
- Antivirus blocking

## Tips

1. **Always test with DebugView running** to see what's happening
2. **Use simplified version first** to isolate issues
3. **Check Event Viewer** for system-level errors
4. **Test on clean system** to rule out other extensions interfering
5. **Use Application Verifier** when suspecting memory issues
6. **Create crash dumps** with WinDbg for detailed analysis

## Getting Help

When reporting issues, include:

1. **DebugView logs** (full session)
2. **WinDbg stack trace** (if available)
3. **Application Verifier logs** (if used)
4. **Windows version** and build number
5. **Steps to reproduce**
6. **Whether simplified version works**

## References

- [Microsoft: Debugging Shell Extensions](https://docs.microsoft.com/en-us/windows/win32/shell/debugging-shell-extensions)
- [Sysinternals DebugView](https://docs.microsoft.com/en-us/sysinternals/downloads/debugview)
- [WinDbg Documentation](https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/)
- [Application Verifier](https://docs.microsoft.com/en-us/windows-hardware/drivers/devtest/application-verifier)

