---
title: Prove the functor, applicative, and monad laws instead of sampling them
summary: ResultLawTests asserts each law at one fixed input, so an implementation correct only at the sampled values passes the suite that claims to pin the algebraic contract.
tags: [testing, laws, property-based]
created: 2026-07-28
priority: medium
status: open
---

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
