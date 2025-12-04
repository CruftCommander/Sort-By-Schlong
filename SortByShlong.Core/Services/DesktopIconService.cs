using System.ComponentModel;
using System.Diagnostics;
using SortBySchlong.Core.Exceptions;
using SortBySchlong.Core.Interfaces;
using SortBySchlong.Core.Models;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;
using Serilog;

namespace SortBySchlong.Core.Services;

/// <summary>
/// Service for interacting with the Windows desktop to enumerate and reposition icons.
/// </summary>
public class DesktopIconService : IDesktopIconProvider, IIconLayoutApplier
{
    private readonly ILogger _logger;
    private const string ProgmanWindowClass = "Progman";
    private const string WorkerWWindowClass = "WorkerW";
    private const string ShellDllDefViewWindowClass = "SHELLDLL_DefView";
    private const string SysListView32WindowClass = "SysListView32";
    private const int LVM_GETITEMCOUNT = 0x1004;
    private const int LVM_GETITEMPOSITION = 0x1010;
    private const int LVM_SETITEMPOSITION = 0x100F;
    private const int LVM_GETITEMTEXT = 0x1073;
    private const int LVM_UPDATE = 0x1002;
    private const int LVM_REDRAWITEMS = 0x1015;

    /// <summary>
    /// Initializes a new instance of the <see cref="DesktopIconService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    public DesktopIconService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DesktopIcon>> GetIconsAsync(CancellationToken ct = default)
    {
        _logger.Debug("Starting icon enumeration");

        return await Task.Run<IReadOnlyList<DesktopIcon>>(() =>
        {
            try
            {
                // Add a small delay to let Explorer recover if it was stressed by previous operations
                System.Threading.Thread.Sleep(50);
                
                // Use retry logic with more attempts to handle transient window discovery issues
                // This matches the approach used in GetDesktopBoundsAsync and ApplyLayoutAsync
                var listViewHandle = FindDesktopListViewWithRetry(maxRetries: 3, delayMs: 200);
                if (listViewHandle == IntPtr.Zero)
                {
                    throw new DesktopAccessException("Could not find desktop ListView window.");
                }

                // Try to get icon count - this will validate the handle is usable
                var iconCount = SendMessage(listViewHandle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                if (iconCount < 0)
                {
                    throw new DesktopAccessException("Failed to get icon count from desktop ListView window.");
                }
                _logger.Information("Found {IconCount} icons on desktop", iconCount);

                if (iconCount == 0)
                {
                    return Array.Empty<DesktopIcon>();
                }

                var icons = new List<DesktopIcon>(iconCount);
                var consecutiveFailures = 0;
                const int maxConsecutiveFailures = 10; // Increased threshold to reduce refresh attempts
                var refreshAttempted = false; // Track if we've already tried to refresh
                var lastRefreshAttempt = 0; // Track when we last tried to refresh

                for (int i = 0; i < iconCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    // Add small delay between operations to avoid overwhelming Explorer
                    // This prevents Explorer crashes from too many rapid API calls
                    if (i > 0 && i % 10 == 0)
                    {
                        System.Threading.Thread.Sleep(10); // Small delay every 10 icons
                    }

                    // Try to get position
                    var position = GetIconPosition(listViewHandle, i, out var hadError);
                    
                    // Track consecutive failures to detect if handle is truly invalid
                    if (hadError)
                    {
                        consecutiveFailures++;
                        
                        // Only try to refresh handle after multiple consecutive failures
                        // AND only if we haven't already tried recently (cooldown period)
                        // This prevents unnecessary refreshes and Explorer crashes
                        var iconsSinceLastRefresh = i - lastRefreshAttempt;
                        if (consecutiveFailures >= maxConsecutiveFailures && 
                            !refreshAttempted && 
                            iconsSinceLastRefresh >= 20) // Cooldown: wait 20 icons between refresh attempts
                        {
                            _logger.Debug("Multiple consecutive failures detected, attempting to refresh handle");
                            var refreshedHandle = FindDesktopListView();
                            if (refreshedHandle != IntPtr.Zero)
                            {
                                listViewHandle = refreshedHandle;
                                consecutiveFailures = 0; // Reset counter on successful refresh
                                refreshAttempted = false; // Allow future refreshes
                                lastRefreshAttempt = i;
                                // Retry getting position with new handle
                                position = GetIconPosition(listViewHandle, i, out hadError);
                                if (!hadError)
                                {
                                    consecutiveFailures = 0;
                                }
                            }
                            else
                            {
                                // If we can't refresh, mark as attempted and continue with current handle
                                // Don't try again for a while to avoid log spam
                                refreshAttempted = true;
                                lastRefreshAttempt = i;
                                consecutiveFailures = 0; // Reset to prevent immediate retry
                                // Only log once to avoid spam
                                if (i < 30) // Only log early failures
                                {
                                    _logger.Warning("Could not refresh desktop window handle, continuing with current handle");
                                }
                            }
                        }
                    }
                    else
                    {
                        consecutiveFailures = 0; // Reset on success
                        // If we had a successful operation after a failed refresh attempt, allow refresh again
                        if (refreshAttempted && i - lastRefreshAttempt > 50)
                        {
                            refreshAttempted = false; // Reset after 50 successful icons
                        }
                    }

                    var text = GetIconText(listViewHandle, i);
                    icons.Add(new DesktopIcon(i, position, text));
                }

                _logger.Debug("Successfully enumerated {IconCount} icons", icons.Count);
                return icons;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Icon enumeration was cancelled");
                throw;
            }
            catch (Exception ex) when (ex is not DesktopAccessException)
            {
                _logger.Error(ex, "Error enumerating desktop icons");
                throw new DesktopAccessException("Failed to enumerate desktop icons.", ex);
            }
        }, ct);
    }

    /// <inheritdoc/>
    public async Task<DesktopBounds> GetDesktopBoundsAsync(CancellationToken ct = default)
    {
        _logger.Debug("Getting desktop bounds");

        return await Task.Run<DesktopBounds>(() =>
        {
            try
            {
                // Add a small delay to let Explorer recover if it was stressed by enumeration
                System.Threading.Thread.Sleep(50);

                // Use retry logic with more attempts to handle transient window discovery issues
                var listViewHandle = FindDesktopListViewWithRetry(maxRetries: 3, delayMs: 200);
                if (listViewHandle == IntPtr.Zero)
                {
                    // Fallback: Use screen dimensions if we can't find the ListView window
                    // This is a reasonable fallback since desktop icons are typically on the primary screen
                    _logger.Warning("Could not find desktop ListView window, using screen dimensions as fallback");
                    return GetScreenBoundsFallback();
                }

                // Try to get client rect - this will validate the handle is usable
                var hwnd = new HWND(listViewHandle);
                if (!User32.GetClientRect(hwnd, out var rect))
                {
                    var error = new Win32Exception();
                    _logger.Warning("Failed to get client rect: {Error}, using screen dimensions as fallback", error.Message);
                    return GetScreenBoundsFallback();
                }

                var bounds = new DesktopBounds(rect.Width, rect.Height);
                _logger.Debug("Desktop bounds: {Width}x{Height}", bounds.Width, bounds.Height);
                return bounds;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Get desktop bounds was cancelled");
                throw;
            }
            catch (Exception ex) when (ex is not DesktopAccessException)
            {
                _logger.Warning(ex, "Error getting desktop bounds, using screen dimensions as fallback");
                return GetScreenBoundsFallback();
            }
        }, ct);
    }

    private DesktopBounds GetScreenBoundsFallback()
    {
        try
        {
            // Get primary screen dimensions as fallback
            var width = User32.GetSystemMetrics(User32.SystemMetric.SM_CXSCREEN);
            var height = User32.GetSystemMetrics(User32.SystemMetric.SM_CYSCREEN);
            _logger.Debug("Using screen dimensions as fallback: {Width}x{Height}", width, height);
            return new DesktopBounds(width, height);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get screen dimensions, using default bounds");
            // Last resort: use a reasonable default (1920x1080)
            return new DesktopBounds(1920, 1080);
        }
    }

    /// <inheritdoc/>
    public async Task ApplyLayoutAsync(IReadOnlyList<DesktopIcon> icons, CancellationToken ct = default)
    {
        if (icons == null)
        {
            throw new ArgumentNullException(nameof(icons));
        }

        _logger.Debug("Applying layout to {IconCount} icons", icons.Count);

        if (icons.Count == 0)
        {
            _logger.Information("No icons to arrange");
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                // Add a longer delay to let Explorer recover if it was stressed by enumeration
                // Enumeration can be intensive, so we need more time for Explorer to stabilize
                _logger.Debug("Waiting for Explorer to stabilize before applying layout...");
                System.Threading.Thread.Sleep(500);

                // Get handle with more retries and longer delays to handle Explorer recovery
                // After enumeration, Explorer may need time to recreate windows
                var listViewHandle = FindDesktopListViewWithRetry(maxRetries: 5, delayMs: 300);
                if (listViewHandle == IntPtr.Zero)
                {
                    // Try one more time with even longer delays
                    _logger.Warning("Could not find desktop ListView window, waiting longer and retrying...");
                    System.Threading.Thread.Sleep(1000);
                    listViewHandle = FindDesktopListViewWithRetry(maxRetries: 5, delayMs: 500);
                    
                    if (listViewHandle == IntPtr.Zero)
                    {
                        throw new DesktopAccessException("Could not find desktop ListView window after multiple attempts. Explorer may be unresponsive.");
                    }
                }

                // Validate the handle is still valid
                if (!IsWindowHandleValid(listViewHandle))
                {
                    _logger.Warning("Initial handle is invalid, re-acquiring...");
                    listViewHandle = FindDesktopListViewWithRetry(maxRetries: 3, delayMs: 200);
                    if (listViewHandle == IntPtr.Zero)
                    {
                        throw new DesktopAccessException("Could not find desktop ListView window after re-acquisition.");
                    }
                }

                // Give Explorer time to recover from enumeration before checking responsiveness
                // Explorer may be busy processing the enumeration, so we wait a bit
                _logger.Debug("Waiting for Explorer to recover from enumeration before checking responsiveness...");
                System.Threading.Thread.Sleep(300);
                
                // Check if Explorer is responsive - but be lenient and retry if needed
                // Explorer might just be busy, not hung, so we give it multiple chances
                if (!IsExplorerResponsiveWithRetry(listViewHandle, maxRetries: 3, delayMs: 200))
                {
                    _logger.Error("Explorer appears to be hung/unresponsive after multiple attempts. Aborting layout application to prevent crash.");
                    throw new DesktopAccessException("Explorer is unresponsive. Please wait and try again.");
                }

                var currentIconCount = SendMessage(listViewHandle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                
                // If we get 0 icons, check if Explorer is hung before trying to re-acquire
                if (currentIconCount == 0 && icons.Count > 0)
                {
                    // Check if Explorer is responsive - if not, abort
                    // Use retry logic to be more lenient - Explorer might just be busy
                    if (!IsExplorerResponsiveWithRetry(listViewHandle, maxRetries: 2, delayMs: 200))
                    {
                        _logger.Error("Explorer appears hung when checking icon count. Aborting to prevent crash.");
                        throw new DesktopAccessException("Explorer is unresponsive. Please wait and try again.");
                    }

                    _logger.Warning(
                        "Desktop shows 0 icons (expected {ExpectedCount}). Handle may be invalid, attempting to re-acquire...",
                        icons.Count);
                    
                    // Add delay before re-acquisition to let Explorer recover
                    System.Threading.Thread.Sleep(500);
                    
                    // Try re-acquiring the handle with conservative retries
                    var newHandle = FindDesktopListViewWithRetry(maxRetries: 2, delayMs: 300);
                    if (newHandle != IntPtr.Zero && IsWindowHandleValid(newHandle))
                    {
                        // Check if new handle is responsive before using it
                        if (IsExplorerResponsive(newHandle))
                        {
                            var newCount = SendMessage(newHandle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                            if (newCount > 0)
                            {
                                _logger.Information("Re-acquired handle successfully, found {Count} icons", newCount);
                                listViewHandle = newHandle;
                                currentIconCount = newCount;
                            }
                        }
                        else
                        {
                            _logger.Error("Re-acquired handle but Explorer is unresponsive. Aborting.");
                            throw new DesktopAccessException("Explorer is unresponsive. Please wait and try again.");
                        }
                    }
                }
                
                // Be more lenient with validation - if count is 0, it might be a transient issue
                // But check if Explorer is hung first
                if (currentIconCount != icons.Count)
                {
                    if (currentIconCount == 0)
                    {
                        // Check one more time if Explorer is responsive
                        // Use retry logic to be more lenient
                        if (!IsExplorerResponsiveWithRetry(listViewHandle, maxRetries: 2, delayMs: 200))
                        {
                            _logger.Error("Explorer appears hung. Aborting layout application.");
                            throw new DesktopAccessException("Explorer is unresponsive. Please wait and try again.");
                        }
                        
                        // If count is still 0 after re-acquisition, log warning but continue
                        _logger.Warning(
                            "Desktop shows 0 icons (expected {ExpectedCount}). This may be a transient Explorer issue. Attempting to apply layout anyway.",
                            icons.Count);
                    }
                    else
                    {
                        // If count is different but not zero, it's a real mismatch
                        _logger.Warning(
                            "Icon count mismatch: expected {ExpectedCount}, found {ActualCount}. Attempting to apply layout anyway.",
                            icons.Count,
                            currentIconCount);
                        
                        // Only throw if the mismatch is significant (more than 10% difference)
                        var difference = Math.Abs(currentIconCount - icons.Count);
                        if (difference > icons.Count * 0.1)
                        {
                            throw new InvalidLayoutException(
                                $"Icon count mismatch: expected {icons.Count}, but desktop has {currentIconCount} icons.");
                        }
                    }
                }

                // Validate all positions are within bounds
                var bounds = GetDesktopBoundsAsync(ct).GetAwaiter().GetResult();
                var invalidIcons = icons.Where(icon => !bounds.Contains(icon.Position)).ToList();
                if (invalidIcons.Any())
                {
                    _logger.Error(
                        "Found {InvalidCount} icons with positions outside desktop bounds",
                        invalidIcons.Count);
                    throw new InvalidLayoutException(
                        $"{invalidIcons.Count} icon(s) have positions outside the desktop bounds.");
                }

                // Apply positions with handle validation and re-acquisition
                var successfulPositions = 0;
                var consecutiveFailures = 0;
                const int maxConsecutiveFailures = 5; // Re-acquire handle after 5 consecutive failures
                
                foreach (var icon in icons)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    // Check if Explorer is responsive before each operation
                    // This prevents continuing when Explorer is hung and causing crashes
                    // Use retry logic to be more lenient - Explorer might just be busy
                    if (icon.Index % 10 == 0 && !IsExplorerResponsiveWithRetry(listViewHandle, maxRetries: 2, delayMs: 100))
                    {
                        _logger.Error("Explorer became unresponsive at icon {Index} after retries. Aborting to prevent crash.", icon.Index);
                        throw new DesktopAccessException($"Explorer became unresponsive while positioning icons. {successfulPositions} icons were positioned before aborting.");
                    }
                    
                    // Validate handle periodically and re-acquire if needed
                    if (icon.Index % 20 == 0 && !IsWindowHandleValid(listViewHandle))
                    {
                        _logger.Debug("Handle invalid at icon {Index}, re-acquiring...", icon.Index);
                        // Add delay before re-acquisition
                        System.Threading.Thread.Sleep(200);
                        var newHandle = FindDesktopListViewWithRetry(maxRetries: 2, delayMs: 200);
                        if (newHandle != IntPtr.Zero && IsWindowHandleValid(newHandle) && IsExplorerResponsive(newHandle))
                        {
                            listViewHandle = newHandle;
                            consecutiveFailures = 0;
                            _logger.Debug("Successfully re-acquired handle at icon {Index}", icon.Index);
                        }
                        else
                        {
                            _logger.Error("Could not re-acquire valid responsive handle. Aborting.");
                            throw new DesktopAccessException($"Could not re-acquire desktop handle at icon {icon.Index}. {successfulPositions} icons were positioned.");
                        }
                    }
                    
                    // Add small delay between operations to avoid overwhelming Explorer
                    if (icon.Index > 0 && icon.Index % 5 == 0)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                    
                    try
                    {
                        SetIconPosition(listViewHandle, icon.Index, icon.Position);
                        successfulPositions++;
                        consecutiveFailures = 0; // Reset on success
                    }
                    catch (Exception ex)
                    {
                        consecutiveFailures++;
                        
                        // Check if Explorer is hung before trying to recover
                        // Use retry logic to be more lenient
                        if (!IsExplorerResponsiveWithRetry(listViewHandle, maxRetries: 2, delayMs: 100))
                        {
                            _logger.Error("Explorer appears hung after failure. Aborting to prevent crash.");
                            throw new DesktopAccessException($"Explorer became unresponsive after {successfulPositions} successful positions. Aborting to prevent crash.");
                        }
                        
                        // If we have multiple consecutive failures, try re-acquiring the handle
                        if (consecutiveFailures >= maxConsecutiveFailures)
                        {
                            _logger.Warning("Multiple consecutive failures detected, attempting to re-acquire handle...");
                            // Add delay before re-acquisition
                            System.Threading.Thread.Sleep(300);
                            var newHandle = FindDesktopListViewWithRetry(maxRetries: 2, delayMs: 200);
                            if (newHandle != IntPtr.Zero && IsWindowHandleValid(newHandle) && IsExplorerResponsive(newHandle))
                            {
                                listViewHandle = newHandle;
                                consecutiveFailures = 0;
                                _logger.Information("Successfully re-acquired handle after failures");
                                
                                // Retry the current icon with new handle
                                try
                                {
                                    SetIconPosition(listViewHandle, icon.Index, icon.Position);
                                    successfulPositions++;
                                    continue; // Success, move to next icon
                                }
                                catch
                                {
                                    // Still failed, continue with logging
                                }
                            }
                            else
                            {
                                _logger.Error("Could not re-acquire responsive handle. Aborting.");
                                throw new DesktopAccessException($"Could not re-acquire desktop handle after failures. {successfulPositions} icons were positioned.");
                            }
                        }
                        
                        // Log but continue - some positions might still work
                        if (successfulPositions == 0 || icon.Index % 10 == 0)
                        {
                            _logger.Warning(ex, "Failed to set position for icon {Index} to ({X}, {Y})", 
                                icon.Index, icon.Position.X, icon.Position.Y);
                        }
                    }
                }
                
                if (successfulPositions < icons.Count)
                {
                    _logger.Warning("Successfully set {SuccessfulCount} out of {TotalCount} icon positions", 
                        successfulPositions, icons.Count);
                }

                // Force desktop redraw with comprehensive refresh
                RefreshDesktopIcons(listViewHandle);

                _logger.Information("Successfully applied layout to {IconCount} icons", icons.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Layout application was cancelled");
                throw;
            }
            catch (Exception ex) when (ex is not InvalidLayoutException and not DesktopAccessException)
            {
                _logger.Error(ex, "Error applying layout");
                throw new InvalidLayoutException("Failed to apply layout to desktop icons.", ex);
            }
        }, ct);
    }

    private IntPtr FindDesktopListView()
    {
        // Reduced retries to prevent overwhelming Explorer
        return FindDesktopListViewWithRetry(maxRetries: 1, delayMs: 50);
    }

    private IntPtr FindDesktopListViewWithRetry(int maxRetries = 1, int delayMs = 50)
    {
        // First, wait for Explorer to be ready by checking if Progman window exists
        // This helps when Explorer is recreating windows after being stressed
        if (!WaitForExplorerReady(maxWaitMs: delayMs * (maxRetries + 1)))
        {
            _logger.Warning("Explorer does not appear to be ready, but continuing with window discovery");
        }

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                _logger.Debug("Retrying desktop ListView discovery (attempt {Attempt}/{MaxRetries})", attempt + 1, maxRetries + 1);
                // Add delay to avoid overwhelming Explorer with rapid retries
                System.Threading.Thread.Sleep(delayMs);
            }
            
            // Check if Explorer process is still running before attempting
            // This helps detect if Explorer has crashed
            if (!IsExplorerRunning())
            {
                _logger.Error("Explorer.exe appears to have crashed. Stopping window discovery.");
                return IntPtr.Zero;
            }

            // Find Progman window
            var progman = User32.FindWindow(ProgmanWindowClass, null);
            if (progman.IsNull)
            {
                if (attempt == maxRetries)
                {
                    _logger.Error("Could not find Progman window after {Attempts} attempts", attempt + 1);
                }
                else
                {
                    _logger.Debug("Could not find Progman window, will retry");
                }
                continue;
            }

            _logger.Debug("Found Progman window: {Handle}", progman.DangerousGetHandle());

            // Try classic Windows hierarchy first: Progman -> SHELLDLL_DefView -> SysListView32
            var defView = User32.FindWindowEx(progman, HWND.NULL, ShellDllDefViewWindowClass, null);
            if (!defView.IsNull)
            {
                _logger.Debug("Found SHELLDLL_DefView window (classic hierarchy): {Handle}", defView.DangerousGetHandle());
                
                var listView = User32.FindWindowEx(defView, HWND.NULL, SysListView32WindowClass, null);
                if (!listView.IsNull)
                {
                    _logger.Debug("Found SysListView32 window: {Handle}", listView.DangerousGetHandle());
                    return listView.DangerousGetHandle();
                }
            }

            // Try modern Windows hierarchy: Progman -> WorkerW -> SHELLDLL_DefView -> SysListView32
            _logger.Debug("Classic hierarchy not found, trying modern hierarchy with WorkerW");
            
            var workerW = FindWorkerWWindow(progman);
            if (workerW.IsNull)
            {
                if (attempt == maxRetries)
                {
                    _logger.Warning("Could not find WorkerW window after {Attempts} attempts, trying alternative method", attempt + 1);
                    // Try alternative method using EnumWindows as last resort
                    var alternativeHandle = FindDesktopListViewAlternative();
                    if (alternativeHandle != IntPtr.Zero)
                    {
                        _logger.Information("Found desktop ListView using alternative EnumWindows method");
                        return alternativeHandle;
                    }
                }
                else
                {
                    _logger.Debug("Could not find WorkerW window, will retry");
                }
                continue;
            }

            _logger.Debug("Found WorkerW window: {Handle}", workerW.DangerousGetHandle());

            defView = User32.FindWindowEx(workerW, HWND.NULL, ShellDllDefViewWindowClass, null);
            if (defView.IsNull)
            {
                if (attempt == maxRetries)
                {
                    _logger.Warning("Could not find SHELLDLL_DefView window under WorkerW after {Attempts} attempts, trying alternative method", attempt + 1);
                    // Try alternative method using EnumWindows as last resort
                    var alternativeHandle = FindDesktopListViewAlternative();
                    if (alternativeHandle != IntPtr.Zero)
                    {
                        _logger.Information("Found desktop ListView using alternative EnumWindows method");
                        return alternativeHandle;
                    }
                }
                continue;
            }

            _logger.Debug("Found SHELLDLL_DefView window (modern hierarchy): {Handle}", defView.DangerousGetHandle());

            var listViewModern = User32.FindWindowEx(defView, HWND.NULL, SysListView32WindowClass, null);
            if (listViewModern.IsNull)
            {
                if (attempt == maxRetries)
                {
                    _logger.Warning("Could not find SysListView32 window after {Attempts} attempts, trying alternative method", attempt + 1);
                    // Try alternative method using EnumWindows as last resort
                    var alternativeHandle = FindDesktopListViewAlternative();
                    if (alternativeHandle != IntPtr.Zero)
                    {
                        _logger.Information("Found desktop ListView using alternative EnumWindows method");
                        return alternativeHandle;
                    }
                }
                continue;
            }

            _logger.Debug("Found SysListView32 window: {Handle}", listViewModern.DangerousGetHandle());
            return listViewModern.DangerousGetHandle();
        }

