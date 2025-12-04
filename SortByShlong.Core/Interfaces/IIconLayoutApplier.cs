using SortBySchlong.Core.Models;

namespace SortBySchlong.Core.Interfaces;

/// <summary>
/// Applies a layout to desktop icons by repositioning them.
/// </summary>
public interface IIconLayoutApplier
{
    /// <summary>
    /// Applies a new layout to the desktop icons.
    /// </summary>
    /// <param name="icons">The icons with their new positions to apply.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when icons is null.</exception>
    /// <exception cref="SortBySchlong.Core.Exceptions.InvalidLayoutException">Thrown when layout validation fails.</exception>
    /// <exception cref="SortBySchlong.Core.Exceptions.DesktopAccessException">Thrown when desktop window cannot be accessed.</exception>
    Task ApplyLayoutAsync(IReadOnlyList<DesktopIcon> icons, CancellationToken ct = default);
}

