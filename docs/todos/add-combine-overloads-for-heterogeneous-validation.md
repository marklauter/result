---
title: Add Combine overloads for heterogeneous validation
type: todo
summary: Accumulating across independently validated fields of different types means hand-currying through repeated Apply, so the library's headline feature has its least usable path.
tags: [api, applicative, ergonomics]
created: 2026-07-29
priority: high
status: open
---

`Result.Apply<T, TResult>(Result<Func<T, TResult>>, Result<T>)` is the binary
applicative, and currying is how it reaches higher arities. That works, and
`ResultLawTests.Applicative_Composition` proves it, but it is not what a caller
can reasonably write. Building a value from four validated fields reads:

```csharp
Result.Apply(Result.Apply(Result.Apply(Result.Apply(
    Result.Success(curried), name), email), age), address)
```

with a matching `Func<A, Func<B, Func<C, Func<D, Customer>>>>` declared by hand
at the call site. That is the DDD case this library exists for: several
independent checks, every violation reported. It is currently the hardest thing
in the API to express.

The existing accumulating entry points do not cover it.
`ResultSequence.Sequence` takes `Result<T>` of one `T`, so it handles a
collection of like values, not a record built from unlike ones. The variadic
`Result.Apply(params ReadOnlySpan<Result<Unit>>)` accumulates across checks but
discards every value, so it validates without constructing.

## Shape

`Combine` overloads at arities 2 through 8, each taking the results plus a
selector and accumulating on the failure path:

```csharp
public static Result<TResult> Combine<T1, T2, TResult>(
    Result<T1> first,
    Result<T2> second,
    Func<T1, T2, TResult> selector)
```

Errors accumulate in argument order, matching the order `Apply` already
documents (function errors before argument errors) and the order `Sequence`
uses. Whether to also offer tuple-returning overloads without a selector is
open; a selector-only surface is smaller and reads better at the call site.

The near-identical arities can be generated or written by hand. Hand-written is
acceptable if each carries its own doc comment, which the analyzer settings
require on the packed assembly.

## Tests

Route the properties through `PropertyCheck.Law`; the architecture test
`PropertyTestArchitectureTests.OnlyPropertyCheckSamples` fails the build
otherwise. The properties that matter:

- Agreement with currying: `Combine(a, b, f)` equals the curried `Apply` chain
  for the same inputs, at every arity.
- Accumulation order: the errors of a combined failure are the errors of each
  failing input, concatenated in argument order.
- Success: when every input succeeds, the result is `selector` applied to the
  values.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`. Add the new overloads
to the API table in `README.md`.