        // Final fallback: try alternative method
        _logger.Debug("Standard methods failed, trying alternative EnumWindows method");
        var finalAttempt = FindDesktopListViewAlternative();
        if (finalAttempt != IntPtr.Zero)
        {
            _logger.Information("Found desktop ListView using alternative EnumWindows method");
            return finalAttempt;
        }

        return IntPtr.Zero;
    }

    private HWND FindWorkerWWindow(HWND progman)
    {
        // Find WorkerW windows by iterating through child windows
        // We need to find the WorkerW that contains SHELLDLL_DefView
        HWND currentChild = HWND.NULL;
        int workerWCount = 0;
        
        // Iterate through all child windows of Progman
        while (true)
        {
            currentChild = User32.FindWindowEx(progman, currentChild, WorkerWWindowClass, null);
            if (currentChild.IsNull)
            {
                break; // No more WorkerW windows found
            }

            workerWCount++;
            _logger.Debug("Found WorkerW window #{Count}: {Handle}", workerWCount, currentChild.DangerousGetHandle());

            // Check if this WorkerW contains SHELLDLL_DefView
            var defView = User32.FindWindowEx(currentChild, HWND.NULL, ShellDllDefViewWindowClass, null);
            if (!defView.IsNull)
            {
                // Found the correct WorkerW window
                _logger.Debug("WorkerW window #{Count} contains SHELLDLL_DefView", workerWCount);
                return currentChild;
            }
        }

        if (workerWCount == 0)
        {
            _logger.Debug("No WorkerW windows found under Progman");
        }
        else
        {
            _logger.Debug("Found {Count} WorkerW windows but none contain SHELLDLL_DefView", workerWCount);
        }

        return HWND.NULL;
    }

    /// <summary>
    /// Alternative method to find desktop ListView using EnumWindows.
    /// This is used as a fallback when standard methods fail.
    /// </summary>
    private IntPtr FindDesktopListViewAlternative()
    {
        try
        {
            IntPtr foundHandle = IntPtr.Zero;

            // Use EnumWindows to find all top-level windows
            User32.EnumWindows((hwnd, lParam) =>
            {
                // Check if this window has a SHELLDLL_DefView child
                var defView = User32.FindWindowEx(hwnd, HWND.NULL, ShellDllDefViewWindowClass, null);
                if (!defView.IsNull)
                {
                    // Check if this DefView has a SysListView32 child
                    var listView = User32.FindWindowEx(defView, HWND.NULL, SysListView32WindowClass, null);
                    if (!listView.IsNull)
                    {
                        // Verify this is actually the desktop ListView by checking if it has items
                        // This helps avoid false positives
                        var testCount = SendMessage(listView.DangerousGetHandle(), LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                        if (testCount >= 0)
                        {
                            foundHandle = listView.DangerousGetHandle();
                            _logger.Debug("Found desktop ListView via EnumWindows: {Handle} with {Count} items", foundHandle, testCount);
                            return false; // Stop enumeration
                        }
                    }
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            return foundHandle;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error in alternative desktop ListView discovery method");
            return IntPtr.Zero;
        }
    }

    private bool IsWindowHandleValid(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        var hwnd = new HWND(hWnd);
        return User32.IsWindow(hwnd);
    }

    private int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        // Don't validate handle here - let the actual operation determine if it's valid
        // IsWindow can return false for valid handles in some edge cases
        var hwnd = new HWND(hWnd);
        
        // Use SendMessageTimeout for critical operations to prevent hanging
        // This helps prevent Explorer crashes from blocking operations
        if (msg == LVM_GETITEMCOUNT)
        {
            const uint timeoutMs = 1000; // Reduced to 1 second to detect hangs faster
            IntPtr result = IntPtr.Zero;
            var success = User32.SendMessageTimeout(
                hwnd,
                (uint)msg,
                wParam,
                lParam,
                User32.SMTO.SMTO_ABORTIFHUNG | User32.SMTO.SMTO_NORMAL,
                timeoutMs,
                ref result);
            
            if (success == IntPtr.Zero)
            {
                var lastError = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (lastError != 0)
                {
                    var error = new Win32Exception(lastError);
                    // If timeout, Explorer is likely hung
                    if (lastError == 1460) // ERROR_TIMEOUT
                    {
                        _logger.Error("SendMessageTimeout timed out for LVM_GETITEMCOUNT - Explorer appears hung");
                    }
                    else
                    {
                        _logger.Warning("SendMessageTimeout failed for LVM_GETITEMCOUNT: {Error}", error.Message);
                    }
                }
                return 0;
            }
            
            return result.ToInt32();
        }
        
        // For other messages, use regular SendMessage but be careful
        var resultMsg = User32.SendMessage(hwnd, (User32.WindowMessage)msg, wParam, lParam);
        
        // Only log warnings for messages where zero indicates failure
        // LVM_GETITEMPOSITION can return zero in valid scenarios
        if (resultMsg == IntPtr.Zero && msg != LVM_GETITEMPOSITION)
        {
            var lastError = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (lastError != 0)
            {
                var error = new Win32Exception();
                _logger.Warning("SendMessage returned zero for message {Message}: {Error}", msg, error.Message);
            }
        }

        return resultMsg.ToInt32();
    }

    private Point GetIconPosition(IntPtr listViewHandle, int index, out bool hadError)
    {
        hadError = false;
        var point = new Vanara.PInvoke.POINT();
        var lParam = System.Runtime.InteropServices.Marshal.AllocHGlobal(
            System.Runtime.InteropServices.Marshal.SizeOf(point));

        try
        {
            // Initialize the structure
            System.Runtime.InteropServices.Marshal.StructureToPtr(point, lParam, false);
            
            var hwnd = new HWND(listViewHandle);
            
            // Use SendMessageTimeout to prevent hanging if Explorer is unresponsive
            // This helps prevent crashes by not blocking indefinitely
            const uint timeoutMs = 1000; // 1 second timeout
            IntPtr resultPtr = IntPtr.Zero;
            var result = User32.SendMessageTimeout(
                hwnd,
                (uint)LVM_GETITEMPOSITION,
                new IntPtr(index),
                lParam,
                User32.SMTO.SMTO_ABORTIFHUNG | User32.SMTO.SMTO_NORMAL,
                timeoutMs,
                ref resultPtr);

            if (result == IntPtr.Zero)
            {
                // Timeout or error - mark as error but still return (0,0) which might be valid
                hadError = true;
                var lastError = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                // Only log actual errors, not ERROR_SUCCESS
                if (lastError != 0 && index % 20 == 0) // Only log occasionally
                {
                    var error = new Win32Exception();
                    _logger.Debug("SendMessageTimeout failed for icon {Index}, last error: {Error}", index, error.Message);
                }
                return new Point(0, 0);
            }

            // Read the position from the structure (it's filled in by Windows)
            point = System.Runtime.InteropServices.Marshal.PtrToStructure<Vanara.PInvoke.POINT>(lParam)!;
            
            return new Point(point.X, point.Y);
        }
        catch (Exception ex)
        {
            hadError = true;
            // Don't log every exception to avoid overwhelming the log
            if (index % 20 == 0)
            {
                _logger.Debug(ex, "Exception getting position for icon {Index}", index);
            }
            return new Point(0, 0);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(lParam);
        }
    }

    private string? GetIconText(IntPtr listViewHandle, int index)
    {
        try
        {
            // Try to get icon text (optional, may not always work)
            // This is a simplified version - full implementation would use LVITEM structure
            return null;
        }
        catch
        {
            // Icon text is optional, so we don't fail if it can't be retrieved
            return null;
        }
    }

    private void SetIconPosition(IntPtr listViewHandle, int index, Point position)
    {
        // Validate handle before use
        if (!IsWindowHandleValid(listViewHandle))
        {
            var error = new Win32Exception();
            throw new InvalidOperationException($"Invalid window handle when setting position for icon {index}: {error.Message}", error);
        }

        var point = new Vanara.PInvoke.POINT { X = position.X, Y = position.Y };
        var lParam = MAKELPARAM(point.X, point.Y);

        var hwnd = new HWND(listViewHandle);
        var result = User32.SendMessage(
            hwnd,
            (User32.WindowMessage)LVM_SETITEMPOSITION,
            new IntPtr(index),
            lParam);

        // Check for errors - LVM_SETITEMPOSITION returns non-zero on success
        if (result == IntPtr.Zero)
        {
            var lastError = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (lastError != 0)
            {
                var error = new Win32Exception(lastError);
                throw new InvalidOperationException($"Failed to set position for icon {index} to ({position.X}, {position.Y}): {error.Message}", error);
            }
        }
    }

    private static IntPtr MAKELPARAM(int low, int high) => new IntPtr((int)((ushort)low | ((uint)(ushort)high << 16)));

    /// <summary>
    /// Forces a comprehensive refresh of the desktop icons to ensure changes are visible.
    /// </summary>
    private void RefreshDesktopIcons(IntPtr listViewHandle)
    {
        try
        {
            var hwnd = new HWND(listViewHandle);
            
            // Get parent windows for comprehensive refresh
            var defView = User32.GetParent(hwnd);
            var workerW = defView.IsNull ? HWND.NULL : User32.GetParent(defView);
            var progman = workerW.IsNull ? HWND.NULL : User32.GetParent(workerW);
            
            // Method 1: Use RedrawWindow with comprehensive flags
            // This forces a complete redraw including all children
            var redrawFlags = User32.RedrawWindowFlags.RDW_INVALIDATE | 
                             User32.RedrawWindowFlags.RDW_UPDATENOW | 
                             User32.RedrawWindowFlags.RDW_ALLCHILDREN |
                             User32.RedrawWindowFlags.RDW_ERASE;
            
            try
            {
                User32.RedrawWindow(hwnd, null, IntPtr.Zero, redrawFlags);
            }
            catch
            {
                _logger.Debug("RedrawWindow failed for ListView, trying alternative methods");
            }
            
            // Method 2: Invalidate and update the ListView
            if (!User32.InvalidateRect(hwnd, null, true))
            {
                _logger.Debug("Failed to invalidate ListView rect");
            }
            
            if (!User32.UpdateWindow(hwnd))
            {
                _logger.Debug("Failed to update ListView window");
            }
            
            // Method 3: Invalidate parent windows to ensure full refresh
            if (!defView.IsNull)
            {
                User32.InvalidateRect(defView, null, true);
                User32.UpdateWindow(defView);
            }
            
            if (!workerW.IsNull)
            {
                User32.InvalidateRect(workerW, null, true);
                User32.UpdateWindow(workerW);
            }
            
            if (!progman.IsNull)
            {
                User32.InvalidateRect(progman, null, true);
                User32.UpdateWindow(progman);
            }
            
            // Method 4: Send refresh messages to the ListView
            // WM_SETREDRAW: Enable redraw (true)
            User32.SendMessage(hwnd, User32.WindowMessage.WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            
            // LVM_UPDATE: Update a specific item (sending -1 updates all items)
            User32.SendMessage(hwnd, (User32.WindowMessage)LVM_UPDATE, new IntPtr(-1), IntPtr.Zero);
            
            // Check if Explorer is responsive before doing refresh operations
            // If Explorer is hung, skip aggressive refresh to prevent crash
            if (!IsExplorerResponsive(listViewHandle))
            {
                _logger.Warning("Explorer appears unresponsive, skipping aggressive refresh operations");
                // Just do basic invalidation
                User32.InvalidateRect(hwnd, null, true);
                return;
            }

            // LVM_REDRAWITEMS: Redraw a range of items (0 to count-1)
            // Only do this if Explorer is responsive
            var iconCount = SendMessage(listViewHandle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            if (iconCount > 0 && iconCount < 200) // Limit to reasonable icon counts
            {
                // Redraw all items from 0 to iconCount-1
                var lParam = MAKELPARAM(0, iconCount - 1);
                User32.SendMessage(hwnd, (User32.WindowMessage)LVM_REDRAWITEMS, new IntPtr(0), lParam);
            }
            
            // WM_PAINT: Force a paint message (less aggressive than RedrawWindow)
            User32.SendMessage(hwnd, User32.WindowMessage.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
            
            // Method 5: Use RedrawWindow on parent windows as well (only if Explorer is responsive)
            // Skip aggressive refresh if Explorer appears hung
            if (IsExplorerResponsive(listViewHandle))
            {
                try
                {
                    if (!defView.IsNull)
                    {
                        User32.RedrawWindow(defView, null, IntPtr.Zero, redrawFlags);
                    }
                    
                    if (!workerW.IsNull)
                    {
                        User32.RedrawWindow(workerW, null, IntPtr.Zero, redrawFlags);
                    }
                    
                    // Method 6: Small delay then final refresh to ensure changes are committed
                    System.Threading.Thread.Sleep(50);
                    User32.RedrawWindow(hwnd, null, IntPtr.Zero, redrawFlags);
                }
                catch
                {
                    _logger.Debug("Some RedrawWindow calls failed, but alternative methods were used");
                }
            }
            else
            {
                _logger.Warning("Skipping RedrawWindow operations - Explorer appears unresponsive");
            }
            
            _logger.Debug("Desktop refresh completed");
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error during desktop refresh, some changes may not be visible");
        }
    }

    /// <summary>
    /// Checks if Explorer is responsive by attempting a quick operation with timeout.
    /// </summary>
    /// <param name="listViewHandle">The ListView handle to test.</param>
    /// <returns>True if Explorer is responsive, false if hung/unresponsive.</returns>
    private bool IsExplorerResponsive(IntPtr listViewHandle)
    {
        if (listViewHandle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            // Check if Explorer process is still running
            if (!IsExplorerRunning())
            {
                return false;
            }

            // Try a quick, non-blocking operation with a reasonable timeout
            // Increased timeout to account for Explorer being busy (not hung)
            var hwnd = new HWND(listViewHandle);
            const uint timeoutMs = 1500; // Increased timeout - Explorer might just be busy
            IntPtr result = IntPtr.Zero;
            
            // Use SendMessageTimeout with a simple message to test responsiveness
            // WM_NULL is a no-op message that's safe to send
            var success = User32.SendMessageTimeout(
                hwnd,
                (uint)User32.WindowMessage.WM_NULL,
                IntPtr.Zero,
                IntPtr.Zero,
                User32.SMTO.SMTO_ABORTIFHUNG | User32.SMTO.SMTO_NORMAL,
                timeoutMs,
                ref result);

            // If SendMessageTimeout returns zero, Explorer might be hung or just busy
            if (success == IntPtr.Zero)
            {
                var lastError = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                // ERROR_TIMEOUT (1460) might just mean Explorer is busy, not necessarily hung
                if (lastError == 1460)
                {
                    _logger.Debug("Explorer timeout on responsiveness check (might just be busy)");
                }
                else if (lastError != 0)
                {
                    _logger.Warning("Explorer appears unresponsive (error: {Error})", lastError);
                }
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error checking Explorer responsiveness");
            return false;
        }
    }

    /// <summary>
    /// Checks if Explorer is responsive with retries, giving it time to recover.
    /// </summary>
    /// <param name="listViewHandle">The ListView handle to test.</param>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <param name="delayMs">Delay between retries in milliseconds.</param>
    /// <returns>True if Explorer becomes responsive, false if still unresponsive after retries.</returns>
    private bool IsExplorerResponsiveWithRetry(IntPtr listViewHandle, int maxRetries = 3, int delayMs = 200)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                _logger.Debug("Retrying Explorer responsiveness check (attempt {Attempt}/{MaxRetries})", attempt + 1, maxRetries + 1);
                System.Threading.Thread.Sleep(delayMs);
            }

            if (IsExplorerResponsive(listViewHandle))
            {
                if (attempt > 0)
                {
                    _logger.Information("Explorer became responsive after {Attempts} attempts", attempt + 1);
                }
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Waits for Explorer to be ready by checking if the Progman window exists.
    /// </summary>
    /// <param name="maxWaitMs">Maximum time to wait in milliseconds.</param>
    /// <returns>True if Explorer appears ready (Progman window found), false if timeout.</returns>
    private bool WaitForExplorerReady(int maxWaitMs = 2000)
    {
        const int checkIntervalMs = 100;
        int elapsedMs = 0;
        
        while (elapsedMs < maxWaitMs)
        {
            // Check if Explorer process is running
            if (!IsExplorerRunning())
            {
                _logger.Warning("Explorer process not found while waiting for readiness");
                return false;
            }
            
            // Check if Progman window exists
            var progman = User32.FindWindow(ProgmanWindowClass, null);
            if (!progman.IsNull)
            {
                _logger.Debug("Explorer appears ready (Progman window found)");
                return true;
            }
            
            // Wait before next check
            System.Threading.Thread.Sleep(checkIntervalMs);
            elapsedMs += checkIntervalMs;
        }
        
        _logger.Debug("Timeout waiting for Explorer to be ready after {ElapsedMs}ms", elapsedMs);
        return false;
    }

    private static bool IsExplorerRunning()
    {
        try
        {
            var explorerProcesses = Process.GetProcessesByName("explorer");
            return explorerProcesses.Length > 0;
        }
        catch
        {
            // If we can't check, assume it's running to avoid false positives
            return true;
        }
    }
}

