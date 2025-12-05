# Shell Extension Troubleshooting Guide

## Issue: Menu Item Not Appearing After Registration

### Problem
After registering the shell extension and restarting Explorer, the "Sort by → Penis" menu item does not appear.

### Common Causes

1. **CLSID Not Registered Properly**
   - The CLSID must be registered for Explorer to load the DLL
   - Check: `reg query "HKCU\Software\Classes\CLSID\{A8B3C4D5-E6F7-4A8B-9C0D-1E2F3A4B5C6D}"`

2. **DLL Not Loading**
   - Check Windows Event Viewer for DLL loading errors
   - Use DebugView to see debug output from the extension

3. **Menu Item Not Found**
   - The extension searches for "Sort by" menu in English
   - On localized systems, this might fail silently

### Steps to Fix

1. **Unregister the old DLL:**
   ```
   tools\unregister_shell.cmd
   ```

2. **Rebuild the DLL** in Visual Studio (Release x64 or Debug x64)

3. **Re-register the new DLL:**
   ```
   tools\register_shell.cmd
   ```

4. **Verify Registry Entries:**
   ```cmd
   reg query "HKCU\Software\Classes\CLSID\{A8B3C4D5-E6F7-4A8B-9C0D-1E2F3A4B5C6D}"
   reg query "HKCU\Software\Classes\Directory\Background\shellex\ContextMenuHandlers\SortBySchlong"
   ```

5. **Check Debug Output:**
   - Download DebugView from Sysinternals
   - Run as Administrator
   - Enable "Capture Global Win32"
   - Right-click desktop
   - Look for messages starting with `[CSortBySchlongExtension]`

6. **Restart Explorer:**
   - Open Task Manager
   - Find "Windows Explorer"
   - Right-click → Restart

### Verification Checklist

- [ ] CLSID is registered under `HKCU\Software\Classes\CLSID\{A8B3C4D5-E6F7-4A8B-9C0D-1E2F3A4B5C6D}`
- [ ] Context menu handler is registered
- [ ] DLL path in registry points to correct location
- [ ] ThreadingModel is set to "Apartment"
- [ ] Explorer was restarted after registration
- [ ] DebugView shows extension being called (if enabled)

