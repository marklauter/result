using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Results;

/// <summary>
/// A discriminated result of a domain operation: either a successful <typeparamref name="T"/> value (<see cref="Result{T}.Success"/>) or a non-empty
/// collection of <see cref="Error"/>s (<see cref="Result{T}.Failure"/>). Consume the result with <see cref="Match"/>, transform it with <see cref="Map"/>, and
/// chain it with <see cref="Bind"/> or <see cref="BindAsync"/>; once a chain is asynchronous it continues through the <see cref="ResultAsync"/> extensions on
/// <see cref="ValueTask{TResult}"/>. <see cref="Select"/> and the
/// <see cref="SelectMany{TIntermediate, TResult}(Func{T, Result{TIntermediate}}, Func{T, TIntermediate, TResult})"/> shapes are LINQ-named aliases of those
/// operations. Combine independent results with <see cref="Result.Apply{T, TResult}(Result{System.Func{T, TResult}}, Result{T})"/>, the applicative overload
/// that accumulates errors on the failure path, or with the Unit-keyed <c>Result.Apply</c> overload for effect sequencing. The hierarchy is closed:
/// <see cref="Result{T}.Success"/> and <see cref="Result{T}.Failure"/> are its only inhabitants. Dispatch is virtual, so each inhabitant implements the
/// combinators and exhaustiveness is a compile-time fact rather than a runtime switch.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Discriminated-union shape: Success and Failure are inhabitants of Result<T>; nesting names the relationship in the type itself.")]
public abstract record Result<T>
{
    private protected Result() { }

    /// <summary>
    /// The successful inhabitant of <see cref="Result{T}"/>. Equality is the record-synthesized comparison over <see cref="Value"/>, so it delegates to
    /// <c>EqualityComparer&lt;T&gt;.Default</c>: two successes are equal exactly when their payloads are, and a payload without value semantics — an array, a
    /// <see cref="List{T}"/>, an <see cref="ImmutableArray{T}"/> — compares by reference here just as it does anywhere else. The payload's owner defines its
    /// equality; <see cref="Failure"/> hand-writes its own only because its payload is library-owned.
    /// </summary>
    public sealed record Success(T Value)
        : Result<T>
    {
        /// <inheritdoc/>
        public override TResult Match<TResult>(Func<T, TResult> onSuccess, Func<ImmutableArray<Error>, TResult> onError) => onSuccess(Value);

        /// <inheritdoc/>
        public override Result<TResult> Map<TResult>(Func<T, TResult> fn) => new Result<TResult>.Success(fn(Value));

        /// <inheritdoc/>
        public override Result<TResult> Bind<TResult>(Func<T, Result<TResult>> fn) => fn(Value);

        /// <inheritdoc/>
        public override ValueTask<Result<TResult>> BindAsync<TResult>(Func<T, ValueTask<Result<TResult>>> fn) => fn(Value);
    }

