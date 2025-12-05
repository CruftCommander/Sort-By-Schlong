# Shell Extension Conflict Detection Scanner

## Overview

The `detect_shell_extensions.ps1` script enumerates all registered context menu handlers for the desktop background and identifies potential conflicts that might interfere with SortBySchlong or cause Explorer crashes.

## Usage

### Basic Usage

Run the script from PowerShell (as Administrator for best results):

```powershell
.\tools\detect_shell_extensions.ps1
```

### With Custom Output Directory

```powershell
.\tools\detect_shell_extensions.ps1 -OutputDir "C:\Reports"
```

### Verbose Output

```powershell
.\tools\detect_shell_extensions.ps1 -Verbose
```

## Output Files

The script generates two report files:

1. **`shell_extensions_report.txt`** - Human-readable text report
2. **`shell_extensions_report.csv`** - Machine-readable CSV for analysis

Both files are created in the current directory (or specified `-OutputDir`).

## What It Scans

The script examines:

- **HKCU Context Menu Handlers**: User-specific extensions
- **HKLM Context Menu Handlers**: System-wide extensions
- **CLSID Registrations**: Extension class information
- **DLL Locations**: Where extension DLLs are located
- **Architecture**: x86, x64, or ARM64
- **Threading Model**: Apartment, Free, etc.

## Detected Conflicts

The scanner identifies several types of conflicts:

### High Severity

1. **Duplicate CLSID**: Same CLSID registered multiple times
   - Can cause unpredictable behavior
   - May indicate corrupted registry

2. **Missing DLL**: Registered extension but DLL file doesn't exist
   - Will cause Explorer to fail loading the extension
   - Can cause crashes or error dialogs

### Medium Severity

3. **Architecture Mismatch**: x86 extension in x64 Explorer
   - Modern Windows 10+ uses 64-bit Explorer
   - x86 extensions may not load or may cause issues

4. **Missing ThreadingModel**: No threading model specified
   - Can cause COM initialization failures
   - May lead to crashes

## Interpreting Results

### Text Report Structure

```
Shell Extension Conflict Detection Report
Generated: 2025-12-05 15:30:00
================================================

CONTEXT MENU HANDLERS
=====================

Extension: SortBySchlong
  CLSID: {A8B3C4D5-E6F7-4A8B-9C0D-1E2F3A4B5C6D}
  Registration: HKCU
  CLSID Location: HKCU
  DLL Path: C:\...\SortBySchlong.Shell.dll
  DLL Exists: True
  Architecture: x64
  Threading Model: Apartment
  Description: SortBySchlong Shell Extension

CONFLICTS DETECTED
==================

[High] Missing DLL
  SomeExtension: DLL not found at C:\Path\Missing.dll
```

### CSV Report Columns

- **Name**: Extension handler name
- **CLSID**: Class identifier (GUID)
- **RegistrationLocation**: HKCU or HKLM
- **DLLPath**: Full path to extension DLL
- **ThreadingModel**: COM threading model
- **Description**: Extension description
- **CLSIDLocation**: Where CLSID is registered
- **Architecture**: DLL architecture (x86/x64/ARM64)
- **DLLExists**: Whether DLL file exists

## Common Issues and Solutions

### Issue: Multiple Extensions with Same CLSID

**Symptom**: Duplicate CLSID detected

**Solution**:
1. Identify which registration is correct
2. Remove duplicate entries
3. Restart Explorer

### Issue: Missing DLL

**Symptom**: Extension registered but DLL not found

**Solution**:
1. Unregister the extension: `regsvr32 /u "path\to\old.dll"`
2. Remove registry entries manually if needed
3. Restart Explorer

### Issue: Architecture Mismatch

**Symptom**: x86 extension in x64 system

**Solution**:
1. Check if x64 version exists
2. Unregister x86 version
3. Register x64 version if available
4. If only x86 exists, may need to disable or find alternative

### Issue: Explorer Crashes

