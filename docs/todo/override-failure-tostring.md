---
title: Override ToString on Result<T>.Failure
summary: Failure inherits the record-synthesized ToString, which prints ImmutableArray's type name — every error code and message is absent from any log line that formats a failed result.
tags: [correctness, diagnostics, logging]
created: 2026-07-28
priority: medium
effort: low
status: open
---

## Start by pinning the failure

Starts red. Write it before touching `Result<T>.Failure`:

```csharp
var s = Result.Failure<int>(Error.Validation("err.x", "boom")).ToString();
Assert.Contains("err.x", s, StringComparison.Ordinal);
Assert.Contains("boom", s, StringComparison.Ordinal);
```

Fails today (the string contains neither), passes after the override.

Assert containment of the codes and messages, not the exact rendered string.
The `csharp:writing-csharp` rule against testing outside the contract applies
here: pinning exact `ToString` wording couples the suite to a format that is not
part of the promise, so a later formatting tweak fails a test for no behavioral
reason. The contract should promise that the codes and messages are present, and
that is what the test should check.

## The defect

`Result<T>.Failure` hand-writes `Equals` and `GetHashCode` to fix
`ImmutableArray`'s reference semantics, but leaves `ToString` synthesized. The
record's generated `PrintMembers` prints `Errors` via
`ImmutableArray<Error>.ToString()`, which is `ValueType.ToString()` — the type
name.

Verified:

```csharp
$"{Result.Failure<int>(Error.Validation("err.x","boom"), Error.NotFound("err.y","gone"))}"
// => Failure { Errors = System.Collections.Immutable.ImmutableArray`1[Results.Error] }
```

Every code and message is gone.

## Failure mode

A caller logs the failure value directly:

```csharp
logger.LogWarning("operation failed: {Result}", result);
```

That records the type name and nothing else. The diagnostic that would identify
*which* validation failed is silently lost. `Success` prints its payload
(`Success { Value = 42 }`), so the loss stays invisible until a real failure
occurs in production.

`Error` itself prints correctly: it is a record struct whose synthesized
`PrintMembers` reads `Type`, `Code`, and `Message`. The loss is purely the array
formatting.

## What to do

Override `ToString` (or `PrintMembers`, which is the record-idiomatic hook and
keeps the `Failure { ... }` envelope) on `Result<T>.Failure` to enumerate the
errors. Overriding `PrintMembers` is the smaller change and composes with the
record shape rather than replacing it.

`Error.Code` and `Error.Message` throw on an uninitialized `Error`, so a
`ToString` that reads them can throw. A `ToString` that throws while logging a
failure is the same trap described in
[validate-error-in-failure-factories.md](validate-error-in-failure-factories.md).
Either land that todo first, or make the formatting defensive.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
