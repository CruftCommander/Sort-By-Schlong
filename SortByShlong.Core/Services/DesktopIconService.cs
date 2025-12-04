using System.ComponentModel;
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
                var listViewHandle = FindDesktopListView();
                if (listViewHandle == IntPtr.Zero)
                {
                    throw new DesktopAccessException("Could not find desktop ListView window.");
                }

                var iconCount = SendMessage(listViewHandle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                _logger.Information("Found {IconCount} icons on desktop", iconCount);

                if (iconCount == 0)
                {
                    return Array.Empty<DesktopIcon>();
                }

                var icons = new List<DesktopIcon>(iconCount);

                for (int i = 0; i < iconCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var position = GetIconPosition(listViewHandle, i);
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
                var listViewHandle = FindDesktopListView();
                if (listViewHandle == IntPtr.Zero)
                {
                    throw new DesktopAccessException("Could not find desktop ListView window.");
                }

                var hwnd = new HWND(listViewHandle);
                if (!User32.GetClientRect(hwnd, out var rect))
                {
                    var error = new Win32Exception();
                    _logger.Error("Failed to get client rect: {Error}", error.Message);
                    throw new DesktopAccessException("Failed to get desktop bounds.", error);
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
                _logger.Error(ex, "Error getting desktop bounds");
                throw new DesktopAccessException("Failed to get desktop bounds.", ex);
            }
        }, ct);
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
                var listViewHandle = FindDesktopListView();
                if (listViewHandle == IntPtr.Zero)
                {
                    throw new DesktopAccessException("Could not find desktop ListView window.");
                }

                // Get current icon count to validate
                var currentIconCount = SendMessage(listViewHandle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                if (currentIconCount != icons.Count)
                {
                    _logger.Warning(
                        "Icon count mismatch: expected {ExpectedCount}, found {ActualCount}",
                        icons.Count,
                        currentIconCount);
                    throw new InvalidLayoutException(
                        $"Icon count mismatch: expected {icons.Count}, but desktop has {currentIconCount} icons.");
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
                foreach (var icon in icons)
                {
                    ct.ThrowIfCancellationRequested();
                    SetIconPosition(listViewHandle, icon.Index, icon.Position);
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
        // Find Progman window
        var progman = User32.FindWindow(ProgmanWindowClass, null);
        if (progman.IsNull)
        {
            _logger.Error("Could not find Progman window");
            return IntPtr.Zero;
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
            _logger.Error("Could not find WorkerW window");
            return IntPtr.Zero;
        }

        _logger.Debug("Found WorkerW window: {Handle}", workerW.DangerousGetHandle());

        defView = User32.FindWindowEx(workerW, HWND.NULL, ShellDllDefViewWindowClass, null);
        if (defView.IsNull)
        {
            _logger.Error("Could not find SHELLDLL_DefView window under WorkerW");
            return IntPtr.Zero;
        }

        _logger.Debug("Found SHELLDLL_DefView window (modern hierarchy): {Handle}", defView.DangerousGetHandle());

        var listViewModern = User32.FindWindowEx(defView, HWND.NULL, SysListView32WindowClass, null);
        if (listViewModern.IsNull)
        {
            _logger.Error("Could not find SysListView32 window");
            return IntPtr.Zero;
        }

        _logger.Debug("Found SysListView32 window: {Handle}", listViewModern.DangerousGetHandle());
        return listViewModern.DangerousGetHandle();
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

    private int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        var hwnd = new HWND(hWnd);
        var result = User32.SendMessage(hwnd, (User32.WindowMessage)msg, wParam, lParam);
        if (result == IntPtr.Zero && msg != LVM_GETITEMCOUNT)
        {
            var error = new Win32Exception();
            _logger.Warning("SendMessage returned zero for message {Message}: {Error}", msg, error.Message);
        }

        return result.ToInt32();
    }

    private Point GetIconPosition(IntPtr listViewHandle, int index)
    {
        var point = new Vanara.PInvoke.POINT();
        var lParam = System.Runtime.InteropServices.Marshal.AllocHGlobal(
            System.Runtime.InteropServices.Marshal.SizeOf(point));

        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(point, lParam, false);
            var hwnd = new HWND(listViewHandle);
            var result = User32.SendMessage(
                hwnd,
                (User32.WindowMessage)LVM_GETITEMPOSITION,
                new IntPtr(index),
                lParam);

            if (result == IntPtr.Zero)
            {
                var error = new Win32Exception();
                _logger.Warning("Failed to get position for icon {Index}: {Error}", index, error.Message);
                return new Point(0, 0);
            }

            point = System.Runtime.InteropServices.Marshal.PtrToStructure<Vanara.PInvoke.POINT>(lParam)!;
            return new Point(point.X, point.Y);
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
}

