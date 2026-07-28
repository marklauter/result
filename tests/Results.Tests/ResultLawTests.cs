namespace Results.Tests;

/// <summary>
/// Functor, applicative, and monad law tests for <see cref="Result{T}"/>. These pin the algebraic
/// contract: implementation changes that break a law will break a test here.
/// </summary>
public sealed class ResultLawTests
{
    private static readonly Error ErrA = Error.Validation("err.a", "a");
    private static readonly Error ErrB = Error.Validation("err.b", "b");

    private static void AssertEquivalent<T>(Result<T> expected, Result<T> actual) =>
        Assert.Equal(expected, actual);

    // ---------- Functor laws ----------
    // 1. Identity:    fa.Map(id) == fa
    // 2. Composition: fa.Map(f).Map(g) == fa.Map(x => g(f(x)))

    [Fact]
    public void Functor_Identity_Success() =>
        AssertEquivalent(Result.Success(42), Result.Success(42).Map(x => x));

    [Fact]
    public void Functor_Identity_Failure() =>
        AssertEquivalent(Result.Failure<int>(ErrA), Result.Failure<int>(ErrA).Map(x => x));

    [Fact]
    public void Functor_Composition_Success()
    {
        var fa = Result.Success(10);
        int f(int x) => x + 1;
        string g(int x) => $"v={x}";
        AssertEquivalent(fa.Map(f).Map(g), fa.Map(x => g(f(x))));
    }

    [Fact]
    public void Functor_Composition_Failure()
    {
        var fa = Result.Failure<int>(ErrA);
        int f(int x) => x + 1;
        string g(int x) => $"v={x}";
        AssertEquivalent(fa.Map(f).Map(g), fa.Map(x => g(f(x))));
    }

    // ---------- Applicative laws ----------
    // 1. Identity:      pure(id) <*> v == v
    // 2. Homomorphism:  pure(f) <*> pure(x) == pure(f(x))
    // 3. Interchange:   u <*> pure(y) == pure(f => f(y)) <*> u
    // 4. Composition:   pure(compose) <*> u <*> v <*> w == u <*> (v <*> w)

    [Fact]
    public void Applicative_Identity_Success()
    {
        var v = Result.Success(42);
        AssertEquivalent(v, Result.Apply(Result.Success<Func<int, int>>(x => x), v));
    }

    [Fact]
    public void Applicative_Identity_Failure()
    {
        var v = Result.Failure<int>(ErrA);
        AssertEquivalent(v, Result.Apply(Result.Success<Func<int, int>>(x => x), v));
    }

    [Fact]
    public void Applicative_Homomorphism()
    {
        static int f(int x) => x * 2;
        AssertEquivalent(
            Result.Success(f(21)),
            Result.Apply(Result.Success(f), Result.Success(21)));
    }

    [Fact]
    public void Applicative_Interchange_Success()
    {
        static string fn(int x) => $"v={x}";
        var u = Result.Success(fn);
        const int y = 42;
        AssertEquivalent(
            Result.Apply(u, Result.Success(y)),
            Result.Apply(Result.Success<Func<Func<int, string>, string>>(f => f(y)), u));
    }

    [Fact]
    public void Applicative_Interchange_Failure()
    {
        var u = Result.Failure<Func<int, string>>(ErrA);
        const int y = 42;
        AssertEquivalent(
            Result.Apply(u, Result.Success(y)),
            Result.Apply(Result.Success<Func<Func<int, string>, string>>(f => f(y)), u));
    }

    [Fact]
    public void Applicative_Composition_AllSuccess()
    {
        // pure(compose) <*> u <*> v <*> w == u <*> (v <*> w)
        Func<Func<bool, int>, Func<bool, string>> compose(Func<int, string> f) => g => x => f(g(x));
        var u = Result.Success<Func<int, string>>(x => $"v={x}");
        var v = Result.Success<Func<bool, int>>(b => b ? 1 : 0);
        var w = Result.Success(true);

        var lhs = Result.Apply(Result.Apply(Result.Apply(Result.Success(compose), u), v), w);
        var rhs = Result.Apply(u, Result.Apply(v, w));
        AssertEquivalent(lhs, rhs);
    }

