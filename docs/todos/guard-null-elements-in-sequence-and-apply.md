---
title: Guard null elements in Sequence and the variadic Apply
type: todo
summary: Both element loops classify with `is` type patterns and no else, so a null element is silently ignored — Sequence returns a short Success and Apply counts it as a passing check.
tags: [correctness, null-safety, applicative]
created: 2026-07-28
priority: high
status: closed
---

## Resolution

Closed 2026-07-29 on the `ArgumentException` channel this note recommended.

`ResultSequence.Sequence<T>` gained an `else` arm and the variadic `Result.Apply`
an `is null` arm; both throw
`new ArgumentException($"input at index {i} is null", nameof(results))`. The
message carries the offending index and the parameter name is the collection,
because the collection is the argument and the index is what locates the defect
inside it. Both `<exception>` tags are on the methods.

The value channel was implemented first and reverted. That version added
`ErrorType.InvalidOperation`, an `Error.InvalidOperation` factory, and an
`ErrorCodes` class holding `sequence.null_input` and `apply.null_input`, and it
widened both of `Apply`'s fast paths to `&& nulls == 0` so the single-failure
shortcut could not return before the null's error was collected. It was reverted
against the throw-vs-return rule in `csharp:writing-csharp`: a null element is a
partiality the types were supposed to remove, so it belongs in the exception
channel rather than modeled as a domain outcome that a caller's error handler has
to distinguish from real validation failures. `ErrorCodes.cs` was deleted with
it, and `ErrorType.InvalidOperation` and `Error.InvalidOperation` were removed
once the walk-back left them without a library-emitted use.

Eight tests cover the contract, four per method: the null at each of three
positions asserting the index appears in the message, an unfilled
`new Result<T>[3]` slot, which reaches the defect with no `null!` and no
suppression, a null alongside a modeled failure to pin that the throw beats
accumulation, and a `ParamName` assertion. `Assert.Throws<T>` is exact-match in
xUnit, so drift to `ArgumentNullException` fails.

A null *collection* keeps its existing treatment: `Sequence` keeps
`ArgumentNullException.ThrowIfNull(results)` and the binary `Apply` keeps its two
guards. Both are the same channel as the element throw now, so the surface is
consistent — the note's own consistency requirement is met.

## Start by pinning the failure

Both start red. Write these before touching `src/Results`:

```csharp
// ResultSequenceTests — today returns a Success carrying one element
Assert.Throws<ArgumentException>(
    () => new Result<int>[] { Result.Success(1), null! }.Sequence());

// ApplyUnitTests — today returns Success(Unit.Value)
Assert.Throws<ArgumentException>(
    () => Result.Apply(Result.Success(Unit.Value), null!));
```

Swap the expected exception if the returned-value channel is chosen instead.
Either way the current build reports success and the test says it must not.

Add a null element in first, middle, and last position. Then add one to an
otherwise all-success batch, the shape that returns a false success rather than
merely losing data.

Related: [test-the-argumentnullexception-guards.md](test-the-argumentnullexception-guards.md)
covers the guards that already exist and are untested; those do not start red.

## The defect

Two methods walk a collection of results and classify each element with `is`
type patterns. A `null` element matches neither pattern, and neither loop has a
final `else`, so the null is silently skipped. Both admit invalid input and
return a success.

## `ResultSequence.Sequence<T>`

The `foreach` in `ResultSequence.Sequence<T>` is:

```csharp
if (result is Result<T>.Success success)
    values.Add(success.Value);
else if (result is Result<T>.Failure failure)
    errors.AddRange(failure.Errors);
```

`ArgumentNullException.ThrowIfNull(results)` at the top of the method guards the
sequence itself, never its elements.

Verified: `new Result<int>[]{ Result.Success(1), null! }.Sequence()` returns a
`Success` carrying a single element. The caller gets a success whose array is
shorter than the input and has no way to detect it. Indices no longer line up
with the source collection.

A null reaches the sequence from an adapter that returns `null` for an unmapped
case, or from a pre-sized buffer (`new Result<int>[3]`) that a projection only
partly fills. A batch parse then writes N-1 rows and reports total success.

## `Result.Apply(params ReadOnlySpan<Result<Unit>>)`

The classification loop in the variadic `Apply` overload counts failures with:

```csharp
if (results[i] is Result<Unit>.Failure f)
```

A null element fails that test, so `failures` is never incremented for it. If
the assigned checks all passed, the method falls through to
`return Success(Unit.Value)`.

Verified: `Result.Apply(Result.Success(Unit.Value), null!)` returns a success.

This overload is the validation-accumulation entry point, so the null admits
unvalidated input rather than dropping data:

```csharp
var checks = new Result<Unit>[3];
checks[0] = Result.Validate(...);
checks[1] = Result.Validate(...);
// slot 2 never assigned
return Result.Apply(checks);
```

The caller treats unvalidated input as validated and writes it.

## The surface already guards elsewhere

The binary `Apply<T, TResult>(Result<Func<T, TResult>>, Result<T>)` overload in
the same class already does `ArgumentNullException.ThrowIfNull` on both of its
`Result` arguments and documents it with an `<exception>` tag. The variadic
overload sitting below it does not, and neither does `Sequence`. Whatever is
chosen here should make the three consistent.

## What to decide

The guard is easy; the channel is the design question. Settle it before writing
code, because it sets the pattern for the whole surface. Per the
`csharp:writing-csharp` throw-vs-return rule, a null element is a caller bug, a
partiality the types were supposed to remove, rather than a modeled domain
outcome. That argues for `ArgumentNullException`, matching the binary `Apply`,
over folding it into the error accumulation as a synthesized `Error`.

Adding a `where T : notnull` constraint does not close this hole. The elements
are `Result<T>` references, not `T` values. See
[constrain-result-value-to-notnull.md](constrain-result-value-to-notnull.md),
which is a separate defect.

Whichever channel is chosen, apply it identically in both methods and document
it with an `<exception>` tag matching the binary `Apply`'s.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`. The Results module is
at 100/100/100 today, so the new branches must come with tests or the 95%
ratchet in `Directory.Build.props` will catch the gap.
