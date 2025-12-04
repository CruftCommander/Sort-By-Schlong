namespace SortBySchlong.Core.Exceptions;

/// <summary>
/// Exception thrown when layout validation fails.
/// </summary>
public class InvalidLayoutException : IconArrangementException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidLayoutException"/> class.
    /// </summary>
    public InvalidLayoutException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidLayoutException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InvalidLayoutException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidLayoutException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InvalidLayoutException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

