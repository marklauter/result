---
title: Validate each Error in the Failure factories
summary: The Result.Failure<T> factories check only the count of errors, never that each Error is initialized, so a default(Error) throws later from inside the caller's error-handling path.
tags: [todo, correctness, invariants, error]
created: 2026-07-28
priority: high
effort: low
status: open
---

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

None checks that the errors themselves are initialized.

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

Validate each element in every `Failure<T>` overload. Detecting an uninitialized
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

## Failing test

Starts red:

```csharp
Assert.Throws<ArgumentException>(() => Result.Failure<int>(default(Error)));
```

Today this constructs successfully and throws nothing, so the assertion fails.

`ErrorTests` covers the uninitialized-read throw on `Error` itself. The
factory-level case is missing. Cover all four overloads. The single-`Error` one
has no validation at all, and the collection overloads need a `default(Error)`
mixed in among valid ones, the case a length check cannot catch:

```csharp
Assert.Throws<ArgumentException>(
    () => Result.Failure<int>(Error.Validation("err.x", "boom"), default));
```

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
