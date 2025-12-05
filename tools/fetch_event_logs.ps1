# Event Viewer Log Fetcher for Explorer Crashes
# Fetches relevant Windows Event Log entries related to Explorer crashes and SortBySchlong

param(
    [string]$OutputDir = ".",
    [int]$HoursBack = 24,
    [switch]$IncludeSystem,
    [switch]$IncludeApplication,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

# Output files
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$txtOutput = Join-Path $OutputDir "event_logs_$timestamp.txt"
$csvOutput = Join-Path $OutputDir "event_logs_$timestamp.csv"
$xmlOutput = Join-Path $OutputDir "event_logs_$timestamp.xml"

Write-Host "Event Viewer Log Fetcher" -ForegroundColor Cyan
Write-Host "=======================" -ForegroundColor Cyan
Write-Host ""

# Calculate time range
$startTime = (Get-Date).AddHours(-$HoursBack)
$endTime = Get-Date

Write-Host "Fetching events from $startTime to $endTime..." -ForegroundColor Yellow
Write-Host ""

$allEvents = @()

# Function to format event data
function Format-EventData {
    param($Event)
    
    $eventData = @{
        TimeCreated = $Event.TimeCreated
        Id = $Event.Id
        Level = $Event.LevelDisplayName
        LogName = $Event.LogName
        ProviderName = $Event.ProviderName
        MachineName = $Event.MachineName
        Message = $Event.Message
        ProcessId = $Event.ProcessId
        ThreadId = $Event.ThreadId
        UserId = $Event.UserId
    }
    
    # Extract additional properties from XML if available
    if ($Event.ToXml) {
        try {
            $xml = [xml]$Event.ToXml()
            if ($xml.Event.EventData) {
                $eventData.Data = @{}
                foreach ($data in $xml.Event.EventData.Data) {
                    $eventData.Data[$data.Name] = $data.'#text'
                }
            }
        }
        catch {
            # Ignore XML parsing errors
        }
    }
    
    return $eventData
}

# Fetch Application log events
if ($IncludeApplication -or (-not $IncludeSystem)) {
    Write-Host "Scanning Application log..." -ForegroundColor Gray
    
    try {
        # Explorer crashes (Event ID 1000, 1001)
        $appEvents = Get-WinEvent -FilterHashtable @{
            LogName = 'Application'
            StartTime = $startTime
            EndTime = $endTime
        } -ErrorAction SilentlyContinue | Where-Object {
            ($_.Id -eq 1000 -or $_.Id -eq 1001) -and
            ($_.Message -like "*explorer.exe*" -or $_.Message -like "*SortBySchlong*")
        }
        
        foreach ($event in $appEvents) {
            $formatted = Format-EventData -Event $event
            $formatted.LogName = "Application"
            $allEvents += [PSCustomObject]$formatted
        }
        
        Write-Host "  Found $($appEvents.Count) Application log event(s)" -ForegroundColor Gray
    }
    catch {
        Write-Host "  Error reading Application log: $_" -ForegroundColor Red
    }
}

# Fetch System log events
if ($IncludeSystem) {
    Write-Host "Scanning System log..." -ForegroundColor Gray
    
    try {
        # System errors related to Explorer
        $systemEvents = Get-WinEvent -FilterHashtable @{
            LogName = 'System'
            StartTime = $startTime
            EndTime = $endTime
        } -ErrorAction SilentlyContinue | Where-Object {
            ($_.Id -eq 7034 -or $_.Id -eq 1000 -or $_.Id -eq 1001) -and
            ($_.Message -like "*explorer.exe*" -or $_.Message -like "*SortBySchlong*")
        }
        
        foreach ($event in $systemEvents) {
            $formatted = Format-EventData -Event $event
            $formatted.LogName = "System"
            $allEvents += [PSCustomObject]$formatted
        }
        
        Write-Host "  Found $($systemEvents.Count) System log event(s)" -ForegroundColor Gray
    }
    catch {
        Write-Host "  Error reading System log: $_" -ForegroundColor Red
    }
}

# Also check for Windows Error Reporting events
Write-Host "Scanning for Windows Error Reporting events..." -ForegroundColor Gray

try {
    $werEvents = Get-WinEvent -FilterHashtable @{
        LogName = 'Application'
        ProviderName = 'Windows Error Reporting'
        StartTime = $startTime
        EndTime = $endTime
    } -ErrorAction SilentlyContinue | Where-Object {
        $_.Message -like "*explorer.exe*" -or $_.Message -like "*SortBySchlong*"
    }
    
    foreach ($event in $werEvents) {
        $formatted = Format-EventData -Event $event
        $formatted.LogName = "Application (WER)"
        $allEvents += [PSCustomObject]$formatted
    }
    
    Write-Host "  Found $($werEvents.Count) WER event(s)" -ForegroundColor Gray
}
catch {
    Write-Host "  Error reading WER events: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Total events found: $($allEvents.Count)" -ForegroundColor Green
Write-Host ""

# Generate text report
if ($allEvents.Count -gt 0) {
    Write-Host "Generating reports..." -ForegroundColor Yellow
    
    $report = @"
Windows Event Log Report - Explorer Crashes
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Time Range: $startTime to $endTime
================================================

"@
    
    foreach ($event in $allEvents | Sort-Object TimeCreated -Descending) {
        $report += @"
Event #$($allEvents.IndexOf($event) + 1)
----------------------------------------
Time: $($event.TimeCreated)
Log: $($event.LogName)
Level: $($event.Level)
Event ID: $($event.Id)
Provider: $($event.ProviderName)
Process ID: $($event.ProcessId)
Thread ID: $($event.ThreadId)

Message:
$($event.Message)

"@
        
        if ($event.Data) {
            $report += "Additional Data:`n"
            foreach ($key in $event.Data.Keys) {
                $report += "  $key = $($event.Data[$key])`n"
            }
            $report += "`n"
        }
        
        $report += "`n"
    }
    
    $report | Out-File -FilePath $txtOutput -Encoding UTF8
    
    # Generate CSV (flattened)
    $csvData = @()
    foreach ($event in $allEvents) {
        $row = [PSCustomObject]@{
            TimeCreated = $event.TimeCreated
            LogName = $event.LogName
            Level = $event.Level
            EventId = $event.Id
            ProviderName = $event.ProviderName
            ProcessId = $event.ProcessId
            ThreadId = $event.ThreadId
            Message = $event.Message
        }
        
        # Add data fields as columns
        if ($event.Data) {
            foreach ($key in $event.Data.Keys) {
                $row | Add-Member -NotePropertyName "Data_$key" -NotePropertyValue $event.Data[$key]
            }
        }
        
        $csvData += $row
    }
    
    $csvData | Export-Csv -Path $csvOutput -NoTypeInformation -Encoding UTF8
    
    # Generate XML export
    try {
        $allEvents | Export-Clixml -Path $xmlOutput -Depth 10
    }
    catch {
        Write-Host "  Warning: Could not generate XML export" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "Reports generated:" -ForegroundColor Green
    Write-Host "  Text: $txtOutput" -ForegroundColor Gray
    Write-Host "  CSV:  $csvOutput" -ForegroundColor Gray
    Write-Host "  XML:  $xmlOutput" -ForegroundColor Gray
    Write-Host ""
    
    # Summary
    Write-Host "Summary:" -ForegroundColor Cyan
    $byLevel = $allEvents | Group-Object -Property Level
    foreach ($level in $byLevel) {
        Write-Host "  $($level.Name): $($level.Count)" -ForegroundColor Gray
    }
    
    $byLog = $allEvents | Group-Object -Property LogName
    Write-Host ""
    Write-Host "By Log:" -ForegroundColor Cyan
    foreach ($log in $byLog) {
        Write-Host "  $($log.Name): $($log.Count)" -ForegroundColor Gray
    }
}
else {
    Write-Host "No relevant events found in the specified time range." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Try:" -ForegroundColor Cyan
    Write-Host "  - Increasing -HoursBack parameter" -ForegroundColor Gray
    Write-Host "  - Using -IncludeSystem to scan System log" -ForegroundColor Gray
    Write-Host "  - Checking if Explorer actually crashed (check Task Manager)" -ForegroundColor Gray
}

Write-Host ""

