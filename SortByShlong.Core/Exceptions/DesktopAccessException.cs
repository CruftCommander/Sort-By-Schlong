namespace SortBySchlong.Core.Exceptions;

/// <summary>
/// Exception thrown when the desktop window cannot be accessed.
/// </summary>
public class DesktopAccessException : IconArrangementException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesktopAccessException"/> class.
    /// </summary>
    public DesktopAccessException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DesktopAccessException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DesktopAccessException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DesktopAccessException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DesktopAccessException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

