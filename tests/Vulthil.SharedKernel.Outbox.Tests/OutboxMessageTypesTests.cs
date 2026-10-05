using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

/// <summary>
/// Pins the type part of the outbox row format: a row stores the type's full name, and the lookup finds the type again
/// among the loaded assemblies, counts a forwarded type once, and fails for an unknown or an ambiguous name.
/// </summary>
public sealed class OutboxMessageTypesTests : BaseUnitTestCase
{
    [Fact]
    public void ARowStoresTheFullNameAndResolvesItBackToTheType()
    {
        // Act
        var name = OutboxMessageTypes.NameOf(typeof(OrderPlaced));
        var type = OutboxMessageTypes.Resolve(name);

        // Assert
        name.ShouldBe(typeof(OrderPlaced).FullName);
        type.ShouldBe(typeof(OrderPlaced));
    }

    [Fact]
    public void AGenericMessageTypeResolvesBackToItsType()
    {
        // Act
        var type = OutboxMessageTypes.Resolve(OutboxMessageTypes.NameOf(typeof(Envelope<OrderPlaced>)));

        // Assert
        type.ShouldBe(typeof(Envelope<OrderPlaced>));
    }

    [Fact]
    public void AnAssemblyQualifiedNameResolvesThroughItsAssembly()
    {
        // Act
        var type = OutboxMessageTypes.Resolve(typeof(OrderPlaced).AssemblyQualifiedName!);

        // Assert
        type.ShouldBe(typeof(OrderPlaced));
    }

    [Fact]
    public void ATypeThatAFacadeForwardsCountsOnce()
    {
        // Arrange
        var facade = Assembly.Load("netstandard");
        var name = typeof(Regex).FullName!;

        // Act
        var type = OutboxMessageTypes.Resolve(name);

        // Assert
        facade.GetType(name).ShouldBe(typeof(Regex));
        type.ShouldBe(typeof(Regex));
    }

    [Fact]
    public void AnUnknownTypeNameThrows()
    {
        // Arrange
        var name = $"Vulthil.SharedKernel.Outbox.Tests.Missing{Guid.NewGuid():N}";

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => OutboxMessageTypes.Resolve(name));

        // Assert
        exception.Message.ShouldContain("no loaded assembly defines it");
    }

    [Fact]
    public void TwoLoadedAssembliesThatDefineDifferentTypesWithOneNameMakeTheNameAmbiguous()
    {
        // Arrange
        var name = $"Probes.Duplicate{Guid.NewGuid():N}";
        DefineDynamicType("ProbeAssemblyA", name);
        DefineDynamicType("ProbeAssemblyB", name);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => OutboxMessageTypes.Resolve(name));

        // Assert
        exception.Message.ShouldContain("is ambiguous");
        exception.Message.ShouldContain("'ProbeAssemblyA");
        exception.Message.ShouldContain("'ProbeAssemblyB");
    }

    [Fact]
    public void ATypeWithoutAFullNameCannotBeStored()
    {
        // Arrange
        var genericParameter = typeof(Envelope<>).GetGenericArguments()[0];

        // Act & Assert
        Should.Throw<ArgumentException>(() => OutboxMessageTypes.NameOf(genericParameter));
    }

    private static void DefineDynamicType(string assemblyName, string typeName)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        assembly.DefineDynamicModule(assemblyName).DefineType(typeName, TypeAttributes.Public).CreateType();
    }

    public sealed record OrderPlaced(Guid OrderId);

    public sealed record Envelope<T>(T Message);
}
