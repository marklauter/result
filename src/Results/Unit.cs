namespace Results;

/// <summary>
/// The single-inhabitant type, the "no meaningful value" marker. Used as the success payload of <see cref="Result{T}"/> when an operation succeeds but
/// produces nothing to return (effects, validations, side-effecting writes).
/// </summary>
public readonly record struct Unit
{
    /// <summary>The singleton <see cref="Unit"/> value. Equal to <c>default(Unit)</c>.</summary>
    public static Unit Value { get; }
}
