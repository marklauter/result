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

    /// <summary>
    /// Every generic parameter in the public API carries a <c>notnull</c> constraint, so a
    /// null-carrying success is unrepresentable at compile time. ArchUnitNET has no predicate for
    /// nullability metadata, so this rule reads the compiler-emitted encoding directly:
    /// <c>where T : notnull</c> is <c>NullableAttribute(1)</c> on the generic parameter, compressed
    /// into an enclosing <c>NullableContextAttribute(1)</c> when it matches the ambient context.
    /// </summary>
    [Fact]
    public void PublicGenericParametersAreConstrainedNotNull()
    {
        var exported = TargetAssembly.GetExportedTypes();
        var typeParameters = exported
            .Where(static t => t.IsGenericTypeDefinition)
            .SelectMany(static t => t.GetGenericArguments())
            .Concat(exported
                .SelectMany(static t => t.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(static m => m.IsGenericMethodDefinition)
                .SelectMany(static m => m.GetGenericArguments()))
            .ToList();

        Assert.NotEmpty(typeParameters);
        Assert.All(typeParameters, static p => Assert.True(
            HasNotNullConstraint(p),
            $"{p.DeclaringMethod?.Name ?? p.DeclaringType?.Name} declares {p.Name} without a notnull constraint"));
    }

    private static bool HasNotNullConstraint(Type typeParameter)
    {
        if (NullableFlag(typeParameter.GetCustomAttributesData(), "NullableAttribute") is { } direct)
            return direct == 1;

        if (typeParameter.DeclaringMethod is { } method
            && NullableFlag(method.GetCustomAttributesData(), "NullableContextAttribute") is { } methodContext)
            return methodContext == 1;

        for (var scope = typeParameter.DeclaringType; scope is not null; scope = scope.DeclaringType)
        {
            if (NullableFlag(scope.GetCustomAttributesData(), "NullableContextAttribute") is { } typeContext)
                return typeContext == 1;
        }

        return NullableFlag(typeParameter.Module.GetCustomAttributesData(), "NullableContextAttribute") == 1;
    }

    private static byte? NullableFlag(IEnumerable<CustomAttributeData> attributes, string attributeName)
    {
        var attribute = attributes.SingleOrDefault(a =>
            a.AttributeType.Namespace == "System.Runtime.CompilerServices"
            && a.AttributeType.Name == attributeName);

        // NullableAttribute has a second, byte[] constructor whose element 0 is the top-level
        // annotation; treating that form as absent would let an attributed parameter fall through
        // to the ambient context and pass unchecked.
        return attribute?.ConstructorArguments[0].Value switch
        {
            byte flag => flag,
            IReadOnlyList<CustomAttributeTypedArgument> flags when flags.Count > 0 => flags[0].Value as byte?,
            _ => null,
        };
    }
}
