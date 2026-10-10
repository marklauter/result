using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Results;

/// <summary>
/// Traversal: maps a fallible function over a collection and turns the collection of results inside out, so <c>t a</c> and <c>a -&gt; f b</c> become
/// <c>f (t b)</c>. This is Haskell's <c>traverse</c> at <c>f = Either</c> — the shape whose <c>Applicative</c> short-circuits — so it stops at the first
/// failure: the mapping function is not invoked for the elements after it, and the returned failure carries that element's errors and no others.
/// <para>
/// That makes it the fail-fast half of a deliberate pair, and the half to reach for when the mapping is expensive or effectful — a lookup per element, a
/// call per row — because the work after the first failure is never done. When you want every element's failure reported instead, evaluate them all and
/// accumulate: <c>source.Select(fn).Sequence()</c>, the applicative spelling
/// (see <see cref="ResultSequence.Sequence{T}(IEnumerable{Result{T}})"/>). The two agree exactly when every element succeeds.
/// </para>
/// </summary>
public static class ResultTraverse
{
    /// <summary>Maps <paramref name="fn"/> over <paramref name="source"/>, collecting the values and stopping at the first failure.</summary>
    /// <returns>
    /// A <see cref="Result{T}.Success"/> carrying every mapped value in input order when <paramref name="fn"/> succeeds for every element, and when
    /// <paramref name="source"/> is empty (the identity element). Otherwise a <see cref="Result{T}.Failure"/> carrying the errors of the first failing
    /// element, with the elements after it left untouched.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="fn"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fn"/> returned <see langword="null"/> for an element — a defect in the calling code, thrown at
    /// the boundary it crosses with the offending index in the message rather than modeled as a domain outcome.</exception>
    public static Result<ImmutableArray<TResult>> Traverse<T, TResult>(
        this IEnumerable<T> source,
        Func<T, Result<TResult>> fn)
        where T : notnull
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(fn);

        // The builder is lazy: a failure before the first success never allocates it, and the empty
        // source never allocates at all. The successes collected before a failure are the irreducible
        // waste of a single pass, and a single pass is forced twice over — enumerating an arbitrary
        // IEnumerable twice is the CA1851 defect, and fn is the caller's function, so a pass that ran
        // it a second time would duplicate whatever it does.
        ImmutableArray<TResult>.Builder? values = null;
        var index = -1;
        foreach (var item in source)
        {
            index++;
            var result = fn(item);

            // The failing element's errors array is reused rather than copied: it is already an
            // immutable array owned by a Failure, and the returned failure is the same errors under a
            // different success type.
            if (result is Result<TResult>.Failure failure)
                return new Result<ImmutableArray<TResult>>.Failure(failure.Errors);

            if (result is not Result<TResult>.Success success)
                throw new ArgumentException($"fn returned null for the input at index {index}", nameof(fn));

            (values ??= ImmutableArray.CreateBuilder<TResult>())
                .Add(success.Value);
        }

        return Result.Success(values?.ToImmutable() ?? []);
    }

    /// <summary>
    /// Maps <paramref name="fn"/> over a span, stopping at the first failure. <see cref="ReadOnlySpan{T}"/> keeps a stack-allocated or sliced batch off the
    /// heap, and the known length presets the builder's capacity, so the all-success path hands its array off with
    /// <see cref="ImmutableArray{T}.Builder.MoveToImmutable"/> rather than copying. Collection expressions and pre-built <see cref="ImmutableArray{T}"/>s
    /// bind to the <see cref="ImmutableArray{T}"/> overload instead (see its <see cref="OverloadResolutionPriorityAttribute"/>).
    /// </summary>
    /// <returns>
    /// A <see cref="Result{T}.Success"/> carrying every mapped value in input order when <paramref name="fn"/> succeeds for every element, and when
    /// <paramref name="source"/> is empty (the identity element). Otherwise a <see cref="Result{T}.Failure"/> carrying the errors of the first failing
    /// element, with the elements after it left untouched.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="fn"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fn"/> returned <see langword="null"/> for an element; the message carries the offending index.</exception>
    public static Result<ImmutableArray<TResult>> Traverse<T, TResult>(
        this ReadOnlySpan<T> source,
        Func<T, Result<TResult>> fn)
        where T : notnull
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(fn);

        // The length is known here, but it does not buy the two-pass classify-then-fill shape the span
        // overloads of Sequence use: classifying means running fn, and running it a second time is not
        // the library's call to make. What it does buy is an exactly-sized builder, allocated only once
        // an element succeeds — never speculatively, so a failure at index 0 allocates nothing.
        ImmutableArray<TResult>.Builder? values = null;
        for (var i = 0; i < source.Length; i++)
        {
            var result = fn(source[i]);
            if (result is Result<TResult>.Failure failure)
                return new Result<ImmutableArray<TResult>>.Failure(failure.Errors);

            if (result is not Result<TResult>.Success success)
                throw new ArgumentException($"fn returned null for the input at index {i}", nameof(fn));

            (values ??= ImmutableArray.CreateBuilder<TResult>(source.Length))
                .Add(success.Value);
        }

        // The loop ran to completion, so every element succeeded and the builder's count equals the
        // preset capacity: MoveToImmutable hands the array off without a copy.
        return Result.Success(values?.MoveToImmutable() ?? []);
    }

    /// <summary>
    /// Maps <paramref name="fn"/> over a pre-built <see cref="ImmutableArray{T}"/>, stopping at the first failure. Collection expressions
    /// (<c>[a, b].Traverse(fn)</c>) build it directly. The priority attribute breaks the tie with the <see cref="ReadOnlySpan{T}"/> overload that
    /// <see cref="ImmutableArray{T}"/>'s implicit span conversion would otherwise cause.
    /// </summary>
    /// <returns>
    /// A <see cref="Result{T}.Success"/> carrying every mapped value in input order when <paramref name="fn"/> succeeds for every element, and when
    /// <paramref name="source"/> is empty (the identity element). Otherwise a <see cref="Result{T}.Failure"/> carrying the errors of the first failing
    /// element, with the elements after it left untouched.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="default"/> — that struct's null, mapped to the same channel as a
    /// null <see cref="IEnumerable{T}"/> rather than the <see cref="NullReferenceException"/> enumeration would produce — or <paramref name="fn"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fn"/> returned <see langword="null"/> for an element; the message carries the offending index.</exception>
    [OverloadResolutionPriority(1)]
    public static Result<ImmutableArray<TResult>> Traverse<T, TResult>(
        this ImmutableArray<T> source,
        Func<T, Result<TResult>> fn)
        where T : notnull
        where TResult : notnull
        => source.IsDefault
            ? throw new ArgumentNullException(nameof(source))
            : Traverse(source.AsSpan(), fn);
}
