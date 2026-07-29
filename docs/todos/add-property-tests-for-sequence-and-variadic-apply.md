---
title: Property-test Sequence and the variadic Apply overload
type: todo
summary: Four hand-written accumulation implementations share a contract that no property constrains; the law suite covers Map, Bind, and binary Apply only.
tags: [testing, laws, property-based]
created: 2026-07-29
priority: medium
status: open
---

`ResultLawTests` now proves the functor, applicative, and monad laws over
generated inputs, but it reaches `Map`, `Bind`, `Select`, `SelectMany`, and the
binary `Result.Apply` only. The accumulating surface — the part of this library
that is not just another Result type — is still pinned by examples alone.

## The gap

There are four separate implementations of the same accumulation contract, and
nothing forces them to agree:

- `ResultSequence.Sequence(IEnumerable<Result<T>>)` — single-pass builder.
- `ResultSequence.Sequence(ReadOnlySpan<Result<T>>)` — two-pass, with a
  `failures == 1` fast path that wraps a *new* `Failure` around the failing
  input's existing error array rather than copying it.
- `ResultSequence.Sequence(ImmutableArray<Result<T>>)` — delegates to the span
  overload.
- `Result.Apply(params ReadOnlySpan<Result<Unit>>)` — a fourth, whose
  `failures == 1` path differs: it returns the failing input instance itself,
  reference identity that `ApplyUnitTests` pins with `Assert.Same`.

The fast paths are the risk, and the fact that two of them shortcut differently
is the reason. A drifted one still returns a failure carrying the right errors
for single-failure input, so every example test keeps passing; only
multi-failure, multi-error input separates them, and that is exactly the shape a
generator produces and a hand-written example rarely does.

## The properties

`ResultGen` already supplies everything needed — `NonEmptyErrors`, `AnyInt`, and
`ResultOf<T>` — so these are cheap to state.

These properties supply their own equality. `Sequence` returns
`Result<ImmutableArray<T>>`, and `Result<T>.Success` delegates to
`EqualityComparer<T>.Default` — the payload's owner defines its equality, as the
doc comment on `Success` states and
[fix-success-equality-for-immutablearray-payloads.md](fix-success-equality-for-immutablearray-payloads.md)
argues at length. `ImmutableArray<T>` compares by reference, so a test wanting
value equality over the payload is the caller that has to provide it; `==` would
be false for structurally identical successes and the property would fail on the
first all-success input. Provide it through `Match`: successes element-wise with
`SequenceEqual`, failures by `Errors.SequenceEqual`. A small
`SequenceEquivalent` helper in the test file keeps the properties readable.

- **Overload agreement.** For a generated `Result<int>[]`, all three of
  `xs.Sequence()`, `Sequence(xs.AsSpan())`, and `xs.ToImmutableArray().Sequence()`
  are equivalent under that comparison. This is the property that catches a
  drifted fast path.
- **Accumulation order.** The errors of `Sequence(xs)` equal the errors of every
  failure in `xs`, concatenated in input order.
- **Success payload.** When every element succeeds, `Sequence(xs)` succeeds with
  the element values in input order.
- **Identity.** `Sequence<int>()` succeeds over an empty array — again by
  element-wise comparison, not `==`. `Result.Apply()` is `Result.Success()`,
  which *can* use `==`, since `Unit` is not an `ImmutableArray`.
- **Monoid associativity** for the variadic overload:
  `Apply(Apply(a, b), c) == Apply(a, b, c)`, comparing `Result<Unit>` values.

Note that accumulation order does not follow from the applicative laws — an
implementation that concatenates backwards satisfies all four — so state it
directly, the way
[prove-the-laws-with-property-based-tests.md](prove-the-laws-with-property-based-tests.md)
had to for the binary `Apply`.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`. Route every property
through `PropertyCheck.Law`; the architecture test
`PropertyTestArchitectureTests.OnlyPropertyCheckSamples` fails the build if a
new file calls CsCheck's sampler directly.
