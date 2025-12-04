using SortBySchlong.Core.Interfaces;
using SortBySchlong.Core.Models;

namespace SortBySchlong.Core.Shapes;

/// <summary>
/// Shape provider that arranges icons in a penis shape.
/// </summary>
public class PenisShapeProvider : IShapeProvider
{
    private const int MinIconCount = 3;

    /// <inheritdoc/>
    public string Key => "penis";

    /// <inheritdoc/>
    public IReadOnlyList<Point> GenerateLayout(int iconCount, DesktopBounds bounds)
    {
        if (iconCount < MinIconCount)
        {
            throw new ArgumentException(
                $"At least {MinIconCount} icons are required for the penis shape, but {iconCount} were provided.",
                nameof(iconCount));
        }

        var points = new List<Point>();

        // Calculate proportions
        var centerX = bounds.Width / 2;
        var bottomY = (int)(bounds.Height * 0.9);
        var topY = (int)(bounds.Height * 0.1);

        // Allocate icons:
        // - 2 for balls
        // - Remaining icons for shaft + head
        var iconsForBalls = 2;
        var iconsForShaftAndHead = iconCount - iconsForBalls;

        // Generate balls (two ellipses at bottom)
        var ballRadius = Math.Min(bounds.Width, bounds.Height) / 10;
        var ballCenterY = bottomY;
        var leftBallCenterX = centerX - ballRadius;
        var rightBallCenterX = centerX + ballRadius;

        points.Add(new Point(leftBallCenterX, ballCenterY)); // Left ball center
        points.Add(new Point(rightBallCenterX, ballCenterY)); // Right ball center

        // Generate shaft (vertical line from bottom to top)
        var shaftTopY = topY + (int)((bounds.Height * 0.1)); // Leave some room for head
        var shaftLength = bottomY - shaftTopY;
        var iconsForShaft = Math.Max(1, iconsForShaftAndHead - 3); // Reserve some for head

        if (iconsForShaft > 1)
        {
            for (int i = 0; i < iconsForShaft; i++)
            {
                var y = shaftTopY + (int)((double)i / (iconsForShaft - 1) * shaftLength);
                points.Add(new Point(centerX, y));
            }
        }
        else
        {
            // Single point for shaft
            points.Add(new Point(centerX, (shaftTopY + bottomY) / 2));
        }

        // Generate head (curved segment at top - ellipse arc)
        var headCenterY = topY;
        var headWidth = ballRadius * 1.5;
        var headHeight = (int)(bounds.Height * 0.08);
        var iconsForHead = iconsForShaftAndHead - iconsForShaft;

        if (iconsForHead > 0)
        {
            for (int i = 0; i < iconsForHead; i++)
            {
                var angle = Math.PI * i / (iconsForHead + 1); // 0 to PI (top half of ellipse)
                var x = centerX + (int)(headWidth * Math.Cos(angle));
                var y = headCenterY + (int)(headHeight * Math.Sin(angle));
                points.Add(new Point(x, y));
            }
        }

        // Ensure all points are within bounds and validate
        var validPoints = points.Where(p => bounds.Contains(p)).ToList();

        if (validPoints.Count != iconCount)
        {
            // If we lost points due to bounds checking, adjust proportionally
            // This is a fallback - in practice, this shouldn't happen with reasonable desktop sizes
            validPoints = AdjustPointsToBounds(validPoints, bounds, iconCount);
        }

        return validPoints;
    }

    private static List<Point> AdjustPointsToBounds(List<Point> points, DesktopBounds bounds, int targetCount)
    {
        // Simple adjustment: scale points to fit within bounds
        var result = new List<Point>(targetCount);

        if (points.Count == 0)
        {
            // Fallback: distribute evenly if no valid points
            var spacing = Math.Min(bounds.Width, bounds.Height) / (targetCount + 1);
            for (int i = 0; i < targetCount; i++)
            {
                result.Add(new Point(spacing * (i + 1), spacing * (i + 1)));
            }

            return result;
        }

        // Find bounding box of current points
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);

        var currentWidth = maxX - minX;
        var currentHeight = maxY - minY;

        if (currentWidth == 0) currentWidth = 1;
        if (currentHeight == 0) currentHeight = 1;

        // Scale factor to fit within bounds (with margin)
        var margin = 0.05;
        var scaleX = (bounds.Width * (1 - 2 * margin)) / currentWidth;
        var scaleY = (bounds.Height * (1 - 2 * margin)) / currentHeight;
        var scale = Math.Min(scaleX, scaleY);

        // Scale and center points
        var offsetX = (int)(bounds.Width * margin) - (int)(minX * scale);
        var offsetY = (int)(bounds.Height * margin) - (int)(minY * scale);

        foreach (var point in points)
        {
            var scaledX = (int)(point.X * scale) + offsetX;
            var scaledY = (int)(point.Y * scale) + offsetY;

            // Clamp to bounds
            scaledX = Math.Max(0, Math.Min(bounds.Width - 1, scaledX));
            scaledY = Math.Max(0, Math.Min(bounds.Height - 1, scaledY));

            result.Add(new Point(scaledX, scaledY));
        }

        // If we need more points, interpolate
        while (result.Count < targetCount)
        {
            var index = result.Count % result.Count;
            result.Add(result[index]);
        }

        // If we have too many, trim
        while (result.Count > targetCount)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }
}

