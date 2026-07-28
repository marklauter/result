using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Extensions;
using ArchUnitNET.Loader;
using System.Collections.Concurrent;
using System.Reflection;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace Architecture.Testing;

/// <summary>
/// One source of truth for the universal architecture rules the writing-csharp discipline encodes:
/// flat namespace, sealed concretes, no public instance state, no public nested types, Async suffix.
/// Each test assembly derives a sealed class, points <see cref="TargetAssembly"/> at its system under
/// test, names its <see cref="RootNamespace"/>, and adds any assembly-specific rules as extra <c>[Fact]</c>s.
/// </summary>
public abstract class ArchitectureTestsBase
{
    // Loading and building an architecture model is expensive; cache one per assembly so every
    // derived class and every [Fact] (xUnit news up the class per test) reuses the same model.
    private static readonly ConcurrentDictionary<Assembly, ArchitectureModel> Models = new();

    /// <summary>The assembly whose types the rules are evaluated against.</summary>
    protected abstract Assembly TargetAssembly { get; }

    /// <summary>The single flat namespace every type in <see cref="TargetAssembly"/> must reside in.</summary>
    protected abstract string RootNamespace { get; }

    private ArchitectureModel Model =>
        Models.GetOrAdd(TargetAssembly, static assembly => new ArchLoader().LoadAssemblies(assembly).Build());

    [Fact]
    public void AllTypesResideInRootNamespace() =>
        Verify(Types()
            .That()
            .DoNotHaveNameContaining("<") // exclude compiler-generated closures / async state machines
            .Should()
            .ResideInNamespace(RootNamespace)
            // a freshly scaffolded assembly may match no subjects yet; absence of a violation is a pass.
            .Because($"{RootNamespace} is intentionally a single, flat namespace; new sub-namespaces are a design change, not a drive-by.")
            .WithoutRequiringPositiveResults());

    [Fact]
    public void ConcreteClassesAreSealed() =>
        Verify(Classes()
            .That()
            .AreNotAbstract() // C# 'static' compiles to 'abstract sealed' — this also excludes static factories
            .And()
            .DoNotHaveNameContaining("<")
            .Should()
            .BeSealed()
            .Because("writing-csharp: seal records and classes by default (enables devirtualization).")
            .WithoutRequiringPositiveResults());

    [Fact]
    public virtual void InstanceFieldsAreNotPublic() =>
        Verify(FieldMembers()
            .That()
            .AreNotStatic() // const / static readonly may be public; instance state must not be.
            .And()
            .DoNotHaveNameContaining("<") // exclude compiler-generated backing fields
            .And()
            .DoNotHaveName("value__") // every enum carries a public instance field of this name; it is the enum, not state
            .Should()
            .NotBePublic()
            .Because("writing-csharp: immutable-by-default; no public mutable instance state.")
            .WithoutRequiringPositiveResults());

    [Fact]
    public virtual void PublicTypesAreNotNested() =>
        Verify(Types()
            .That()
            .AreNested()
            .And()
            .DoNotHaveNameContaining("<") // exclude compiler-generated closures / state machines
            .Should()
            .NotBePublic()
            // an assembly may legitimately have no nested types at all
            .Because("the public API is intentionally flat and discoverable; a public nested type hides surface area inside another type.")
            .WithoutRequiringPositiveResults());

    [Fact]
    public void AsyncMethodsHaveAsyncSuffix() =>
        Verify(MethodMembers()
            .That()
            // A member's FullName begins with its return type; match Task / Task<T> / ValueTask / ValueTask<T>.
            // ArchUnit's HaveReturnType(typeof(Task<>)) does not match constructed generics, so match on the name.
            .HaveFullNameMatching(@"^System\.Threading\.Tasks\.(Task|ValueTask)(`\d+)?(<.*>)? ")
            .And()
            .DoNotHaveNameContaining("<") // exclude compiler-generated async state-machine members
            .And()
            .DoNotHaveNameContaining("Invoke(") // exclude delegate Invoke/BeginInvoke/EndInvoke (not user methods)
            .Should()
            // The member Name carries its parameter list (e.g. "InvokeAsync(TRequest)"), so assert the
            // identifier ends in Async immediately before the '(' rather than using HaveNameEndingWith.
            .HaveNameMatching(@"Async\(")
            .Because("writing-csharp: Async suffix on async methods — the suffix is the only signal a call returns a Task that must be awaited.")
            .WithoutRequiringPositiveResults());

    /// <summary>Evaluate a rule against <see cref="TargetAssembly"/>, failing with ArchUnit's diagnostic on violation.</summary>
    /// <param name="rule">The ArchUnit rule to evaluate.</param>
    protected void Verify(IArchRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (!rule.HasNoViolations(Model))
        {
            Assert.Fail(rule.Evaluate(Model).ToErrorMessage());
        }
    }
}
