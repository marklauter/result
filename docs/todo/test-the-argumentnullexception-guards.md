---
title: Test the documented ArgumentNullException guards
summary: Three public entry points document and enforce a null guard that no test exercises, and because ThrowIfNull emits no branch, the coverage gate cannot see the gap.
tags: [testing, coverage, null-safety]
created: 2026-07-28
priority: medium
effort: low
status: open
---

## Start by pinning the behavior

**This one does not start red**, and it is the only todo from the review where
that is true. There is no defect to reproduce; the guards already behave
correctly, so

```csharp
Assert.Throws<ArgumentNullException>(() => Result.Failure<int>((IReadOnlyList<Error>)null!));
```

passes on first run. It buys regression protection: a silent deletion of the
guards becomes a failing build.

Cover all four sites: the `IReadOnlyList` failure factory, both parameters of
the binary `Apply`, and `Sequence`. Assert the `ParamName` so the test also pins
*which* argument was rejected.

Related: [guard-null-elements-in-sequence-and-apply.md](guard-null-elements-in-sequence-and-apply.md)
and [guard-delegate-parameters-in-combinators.md](guard-delegate-parameters-in-combinators.md)
both add new guards. If either lands first, fold its tests in here so all the
null-contract cases live together.

## The gap

Three public entry points guard a reference parameter with
`ArgumentNullException.ThrowIfNull` and document it with an `<exception>` tag:

- `Result.Failure<T>(IReadOnlyList<Error> errors)`
- `Result.Apply<T, TResult>(Result<Func<T, TResult>>, Result<T>)` — both parameters
- `ResultSequence.Sequence<T>(IEnumerable<Result<T>>)`

No test in the suite passes `null` to any of them.

## This is not a defect

Unlike the other items in `docs/todo/` from the same review, the code here is
**correct**. The guards work. This is a missing-test item, and the one place
where the "a bug fix starts red" rule does not apply.

## Why the coverage gate does not catch it

`ArgumentNullException.ThrowIfNull` compiles to a *call*, not a branch in the
calling method. Coverlet therefore records no uncovered branch, and the 95%
branch-coverage threshold in `Directory.Build.props` stays green with these
paths never executed. The Results module reports 100/100/100 today with zero
tests for any of them.

The number says covered and the case list says otherwise. The
`csharp:writing-csharp` position is that the case list is the authority and the
report is only a proxy.

## What goes wrong without them

A future cleanup deletes the guards as "redundant under nullable enable", a
common refactor in a nullable-enabled library, and the entire suite still
passes. A caller then passes a null list from a deserialized payload and gets
`NullReferenceException` out of
`Result.Failure<T>(errors)` instead of the `ArgumentNullException` the
`<exception>` tag promises. Their `catch (ArgumentException)` no longer matches
and the request fails unhandled.

## The suite covers half the pattern

`ResultTests` covers the empty-collection case for every `Failure<T>` overload.
The null case for the same factories is missing. The pattern was established and
then applied to only one of its two halves.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
