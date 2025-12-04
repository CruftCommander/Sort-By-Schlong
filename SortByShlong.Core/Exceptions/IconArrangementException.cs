namespace SortBySchlong.Core.Exceptions;

/// <summary>
/// Base exception for icon arrangement failures.
/// </summary>
public class IconArrangementException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IconArrangementException"/> class.
    /// </summary>
    public IconArrangementException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IconArrangementException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public IconArrangementException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IconArrangementException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public IconArrangementException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

