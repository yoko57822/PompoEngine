using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pompo.VisualScripting.Runtime;

namespace Pompo.Scripting;

public sealed class UserScriptRuntimeNodeHandler : IRuntimeCustomNodeHandler
{
    private readonly Assembly _assembly;
    private readonly Dictionary<string, Type> _nodeTypes;
    private readonly Type[] _moduleTypes;

    public UserScriptRuntimeNodeHandler(byte[] assemblyBytes)
        : this(Assembly.Load(assemblyBytes))
    {
    }

    public UserScriptRuntimeNodeHandler(string assemblyPath)
        : this(Assembly.LoadFrom(assemblyPath))
    {
    }

    public UserScriptRuntimeNodeHandler(Assembly assembly)
    {
        _assembly = assembly;
        _nodeTypes = _assembly
            .GetTypes()
            .Where(type => IsConcrete(type) &&
                (typeof(PompoCommandNode).IsAssignableFrom(type) ||
                    typeof(PompoConditionNode).IsAssignableFrom(type)))
            .SelectMany(CreateNodeTypeKeys)
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Type, StringComparer.Ordinal);
        _moduleTypes = _assembly
            .GetTypes()
            .Where(type => IsConcrete(type) && typeof(IPompoRuntimeModule).IsAssignableFrom(type))
            .ToArray();
    }

    public ValueTask<RuntimeCustomNodeResult> ExecuteAsync(
        RuntimeCustomNodeContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nodeTypeName = ReadString(context.Arguments, "nodeType") ??
            ReadString(context.Arguments, "customNodeType") ??
            ReadString(context.Arguments, "type");
        if (string.IsNullOrWhiteSpace(nodeTypeName))
        {
            throw new InvalidOperationException(
                $"Custom node '{context.SourceNodeId}' is missing a nodeType property.");
        }

        if (!_nodeTypes.TryGetValue(nodeTypeName, out var nodeType))
        {
            throw new InvalidOperationException(
                $"Custom node type '{nodeTypeName}' was not found in user script assembly.");
        }

        var runtimeContext = new PompoRuntimeContext(context.Variables);
        RegisterModules(runtimeContext);
        var instance = Activator.CreateInstance(nodeType)
            ?? throw new InvalidOperationException($"Custom node type '{nodeType.FullName}' could not be created.");
        ApplyInputs(instance, context.Arguments);

        return instance switch
        {
            PompoCommandNode command => ExecuteCommandAsync(command, runtimeContext, cancellationToken),
            PompoConditionNode condition => ExecuteConditionAsync(condition, runtimeContext, cancellationToken),
            _ => throw new InvalidOperationException($"Custom node type '{nodeType.FullName}' is not executable.")
        };
    }

    private static async ValueTask<RuntimeCustomNodeResult> ExecuteCommandAsync(
        PompoCommandNode command,
        PompoRuntimeContext context,
        CancellationToken cancellationToken)
    {
        await command.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        return new RuntimeCustomNodeResult("out");
    }

    private static async ValueTask<RuntimeCustomNodeResult> ExecuteConditionAsync(
        PompoConditionNode condition,
        PompoRuntimeContext context,
        CancellationToken cancellationToken)
    {
        var result = await condition.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        return new RuntimeCustomNodeResult(result ? "true" : "false");
    }

    private void RegisterModules(PompoRuntimeContext context)
    {
        foreach (var moduleType in _moduleTypes)
        {
            if (Activator.CreateInstance(moduleType) is IPompoRuntimeModule module)
            {
                module.Register(context);
            }
        }
    }

    private static void ApplyInputs(object instance, IReadOnlyDictionary<string, JsonNode?> arguments)
    {
        var properties = instance.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.SetMethod is not null)
            .ToArray();
        foreach (var property in properties)
        {
            if (!TryGetArgument(arguments, property.Name, out var node) || node is null)
            {
                continue;
            }

            property.SetValue(instance, ConvertJsonNode(node, property.PropertyType));
        }
    }

    private static bool TryGetArgument(
        IReadOnlyDictionary<string, JsonNode?> arguments,
        string propertyName,
        out JsonNode? node)
    {
        if (arguments.TryGetValue(propertyName, out node))
        {
            return true;
        }

        var camelName = JsonNamingPolicy.CamelCase.ConvertName(propertyName);
        return arguments.TryGetValue(camelName, out node);
    }

    private static object? ConvertJsonNode(JsonNode node, Type targetType)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        var effectiveType = nullableType ?? targetType;
        if (effectiveType.IsEnum)
        {
            return Enum.Parse(effectiveType, node.GetValue<string>(), ignoreCase: true);
        }

        return node.Deserialize(targetType, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static string? ReadString(IReadOnlyDictionary<string, JsonNode?> arguments, string name)
    {
        return arguments.TryGetValue(name, out var node) ? node?.GetValue<string>() : null;
    }

    private static bool IsConcrete(Type type)
    {
        return type is { IsAbstract: false, IsInterface: false } &&
            type.GetConstructor(Type.EmptyTypes) is not null;
    }

    private static IEnumerable<(string Key, Type Type)> CreateNodeTypeKeys(Type type)
    {
        if (!string.IsNullOrWhiteSpace(type.FullName))
        {
            yield return (type.FullName, type);
        }

        yield return (type.Name, type);
    }
}
