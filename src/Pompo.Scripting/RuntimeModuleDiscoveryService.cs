using System.Reflection;

namespace Pompo.Scripting;

public sealed class RuntimeModuleDiscoveryService
{
    public IReadOnlyList<PompoRuntimeModuleDescriptor> Discover(byte[] assemblyBytes)
    {
        ArgumentNullException.ThrowIfNull(assemblyBytes);

        using var stream = new MemoryStream(assemblyBytes);
        var assembly = Assembly.Load(stream.ToArray());
        return Discover(assembly);
    }

    public IReadOnlyList<PompoRuntimeModuleDescriptor> Discover(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly
            .GetTypes()
            .Where(IsRuntimeModuleType)
            .OrderBy(type => type.FullName ?? type.Name, StringComparer.Ordinal)
            .Select(type => new PompoRuntimeModuleDescriptor(type.FullName ?? type.Name, type))
            .ToArray();
    }

    private static bool IsRuntimeModuleType(Type type)
    {
        return type is { IsAbstract: false, IsClass: true } &&
            typeof(IPompoRuntimeModule).IsAssignableFrom(type);
    }
}
