using CsCheck;
using static Results.Tests.PropertyCheck;

namespace Results.Tests;

/// <summary>
/// Functor, applicative, and monad law tests for <see cref="Result{T}"/>. These pin the algebraic contract: implementation changes that break a law will
/// break a test here. Each law is a universally quantified statement, so each is checked against <see cref="PropertyCheck.Iterations"/> generated inputs
/// covering both inhabitants and generated functions, rather than asserted at one hand-picked value. The corpus is fixed, not random, and a failing law
/// prints the seed that reproduces its exact input; see the remarks on <see cref="PropertyCheck.Law{T}"/>.
/// </summary>
public sealed class ResultLawTests
{
    // ---------- Functor laws ----------
    // 1. Identity:    fa.Map(id) == fa
    // 2. Composition: fa.Map(f).Map(g) == fa.Map(x => g(f(x)))

    [Fact]
    public void Functor_Identity() =>
        Law(ResultGen.IntResult, fa => fa.Map(x => x) == fa);

    [Fact]
    public void Functor_Composition() =>
        Law(Gen.Select(ResultGen.IntResult, ResultGen.IntToInt, ResultGen.IntToString), t =>
        {
            var (fa, f, g) = t;
            return fa.Map(f).Map(g) == fa.Map(x => g(f(x)));
        });

    // ---------- Applicative laws ----------
    // 1. Identity:      pure(id) <*> v == v
    // 2. Homomorphism:  pure(f) <*> pure(x) == pure(f(x))
    // 3. Interchange:   u <*> pure(y) == pure(f => f(y)) <*> u
    // 4. Composition:   pure(compose) <*> u <*> v <*> w == u <*> (v <*> w)
    // 5. Map coherence: pure(f) <*> v == v.Map(f)

    [Fact]
    public void Applicative_Identity() =>
        Law(ResultGen.IntResult, v => Result.Apply(Result.Success<Func<int, int>>(x => x), v) == v);

    [Fact]
    public void Applicative_Homomorphism() =>
        Law(Gen.Select(ResultGen.IntToString, ResultGen.AnyInt), t =>
        {
            var (f, x) = t;
            return Result.Apply(Result.Success(f), Result.Success(x)) == Result.Success(f(x));
        });

    [Fact]
    public void Applicative_Interchange() =>
        Law(Gen.Select(ResultGen.ResultOf(ResultGen.IntToString), ResultGen.AnyInt), t =>
        {
            var (u, y) = t;
            return Result.Apply(u, Result.Success(y))
                == Result.Apply(Result.Success<Func<Func<int, string>, string>>(f => f(y)), u);
        });

    [Fact]
    public void Applicative_Composition() =>
        Law(
            Gen.Select(
                ResultGen.ResultOf(ResultGen.IntToString),
                ResultGen.ResultOf(ResultGen.BoolToInt),
                ResultGen.ResultOf(Gen.Bool)),
            t =>
            {
                var (u, v, w) = t;
                static Func<Func<bool, int>, Func<bool, string>> Compose(Func<int, string> f) => g => x => f(g(x));
                var lhs = Result.Apply(Result.Apply(Result.Apply(Result.Success(Compose), u), v), w);
                var rhs = Result.Apply(u, Result.Apply(v, w));
                return lhs == rhs;
            });

    [Fact]
    public void Applicative_MapCoherence() =>
        Law(Gen.Select(ResultGen.IntResult, ResultGen.IntToString), t =>
        {
            var (v, f) = t;
            return Result.Apply(Result.Success(f), v) == v.Map(f);
        });

    // ---------- Error accumulation ----------
    // Not a law: the four applicative laws are all satisfied by an implementation that accumulates
    // in the opposite order, because reversing every concatenation reverses both sides of each law
    // equally. Order is a contract this library states (Result.Apply: "function errors first, then
    // argument errors"), so it needs a property of its own.

    [Fact]
    public void Apply_AccumulatesFunctionErrorsThenArgumentErrors() =>
        Law(Gen.Select(ResultGen.NonEmptyErrors, ResultGen.NonEmptyErrors), t =>
        {
            var (fnErrors, argErrors) = t;
            return Result.Apply(Result.Failure<Func<int, string>>(fnErrors), Result.Failure<int>(argErrors))
                == Result.Failure<string>([.. fnErrors, .. argErrors]);
        });

    // ---------- Monad laws ----------
    // 1. Left identity:  return(a).Bind(f) == f(a)
    // 2. Right identity: m.Bind(return) == m
    // 3. Associativity:  m.Bind(f).Bind(g) == m.Bind(x => f(x).Bind(g))

    [Fact]
    public void Monad_LeftIdentity() =>
        Law(Gen.Select(ResultGen.AnyInt, ResultGen.IntToResultString), t =>
        {
            var (a, f) = t;
            return Result.Success(a).Bind(f) == f(a);
        });

    [Fact]
    public void Monad_RightIdentity() =>
        Law(ResultGen.IntResult, m => m.Bind(Result.Success) == m);

    [Fact]
    public void Monad_Associativity() =>
        Law(Gen.Select(ResultGen.IntResult, ResultGen.IntToResultInt, ResultGen.IntToResultString), t =>
        {
            var (m, f, g) = t;
            return m.Bind(f).Bind(g) == m.Bind(x => f(x).Bind(g));
        });

    // ---------- LINQ alias coherence ----------
    // Select and SelectMany are aliases, not reimplementations: each must agree with the
    // operation it names, so the laws proven above transfer to the LINQ vocabulary for free.

    [Fact]
    public void Select_AgreesWithMap() =>
        Law(Gen.Select(ResultGen.IntResult, ResultGen.IntToString), t =>
        {
            var (m, f) = t;
            return m.Map(f) == m.Select(f);
        });

    [Fact]
    public void SelectMany_AgreesWithBind() =>
        Law(Gen.Select(ResultGen.IntResult, ResultGen.IntToResultString), t =>
        {
            var (m, f) = t;
            return m.Bind(f) == m.SelectMany(f);
        });

    [Fact]
    public void SelectMany_Projection_AgreesWithBindThenMap() =>
        Law(Gen.Select(ResultGen.IntResult, ResultGen.IntToResultInt, ResultGen.IntToString), t =>
        {
            var (m, f, g) = t;
            string Project(int x, int y) => $"{g(x)}+{y}";
            return m.Bind(x => f(x).Map(y => Project(x, y))) == m.SelectMany(f, Project);
        });
}
