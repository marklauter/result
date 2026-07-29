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
        var error = Error.Validation(ErrorCode.Unchecked("err.left"), ErrorMessage.Unchecked("left bad"));
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
        var error = Error.Validation(ErrorCode.Unchecked("err.right"), ErrorMessage.Unchecked("right bad"));
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
        var failure = Result.Failure<Unit>(Error.Validation(ErrorCode.Unchecked("err.only"), ErrorMessage.Unchecked("only failure")));

        var result = Result.Apply(Result.Success(Unit.Value), failure, Result.Success(Unit.Value));

        Assert.Same(failure, result);
    }

    [Fact]
    public void TwoInputs_BothFailure_AccumulatesInOrder()
    {
        var leftError = Error.Validation(ErrorCode.Unchecked("err.left"), ErrorMessage.Unchecked("left bad"));
        var rightError = Error.Validation(ErrorCode.Unchecked("err.right"), ErrorMessage.Unchecked("right bad"));
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
        var error = Error.Validation(ErrorCode.Unchecked("err.mid"), ErrorMessage.Unchecked("middle bad"));
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
        var e1 = Error.Validation(ErrorCode.Unchecked("err.1"), ErrorMessage.Unchecked("first"));
        var e2 = Error.Validation(ErrorCode.Unchecked("err.2"), ErrorMessage.Unchecked("second"));
        var e3 = Error.Validation(ErrorCode.Unchecked("err.3"), ErrorMessage.Unchecked("third"));
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

    // A null element is a defect in the calling code, not a domain outcome: Failure elements are
    // modeled results to accumulate, but a null is nothing at all, so it throws at the boundary it
    // crosses — proximate to the defect, with the offending index in the message — rather than
    // counting as a check that passed.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Variadic_NullElement_ThrowsNamingItsIndex(int index)
    {
        var results = new Result<Unit>[] { Result.Success(Unit.Value), Result.Success(Unit.Value), Result.Success(Unit.Value) };
        results[index] = null!;

        var ex = Assert.Throws<ArgumentException>(() => Result.Apply(results));
        Assert.Contains($"index {index}", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Variadic_UnassignedBufferSlot_ThrowsNamingItsIndex()
    {
        var results = new Result<Unit>[3];
        results[0] = Result.Success(Unit.Value);
        results[1] = Result.Success(Unit.Value);

        var ex = Assert.Throws<ArgumentException>(() => Result.Apply(results));
        Assert.Contains("index 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Variadic_NullElement_NamesTheParameter()
    {
        var results = new Result<Unit>[] { Result.Success(Unit.Value), null!, Result.Success(Unit.Value) };

        Assert.Equal("results", Assert.Throws<ArgumentException>(() => Result.Apply(results)).ParamName);
    }

    // The throw wins over accumulation: a batch of modeled failures does not soften the defect
    // into one more error among them.
    [Fact]
    public void Variadic_NullElement_AmongFailures_StillThrows()
    {
        var results = new Result<Unit>[] { Result.Failure<Unit>(Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom"))), null! };

        var ex = Assert.Throws<ArgumentException>(() => Result.Apply(results));
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
    }
}
