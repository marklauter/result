namespace Results;

/// <summary>
/// Machine-readable codes for the <see cref="Error"/>s the library itself synthesizes, declared once so callers match on the constant instead of quoting the
/// literal.
/// </summary>
public static class ErrorCodes
{
    /// <summary>
    /// Code of the <see cref="ErrorType.InvalidOperation"/> error synthesized for a null element in
    /// <see cref="ResultSequence.Sequence{T}(IEnumerable{Result{T}})"/>.
    /// </summary>
    public const string SequenceNullInput = "sequence.null_input";

    /// <summary>
    /// Code of the <see cref="ErrorType.InvalidOperation"/> error synthesized for a null element in the variadic
    /// <see cref="Result.Apply(ReadOnlySpan{Result{Unit}})"/>.
    /// </summary>
    public const string ApplyNullInput = "apply.null_input";
}
