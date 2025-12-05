# Shell Extension Conflict Detection Script
# Enumerates all registered context menu handlers and identifies potential conflicts

param(
    [string]$OutputDir = ".",
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

# Output files
$txtOutput = Join-Path $OutputDir "shell_extensions_report.txt"
$csvOutput = Join-Path $OutputDir "shell_extensions_report.csv"

Write-Host "Shell Extension Conflict Detection" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

# Array to store extension information
$extensions = @()

# Function to get registry value safely
function Get-RegistryValue {
    param(
        [string]$Path,
        [string]$Name = "(default)"
    )
    
    try {
        $value = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
        if ($value) {
            return $value.$Name
        }
    }
    catch {
        return $null
    }
    return $null
}

# Function to check if CLSID exists
function Get-CLSIDInfo {
    param(
        [string]$CLSID
    )
    
    $info = @{
        Exists = $false
        DLLPath = $null
        ThreadingModel = $null
        Description = $null
        Location = $null
    }
    
    # Check HKCU
    $hkuPath = "HKCU:\Software\Classes\CLSID\$CLSID"
    if (Test-Path $hkuPath) {
        $info.Exists = $true
        $info.Location = "HKCU"
        $info.DLLPath = Get-RegistryValue -Path "$hkuPath\InprocServer32" -Name "(default)"
        $info.ThreadingModel = Get-RegistryValue -Path "$hkuPath\InprocServer32" -Name "ThreadingModel"
        $info.Description = Get-RegistryValue -Path $hkuPath -Name "(default)"
    }
    
    # Check HKLM (only if not found in HKCU, or to get more info)
    if (-not $info.Exists) {
        $hklmPath = "HKLM:\Software\Classes\CLSID\$CLSID"
        if (Test-Path $hklmPath) {
            $info.Exists = $true
            $info.Location = "HKLM"
            $info.DLLPath = Get-RegistryValue -Path "$hklmPath\InprocServer32" -Name "(default)"
            $info.ThreadingModel = Get-RegistryValue -Path "$hklmPath\InprocServer32" -Name "ThreadingModel"
            $info.Description = Get-RegistryValue -Path $hklmPath -Name "(default)"
        }
    }
    
    # Also check WOW64 node for 32-bit extensions
    if (-not $info.Exists) {
        $wow64Path = "HKLM:\Software\WOW6432Node\Classes\CLSID\$CLSID"
        if (Test-Path $wow64Path) {
            $info.Exists = $true
            $info.Location = "HKLM (WOW64)"
            $info.DLLPath = Get-RegistryValue -Path "$wow64Path\InprocServer32" -Name "(default)"
            $info.ThreadingModel = Get-RegistryValue -Path "$wow64Path\InprocServer32" -Name "ThreadingModel"
            $info.Description = Get-RegistryValue -Path $wow64Path -Name "(default)"
        }
    }
    
    return $info
}

# Function to get DLL architecture
function Get-DLLArchitecture {
    param(
        [string]$DLLPath
    )
    
    if (-not $DLLPath -or -not (Test-Path $DLLPath)) {
        return "Unknown"
    }
    
    try {
        # Use file command if available, or check PE header
        $bytes = [System.IO.File]::ReadAllBytes($DLLPath)
        if ($bytes.Length -lt 64) {
            return "Invalid"
        }
        
        # Check PE magic number
        $peOffset = [BitConverter]::ToInt32($bytes, 60)
        if ($peOffset -ge $bytes.Length) {
            return "Invalid"
        }
        
        # Check machine type at offset 4 from PE header
        $machineType = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
        
        switch ($machineType) {
            0x014c { return "x86" }
            0x8664 { return "x64" }
            0xAA64 { return "ARM64" }
            default { return "Unknown ($machineType)" }
        }
    }
    catch {
        return "Error"
    }
}

Write-Host "Scanning context menu handlers for Directory\Background..." -ForegroundColor Yellow

# Scan HKCU context menu handlers
$hkuHandlersPath = "HKCU:\Software\Classes\Directory\Background\shellex\ContextMenuHandlers"
if (Test-Path $hkuHandlersPath) {
    $hkuHandlers = Get-ChildItem -Path $hkuHandlersPath -ErrorAction SilentlyContinue
    foreach ($handler in $hkuHandlers) {
        $handlerName = $handler.PSChildName
        $clsid = Get-RegistryValue -Path $handler.PSPath -Name "(default)"
        
        if ($clsid) {
            Write-Host "  Found: $handlerName (HKCU) - $clsid" -ForegroundColor Gray
            $clsidInfo = Get-CLSIDInfo -CLSID $clsid
            
            $extension = [PSCustomObject]@{
                Name = $handlerName
                CLSID = $clsid
                RegistrationLocation = "HKCU"
                DLLPath = $clsidInfo.DLLPath
                ThreadingModel = $clsidInfo.ThreadingModel
                Description = $clsidInfo.Description
                CLSIDLocation = $clsidInfo.Location
                Architecture = if ($clsidInfo.DLLPath) { Get-DLLArchitecture -DLLPath $clsidInfo.DLLPath } else { "N/A" }
                DLLExists = if ($clsidInfo.DLLPath) { Test-Path $clsidInfo.DLLPath } else { $false }
            }
            
            $extensions += $extension
        }
    }
}

# Scan HKLM context menu handlers
$hklmHandlersPath = "HKLM:\Software\Classes\Directory\Background\shellex\ContextMenuHandlers"
if (Test-Path $hklmHandlersPath) {
    $hklmHandlers = Get-ChildItem -Path $hklmHandlersPath -ErrorAction SilentlyContinue
    foreach ($handler in $hklmHandlers) {
        $handlerName = $handler.PSChildName
        $clsid = Get-RegistryValue -Path $handler.PSPath -Name "(default)"
        
        if ($clsid) {
            Write-Host "  Found: $handlerName (HKLM) - $clsid" -ForegroundColor Gray
            $clsidInfo = Get-CLSIDInfo -CLSID $clsid
            
            $extension = [PSCustomObject]@{
                Name = $handlerName
                CLSID = $clsid
                RegistrationLocation = "HKLM"
                DLLPath = $clsidInfo.DLLPath
                ThreadingModel = $clsidInfo.ThreadingModel
                Description = $clsidInfo.Description
                CLSIDLocation = $clsidInfo.Location
                Architecture = if ($clsidInfo.DLLPath) { Get-DLLArchitecture -DLLPath $clsidInfo.DLLPath } else { "N/A" }
                DLLExists = if ($clsidInfo.DLLPath) { Test-Path $clsidInfo.DLLPath } else { $false }
            }
            
            $extensions += $extension
        }
    }
}

Write-Host ""
Write-Host "Found $($extensions.Count) context menu handler(s)" -ForegroundColor Green
Write-Host ""

# Detect conflicts
Write-Host "Analyzing for conflicts..." -ForegroundColor Yellow

$conflicts = @()
$duplicateCLSIDs = $extensions | Group-Object -Property CLSID | Where-Object { $_.Count -gt 1 }

if ($duplicateCLSIDs) {
    foreach ($dup in $duplicateCLSIDs) {
        $conflicts += [PSCustomObject]@{
            Type = "Duplicate CLSID"
            Severity = "High"
            Details = "CLSID $($dup.Name) is registered multiple times: $($dup.Group.Name -join ', ')"
        }
    }
}

# Check for missing DLLs
$missingDLLs = $extensions | Where-Object { $_.DLLPath -and -not $_.DLLExists }
if ($missingDLLs) {
    foreach ($missing in $missingDLLs) {
        $conflicts += [PSCustomObject]@{
            Type = "Missing DLL"
            Severity = "High"
            Details = "$($missing.Name): DLL not found at $($missing.DLLPath)"
        }
    }
}

# Check for architecture mismatches (x86 extensions in x64 Explorer)
$x86Extensions = $extensions | Where-Object { $_.Architecture -eq "x86" }
if ($x86Extensions) {
    foreach ($x86 in $x86Extensions) {
        $conflicts += [PSCustomObject]@{
            Type = "Architecture Mismatch"
            Severity = "Medium"
            Details = "$($x86.Name): x86 DLL in x64 Explorer (may cause issues)"
        }
    }
}

# Check for missing ThreadingModel
$noThreadingModel = $extensions | Where-Object { -not $_.ThreadingModel }
if ($noThreadingModel) {
    foreach ($noTM in $noThreadingModel) {
        $conflicts += [PSCustomObject]@{
            Type = "Missing ThreadingModel"
            Severity = "Medium"
            Details = "$($noTM.Name): No ThreadingModel specified"
        }
    }
}

# Generate text report
Write-Host "Generating reports..." -ForegroundColor Yellow

$report = @"
Shell Extension Conflict Detection Report
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
================================================

CONTEXT MENU HANDLERS
=====================

"@

foreach ($ext in $extensions) {
    $report += @"
Extension: $($ext.Name)
  CLSID: $($ext.CLSID)
  Registration: $($ext.RegistrationLocation)
  CLSID Location: $($ext.CLSIDLocation)
  DLL Path: $($ext.DLLPath)
  DLL Exists: $($ext.DLLExists)
  Architecture: $($ext.Architecture)
  Threading Model: $($ext.ThreadingModel)
  Description: $($ext.Description)
  
"@
}

if ($conflicts.Count -gt 0) {
    $report += @"

CONFLICTS DETECTED
==================

"@
    foreach ($conflict in $conflicts) {
        $report += @"
[$($conflict.Severity)] $($conflict.Type)
  $($conflict.Details)

"@
    }
}
else {
    $report += @"

No conflicts detected.

"@
}

# Find SortBySchlong
$sortBySchlong = $extensions | Where-Object { $_.Name -like "*SortBySchlong*" }
if ($sortBySchlong) {
    $report += @"

SORTBYSCHLONG EXTENSION
=======================

"@
    foreach ($sbs in $sortBySchlong) {
        $report += @"
Extension: $($sbs.Name)
  CLSID: $($sbs.CLSID)
  Registration: $($sbs.RegistrationLocation)
  DLL Path: $($sbs.DLLPath)
  DLL Exists: $($sbs.DLLExists)
  Architecture: $($sbs.Architecture)
  Threading Model: $($sbs.ThreadingModel)
  
"@
    }
}

$report | Out-File -FilePath $txtOutput -Encoding UTF8

# Generate CSV report
$extensions | Export-Csv -Path $csvOutput -NoTypeInformation -Encoding UTF8

Write-Host ""
Write-Host "Reports generated:" -ForegroundColor Green
Write-Host "  Text: $txtOutput" -ForegroundColor Gray
Write-Host "  CSV:  $csvOutput" -ForegroundColor Gray
Write-Host ""

if ($conflicts.Count -gt 0) {
    Write-Host "WARNING: $($conflicts.Count) conflict(s) detected!" -ForegroundColor Red
    Write-Host ""
    foreach ($conflict in $conflicts) {
        Write-Host "  [$($conflict.Severity)] $($conflict.Type): $($conflict.Details)" -ForegroundColor Yellow
    }
}
else {
    Write-Host "No conflicts detected." -ForegroundColor Green
}

Write-Host ""

