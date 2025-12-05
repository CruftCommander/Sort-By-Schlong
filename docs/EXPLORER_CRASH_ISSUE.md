# Explorer Crash Issue - Comprehensive Documentation

## Issue Summary

**Problem**: Windows Explorer crashes when the SortBySchlong shell extension is registered and the desktop context menu is accessed.

**Severity**: Critical - Prevents normal Explorer operation

**Status**: Under Investigation

**Date First Reported**: December 2025

## Symptoms

### Primary Symptoms

1. **Explorer crashes immediately after right-clicking desktop**
   - Context menu may appear briefly or not at all
   - Explorer process terminates
   - Desktop and taskbar become unresponsive
   - Windows restarts Explorer automatically

2. **Crash occurs before context menu fully appears**
   - No interaction with menu items required
   - Crash happens during menu construction phase

3. **Crash occurs with both menu implementations**
   - Submenu version (normal): Crashes
   - Direct menu item version (simple test): Also crashes
   - Suggests issue is not submenu-specific

### Secondary Symptoms

- Windows Error Reporting (WER) process appears after crash
- Explorer restarts automatically (Windows recovery)
- No error dialogs shown to user
- System remains functional after Explorer restart

## Environment

### Tested Configurations

- **Windows 10** (fully updated, Extended Security Updates)
- **Windows 11 VM** (fresh install, fully updated) - Testing in progress
- **Architecture**: x64 only (Explorer is 64-bit)

### System State

- **sfc /scannow**: Found and repaired corrupt files (before testing)
- **System Updates**: Fully up to date
- **Antivirus**: Not blocking extension
- **Other Extensions**: Multiple shell extensions installed (conflict possible)

## Crash Timeline

Based on debug logs, the crash occurs in this sequence:

1. **Initialize()** called successfully
   - Desktop background detected
   - Extension object created (m_cRef=2, then 1)

2. **QueryContextMenu()** called successfully
   - Menu handle validated
   - Menu item/submenu created
   - Command ID range stored
   - Returns HRESULT 0x00000001 (1 command added)

3. **Extension object remains alive**
   - Reference count = 1 (not destroyed)
   - Object should be available for GetCommandString calls

4. **Crash occurs**
   - No GetCommandString calls logged
   - No InvokeCommand calls logged
   - No destructor called
   - Explorer terminates

## Debugging Evidence

### Logs Analysis

**What We See:**
```
[TID:64892] Initialize: ENTRY ... Desktop background detected
[TID:64892] QueryContextMenu: ENTRY ... 
[TID:64892] QueryContextMenu: EXIT - Successfully added menu
[TID:64892] QueryContextMenu ABOUT TO RETURN: hr=0x00000001
[StartUI.SplitViewFrame]  <- Explorer crash indicator
```

**What We Don't See:**
- No GetCommandString calls (even with immediate logging)
- No InvokeCommand calls
- No destructor calls
- No exception logs from our code

### Process Monitor Evidence

- DLL loads successfully
- Registry queries succeed
- No file access errors
- No permission issues

### Windows Event Viewer

- Check Application log for Event ID 1000, 1001 (Application crashes)
- Check System log for Event ID 7034 (Service crashes)
- Windows Error Reporting events may contain crash dumps

## Potential Root Causes

### 1. Extension Conflict (Most Likely)

**Hypothesis**: Another shell extension conflicts with SortBySchlong

**Evidence**:
- Multiple extensions registered
- Crash occurs during menu construction (when all extensions are queried)
- Both simple and complex menu versions crash (suggests not our menu structure)

**Investigation Steps**:
1. Run `tools\detect_shell_extensions.ps1` to enumerate all extensions
2. Disable extensions one by one
3. Test if crash still occurs with only SortBySchlong enabled

### 2. COM Threading Issue

**Hypothesis**: Threading model mismatch or COM apartment issue

**Current Configuration**:
- ThreadingModel: "Apartment"
- Extension runs in Explorer's main thread
- No explicit threading code

**Investigation Steps**:
1. Verify ThreadingModel in registry
2. Check if other extensions use different threading models
3. Test with different ThreadingModel values (not recommended - may break)

### 3. Memory Corruption