    /// <summary>
    /// The failed inhabitant of <see cref="Result{T}"/>. <see cref="Errors"/> is always non-empty: the constructor is internal, so external construction goes
    /// through the <c>Result.Failure</c> factories that enforce it. Equality is structural over <see cref="Errors"/> (element-wise, order-sensitive), because
    /// <see cref="ImmutableArray{T}"/>'s default record equality would compare the underlying array reference instead.
    /// </summary>
    public sealed record Failure
        : Result<T>
    {
        /// <summary>The errors carried by this failure; never empty.</summary>
        public ImmutableArray<Error> Errors { get; }

        internal Failure(ImmutableArray<Error> errors) => Errors = errors;

        /// <inheritdoc/>
        public override TResult Match<TResult>(Func<T, TResult> onSuccess, Func<ImmutableArray<Error>, TResult> onError) => onError(Errors);

        /// <inheritdoc/>
        public override Result<TResult> Map<TResult>(Func<T, TResult> fn) => new Result<TResult>.Failure(Errors);

        /// <inheritdoc/>
        public override Result<TResult> Bind<TResult>(Func<T, Result<TResult>> fn) => new Result<TResult>.Failure(Errors);

        /// <inheritdoc/>
        public override ValueTask<Result<TResult>> BindAsync<TResult>(Func<T, ValueTask<Result<TResult>>> fn) => ValueTask.FromResult<Result<TResult>>(new Result<TResult>.Failure(Errors));

        /// <summary>
        /// Structural equality over <see cref="Errors"/>, element-wise and order-sensitive. Replaces the record-synthesized comparison, which would compare
        /// <see cref="ImmutableArray{T}"/>'s underlying array by reference.
        /// </summary>
        /// <param name="other">The failure to compare against.</param>
        /// <returns><see langword="true"/> when <paramref name="other"/> carries the same errors in the same order; otherwise <see langword="false"/>.</returns>
        public bool Equals(Failure? other) =>
            other is not null && Errors.AsSpan().SequenceEqual(other.Errors.AsSpan());

        /// <summary>Hash code combining <see cref="Errors"/> element-wise, consistent with <see cref="Equals(Failure)"/>.</summary>
        /// <returns>The combined hash code.</returns>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var error in Errors.AsSpan())
                hash.Add(error);
            return hash.ToHashCode();
        }
    }

    /// <summary>Pattern-matches the result and produces a value on either path.</summary>
    /// <returns>The value returned by <paramref name="onSuccess"/> for a success, or by <paramref name="onError"/> for a failure.</returns>
    public abstract TResult Match<TResult>(Func<T, TResult> onSuccess, Func<ImmutableArray<Error>, TResult> onError);

    /// <summary>
    /// Functor map. Transforms the success value with <paramref name="fn"/> and passes a failure through unchanged.
    /// </summary>
    /// <returns>A success holding the mapped value, or the original failure unchanged.</returns>
    public abstract Result<TResult> Map<TResult>(Func<T, TResult> fn);

    /// <summary>
    /// Monadic bind. Chains a result-returning function after a successful result and short-circuits on failure.
    /// </summary>
    /// <returns>The result of <paramref name="fn"/> applied to the success value, or the original failure unchanged.</returns>
    public abstract Result<TResult> Bind<TResult>(Func<T, Result<TResult>> fn);

    /// <summary>
    /// Async monadic bind, the entry point into a <see cref="ValueTask{TResult}"/> chain from a synchronous result. Chains a result-returning async function
    /// after a successful result and short-circuits on failure. <see cref="ValueTask{TResult}"/> is the async currency of the whole surface, so a continuation
    /// that completes synchronously — a cache hit, a memoized read — allocates nothing; a continuation holding a <see cref="Task{TResult}"/> wraps it with
    /// <c>new ValueTask&lt;Result&lt;TResult&gt;&gt;(task)</c>. Cancellation is the responsibility of <paramref name="fn"/>: the API threads no
    /// <see cref="System.Threading.CancellationToken"/>, so a caller needing cancellation passes a lambda that captures the token from its enclosing scope.
    /// </summary>
    /// <returns>A value task for the result of <paramref name="fn"/> applied to the success value, or the original failure unchanged.</returns>
    public abstract ValueTask<Result<TResult>> BindAsync<TResult>(Func<T, ValueTask<Result<TResult>>> fn);

    /// <summary>
    /// LINQ-named alias of <see cref="Map"/>. Transforms the success value with <paramref name="selector"/> and passes a failure through unchanged.
    /// </summary>
    /// <returns>A success holding the mapped value, or the original failure unchanged.</returns>
    public Result<TResult> Select<TResult>(Func<T, TResult> selector) => Map(selector);

    /// <summary>LINQ-named alias of <see cref="Bind"/>. Chains a result-returning function after a successful result and short-circuits on failure.</summary>
    /// <returns>The result of <paramref name="selector"/> applied to the success value, or the original failure unchanged.</returns>
    public Result<TResult> SelectMany<TResult>(Func<T, Result<TResult>> selector) => Bind(selector);

    /// <summary>
    /// Monadic bind with projection, the shape the compiler binds for chained <c>from</c> clauses in query syntax. It is equivalent to
    /// <c>Bind(x =&gt; selector(x).Map(y =&gt; resultSelector(x, y)))</c> and short-circuits on the first failure, so errors do not accumulate across clauses.
    /// Bind is sequential; use <c>Result.Apply</c> when independent failures should all be reported.
    /// </summary>
    /// <returns>The projected success value, or the first failure encountered.</returns>
    public Result<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, Result<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector) =>
        Bind(x => selector(x).Map(y => resultSelector(x, y)));
}

