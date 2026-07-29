---
title: Constrain Result<T> to notnull
type: todo
summary: Result<T> has no where T : notnull, so a null-carrying "success" is representable and NREs downstream. Adding the constraint after 1.0 is source-breaking for every consumer.
tags: [correctness, nullability, api-contract, pre-1.0]
created: 2026-07-28
priority: high
status: open
---

## Start by pinning the failure

**Not a behavioral test.** A generic constraint is a compile-time fact, so the
direct reproduction cannot be written as a normal test: after the fix,
`Result.Success<string?>(null)` stops compiling, which means the test source
would have to be deleted to make the build green. A test that must be removed to
pass is not a regression gate.

The workable form is a metadata assertion, which does start red. Write it first:

```csharp
var t = typeof(Result<>).GetGenericArguments()[0];
// notnull is encoded as [Nullable(1)] on the generic parameter
Assert.Contains(
    t.GetCustomAttributesData(),
    a => a.AttributeType.Name == "NullableAttribute");
```

Red today (the attribute is absent), green after the constraint is added. Assert
it across every generic entry point listed under Scope of the change, not just
`Result<>`, so a partial application of the constraint fails.

Put it in `tests/Results.Tests/Architecture` as an ArchUnitNET test. Generalized
to "every public generic in the assembly constrains its success type", it also
catches the next generic someone adds. Per the `csharp:writing-csharp` rule, the
first instance of a pattern carries its architecture test in the same change
set.

## The defect

`Result<T>` declares no generic constraint:

```csharp
public abstract record Result<T>
```

and neither does `Result.Success<T>(T value)`. A grep for `notnull` / `where T`
across `src/Results` returns nothing, so nothing enforces it elsewhere either.

A success carrying `null` is therefore representable — the precise failure mode
the type exists to eliminate.

## Failure mode

Verified: `Result.Success<string?>(null)` constructs a `Result<string?>.Success`,
and `.Match(v => v!.Length, e => -1)` throws `NullReferenceException` on the
**success** path.

The null arrives from an adapter wrapping a nullable-oblivious API, an EF
projection, or a deserializer:

```csharp
Result.Success(dict.GetValueOrDefault(key))   // null on miss
```

The result reports success, and the downstream `Map`/`Match` handler NREs
somewhere far from the construction site.

The same hole reaches `Result.Apply`: `Result.Success<Func<int,int>>(null!)` is
constructible, and the success/success arm of the binary `Apply` does
`f.Value(a.Value)`, dereferencing the null function.

## Adding the constraint later is source-breaking

Adding `where T : notnull` later is a **source-breaking change** for every
consumer that instantiated `Result<T>` with a nullable `T`. The package is at
1.0.0 and unannounced (see
[add-package-icon.md](add-package-icon.md), which notes the package has not been
announced anywhere yet). The change costs nothing now and costs every consumer
a compile error later.

## Scope of the change

The constraint has to be added consistently or it will not compile:

- `Result<T>` itself, and both nested inhabitants
- `Result.Success<T>`, all four `Result.Failure<T>` overloads
- the `TResult` of `Match`, `Map`, `Bind`, `BindAsync`, `Select`, `SelectMany`,
  and the `TIntermediate` of the projecting `SelectMany`
- `Result.Apply<T, TResult>`
- `ResultSequence.Sequence<T>`
- the `ResultAsync` extension methods, whose type parameters must match the
  instance methods they wrap

Expect fallout in the test projects where nullable types are used casually.

This does **not** fix the null-*element* hole in `Sequence` and the variadic
`Apply`. Those take `Result<T>` references, not `T` values. That is
[guard-null-elements-in-sequence-and-apply.md](guard-null-elements-in-sequence-and-apply.md).

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`. Watch for new
nullability warnings — `TreatWarningsAsErrors` is on, so they will fail the
build rather than accumulate.
