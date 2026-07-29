---
title: Decide the ConfigureAwait policy for caller-supplied callbacks
summary: The ResultAsync extensions invoke caller callbacks after ConfigureAwait(false), so a UI caller's terminal MatchAsync runs off the captured context and throws cross-thread.
tags: [async, api-contract, decision]
created: 2026-07-28
priority: low
status: open
---

## Start by pinning the failure

**Settle the decision below before writing this test**, because the test locks
the answer in. It starts red only against the "drop ConfigureAwait" option, and
it needs a harness:

```csharp
// install a custom SynchronizationContext, then assert inside the callback:
await pending.MatchAsync(
    v => { Assert.Same(installedContext, SynchronizationContext.Current); return v; },
    e => 0);
```

Today `SynchronizationContext.Current` is null inside the callback, so the
assertion fails. After dropping `ConfigureAwait(false)` it passes.

Two cautions. The test double has to implement `Post` and `Send` correctly, and
thread-affinity tests go flaky when that implementation is wrong. The
`csharp:writing-csharp` position is that a flaky test is a defect, to be fixed on
discovery. Under the documentation-only option there is nothing to test at all:
the behavior is already correct and the change is prose.

## The defect

Every method in `ResultAsync` awaits its receiver with `ConfigureAwait(false)`
and then invokes a caller-supplied delegate in the continuation. `MatchAsync` is
the clearest case, because its callbacks are the terminal step of a chain:

```csharp
(await result.ConfigureAwait(false)).Match(onSuccess, onError);
```

`MapAsync` and both `BindAsync` overloads have the same shape.

## Failure mode

A WPF or WinForms caller writes the idiomatic terminal step:

```csharp
await repo.LoadAsync(id)
    .MapAsync(Render)
    .MatchAsync(
        v => statusLabel.Content = v,
        errors => statusLabel.Content = errors[0].Message);
```

Because the receiver was awaited without capturing the synchronization context,
`onSuccess`/`onError` execute on a thread-pool thread, and the control
assignment throws:

> InvalidOperationException: The calling thread cannot access this object
> because a different thread owns it

The error callback runs only when something has already gone wrong, which makes
it the least-exercised branch in a UI app.

## The verdict was PLAUSIBLE, not CONFIRMED

That verdict is correct. `ConfigureAwait(false)` is the right default for
library plumbing, and a caller who needs the context can marshal explicitly.
What remains is narrower: the library runs *user code* under a policy chosen for
*its own* plumbing, and says nothing about it.

Whether this is worth changing depends on whether the library targets UI hosts
at all. ASP.NET Core has no synchronization context, so server-side consumers
are unaffected.

Options, in increasing cost:

- **Document it.** Add to the `ResultAsync` class doc that callbacks run without
  the captured context and a UI caller must marshal. Zero code change.
- **Overloads taking a `bool continueOnCapturedContext`.** Explicit, verbose,
  doubles the extension surface.
- **Drop `ConfigureAwait(false)` on the callback-invoking methods.** Matches
  caller expectations, costs the context capture on every await, and is the
  wrong default for server-side consumers.

Record the outcome under `docs/decisions/`. It is a public-contract choice and
awkward to reverse.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