**Symptom**: Explorer crashes when opening context menu

**Troubleshooting Steps**:
1. Run the scanner to identify all extensions
2. Temporarily disable extensions one by one:
   - Rename the handler registry key (add `.bak` suffix)
   - Restart Explorer
   - Test if crash still occurs
3. Identify the problematic extension
4. Unregister or update the problematic extension

## Disabling Extensions for Testing

To temporarily disable an extension for testing:

### Method 1: Rename Registry Key

```powershell
# Backup and disable
Rename-Item "HKCU:\Software\Classes\Directory\Background\shellex\ContextMenuHandlers\ExtensionName" "ExtensionName.disabled"

# Restore
Rename-Item "HKCU:\Software\Classes\Directory\Background\shellex\ContextMenuHandlers\ExtensionName.disabled" "ExtensionName"
```

### Method 2: Remove Default Value

```powershell
# Disable
Remove-ItemProperty "HKCU:\Software\Classes\Directory\Background\shellex\ContextMenuHandlers\ExtensionName" -Name "(default)"

# Re-enable (restore CLSID value)
Set-ItemProperty "HKCU:\Software\Classes\Directory\Background\shellex\ContextMenuHandlers\ExtensionName" -Name "(default)" -Value "{CLSID-GUID}"
```

**Important**: Always restart Explorer after making registry changes:
- Task Manager → Windows Explorer → Restart
- Or log off/on

## Known Problematic Extensions

Some extensions are known to cause issues:

- **Old antivirus extensions**: May conflict with other extensions
- **Cloud storage extensions** (OneDrive, Dropbox, etc.): Sometimes cause conflicts
- **File manager extensions**: Can interfere with context menu
- **Compression tool extensions**: May have threading issues

## Integration with Troubleshooting

When troubleshooting SortBySchlong crashes:

1. **Run the scanner** to get baseline
2. **Document all extensions** before making changes
3. **Disable extensions one by one** to isolate conflicts
4. **Re-run scanner** after changes to verify
5. **Keep reports** for comparison

## Advanced Usage

### Filtering Results

```powershell
# Import CSV and filter
$extensions = Import-Csv .\shell_extensions_report.csv
$extensions | Where-Object { $_.Architecture -eq "x86" }
$extensions | Where-Object { -not $_.DLLExists }
```

### Comparing Reports

```powershell
# Generate before/after reports
.\detect_shell_extensions.ps1 -OutputDir ".\before"
# ... make changes ...
.\detect_shell_extensions.ps1 -OutputDir ".\after"

# Compare
Compare-Object (Import-Csv .\before\shell_extensions_report.csv) (Import-Csv .\after\shell_extensions_report.csv)
```

## Limitations

- **WOW64 Extensions**: May not detect all 32-bit extensions in all scenarios
- **Dynamic Loading**: Extensions loaded dynamically may not appear
- **Third-party Tools**: Some tools use non-standard registration methods
- **Permissions**: Some registry keys may require elevated permissions

## Requirements

- **PowerShell 5.1+** (Windows 10/11 default)
- **Administrator privileges** (recommended for full access)
- **Registry read access** (minimum)

## Troubleshooting the Scanner

### Issue: "Access Denied" Errors

**Solution**: Run PowerShell as Administrator

### Issue: No Extensions Found

**Possible Causes**:
- No extensions registered
- Registry path doesn't exist
- Permission issues

**Solution**: Check registry manually:
```powershell
Get-ChildItem "HKCU:\Software\Classes\Directory\Background\shellex\ContextMenuHandlers"
```

### Issue: Architecture Detection Fails

**Solution**: DLL may be corrupted or in use. Try:
1. Close Explorer
2. Check if DLL is locked
3. Verify file is valid PE executable

## See Also

- [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - General troubleshooting guide
- [DEBUGGING.md](../docs/DEBUGGING.md) - Debugging techniques
- [SHELL_INTEGRATION.md](../docs/SHELL_INTEGRATION.md) - Extension architecture

