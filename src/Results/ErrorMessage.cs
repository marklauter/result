using System.Numerics;

namespace Results;

/// <summary>
/// The human-readable message of an <see cref="Error"/>: prose for logs and error responses, free to be reworded, typed apart from the machine-readable
/// <see cref="ErrorCode"/> so transposing the two at a call site is a compile error instead of a silent defect. <see cref="Checked(string)"/> is the
/// fallible lift from <see cref="string"/>; <see cref="Unchecked(string)"/> is the total embedding for values the caller vouches for.
/// </summary>
public readonly record struct ErrorMessage
    : IComparable<ErrorMessage>
    , IComparisonOperators<ErrorMessage, ErrorMessage, bool>
{
    private const string InvalidCode = "err.error_message.invalid";

    /// <summary>The underlying string. The one projection back to the primitive.</summary>
    public string Value { get; }

    private ErrorMessage(string value) => Value = value;

    /// <summary>
    /// Checks <paramref name="value"/> into an <see cref="ErrorMessage"/>. Rejects null, empty, and whitespace; any other content passes for now, so
    /// stricter shape rules can tighten later without changing the signature.
    /// </summary>
    /// <returns>A success carrying the message, or an <see cref="ErrorType.Validation"/> failure with code <c>err.error_message.invalid</c>.</returns>
    public static Result<ErrorMessage> Checked(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Result.Failure<ErrorMessage>(Error.Validation(
                ErrorCode.Unchecked(InvalidCode),
                Unchecked("error message must not be null, empty, or whitespace")))
            : Result.Success(Unchecked(value));

    /// <summary>
    /// Wraps <paramref name="value"/> without validation: pure assignment, lawful only on values <see cref="Checked(string)"/> would accept. Passing
    /// anything else is the caller's defect, surfacing wherever the invalid message is next read.
    /// </summary>
    /// <returns>An <see cref="ErrorMessage"/> carrying <paramref name="value"/> as given.</returns>
    public static ErrorMessage Unchecked(string value) => new(value);

    /// <summary>Ordinal comparison over <see cref="Value"/>, so messages sort like the strings they wrap.</summary>
    /// <returns>A negative value, zero, or a positive value as this message sorts before, with, or after <paramref name="other"/>.</returns>
    public int CompareTo(ErrorMessage other) => string.CompareOrdinal(Value, other.Value);

    /// <summary>Ordinal comparison over <see cref="Value"/>.</summary>
    /// <returns><see langword="true"/> if <paramref name="left"/> sorts before <paramref name="right"/>.</returns>
    public static bool operator <(ErrorMessage left, ErrorMessage right) => left.CompareTo(right) < 0;

    /// <summary>Ordinal comparison over <see cref="Value"/>.</summary>
    /// <returns><see langword="true"/> if <paramref name="left"/> sorts before or with <paramref name="right"/>.</returns>
    public static bool operator <=(ErrorMessage left, ErrorMessage right) => left.CompareTo(right) <= 0;

    /// <summary>Ordinal comparison over <see cref="Value"/>.</summary>
    /// <returns><see langword="true"/> if <paramref name="left"/> sorts after <paramref name="right"/>.</returns>
    public static bool operator >(ErrorMessage left, ErrorMessage right) => left.CompareTo(right) > 0;

    /// <summary>Ordinal comparison over <see cref="Value"/>.</summary>
    /// <returns><see langword="true"/> if <paramref name="left"/> sorts after or with <paramref name="right"/>.</returns>
    public static bool operator >=(ErrorMessage left, ErrorMessage right) => left.CompareTo(right) >= 0;

    /// <summary>Renders the wrapped <see cref="Value"/> alone, so a message interpolates and logs exactly as the string it wraps.</summary>
    /// <returns><see cref="Value"/>.</returns>
    public override string ToString() => Value;
}
