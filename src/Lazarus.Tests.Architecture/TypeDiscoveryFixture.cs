using Lazarus.Public;
using Lazarus.Public.Configuration;

namespace Lazarus.Tests.Architecture;

public static class TypeDiscoveryFixture
{
    private const string INTERNAL_SLUG = ".Internal";
    private const string PUBLIC_SLUG = ".Public";

    private static readonly Type[] TYPES = typeof(ServiceCollectionExtensions).Assembly.GetTypes();

    public static IEnumerable<Type> GetInternalNamespaceTypes() =>
        TYPES.Where(t => t.Namespace?.Contains(INTERNAL_SLUG, StringComparison.Ordinal) == true);

    public static IEnumerable<Type> GetPublicNamespaceTypes() =>
        TYPES.Where(t => t.Namespace?.Contains(PUBLIC_SLUG, StringComparison.Ordinal) == true);
}
