namespace SortBySchlong.Core.Models;

/// <summary>
/// Represents the bounds of the desktop ListView window.
/// </summary>
/// <param name="Width">The width of the desktop bounds in pixels.</param>
/// <param name="Height">The height of the desktop bounds in pixels.</param>
public readonly record struct DesktopBounds(int Width, int Height)
{
    /// <summary>
    /// Gets the center point of the desktop bounds.
    /// </summary>
    public Point Center => new(Width / 2, Height / 2);

    /// <summary>
    /// Validates that a point is within the bounds.
    /// </summary>
    /// <param name="point">The point to validate.</param>
    /// <returns>True if the point is within bounds; otherwise, false.</returns>
    public bool Contains(Point point) =>
        point.X >= 0 && point.X < Width && point.Y >= 0 && point.Y < Height;

    /// <summary>
    /// Validates that all points are within the bounds.
    /// </summary>
    /// <param name="points">The points to validate.</param>
    /// <returns>True if all points are within bounds; otherwise, false.</returns>
    public bool ContainsAll(IEnumerable<Point> points) => points.All(Contains);
}

