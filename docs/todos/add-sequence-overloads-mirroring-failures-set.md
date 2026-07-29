---
title: Add Sequence overloads mirroring Failure's set
type: todo
summary: Sequence takes only IEnumerable while Failure has a span and an ImmutableArray overload; add the params ReadOnlySpan and priority ImmutableArray overloads so batches stay off the heap and known counts preset the builder.
tags: [api-surface, efficiency]
created: 2026-07-29
priority: low
status: open
relates-to: "[[avoid-discarded-builder-work-in-sequence]]"
---

`ResultSequence.Sequence<T>` has one shape, `this IEnumerable<Result<T>>`, while
the `Result.Failure<T>` factories offer a `params ReadOnlySpan` and an
`ImmutableArray` overload with `[OverloadResolutionPriority]`. Mirror that set:

```csharp
// as today; null source throws ArgumentNullException
public static Result<ImmutableArray<T>> Sequence<T>(this IEnumerable<Result<T>> results)

// varargs and spans off the heap, like the variadic Result.Apply; the count is
// known, so the builders preset capacity and MoveToImmutable skips the copy
public static Result<ImmutableArray<T>> Sequence<T>(params ReadOnlySpan<Result<T>> results)

// [OverloadResolutionPriority(1)] like Failure's ImmutableArray overload;
// delegates to the span overload
public static Result<ImmutableArray<T>> Sequence<T>(this ImmutableArray<Result<T>> results)
```

Contract points the signatures don't carry:

- A null element throws `ArgumentException` naming the index in every overload —
  the contract `ResultSequence.cs` documents today must hold across the set.
- `default(ImmutableArray<Result<T>>)` is that struct's null, so the
  `ImmutableArray` overload maps `IsDefault` to `ArgumentNullException`, the
  same channel as a null `IEnumerable`, instead of the `NullReferenceException`
  enumeration would produce.
- Skip `IReadOnlyList`: the span overload covers arrays, the `ImmutableArray`
  overload covers the immutable case, and `IEnumerable` catches the rest.
  `Failure` carries its list overload because it has no `IEnumerable` entry
  point; `Sequence` does.

On .NET 10 an array receiver like `results.Sequence()` binds to the span
overload rather than the `IEnumerable` one once both exist, so the array-based
tests in `ResultSequenceTests` silently switch overloads. The null-element
theories need to run against each overload explicitly.

[[avoid-discarded-builder-work-in-sequence]] closed 2026-07-29 with lazy
builders in the `IEnumerable` overload, so the loop-rewrite ordering constraint
is discharged. The new overloads should not copy that shape: a span can be read
twice, so pass one scans for failure (counting errors, throwing on null
elements before anything allocates) and pass two fills exactly one right-sized
builder — `MoveToImmutable` on the success path — with zero waste on either
path. Capacity presets on a single speculative pass would waste an n-sized
values array whenever an early element fails.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
