using CsCheck;
using System.Collections.Immutable;
using static Results.Tests.PropertyCheck;

namespace Results.Tests;

/// <summary>
/// Property-based tests for <see cref="ResultTraverse"/>. The examples in <see cref="ResultTraverseTests"/> pin the contract at hand-picked inputs; these
/// state it universally, over generated sources and a generated Kleisli arrow that fails on a residue class, so the failing element lands at every
/// position rather than the one an example author thought to write. Three overloads implement the same contract independently, and only generated
/// multi-failure input separates a drifted one from a correct one — a single-failure source cannot tell short-circuiting from accumulation.
/// <para>
/// These properties supply their own equality. <see cref="ResultTraverse.Traverse{T, TResult}(IEnumerable{T}, Func{T, Result{TResult}})"/> returns
/// <c>Result&lt;ImmutableArray&lt;T&gt;&gt;</c>, and <c>Result&lt;T&gt;.Success</c> delegates to <c>EqualityComparer&lt;T&gt;.Default</c>, which compares
/// an <see cref="ImmutableArray{T}"/> by its underlying array reference; <c>==</c> would therefore be false for two structurally identical successes and
/// every property would fail on its first all-success input. <see cref="Equivalent{T}"/> provides element-wise comparison through <c>Match</c>.
/// </para>
/// </summary>
public sealed class ResultTraverseLawTests
{
    /// <summary>Sources of zero to five elements: enough to place a failure at an interior position, with the empty and single-element edges included.</summary>
    private static readonly Gen<int[]> Sources = ResultGen.AnyInt.Array[0, 5];

    private static readonly Gen<(int[], Func<int, Result<int>>)> SourceAndArrow =
        Gen.Select(Sources, ResultGen.IntToResultInt);

    // The defining property, stated against an oracle written the other way round: LINQ's lazy Select
    // composed with First, rather than a loop that collects. An implementation that accumulated would
    // agree with this on every single-failure input and part from it on the rest.
    [Fact]
    public void Traverse_ReturnsTheFirstFailure_OrEveryMappedValueInOrder() =>
        Law(SourceAndArrow, t =>
        {
            var (source, fn) = t;
            var results = source.Select(fn);
            var expected = results.OfType<Result<int>.Failure>().FirstOrDefault() is { } failure
                ? new Result<ImmutableArray<int>>.Failure(failure.Errors)
                : Result.Success(results.Cast<Result<int>.Success>().Select(static s => s.Value).ToImmutableArray());

            return Equivalent(source.Traverse(fn), expected);
        });

    // Short-circuiting is a claim about work not done, which no assertion on the returned value can
    // make: an implementation that ran fn to the end and then discarded the extra failures returns the
    // same result. Count the calls.
    [Fact]
    public void Traverse_InvokesFnOncePerElementThroughTheFirstFailure() =>
        Law(SourceAndArrow, t =>
        {
            var (source, fn) = t;
            var calls = 0;

            _ = source.Traverse(x =>
            {
                calls++;
                return fn(x);
            });

            var firstFailure = Array.FindIndex(source, x => fn(x) is Result<int>.Failure);
            return calls == (firstFailure < 0 ? source.Length : firstFailure + 1);
        });

    // The three overloads are three separate loops over the same contract. Nothing but this property
    // forces them to agree.
    [Fact]
    public void Traverse_OverloadsAgree() =>
        Law(SourceAndArrow, t =>
        {
            var (source, fn) = t;
            var viaEnumerable = source.Traverse(fn);
            var viaSpan = new ReadOnlySpan<int>(source).Traverse(fn);
            var viaImmutableArray = source.ToImmutableArray().Traverse(fn);

            return Equivalent(viaEnumerable, viaSpan) && Equivalent(viaEnumerable, viaImmutableArray);
        });

    // Traverse is the fail-fast half and Select-then-Sequence the accumulating half, so they agree
    // exactly on sources where nothing fails. An arrow that always succeeds is how that condition is
    // met for every generated source rather than for the ones that happen to avoid the residue class.
    [Fact]
    public void Traverse_AgreesWithSelectThenSequence_WhenEveryElementSucceeds() =>
        Law(Gen.Select(Sources, ResultGen.IntToInt), t =>
        {
            var (source, fn) = t;
            Result<int> Arrow(int x) => Result.Success(fn(x));

            return Equivalent(source.Traverse(Arrow), source.Select(Arrow).Sequence());
        });

    // Sequence is Traverse at the identity arrow — the identity that names the pair — on the sources
    // where the two agree, which for a source of already-computed results is those with at most one
    // failure to accumulate.
    [Fact]
    public void Sequence_IsTraverseAtIdentity_ForSourcesWithAtMostOneFailure() =>
        Law(ResultGen.IntResult.Array[0, 5], results =>
        {
            var trimmed = results
                .TakeWhile(static r => r is Result<int>.Success)
                .Concat(results.SkipWhile(static r => r is Result<int>.Success).Take(1))
                .ToArray();

            return Equivalent(trimmed.Traverse(static r => r), trimmed.Sequence());
        });

    /// <summary>
    /// Element-wise equality over both paths: successes by payload, failures by errors. The payload's owner defines its equality, and
    /// <see cref="ImmutableArray{T}"/>'s is reference equality, so a caller wanting value semantics is the one that has to supply them.
    /// </summary>
    private static bool Equivalent<T>(Result<ImmutableArray<T>> left, Result<ImmutableArray<T>> right)
        where T : notnull, IEquatable<T>
        => left.Match(
            leftValues => right.Match(
                rightValues => leftValues.AsSpan().SequenceEqual(rightValues.AsSpan()),
                static _ => false),
            leftErrors => right.Match(
                static _ => false,
                rightErrors => leftErrors.AsSpan().SequenceEqual(rightErrors.AsSpan())));
}
