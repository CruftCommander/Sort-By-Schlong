# Using DebugView to Debug Shell Extension

## Download DebugView

1. Download DebugView from Microsoft Sysinternals:
   https://docs.microsoft.com/en-us/sysinternals/downloads/debugview

2. Extract and run `Dbgview.exe`

## Setup DebugView

1. **Run DebugView as Administrator** (right-click → Run as administrator)
   - This is required to capture global Win32 debug output

2. **Enable Capture Options:**
   - Check "Capture Global Win32" (in the Capture menu)
   - Check "Capture Kernel" (optional, but helpful)
   - Uncheck "Capture Win32" (we want Global, not regular Win32)

3. **Clear the log** (Edit → Clear Log) to start fresh

## Testing the Extension

1. With DebugView running and capturing, **right-click on the desktop** (empty area, not on an icon)

2. Look for debug messages in DebugView that start with:
   - `[CSortBySchlongExtension]` - Extension class messages
   - `[SortBySchlong.Shell]` - ProcessLauncher messages

## What to Look For

### If you see "Initialize called":
- ✅ The DLL is loading correctly
- ✅ Explorer is calling the extension

### If you see "QueryContextMenu called":
- ✅ The extension is being invoked for the context menu
- Look for subsequent messages about finding the "Sort by" menu

### If you see "Sort by menu not found":
- ❌ The menu search is failing
- This could be due to localization or menu structure differences

### If you see NO messages at all:
- ❌ The DLL might not be loading
- Check Windows Event Viewer for errors
- Verify the DLL path in registry is correct

## Common Issues

### No Messages Appearing
- Make sure DebugView is running as Administrator
- Verify "Capture Global Win32" is enabled
- Try restarting Explorer again after starting DebugView

### DLL Loading Errors
- Check Event Viewer (Windows Logs → Application)
- Look for errors mentioning "SortBySchlong.Shell.dll"
- Verify the DLL exists at the path in registry

