---
title: Guard the delegate parameters on Map, Bind, Match, and Select
summary: None of the combinators null-check their delegates, and behavior diverges by inhabitant — a null selector NREs on Success and is silently ignored on Failure.
tags: [todo, correctness, null-safety, api-contract]
created: 2026-07-28
priority: medium
effort: low
status: open
---

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
The combinator surface does not. Same theme as
[guard-null-elements-in-sequence-and-apply.md](guard-null-elements-in-sequence-and-apply.md):
the library guards some public entry points and not others, with no stated rule
for which.

Settling that rule is the real task here. Decide whether every public entry
point guards its reference parameters, apply it everywhere, and encode it as an
ArchUnitNET test so the next combinator cannot omit it.

## Where the guard goes

On the abstract base's public members rather than in each inhabitant, so the
behavior stops depending on which inhabitant received the call. `Map`, `Bind`,
`BindAsync`, and `Match` are `abstract`, so guarding them uniformly means either
converting them to a guarded non-virtual public method delegating to a protected
abstract one, or repeating the guard in all six overrides. The first is more
code but makes the guard structural; the second is easy to forget on the next
inhabitant. `Select` and `SelectMany` are already non-abstract and can be
guarded directly.

## Failing test

Starts red. Two assertions per combinator:

```csharp
Assert.Throws<ArgumentNullException>(() => Result.Success(1).Map<string>(null!));
Assert.Throws<ArgumentNullException>(() => Result.Failure<int>(err).Map<string>(null!));
```

The first fails today with `NullReferenceException`, the wrong exception type.
The second fails today because nothing is thrown at all. Both go green after the
fix. Repeat across `Match`, `Bind`, `BindAsync`, `Select`, and both `SelectMany`
shapes. Keeping the assertions in pairs is what pins the two inhabitants to the
same behavior.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
