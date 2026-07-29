using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Results.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_Factory_ReturnsSuccessVariant()
    {
        var result = Result.Success(42);
        var s = Assert.IsType<Result<int>.Success>(result);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public void Success_Factory_NullSmuggledPastConstraint_Throws()
    {
        // The notnull constraint is compile-time only; null! and nullable-oblivious callers get past it.
        var ex = Assert.Throws<ArgumentNullException>(() => Result.Success<string>(null!));
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void Success_UnitFactory_ReturnsSuccessHoldingUnitValue()
    {
        var result = Result.Success();
        var s = Assert.IsType<Result<Unit>.Success>(result);
        Assert.Equal(Unit.Value, s.Value);
    }

    [Fact]
    public void Failure_Factory_ReturnsFailureVariant()
    {
        var error = Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg"));
        var result = Result.Failure<int>(error);
        var f = Assert.IsType<Result<int>.Failure>(result);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public void Failure_ParamsFactory_PreservesOrder()
    {
        var e1 = Error.Validation(ErrorCode.Unchecked("err.1"), ErrorMessage.Unchecked("first"));
        var e2 = Error.Validation(ErrorCode.Unchecked("err.2"), ErrorMessage.Unchecked("second"));
        var result = Result.Failure<int>(e1, e2);
        var f = Assert.IsType<Result<int>.Failure>(result);
        Assert.Equal(2, f.Errors.Length);
        Assert.Equal(e1, f.Errors[0]);
        Assert.Equal(e2, f.Errors[1]);
    }

    [Fact]
    public void Failure_ParamsFactory_EmptyArray_Throws() =>
        Assert.Throws<ArgumentException>(() => Result.Failure<int>(Array.Empty<Error>()));

    [Fact]
    public void Failure_CollectionExpression_ResolvesToImmutableArrayOverload()
    {
        // Pins overload resolution for the collection-expression call shape: it must bind
        // (to the priority-tagged ImmutableArray overload) rather than go CS0121-ambiguous.
        var e1 = Error.Validation(ErrorCode.Unchecked("err.1"), ErrorMessage.Unchecked("first"));
        var e2 = Error.Validation(ErrorCode.Unchecked("err.2"), ErrorMessage.Unchecked("second"));

        var result = Result.Failure<int>([e1, e2]);

        var f = Assert.IsType<Result<int>.Failure>(result);
        Assert.Equal(2, f.Errors.Length);
        Assert.Equal(e1, f.Errors[0]);
        Assert.Equal(e2, f.Errors[1]);
    }

    [Fact]
    public void Failure_ImmutableArrayFactory_StoresArrayDirectly()
    {
        var e1 = Error.Validation(ErrorCode.Unchecked("err.1"), ErrorMessage.Unchecked("first"));
        var e2 = Error.Validation(ErrorCode.Unchecked("err.2"), ErrorMessage.Unchecked("second"));
        var errors = ImmutableArray.Create(e1, e2);
        var result = Result.Failure<int>(errors);
        var f = Assert.IsType<Result<int>.Failure>(result);
        Assert.Equal(errors, f.Errors);
        Assert.Equal(2, f.Errors.Length);
        Assert.Equal(e1, f.Errors[0]);
        Assert.Equal(e2, f.Errors[1]);
    }

    [Fact]
    public void Failure_ImmutableArrayFactory_Default_Throws() =>
        Assert.Throws<ArgumentException>(() => Result.Failure<int>(default));

    [Fact]
    public void Failure_ImmutableArrayFactory_Empty_Throws() =>
        Assert.Throws<ArgumentException>(() => Result.Failure<int>([]));

    [Fact]
    public void Failure_ListFactory_PreservesOrder()
    {
        var e1 = Error.Validation(ErrorCode.Unchecked("err.1"), ErrorMessage.Unchecked("first"));
        var e2 = Error.Validation(ErrorCode.Unchecked("err.2"), ErrorMessage.Unchecked("second"));
        var result = Result.Failure<int>(new List<Error> { e1, e2 });
        var f = Assert.IsType<Result<int>.Failure>(result);
        Assert.Equal(2, f.Errors.Length);
        Assert.Equal(e1, f.Errors[0]);
        Assert.Equal(e2, f.Errors[1]);
    }

    [Fact]
    public void Failure_ListFactory_Empty_Throws() =>
        Assert.Throws<ArgumentException>(() => Result.Failure<int>(new List<Error>()));

    [Fact]
    public void Failure_SingleErrorFactory_UninitializedError_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => Result.Failure<int>(default(Error)));
        Assert.Equal("error", ex.ParamName);
    }

    [Fact]
    public void Failure_ParamsFactory_UninitializedErrorAmongValid_ThrowsNamingItsIndex()
    {
        // A default(Error) mixed in among valid ones — the case a length check cannot catch.
        var ex = Assert.Throws<ArgumentException>(
            () => Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom")), default));
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
        Assert.Equal("errors", ex.ParamName);
    }

    [Fact]
    public void Failure_ImmutableArrayFactory_UninitializedErrorAmongValid_ThrowsNamingItsIndex()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Result.Failure<int>([Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom")), default]));
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
        Assert.Equal("errors", ex.ParamName);
    }

    [Fact]
    public void Failure_ListFactory_UninitializedErrorAmongValid_ThrowsNamingItsIndex()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Result.Failure<int>(new List<Error> { Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom")), default }));
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
        Assert.Equal("errors", ex.ParamName);
    }

    [Fact]
    public void Match_Success_InvokesOnSuccess()
    {
        var result = Result.Success(42);
        var matched = result.Match(
            onSuccess: v => $"value: {v}",
            onError: errors => $"error: {errors[0].Code}");
        Assert.Equal("value: 42", matched);
    }

    [Fact]
    public void Match_Failure_InvokesOnError()
    {
        var result = Result.Failure<int>(Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg")));
        var matched = result.Match(
            onSuccess: v => $"value: {v}",
            onError: errors => $"error: {errors[0].Code}");
        Assert.Equal("error: err.x", matched);
    }

    [Fact]
    public void Map_OverSuccess_TransformsValue()
    {
        var result = Result.Success(21);
        var mapped = result.Map(x => x * 2);
        var s = Assert.IsType<Result<int>.Success>(mapped);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public void Map_OverFailure_PassesFailureThrough()
    {
        var error = Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg"));
        var result = Result.Failure<int>(error);
        var mapped = result.Map(x => x * 2);
        var f = Assert.IsType<Result<int>.Failure>(mapped);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public void Map_TypeChange_TransformsToNewType()
    {
        var result = Result.Success(42);
        var mapped = result.Map(x => $"value: {x}");
        var s = Assert.IsType<Result<string>.Success>(mapped);
        Assert.Equal("value: 42", s.Value);
    }

    [Fact]
    public void Bind_OverSuccess_RunsContinuation()
    {
        var result = Result.Success(21);
        var bound = result.Bind(x => Result.Success(x * 2));
        var s = Assert.IsType<Result<int>.Success>(bound);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public void Bind_OverSuccessReturningFailure_PropagatesInnerFailure()
    {
        var inner = Error.Validation(ErrorCode.Unchecked("err.range"), ErrorMessage.Unchecked("out of range"));
        var result = Result.Success(21);
        var bound = result.Bind(_ => Result.Failure<int>(inner));
        var f = Assert.IsType<Result<int>.Failure>(bound);
        var only = Assert.Single(f.Errors);
        Assert.Equal(inner, only);
    }

    [Fact]
    public void Bind_OverFailure_SkipsContinuation()
    {
        var error = Error.NotFound(ErrorCode.Unchecked("err.outer"), ErrorMessage.Unchecked("outer failure"));
        var result = Result.Failure<int>(error);
        var invoked = false;
        var bound = result.Bind(x =>
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
    public async Task BindAsync_OverSuccess_AwaitsAndReturnsContinuation()
    {
        var result = Result.Success(21);
        var bound = await result.BindAsync(async x =>
        {
            await Task.Yield();
            return Result.Success(x * 2);
        });
        var s = Assert.IsType<Result<int>.Success>(bound);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public async Task BindAsync_OverFailure_SkipsContinuation()
    {
        var error = Error.NotFound(ErrorCode.Unchecked("err.outer"), ErrorMessage.Unchecked("outer failure"));
        var result = Result.Failure<int>(error);
        var invoked = false;
        var bound = await result.BindAsync(async _ =>
        {
            invoked = true;
            await Task.Yield();
            return Result.Success(0);
        });
        Assert.False(invoked);
        var f = Assert.IsType<Result<int>.Failure>(bound);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public async Task BindAsync_OverSuccessReturningFailure_PropagatesInnerFailure()
    {
        var inner = Error.Validation(ErrorCode.Unchecked("err.range"), ErrorMessage.Unchecked("out of range"));
        var result = Result.Success(21);
        var bound = await result.BindAsync(async _ =>
        {
            await Task.Yield();
            return Result.Failure<int>(inner);
        });
        var f = Assert.IsType<Result<int>.Failure>(bound);
        var only = Assert.Single(f.Errors);
        Assert.Equal(inner, only);
    }

    [Fact]
    public void PatternMatch_DispatchesOnSuccessVariant()
    {
        var result = Result.Success(42);
        var matched = result switch
        {
            Result<int>.Success s => s.Value,
            Result<int>.Failure => -1,
            _ => -2,
        };
        Assert.Equal(42, matched);
    }

    [Fact]
    public void PatternMatch_DispatchesOnFailureVariant()
    {
        var result = Result.Failure<int>(Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg")));
        var code = result switch
        {
            Result<int>.Success => "ok",
            Result<int>.Failure f => f.Errors[0].Code.Value,
            _ => "?",
        };
        Assert.Equal("err.x", code);
    }

    [Fact]
    public void Equals_ReturnsTrue_ForTwoSuccessesWithSameValue()
    {
        var a = Result.Success(42);
        var b = Result.Success(42);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_ReturnsTrue_ForIndependentlyConstructedFailuresWithEqualErrors()
    {
        var a = Result.Failure<int>(Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg")));
        var b = Result.Failure<int>(Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg")));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenFailureErrorsDiffer()
    {
        var a = Result.Failure<int>(Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg")));
        var b = Result.Failure<int>(Error.NotFound(ErrorCode.Unchecked("err.y"), ErrorMessage.Unchecked("msg")));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_IsOrderSensitive_OverFailureErrors()
    {
        // Accumulation order is part of the contract (function errors first, left-then-right),
        // so equality is sequence equality, not set equality.
        var e1 = Error.Validation(ErrorCode.Unchecked("err.1"), ErrorMessage.Unchecked("first"));
        var e2 = Error.Validation(ErrorCode.Unchecked("err.2"), ErrorMessage.Unchecked("second"));
        var a = Result.Failure<int>(e1, e2);
        var b = Result.Failure<int>(e2, e1);
        Assert.NotEqual(a, b);
    }

    [Fact]
    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "always-false is the behavior under test: pins the null branch of the hand-written Equals")]
    public void Equals_ReturnsFalse_ForNullFailure()
    {
        var failure = Assert.IsType<Result<int>.Failure>(Result.Failure<int>(Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg"))));
        Assert.False(failure.Equals(null));
    }

    [Fact]
    public void GetHashCode_ReturnsSameValue_ForEqualFailures()
    {
        var a = Result.Failure<int>(Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg")));
        var b = Result.Failure<int>(Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg")));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_ReturnsFalse_BetweenSuccessAndFailure()
    {
        var success = Result.Success(42);
        var failure = Result.Failure<int>(Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg")));
        Assert.NotEqual(success, failure);
    }

    [Fact]
    public void Failure_ToString_SingleError_ContainsCodeAndMessage()
    {
        // Containment only: the exact rendered format is not part of the contract.
        var s = Result.Failure<int>(Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom"))).ToString();
        Assert.Contains("err.x", s, StringComparison.Ordinal);
        Assert.Contains("boom", s, StringComparison.Ordinal);
    }

    [Fact]
    public void Failure_ToString_ContainsEveryCodeAndMessage()
    {
        var s = Result.Failure<int>(
            Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom")),
            Error.NotFound(ErrorCode.Unchecked("err.y"), ErrorMessage.Unchecked("gone"))).ToString();
        Assert.Contains("err.x", s, StringComparison.Ordinal);
        Assert.Contains("boom", s, StringComparison.Ordinal);
        Assert.Contains("err.y", s, StringComparison.Ordinal);
        Assert.Contains("gone", s, StringComparison.Ordinal);
    }

    [Fact]
    public void Failure_StringInterpolation_ContainsEveryCodeAndMessage()
    {
        // The motivating scenario: a failure formatted into a log message via interpolation.
        var result = Result.Failure<int>(
            Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("boom")),
            Error.NotFound(ErrorCode.Unchecked("err.y"), ErrorMessage.Unchecked("gone")));
        var s = $"operation failed: {result}";
        Assert.Contains("err.x", s, StringComparison.Ordinal);
        Assert.Contains("boom", s, StringComparison.Ordinal);
        Assert.Contains("err.y", s, StringComparison.Ordinal);
        Assert.Contains("gone", s, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_OverWrappedFunctionAndWrappedSuccess_AppliesFunction()
    {
        var doubler = Result.Success<Func<int, int>>(x => x * 2);
        var arg = Result.Success(21);

        var applied = Result.Apply(doubler, arg);

        var s = Assert.IsType<Result<int>.Success>(applied);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public void Apply_WrappedFunctionFailure_PassesFunctionErrorThrough()
    {
        var error = Error.NotFound(ErrorCode.Unchecked("err.fn"), ErrorMessage.Unchecked("function unavailable"));
        var doubler = Result.Failure<Func<int, int>>(error);
        var arg = Result.Success(21);

        var applied = Result.Apply(doubler, arg);

        var f = Assert.IsType<Result<int>.Failure>(applied);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public void Apply_WrappedArgFailure_PassesArgErrorThrough()
    {
        var doubler = Result.Success<Func<int, int>>(x => x * 2);
        var error = Error.NotFound(ErrorCode.Unchecked("err.arg"), ErrorMessage.Unchecked("arg missing"));
        var arg = Result.Failure<int>(error);

        var applied = Result.Apply(doubler, arg);

        var f = Assert.IsType<Result<int>.Failure>(applied);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public void Apply_BothFailures_AccumulatesBothErrors()
    {
        var fnError = Error.NotFound(ErrorCode.Unchecked("err.fn"), ErrorMessage.Unchecked("function unavailable"));
        var argError = Error.NotFound(ErrorCode.Unchecked("err.arg"), ErrorMessage.Unchecked("arg missing"));
        var doubler = Result.Failure<Func<int, int>>(fnError);
        var arg = Result.Failure<int>(argError);

        var applied = Result.Apply(doubler, arg);

        var f = Assert.IsType<Result<int>.Failure>(applied);
        Assert.Equal(2, f.Errors.Length);
        Assert.Equal(fnError, f.Errors[0]);
        Assert.Equal(argError, f.Errors[1]);
    }

    [Fact]
    public void Apply_BinaryViaCurry_OverTwoSuccesses_AppliesFunction()
    {
        // Curry a binary function and chain Apply twice — the canonical applicative pattern.
        var curriedAdd = Result.Success<Func<int, Func<int, int>>>(x => y => x + y);
        var a = Result.Success(10);
        var b = Result.Success(32);

        var sum = Result.Apply(Result.Apply(curriedAdd, a), b);

        var s = Assert.IsType<Result<int>.Success>(sum);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public void Select_OverSuccess_TransformsValue()
    {
        var result = Result.Success(21);
        var selected = result.Select(x => x * 2);
        var s = Assert.IsType<Result<int>.Success>(selected);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public void Select_OverFailure_PassesFailureThrough()
    {
        var error = Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg"));
        var result = Result.Failure<int>(error);
        var selected = result.Select(x => x * 2);
        var f = Assert.IsType<Result<int>.Failure>(selected);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public void SelectMany_OverSuccess_RunsContinuation()
    {
        var result = Result.Success(21);
        var bound = result.SelectMany(x => Result.Success(x * 2));
        var s = Assert.IsType<Result<int>.Success>(bound);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public void SelectMany_OverFailure_SkipsContinuation()
    {
        var error = Error.NotFound(ErrorCode.Unchecked("err.outer"), ErrorMessage.Unchecked("outer failure"));
        var result = Result.Failure<int>(error);
        var invoked = false;
        var bound = result.SelectMany(x =>
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
    public void SelectMany_WithProjection_OverTwoSuccesses_CombinesValues()
    {
        var result = Result.Success(10).SelectMany(
            x => Result.Success(32),
            (x, y) => x + y);
        var s = Assert.IsType<Result<int>.Success>(result);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public void SelectMany_WithProjection_InnerFailure_PropagatesWithoutProjecting()
    {
        var inner = Error.Validation(ErrorCode.Unchecked("err.inner"), ErrorMessage.Unchecked("inner failure"));
        var projected = false;
        var result = Result.Success(10).SelectMany(
            _ => Result.Failure<int>(inner),
            (x, y) =>
            {
                projected = true;
                return x + y;
            });
        Assert.False(projected);
        var f = Assert.IsType<Result<int>.Failure>(result);
        var only = Assert.Single(f.Errors);
        Assert.Equal(inner, only);
    }

    [Fact]
    public void SelectMany_QuerySyntax_BindsAndProjects()
    {
        // Pins the compiler's query-syntax binding: chained `from` clauses must resolve to the
        // projection overload of SelectMany.
        var result =
            from x in Result.Success(10)
            from y in Result.Success(32)
            select x + y;
        var s = Assert.IsType<Result<int>.Success>(result);
        Assert.Equal(42, s.Value);
    }

    [Fact]
    public void SelectMany_QuerySyntax_ShortCircuitsOnFirstFailure()
    {
        // Bind is sequential: the second clause's error is never produced, unlike applicative Apply.
        var e1 = Error.Validation(ErrorCode.Unchecked("err.1"), ErrorMessage.Unchecked("first"));
        var result =
            from x in Result.Failure<int>(e1)
            from y in Result.Success(32)
            select x + y;
        var f = Assert.IsType<Result<int>.Failure>(result);
        var only = Assert.Single(f.Errors);
        Assert.Equal(e1, only);
    }

    [Fact]
    public void Validate_ConditionHolds_ReturnsSuccessUnit()
    {
        var result = Result.Validate(condition: true, Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("unused")));
        var s = Assert.IsType<Result<Unit>.Success>(result);
        Assert.Equal(Unit.Value, s.Value);
    }

    [Fact]
    public void Validate_ConditionFails_ReturnsFailureCarryingError()
    {
        var error = Error.Validation(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("condition violated"));
        var result = Result.Validate(condition: false, error);
        var f = Assert.IsType<Result<Unit>.Failure>(result);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }

    [Fact]
    public void Validate_UninitializedError_ConditionHolds_Throws()
    {
        // The guard is eager: a true condition must not let the trash error slide through unread,
        // or the defect stays data-dependent and waits for the first failing check in production.
        var ex = Assert.Throws<ArgumentException>(() => Result.Validate(condition: true, default));
        Assert.Equal("error", ex.ParamName);
    }

    [Fact]
    public void Validate_UninitializedError_ConditionFails_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => Result.Validate(condition: false, default));
        Assert.Equal("error", ex.ParamName);
    }

    [Fact]
    public void Validate_ComposedWithVariadicApply_AccumulatesEveryViolation()
    {
        // The intended pairing: lift independent checks, combine applicatively, report all violations.
        var e1 = Error.Validation(ErrorCode.Unchecked("err.1"), ErrorMessage.Unchecked("first"));
        var e2 = Error.Validation(ErrorCode.Unchecked("err.2"), ErrorMessage.Unchecked("second"));

        var result = Result.Apply(
            Result.Validate(condition: false, e1),
            Result.Validate(condition: true, Error.Validation(ErrorCode.Unchecked("err.ok"), ErrorMessage.Unchecked("unused"))),
            Result.Validate(condition: false, e2));

        var f = Assert.IsType<Result<Unit>.Failure>(result);
        Assert.Equal(2, f.Errors.Length);
        Assert.Equal(e1, f.Errors[0]);
        Assert.Equal(e2, f.Errors[1]);
    }

    [Fact]
    public void Apply_BinaryViaCurry_FirstArgFailure_ShortCircuits()
    {
        var curriedAdd = Result.Success<Func<int, Func<int, int>>>(x => y => x + y);
        var error = Error.Validation(ErrorCode.Unchecked("err.first"), ErrorMessage.Unchecked("first arg bad"));
        var a = Result.Failure<int>(error);
        var b = Result.Success(32);

        var sum = Result.Apply(Result.Apply(curriedAdd, a), b);

        var f = Assert.IsType<Result<int>.Failure>(sum);
        var only = Assert.Single(f.Errors);
        Assert.Equal(error, only);
    }
}
