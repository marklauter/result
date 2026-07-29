---
title: Assert the no-copy behavior in Failure_ImmutableArrayFactory_StoresArrayDirectly
type: todo
summary: The test named StoresArrayDirectly asserts value equality, which a copying implementation also satisfies, so the no-copy promise in the factory's XML doc is unpinned.
tags: [testing, contract]
created: 2026-07-28
priority: low
status: open
---

## Start by pinning the behavior

**Does not start red.** The implementation already stores the array directly, so
the stronger assertion passes on first run. What it fixes is the gap between what
the test's name claims and what its body checks.

`ImmutableCollectionsMarshal.AsArray` reaches the underlying array, which makes
the no-copy claim directly assertable:

```csharp
Assert.Same(
    ImmutableCollectionsMarshal.AsArray(errors),
    ImmutableCollectionsMarshal.AsArray(f.Errors));
```

Keep the existing element assertions alongside it. They pin the contents; this
pins the storage.

## The gap

`Failure_ImmutableArrayFactory_StoresArrayDirectly` in `ResultTests` currently
asserts:

```csharp
Assert.Equal(errors, f.Errors);
Assert.Equal(2, f.Errors.Length);
Assert.Equal(e1, f.Errors[0]);
Assert.Equal(e2, f.Errors[1]);
```

Every one of those holds for an implementation that copies the array. The test
name and the factory's XML doc both promise more than that. The doc on the
`ImmutableArray<Error>` overload of `Result.Failure<T>` says it is "the cheapest
factory: the array is stored directly with no copy", and the
`OverloadResolutionPriority(1)` attribute exists to make sure that overload wins
against the `ReadOnlySpan` one, which does copy.

So the no-copy behavior is the reason the overload exists, and nothing tests it.
A refactor that changed the factory to `[.. errors]` would keep the whole suite
green while silently removing the optimization the attribute was added to
protect.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
