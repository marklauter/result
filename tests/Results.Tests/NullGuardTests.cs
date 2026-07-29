using System.Diagnostics.CodeAnalysis;

namespace Results.Tests;

/// <summary>
/// Pins the null contract of the whole public surface: <see cref="ArgumentNullException"/> naming the parameter. The delegate-parameter tests assert both
/// inhabitants together because dispatch is virtual — a guard in one override and not the other would let the behavior diverge by inhabitant (before the
/// guards, a null delegate NREd on Success and was silently ignored on Failure). The reference-parameter tests at the end pin guards that predate this class;
/// ThrowIfNull compiles to a call rather than a branch, so the coverage gate cannot notice their deletion — only these tests can.
/// </summary>
[SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "Every discarded call is expected to throw from its synchronous guard before a ValueTask exists; the discard is the no-await form that proves the throw is synchronous.")]
public sealed class NullGuardTests
{
    private static readonly Result<int> Succeeded = Result.Success(1);
    private static readonly Result<int> Failed = Result.Failure<int>(Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("not found")));

    [Fact]
    public void Match_NullOnSuccess_Throws_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Succeeded.Match<int>(null!, errors => errors.Length));
        Assert.Equal("onSuccess", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => Failed.Match<int>(null!, errors => errors.Length));
        Assert.Equal("onSuccess", ex.ParamName);
    }

    [Fact]
    public void Match_NullOnError_Throws_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Succeeded.Match<int>(x => x, null!));
        Assert.Equal("onError", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => Failed.Match<int>(x => x, null!));
        Assert.Equal("onError", ex.ParamName);
    }

    [Fact]
    public void Map_NullDelegate_Throws_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Succeeded.Map<string>(null!));
        Assert.Equal("fn", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => Failed.Map<string>(null!));
        Assert.Equal("fn", ex.ParamName);
    }

    [Fact]
    public void Bind_NullDelegate_Throws_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Succeeded.Bind<string>(null!));
        Assert.Equal("fn", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => Failed.Bind<string>(null!));
        Assert.Equal("fn", ex.ParamName);
    }

    [Fact]
    public void BindAsync_NullDelegate_ThrowsSynchronously_OnBothInhabitants()
    {
        // No await: the documented contract is a synchronous throw at the call site. An exception
        // captured into the returned value task instead would return normally here and fail the assert.
        var ex = Assert.Throws<ArgumentNullException>(() => _ = Succeeded.BindAsync<string>(null!));
        Assert.Equal("fn", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => _ = Failed.BindAsync<string>(null!));
        Assert.Equal("fn", ex.ParamName);
    }

    [Fact]
    public void Select_NullSelector_Throws_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Succeeded.Select<string>(null!));
        Assert.Equal("selector", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => Failed.Select<string>(null!));
        Assert.Equal("selector", ex.ParamName);
    }

    [Fact]
    public void SelectMany_NullSelector_Throws_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Succeeded.SelectMany<string>(null!));
        Assert.Equal("selector", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => Failed.SelectMany<string>(null!));
        Assert.Equal("selector", ex.ParamName);
    }

    [Fact]
    public void SelectMany_Projection_NullSelector_Throws_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Succeeded.SelectMany<string, string>(null!, (x, y) => y));
        Assert.Equal("selector", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => Failed.SelectMany<string, string>(null!, (x, y) => y));
        Assert.Equal("selector", ex.ParamName);
    }

    [Fact]
    public void SelectMany_Projection_NullResultSelector_Throws_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Succeeded.SelectMany<string, string>(x => Result.Success("v"), null!));
        Assert.Equal("resultSelector", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => Failed.SelectMany<string, string>(x => Result.Success("v"), null!));
        Assert.Equal("resultSelector", ex.ParamName);
    }

    [Fact]
    public void MapAsync_NullDelegate_ThrowsSynchronously_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => _ = ValueTask.FromResult(Succeeded).MapAsync<int, string>(null!));
        Assert.Equal("fn", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => _ = ValueTask.FromResult(Failed).MapAsync<int, string>(null!));
        Assert.Equal("fn", ex.ParamName);
    }

    [Fact]
    public void BindAsync_SyncContinuation_NullDelegate_ThrowsSynchronously_OnBothInhabitants()
    {
        // The cast disambiguates the two BindAsync extension overloads, which a bare null! satisfies equally.
        var ex = Assert.Throws<ArgumentNullException>(() => _ = ValueTask.FromResult(Succeeded).BindAsync((Func<int, Result<string>>)null!));
        Assert.Equal("fn", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => _ = ValueTask.FromResult(Failed).BindAsync((Func<int, Result<string>>)null!));
        Assert.Equal("fn", ex.ParamName);
    }

    [Fact]
    public void BindAsync_AsyncContinuation_NullDelegate_ThrowsSynchronously_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => _ = ValueTask.FromResult(Succeeded).BindAsync((Func<int, ValueTask<Result<string>>>)null!));
        Assert.Equal("fn", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => _ = ValueTask.FromResult(Failed).BindAsync((Func<int, ValueTask<Result<string>>>)null!));
        Assert.Equal("fn", ex.ParamName);
    }

    [Fact]
    public void MatchAsync_NullOnSuccess_ThrowsSynchronously_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => _ = ValueTask.FromResult(Succeeded).MatchAsync<int, int>(null!, errors => errors.Length));
        Assert.Equal("onSuccess", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => _ = ValueTask.FromResult(Failed).MatchAsync<int, int>(null!, errors => errors.Length));
        Assert.Equal("onSuccess", ex.ParamName);
    }

    [Fact]
    public void MatchAsync_NullOnError_ThrowsSynchronously_OnBothInhabitants()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => _ = ValueTask.FromResult(Succeeded).MatchAsync<int, int>(x => x, null!));
        Assert.Equal("onError", ex.ParamName);
        ex = Assert.Throws<ArgumentNullException>(() => _ = ValueTask.FromResult(Failed).MatchAsync<int, int>(x => x, null!));
        Assert.Equal("onError", ex.ParamName);
    }

    [Fact]
    public void Failure_ListFactory_NullList_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Result.Failure<int>((IReadOnlyList<Error>)null!));
        Assert.Equal("errors", ex.ParamName);
    }

    [Fact]
    public void Apply_NullResultFn_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Result.Apply<int, int>(null!, Succeeded));
        Assert.Equal("resultFn", ex.ParamName);
    }

    [Fact]
    public void Apply_NullResultArg_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Result.Apply(Result.Success<Func<int, int>>(x => x), null!));
        Assert.Equal("resultArg", ex.ParamName);
    }

    [Fact]
    public void Sequence_NullSource_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ((IEnumerable<Result<int>>)null!).Sequence());
        Assert.Equal("results", ex.ParamName);
    }
}
