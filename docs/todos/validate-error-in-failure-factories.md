---
title: Validate each Error in the Failure factories
type: todo
summary: The Result.Failure<T> factories and Validate check only the count of errors, never that each Error is initialized, so a default(Error) throws later from inside the caller's error-handling path.
tags: [correctness, invariants, error]
created: 2026-07-28
priority: high
status: closed
---

## Resolution

Closed 2026-07-29. Every `Failure<T>` overload and `Validate` now throws
`ArgumentException` when an error's `Type` is `ErrorType.Undefined` — the
single-error factory and `Validate` name the parameter, the collection overloads
name the offending index. The `Validate` guard is eager, firing on both paths as
this note required.

The note's premise that detection needs an internal `IsInitialized` was wrong,
and the "Type-based check cannot substitute" paragraph had the doctrine
backwards. `ErrorType.Undefined` is never emitted deliberately (see the
`Undefined` doc comment: treated as a bug, not a domain outcome), so a factory
has no reason to distinguish a zeroed struct from a deliberately built
`Undefined` error — both are defects, and one type check rejects both. The
`Error.Undefined` factory was then removed outright: with no legal destination
for its output, keeping it meant the library handing callers the exact value
its own guards reject. A deliberate `Undefined` is now unconstructible, which
makes the type check precisely the `default(Error)` detector. Two `ResultTests`
fixtures that used `Error.Undefined` casually were retyped to `NotFound`, and
the factory's own tests went with it.

Tests cover the red set from this note — `default(Error)` alone, a default
mixed among valid errors in each collection overload asserting the index, and
`Validate` on both the true and false paths. Gate green: 135 tests, coverage
100/100/100.

## Start by pinning the failure

Starts red. Write it before adding any validation:

```csharp
Assert.Throws<ArgumentException>(() => Result.Failure<int>(default(Error)));
```

Today this constructs successfully and throws nothing, so the assertion fails.

`ErrorTests` covers the uninitialized-read throw on `Error` itself. The
factory-level case is missing. Cover all four overloads and `Result.Validate`.
The single-`Error` one has no validation at all, and the collection overloads
need a `default(Error)` mixed in among valid ones, the case a length check
cannot catch:

```csharp
Assert.Throws<ArgumentException>(
    () => Result.Failure<int>(Error.Validation("err.x", "boom"), default));
```

`Validate` needs the `true`-condition case pinned, because that is where a
conditional guard would hide:

```csharp
Assert.Throws<ArgumentException>(() => Result.Validate(true, default));
```

The guard is eager — the error is validated whether or not the condition holds.
A guard that only fires on the false path leaves the defect data-dependent:
every test and every request that happens to pass its checks sails through, and
the crash waits for the first failing check in production.

## The defect

`Error` is a `readonly record struct` whose `Code` and `Message` properties throw
`InvalidOperationException` when read on an uninitialized instance:

```csharp
public string Code => field ?? throw new InvalidOperationException(UninitializedMessage);
```

The type's own doc comment names `default(Error)` as "itself a bug" and makes
the read throw "rather than leaking nulls through their non-nullable
declarations". `Error.Create` enforces the invariant at construction with
`ArgumentException.ThrowIfNullOrWhiteSpace` on both strings, and every public
factory (`Validation`, `NotFound`, `Gone`, `Conflict`, `Undefined`) routes
through it.

The `Result.Failure<T>` factories bypass all of that. All four overloads
validate only **cardinality**:

- `Failure<T>(Error error)` — no validation at all, splats straight to
  `new Result<T>.Failure([error])`
- `Failure<T>(params ReadOnlySpan<Error>)` — checks `errors.Length == 0`
- `Failure<T>(ImmutableArray<Error>)` — checks `errors.IsDefaultOrEmpty`
- `Failure<T>(IReadOnlyList<Error>)` — null-guards the list, checks `Count == 0`

None checks that the errors themselves are initialized. `Result.Validate(bool
condition, Error error)` is a fifth entry point with the same hole: it accepts
the error unexamined and builds the `Failure` from it on the false path.

## Failure mode

The throw is deferred out of construction and into the consumer's error-handling
path, where it destroys the failure it was supposed to report.

```csharp
private static readonly Error NotConfigured;   // never assigned
...
return Result.Failure<Order>(NotConfigured);   // constructs fine
```

The failure travels normally until the terminal match:

```csharp
result.Match(
    ok => ...,
    errors => logger.LogError("{Code}: {Message}", errors[0].Code, errors[0].Message));
```

Reading `Code` throws `InvalidOperationException` from inside the error handler.
The original failure is lost and replaced by an unhandled exception raised while
trying to report it.

`Error` being a struct is what makes this reachable. An unassigned field, an
array slot, a deserialized struct member, and `default(Error)` all produce a
zeroed instance with no constructor ever running.

## What to do

Validate each element in every `Failure<T>` overload and the `Error` parameter
of `Validate`, eagerly on both paths. Detecting an uninitialized
`Error` without triggering the throw needs a total predicate on the type, and
`Error` exposes none. There is no way to ask "am I initialized?" short of
reading `Code` or `Message` and catching. Adding an internal `IsInitialized` is
part of this work.

A `Type`-based check cannot substitute. `ErrorType.Undefined` is `0`, and its
own doc comment says an uninitialized `default(Error)` "reads as this zero
value", so the check cannot tell a zeroed struct from a genuine
`Error.Undefined(...)`.

Per the throw-vs-return rule in `csharp:writing-csharp`, a `default(Error)`
reaching a factory is a caller bug, so `ArgumentException` is the right channel,
consistent with the existing empty-collection guards in the same methods. Add
matching `<exception>` tags.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
