using System;

namespace MiniNecesse.Core.Exceptions;

/// <summary>
/// Exception thrown when a saved game file is corrupted, unreadable,
/// missing required fields, or fails domain invariant validation.
/// </summary>
public sealed class SaveCorruptedException : Exception
{
    /// <summary>Gets the save slot index associated with the corrupted data, if applicable.</summary>
    public int? SlotIndex { get; }

    /// <summary>Initializes a new instance of <see cref="SaveCorruptedException"/>.</summary>
    public SaveCorruptedException(string message, int? slotIndex = null)
        : base(message)
    {
        SlotIndex = slotIndex;
    }

    /// <summary>Initializes a new instance of <see cref="SaveCorruptedException"/> with an inner exception.</summary>
    public SaveCorruptedException(string message, Exception innerException, int? slotIndex = null)
        : base(message, innerException)
    {
        SlotIndex = slotIndex;
    }
}
