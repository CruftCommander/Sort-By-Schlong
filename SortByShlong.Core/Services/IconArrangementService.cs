using System.Diagnostics;
using SortBySchlong.Core.Exceptions;
using SortBySchlong.Core.Interfaces;
using SortBySchlong.Core.Models;
using Serilog;
using Serilog.Context;

namespace SortBySchlong.Core.Services;

/// <summary>
/// Orchestrates the icon arrangement process by coordinating icon retrieval, shape generation, and layout application.
/// </summary>
public class IconArrangementService
{
    private readonly IDesktopIconProvider _iconProvider;
    private readonly IIconLayoutApplier _layoutApplier;
    private readonly IShapeRegistry _shapeRegistry;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IconArrangementService"/> class.
    /// </summary>
    /// <param name="iconProvider">The desktop icon provider.</param>
    /// <param name="layoutApplier">The layout applier.</param>
    /// <param name="shapeRegistry">The shape registry.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public IconArrangementService(
        IDesktopIconProvider iconProvider,
        IIconLayoutApplier layoutApplier,
        IShapeRegistry shapeRegistry,
        ILogger logger)
    {
        _iconProvider = iconProvider ?? throw new ArgumentNullException(nameof(iconProvider));
        _layoutApplier = layoutApplier ?? throw new ArgumentNullException(nameof(layoutApplier));
        _shapeRegistry = shapeRegistry ?? throw new ArgumentNullException(nameof(shapeRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Arranges desktop icons in the specified shape.
    /// </summary>
    /// <param name="shapeKey">The key of the shape to arrange icons in.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when shapeKey is null or empty.</exception>
    /// <exception cref="ShapeNotFoundException">Thrown when the specified shape is not found.</exception>
    /// <exception cref="IconArrangementException">Thrown when arrangement fails.</exception>
    public async Task ArrangeIconsAsync(string shapeKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(shapeKey))
        {
            throw new ArgumentException("Shape key cannot be null or empty.", nameof(shapeKey));
        }

        var correlationId = Guid.NewGuid().ToString("N")[..8];
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            _logger.Information("Starting icon arrangement for shape '{ShapeKey}' (CorrelationId: {CorrelationId})", shapeKey, correlationId);

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Step 1: Get shape provider
            var shapeProvider = _shapeRegistry.GetProvider(shapeKey);
            if (shapeProvider == null)
            {
                _logger.Error("Shape '{ShapeKey}' not found in registry. Available shapes: {AvailableShapes}",
                    shapeKey,
                    string.Join(", ", _shapeRegistry.GetAvailableShapes()));
                throw new ShapeNotFoundException(shapeKey);
            }

            _logger.Debug("Found shape provider '{ShapeKey}'", shapeKey);

            // Step 2: Get icons from desktop
            _logger.Debug("Retrieving icons from desktop");
            var icons = await _iconProvider.GetIconsAsync(ct);

            if (icons.Count == 0)
            {
                _logger.Warning("No icons found on desktop. Nothing to arrange.");
                return;
            }

            _logger.Information("Retrieved {IconCount} icons from desktop", icons.Count);

            // Step 3: Get desktop bounds
            _logger.Debug("Retrieving desktop bounds");
            var bounds = await _iconProvider.GetDesktopBoundsAsync(ct);
            _logger.Debug("Desktop bounds: {Width}x{Height}", bounds.Width, bounds.Height);

            // Step 4: Generate layout
            _logger.Debug("Generating layout for {IconCount} icons", icons.Count);
            IReadOnlyList<Point> layout;
            try
            {
                layout = shapeProvider.GenerateLayout(icons.Count, bounds);
            }
            catch (ArgumentException ex)
            {
                _logger.Error(ex, "Failed to generate layout: {Message}", ex.Message);
                throw new InvalidLayoutException($"Failed to generate layout: {ex.Message}", ex);
            }

            if (layout.Count != icons.Count)
            {
                _logger.Error(
                    "Layout count mismatch: expected {ExpectedCount}, got {ActualCount}",
                    icons.Count,
                    layout.Count);
                throw new InvalidLayoutException(
                    $"Layout generation produced {layout.Count} points, but {icons.Count} icons are present.");
            }

            _logger.Debug("Generated layout with {PointCount} points", layout.Count);

            // Step 5: Validate layout points are within bounds
            if (!bounds.ContainsAll(layout))
            {
                var invalidPoints = layout.Where(p => !bounds.Contains(p)).ToList();
                _logger.Error(
                    "Layout contains {InvalidCount} points outside desktop bounds",
                    invalidPoints.Count);
                throw new InvalidLayoutException(
                    $"Layout contains {invalidPoints.Count} point(s) outside the desktop bounds.");
            }

            // Step 6: Create icons with new positions
            var iconsWithNewPositions = icons
                .Select((icon, index) => icon with { Position = layout[index] })
                .ToList();

            // Step 7: Apply layout
            // Add a brief delay to let Explorer recover from enumeration before applying layout
            // This helps prevent "window not found" errors when Explorer is recreating windows
            _logger.Debug("Waiting briefly before applying layout to let Explorer stabilize...");
            await Task.Delay(300, ct);

            _logger.Debug("Applying layout to desktop");
            await _layoutApplier.ApplyLayoutAsync(iconsWithNewPositions, ct);

            stopwatch.Stop();
            _logger.Information(
                "Successfully arranged {IconCount} icons in shape '{ShapeKey}' in {ElapsedMs}ms (CorrelationId: {CorrelationId})",
                icons.Count,
                shapeKey,
                stopwatch.ElapsedMilliseconds,
                correlationId);
        }
        catch (ShapeNotFoundException)
        {
            // Re-throw shape not found exceptions as-is
            throw;
        }
        catch (InvalidLayoutException)
        {
            // Re-throw layout exceptions as-is
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Icon arrangement was cancelled (CorrelationId: {CorrelationId})", correlationId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error during icon arrangement (CorrelationId: {CorrelationId})", correlationId);
            throw new IconArrangementException(
                $"Failed to arrange icons in shape '{shapeKey}'. See inner exception for details.",
                ex);
        }
        }
    }
}

