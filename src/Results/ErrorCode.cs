using System.Numerics;

namespace Results;

/// <summary>
/// The machine-readable identifier of an <see cref="Error"/> (for example, "fact.not_found"): a stable code callers branch on, typed apart from the
/// human-readable <see cref="ErrorMessage"/> so transposing the two at a call site is a compile error instead of a silent defect.
/// <see cref="Checked(string)"/> is the fallible lift from <see cref="string"/>; <see cref="Unchecked(string)"/> is the total embedding for values the
/// caller vouches for.
/// </summary>
public readonly record struct ErrorCode
    : IComparable<ErrorCode>
    , IComparisonOperators<ErrorCode, ErrorCode, bool>
{
    private const string InvalidCode = "err.error_code.invalid";

    /// <summary>The underlying string. The one projection back to the primitive.</summary>
    public string Value { get; }

    private ErrorCode(string value) => Value = value;

    /// <summary>
    /// Checks <paramref name="value"/> into an <see cref="ErrorCode"/>. Rejects null, empty, and whitespace; any other content passes for now, so
    /// stricter shape rules (for example, dotted-slug codes) can tighten later without changing the signature.
    /// </summary>
    /// <returns>A success carrying the code, or an <see cref="ErrorType.Validation"/> failure with code <c>err.error_code.invalid</c>.</returns>
    public static Result<ErrorCode> Checked(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Result.Failure<ErrorCode>(Error.Validation(
                Unchecked(InvalidCode),
                ErrorMessage.Unchecked("error code must not be null, empty, or whitespace")))
            : Result.Success(Unchecked(value));

    /// <summary>
    /// Wraps <paramref name="value"/> without validation: pure assignment, lawful only on values <see cref="Checked(string)"/> would accept. Passing
    /// anything else is the caller's defect, surfacing wherever the invalid code is next read.
    /// </summary>
    /// <returns>An <see cref="ErrorCode"/> carrying <paramref name="value"/> as given.</returns>
    public static ErrorCode Unchecked(string value) => new(value);

    /// <summary>Ordinal comparison over <see cref="Value"/>, so codes sort like the strings they wrap.</summary>
    /// <returns>A negative value, zero, or a positive value as this code sorts before, with, or after <paramref name="other"/>.</returns>
    public int CompareTo(ErrorCode other) => string.CompareOrdinal(Value, other.Value);

    /// <summary>Ordinal comparison over <see cref="Value"/>.</summary>
    /// <returns><see langword="true"/> if <paramref name="left"/> sorts before <paramref name="right"/>.</returns>
    public static bool operator <(ErrorCode left, ErrorCode right) => left.CompareTo(right) < 0;

    /// <summary>Ordinal comparison over <see cref="Value"/>.</summary>
    /// <returns><see langword="true"/> if <paramref name="left"/> sorts before or with <paramref name="right"/>.</returns>
    public static bool operator <=(ErrorCode left, ErrorCode right) => left.CompareTo(right) <= 0;

    /// <summary>Ordinal comparison over <see cref="Value"/>.</summary>
    /// <returns><see langword="true"/> if <paramref name="left"/> sorts after <paramref name="right"/>.</returns>
    public static bool operator >(ErrorCode left, ErrorCode right) => left.CompareTo(right) > 0;

    /// <summary>Ordinal comparison over <see cref="Value"/>.</summary>
    /// <returns><see langword="true"/> if <paramref name="left"/> sorts after or with <paramref name="right"/>.</returns>
    public static bool operator >=(ErrorCode left, ErrorCode right) => left.CompareTo(right) >= 0;

    /// <summary>Renders the wrapped <see cref="Value"/> alone, so a code interpolates and logs exactly as the string it wraps.</summary>
    /// <returns><see cref="Value"/>.</returns>
    public override string ToString() => Value;
}