/// <summary>
/// Non-generic factories and combinators for <see cref="Result{T}"/>. Use <c>Result.Success(value)</c> and <c>Result.Failure&lt;T&gt;(error)</c> when you want
/// type inference at the construction site, and the <c>Result.Apply</c> overloads to combine independent results, accumulating all errors on the failure path.
/// </summary>
public static class Result
{
    /// <summary>Constructs a successful result. The type parameter is inferred from <paramref name="value"/>.</summary>
    /// <returns>A <see cref="Result{T}.Success"/> holding <paramref name="value"/>.</returns>
    public static Result<T> Success<T>(T value) => new Result<T>.Success(value);

    /// <summary>
    /// Constructs a failed result from a single <see cref="Error"/>. The success type <typeparamref name="T"/> must be supplied explicitly, because it cannot be
    /// inferred from <paramref name="error"/>.
    /// </summary>
    /// <returns>A <see cref="Result{T}.Failure"/> carrying <paramref name="error"/>.</returns>
    public static Result<T> Failure<T>(Error error) => new Result<T>.Failure([error]);

    /// <summary>
    /// Constructs a failed result from one or more <see cref="Error"/>s. <c>params ReadOnlySpan</c> keeps varargs argument lists off the heap. Collection
    /// expressions and pre-built <see cref="ImmutableArray{T}"/>s bind to the <see cref="ImmutableArray{T}"/> overload instead (see its
    /// <see cref="OverloadResolutionPriorityAttribute"/>).
    /// </summary>
    /// <returns>A <see cref="Result{T}.Failure"/> carrying the given <paramref name="errors"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty; a failure must carry at least one error.</exception>
    public static Result<T> Failure<T>(params ReadOnlySpan<Error> errors) => errors.Length == 0
        ? throw new ArgumentException("Failure requires at least one error.", nameof(errors))
        : new Result<T>.Failure([.. errors]);

    /// <summary>
    /// Constructs a failed result from a pre-built <see cref="ImmutableArray{T}"/> of <see cref="Error"/>s. This is the cheapest factory: the array is stored
    /// directly with no copy, and collection expressions (<c>Failure&lt;T&gt;([a, b])</c>) build it directly. The priority attribute breaks the tie with the
    /// <see cref="ReadOnlySpan{T}"/> overload that <see cref="ImmutableArray{T}"/>'s implicit span conversion would otherwise cause.
    /// </summary>
    /// <returns>A <see cref="Result{T}.Failure"/> carrying <paramref name="errors"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is default or empty; a failure must carry at least one error.</exception>
    [OverloadResolutionPriority(1)]
    public static Result<T> Failure<T>(ImmutableArray<Error> errors) => errors.IsDefaultOrEmpty
        ? throw new ArgumentException("Failure requires at least one error.", nameof(errors))
        : new Result<T>.Failure(errors);

    /// <summary>
    /// Constructs a failed result from a list of <see cref="Error"/>s.
    /// </summary>
    /// <returns>A <see cref="Result{T}.Failure"/> carrying the given <paramref name="errors"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty; a failure must carry at least one error.</exception>
    public static Result<T> Failure<T>(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return errors.Count == 0
            ? throw new ArgumentException("Failure requires at least one error.", nameof(errors))
            : new Result<T>.Failure([.. errors]);
    }

