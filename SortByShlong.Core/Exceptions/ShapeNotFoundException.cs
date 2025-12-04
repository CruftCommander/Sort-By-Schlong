namespace SortBySchlong.Core.Exceptions;

/// <summary>
/// Exception thrown when a requested shape is not found in the registry.
/// </summary>
public class ShapeNotFoundException : IconArrangementException
{
    /// <summary>
    /// Gets the shape key that was not found.
    /// </summary>
    public string ShapeKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShapeNotFoundException"/> class.
    /// </summary>
    /// <param name="shapeKey">The shape key that was not found.</param>
    public ShapeNotFoundException(string shapeKey) : base($"Shape '{shapeKey}' was not found in the registry.")
    {
        ShapeKey = shapeKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShapeNotFoundException"/> class with a specified error message.
    /// </summary>
    /// <param name="shapeKey">The shape key that was not found.</param>
    /// <param name="message">The message that describes the error.</param>
    public ShapeNotFoundException(string shapeKey, string message) : base(message)
    {
        ShapeKey = shapeKey;
    }
}

