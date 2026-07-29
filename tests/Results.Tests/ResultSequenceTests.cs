using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Results.Tests;

public sealed class ResultSequenceTests
{
    [Fact]
    public void Sequence_AllSuccesses_ReturnsValuesInInputOrder()
    {
        IEnumerable<Result<int>> results = [Result.Success(1), Result.Success(2), Result.Success(3)];

        var success = Assert.IsType<Result<ImmutableArray<int>>.Success>(results.Sequence());
        Assert.Equal([1, 2, 3], success.Value);
    }

    [Fact]
    public void Sequence_Empty_ReturnsEmptySuccess()
    {
        IEnumerable<Result<int>> results = [];

        var success = Assert.IsType<Result<ImmutableArray<int>>.Success>(results.Sequence());
        Assert.Empty(success.Value);
    }

    [Fact]
    public void Sequence_SingleFailure_ReturnsItsErrors()
    {
        IEnumerable<Result<int>> results = [Result.Success(1), Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom")))];

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(results.Sequence());
        var error = Assert.Single(failure.Errors);
        Assert.Equal("err.x", error.Code.Value);
    }

    [Fact]
    public void Sequence_MultipleFailures_AccumulatesEveryErrorInInputOrder()
    {
        IEnumerable<Result<int>> results =
        [
            Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.1"), ErrorMessage.Unchecked("first"))),
            Result.Success(1),
            Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.2"), ErrorMessage.Unchecked("second")), Error.Validation(ErrorCode.Unchecked("err.3"), ErrorMessage.Unchecked("third"))),
        ];

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(results.Sequence());
        Assert.Equal(["err.1", "err.2", "err.3"], failure.Errors.Select(error => error.Code.Value));
    }

    // A null element is a defect in the calling code, not a domain outcome: Failure elements are
    // modeled results to accumulate, but a null is nothing at all, so it throws at the boundary it
    // crosses — proximate to the defect, with the offending index in the message — rather than
    // swimming downstream dressed as a validation failure.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Sequence_NullElement_ThrowsNamingItsIndex(int index)
    {
        var results = new Result<int>[] { Result.Success(0), Result.Success(1), Result.Success(2) };
        results[index] = null!;

        var ex = Assert.Throws<ArgumentException>(() => results.Sequence());
        Assert.Contains($"index {index}", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sequence_UnassignedBufferSlot_ThrowsNamingItsIndex()
    {
        var results = new Result<int>[3];
        results[0] = Result.Success(0);
        results[1] = Result.Success(1);

        var ex = Assert.Throws<ArgumentException>(() => results.Sequence());
        Assert.Contains("index 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sequence_NullElement_NamesTheParameter()
    {
        var results = new Result<int>[] { Result.Success(0), null!, Result.Success(2) };

        Assert.Equal("results", Assert.Throws<ArgumentException>(() => results.Sequence()).ParamName);
    }

    // The throw wins over accumulation: a batch of modeled failures does not soften the defect
    // into one more error among them.
    [Fact]
    public void Sequence_NullElement_AmongFailures_StillThrows()
    {
        var results = new Result<int>[] { Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom"))), null! };

        var ex = Assert.Throws<ArgumentException>(() => results.Sequence());
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
    }

    // ---- params ReadOnlySpan overload: varargs form and explicit spans ----

    [Fact]
    public void Sequence_Span_AllSuccesses_ReturnsValuesInInputOrder()
    {
        var result = ResultSequence.Sequence(Result.Success(1), Result.Success(2), Result.Success(3));

        var success = Assert.IsType<Result<ImmutableArray<int>>.Success>(result);
        Assert.Equal([1, 2, 3], success.Value);
    }

    [Fact]
    public void Sequence_Span_Empty_ReturnsEmptySuccess()
    {
        var success = Assert.IsType<Result<ImmutableArray<int>>.Success>(ResultSequence.Sequence<int>());
        Assert.Empty(success.Value);
    }

    // The single-failure path reuses the failing input's errors array; same backing array, not a
    // copy, is the documented contract, mirroring the variadic Apply.
    [Fact]
    public void Sequence_Span_SingleFailure_ReusesItsErrorsArray()
    {
        var failed = Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom")));

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(ResultSequence.Sequence(Result.Success(1), failed));
        Assert.Same(
            ImmutableCollectionsMarshal.AsArray(((Result<int>.Failure)failed).Errors),
            ImmutableCollectionsMarshal.AsArray(failure.Errors));
    }

    [Fact]
    public void Sequence_Span_MultipleFailures_AccumulatesEveryErrorInInputOrder()
    {
        var result = ResultSequence.Sequence(
            Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.1"), ErrorMessage.Unchecked("first"))),
            Result.Success(1),
            Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.2"), ErrorMessage.Unchecked("second")), Error.Validation(ErrorCode.Unchecked("err.3"), ErrorMessage.Unchecked("third"))));

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(result);
        Assert.Equal(["err.1", "err.2", "err.3"], failure.Errors.Select(error => error.Code.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Sequence_Span_NullElement_ThrowsNamingItsIndex(int index)
    {
        var results = new Result<int>[] { Result.Success(0), Result.Success(1), Result.Success(2) };
        results[index] = null!;

        var ex = Assert.Throws<ArgumentException>(() => ResultSequence.Sequence(results.AsSpan()));
        Assert.Contains($"index {index}", ex.Message, StringComparison.Ordinal);
        Assert.Equal("results", ex.ParamName);
    }

    [Fact]
    public void Sequence_Span_NullElement_AmongFailures_StillThrows()
    {
        var results = new Result<int>[] { Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom"))), null! };

        var ex = Assert.Throws<ArgumentException>(() => ResultSequence.Sequence(results.AsSpan()));
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
    }

    // ---- ImmutableArray overload: priority extension delegating to the span overload ----

    [Fact]
    public void Sequence_ImmutableArray_AllSuccesses_ReturnsValuesInInputOrder()
    {
        ImmutableArray<Result<int>> results = [Result.Success(1), Result.Success(2)];

        var success = Assert.IsType<Result<ImmutableArray<int>>.Success>(results.Sequence());
        Assert.Equal([1, 2], success.Value);
    }

    [Fact]
    public void Sequence_ImmutableArray_MultipleFailures_AccumulatesEveryErrorInInputOrder()
    {
        ImmutableArray<Result<int>> results =
        [
            Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.1"), ErrorMessage.Unchecked("first"))),
            Result.Success(1),
            Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.2"), ErrorMessage.Unchecked("second"))),
        ];

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(results.Sequence());
        Assert.Equal(["err.1", "err.2"], failure.Errors.Select(error => error.Code.Value));
    }

    // default(ImmutableArray) is that struct's null: same channel as a null IEnumerable, not the
    // NullReferenceException enumeration would produce. Passing this assert also proves the call
    // bound to the ImmutableArray overload, since the IEnumerable path cannot throw it.
    [Fact]
    public void Sequence_ImmutableArray_Default_ThrowsArgumentNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => default(ImmutableArray<Result<int>>).Sequence());
        Assert.Equal("results", ex.ParamName);
    }

    [Fact]
    public void Sequence_ImmutableArray_NullElement_ThrowsNamingItsIndex()
    {
        ImmutableArray<Result<int>> results = [Result.Success(0), null!, Result.Success(2)];

        var ex = Assert.Throws<ArgumentException>(() => results.Sequence());
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
        Assert.Equal("results", ex.ParamName);
    }
}
