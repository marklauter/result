using System.Collections.Immutable;

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
        IEnumerable<Result<int>> results = [Result.Success(1), Result.Failure<int>(Error.Validation("err.x", "boom"))];

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(results.Sequence());
        var error = Assert.Single(failure.Errors);
        Assert.Equal("err.x", error.Code);
    }

    [Fact]
    public void Sequence_MultipleFailures_AccumulatesEveryErrorInInputOrder()
    {
        IEnumerable<Result<int>> results =
        [
            Result.Failure<int>(Error.Validation("err.1", "first")),
            Result.Success(1),
            Result.Failure<int>(Error.Validation("err.2", "second"), Error.Validation("err.3", "third")),
        ];

        var failure = Assert.IsType<Result<ImmutableArray<int>>.Failure>(results.Sequence());
        Assert.Equal(["err.1", "err.2", "err.3"], failure.Errors.Select(error => error.Code));
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
        var results = new Result<int>[] { Result.Failure<int>(Error.Validation("err.x", "boom")), null! };

        var ex = Assert.Throws<ArgumentException>(() => results.Sequence());
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
    }
}
