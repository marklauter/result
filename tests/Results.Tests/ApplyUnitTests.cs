namespace Results.Tests;

public sealed class ApplyUnitTests
{
    [Fact]
    public void TwoInputs_BothSuccess_ReturnsSuccess()
    {
        var result = Result.Apply(Result.Success(Unit.Value), Result.Success(Unit.Value));
        _ = Assert.IsType<Result<Unit>.Success>(result);
    }

    [Fact]
    public void TwoInputs_LeftFailure_RightSuccess_PropagatesLeftErrors()
    {
        var error = Error.Validation("err.left", "left bad");
        var left = Result.Failure<Unit>(error);
        var right = Result.Success(Unit.Value);

        var result = Result.Apply(left, right);

        var f = Assert.IsType<Result<Unit>.Failure>(result);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public void TwoInputs_LeftSuccess_RightFailure_PropagatesRightErrors()
    {
        var error = Error.Validation("err.right", "right bad");
        var left = Result.Success(Unit.Value);
        var right = Result.Failure<Unit>(error);

        var result = Result.Apply(left, right);

        var f = Assert.IsType<Result<Unit>.Failure>(result);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public void SingleFailure_ReturnsFailingInputUnchanged()
    {
        var failure = Result.Failure<Unit>(Error.Validation("err.only", "only failure"));

        var result = Result.Apply(Result.Success(Unit.Value), failure, Result.Success(Unit.Value));

        Assert.Same(failure, result);
    }

    [Fact]
    public void TwoInputs_BothFailure_AccumulatesInOrder()
    {
        var leftError = Error.Validation("err.left", "left bad");
        var rightError = Error.Validation("err.right", "right bad");
        var left = Result.Failure<Unit>(leftError);
        var right = Result.Failure<Unit>(rightError);

        var result = Result.Apply(left, right);

        var f = Assert.IsType<Result<Unit>.Failure>(result);
        Assert.Equal(2, f.Errors.Length);
        Assert.Equal(leftError, f.Errors[0]);
        Assert.Equal(rightError, f.Errors[1]);
    }

    [Fact]
    public void Variadic_Empty_ReturnsSuccess()
    {
        var result = Result.Apply();
        _ = Assert.IsType<Result<Unit>.Success>(result);
    }

    [Fact]
    public void Variadic_AllSuccess_ReturnsSuccess()
    {
        var result = Result.Apply(
            Result.Success(Unit.Value),
            Result.Success(Unit.Value),
            Result.Success(Unit.Value));
        _ = Assert.IsType<Result<Unit>.Success>(result);
    }

    [Fact]
    public void Variadic_Mixed_PropagatesOnlyFailureErrors()
    {
        var error = Error.Validation("err.mid", "middle bad");
        var result = Result.Apply(
            Result.Success(Unit.Value),
            Result.Failure<Unit>(error),
            Result.Success(Unit.Value));

        var f = Assert.IsType<Result<Unit>.Failure>(result);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public void Variadic_ThreeFailures_AccumulatesAllInInputOrder()
    {
        var e1 = Error.Validation("err.1", "first");
        var e2 = Error.Validation("err.2", "second");
        var e3 = Error.Validation("err.3", "third");
        var result = Result.Apply(
            Result.Failure<Unit>(e1),
            Result.Failure<Unit>(e2),
            Result.Failure<Unit>(e3));

        var f = Assert.IsType<Result<Unit>.Failure>(result);
        Assert.Equal(3, f.Errors.Length);
        Assert.Equal(e1, f.Errors[0]);
        Assert.Equal(e2, f.Errors[1]);
        Assert.Equal(e3, f.Errors[2]);
    }

    // Every input contributes an error in input order, the null one included, so the diff shows the
    // null's error standing in the position its input occupied rather than appended at the end.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Variadic_NullElement_AccumulatesAnErrorForEveryInputIndex(int index)
    {
        var results = new Result<Unit>[]
        {
            Result.Failure<Unit>(Error.Validation("err.0", "zero")),
            Result.Failure<Unit>(Error.Validation("err.1", "one")),
            Result.Failure<Unit>(Error.Validation("err.2", "two")),
        };
        results[index] = null!;
        string[] expected = ["err.0", "err.1", "err.2"];
        expected[index] = ErrorCodes.ApplyNullInput;

        Assert.Equal(
            expected,
            Result.Apply(results).Match(_ => [], errors => errors.Select(error => error.Code)));
    }

    [Fact]
    public void Variadic_UnassignedBufferSlot_AccumulatesAnErrorForEveryInputIndex()
    {
        var results = new Result<Unit>[3];
        results[0] = Result.Failure<Unit>(Error.Validation("err.0", "zero"));
        results[1] = Result.Failure<Unit>(Error.Validation("err.1", "one"));

        Assert.Equal(
            ["err.0", "err.1", ErrorCodes.ApplyNullInput],
            Result.Apply(results).Match(_ => [], errors => errors.Select(error => error.Code)));
    }

    // The all-passing batch is the dangerous shape: every assigned check passed, so the null is the
    // only input that did not, and it is the sole contributor to the reported failure.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Variadic_NullElement_AmongPassingChecks_ReportsTheNullInputCode(int index)
    {
        var results = new Result<Unit>[] { Result.Success(Unit.Value), Result.Success(Unit.Value), Result.Success(Unit.Value) };
        results[index] = null!;

        Assert.Equal(
            [ErrorCodes.ApplyNullInput],
            Result.Apply(results).Match(_ => [], errors => errors.Select(error => error.Code)));
    }

    // The index is what the contract promises the message carries; the phrasing around it is not.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Variadic_NullElement_NamesItsIndexInTheMessage(int index)
    {
        var results = new Result<Unit>[] { Result.Success(Unit.Value), Result.Success(Unit.Value), Result.Success(Unit.Value) };
        results[index] = null!;

        Assert.Contains(
            $"index {index}",
            Result.Apply(results).Match(_ => string.Empty, errors => string.Join(';', errors.Select(error => error.Message))),
            StringComparison.Ordinal);
    }

    // The category is the contract: a null input is a caller defect, not a domain outcome, so a
    // regression to any other factory has to fail even though the code and message survive.
    [Fact]
    public void Variadic_NullElement_ReportsInvalidOperationType()
    {
        var results = new Result<Unit>[] { Result.Success(Unit.Value), null!, Result.Success(Unit.Value) };

        Assert.Equal(
            [ErrorType.InvalidOperation],
            Result.Apply(results).Match(_ => [], errors => errors.Select(error => error.Type)));
    }

    // Exercises the failures == 1 fast path, which returns the lone failing input unchanged. With a
    // null alongside it that shortcut would drop the null's error, so both must be reported.
    [Fact]
    public void Variadic_SingleFailureWithNull_ReportsBothErrors()
    {
        var results = new Result<Unit>[] { Result.Failure<Unit>(Error.Validation("err.0", "zero")), null!, Result.Success(Unit.Value) };

        Assert.Equal(
            ["err.0", ErrorCodes.ApplyNullInput],
            Result.Apply(results).Match(_ => [], errors => errors.Select(error => error.Code)));
    }
}