**Hypothesis**: Memory corruption in menu creation or string handling

**Evidence**:
- sfc /scannow found corrupt files (may be unrelated)
- Crash occurs after successful menu creation
- No access violations logged in our code

**Investigation Steps**:
1. Use Application Verifier to detect heap corruption
2. Use WinDbg to analyze crash dump
3. Check for buffer overflows in menu text handling

### 4. Explorer Bug/Incompatibility

**Hypothesis**: Explorer has a bug with certain menu configurations

**Evidence**:
- Crash occurs even with simplest menu item
- No errors in our code
- Crash happens in Explorer's code path

**Investigation Steps**:
1. Test on clean Windows 11 VM
2. Check Windows version-specific issues
3. Report to Microsoft if reproducible on clean system

### 5. DLL Loading Issue

**Hypothesis**: DLL dependencies or loading order issue

**Evidence**:
- DLL loads successfully (Process Monitor confirms)
- No missing dependency errors
- DLL is x64 (correct architecture)

**Investigation Steps**:
1. Check DLL dependencies with Dependency Walker
2. Verify all dependencies are available
3. Check for DLL hijacking vulnerabilities

## Investigation Tools and Methods

### 1. Shell Extension Scanner

**Tool**: `tools\detect_shell_extensions.ps1`

**Purpose**: Identify all registered extensions and potential conflicts

**Usage**:
```powershell
.\tools\detect_shell_extensions.ps1
```

**Output**: Lists all extensions, their CLSIDs, DLL paths, and detects conflicts

### 2. Event Viewer Log Fetcher

**Tool**: `tools\fetch_event_logs.ps1`

**Purpose**: Extract Explorer crash events from Windows Event Log

**Usage**:
```powershell
.\tools\fetch_event_logs.ps1 -HoursBack 24 -IncludeSystem
```

**Output**: Text, CSV, and XML reports of crash events

### 3. DebugView

**Tool**: Sysinternals DebugView

**Purpose**: View real-time debug output from extension

**Usage**:
1. Run DebugView as Administrator
2. Enable "Capture Global Win32"
3. Right-click desktop
4. View logs in real-time

