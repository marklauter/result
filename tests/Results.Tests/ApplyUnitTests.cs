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
}
