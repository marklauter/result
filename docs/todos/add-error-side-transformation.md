---
title: Add error-side transformation to Result<T>
type: todo
summary: An Error cannot be enriched once created, so a caller wanting to add context at a layer boundary has to Match and rebuild the failure by hand.
tags: [api, errors, diagnostics]
created: 2026-07-29
priority: medium
status: open
---

`Result<T>` transforms the success path with `Map` and chains it with `Bind`.
Nothing on the type reaches the errors. Adding context as a result crosses a
layer boundary therefore means destructuring with `Match` and rebuilding the
failure from its parts. Naming the aggregate a failure came from, or turning a
repository-level `ErrorType.NotFound` into a domain-level message, both take
that route. `Map` and `Bind` exist so the success path never needs
destructuring; the failure path still does.

## Shape

An instance method on `Result<T>`, dispatched virtually like the others:
`MapErrorCore` on both inhabitants, a public non-virtual wrapper carrying the
`ArgumentNullException.ThrowIfNull` guard, so the contract cannot diverge by
inhabitant. `Success` returns itself; `Failure` maps each error.

```csharp
public Result<T> MapError(Func<Error, Error> fn)
```

Two decisions to settle before writing it:

- **Per-error or whole-collection.** `Func<Error, Error>` maps element-wise and
  cannot change the count, which preserves the non-empty invariant for free.
  `Func<ImmutableArray<Error>, ImmutableArray<Error>>` is more general — it
  could collapse several errors into a summary — but can return empty, which
  `Failure` forbids, so it would need a guard and a documented exception.
  Element-wise is the smaller contract; take it unless collapsing is wanted.
- **The `Undefined` guard.** `fn` is caller-supplied and can return
  `default(Error)`. Every other constructor of a failure rejects
  `ErrorType.Undefined` — see `ThrowIfAnyUndefined` in `Result.cs` — so this one
  must too, or it becomes the hole through which a corrupted error enters a
  previously valid failure.

Whether the async surface needs a matching `MapErrorAsync` on
`ValueTask<Result<T>>` follows the same argument as the other `ResultAsync`
extensions; add it in the same change if the chain would otherwise have to drop
out of the pipeline to enrich an error.

## Tests

Route the properties through `PropertyCheck.Law`. `MapError(e => e)` is identity
on both inhabitants, `MapError` composes, and mapping a success never invokes
the delegate. The `Undefined` guard needs a test in `NullGuardTests` alongside
the existing argument guards.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`. Add the method to the
API table in `README.md`.
