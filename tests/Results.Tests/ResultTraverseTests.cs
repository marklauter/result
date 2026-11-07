using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Results.Tests;

public sealed class ResultTraverseTests
{
    private static Result<int> Parse(string s) =>
        int.TryParse(s, out var n)
            ? Result.Success(n)
            : Result.Failure<int>(Error.Validation(ErrorCode.Unchecked($"parse.{s}"), ErrorMessage.Unchecked($"'{s}' is not a number")));

    [Fact]
    public void Traverse_AllSuccesses_ReturnsMappedValuesInInputOrder()
    {
        IEnumerable<string> source = ["1", "2", "3"];

        var success = Assert.IsType<Result<ImmutableArray<int>>.Success>(source.Traverse(Parse));
        Assert.Equal([1, 2, 3], success.Value);
    }

    [Fact]
    public void Traverse_Empty_ReturnsEmptySuccess()
    {
        IEnumerable<string> source = [];

        var success = Assert.IsType<Result<ImmutableArray<int>>.Success>(source.Traverse(Parse));
        Assert.Empty(success.Value);
    }

    // Short-circuit: the first failure is the whole answer. The later failing element's errors are
    // absent because fn never ran for it — this is the contract that separates Traverse from
    // Select-then-Sequence, and the one an accumulating implementation would silently pass on
    // single-failure input alone.
    [Fact]
    public void Traverse_MultipleFailures_ReturnsOnlyTheFirstFailuresErrors()
    {
        IEnumerable<string> source = ["x", "1", "y"];

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(source.Traverse(Parse));
        Assert.Equal("parse.x", Assert.Single(failure.Errors).Code.Value);
    }

    [Fact]
    public void Traverse_Failure_CarriesEveryErrorOfThatElement()
    {
        IEnumerable<int> source = [0];

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(source.Traverse(static n => Result.Failure<int>(
            Error.Validation(ErrorCode.Unchecked($"err.{n}a"), ErrorMessage.Unchecked("a")),
            Error.Validation(ErrorCode.Unchecked($"err.{n}b"), ErrorMessage.Unchecked("b")))));

        Assert.Equal(["err.0a", "err.0b"], failure.Errors.Select(error => error.Code.Value));
    }

    // The point of short-circuiting: the work after the first failure is never done. An expensive fn —
    // a lookup per element, a call per row — stops at the failing element rather than running to the
    // end of the source.
    [Fact]
    public void Traverse_Failure_StopsInvokingFn()
    {
        var calls = 0;
        IEnumerable<string> source = ["1", "x", "2", "y"];

        _ = source.Traverse(s =>
        {
            calls++;
            return Parse(s);
        });

        Assert.Equal(2, calls);
    }

    // A lazily enumerated source is not drained past the failing element either: short-circuiting is a
    // property of the enumeration, not just of the delegate calls.
    [Fact]
    public void Traverse_Failure_StopsEnumeratingTheSource()
    {
        var pulled = 0;

        IEnumerable<string> Source()
        {
            foreach (var s in new[] { "1", "x", "2" })
            {
                pulled++;
                yield return s;
            }
        }

        _ = Source().Traverse(Parse);

        Assert.Equal(2, pulled);
    }

    // The failing element's errors array is reused rather than copied: same backing array, not an
    // equal one. The returned failure is those errors under a different success type.
    [Fact]
    public void Traverse_Failure_ReusesItsErrorsArray()
    {
        var failed = Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom")));
        IEnumerable<int> source = [0, 1];

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(
            source.Traverse(n => n == 0 ? Result.Success(n) : failed));

        Assert.Same(
            ImmutableCollectionsMarshal.AsArray(((Result<int>.Failure)failed).Errors),
            ImmutableCollectionsMarshal.AsArray(failure.Errors));
    }