**What to Look For**:
- GetCommandString calls (we're not seeing these)
- Exception messages
- Parameter values

### 4. WinDbg

**Tool**: Windows Debugger

**Purpose**: Attach to explorer.exe and catch crashes

**Limitation**: Explorer becomes unresponsive when WinDbg is attached (known issue)

**Alternative**: Use crash dumps instead

**Usage**:
1. Enable crash dumps (see DEBUGGING.md)
2. Reproduce crash
3. Analyze dump file with WinDbg

### 5. Application Verifier

**Tool**: Application Verifier

**Purpose**: Detect heap corruption, handle leaks, etc.

**Usage**:
1. Install Application Verifier
2. Add explorer.exe
3. Enable: Heaps, Handles, Locks
4. Restart Explorer
5. Reproduce crash
6. Check Event Viewer for Application Verifier logs

### 6. Process Monitor

**Tool**: Sysinternals Process Monitor

**Purpose**: Monitor file, registry, and process activity

**Usage**:
1. Run as Administrator
2. Filter: Process Name = explorer.exe
3. Filter: Path contains SortBySchlong
4. Reproduce crash
5. Analyze captured events

## Troubleshooting Steps

### Step 1: Identify Conflicting Extensions

```powershell
# Run scanner
.\tools\detect_shell_extensions.ps1

# Review report
notepad .\shell_extensions_report.txt
```

**Action**: Document all extensions before making changes

### Step 2: Disable Extensions One by One

```powershell
# Disable an extension (example)
Rename-Item "HKCU:\Software\Classes\Directory\Background\shellex\ContextMenuHandlers\ExtensionName" "ExtensionName.disabled"

# Restart Explorer
Get-Process explorer | Stop-Process
Start-Process explorer

# Test if crash still occurs
# If crash stops, that extension is the conflict
```

**Action**: Test after each disable to identify the conflicting extension

### Step 3: Fetch Crash Logs

```powershell
# Get recent crash events
.\tools\fetch_event_logs.ps1 -HoursBack 48 -IncludeSystem

# Review crash details
notepad .\event_logs_*.txt
```

**Action**: Look for crash dumps, error codes, stack traces

### Step 4: Test on Clean System

1. Set up Windows 11 VM (fresh install)
2. Install only SortBySchlong extension
3. Test if crash occurs
4. If crash doesn't occur, conflict is confirmed
5. If crash still occurs, may be Explorer bug or our code issue

### Step 5: Enable Crash Dumps

1. Enable automatic crash dumps (see DEBUGGING.md)
2. Reproduce crash
3. Analyze dump with WinDbg
4. Get stack trace at crash point

## Known Issues and Workarounds

### Issue: WinDbg Makes Explorer Unresponsive

**Workaround**: Don't attach WinDbg directly. Instead:
1. Enable crash dumps
2. Let Explorer crash naturally
3. Analyze dump file offline

### Issue: Crash Happens Too Fast to Debug

**Workaround**: Use comprehensive logging (already implemented)
- All COM methods log immediately
- Reference counting tracked
- Object lifetime monitored

### Issue: No GetCommandString Calls Logged

**Implication**: Crash occurs before Explorer can query menu information
- Suggests crash in Explorer's menu construction code
- May be conflict with another extension
- May be Explorer bug

## Code Changes Made for Debugging

### Enhanced Logging

- All COM methods log entry/exit with parameters
- Thread IDs included in all logs
- Immediate OutputDebugString at function start
- Reference counting logged

### Simplified Test Version

- Compile-time flag `SIMPLE_MENU_TEST`
- Direct menu item instead of submenu
- Helps isolate if issue is submenu-specific
- **Result**: Both versions crash (not submenu-specific)

### Defensive Programming

- All validation checks added
- Exception handling in all methods
- Buffer size checks
- Null pointer checks

## Next Steps

### Immediate Actions

1. **Run shell extension scanner** to identify all extensions
2. **Disable extensions one by one** to find conflict
3. **Test on clean Windows 11 VM** to rule out system-specific issues
4. **Fetch Event Viewer logs** to get crash details
5. **Enable crash dumps** for detailed analysis

### Long-term Actions

1. **If conflict found**: Document the conflicting extension
2. **If no conflict**: Investigate Explorer compatibility
3. **If Explorer bug**: Report to Microsoft
4. **If our code issue**: Fix based on crash dump analysis

## Related Documentation

- [DEBUGGING.md](DEBUGGING.md) - Debugging techniques and tools
- [SHELL_INTEGRATION.md](SHELL_INTEGRATION.md) - Extension architecture
- [SHELL_EXTENSION_SCANNER.md](../tools/SHELL_EXTENSION_SCANNER.md) - Scanner usage
- [TROUBLESHOOTING.md](../tools/TROUBLESHOOTING.md) - General troubleshooting

## Crash Dump Analysis

When crash dumps are available:

1. **Open in WinDbg**:
   ```
   windbg -z crash.dmp
   ```

2. **Analyze exception**:
   ```
   !analyze -v
   ```

3. **Get stack trace**:
   ```
   k
   ```

4. **Check loaded modules**:
   ```
   lm
   ```

5. **Look for our DLL**:
   ```
   lm m SortBySchlong
   ```

## Reporting the Issue

If issue persists after all troubleshooting:

### Information to Collect

1. **Shell extension scanner report**
2. **Event Viewer logs** (from fetch_event_logs.ps1)
3. **DebugView logs** (full session)
4. **Crash dump** (if available)
5. **Windows version** (winver output)
6. **List of installed shell extensions**
7. **Steps to reproduce** (detailed)

### Where to Report

- **GitHub Issues**: If confirmed bug in our code
- **Microsoft Feedback Hub**: If Explorer bug
- **Stack Overflow**: For community help
- **Windows Developer Forums**: For shell extension experts

## Conclusion

The Explorer crash is a critical issue that prevents normal operation. The crash occurs immediately after QueryContextMenu succeeds, suggesting:

1. **Most likely**: Conflict with another shell extension
2. **Possible**: Explorer bug or incompatibility
3. **Less likely**: Memory corruption in our code (no evidence)

The comprehensive logging and diagnostic tools should help identify the root cause. The next critical step is to test on a clean system and systematically disable other extensions to isolate the conflict.

