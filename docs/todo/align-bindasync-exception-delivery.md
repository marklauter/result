---
title: Align exception delivery between the two BindAsync shapes
type: todo
summary: Success.BindAsync returns fn(Value) from a non-async method, so a pre-await throw escapes synchronously past the caller's try — while the ResultAsync extension of the same name captures it into the returned ValueTask.
tags: [correctness, async, api-contract]
created: 2026-07-28
priority: medium
status: open
---

## Start by pinning the failure

Starts red, and the assertion depends on which option below is chosen. For the
uniform-delivery options:

```csharp
var pending = Result.Success(1).BindAsync<int>(_ => throw new InvalidOperationException());
await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
```

Today this never reaches the assertion. The exception is thrown while
*constructing* `pending`, so the test fails on the first line. After the fix it
goes green. That first-line failure is the defect itself, which makes the test a
faithful reproduction.

The documentation-only option inverts the test: assert that `BindAsync` does
throw synchronously. That version is green on day one and pins current behavior
rather than proving a fix. Note in the commit which of the two the test is
doing.

`ResultAsyncTests` should get the mirror case for the extension overload, so the
pair of behaviors is pinned together.

## The defect

`Result<T>.Success.BindAsync` is a direct passthrough from a **non-async**
method:

```csharp
public override ValueTask<Result<TResult>> BindAsync<TResult>(Func<T, ValueTask<Result<TResult>>> fn) => fn(Value);
```

Because the method is not `async`, the call to `fn` runs on the caller's stack.
Any exception thrown before `fn` reaches its first suspension point propagates
**synchronously**, at the `BindAsync` call site, rather than being delivered
through the returned `ValueTask`.

The sibling extension `ResultAsync.BindAsync` is an `async` method, so the same
throw is captured into its returned value task. The two shapes with the same
name deliver continuation exceptions differently, and neither the XML docs nor
any test says which one a caller gets.

## Failure mode

```csharp
var pending = result.BindAsync(x => repo.LoadAsync(x));
// ...
try { return await pending; }
catch (Exception ex) { return Result.Failure<T>(...); }
```

Suppose `LoadAsync` throws before its first `await`, from an argument guard in a
non-async `ValueTask`-returning port or a null dereference in the lambda. The
throw happens at the `BindAsync` line, **outside** the `try`. The request
crashes instead of being converted to a failure.

The value-task surface encourages this split-construction-and-await shape, so
expect callers to write it.

## The tension

The passthrough is deliberate. It is the reason the sync-completion path
allocates nothing, which is the property the `Result<T>.BindAsync` XML doc
advertises: "a continuation that completes synchronously — a cache hit, a
memoized read — allocates nothing". Making the method `async` introduces the
state machine builder the current shape avoids.

Three options:

- Make `Success.BindAsync` `async` — uniform exception delivery, loses the
  zero-allocation passthrough.
- Wrap in try/catch and return a faulted `ValueTask` — keeps the fast path,
  costs a try/catch on every call, and needs care to preserve the stack trace.
- Keep the behavior and **document** it on both `BindAsync` members, so callers
  know the synchronous shape can throw at the call site.

The third is cheapest. The current state is the one to reject: the two shapes
differ and nothing records it.

Whatever is chosen, `Failure.BindAsync` (which returns
`ValueTask.FromResult(...)` and never invokes `fn`) means the behavior also
differs by inhabitant, the same theme as
[guard-delegate-parameters-in-combinators.md](guard-delegate-parameters-in-combinators.md).

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
