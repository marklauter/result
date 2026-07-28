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
}
