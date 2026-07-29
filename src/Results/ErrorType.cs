namespace Results;

/// <summary>
/// Named failure shapes the domain knows how to act on.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// The zero value an uninitialized <c>default(Error)</c> reads as, and the only way an instance carries it: no factory produces this type, and the
    /// <c>Result</c> factories reject any error that does carry it. It marks a bug, not a domain outcome. Never assign it deliberately.
    /// </summary>
    Undefined = 0,

    /// <summary>Input failed validation. Caller should fix the input and retry.</summary>
    Validation,

    /// <summary>Resource was not found. May or may not have ever existed.</summary>
    NotFound,

    /// <summary>Resource existed but has been deleted. Distinct from <see cref="NotFound"/>.</summary>
    Gone,

    /// <summary>Operation would violate a uniqueness or version invariant.</summary>
    Conflict,
}
