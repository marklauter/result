# Architecture

Invariants that the XML doc comments state but that are easy to break by accident, plus the reasoning that isn't in the source.

## The hierarchy is closed, and closure is load-bearing

`Result<T>` is an abstract record whose constructor is `private protected`, and `Success` and `Failure` are nested inside it. Nothing outside the assembly can add an inhabitant.

The combinators (`Match`, `Map`, `Bind`, `BindAsync`) are **abstract on the base and overridden on each inhabitant** — not a `switch` over the two cases. That is deliberate: adding an inhabitant breaks the build, where a `switch` expression would only warn. Do not refactor virtual dispatch into pattern matching to "simplify" it; that trades a compile-time guarantee for a warning.

`CA1034` (nested types should not be visible) is suppressed on `Result<T>` for the same reason — the nesting names the union relationship in the type itself.

## `Failure.Errors` is never empty

The `Failure` constructor is `internal`. All external construction goes through the `Result.Failure<T>` factories, and each one rejects an empty input (`ArgumentException`) or a `default` `ImmutableArray` (`IsDefaultOrEmpty`). Any new factory must enforce the same thing, or "failed with no errors" becomes representable.

## `Failure` hand-writes `Equals` and `GetHashCode`

Record-synthesized equality would compare `ImmutableArray<Error>` by its **underlying array reference**, so two failures carrying equal errors would compare unequal. `Failure.Equals(Failure?)` does an element-wise, order-sensitive `SequenceEqual` over spans, and `GetHashCode` combines element-wise to stay consistent. If a field is added to `Failure`, both must be updated by hand — the compiler will not do it.

## `Error` guards against `default(Error)`

`Error` is a `readonly record struct`, so `default(Error)` is always constructible and its `Code`/`Message` backing fields would be `null` behind non-nullable declarations. The property accessors use the `field` keyword and throw `InvalidOperationException` rather than leak the null. Every valid instance comes from the static factories (`Validation`, `NotFound`, `Gone`, `Conflict`, `Undefined`), which reject null/empty/whitespace code and message.

`CA1716` is suppressed on the type: `Error` is the ubiquitous-language term, and VB consumers can escape it as `[Error]`.

## Two failure semantics, chosen per call site

- **Fail-fast** — `Bind`, `BindAsync`, `SelectMany`. Sequential; short-circuits on the first failure. Query syntax (`from` clauses) binds to `SelectMany`, so **LINQ query syntax does not accumulate errors**.
- **Accumulating** — `Result.Apply` (both overloads), `Sequence`. Independent; reports every error in input order.

`Result.Apply<T, TResult>` accumulates function errors first, then argument errors. This is Validation-applicative behavior, not the monadic instance — they disagree, and the disagreement is the point.

## Overload resolution on `Result.Failure<T>` is deliberate

`ImmutableArray<T>` has an implicit conversion to `ReadOnlySpan<T>`, which would make the span overload ambiguous with (or preferred over) the array one. `[OverloadResolutionPriority(1)]` on the `ImmutableArray<Error>` overload breaks the tie so a pre-built array or a collection expression binds there and is stored with no copy. Removing that attribute silently adds an allocation to the cheapest path.

## The coverage ratchet shapes the code

`Directory.Build.props` sets `Threshold` to `100,100,100` on line, branch, and method, with `ThresholdStat` minimum — **every class** must clear the floor, not the assembly average.

This is why the fourth row of the `Apply` truth table is written as a discard (`_ =>`) with explicit casts instead of a type pattern: a final type-pattern arm makes the compiler synthesize unreachable type-test and default branches, which coverlet counts and the branch ratchet then fails. Similar shapes will hit the same wall. Never lower the threshold to make a build pass.

Excluded from coverage: `[*]*Exception` (plumbing), `[Architecture.Testing]*` (test infrastructure — measuring it would set the floor from the harness rather than the package), and `**/obj/**/*.g.cs`.

## Async is `ValueTask`, and cancellation is the caller's job

`ValueTask<Result<T>>` is the async currency of the whole surface, so a synchronously-completing continuation allocates nothing. Chains that start with `Result<T>.BindAsync` continue through the `ResultAsync` extensions on `ValueTask<Result<T>>`.

The API threads **no** `CancellationToken`. A caller who needs cancellation captures the token in the lambda it passes. Do not add token parameters to the combinators without deciding that question deliberately.

## `Sequence` returns the identity on empty

An empty input is a success carrying an empty array, not a failure — the identity element. The same holds for the variadic `Result.Apply(params ReadOnlySpan<Result<Unit>>)`. Its single-failure path returns the failing input **unchanged** rather than copying its errors, which is why the loop counts before it builds.
