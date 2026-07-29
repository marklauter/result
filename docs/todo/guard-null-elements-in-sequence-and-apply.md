---
title: Guard null elements in Sequence and the variadic Apply
summary: Both element loops classify with `is` type patterns and no else, so a null element is silently ignored — Sequence returns a short Success and Apply counts it as a passing check.
tags: [todo, correctness, null-safety, applicative]
created: 2026-07-28
priority: high
effort: low
status: open
---

Two methods walk a collection of results and classify each element with `is`
type patterns. A `null` element matches neither pattern, and neither loop has a
final `else`, so the null is silently skipped. Both then report success.

This is the highest-severity pair in the review: they admit invalid input and
return a value that says everything worked.

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

The realistic path in: an adapter that returns `null` for an unmapped case, or a
pre-sized buffer (`new Result<int>[3]`) that a projection only partly fills. A
batch parse then writes N-1 rows and reports total success.

## `Result.Apply(params ReadOnlySpan<Result<Unit>>)`

The classification loop in the variadic `Apply` overload counts failures with:

```csharp
if (results[i] is Result<Unit>.Failure f)
```

A null element fails that test, so `failures` is never incremented for it. If
the assigned checks all passed, the method falls through to
`return Success(Unit.Value)`.

Verified: `Result.Apply(Result.Success(Unit.Value), null!)` returns a success.

The failure mode is worse than Sequence's, because this overload is the
validation-accumulation entry point:

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

## Failing test

Both start red. Against the current build:

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

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`. The Results module is
at 100/100/100 today, so the new branches must come with tests or the 95%
ratchet in `Directory.Build.props` will catch the gap.
