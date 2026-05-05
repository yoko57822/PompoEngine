using Pompo.Core.Assets;

namespace Pompo.Scripting;

public interface IPompoRuntimeModule
{
    void Register(PompoRuntimeContext context);
}

public interface ICustomNodeProvider
{
    IEnumerable<PompoCustomNodeDescriptor> GetNodes();
}

public abstract class PompoCommandNode
{
    public abstract ValueTask ExecuteAsync(PompoRuntimeContext context, CancellationToken cancellationToken);
}

public abstract class PompoConditionNode
{
    public abstract ValueTask<bool> EvaluateAsync(PompoRuntimeContext context, CancellationToken cancellationToken);
}

public sealed class PompoRuntimeContext
{
    private readonly IDictionary<string, object?> _variables;

    public PompoRuntimeContext()
        : this(new Dictionary<string, object?>(StringComparer.Ordinal))
    {
    }

    public PompoRuntimeContext(IDictionary<string, object?> variables)
    {
        _variables = variables;
    }

    public IReadOnlyDictionary<string, object?> Variables =>
        _variables as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>(_variables, StringComparer.Ordinal);

    public void SetVariable(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _variables[name] = value;
    }

    public T? GetVariable<T>(string name)
    {
        return _variables.TryGetValue(name, out var value) && value is T typed ? typed : default;
    }
}

public sealed record PompoCustomNodeDescriptor(
    string NodeId,
    string DisplayName,
    Type NodeType,
    IReadOnlyList<PompoNodeInputDescriptor> Inputs);

public sealed record PompoCustomNodeProviderDescriptor(string ProviderId, Type ProviderType);

public sealed record PompoRuntimeModuleDescriptor(string ModuleId, Type ModuleType);

public sealed record PompoNodeInputDescriptor(
    string Name,
    Type ValueType,
    object? DefaultValue = null,
    string? DisplayName = null);

public sealed record PompoAssetRef<TAsset>(string AssetId, PompoAssetType Type);

[AttributeUsage(AttributeTargets.Property)]
public sealed class PompoNodeInputAttribute(string displayName) : Attribute
{
    public string DisplayName { get; } = displayName;
    public object? DefaultValue { get; init; }
}
