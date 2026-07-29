using System.Collections.Immutable;
using System.Runtime.CompilerServices;

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
            ? new Result<ImmutableArray<T>>.Failure(errors.ToImmutable())
            : Result.Success(values?.ToImmutable() ?? []);
    }

    /// <summary>
    /// Sequences any number of results into a single result. <c>params ReadOnlySpan</c> keeps varargs argument lists off the heap, and the known count buys a
    /// two-pass shape: pass one classifies the inputs, pass two fills exactly one right-sized array, so nothing is built speculatively on either path and a
    /// single failing input's errors are reused rather than copied. Collection expressions and pre-built <see cref="ImmutableArray{T}"/>s bind to the
    /// <see cref="ImmutableArray{T}"/> overload instead (see its <see cref="OverloadResolutionPriorityAttribute"/>).
    /// </summary>
    /// <returns>
    /// A <see cref="Result{T}.Success"/> carrying every value in input order when all inputs succeed, and when <paramref name="results"/> is empty (the identity
    /// element). Otherwise a <see cref="Result{T}.Failure"/> carrying every error from every failed input, accumulated in input order.
    /// </returns>
    /// <exception cref="ArgumentException">An element of <paramref name="results"/> is <see langword="null"/> — a defect in the calling code, thrown at the
    /// boundary it crosses with the offending index in the message, from pass one, before anything is allocated.</exception>
    public static Result<ImmutableArray<T>> Sequence<T>(params ReadOnlySpan<Result<T>> results)
        where T : notnull
    {
        var failures = 0;
        var total = 0;
        var lastFailure = -1;
        for (var i = 0; i < results.Length; i++)
        {
            if (results[i] is Result<T>.Failure f)
            {
                failures++;
                total += f.Errors.Length;
                lastFailure = i;
            }
            else if (results[i] is null)
            {
                throw new ArgumentException($"input at index {i} is null", nameof(results));
            }
        }

        if (failures == 0)
        {
            var values = ImmutableArray.CreateBuilder<T>(results.Length);
            for (var i = 0; i < results.Length; i++)
            {
                // Pass one proved every element is a Success; a cast emits no branch for the coverage ratchet where a pattern match would leave a dead arm.
                values.Add(((Result<T>.Success)results[i]).Value);
            }

            return Result.Success(values.MoveToImmutable());
        }

        if (failures == 1)
            return new Result<ImmutableArray<T>>.Failure(((Result<T>.Failure)results[lastFailure]).Errors);

        var errors = ImmutableArray.CreateBuilder<Error>(total);
        for (var i = 0; i < results.Length; i++)
        {
            if (results[i] is Result<T>.Failure f)
                errors.AddRange(f.Errors);
        }

        return new Result<ImmutableArray<T>>.Failure(errors.MoveToImmutable());
    }

    /// <summary>
    /// Sequences a pre-built <see cref="ImmutableArray{T}"/> of results. Collection expressions (<c>[a, b].Sequence()</c>) build it directly. The priority
    /// attribute breaks the tie with the <see cref="ReadOnlySpan{T}"/> overload that <see cref="ImmutableArray{T}"/>'s implicit span conversion would otherwise
    /// cause.
    /// </summary>
    /// <returns>
    /// A <see cref="Result{T}.Success"/> carrying every value in input order when all inputs succeed, and when <paramref name="results"/> is empty (the identity
    /// element). Otherwise a <see cref="Result{T}.Failure"/> carrying every error from every failed input, accumulated in input order.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="default"/> — that struct's null, mapped to the same channel as a
    /// null <see cref="IEnumerable{T}"/> rather than the <see cref="NullReferenceException"/> enumeration would produce.</exception>
    /// <exception cref="ArgumentException">An element of <paramref name="results"/> is <see langword="null"/>; the message carries the offending index.</exception>
    [OverloadResolutionPriority(1)]
    public static Result<ImmutableArray<T>> Sequence<T>(this ImmutableArray<Result<T>> results)
        where T : notnull
        => results.IsDefault
            ? throw new ArgumentNullException(nameof(results))
            : Sequence(results.AsSpan());
}
