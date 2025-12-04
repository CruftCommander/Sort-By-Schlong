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
                // Add a small delay to let Explorer recover if it was stressed
                System.Threading.Thread.Sleep(100);

                var listViewHandle = FindDesktopListViewWithRetry(maxRetries: 3, delayMs: 200);
                if (listViewHandle == IntPtr.Zero)
                {
                    throw new DesktopAccessException("Could not find desktop ListView window.");
                }

                // Get current icon count to validate
                var currentIconCount = SendMessage(listViewHandle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                
                // Be more lenient with validation - if count is 0, it might be a transient issue
                // Still try to apply the layout as the icons might reappear
                if (currentIconCount != icons.Count)
                {
                    if (currentIconCount == 0)
                    {
                        // If count is 0, it might be a transient Explorer issue
                        // Log warning but continue - the layout might still work
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

                // Apply positions
                var successfulPositions = 0;
                foreach (var icon in icons)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    try
                    {
                        SetIconPosition(listViewHandle, icon.Index, icon.Position);
                        successfulPositions++;
                    }
                    catch (Exception ex)
                    {
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

                // Force desktop redraw
                var hwnd = new HWND(listViewHandle);
                if (!User32.InvalidateRect(hwnd, null, true))
                {
                    _logger.Warning("Failed to invalidate desktop rect for redraw");
                }

                if (!User32.UpdateWindow(hwnd))
                {
                    _logger.Warning("Failed to update desktop window");
                }

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
                    _logger.Error("Could not find WorkerW window after {Attempts} attempts", attempt + 1);
                }
                continue;
            }

            _logger.Debug("Found WorkerW window: {Handle}", workerW.DangerousGetHandle());

            defView = User32.FindWindowEx(workerW, HWND.NULL, ShellDllDefViewWindowClass, null);
            if (defView.IsNull)
            {
                if (attempt == maxRetries)
                {
                    _logger.Error("Could not find SHELLDLL_DefView window under WorkerW after {Attempts} attempts", attempt + 1);
                }
                continue;
            }

            _logger.Debug("Found SHELLDLL_DefView window (modern hierarchy): {Handle}", defView.DangerousGetHandle());

            var listViewModern = User32.FindWindowEx(defView, HWND.NULL, SysListView32WindowClass, null);
            if (listViewModern.IsNull)
            {
                if (attempt == maxRetries)
                {
                    _logger.Error("Could not find SysListView32 window after {Attempts} attempts", attempt + 1);
                }
                continue;
            }

            _logger.Debug("Found SysListView32 window: {Handle}", listViewModern.DangerousGetHandle());
            return listViewModern.DangerousGetHandle();
        }

        return IntPtr.Zero;
    }

    private HWND FindWorkerWWindow(HWND progman)
    {
        // Find WorkerW windows by iterating through child windows
        // We need to find the WorkerW that contains SHELLDLL_DefView
        HWND currentChild = HWND.NULL;
        
        // Iterate through all child windows of Progman
        while (true)
        {
            currentChild = User32.FindWindowEx(progman, currentChild, WorkerWWindowClass, null);
            if (currentChild.IsNull)
            {
                break; // No more WorkerW windows found
            }

            // Check if this WorkerW contains SHELLDLL_DefView
            var defView = User32.FindWindowEx(currentChild, HWND.NULL, ShellDllDefViewWindowClass, null);
            if (!defView.IsNull)
            {
                // Found the correct WorkerW window
                return currentChild;
            }
        }

        return HWND.NULL;
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
            const uint timeoutMs = 2000; // 2 second timeout for getting count
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
                    var error = new Win32Exception();
                    _logger.Warning("SendMessageTimeout failed for LVM_GETITEMCOUNT: {Error}", error.Message);
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
        var point = new Vanara.PInvoke.POINT { X = position.X, Y = position.Y };
        var lParam = MAKELPARAM(point.X, point.Y);

        var hwnd = new HWND(listViewHandle);
        var result = User32.SendMessage(
            hwnd,
            (User32.WindowMessage)LVM_SETITEMPOSITION,
            new IntPtr(index),
            lParam);

        if (result == IntPtr.Zero)
        {
            var error = new Win32Exception();
            _logger.Warning("Failed to set position for icon {Index} to ({X}, {Y}): {Error}",
                index, position.X, position.Y, error.Message);
        }
    }

    private static IntPtr MAKELPARAM(int low, int high) => new IntPtr((int)((ushort)low | ((uint)(ushort)high << 16)));

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

