namespace Ubiety.Nbt;

/// <summary>
/// The exception thrown when NBT or SNBT data is malformed or cannot be encoded.
/// </summary>
public class NbtFormatException : FormatException
{
    /// <summary>Initializes a new instance of the <see cref="NbtFormatException"/> class.</summary>
    public NbtFormatException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NbtFormatException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public NbtFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NbtFormatException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public NbtFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
