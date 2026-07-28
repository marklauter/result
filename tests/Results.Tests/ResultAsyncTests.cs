namespace Results.Tests;

public sealed class ResultAsyncTests
{
    private static ValueTask<Result<int>> Pending(Result<int> result) => new(Task.Run(async () =>
    {
        await Task.Yield();
        return result;
    }));

    [Fact]
    public async Task MapAsync_OverSuccess_MapsValue()
    {
        var mapped = await ValueTask.FromResult(Result.Success(21)).MapAsync(x => x * 2);
        var s = Assert.IsType<Result<int>.Success>(mapped);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public async Task MapAsync_OverPendingSuccess_MapsValue()
    {
        var mapped = await Pending(Result.Success(21)).MapAsync(x => x * 2);
        var s = Assert.IsType<Result<int>.Success>(mapped);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public async Task MapAsync_OverFailure_SkipsContinuation()
    {
        var error = Error.NotFound("err.outer", "outer failure");
        var invoked = false;
        var mapped = await ValueTask.FromResult(Result.Failure<int>(error)).MapAsync(x =>
        {
            invoked = true;
            return x * 2;
        });
        Assert.False(invoked);
        var f = Assert.IsType<Result<int>.Failure>(mapped);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public async Task BindAsync_SyncContinuation_OverSuccess_ReturnsContinuation()
    {
        var bound = await Pending(Result.Success(21)).BindAsync(x => Result.Success(x * 2));
        var s = Assert.IsType<Result<int>.Success>(bound);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public async Task BindAsync_SyncContinuation_OverFailure_SkipsContinuation()
    {
        var error = Error.NotFound("err.outer", "outer failure");
        var invoked = false;
        var bound = await Pending(Result.Failure<int>(error)).BindAsync(x =>
        {
            invoked = true;
            return Result.Success(x * 2);
        });
        Assert.False(invoked);
        var f = Assert.IsType<Result<int>.Failure>(bound);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public async Task BindAsync_AsyncContinuation_OverSuccess_ReturnsContinuation()
    {
        var bound = await Pending(Result.Success(21)).BindAsync(x => Pending(Result.Success(x * 2)));
        var s = Assert.IsType<Result<int>.Success>(bound);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public async Task BindAsync_AsyncContinuation_OverFailure_SkipsContinuation()
    {
        var error = Error.NotFound("err.outer", "outer failure");
        var invoked = false;
        var bound = await Pending(Result.Failure<int>(error)).BindAsync(x =>
        {
            invoked = true;
            return Pending(Result.Success(x * 2));
        });
        Assert.False(invoked);
        var f = Assert.IsType<Result<int>.Failure>(bound);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public async Task BindAsync_AsyncContinuation_OverSuccessReturningFailure_PropagatesInnerFailure()
    {
        var inner = Error.Validation("err.range", "out of range");
        var bound = await Pending(Result.Success(21)).BindAsync(_ => Pending(Result.Failure<int>(inner)));
        var f = Assert.IsType<Result<int>.Failure>(bound);
        var only = Assert.Single(f.Errors);
        Assert.Equal(inner, only);
    }

    [Fact]
    public async Task BindAsync_ChainedAcrossSteps_ThreadsValueThrough()
    {
        var total = await Pending(Result.Success(1))
            .BindAsync(x => Pending(Result.Success(x + 1)))
            .MapAsync(x => x * 10)
            .BindAsync(x => Result.Success(x + 5))
            .MatchAsync(x => x, errors => -errors.Length);
        Assert.Equal(25, total);
    }

    [Fact]
    public async Task MatchAsync_OverSuccess_TakesSuccessPath()
    {
        var matched = await Pending(Result.Success(42)).MatchAsync(x => x, errors => -errors.Length);
        Assert.Equal(42, matched);
    }

    [Fact]
    public async Task MatchAsync_OverFailure_TakesErrorPath()
    {
        var error = Error.NotFound("err.outer", "outer failure");
        var matched = await Pending(Result.Failure<int>(error)).MatchAsync(x => x, errors => -errors.Length);
        Assert.Equal(-1, matched);
    }
}
