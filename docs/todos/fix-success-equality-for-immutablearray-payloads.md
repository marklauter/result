---
title: Fix Result<T>.Success equality for ImmutableArray payloads
type: todo
summary: Success uses record-synthesized equality, which compares an ImmutableArray payload by reference — the exact hazard Failure hand-writes Equals to avoid, and Sequence returns Result<ImmutableArray<T>>.
tags: [correctness, equality, value-semantics]
created: 2026-07-28
priority: high
status: declined
---

## Resolution

Declined 2026-07-28, no code change. `Result<T>.Success` delegates equality to
`EqualityComparer<T>.Default`, which is the correct semantics for a generic
wrapper: two successes are equal exactly when their payloads are. That the
`ImmutableArray<T>` payload compares its underlying array by reference is
`ImmutableArray<T>`'s own contract, not a defect in `Success`, and it is the
same behavior any reference-typed `T` without value equality already has.

The repro below was confirmed against the current build — `Assert.Equal` on two
structurally identical `Sequence` outputs fails, while the `Failure` and scalar
cases pass — so the observation is accurate. What it does not establish is a
defect: imposing structural equality on `Success` would make two results compare
equal whose payloads compare unequal, which is the worse surprise. The remaining
asymmetry is that `Failure` hand-writes structural equality over
`ImmutableArray<Error>` rather than delegating; that stands, because `Errors` is
library-owned and value semantics is the contract `Failure` promises.

The one change made is documentation: `Success`'s doc comment now states that
equality delegates to `EqualityComparer<T>.Default` and that the payload's owner
defines its equality, which is also why `Failure` hand-writes its own — its
payload is library-owned. This note existing at all is the evidence the
delegation was not self-evident to a reader coming to `Result.cs` cold.

## Start by pinning the failure

Starts red. Write it before choosing between the options below:

```csharp
var a = new[] { Result.Success(1), Result.Success(2) }.Sequence();
var b = new[] { Result.Success(1), Result.Success(2) }.Sequence();

Assert.Equal(a, b);
Assert.Equal(a.GetHashCode(), b.GetHashCode());
```

Both assertions fail against the current build.

The assertion holds whichever of the three options is chosen. Only the type of
`a` and `b` changes, and only if `Sequence`'s return type moves.

`ResultTests` covers `Failure` equality; there is no matching case pinning
`Success` equality for a collection payload. Add the inequality case (two
sequences differing in one element) alongside, so the fix cannot be a
degenerate always-equal `Equals`.

## The defect

`Result<T>.Success` is declared as a positional record with no equality members:

```csharp
public sealed record Success(T Value) : Result<T>
```

so the compiler-synthesized `Equals` compares `Value` with
`EqualityComparer<T>.Default`. When `T` is `ImmutableArray<X>` that dispatches
to `ImmutableArray<T>.Equals`, which compares the underlying array **by
reference**.

Its sibling inhabitant, `Result<T>.Failure`, hand-writes `Equals(Failure?)` and
`GetHashCode` precisely to avoid this, and its XML doc says so:

> Equality is structural over `Errors` (element-wise, order-sensitive), because
> `ImmutableArray<T>`'s default record equality would compare the underlying
> array reference instead.

So the type is internally inconsistent: equality is element-wise on one
inhabitant and reference-based on the other.

## The library produces the triggering type itself

`ResultSequence.Sequence<T>` returns `Result<ImmutableArray<T>>`, so the payload
type that triggers this comes out of the library's own API.

Verified against the built assembly:

```csharp
new[]{ Result.Success(1), Result.Success(2) }.Sequence()
    .Equals(new[]{ Result.Success(1), Result.Success(2) }.Sequence())
// => False, and the two GetHashCode values differ
```

Any caller that compares, dedups, or memo-caches sequenced results by value gets
a spurious mismatch. A cache keyed on a `Result<ImmutableArray<T>>` never hits
and recomputes forever — a silent performance failure with no exception to trace.
An equality assertion over two structurally identical sequence outputs fails for
no visible reason.

## The fix is not local

`Failure` could hand-write `Equals` because it knows its payload is
`ImmutableArray<Error>`. `Success` is generic over `T` and cannot special-case
`ImmutableArray<X>` without either reflection or a type test on every
comparison. Three options:

- Leave `Success` alone and have `Sequence` return something other than a bare
  `Result<ImmutableArray<T>>` — a wrapper with proper value semantics. Changes
  the public signature of `Sequence`.
- Give `Success` a custom `Equals` that detects a payload implementing
  `IStructuralEquatable` and defers to it. Costs a type test per comparison and
  changes equality semantics for every `T`, not just arrays.
- Document the asymmetry as intended and leave it. Cheapest of the three. The
  cost is that `Sequence` output does not compare by value, which contradicts
  what a `record`-shaped API leads a caller to expect.

Record the decision before implementing. Consumers come to depend on equality
semantics, so the choice is hard to reverse. A note under `docs/decisions/`
fits whichever way it goes.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
