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

        // Calculate horizontal proportions (left-to-right orientation)
        var centerY = bounds.Height / 2;
        var leftX = (int)(bounds.Width * 0.1);
        var rightX = (int)(bounds.Width * 0.9);

        // Allocate icons:
        // - Circular balls on the left (allocate proportionally, minimum 4 per ball for circular shape)
        // - Remaining icons for 2-column shaft + head
        var minIconsPerBall = 4;
        var totalBallIcons = Math.Max(minIconsPerBall * 2, (int)(iconCount * 0.2)); // At least 8 icons for balls, or 20% of total
        var iconsForShaftAndHead = iconCount - totalBallIcons;
        
        // Ensure we have enough icons for shaft and head
        if (iconsForShaftAndHead < 3)
        {
            totalBallIcons = iconCount - 3;
            iconsForShaftAndHead = 3;
        }

        var iconsPerBall = totalBallIcons / 2;
        var leftBallIcons = iconsPerBall;
        var rightBallIcons = totalBallIcons - leftBallIcons;

        // Generate circular balls on the left
        var ballRadius = Math.Min(bounds.Width, bounds.Height) / 12;
        var ballCenterX = leftX + ballRadius;
        var topBallCenterY = centerY - ballRadius;
        var bottomBallCenterY = centerY + ballRadius;

        // Generate left (top) ball circle
        for (int i = 0; i < leftBallIcons; i++)
        {
            var angle = 2.0 * Math.PI * i / leftBallIcons;
            var x = ballCenterX + (int)(ballRadius * Math.Cos(angle));
            var y = topBallCenterY + (int)(ballRadius * Math.Sin(angle));
            points.Add(new Point(x, y));
        }

        // Generate right (bottom) ball circle
        for (int i = 0; i < rightBallIcons; i++)
        {
            var angle = 2.0 * Math.PI * i / rightBallIcons;
            var x = ballCenterX + (int)(ballRadius * Math.Cos(angle));
            var y = bottomBallCenterY + (int)(ballRadius * Math.Sin(angle));
            points.Add(new Point(x, y));
        }

        // Generate shaft (2 columns extending right)
        var shaftStartX = ballCenterX + ballRadius;
        var shaftEndX = rightX - (int)(bounds.Width * 0.08); // Leave room for head
        var shaftLength = shaftEndX - shaftStartX;
        var shaftColumnOffset = ballRadius / 2; // Offset for the two columns
        var iconsForShaft = Math.Max(2, iconsForShaftAndHead - 3); // Reserve some for head, ensure at least 2 for 2 columns

        if (iconsForShaft >= 2)
        {
            // Distribute icons across 2 columns
            var iconsPerColumn = iconsForShaft / 2;
            var remainder = iconsForShaft % 2;

            // First column (top)
            var firstColumnCount = iconsPerColumn + remainder;
            for (int i = 0; i < firstColumnCount; i++)
            {
                var x = shaftStartX;
                var y = centerY - shaftColumnOffset;
                if (firstColumnCount == 1)
                {
                    x = shaftStartX + shaftLength / 2;
                }
                else
                {
                    x = shaftStartX + (int)((double)i / (firstColumnCount - 1) * shaftLength);
                }
                points.Add(new Point(x, y));
            }

            // Second column (bottom)
            for (int i = 0; i < iconsPerColumn; i++)
            {
                var x = shaftStartX;
                var y = centerY + shaftColumnOffset;
                if (iconsPerColumn == 1)
                {
                    x = shaftStartX + shaftLength / 2;
                }
                else
                {
                    x = shaftStartX + (int)((double)i / (iconsPerColumn - 1) * shaftLength);
                }
                points.Add(new Point(x, y));
            }
        }
        else
        {
            // Fallback: single point in each column
            points.Add(new Point(shaftStartX + shaftLength / 2, centerY - shaftColumnOffset));
            points.Add(new Point(shaftStartX + shaftLength / 2, centerY + shaftColumnOffset));
        }

        // Generate head (curved segment on the right - horizontal ellipse arc)
        var headCenterX = rightX;
        var headWidth = (int)(bounds.Width * 0.06);
        var headHeight = ballRadius * 1.2;
        var iconsForHead = iconsForShaftAndHead - iconsForShaft;

        if (iconsForHead > 0)
        {
            for (int i = 0; i < iconsForHead; i++)
            {
                // Generate points along the right side of an ellipse (vertical arc)
                var angle = Math.PI / 2 + Math.PI * i / (iconsForHead + 1); // PI/2 to 3*PI/2 (right half of ellipse)
                var x = headCenterX - (int)(headWidth * Math.Cos(angle));
                var y = centerY + (int)(headHeight * Math.Sin(angle));
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

