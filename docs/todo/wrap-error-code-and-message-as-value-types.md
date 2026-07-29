---
title: Wrap Error.Code and Error.Message as value types
summary: Code and Message are bare interchangeable strings, so every factory call can transpose them and still compile, which the writing-csharp rule on wrapping primitives exists to prevent.
tags: [domain-modeling, primitive-obsession, api-contract, pre-1.0]
created: 2026-07-28
priority: medium
status: open
---

## Start by pinning the failure

**Not a behavioral test.** The defect is that a wrong call compiles, so there is
no runtime behavior to assert. `Error.Validation("boom", "err.x")` with the
arguments transposed produces a perfectly valid `Error`; it is only wrong to a
reader.

The test is the compile-time fact, asserted through metadata, and it starts red:

```csharp
Assert.Equal(typeof(ErrorCode), typeof(Error).GetProperty(nameof(Error.Code))!.PropertyType);
Assert.Equal(typeof(ErrorMessage), typeof(Error).GetProperty(nameof(Error.Message))!.PropertyType);
```

Better, put it in `tests/Results.Tests/Architecture` as an ArchUnitNET rule over
the whole assembly: no public property carrying domain meaning is typed
`string`. That form also catches the next primitive someone adds. Same shape as
the architecture test in
[constrain-result-value-to-notnull.md](constrain-result-value-to-notnull.md),
and the two should share one rule class if both land.

## The gap

`Error` exposes both properties as bare strings:

```csharp
public string Code { get; }
public string Message { get; }
```

and every factory takes them in the same order with the same type:
`Error.Validation(string code, string message)`, and the same for `NotFound`,
`Gone`, `Conflict`, and `Undefined`. Nothing stops a caller transposing them, and
nothing at the call site reads wrong when they do.

The `csharp:writing-csharp` rule is explicit about this: wrap primitives that
carry meaning in a `readonly record struct` implementing
`IValue<TSelf, TValue>`, self-referential so the static abstract members resolve
through the type parameter, with `Parse` as the fallible lift and `Unchecked` as
the total embedding. A bare `string` then stops being assignment-compatible with
a checked value, and the transposition becomes a compile error.

The two are different concepts, which is what makes the wrapping worth its cost
here. `Code` is a stable machine-readable identifier that callers branch
on and that must not change once published. `Message` is human-readable prose for
logs and error responses, free to be reworded. They have different validation
rules (a code is a dotted slug; a message is a sentence) and different change
policies, and the type system currently records neither.

## The cost

`Error.Create` is the single validation choke point today, so the parsing moves
there cleanly. The cost is at the edges:

- Two new public types, each with `Parse`, `Unchecked`, `IComparable<TSelf>`,
  `IEquatable<TSelf>`, and `IComparisonOperators<TSelf, TSelf, bool>` per the
  skill's wrapper contract.
- Five public factory signatures change, which is a **breaking change** for
  every consumer. Same pre-1.0 argument as
  [constrain-result-value-to-notnull.md](constrain-result-value-to-notnull.md):
  the package is at 1.0.0 and unannounced, so now is when this is cheap. If the
  two land together, they should land in one change set.
- A decision on whether the factories keep `string` overloads that parse, or
  take only the wrapped types and push parsing to the caller. The first is
  friendlier and reintroduces the transposition hazard on the `string` overload.
  The second is stricter and noisier at every call site.
- `Error`'s uninitialized-instance behavior has to survive. `Code` and `Message`
  currently throw `InvalidOperationException` on a `default(Error)` via
  `field ?? throw`. A `readonly record struct` wrapper has its own `default`,
  so the null check that drives that behavior needs rework. See
  [validate-error-in-failure-factories.md](validate-error-in-failure-factories.md),
  which turns on the same uninitialized-detection problem.

Record the decision on that third point under `docs/decisions/` before
implementing.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
