using System.Reflection;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Results.Tests.Architecture;

/// <summary>
/// Results's architecture rules: the shared invariants from
/// <see cref="global::Architecture.Testing.ArchitectureTestsBase"/>, with the flat-public-API rule
/// narrowed for the discriminated-union shape. Encode each project-specific invariant the first time
/// it matters so drift trips the build.
/// </summary>
public sealed class ArchitectureTests : global::Architecture.Testing.ArchitectureTestsBase
{
    // The only public nested types this assembly is allowed to have: the two inhabitants of Result<T>.
    private const string ResultInhabitants = @"^Results\.Result`1\+(Success|Failure)$";

    protected override Assembly TargetAssembly => typeof(Unit).Assembly;

    protected override string RootNamespace => "Results";

    /// <summary>
    /// Narrows the base rule for the closed hierarchy. <c>Result&lt;T&gt;.Success</c> and
    /// <c>Result&lt;T&gt;.Failure</c> are public nested types on purpose: nesting is what makes them
    /// the only inhabitants of <c>Result&lt;T&gt;</c> and names that relationship in the type itself.
    /// The rule still fires for any other public nested type, so the exemption can't widen by accident.
    /// </summary>
    public override void PublicTypesAreNotNested() =>
        Verify(Types()
            .That()
            .AreNested()
            .And()
            .DoNotHaveNameContaining("<")
            .And()
            .DoNotHaveFullNameMatching(ResultInhabitants)
            .Should()
            .NotBePublic()
            .Because("the public API is intentionally flat and discoverable; a public nested type hides surface area inside another type. Result<T>'s two inhabitants are the documented exception.")
            .WithoutRequiringPositiveResults());
}
