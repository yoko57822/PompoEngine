using System.Reflection;

namespace Pompo.Scripting;

public sealed class CustomNodeDiscoveryService
{
    private static readonly StringComparer TypeNameComparer = StringComparer.Ordinal;

    public IReadOnlyList<PompoCustomNodeDescriptor> Discover(byte[] assemblyBytes)
    {
        ArgumentNullException.ThrowIfNull(assemblyBytes);

        using var stream = new MemoryStream(assemblyBytes);
        var assembly = Assembly.Load(stream.ToArray());
        return Discover(assembly);
    }

    public IReadOnlyList<PompoCustomNodeDescriptor> Discover(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly
            .GetTypes()
            .Where(IsCustomNodeType)
            .OrderBy(type => type.FullName ?? type.Name, TypeNameComparer)
            .Select(type => new PompoCustomNodeDescriptor(
                type.FullName ?? type.Name,
                type.Name,
                type,
                DiscoverInputs(type)))
            .ToArray();
    }

    public IReadOnlyList<PompoCustomNodeProviderDescriptor> DiscoverProviders(byte[] assemblyBytes)
    {
        ArgumentNullException.ThrowIfNull(assemblyBytes);

        using var stream = new MemoryStream(assemblyBytes);
        var assembly = Assembly.Load(stream.ToArray());
        return DiscoverProviders(assembly);
    }

    public IReadOnlyList<PompoCustomNodeProviderDescriptor> DiscoverProviders(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly
            .GetTypes()
            .Where(IsCustomNodeProviderType)
            .OrderBy(type => type.FullName ?? type.Name, TypeNameComparer)
            .Select(type => new PompoCustomNodeProviderDescriptor(type.FullName ?? type.Name, type))
            .ToArray();
    }

    private static bool IsCustomNodeType(Type type)
    {
        return type is { IsAbstract: false, IsClass: true } &&
            (typeof(PompoCommandNode).IsAssignableFrom(type) ||
                typeof(PompoConditionNode).IsAssignableFrom(type));
    }

    private static bool IsCustomNodeProviderType(Type type)
    {
        return type is { IsAbstract: false, IsClass: true } &&
            typeof(ICustomNodeProvider).IsAssignableFrom(type);
    }

    private static IReadOnlyList<PompoNodeInputDescriptor> DiscoverInputs(Type type)
    {
        return type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => new
            {
                Property = property,
                Attribute = property.GetCustomAttribute<PompoNodeInputAttribute>()
            })
            .Where(item => item.Attribute is not null)
            .OrderBy(item => item.Property.Name, StringComparer.Ordinal)
            .Select(item => new PompoNodeInputDescriptor(
                item.Property.Name,
                item.Property.PropertyType,
                item.Attribute!.DefaultValue,
                item.Attribute.DisplayName))
            .ToArray();
    }
}
