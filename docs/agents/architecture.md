---
title: Architecture — non-obvious invariants
summary: Design invariants of the Result hierarchy that the code leaves implicit, and the constraints that shape it.
tags: [agent-guidance, architecture]
created: 2026-07-28
---

# Architecture — non-obvious invariants

## The hierarchy is closed

`Result<T>` is an abstract record whose constructor is `private protected`. `Success` and `Failure` are nested inside it. Nothing outside the assembly can add an inhabitant.

The combinators (`Match`, `Map`, `Bind`, `BindAsync`) are abstract on the base and overridden on each inhabitant, rather than dispatched by a `switch` over the two cases. Adding an inhabitant breaks the build. A `switch` expression would only warn, so refactoring virtual dispatch into pattern matching downgrades a compile error to a warning.

`CA1034` (nested types should not be visible) is suppressed on `Result<T>` for the same reason: the nesting names the union relationship in the type itself.

## `Failure.Errors` is never empty

The `Failure` constructor is `internal`. All external construction goes through the `Result.Failure<T>` factories. Each factory rejects an empty input with `ArgumentException`, and the `ImmutableArray` overload also rejects a `default` array via `IsDefaultOrEmpty`. A new factory that skips this check makes "failed with no errors" representable.

## `Failure` hand-writes `Equals` and `GetHashCode`

Record-synthesized equality compares `ImmutableArray<Error>` by its underlying array reference, so two failures carrying equal errors would compare unequal. `Failure.Equals(Failure?)` does an element-wise, order-sensitive `SequenceEqual` over spans. `GetHashCode` combines element-wise to stay consistent with it. Adding a field to `Failure` means updating both by hand; the compiler will not do it.

## `Error` guards against `default(Error)`

`Error` is a `readonly record struct`, so `default(Error)` is always constructible. Its `Code` and `Message` backing fields are then `null` behind non-nullable declarations. The accessors use the `field` keyword and throw `InvalidOperationException` instead of returning the null.

Every valid instance comes from the static factories — `Validation`, `NotFound`, `Gone`, `Conflict`, `Undefined` — which reject a null, empty, or whitespace code or message.

`CA1716` is suppressed on the type. `Error` is the ubiquitous-language term, and VB consumers can escape it as `[Error]`.

## Two failure semantics, chosen per call site

- **Fail-fast** — `Bind`, `BindAsync`, `SelectMany`. Sequential; short-circuits on the first failure. Query syntax (`from` clauses) binds to `SelectMany`, so LINQ query syntax does not accumulate errors.
- **Accumulating** — `Result.Apply` (both overloads), `Sequence`. Independent; reports every error in input order.

`Result.Apply<T, TResult>` accumulates function errors first, then argument errors. That is the Validation applicative, not the monadic instance. The two give different answers for the same inputs, so the choice belongs at the call site.

## `[OverloadResolutionPriority]` pins the `Failure<T>` array overload

`ImmutableArray<T>` converts implicitly to `ReadOnlySpan<T>`, which would make the span overload ambiguous with the array one. `[OverloadResolutionPriority(1)]` on the `ImmutableArray<Error>` overload breaks the tie, so a pre-built array or a collection expression binds there and is stored with no copy. Removing the attribute adds a silent allocation to the cheapest path.

## The coverage ratchet shapes the code

`Directory.Build.props` sets `Threshold` to `100,100,100` on line, branch, and method, with `ThresholdStat` minimum. Every class must clear the floor, not the assembly average.

That constraint is why the fourth row of the `Apply` truth table is a discard arm with explicit casts instead of a type pattern. A final type-pattern arm makes the compiler synthesize unreachable type-test and default branches. Coverlet counts those branches, and the ratchet fails on them. Any `switch` whose arms exhaust a closed hierarchy by type will hit this.

Never lower the threshold to make a build pass.

Excluded from coverage: `[*]*Exception` as plumbing, `[Architecture.Testing]*` as test infrastructure (measuring it would set the floor from the harness rather than the package), and `**/obj/**/*.g.cs`.

## Async is `ValueTask`, and cancellation is the caller's job

`ValueTask<Result<T>>` is the async currency of the whole surface, so a synchronously-completing continuation allocates nothing. Chains that start with `Result<T>.BindAsync` continue through the `ResultAsync` extensions on `ValueTask<Result<T>>`.

The API threads no `CancellationToken`. A caller needing cancellation captures the token in the lambda it passes. Adding a token parameter to the combinators would double every async overload.

## `Sequence` returns the identity on empty

An empty input yields a success carrying an empty array, not a failure. The variadic `Result.Apply(params ReadOnlySpan<Result<Unit>>)` behaves the same way. Its single-failure path returns the failing input unchanged rather than copying its errors, which is why the loop counts failures before it builds.
