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
    /// element). Otherwise a <see cref="Result{T}.Failure"/> carrying every error from every failed input, accumulated in input order.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An element of <paramref name="results"/> is <see langword="null"/> — a defect in the calling code, thrown at the
    /// boundary it crosses with the offending index in the message rather than modeled as a domain outcome.</exception>
    public static Result<ImmutableArray<T>> Sequence<T>(
        this IEnumerable<Result<T>> results)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(results);

        // Both builders are lazy: all-success never allocates errors, and a failure before the
        // first success never allocates values. Once errors exists the values builder's contents
        // are dead — the failure path discards them — so later successes stop feeding it. The
        // successes collected before the first failure are the irreducible waste of a single
        // pass, and a single pass is forced: enumerating an arbitrary IEnumerable twice is the
        // CA1851 defect.
        ImmutableArray<T>.Builder? values = null;
        ImmutableArray<Error>.Builder? errors = null;
        var index = 0;
        foreach (var result in results)
        {
            if (result is Result<T>.Success success)
            {
                if (errors is null)
                    (values ??= ImmutableArray.CreateBuilder<T>())
                        .Add(success.Value);
            }
            else if (result is Result<T>.Failure failure)
            {
                (errors ??= ImmutableArray.CreateBuilder<Error>())
                    .AddRange(failure.Errors);
            }
            else
            {
                throw new ArgumentException($"input at index {index} is null", nameof(results));
            }

            index++;
        }

        return errors is not null
            ? Result.Failure<ImmutableArray<T>>(errors.ToImmutable())
            : Result.Success(values?.ToImmutable() ?? []);
    }
}