    [Fact]
    public void Traverse_NullSource_ThrowsArgumentNull() =>
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => default(IEnumerable<string>)!.Traverse(Parse)).ParamName);

    [Fact]
    public void Traverse_NullFn_ThrowsArgumentNull()
    {
        IEnumerable<string> source = ["1"];

        Assert.Equal("fn", Assert.Throws<ArgumentNullException>(() => source.Traverse(default(Func<string, Result<int>>)!)).ParamName);
    }

    // A null return from fn is a defect in the calling code, not a domain outcome, so it throws at the
    // boundary it crosses with the offending index in the message — the same contract Sequence states
    // for a null element.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Traverse_FnReturnsNull_ThrowsNamingItsIndex(int index)
    {
        IEnumerable<int> source = [0, 1, 2];

        var ex = Assert.Throws<ArgumentException>(() => source.Traverse(n => n == index ? null! : Result.Success(n)));
        Assert.Contains($"index {index}", ex.Message, StringComparison.Ordinal);
        Assert.Equal("fn", ex.ParamName);
    }

    // Traverse and Select-then-Sequence agree exactly when every element succeeds; they part on
    // failure, where Traverse stops and Sequence accumulates. Both halves are pinned here.
    [Fact]
    public void Traverse_AllSuccesses_AgreesWithSelectThenSequence()
    {
        IEnumerable<string> source = ["1", "2", "3"];

        var traversed = Assert.IsType<Result<ImmutableArray<int>>.Success>(source.Traverse(Parse));
        var sequenced = Assert.IsType<Result<ImmutableArray<int>>.Success>(source.Select(Parse).Sequence());
        Assert.Equal(sequenced.Value, traversed.Value);
    }

    [Fact]
    public void Traverse_Failures_ShortCircuitsWhereSequenceAccumulates()
    {
        IEnumerable<string> source = ["x", "1", "y"];

        var traversed = Assert.IsType<Result<ImmutableArray<int>>.Failure>(source.Traverse(Parse));
        var sequenced = Assert.IsType<Result<ImmutableArray<int>>.Failure>(source.Select(Parse).Sequence());
        Assert.Equal(["parse.x"], traversed.Errors.Select(error => error.Code.Value));
        Assert.Equal(["parse.x", "parse.y"], sequenced.Errors.Select(error => error.Code.Value));
    }

    // ---- ReadOnlySpan overload: stack-allocated and sliced batches ----

    [Fact]
    public void Traverse_Span_AllSuccesses_ReturnsMappedValuesInInputOrder()
    {
        ReadOnlySpan<string> source = ["1", "2", "3"];

        var success = Assert.IsType<Result<ImmutableArray<int>>.Success>(source.Traverse(Parse));
        Assert.Equal([1, 2, 3], success.Value);
    }

    [Fact]
    public void Traverse_Span_Empty_ReturnsEmptySuccess()
    {
        var success = Assert.IsType<Result<ImmutableArray<int>>.Success>(ReadOnlySpan<string>.Empty.Traverse(Parse));
        Assert.Empty(success.Value);
    }

    [Fact]
    public void Traverse_Span_MultipleFailures_ReturnsOnlyTheFirstFailuresErrors()
    {
        ReadOnlySpan<string> source = ["x", "1", "y"];

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(source.Traverse(Parse));
        Assert.Equal("parse.x", Assert.Single(failure.Errors).Code.Value);
    }

    [Fact]
    public void Traverse_Span_Failure_StopsInvokingFn()
    {
        var calls = 0;
        ReadOnlySpan<string> source = ["1", "x", "2", "y"];

        _ = source.Traverse(s =>
        {
            calls++;
            return Parse(s);
        });

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Traverse_Span_Failure_ReusesItsErrorsArray()
    {
        var failed = Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom")));
        ReadOnlySpan<int> source = [0, 1];

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(
            source.Traverse(n => n == 0 ? Result.Success(n) : failed));

        Assert.Same(
            ImmutableCollectionsMarshal.AsArray(((Result<int>.Failure)failed).Errors),
            ImmutableCollectionsMarshal.AsArray(failure.Errors));
    }

    [Fact]
    public void Traverse_Span_NullFn_ThrowsArgumentNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ReadOnlySpan<string>.Empty.Traverse(default(Func<string, Result<int>>)!));
        Assert.Equal("fn", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Traverse_Span_FnReturnsNull_ThrowsNamingItsIndex(int index)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<int> source = [0, 1, 2];
            return source.Traverse(n => n == index ? null! : Result.Success(n));
        });

        Assert.Contains($"index {index}", ex.Message, StringComparison.Ordinal);
        Assert.Equal("fn", ex.ParamName);
    }

    // ---- ImmutableArray overload: priority extension delegating to the span overload ----

    [Fact]
    public void Traverse_ImmutableArray_AllSuccesses_ReturnsMappedValuesInInputOrder()
    {
        ImmutableArray<string> source = ["1", "2"];

        var success = Assert.IsType<Result<ImmutableArray<int>>.Success>(source.Traverse(Parse));
        Assert.Equal([1, 2], success.Value);
    }

    [Fact]
    public void Traverse_ImmutableArray_MultipleFailures_ReturnsOnlyTheFirstFailuresErrors()
    {
        ImmutableArray<string> source = ["x", "1", "y"];

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(source.Traverse(Parse));
        Assert.Equal("parse.x", Assert.Single(failure.Errors).Code.Value);
    }

    // default(ImmutableArray) is that struct's null: same channel as a null IEnumerable, not the
    // NullReferenceException enumeration would produce. Passing this assert also proves the call bound
    // to the ImmutableArray overload, since the IEnumerable path cannot throw it.
    [Fact]
    public void Traverse_ImmutableArray_Default_ThrowsArgumentNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => default(ImmutableArray<string>).Traverse(Parse));
        Assert.Equal("source", ex.ParamName);
    }

    // The default check runs before the delegate guard, so a call that is wrong on both counts reports
    // the receiver rather than the argument.
    [Fact]
    public void Traverse_ImmutableArray_Default_ThrowsBeforeGuardingFn() =>
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(
            () => default(ImmutableArray<string>).Traverse(default(Func<string, Result<int>>)!)).ParamName);

    [Fact]
    public void Traverse_ImmutableArray_FnReturnsNull_ThrowsNamingItsIndex()
    {
        ImmutableArray<int> source = [0, 1, 2];

        var ex = Assert.Throws<ArgumentException>(() => source.Traverse(n => n == 1 ? null! : Result.Success(n)));
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
        Assert.Equal("fn", ex.ParamName);
    }
}
