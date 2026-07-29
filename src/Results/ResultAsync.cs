using System.Collections.Immutable;

namespace Results;

/// <summary>
/// Combinators over an in-flight <see cref="ValueTask{TResult}"/> of <see cref="Result{T}"/>, so a chain that starts at an async port stays a chain. A port
/// returning <c>ValueTask&lt;Result&lt;T&gt;&gt;</c> hands back a value the instance combinators cannot reach: <see cref="Result{T}.Bind"/> takes a
/// <see cref="Result{T}"/>, not a value task over one. These extensions close that gap — <see cref="MapAsync"/>, <see cref="BindAsync{T, TResult}(ValueTask{Result{T}}, Func{T, Result{TResult}})"/>
/// and its async-continuation overload, and <see cref="MatchAsync"/> — each awaiting the receiver exactly once, which is the one way a
/// <see cref="ValueTask{TResult}"/> may be consumed. The chain remains a value task throughout, so an adapter that completes synchronously allocates nothing.
/// </summary>
public static class ResultAsync
{
    /// <summary>
    /// Functor map over an awaited result. Transforms the success value with <paramref name="fn"/> and passes a failure through unchanged.
    /// </summary>
    /// <returns>A value task for a success holding the mapped value, or the original failure unchanged.</returns>
    public static async ValueTask<Result<TResult>> MapAsync<T, TResult>(this ValueTask<Result<T>> result, Func<T, TResult> fn)
        where T : notnull
        where TResult : notnull
        => (await result.ConfigureAwait(false)).Map(fn);

    /// <summary>
    /// Monadic bind over an awaited result with a synchronous continuation. Chains <paramref name="fn"/> after a successful result and short-circuits on failure.
    /// </summary>
    /// <returns>A value task for the result of <paramref name="fn"/> applied to the success value, or the original failure unchanged.</returns>
    public static async ValueTask<Result<TResult>> BindAsync<T, TResult>(this ValueTask<Result<T>> result, Func<T, Result<TResult>> fn)
        where T : notnull
        where TResult : notnull
        => (await result.ConfigureAwait(false)).Bind(fn);

    /// <summary>
    /// Monadic bind over an awaited result with an async continuation — the shape that composes one async port call onto the next. Cancellation is the
    /// responsibility of <paramref name="fn"/>, as it is for <see cref="Result{T}.BindAsync"/>.
    /// </summary>
    /// <returns>A value task for the result of <paramref name="fn"/> applied to the success value, or the original failure unchanged.</returns>
    public static async ValueTask<Result<TResult>> BindAsync<T, TResult>(this ValueTask<Result<T>> result, Func<T, ValueTask<Result<TResult>>> fn)
        where T : notnull
        where TResult : notnull
        => await (await result.ConfigureAwait(false)).BindAsync(fn).ConfigureAwait(false);

    /// <summary>Pattern-matches an awaited result and produces a value on either path, the terminal step of an async chain.</summary>
    /// <returns>A value task for the value returned by <paramref name="onSuccess"/> for a success, or by <paramref name="onError"/> for a failure.</returns>
    public static async ValueTask<TResult> MatchAsync<T, TResult>(
        this ValueTask<Result<T>> result,
        Func<T, TResult> onSuccess,
        Func<ImmutableArray<Error>, TResult> onError)
        where T : notnull
        where TResult : notnull
        => (await result.ConfigureAwait(false)).Match(onSuccess, onError);
}
