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

    /// <summary>
    /// Every public instance method that takes a delegate is non-virtual, so its null guard lives on
    /// the single base entry point and dispatch cannot route around it. A virtual delegate-taking
    /// member would put the guard back in the inhabitants, where the next one can omit it and the
    /// null contract silently diverges by inhabitant again.
    /// </summary>
    [Fact]
    public void PublicDelegateTakingMethodsAreNonVirtual()
    {
        var methods = TargetAssembly.GetExportedTypes()
            .SelectMany(static t => t.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(static m => m.GetParameters().Any(static p =>
                typeof(Delegate).IsAssignableFrom(p.ParameterType)))
            .ToList();

        Assert.NotEmpty(methods);
        Assert.All(methods, static m => Assert.False(
            m.IsVirtual,
            $"{m.DeclaringType?.Name}.{m.Name} takes a delegate through a virtual entry point; guard it on a non-virtual wrapper delegating to a private protected core"));
    }

    /// <summary>
    /// No public instance property on an exported type is typed <see cref="string"/>, except the
    /// <c>Value</c> projection a readonly value-type wrapper declares. A bare string property is a
    /// primitive carrying domain meaning the type system no longer records — two of them are
    /// assignment-compatible, so callers can transpose them and still compile. Wrap the primitive
    /// in a <c>readonly record struct</c> and expose it as <c>Value</c>.
    /// </summary>
    [Fact]
    public void PublicStringPropertiesAreValueWrapperProjectionsOnly()
    {
        var properties = TargetAssembly.GetExportedTypes()
            .SelectMany(static t => t.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(static p => p.PropertyType == typeof(string))
            .ToList();

        Assert.NotEmpty(properties);
        Assert.All(properties, static p => Assert.True(
            p.Name == "Value" && IsReadOnlyValueType(p.DeclaringType),
            $"{p.DeclaringType?.Name}.{p.Name} exposes a bare string; wrap the primitive in a readonly record struct and project it through Value"));
    }

    /// <summary>
    /// The two properties that motivated the rule above, pinned by name: <c>Error.Code</c> and
    /// <c>Error.Message</c> carry their own wrapper types, so transposed factory arguments are a
    /// compile error rather than a silently swapped pair of strings.
    /// </summary>
    [Fact]
    public void ErrorCodeAndMessageAreTypedWrappers()
    {
        Assert.Equal(typeof(ErrorCode), typeof(Error).GetProperty(nameof(Error.Code))!.PropertyType);
        Assert.Equal(typeof(ErrorMessage), typeof(Error).GetProperty(nameof(Error.Message))!.PropertyType);
    }

    private static bool IsReadOnlyValueType(Type? type) =>
        type is { IsValueType: true }
        && type.GetCustomAttributesData().Any(static a =>
            a.AttributeType.Namespace == "System.Runtime.CompilerServices"
            && a.AttributeType.Name == "IsReadOnlyAttribute");

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
