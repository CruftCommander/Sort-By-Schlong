using SortBySchlong.Core.Models;

namespace SortBySchlong.Core.Interfaces;

/// <summary>
/// Provides access to desktop icons and their properties.
/// </summary>
public interface IDesktopIconProvider
{
    /// <summary>
    /// Retrieves all icons from the desktop.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A list of desktop icons.</returns>
    /// <exception cref="SortBySchlong.Core.Exceptions.DesktopAccessException">Thrown when desktop window cannot be accessed.</exception>
    Task<IReadOnlyList<DesktopIcon>> GetIconsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the bounds of the desktop ListView window.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The desktop bounds.</returns>
    /// <exception cref="SortBySchlong.Core.Exceptions.DesktopAccessException">Thrown when desktop window cannot be accessed.</exception>
    Task<DesktopBounds> GetDesktopBoundsAsync(CancellationToken ct = default);
}

