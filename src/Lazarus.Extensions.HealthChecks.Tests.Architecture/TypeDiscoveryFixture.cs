using System.Runtime.CompilerServices;
using Lazarus.Extensions.HealthChecks.Public;

namespace Lazarus.Extensions.HealthChecks.Tests.Architecture;

public static class TypeDiscoveryFixture
{
    private const string INTERNAL_SLUG = ".Internal";
    private const string PUBLIC_SLUG = ".Public";

    private static readonly Type[] TYPES = typeof(HealthCheckExtensions).Assembly.GetTypes()
        .Where(t => !IsCompilerGenerated(t))
        .ToArray();

    public static IEnumerable<Type> GetInternalNamespaceTypes() =>
        TYPES.Where(t => t.Namespace?.Contains(INTERNAL_SLUG, StringComparison.Ordinal) == true);

    public static IEnumerable<Type> GetPublicNamespaceTypes() =>
        TYPES.Where(t => t.Namespace?.Contains(PUBLIC_SLUG, StringComparison.Ordinal) == true);

    private static bool IsCompilerGenerated(Type type) =>
        type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
}
