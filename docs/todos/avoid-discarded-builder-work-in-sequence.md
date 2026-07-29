---
title: Avoid discarded builder work in Sequence
type: todo
summary: Sequence allocates both builders on every call and keeps filling the values builder after a failure is seen, so on the failure path all of that work is thrown away.
tags: [efficiency, allocation]
created: 2026-07-28
priority: low
status: closed
---

## Resolution

Closed 2026-07-29. Landed without the benchmark on Mark's direction, after the
alloc-versus-iterate-twice question resolved per overload: the `IEnumerable`
overload cannot enumerate twice (one-shot sources; CA1851), so it stays
single-pass and the only removable waste is speculative allocation and
post-failure appends. Both builders are now lazy — `errors is null` doubles as
the stop-feeding-values guard — so all-success never allocates the errors
builder, a failure before the first success never allocates the values builder,
and successes after the first failure do no work. The successes collected before
the first failure remain the irreducible cost of a single pass, stated in a
comment at the site. Existing `ResultSequenceTests` covered every new branch;
no test changes were needed. The two-pass zero-waste shape (scan and count,
then build exactly one right-sized array) belongs to the known-count overloads
in [[add-sequence-overloads-mirroring-failures-set]].

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
for every subsequent success even after the first failure has been recorded. As
of 2026-07-29 the loop also tracks an `index` and throws `ArgumentException` on
a null element; any rewrite preserves that arm. The return discards the values:

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

[guard-null-elements-in-sequence-and-apply.md](guard-null-elements-in-sequence-and-apply.md)
landed its rewrite of this loop on 2026-07-29, so that ordering constraint is
discharged. [[add-sequence-overloads-mirroring-failures-set]] presets builder
capacity in its span overload, which covers part of this item for known-count
sources; consider landing the two together so the loop is rewritten once.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`.
