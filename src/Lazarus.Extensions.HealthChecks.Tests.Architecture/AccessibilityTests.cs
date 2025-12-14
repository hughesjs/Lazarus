namespace Lazarus.Extensions.HealthChecks.Tests.Architecture;


public class AccessibilityTests
{
    [Test]
    [MethodDataSource(typeof(TypeDiscoveryFixture), nameof(TypeDiscoveryFixture.GetInternalNamespaceTypes))]
    public async Task AllTypesInInternalNamespaceArePrivateOrInternal(Type type) => await Assert.That(type).IsNotPublic();

    [Test]
    [MethodDataSource(typeof(TypeDiscoveryFixture), nameof(TypeDiscoveryFixture.GetPublicNamespaceTypes))]
    public async Task AllTypesInPublicNamespaceArePublic(Type type) => await Assert.That(type).IsPublic();
}
