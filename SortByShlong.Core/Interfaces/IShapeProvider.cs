using SortBySchlong.Core.Models;

namespace SortBySchlong.Core.Interfaces;

/// <summary>
/// Generates layouts for arranging desktop icons in a specific shape.
/// </summary>
public interface IShapeProvider
{
    /// <summary>
    /// Gets the unique key identifying this shape provider.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Generates a layout of points representing where icons should be positioned.
    /// </summary>
    /// <param name="iconCount">The number of icons to arrange.</param>
    /// <param name="bounds">The desktop bounds to arrange icons within.</param>
    /// <returns>A list of points representing icon positions.</returns>
    /// <exception cref="ArgumentException">Thrown when iconCount is less than the minimum required for this shape.</exception>
    IReadOnlyList<Point> GenerateLayout(int iconCount, DesktopBounds bounds);
}

