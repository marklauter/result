using CsCheck;
using System.Collections.Immutable;

namespace Results.Tests;

/// <summary>
/// CsCheck generators for the algebraic law tests. A law is a universally quantified statement, so proving one needs both inhabitants of
/// <see cref="Result{T}"/> and the functions the law composes; fixing either reduces the law back to an example. The function generators build their
/// functions structurally — CsCheck has no arbitrary-function generator — which still varies the shape across runs instead of pinning it to one lambda
/// written at the call site.
/// </summary>
internal static class ResultGen
{
    /// <summary>Every <see cref="Error"/> factory. <see cref="ErrorType.Undefined"/> has no factory — no valid instance carries it and the <c>Result</c>
    /// factories reject any that does — so generating through the factories covers exactly the error space a law can be stated over.</summary>
    private static readonly Gen<Func<ErrorCode, ErrorMessage, Error>> ErrorFactories =
        Gen.OneOfConst<Func<ErrorCode, ErrorMessage, Error>>(
            Error.Validation, Error.NotFound, Error.Gone, Error.Conflict);

    /// <summary>An <see cref="Error"/> of any factory-producible type, with a code and message that vary together so distinct errors compare unequal.</summary>
    public static readonly Gen<Error> AnyError =
        Gen.Select(ErrorFactories, Gen.Int[0, 99], (make, n) =>
            make(ErrorCode.Unchecked($"err.{n}"), ErrorMessage.Unchecked($"message {n}")));

    /// <summary>One to three errors — the shape a <see cref="Result{T}.Failure"/> carries. Multi-error failures are generated because the accumulating
    /// combinators concatenate them, and a law that holds for single-error failures can still break on concatenation.</summary>
    public static readonly Gen<ImmutableArray<Error>> NonEmptyErrors =
        AnyError.Array[1, 3].Select(ImmutableArray.Create);

    /// <summary>Lifts a payload generator into a <see cref="Result{T}"/> generator covering both inhabitants, weighted toward success so the composed
    /// laws still exercise the value paths at higher arities.</summary>
    /// <returns>A generator producing successes and failures of <typeparamref name="T"/>.</returns>
    public static Gen<Result<T>> ResultOf<T>(Gen<T> payload)
        where T : notnull
        => Gen.Frequency(
            (3, payload.Select(Result.Success)),
            (1, NonEmptyErrors.Select(Result.Failure<T>)));

    /// <summary>
    /// <see cref="int"/> payloads. <see cref="Gen.Int"/> ranges over the whole width, including values past a billion, but never produces the exact
    /// boundaries — measured over five million samples, <see cref="int.MinValue"/> and <see cref="int.MaxValue"/> zero times each — so those are mixed in
    /// explicitly, the house rule being that every limit is tested on both sides. Arithmetic in the generated functions is <see langword="unchecked"/> and
    /// wraps identically on both sides of a law, so an overflow at <see cref="int.MinValue"/> cannot make a law hold that would otherwise fail.
    /// </summary>
    public static readonly Gen<int> AnyInt =
        Gen.Frequency(
            (8, Gen.Int),
            (1, Gen.OneOfConst(int.MinValue, int.MaxValue, 0, -1, 1)));

    /// <summary>Successes and failures over <see cref="AnyInt"/>, the payload generator the laws are stated over.</summary>
    public static readonly Gen<Result<int>> IntResult = ResultOf(AnyInt);

    /// <summary>A generated affine function, the <c>int -&gt; int</c> arrow of the composition and associativity laws.</summary>
    public static readonly Gen<Func<int, int>> IntToInt =
        Gen.Select(Gen.Int[-9, 9], Gen.Int[-99, 99], (a, b) => new Func<int, int>(x => unchecked(a * x + b)));

    /// <summary>A generated <c>int -&gt; string</c> arrow, the type-changing half of the composition laws.</summary>
    public static readonly Gen<Func<int, string>> IntToString =
        Gen.Select(Gen.Int[0, 9], Gen.Int[-9, 9], (tag, k) => new Func<int, string>(x => $"{tag}:{unchecked(x + k)}"));

    /// <summary>A generated <c>bool -&gt; int</c> arrow, the innermost function of the applicative composition law.</summary>
    public static readonly Gen<Func<bool, int>> BoolToInt =
        Gen.Select(Gen.Int[-99, 99], Gen.Int[-99, 99], (t, f) => new Func<bool, int>(b => b ? t : f));

    /// <summary>A generated Kleisli arrow <c>int -&gt; Result&lt;int&gt;</c> that fails on a generated residue class, so the monad laws are checked across
    /// the short-circuit boundary rather than only on arrows that always succeed.</summary>
    public static readonly Gen<Func<int, Result<int>>> IntToResultInt =
        Gen.Select(IntToInt, NonEmptyErrors, Gen.Int[0, 3], (f, errors, residue) =>
            new Func<int, Result<int>>(x => int.Abs(x % 4) == residue
                ? Result.Failure<int>(errors)
                : Result.Success(f(x))));

    /// <summary>A generated Kleisli arrow <c>int -&gt; Result&lt;string&gt;</c>, the second arrow of the associativity law.</summary>
    public static readonly Gen<Func<int, Result<string>>> IntToResultString =
        Gen.Select(IntToString, NonEmptyErrors, Gen.Int[0, 3], (f, errors, residue) =>
            new Func<int, Result<string>>(x => int.Abs(x % 4) == residue
                ? Result.Failure<string>(errors)
                : Result.Success(f(x))));
}
