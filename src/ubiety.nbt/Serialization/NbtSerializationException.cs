namespace Ubiety.Nbt.Serialization;

/// <summary>
/// The exception thrown when an object cannot be converted to or from NBT, e.g. because a tag has the wrong type.
/// </summary>
public class NbtSerializationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="NbtSerializationException"/> class.</summary>
    public NbtSerializationException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NbtSerializationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public NbtSerializationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NbtSerializationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public NbtSerializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NbtSerializationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="path">The location of the error, e.g. <c>$.Inventory[3].Count</c>.</param>
    /// <param name="innerException">The exception that caused this one, if any.</param>
    public NbtSerializationException(string message, string path, Exception? innerException = null)
        : base($"{message} Path: {path}.", innerException)
    {
        Path = path;
    }

    /// <summary>Gets the location of the error, e.g. <c>$.Inventory[3].Count</c>.</summary>
    public string? Path { get; }
}
