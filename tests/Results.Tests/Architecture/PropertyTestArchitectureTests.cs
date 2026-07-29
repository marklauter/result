using ArchUnitNET.Fluent.Extensions;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace Results.Tests.Architecture;

/// <summary>
/// Architecture rules for this test assembly rather than the system under test, which is why they do not derive from
/// <see cref="global::Architecture.Testing.ArchitectureTestsBase"/> — that base evaluates against the production assembly.
/// </summary>
public sealed class PropertyTestArchitectureTests
{
    private static readonly ArchitectureModel Model =
        new ArchLoader().LoadAssemblies(typeof(PropertyCheck).Assembly).Build();

    /// <summary>
    /// Property tests reach CsCheck's sampler only through <see cref="PropertyCheck"/>. Sampling policy — iteration count, threading, and the fact that
    /// runs are unseeded by design — is a single decision recorded in one place; a test file calling <c>Sample</c> directly would carry its own copy of
    /// that policy, and the copies would drift silently because every one of them still passes. Generators are unrestricted: depending on
    /// <c>CsCheck.Gen</c> is how a test declares its inputs, and only <c>CsCheck.Check</c> runs them. The sampler is named as a string rather than a
    /// <c>typeof</c> so this rule does not become the first violation of itself. The exemption is the exact full name, not a substring: names like
    /// <c>SequencePropertyChecks</c> are what a second suite's author would pick, so a substring exemption would be widest precisely where the drift
    /// risk is highest.
    /// </summary>
    [Fact]
    public void OnlyPropertyCheckSamples()
    {
        var rule = Types()
            .That()
            .DoNotHaveFullName(typeof(PropertyCheck).FullName!)
            .And()
            .DoNotHaveNameContaining("<") // compiler-generated closures and state machines
            .Should()
            .NotDependOnAnyTypesThat()
            .HaveFullName("CsCheck.Check")
            .Because($"sampling policy lives in {nameof(PropertyCheck)}; a direct Sample call forks it into a copy that drifts without ever going red.")
            .WithoutRequiringPositiveResults();

        if (!rule.HasNoViolations(Model))
        {
            Assert.Fail(rule.Evaluate(Model).ToErrorMessage());
        }
    }
}
