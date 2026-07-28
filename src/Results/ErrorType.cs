namespace Results;

/// <summary>
/// Named failure shapes the domain knows how to act on.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Failure outside the domain's named cases, including an uninitialized <c>default(Error)</c> whose type reads as this zero value. Treated as a bug, not a
    /// domain outcome.
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
