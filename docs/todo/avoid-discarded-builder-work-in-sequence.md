---
title: Avoid discarded builder work in Sequence
summary: Sequence allocates both builders on every call and keeps filling the values builder after a failure is seen, so on the failure path all of that work is thrown away.
tags: [todo, efficiency, allocation]
created: 2026-07-28
priority: low
effort: low
status: open
---

## Start by measuring, not by testing

**No unit test pins this**, and that is the honest shape of the item. The
observable contract of `Sequence` does not change; only the allocation profile
does. A unit test asserting allocation counts would be pinning an implementation
detail, which the `csharp:writing-csharp` rule against testing outside the
contract rules out.

The evidence this needs is a benchmark, and the repo has no benchmark project.
That is the first task: stand one up with BenchmarkDotNet and a
`MemoryDiagnoser`, with cases for an all-success sequence, a first-element
failure, and a last-element failure.

Do not change the code before the benchmark exists. The
`csharp:writing-csharp` rule is that hot-path work happens "when a BenchmarkDotNet
number says so", and this todo is unmeasured today. If the number says the
current code is fast enough, close this and record the number.

The existing `ResultSequenceTests` cases keep the behavior pinned across any
change made here.

## The current shape

`ResultSequence.Sequence<T>` allocates both builders up front, unconditionally:

```csharp
var values = ImmutableArray.CreateBuilder<T>();
var errors = ImmutableArray.CreateBuilder<Error>();
```

The loop then has no `failed` flag, so `values.Add(success.Value)` keeps running
for every subsequent success even after the first failure has been recorded. The
return discards it:

```csharp
return errors.Count > 0
    ? Result.Failure<ImmutableArray<T>>(errors.ToImmutable())
    : Result.Success(values.ToImmutable());
```

So on any failing sequence, every value added and every growth of the values
builder is wasted. On any all-success sequence, the errors builder is allocated
and never touched.

Options if the benchmark justifies a change: allocate each builder lazily on
first use, stop adding to `values` once `errors.Count > 0`, or take a first pass
to size the builders when the source is an `ICollection<T>`. The accumulating
semantics must survive whichever is chosen — `Sequence` reports every error, not
the first, and that is the documented contract.

Note that [guard-null-elements-in-sequence-and-apply.md](guard-null-elements-in-sequence-and-apply.md)
rewrites this same loop. Land that one first; it is a correctness fix and this is
not.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
