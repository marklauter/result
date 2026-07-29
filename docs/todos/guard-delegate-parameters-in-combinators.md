---
title: Guard the delegate parameters on Map, Bind, Match, and Select
type: todo
summary: None of the combinators null-check their delegates, and behavior diverges by inhabitant — a null selector NREs on Success and is silently ignored on Failure.
tags: [correctness, null-safety, api-contract]
created: 2026-07-28
priority: medium
status: closed
---

## Resolution

Closed 2026-07-29. Every delegate parameter across the combinator surface now
throws `ArgumentNullException` naming the parameter, identically on both
inhabitants.

The four abstract members took the structural option this note named: `Match`,
`Map`, `Bind`, and `BindAsync` are now public non-virtual wrappers that guard
with `ArgumentNullException.ThrowIfNull` and delegate to `private protected
abstract` cores (`MatchCore`, `MapCore`, `BindCore`, `BindAsyncCore`) that the
two inhabitants override. `Select` and both `SelectMany` shapes guard directly
and call the cores, so the exception names `selector`/`resultSelector` rather
than `fn`. The `ResultAsync` extensions split into a guarding wrapper over a
private async core, so the guard throws synchronously at the call site instead
of being captured into the returned value task — a usage error does not wait for
the receiver.

Two regression gates. `NullGuardTests` (born `DelegateGuardTests`, renamed when
[test-the-argumentnullexception-guards.md](test-the-argumentnullexception-guards.md)
folded its cases in) holds fourteen tests, one per
delegate parameter, each asserting the pair of inhabitants together with
`ParamName` pinned; all fourteen were proven red first (the Success half threw
`NullReferenceException`, the Failure half threw nothing). The architecture test
`PublicDelegateTakingMethodsAreNonVirtual` pins the pattern itself: a public
instance method taking a delegate must be non-virtual, so the next combinator
cannot put the guard back in the inhabitants.

Contract note: the Failure inhabitants previously accepted a null delegate
silently; they now throw. No `<exception>` tag ever promised the old behavior,
and the package is pre-1.0. Every guarded entry point carries an `<exception>`
tag now. Gate green: 152 tests, coverage 100/100/100.

## Start by pinning the failure

Starts red. Two assertions per combinator, written before any guard is added:

```csharp
Assert.Throws<ArgumentNullException>(() => Result.Success(1).Map<string>(null!));
Assert.Throws<ArgumentNullException>(() => Result.Failure<int>(err).Map<string>(null!));
```

The first fails today with `NullReferenceException`, the wrong exception type.
The second fails today because nothing is thrown at all. Both go green after the
fix. Repeat across `Match`, `Bind`, `BindAsync`, `Select`, both `SelectMany`
shapes, and the `ResultAsync` extensions (`MapAsync`, both `BindAsync` shapes,
`MatchAsync`), which take the same delegates and are equally unguarded. Keeping
the assertions in pairs is what pins the two inhabitants to the same behavior.

## The defect

`Match`, `Map`, `Bind`, `BindAsync`, `Select`, and both `SelectMany` shapes take
delegate parameters and none of them is null-checked.

Because dispatch is virtual, the two inhabitants behave differently for the same
null input:

- `Result<T>.Success.Map` is `new Result<TResult>.Success(fn(Value))` — invokes
  the delegate, so a null `fn` throws `NullReferenceException` with no parameter
  name.
- `Result<T>.Failure.Map` is `new Result<TResult>.Failure(Errors)` — never reads
  `fn` at all, so a null is silently accepted.

Verified: `Result.Failure<int>(e).Map<string>(null!)` returns a failure with no
throw, while `Result.Success(1).Map<string>(null!)` throws
`NullReferenceException`.

## Failure mode

A selector arriving null from a DI, configuration, or reflection path passes
every test and every request that happens to fail validation. It then crashes
the first time an operation *succeeds*, with an unattributable NRE that names no
parameter. The bug hides in the success path, which normally gets the least
error-handling attention.

## The surface guards some entry points and not others

`Result.Apply<T, TResult>` in the same file already does
`ArgumentNullException.ThrowIfNull` on both of its arguments and documents it.
The combinator surface does not.

The rule is settled (2026-07-29): per the throw-vs-return note in
`csharp:writing-csharp`, a caller defect throws at the boundary it crosses, and
[guard-null-elements-in-sequence-and-apply.md](guard-null-elements-in-sequence-and-apply.md)
closed on that channel. What remains here is application — guard every delegate
parameter, including the `ResultAsync` extensions — and encoding it as an
ArchUnitNET test so the next combinator cannot omit it.

## Where the guard goes

On the abstract base's public members rather than in each inhabitant, so the
behavior stops depending on which inhabitant received the call.

`Map`, `Bind`, `BindAsync`, and `Match` are `abstract`, which leaves two ways to
guard them uniformly. Convert each to a guarded non-virtual public method
delegating to a protected abstract one, or repeat the guard in all six
overrides. The first is more code and makes the guard structural. The second is
easy to forget on the next inhabitant.

`Select` and `SelectMany` are already non-abstract and can be guarded directly,
as can the static `ResultAsync` extensions.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