    [Fact]
    public void Applicative_Composition_WithFailures_AccumulatesEquivalently()
    {
        Func<Func<bool, int>, Func<bool, string>> compose(Func<int, string> f) => g => x => f(g(x));
        var u = Result.Failure<Func<int, string>>(ErrA);
        var v = Result.Failure<Func<bool, int>>(ErrB);
        var w = Result.Success(true);

        var lhs = Result.Apply(Result.Apply(Result.Apply(Result.Success(compose), u), v), w);
        var rhs = Result.Apply(u, Result.Apply(v, w));
        AssertEquivalent(lhs, rhs);
    }

    // ---------- Monad laws ----------
    // 1. Left identity:  return(a).Bind(f) == f(a)
    // 2. Right identity: m.Bind(return) == m
    // 3. Associativity:  m.Bind(f).Bind(g) == m.Bind(x => f(x).Bind(g))

    [Fact]
    public void Monad_LeftIdentity()
    {
        static Result<string> f(int x) => Result.Success($"v={x}");
        AssertEquivalent(f(42), Result.Success(42).Bind(f));
    }

    [Fact]
    public void Monad_RightIdentity_Success()
    {
        var m = Result.Success(42);
        AssertEquivalent(m, m.Bind(Result.Success));
    }

    [Fact]
    public void Monad_RightIdentity_Failure()
    {
        var m = Result.Failure<int>(ErrA);
        AssertEquivalent(m, m.Bind(Result.Success));
    }

    [Fact]
    public void Monad_Associativity_AllSuccess()
    {
        Result<int> f(int x) => Result.Success(x + 1);
        Result<string> g(int x) => Result.Success($"v={x}");
        var m = Result.Success(10);
        AssertEquivalent(m.Bind(f).Bind(g), m.Bind(x => f(x).Bind(g)));
    }

    [Fact]
    public void Monad_Associativity_OuterFailure()
    {
        Result<int> f(int x) => Result.Success(x + 1);
        Result<string> g(int x) => Result.Success($"v={x}");
        var m = Result.Failure<int>(ErrA);
        AssertEquivalent(m.Bind(f).Bind(g), m.Bind(x => f(x).Bind(g)));
    }

    [Fact]
    public void Monad_Associativity_InnerFailure()
    {
        Result<int> f(int _) => Result.Failure<int>(ErrA);
        Result<string> g(int x) => Result.Success($"v={x}");
        var m = Result.Success(10);
        AssertEquivalent(m.Bind(f).Bind(g), m.Bind(x => f(x).Bind(g)));
    }

    // ---------- LINQ alias coherence ----------
    // Select and SelectMany are aliases, not reimplementations: each must agree with the
    // operation it names, so the laws proven above transfer to the LINQ vocabulary for free.

    [Fact]
    public void Select_AgreesWithMap_Success()
    {
        var m = Result.Success(10);
        static string f(int x) => $"v={x}";
        AssertEquivalent(m.Map(f), m.Select(f));
    }

    [Fact]
    public void Select_AgreesWithMap_Failure()
    {
        var m = Result.Failure<int>(ErrA);
        static string f(int x) => $"v={x}";
        AssertEquivalent(m.Map(f), m.Select(f));
    }

    [Fact]
    public void SelectMany_AgreesWithBind_Success()
    {
        var m = Result.Success(10);
        static Result<string> f(int x) => Result.Success($"v={x}");
        AssertEquivalent(m.Bind(f), m.SelectMany(f));
    }

    [Fact]
    public void SelectMany_AgreesWithBind_Failure()
    {
        var m = Result.Failure<int>(ErrA);
        static Result<string> f(int x) => Result.Success($"v={x}");
        AssertEquivalent(m.Bind(f), m.SelectMany(f));
    }

    [Fact]
    public void SelectMany_Projection_AgreesWithBindThenMap()
    {
        var m = Result.Success(10);
        static Result<int> f(int x) => Result.Success(x + 1);
        static string project(int x, int y) => $"{x}+{y}";
        AssertEquivalent(
            m.Bind(x => f(x).Map(y => project(x, y))),
            m.SelectMany(f, project));
    }
}
