---
title: Prove the functor, applicative, and monad laws instead of sampling them
type: todo
summary: ResultLawTests asserts each law at one fixed input, so an implementation correct only at the sampled values passes the suite that claims to pin the algebraic contract.
tags: [testing, laws, property-based]
created: 2026-07-28
priority: medium
status: closed
---

## Resolution

Closed 2026-07-29. The twenty-two single-value `[Fact]`s in
`tests/Results.Tests/ResultLawTests.cs` are now fourteen properties, each checked
over 1000 generated inputs. Generators live in
`tests/Results.Tests/ResultGen.cs`, sampling policy in
`tests/Results.Tests/PropertyCheck.cs`, and
`tests/Results.Tests/Architecture/PropertyTestArchitectureTests.cs` pins the
policy in one place by failing the build if any other type calls CsCheck's
sampler directly.

CsCheck 4.7.0, not the FsCheck this note prescribed. FsCheck generates real
arbitrary functions and CsCheck does not, which is the one place CsCheck is
genuinely weaker — the composition and associativity arrows are built
structurally instead (`a * x + b`, `"{tag}:{x + k}"`). Everything else favored
CsCheck: no FSharp.Core in the test tree, a C#-native API rather than F#-flavored
attributes, and better shrinking.

Two things the note did not anticipate. The first is that the obvious spelling of
a property test is not deterministic: CsCheck's `seed` argument seeds only the
first iteration, and the rest of the run draws from unseeded thread-local
generators, so one `Sample(law, seed, iter: 1000)` call is unseeded randomness
wearing a seed. `PropertyCheck.Law` therefore seeds per input — `iter: 1`
against a `PCG` keyed by index — so the corpus is fixed across machines and runs
and the house rule is satisfied rather than deviated from. The cost is
minimization: a budget of one leaves no iterations to shrink with, so a failure
reports its input exactly rather than minimally, and shrinking becomes an opt-in
second step re-running the printed seed with a budget. That trade is argued in
the remarks on `PropertyCheck.Law`.

The second is that the four applicative laws turn out to be satisfied by an
implementation that accumulates errors in the opposite order — reversing every
concatenation reverses both sides of each law equally — so `Apply`'s documented
function-errors-then-argument-errors contract needed a property of its own
rather than falling out of the laws.

A pass now means the law held for all 1000 inputs of a fixed corpus, which is
what the note asked for, and the corpus is the same one everywhere. What remains unproven is `Sequence` and the variadic
`Apply(Unit)` overload: see
[add-property-tests-for-sequence-and-variadic-apply.md](add-property-tests-for-sequence-and-variadic-apply.md).

## Start by pinning the behavior

**Does not start red.** The implementation satisfies the laws, so a
property-based version passes on first run. What changes is what a pass means:
today it means "the law holds at 42", and afterwards it means "the law held for
every generated input in this run".

Add a generator-driven library (FsCheck is the usual choice for xUnit) and
rewrite one law first as the pattern the rest copy:

```csharp
[Property]
public Property Functor_Identity_Success(int x) =>
    (Result.Success(x).Map(v => v) == Result.Success(x)).ToProperty();
```

Per the `csharp:writing-csharp` rule that the first slice sets the pattern, get
that first conversion right before doing the other nine.

Two things to settle while converting. Generating a `Result<T>` needs a custom
`Arbitrary` covering both inhabitants, or the generated cases will be all
success and the failure short-circuit paths stay sampled. And the functions in
the composition and associativity laws should be generated too, not fixed
locally, or the law is still only proved for `x + 1` and `v => $"v={x}"`.

## The gap

`ResultLawTests` documents its own purpose: "These pin the algebraic contract:
implementation changes that break a law will break a test here." Each test then
asserts the law at a single hard-coded value.

`Functor_Identity_Success` checks `Result.Success(42).Map(x => x)`.
`Functor_Composition_Success` fixes `fa = Result.Success(10)`, `f = x + 1`, and
`g = v => $"v={x}"`. The applicative and monad laws follow the same shape, with
`ErrA` and `ErrB` as the only two errors in the file.

A law is a universally quantified statement. An implementation that special-cased
`42`, or that broke only on negative inputs, on `int.MinValue`, on an empty
string, or on a failure carrying more than two errors, satisfies every assertion
in the file. The tests are worth keeping as examples; they are not the proof the
class doc claims.

This is the one item from the review where the fix changes the test strategy
rather than a test, which makes it larger than the other
test-strength todos.

## Verify

`dotnet format "Results.slnx" --severity info --verify-no-changes`, then
`dotnet build -c Release`, then `dotnet test -c Release`. A new package goes in
`Directory.Packages.props`, since Central Package Management is on and the
`.csproj` references by name only.
