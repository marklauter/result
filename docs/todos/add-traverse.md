---
title: Add Traverse
type: todo
summary: Mapping a fallible function over a collection has no name in the library; callers spell it Select-then-Sequence, which always evaluates every element even when the first failure is the whole answer.
tags: [api, traversal, ergonomics]
created: 2026-08-31
priority: medium
status: closed
relates-to: "[[add-sequence-overloads-mirroring-failures-set]]"
---

## Resolution

Closed 2026-08-31. `ResultTraverse.Traverse` landed with the three overloads
`Sequence` carries — `IEnumerable`, `ReadOnlySpan`, and a
`[OverloadResolutionPriority(1)]` `ImmutableArray` that delegates to the span —
and the same null contract: `ArgumentNullException` for a null source, a null
`fn`, and a `default(ImmutableArray<T>)`; `ArgumentException` naming the index
when `fn` returns null, because a null result is a defect in the calling code
rather than a domain outcome.

**Traverse short-circuits.** It stops at the first failure: `fn` is not invoked
for the elements after it, the source is not enumerated past it, and the
returned failure carries that element's errors and no others. That is Haskell's
`traverse` at `f = Either`, whose `Applicative` short-circuits, and it is the
behavior the operation is worth having for — the work after the first failure is
the work you skip.

It is deliberately *not* this library's accumulating applicative, which is the
one deviation worth stating loudly, because `Sequence` is `traverse id` and
`Sequence` accumulates. The pair is the point: `source.Traverse(fn)` when you
want to stop, `source.Select(fn).Sequence()` when you want every error. They
agree exactly when every element succeeds, and the doc comment on
`ResultTraverse`, the README section, and
`ResultTraverseLawTests.Traverse_AgreesWithSelectThenSequence_WhenEveryElementSucceeds`
each say so, so nobody has to infer the split from a signature that cannot carry
it.

Every overload is one pass. The two-pass classify-then-fill shape the span
overloads of `Sequence` use is unavailable here for a reason that is not about
efficiency: pass one would have to run `fn`, and running a caller's function
twice per element is not the library's call to make. The span overload still
spends its known length on an exactly-sized builder allocated lazily at the
first success, so the all-success path ends in `MoveToImmutable` with no copy
and a failure at index 0 allocates nothing. Every failure path reuses the
failing element's errors array rather than copying it, pinned with `Assert.Same`
through `ImmutableCollectionsMarshal`.

`ResultTraverseLawTests` states the contract over generated sources and the
generated Kleisli arrow `ResultGen.IntToResultInt`, which fails on a residue
class, so the failing element lands at every position. Five properties: the
returned value against a lazily-composed LINQ oracle, the *call count* — the
short-circuit claim is about work not done, which no assertion on the returned
value can make — agreement across the three overloads, agreement with
Select-then-Sequence on all-success sources, and `Sequence` as `Traverse` at the
identity arrow. All route through `PropertyCheck.Law`, as
`PropertyTestArchitectureTests.OnlyPropertyCheckSamples` requires.

## The gap it closed

`ResultSequence.Sequence` turns a collection of results into a result of a
collection, but the collection of results has to exist first. The common shape
is a collection of *inputs* and a fallible function over them, which is
`traverse`, and which the library made callers spell as
`source.Select(fn).Sequence()`. That spelling names nothing, allocates an
iterator to hand to a method that immediately drains it, and — the part that
matters — evaluates `fn` for every element no matter what, so a load that fails
on the first of ten thousand rows still runs the other nine thousand nine
hundred and ninety-nine.

## Not done here

A `TraverseAsync` over `Func<T, ValueTask<Result<TResult>>>` is the obvious next
one, and it belongs with
[accumulate-errors-across-the-async-boundary.md](accumulate-errors-across-the-async-boundary.md)
rather than in this change: short-circuiting makes the sequential-versus-
concurrent question easier, not moot, and that todo has to settle the
accumulating shape first.

An effect-only overload — Haskell's `traverse_`, taking `Func<T, Result<Unit>>`
and returning `Result<Unit>` — was left out on purpose. It cannot be an overload
of this name: `Traverse<T, Unit>` already binds, so a second `Traverse` differing
only in returning `Result<Unit>` is ambiguous at every call site that passes a
`Unit`-returning arrow. It needs its own name, and no caller has wanted it yet.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
