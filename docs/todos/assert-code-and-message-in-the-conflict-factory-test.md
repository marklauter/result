---
title: Assert Code and Message in the Error.Conflict factory test
type: todo
summary: Conflict_CreatesErrorWithConflictType asserts only Type, while the Validation, NotFound, and Gone tests assert Type, Code, and Message.
tags: [testing, error]
created: 2026-07-28
priority: low
status: closed
---

## Resolution

Closed 2026-07-29. The two assertions were added to
`Conflict_CreatesErrorWithConflictType`, asserting through `.Code.Value` and
`.Message.Value` since [[wrap-error-code-and-message-as-value-types]] landed
typed wrappers in the meantime. That landing also retired most of this todo's
threat: transposed factory arguments no longer compile. What the assertions
still pin is a factory body that drops or hardcodes its arguments, plus symmetry
with the other three factory tests. The body below predates the wrappers —
`Error.Create` and the string-typed properties it mentions are gone.

## Start by pinning the behavior

**Does not start red.** `Error.Conflict` already passes its arguments through
correctly, so the added assertions pass on first run. This closes a coverage
asymmetry rather than fixing a defect.

```csharp
var error = Error.Conflict("err.version.conflict", "version mismatch");
Assert.Equal(ErrorType.Conflict, error.Type);
Assert.Equal("err.version.conflict", error.Code);      // add
Assert.Equal("version mismatch", error.Message);        // add
```

## The gap

`ErrorTests` covers the five `Error` factories unevenly.
`Validation`, `NotFound`, and `Gone` each assert all three properties.
`Conflict_CreatesErrorWithConflictType` asserts `Type` alone, so a `Conflict`
factory that transposed or dropped its two string arguments would pass.

`Undefined` looks like the same gap and is not.
`Undefined_CreatesErrorWithUndefinedType` also asserts `Type` alone, but
`Undefined_FactoryInstance_ReadsCodeAndMessageNormally` covers its code and
message separately, and deliberately: it exists to show that
`Type == Undefined` is not the uninitialized discriminator. Leave that pair
alone. `Conflict` is the only factory with no code-and-message coverage
anywhere.

This gap is invisible to the coverage gate, because the line and branch through
`Error.Create` are already executed by the assertion on `Type`.

Related: [wrap-error-code-and-message-as-value-types.md](wrap-error-code-and-message-as-value-types.md)
would make the transposition this test guards against impossible to compile, at
which point the assertions become redundant.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
