---
title: Accumulate errors across the async boundary
type: todo
summary: ResultAsync carries Map, Bind, and Match into ValueTask but not Apply or Sequence, so validation with an awaited check falls back to first-failure short-circuiting.
tags: [api, async, applicative]
created: 2026-07-29
priority: medium
status: open
---

`ResultAsync` extends `ValueTask<Result<T>>` with `MapAsync`, two `BindAsync`
overloads, and `MatchAsync`. All four are sequential. The accumulating surface,
`Result.Apply` and `ResultSequence.Sequence`, is synchronous only.

The consequence is that error accumulation stops at the first awaited check.
Consider a domain that validates some fields in memory and others against a
substrate, an email format check beside a uniqueness query. It cannot report
both classes of failure together. The caller either awaits each check and
threads the results into the synchronous `Apply` by hand, or chains with
`BindAsync` and gets
first-failure semantics, which is the behavior this library was written to
avoid.

## Shape

Async counterparts of the two accumulating entry points:

```csharp
public static ValueTask<Result<TResult>> ApplyAsync<T, TResult>(
    ValueTask<Result<Func<T, TResult>>> resultFn,
    ValueTask<Result<T>> resultArg);

public static ValueTask<Result<ImmutableArray<T>>> SequenceAsync<T>(
    IEnumerable<ValueTask<Result<T>>> results);
```

The design question is what "independent" means once the checks are awaited.
Accumulation requires every input to be evaluated — short-circuiting is exactly
the semantics being rejected — so every task must be awaited even after one has
failed. Whether they are also started concurrently is a separate decision:
concurrency is the caller's to choose, and starting them here would impose a
policy the synchronous overloads do not have. Awaiting sequentially and
accumulating is the conservative default; document whichever is chosen, because
a caller cannot infer it from the signature.

Cancellation follows the existing convention recorded on
`Result<T>.BindAsync`: the API threads no `CancellationToken`, and a caller
needing one captures it in the lambda.

This todo and
[add-combine-overloads-for-heterogeneous-validation.md](add-combine-overloads-for-heterogeneous-validation.md)
meet: if `Combine` lands first, the async surface most callers want is
`CombineAsync` rather than `ApplyAsync`. Settle the synchronous shape before
building the async mirror of it.

## Tests

Route the properties through `PropertyCheck.Law`. The generators in `ResultGen`
produce synchronous results; lifting them with `ValueTask.FromResult` covers the
completed-synchronously path, and a generator that yields before returning is
needed to cover the genuinely asynchronous one. The properties are the
synchronous ones restated: accumulation order matches input order, and an
all-success input produces the values in order.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`. Add the new methods to
the API table in `README.md`.
