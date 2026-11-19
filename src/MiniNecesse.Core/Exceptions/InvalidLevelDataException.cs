using System;

namespace MiniNecesse.Core.Exceptions;

/// <summary>
/// Exception thrown when an island level definition contains malformed schema,
/// invalid grid dimensions, or missing spawn configuration.
/// </summary>
public sealed class InvalidLevelDataException : Exception
{
    /// <summary>Gets the identifier of the invalid island, if available.</summary>
    public string? IslandId { get; }

    /// <summary>Initializes a new instance of <see cref="InvalidLevelDataException"/>.</summary>
    public InvalidLevelDataException(string message, string? islandId = null)
        : base(message)
    {
        IslandId = islandId;
    }

    /// <summary>Initializes a new instance of <see cref="InvalidLevelDataException"/> with an inner exception.</summary>
    public InvalidLevelDataException(string message, Exception innerException, string? islandId = null)
        : base(message, innerException)
    {
        IslandId = islandId;
    }
}
