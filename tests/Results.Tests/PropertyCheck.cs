using CsCheck;

namespace Results.Tests;

/// <summary>
/// The single entry point into CsCheck sampling for this assembly. Property tests state a law and call <see cref="Law{T}"/>; the corpus size and the
/// seeding policy are decided here once rather than per test file, so a second property-based suite — over <see cref="Error"/>, over
/// <see cref="ResultSequence"/>, over a type that does not exist yet — inherits the policy instead of forking its own. An architecture test pins that:
/// <see cref="PropertyCheck"/> is the only type in this assembly permitted to depend on <c>CsCheck.Check</c>.
/// </summary>
internal static class PropertyCheck
{
    /// <summary>Inputs generated per property. CsCheck's default is 100; these are cheap pure comparisons, so an order of magnitude more costs little.</summary>
    public const long Iterations = 1000;

    /// <summary>
    /// Checks <paramref name="law"/> holds for every input in a fixed corpus, shrinking to a minimal counterexample when it does not.
    /// </summary>
    /// <remarks>
    /// The corpus is generated rather than written out, but it is not random: each input is drawn from its own <see cref="PCG"/> seeded by index, so the
    /// same <see cref="Iterations"/> inputs are produced on every machine and every run. A law that goes red in CI goes red locally on the same input.
    /// <para>
    /// The obvious spelling — one <c>Sample</c> call with <c>iter: Iterations</c> — is not deterministic, and the near-miss is worth recording because it
    /// reads as though it is. CsCheck's <c>seed</c> argument seeds only the first iteration; the rest of the run draws from unseeded thread-local
    /// generators. That also makes the seed a failure prints a weaker tool than it appears: replaying it puts the failing input first, so the test does go
    /// red immediately, but the remainder of the run is fresh, so the counterexample reported on replay is usually a different one and the seed printed is
    /// different again. Seeding per input costs one <c>Sample</c> call per input and buys a corpus that is genuinely fixed.
    /// </para>
    /// <para>
    /// The cost is minimization. Shrinking spends iterations searching for a smaller failing input, and a budget of one leaves none, so a failure here
    /// reports the generated input as-is — the message reads "0 shrinks". The input is exact rather than minimal, and the seed printed alongside it
    /// reproduces exactly. To minimize, re-run that seed with a budget: <c>gen.Sample(law, seed: "&lt;printed&gt;", iter: 1000)</c> fails on its first
    /// iteration and spends the rest shrinking.
    /// </para>
    /// </remarks>
    public static void Law<T>(Gen<T> gen, Func<T, bool> law)
    {
        for (var i = 0UL; i < Iterations; i++)
            gen.Sample(law, seed: new PCG(1, i).ToString(), iter: 1, threads: 1);
    }
}
