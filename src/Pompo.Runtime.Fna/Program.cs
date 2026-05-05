namespace Pompo.Runtime.Fna;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Pompo.Core;
using Pompo.Core.Localization;
using Pompo.Core.Project;
using Pompo.Core.Runtime;
using Pompo.Runtime.Fna.Presentation;
using Pompo.Scripting;
using Pompo.VisualScripting;
using Pompo.VisualScripting.Runtime;

internal static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            Run(args);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static void Run(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal) ||
            args.Contains("-h", StringComparer.Ordinal) ||
            args.Contains("help", StringComparer.Ordinal))
        {
            PrintHelp();
            return;
        }

        if (args.Contains("--version", StringComparer.Ordinal) ||
            args.Contains("-v", StringComparer.Ordinal) ||
            args.Contains("version", StringComparer.Ordinal))
        {
            PrintVersion(args.Contains("--json", StringComparer.Ordinal));
            return;
        }

        if (args.Contains("--validate-runtime", StringComparer.Ordinal))
        {
            Console.WriteLine("PompoEngine FNA runtime is installed.");
            return;
        }

        var playIrIndex = Array.IndexOf(args, "--play-ir");
        if (playIrIndex >= 0)
        {
            PlayIr(args, playIrIndex);
            return;
        }

        var runIrIndex = Array.IndexOf(args, "--run-ir");
        if (runIrIndex >= 0)
        {
            var launchState = CreateLaunchStateFromIr(args, runIrIndex);
            using var irGame = new FnaRuntimeGame(
                launchState.Frame,
                LoadAssetCatalog(launchState.IrPath),
                launchState.Ir,
                launchState.GraphLibrary,
                launchState.Localizer,
                launchState.SaveRoot,
                launchState.CustomNodeHandler,
                launchState.RuntimeUiTheme,
                launchState.RuntimeUiSkin,
                launchState.RuntimeUiLayout,
                launchState.RuntimeUiAnimation,
                launchState.RuntimePlayback);
            irGame.Run();
            return;
        }

        using var game = new FnaRuntimeGame();
        game.Run();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("PompoEngine FNA Runtime");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  Pompo.Runtime.Fna --validate-runtime");
        Console.WriteLine("  Pompo.Runtime.Fna --play-ir <path> [--locale <locale>] [--choices <indexes>] [--json-trace]");
        Console.WriteLine("  Pompo.Runtime.Fna --run-ir <path> [--locale <locale>] [--choices <indexes>] [--saves <path>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --validate-runtime  Check that the runtime executable starts.");
        Console.WriteLine("  --version           Print runtime version and environment information.");
        Console.WriteLine("  --play-ir <path>    Run compiled IR headlessly for smoke tests.");
        Console.WriteLine("  --run-ir <path>     Run compiled IR interactively in the FNA window.");
        Console.WriteLine("  --locale <locale>   Resolve localized text from packaged project data.");
        Console.WriteLine("  --choices <indexes> Comma-separated zero-based choice selections.");
        Console.WriteLine("  --json-trace        Emit headless trace output as JSON.");
        Console.WriteLine("  --saves <path>      Enable runtime save/load slots.");
    }

    private static void PrintVersion(bool json)
    {
        var assembly = typeof(Program).Assembly;
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(
                new
                {
                    product = "PompoEngine Runtime",
                    runtimeVersion = version,
                    schemaVersion = ProjectConstants.CurrentSchemaVersion,
                    framework = RuntimeInformation.FrameworkDescription,
                    os = RuntimeInformation.OSDescription,
                    architecture = RuntimeInformation.ProcessArchitecture.ToString()
                },
                ProjectFileService.CreateJsonOptions()));
            return;
        }

        Console.WriteLine($"PompoEngine FNA Runtime {version}");
        Console.WriteLine($"schema: {ProjectConstants.CurrentSchemaVersion}");
        Console.WriteLine($"framework: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"os: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"arch: {RuntimeInformation.ProcessArchitecture}");
    }

    private static void PlayIr(string[] args, int playIrIndex)
    {
        if (playIrIndex + 1 >= args.Length)
        {
            throw new ArgumentException("--play-ir requires a path to a .pompo-ir.json file.");
        }

        var irPath = args[playIrIndex + 1];
        using var stream = File.OpenRead(irPath);
        var ir = JsonSerializer.Deserialize<PompoGraphIR>(stream, ProjectFileService.CreateJsonOptions())
            ?? throw new InvalidDataException("IR file is empty or invalid.");
        var choices = ReadChoiceSelections(args);
        var result = new RuntimeTraceRunner().Run(
            ir,
            choices,
            localizer: LoadLocalizer(args, irPath),
            graphLibrary: LoadGraphLibrary(irPath, ir),
            customNodeHandler: LoadCustomNodeHandler(irPath));

        if (args.Contains("--json-trace", StringComparer.Ordinal))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, ProjectFileService.CreateJsonOptions()));
            return;
        }

        foreach (var traceEvent in result.Events)
        {
            switch (traceEvent.Kind)
            {
                case "line":
                    Console.WriteLine(traceEvent.Speaker is null ? traceEvent.Text : $"{traceEvent.Speaker}: {traceEvent.Text}");
                    break;
                case "choice":
                    Console.WriteLine($"Choice: {traceEvent.SelectedChoice}");
                    break;
            }
        }
    }

    private static IReadOnlyList<int> ReadChoiceSelections(string[] args)
    {
        var index = Array.IndexOf(args, "--choices");
        if (index < 0)
        {
            return [];
        }

        if (index + 1 >= args.Length)
        {
            throw new ArgumentException("--choices requires comma-separated zero-based indexes.");
        }

        var rawSelections = args[index + 1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (rawSelections.Length == 0)
        {
            throw new ArgumentException("--choices requires at least one zero-based index.");
        }

        var selections = new List<int>(rawSelections.Length);
        foreach (var rawSelection in rawSelections)
        {
            if (!int.TryParse(rawSelection, out var selection) || selection < 0)
            {
                throw new ArgumentException(
                    $"--choices value '{rawSelection}' is invalid. Use comma-separated zero-based non-negative indexes.");
            }

            selections.Add(selection);
        }

        return selections;
    }

    private static RuntimeLaunchState CreateLaunchStateFromIr(string[] args, int runIrIndex)
    {
        if (runIrIndex + 1 >= args.Length)
        {
            throw new ArgumentException("--run-ir requires a path to a .pompo-ir.json file.");
        }

        var irPath = args[runIrIndex + 1];
        using var stream = File.OpenRead(irPath);
        var ir = JsonSerializer.Deserialize<PompoGraphIR>(stream, ProjectFileService.CreateJsonOptions())
            ?? throw new InvalidDataException("IR file is empty or invalid.");
        var localizer = LoadLocalizer(args, irPath);
        var project = LoadProjectDocumentFromIrPath(irPath);
        var graphLibrary = LoadGraphLibrary(irPath, ir);
        var customNodeHandler = LoadCustomNodeHandler(irPath);
        var snapshot = new GraphRuntimeInterpreter(ir, localizer, graphLibrary, customNodeHandler).Snapshot;
        var saveRoot = ReadSaveRoot(args);
        return new RuntimeLaunchState(
            irPath,
            ir,
            graphLibrary,
            new RuntimeUiLayout(project?.RuntimeUiLayout).CreateFrame(snapshot, ReadSaveSlots(saveRoot)),
            localizer,
            saveRoot,
            customNodeHandler,
            project?.RuntimeUiTheme,
            project?.RuntimeUiSkin,
            project?.RuntimeUiLayout,
            project?.RuntimeUiAnimation,
            project?.RuntimePlayback);
    }

    private static string? ReadSaveRoot(string[] args)
    {
        return ReadOption(args, "--saves");
    }

    private static string? ReadOption(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        if (index < 0)
        {
            return null;
        }

        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        return args[index + 1];
    }

    private static IReadOnlyList<RuntimeSaveSlotMetadata>? ReadSaveSlots(string? saveRoot)
    {
        if (saveRoot is null)
        {
            return null;
        }

        return new RuntimeSaveStore()
            .ListAsync(saveRoot)
            .GetAwaiter()
            .GetResult();
    }

    private static RuntimeAssetCatalog? LoadAssetCatalog(string irPath)
    {
        return new RuntimeAssetCatalogLoader()
            .TryLoadFromIrPathAsync(irPath)
            .GetAwaiter()
            .GetResult();
    }

    private static IReadOnlyDictionary<string, PompoGraphIR> LoadGraphLibrary(string irPath, PompoGraphIR rootIr)
    {
        var library = new Dictionary<string, PompoGraphIR>(StringComparer.Ordinal)
        {
            [rootIr.GraphId] = rootIr
        };
        var directory = Path.GetDirectoryName(Path.GetFullPath(irPath));
        if (directory is null)
        {
            return library;
        }

        foreach (var siblingPath in Directory.EnumerateFiles(directory, "*.pompo-ir.json"))
        {
            if (string.Equals(Path.GetFullPath(siblingPath), Path.GetFullPath(irPath), StringComparison.Ordinal))
            {
                continue;
            }

            using var siblingStream = File.OpenRead(siblingPath);
            var sibling = JsonSerializer.Deserialize<PompoGraphIR>(siblingStream, ProjectFileService.CreateJsonOptions());
            if (sibling is not null)
            {
                library[sibling.GraphId] = sibling;
            }
        }

        return library;
    }

    private static StringTableLocalizer? LoadLocalizer(string[] args, string irPath)
    {
        var locale = ReadOption(args, "--locale");
        if (string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        var project = LoadProjectDocumentFromIrPath(irPath);
        if (project is null)
        {
            return null;
        }

        var fallbackLocale = project.SupportedLocales.FirstOrDefault(
            supported => !string.Equals(supported, locale, StringComparison.Ordinal));
        return new StringTableLocalizer(project.StringTables, locale, fallbackLocale);
    }

    private static PompoProjectDocument? LoadProjectDocumentFromIrPath(string irPath)
    {
        var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(irPath));
        if (dataDirectory is null)
        {
            return null;
        }

        var projectPath = Path.Combine(dataDirectory, ProjectConstants.ProjectFileName);
        if (!File.Exists(projectPath))
        {
            return null;
        }

        using var stream = File.OpenRead(projectPath);
        return JsonSerializer.Deserialize<PompoProjectDocument>(
            stream,
            ProjectFileService.CreateJsonOptions());
    }

    private static IRuntimeCustomNodeHandler? LoadCustomNodeHandler(string irPath)
    {
        foreach (var candidate in GetUserScriptAssemblyCandidates(irPath).Distinct(StringComparer.Ordinal))
        {
            if (File.Exists(candidate))
            {
                return new UserScriptRuntimeNodeHandler(candidate);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetUserScriptAssemblyCandidates(string irPath)
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Pompo.UserScripts.dll");

        var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(irPath));
        if (dataDirectory is null)
        {
            yield break;
        }

        var outputDirectory = Directory.GetParent(dataDirectory)?.FullName;
        if (outputDirectory is null)
        {
            yield break;
        }

        yield return Path.Combine(outputDirectory, "Runtime", "Pompo.UserScripts.dll");
        yield return Path.Combine(outputDirectory, "Scripts", "Pompo.UserScripts.dll");
    }

    private sealed record RuntimeLaunchState(
        string IrPath,
        PompoGraphIR Ir,
        IReadOnlyDictionary<string, PompoGraphIR> GraphLibrary,
        RuntimeUiFrame Frame,
        StringTableLocalizer? Localizer,
        string? SaveRoot,
        IRuntimeCustomNodeHandler? CustomNodeHandler,
        PompoRuntimeUiTheme? RuntimeUiTheme,
        PompoRuntimeUiSkin? RuntimeUiSkin,
        PompoRuntimeUiLayoutSettings? RuntimeUiLayout,
        PompoRuntimeUiAnimationSettings? RuntimeUiAnimation,
        PompoRuntimePlaybackSettings? RuntimePlayback);
}
