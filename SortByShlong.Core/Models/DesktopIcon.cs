namespace SortBySchlong.Core.Models;

/// <summary>
/// Represents a desktop icon with its position and optional text.
/// </summary>
/// <param name="Index">The zero-based index of the icon in the desktop ListView.</param>
/// <param name="Position">The current position of the icon on the desktop.</param>
/// <param name="Text">Optional text/label associated with the icon.</param>
public record DesktopIcon(int Index, Point Position, string? Text = null);

