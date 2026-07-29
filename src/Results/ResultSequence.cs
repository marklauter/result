using System.Collections.Immutable;

namespace Results;

/// <summary>
/// Applicative sequencing for collections of results. All successes yield the collected array. Any failure yields every error accumulated in input order. This
/// is the collection-shaped counterpart of the <see cref="Result.Apply{T, TResult}(Result{System.Func{T, TResult}}, Result{T})"/> applicative: a batch of
/// independent parses reports every error in one pass rather than stopping at the first.
/// </summary>
public static class ResultSequence
{
    /// <summary>Sequences <paramref name="results"/> into a single result.</summary>
    /// <returns>
    /// A <see cref="Result{T}.Success"/> carrying every value in input order when all inputs succeed, and when <paramref name="results"/> is empty (the identity
    /// element). Otherwise a <see cref="Result{T}.Failure"/> carrying every error from every failed input, accumulated in input order. A null element is a
    /// caller defect surfaced as a value rather than dropped: it contributes an <see cref="ErrorType.InvalidOperation"/> error with code
    /// <see cref="ErrorCodes.SequenceNullInput"/> whose message names the input index, accumulated in order with the rest, so a defective batch reports
    /// failure instead of a shortened success.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="null"/>.</exception>
    public static Result<ImmutableArray<T>> Sequence<T>(
        this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var values = ImmutableArray.CreateBuilder<T>();
        var errors = ImmutableArray.CreateBuilder<Error>();
        var index = 0;
        foreach (var result in results)
        {
            if (result is Result<T>.Success success)
                values.Add(success.Value);
            else if (result is Result<T>.Failure failure)
                errors.AddRange(failure.Errors);
            else
                errors.Add(Error.InvalidOperation(ErrorCodes.SequenceNullInput, $"input at index {index} is null"));
            index++;
        }

        return errors.Count > 0
            ? Result.Failure<ImmutableArray<T>>(errors.ToImmutable())
            : Result.Success(values.ToImmutable());
    }
}
