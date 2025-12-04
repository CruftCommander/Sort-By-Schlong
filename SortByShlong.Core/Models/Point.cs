namespace SortBySchlong.Core.Models;

/// <summary>
/// Represents a point in 2D space with X and Y coordinates.
/// </summary>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
public readonly record struct Point(int X, int Y)
{
    /// <summary>
    /// Creates a point from System.Drawing.Point.
    /// </summary>
    public static Point FromSystemPoint(System.Drawing.Point systemPoint) => new(systemPoint.X, systemPoint.Y);

    /// <summary>
    /// Converts to System.Drawing.Point.
    /// </summary>
    public System.Drawing.Point ToSystemPoint() => new(X, Y);
}

