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

    // A null input contributes an error naming its index rather than dropping out of the fold, so
    // the batch reports failure instead of a shortened success. Among passing inputs the null is
    // the only contributor.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Sequence_NullElement_AmongSuccesses_ReportsTheNullInputCode(int index)
    {
        var results = new Result<int>[] { Result.Success(0), Result.Success(1), Result.Success(2) };
        results[index] = null!;

        Assert.Equal(
            [ErrorCodes.SequenceNullInput],
            results.Sequence().Match(_ => [], errors => errors.Select(error => error.Code)));
    }

    // The index is what the contract promises the message carries; the phrasing around it is not.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Sequence_NullElement_NamesItsIndexInTheMessage(int index)
    {
        var results = new Result<int>[] { Result.Success(0), Result.Success(1), Result.Success(2) };
        results[index] = null!;

        Assert.Contains(
            $"index {index}",
            results.Sequence().Match(_ => string.Empty, errors => string.Join(';', errors.Select(error => error.Message))),
            StringComparison.Ordinal);
    }

    // The category is the contract: a null input is a caller defect, not a domain outcome, so a
    // regression to any other factory has to fail even though the code and message survive.
    [Fact]
    public void Sequence_NullElement_ReportsInvalidOperationType()
    {
        var results = new Result<int>[] { Result.Success(0), null!, Result.Success(2) };

        Assert.Equal(
            [ErrorType.InvalidOperation],
            results.Sequence().Match(_ => [], errors => errors.Select(error => error.Type)));
    }

    [Fact]
    public void Sequence_UnassignedBufferSlot_ReportsTheNullInputCode()
    {
        var results = new Result<int>[3];
        results[0] = Result.Success(0);
        results[1] = Result.Success(1);

        Assert.Equal(
            [ErrorCodes.SequenceNullInput],
            results.Sequence().Match(_ => [], errors => errors.Select(error => error.Code)));
    }

    // The failing-input counterpart: every input contributes an error in input order, the null one
    // included, so the diff shows the null's error standing in the position its input occupied.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Sequence_NullElement_AccumulatesAnErrorForEveryInputIndex(int index)
    {
        var results = new Result<int>[]
        {
            Result.Failure<int>(Error.Validation("err.0", "zero")),
            Result.Failure<int>(Error.Validation("err.1", "one")),
            Result.Failure<int>(Error.Validation("err.2", "two")),
        };
        results[index] = null!;
        string[] expected = ["err.0", "err.1", "err.2"];
        expected[index] = ErrorCodes.SequenceNullInput;

        Assert.Equal(
            expected,
            results.Sequence().Match(_ => [], errors => errors.Select(error => error.Code)));
    }
}