    /// <summary>
    /// Lifts a boolean check into a <see cref="Result{T}"/> of <see cref="Unit"/>. This is the entry point for applicative accumulation. Lift each independent
    /// check, then combine them with the variadic <see cref="Apply(ReadOnlySpan{Result{Unit}})"/> overload so every violated condition is reported, not just the
    /// first.
    /// </summary>
    /// <returns>A success of <see cref="Unit"/> when <paramref name="condition"/> holds, otherwise a failure carrying <paramref name="error"/>.</returns>
    public static Result<Unit> Validate(bool condition, Error error) =>
        condition ? Success(Unit.Value) : new Result<Unit>.Failure([error]);

    /// <summary>
    /// Applicative application: feeds a <see cref="Result{T}"/>-wrapped argument to a <see cref="Result{T}"/>-wrapped function. Currying handles multiple
    /// arities; apply repeatedly to consume each argument. When both <paramref name="resultFn"/> and <paramref name="resultArg"/> fail, their errors are
    /// accumulated (function errors first, then argument errors). This is Validation-applicative behavior rather than fail-fast.
    /// </summary>
    /// <returns>A success holding the applied value, or a failure accumulating the errors of whichever inputs failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resultFn"/> or <paramref name="resultArg"/> is <see langword="null"/>.</exception>
    public static Result<TResult> Apply<T, TResult>(Result<Func<T, TResult>> resultFn, Result<T> resultArg)
    {
        ArgumentNullException.ThrowIfNull(resultFn);
        ArgumentNullException.ThrowIfNull(resultArg);

        return (resultFn, resultArg) switch
        {
            (Result<Func<T, TResult>>.Success f, Result<T>.Success a) => Success(f.Value(a.Value)),
            (Result<Func<T, TResult>>.Failure f, Result<T>.Success _) => new Result<TResult>.Failure(f.Errors),
            (Result<Func<T, TResult>>.Success _, Result<T>.Failure a) => new Result<TResult>.Failure(a.Errors),
            // fourth row of the truth table, (Failure f, Failure a) => Failure([.. f.Errors, .. a.Errors]),
            // written as a discard: a final type-pattern would make the compiler synthesize unreachable
            // type-test/default branches under the switch, which coverlet counts and the branch-coverage
            // threshold then fails.
            _ => new Result<TResult>.Failure(
                [.. ((Result<Func<T, TResult>>.Failure)resultFn).Errors, .. ((Result<T>.Failure)resultArg).Errors]),
        };
    }

    /// <summary>
    /// Variadic effect sequencing: combines any number of <see cref="Result{T}"/>s of <see cref="Unit"/>. <c>params ReadOnlySpan</c> keeps the argument list off
    /// the heap at every arity, and the single-failure path returns the failing input unchanged rather than copying its errors.
    /// </summary>
    /// <returns>
    /// A success of <see cref="Unit"/> when every input succeeds or <paramref name="results"/> is empty (the identity element), otherwise a failure whose errors
    /// are accumulated across all failed inputs in input order.
    /// </returns>
    /// <exception cref="ArgumentException">An element of <paramref name="results"/> is <see langword="null"/> — a defect in the calling code, thrown at the
    /// boundary it crosses with the offending index in the message rather than counted as a passing check.</exception>
    public static Result<Unit> Apply(params ReadOnlySpan<Result<Unit>> results)
    {
        var failures = 0;
        var total = 0;
        var lastFailure = -1;
        for (var i = 0; i < results.Length; i++)
        {
            if (results[i] is Result<Unit>.Failure f)
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
            return Success(Unit.Value);

        if (failures == 1)
            return results[lastFailure];

        var builder = ImmutableArray.CreateBuilder<Error>(total);
        for (var i = 0; i < results.Length; i++)
        {
            if (results[i] is Result<Unit>.Failure f)
                builder.AddRange(f.Errors);
        }

        return new Result<Unit>.Failure(builder.MoveToImmutable());
    }
}
