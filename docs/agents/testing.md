# Testing

## Philosophy

**Don't test what you don't own.** Tests exercise the behavior of `Results`, not the BCL. No assertions on how `ImmutableArray<T>` stores elements or how `ValueTask<T>` schedules — focus on the contract: given inputs, does the type produce the right result and the right errors in the right order?

**Test the contract, not the construction.** Assert on what a method promises, not how it does it. No assertions on private state or internal calls — refactoring internals should not break tests.

**Tests are documentation.** A test name describes the scenario and the guaranteed outcome, in `Method_Scenario_Outcome` form (e.g. `Failure_ParamsFactory_EmptyArray_Throws`). `IDE1006` and `CA1707` are suppressed in test projects so underscored, descriptive names are fine.

**Laws are part of the contract.** `ResultLawTests` covers the functor and monad laws: identity, composition, left and right identity, associativity. A change to `Map` or `Bind` that passes the behavioral tests but breaks a law is a regression.

## Setup

Any project named `*.Tests` is auto-configured by [`Directory.Build.props`](../../Directory.Build.props): `xunit.v3`, runner, test SDK, coverlet, `IsTestProject`, and the `Xunit` global using are applied automatically. Every non-test project also grants internals to its `<ProjectName>.Tests` sibling automatically — no hand-written `InternalsVisibleTo`. Add a `ProjectReference` to the system under test and start writing tests.

`tests/Architecture.Testing` is a class library carrying the shared ArchUnitNET base rules; `Results.Tests/Architecture/ArchitectureTests.cs` derives from it. It is excluded from coverage measurement — see `architecture.md`.

## Coverage

The ratchet is 100% line, branch, and method as a **per-class minimum**. New code arrives with tests or the build goes red. Never lower `Threshold` to make a build pass. If a branch is unreachable, restructure the code so the compiler stops emitting it — see the `Apply` truth-table note in `architecture.md`.

## Conventions

- xUnit v3 — never reference legacy `xunit`, `xunit.core`, or `xunit.assert`.
- Pass `TestContext.Current.CancellationToken` to any method that accepts one (xUnit1051).
- Versions are managed centrally in `Directory.Packages.props` — don't pin in csprojs.
- Run with `dotnet test`.
