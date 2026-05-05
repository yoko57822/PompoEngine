using Pompo.Scripting;
using Pompo.VisualScripting.Runtime;
using System.Text.Json.Nodes;

namespace Pompo.Tests;

public sealed class ScriptingTests
{
    [Fact]
    public void Compile_BlocksFileSystemByDefault()
    {
        var result = new UserScriptCompiler().Compile(
            new Dictionary<string, string>
            {
                ["Scripts/Bad.cs"] = "using System.IO; public sealed class Bad { }"
            },
            new ScriptSecurityOptions());

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("System.IO", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_BlocksAliasedFileSystemAccessByDefault()
    {
        var result = new UserScriptCompiler().Compile(
            new Dictionary<string, string>
            {
                ["Scripts/AliasBypass.cs"] = """
                    using S = System;

                    public sealed class AliasBypass
                    {
                        public string Read()
                        {
                            return S.IO.File.ReadAllText("secret.txt");
                        }
                    }
                    """
            },
            new ScriptSecurityOptions());

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("System.IO", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_AllowsFileSystemWhenPermissionIsEnabled()
    {
        var result = new UserScriptCompiler().Compile(
            new Dictionary<string, string>
            {
                ["Scripts/FileAllowed.cs"] = """
                    using System.IO;

                    public sealed class FileAllowed
                    {
                        public string Read()
                        {
                            return File.ReadAllText("allowed.txt");
                        }
                    }
                    """
            },
            new ScriptSecurityOptions(AllowFileSystem: true));

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.NotNull(result.AssemblyBytes);
    }

    [Fact]
    public void Compile_BlocksReflectionNamespaceEvenWhenPermissionsAreEnabled()
    {
        var result = new UserScriptCompiler().Compile(
            new Dictionary<string, string>
            {
                ["Scripts/Reflect.cs"] = """
                    using System.Reflection;

                    public sealed class Reflect
                    {
                        public Assembly? GetAssembly()
                        {
                            return typeof(Reflect).Assembly;
                        }
                    }
                    """
            },
            new ScriptSecurityOptions(
                AllowFileSystem: true,
                AllowNetwork: true,
                AllowProcessExecution: true));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("System.Reflection", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_BlocksTypeGetTypeReflectionBypass()
    {
        var result = new UserScriptCompiler().Compile(
            new Dictionary<string, string>
            {
                ["Scripts/TypeBypass.cs"] = """
                    using System;

                    public sealed class TypeBypass
                    {
                        public Type? Resolve()
                        {
                            return Type.GetType("System.IO.File");
                        }
                    }
                    """
            },
            new ScriptSecurityOptions());

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("System.Type.GetType", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_BlocksActivatorReflectionBypass()
    {
        var result = new UserScriptCompiler().Compile(
            new Dictionary<string, string>
            {
                ["Scripts/ActivatorBypass.cs"] = """
                    using System;

                    public sealed class ActivatorBypass
                    {
                        public object? Create(Type type)
                        {
                            return Activator.CreateInstance(type);
                        }
                    }
                    """
            },
            new ScriptSecurityOptions());

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("System.Activator.CreateInstance", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_AllowsBasicCustomNodeCode()
    {
        var result = new UserScriptCompiler().Compile(
            new Dictionary<string, string>
            {
                ["Scripts/Hello.cs"] = """
                    using System.Threading;
                    using System.Threading.Tasks;
                    using Pompo.Scripting;

                    public sealed class HelloNode : PompoConditionNode
                    {
                        public override ValueTask<bool> EvaluateAsync(PompoRuntimeContext context, CancellationToken cancellationToken)
                        {
                            return ValueTask.FromResult(true);
                        }
                    }
                    """
            },
            new ScriptSecurityOptions());

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.NotNull(result.AssemblyBytes);
    }

    [Fact]
    public void Discover_FindsCustomNodesAndInputMetadata()
    {
        var result = new UserScriptCompiler().Compile(
            new Dictionary<string, string>
            {
                ["Scripts/CustomNodes.cs"] = """
                    using System.Threading;
                    using System.Threading.Tasks;
                    using Pompo.Scripting;

                    public sealed class ReputationGateNode : PompoConditionNode
                    {
                        [PompoNodeInput("Minimum reputation", DefaultValue = 10)]
                        public int Minimum { get; init; }

                        public override ValueTask<bool> EvaluateAsync(PompoRuntimeContext context, CancellationToken cancellationToken)
                        {
                            return ValueTask.FromResult(true);
                        }
                    }

                    public sealed class AwardCgNode : PompoCommandNode
                    {
                        [PompoNodeInput("CG ID")]
                        public string CgId { get; init; } = "";

                        public override ValueTask ExecuteAsync(PompoRuntimeContext context, CancellationToken cancellationToken)
                        {
                            return ValueTask.CompletedTask;
                        }
                    }
                    """
            },
            new ScriptSecurityOptions());

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var nodes = new CustomNodeDiscoveryService().Discover(result.AssemblyBytes!);

        Assert.Contains(nodes, node => node.DisplayName == "AwardCgNode");
        var reputationGate = Assert.Single(nodes, node => node.DisplayName == "ReputationGateNode");
        var input = Assert.Single(reputationGate.Inputs);
        Assert.Equal("Minimum", input.Name);
        Assert.Equal(typeof(int), input.ValueType);
        Assert.Equal(10, input.DefaultValue);
        Assert.Equal("Minimum reputation", input.DisplayName);
    }

    [Fact]
    public void Discover_FindsCustomNodeProvidersAndRuntimeModulesWithoutExecutingThem()
    {
        var result = new UserScriptCompiler().Compile(
            new Dictionary<string, string>
            {
                ["Scripts/ExtensionPoints.cs"] = """
                    using System;
                    using System.Collections.Generic;
                    using Pompo.Scripting;

                    public sealed class ProviderNode : PompoConditionNode
                    {
                        public override System.Threading.Tasks.ValueTask<bool> EvaluateAsync(PompoRuntimeContext context, System.Threading.CancellationToken cancellationToken)
                        {
                            return System.Threading.Tasks.ValueTask.FromResult(true);
                        }
                    }

                    public sealed class TestProvider : ICustomNodeProvider
                    {
                        public static int ConstructorCalls;

                        public TestProvider()
                        {
                            ConstructorCalls++;
                            throw new InvalidOperationException("Discovery must not instantiate providers.");
                        }

                        public IEnumerable<PompoCustomNodeDescriptor> GetNodes()
                        {
                            yield return new PompoCustomNodeDescriptor("provider-node", "Provider Node", typeof(ProviderNode), Array.Empty<PompoNodeInputDescriptor>());
                        }
                    }

                    public sealed class TestRuntimeModule : IPompoRuntimeModule
                    {
                        public static int ConstructorCalls;

                        public TestRuntimeModule()
                        {
                            ConstructorCalls++;
                            throw new InvalidOperationException("Discovery must not instantiate modules.");
                        }

                        public void Register(PompoRuntimeContext context)
                        {
                        }
                    }
                    """
            },
            new ScriptSecurityOptions());

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));

        var providers = new CustomNodeDiscoveryService().DiscoverProviders(result.AssemblyBytes!);
        var modules = new RuntimeModuleDiscoveryService().Discover(result.AssemblyBytes!);

        var provider = Assert.Single(providers);
        var module = Assert.Single(modules);
        Assert.Equal("TestProvider", provider.ProviderType.Name);
        Assert.Equal("TestRuntimeModule", module.ModuleType.Name);
        Assert.Equal(0, provider.ProviderType.GetField("ConstructorCalls")!.GetValue(null));
        Assert.Equal(0, module.ModuleType.GetField("ConstructorCalls")!.GetValue(null));
    }

    [Fact]
    public async Task RuntimeNodeHandler_ExecutesCommandAndConditionNodes()
    {
        var result = new UserScriptCompiler().Compile(
            new Dictionary<string, string>
            {
                ["Scripts/RuntimeNodes.cs"] = """
                    using System.Threading;
                    using System.Threading.Tasks;
                    using Pompo.Scripting;

                    public sealed class SetFlagNode : PompoCommandNode
                    {
                        public string Name { get; init; } = "";
                        public string Value { get; init; } = "";

                        public override ValueTask ExecuteAsync(PompoRuntimeContext context, CancellationToken cancellationToken)
                        {
                            context.SetVariable(Name, Value);
                            return ValueTask.CompletedTask;
                        }
                    }

                    public sealed class FlagGateNode : PompoConditionNode
                    {
                        public string Name { get; init; } = "";
                        public string Expected { get; init; } = "";

                        public override ValueTask<bool> EvaluateAsync(PompoRuntimeContext context, CancellationToken cancellationToken)
                        {
                            return ValueTask.FromResult(context.GetVariable<string>(Name) == Expected);
                        }
                    }
                    """
            },
            new ScriptSecurityOptions());
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var handler = new UserScriptRuntimeNodeHandler(result.AssemblyBytes!);
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal);

        var commandResult = await handler.ExecuteAsync(new RuntimeCustomNodeContext(
            "graph",
            "set",
            new Dictionary<string, JsonNode?>
            {
                ["nodeType"] = "SetFlagNode",
                ["name"] = "route",
                ["value"] = "true"
            },
            variables));
        var conditionResult = await handler.ExecuteAsync(new RuntimeCustomNodeContext(
            "graph",
            "gate",
            new Dictionary<string, JsonNode?>
            {
                ["nodeType"] = "FlagGateNode",
                ["name"] = "route",
                ["expected"] = "true"
            },
            variables));

        Assert.Equal("out", commandResult.OutputPort);
        Assert.Equal("true", variables["route"]);
        Assert.Equal("true", conditionResult.OutputPort);
    }
}
