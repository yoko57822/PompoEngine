using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Pompo.Build;
using Pompo.Core.Assets;
using Pompo.Core.Graphs;
using Pompo.Core.Runtime;
using Pompo.Editor.Avalonia.Controls;
using Pompo.Editor.Avalonia.ViewModels;

namespace Pompo.Editor.Avalonia;

public sealed class MainWindow : Window
{
    private static readonly IBrush AppBackground = Brush.Parse("#f4f5f7");
    private static readonly IBrush PanelBackground = Brushes.White;
    private static readonly IBrush BorderColor = Brush.Parse("#d9dee7");
    private static readonly IBrush MutedText = Brush.Parse("#526071");
    private static readonly IBrush Accent = Brush.Parse("#2563eb");
    private readonly ProjectWorkspaceViewModel _viewModel;
    private Window? _detachedPreviewWindow;
    private Window? _detachedConsoleWindow;
    private Window? _detachedInspectorWindow;
    private Window? _detachedResourceWindow;
    private Window? _detachedSceneWindow;
    private Window? _detachedGraphWindow;

    public MainWindow()
        : this(new ProjectWorkspaceViewModel())
    {
    }

    public MainWindow(ProjectWorkspaceViewModel viewModel)
    {
        _viewModel = viewModel;
        Title = "PompoEngine";
        Width = 1440;
        Height = 900;
        MinWidth = 1100;
        MinHeight = 720;
        DataContext = viewModel;
        Content = CreateLayout();
        Opened += async (_, _) =>
        {
            await RunEditorActionAsync(() => _viewModel.LoadWorkspacePreferencesAsync());
            await RunEditorActionAsync(() => _viewModel.RefreshRecentProjectsAsync());
        };
    }

    private Control CreateLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = AppBackground
        };

        var chrome = CreateTopBar();
        Grid.SetRow(chrome, 0);
        root.Children.Add(chrome);

        var tabs = new TabControl
        {
            Margin = new global::Avalonia.Thickness(12, 0, 12, 12),
            ItemsSource = new[]
            {
                new TabItem { Header = "Dashboard", Content = CreateDashboard() },
                new TabItem { Header = "Workspace", Content = CreateWorkspace() },
                new TabItem { Header = "Localization", Content = CreateLocalizationPanel() },
                new TabItem { Header = "Theme", Content = CreateThemePanel() },
                new TabItem { Header = "Saves", Content = CreateSavesPanel() },
                new TabItem { Header = "Build", Content = CreateBuildPanel() },
                new TabItem { Header = "Help", Content = CreateHelpPanel() }
            }
        };
        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);

        return root;
    }

    private Control CreateTopBar()
    {
        var bar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new global::Avalonia.Thickness(12),
            MinHeight = 48
        };

        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "PompoEngine",
                    FontSize = 22,
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                },
                StatusPill("PC Visual Novel Engine")
            }
        };
        Grid.SetColumn(title, 0);
        bar.Children.Add(title);

        var newProject = CommandButton("New Sample");
        newProject.Click += async (_, _) => await CreateProjectFromFolderAsync(sampleTemplate: true);
        var open = CommandButton("Open");
        open.Click += async (_, _) => await OpenProjectFromFolderAsync();
        var build = PrimaryButton("Build");
        build.Click += async (_, _) => await BuildProjectToFolderAsync();

        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                newProject,
                open,
                build
            }
        };
        Grid.SetColumn(commands, 1);
        bar.Children.Add(commands);
        return bar;
    }

    private Control CreateDashboard()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.1*,1*"),
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new global::Avalonia.Thickness(0, 10, 0, 0)
        };

        var createMinimal = CommandButton("New Minimal");
        createMinimal.Click += async (_, _) => await CreateProjectFromFolderAsync(sampleTemplate: false);
        var createSample = PrimaryButton("New Sample");
        createSample.Click += async (_, _) => await CreateProjectFromFolderAsync(sampleTemplate: true);
        var openProject = CommandButton("Open Project");
        openProject.Click += async (_, _) => await OpenProjectFromFolderAsync();
        var validate = CommandButton("Validate");
        validate.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.ValidateCurrentAsync());
        var runDoctor = CommandButton("Run Doctor");
        runDoctor.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.RunDoctorAsync());

        var hero = Card("Project Dashboard", new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "Create, open, validate, and package visual novel projects from one place.",
                    Foreground = MutedText,
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        createMinimal,
                        createSample,
                        openProject,
                        validate
                    }
                }
            }
        });
        Grid.SetColumn(hero, 0);
        Grid.SetColumnSpan(hero, 2);
        Grid.SetRow(hero, 0);
        grid.Children.Add(hero);

        var recentProjects = new ListBox
        {
            MinHeight = 180,
            ItemTemplate = new FuncDataTemplate<RecentProjectViewItem>((project, _) => RecentProjectRow(project))
        };
        recentProjects.SelectionChanged += (_, _) =>
        {
            if (recentProjects.SelectedItem is RecentProjectViewItem project)
            {
                _viewModel.SelectedRecentProjectRoot = project.ProjectRoot;
            }
        };
        recentProjects.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("RecentProjects")
            {
                FallbackValue = Array.Empty<RecentProjectViewItem>()
            });

        var openRecent = PrimaryButton("Open Recent");
        openRecent.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.OpenSelectedRecentProjectAsync());
        var forgetRecent = CommandButton("Forget");
        forgetRecent.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.ForgetSelectedRecentProjectAsync());
        var refreshRecent = CommandButton("Refresh");
        refreshRecent.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.RefreshRecentProjectsAsync());

        var recent = Card("Recent Projects", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                recentProjects,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        openRecent,
                        forgetRecent,
                        refreshRecent
                    }
                },
                BoundText("SelectedRecentProject.ProjectRoot", "No recent project selected"),
                new Border
                {
                    BorderBrush = BorderColor,
                    BorderThickness = new global::Avalonia.Thickness(1),
                    Padding = new global::Avalonia.Thickness(10),
                    Margin = new global::Avalonia.Thickness(0, 8, 0, 0),
                    Child = new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            BoundText("Summary.ProjectName", "No project loaded", FontWeight.Bold),
                            BoundText("Summary.ProjectRoot", "Create or open a Pompo project."),
                            MetricRow("Scenes", "Summary.SceneCount"),
                            MetricRow("Characters", "Summary.CharacterCount"),
                            MetricRow("Graphs", "Summary.GraphCount"),
                            MetricRow("Assets", "Summary.AssetCount")
                        }
                    }
                }
            }
        });
        Grid.SetColumn(recent, 0);
        Grid.SetRow(recent, 1);
        grid.Children.Add(recent);

        var checks = Card("Production Checks", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                runDoctor,
                ReadinessList(),
                BoundText("StatusMessage", "No project loaded"),
                DiagnosticList("DoctorDiagnostics")
            }
        });
        Grid.SetColumn(checks, 1);
        Grid.SetRow(checks, 1);
        grid.Children.Add(checks);

        return grid;
    }

    private Control CreateHelpPanel()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1*,1*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            Margin = new global::Avalonia.Thickness(0, 10, 0, 0)
        };

        var workflow = Card("Workflow", ListPanel(
            "1. Dashboard: create or open a project.",
            "2. Workspace: author resources, scenes, graphs, preview, and diagnostics.",
            "3. Theme: edit runtime UI theme, skin, layout, and animation.",
            "4. Build: create standalone Windows, macOS, or Linux output.",
            "5. Release: package, verify, audit, and sign build output before publishing."));
        Grid.SetColumn(workflow, 0);
        Grid.SetRow(workflow, 0);
        grid.Children.Add(workflow);

        var docs = Card("Docs", ListPanel(
            "docs/RUN_AND_USE.md - editor, CLI, runtime, build, and release usage.",
            "docs/TROUBLESHOOTING.md - failure diagnosis flow.",
            "docs/ARCHITECTURE.md - module boundaries and data flow.",
            "docs/SCRIPTING.md - C# custom nodes and runtime modules.",
            "docs/COMPATIBILITY.md - schema, API, CLI, and release compatibility.",
            "docs/RELEASING.md - release packaging and signing.",
            "docs/OPEN_SOURCE_RELEASE_CHECKLIST.md - public release gates."));
        Grid.SetColumn(docs, 1);
        Grid.SetRow(docs, 0);
        grid.Children.Add(docs);

        var commands = Card("Repository Gates", ListPanel(
            "dotnet build PompoEngine.slnx --no-restore",
            "dotnet test PompoEngine.slnx --no-build",
            "dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .",
            "dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json"));
        Grid.SetColumn(commands, 0);
        Grid.SetRow(commands, 1);
        grid.Children.Add(commands);

        var projectCommands = Card("Project Gates", ListPanel(
            "dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --project <projectRoot>",
            "dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- validate --project <projectRoot> --json",
            "dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build verify --build <buildOutput> --require-smoke-tested-locales --require-self-contained --json",
            "dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release audit --root . --manifest <releaseManifestJson> --require-smoke-tested-locales --require-self-contained --json"));
        Grid.SetColumn(projectCommands, 1);
        Grid.SetRow(projectCommands, 1);
        grid.Children.Add(projectCommands);

        var notes = Card("Release Notes", ListPanel(
            "PompoEngine is pre-1.0 and should not be presented as a stable 1.0 engine yet.",
            "Runtime archives must stay editor-free and build-manifest verified.",
            "Project schema changes require migration behavior, tests, and compatibility notes.",
            "User-facing behavior changes should update README.md, docs/RUN_AND_USE.md, or release docs.",
            "Live public CI, Pages, and release artifact evidence is required before calling public release readiness complete."));
        Grid.SetColumn(notes, 0);
        Grid.SetColumnSpan(notes, 2);
        Grid.SetRow(notes, 2);
        grid.Children.Add(notes);

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = grid
        };
    }

    private Control CreateWorkspace()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };

        var toolbar = CreateWorkspaceToolbar();
        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        var workspace = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(_viewModel.WorkspaceColumnDefinitions),
            RowDefinitions = new RowDefinitions(_viewModel.WorkspaceRowDefinitions)
        };
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ProjectWorkspaceViewModel.WorkspaceColumnDefinitions) or
                nameof(ProjectWorkspaceViewModel.WorkspaceRowDefinitions))
            {
                ApplyWorkspaceLayout(workspace);
            }
        };

        BindVisibility(AddPanel(workspace, "Project", ResourceBrowserPanel(allowDetach: true), 0, 0, rowSpan: 3), "WorkspaceProjectPanelVisible");
        BindVisibility(AddWorkspaceColumnSplitter(workspace, 1), "WorkspaceProjectPanelVisible");
        BindVisibility(AddPanel(workspace, "Scene", ScenePanel(allowDetach: true), 2, 0), "WorkspaceScenePanelVisible");
        BindVisibility(AddWorkspaceColumnSplitter(workspace, 3), "WorkspaceRightPanelVisible");
        BindVisibility(AddPanel(workspace, "Inspector", GraphInspectorPanel(allowDetach: true), 4, 0), "WorkspaceInspectorPanelVisible");
        BindVisibility(AddWorkspaceRowSplitter(workspace, 2), "WorkspaceCenterSplitterVisible");
        BindVisibility(AddWorkspaceRowSplitter(workspace, 4), "WorkspaceRightPanelVisible");
        BindVisibility(AddPanel(workspace, "Graph", GraphPanel(allowDetach: true), 2, 2), "WorkspaceGraphPanelVisible");
        BindVisibility(AddPanel(workspace, "Console", ConsolePanel(allowDetach: true), 4, 2), "WorkspaceConsolePanelVisible");
        Grid.SetRow(workspace, 1);
        root.Children.Add(workspace);
        return root;
    }

    private Control CreateWorkspaceToolbar()
    {
        var presetButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        foreach (var preset in _viewModel.WorkspaceLayoutPresets)
        {
            var button = CommandButton(preset.DisplayName);
            button.Click += (_, _) => _viewModel.ApplyWorkspaceLayoutPreset(preset.PresetId);
            presetButtons.Children.Add(button);
        }
        Grid.SetColumn(presetButtons, 2);

        var panelToggles = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new global::Avalonia.Thickness(12, 0, 0, 0),
            Children =
            {
                WorkspacePanelToggle("Project", "WorkspaceProjectPanelVisible"),
                WorkspacePanelToggle("Scene", "WorkspaceScenePanelVisible"),
                WorkspacePanelToggle("Graph", "WorkspaceGraphPanelVisible"),
                WorkspacePanelToggle("Inspector", "WorkspaceInspectorPanelVisible"),
                WorkspacePanelToggle("Console", "WorkspaceConsolePanelVisible")
            }
        };
        Grid.SetColumn(panelToggles, 3);
        var saveWorkspace = CommandButton("Save Workspace");
        saveWorkspace.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.SaveWorkspacePreferencesAsync());
        Grid.SetColumn(saveWorkspace, 4);
        var detachTools = CommandButton("Detach Tools");
        detachTools.Click += (_, _) => OpenDetachedWorkspaceTools();
        Grid.SetColumn(detachTools, 5);

        var focusButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new global::Avalonia.Thickness(0, 8, 0, 0)
        };
        foreach (var target in _viewModel.WorkspaceFocusTargets)
        {
            var button = CommandButton(target.DisplayName);
            button.Click += (_, _) => _viewModel.FocusWorkspaceTarget(target.TargetId);
            focusButtons.Children.Add(button);
        }
        Grid.SetRow(focusButtons, 1);
        Grid.SetColumn(focusButtons, 0);
        Grid.SetColumnSpan(focusButtons, 6);

        return new Border
        {
            Background = PanelBackground,
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(1),
            Padding = new global::Avalonia.Thickness(10),
            Margin = new global::Avalonia.Thickness(0, 0, 0, 8),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto,Auto"),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Workspace Presets",
                        FontWeight = FontWeight.Bold,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new global::Avalonia.Thickness(0, 0, 12, 0)
                    },
                    WorkspacePresetDescription(),
                    presetButtons,
                    panelToggles,
                    saveWorkspace,
                    detachTools,
                    focusButtons
                }
            }
        };
    }

    private void OpenDetachedWorkspaceTools()
    {
        OpenDetachedResourceWindow();
        OpenDetachedGraphWindow();
        OpenDetachedInspectorWindow();
        OpenDetachedConsoleWindow();
    }

    private static CheckBox WorkspacePanelToggle(string text, string bindingPath)
    {
        var toggle = new CheckBox
        {
            Content = text,
            VerticalAlignment = VerticalAlignment.Center
        };
        toggle.Bind(
            ToggleButton.IsCheckedProperty,
            new Binding(bindingPath) { Mode = BindingMode.TwoWay });
        return toggle;
    }

    private Control WorkspacePresetDescription()
    {
        var description = BoundText("SelectedWorkspaceLayoutPreset.Description", "Default workspace layout.");
        Grid.SetColumn(description, 1);
        return description;
    }

    private void ApplyWorkspaceLayout(Grid workspace)
    {
        workspace.ColumnDefinitions = new ColumnDefinitions(_viewModel.WorkspaceColumnDefinitions);
        workspace.RowDefinitions = new RowDefinitions(_viewModel.WorkspaceRowDefinitions);
    }

    private static T BindVisibility<T>(T control, string bindingPath)
        where T : Control
    {
        control.Bind(
            IsVisibleProperty,
            new Binding(bindingPath)
            {
                FallbackValue = true,
                TargetNullValue = true
            });
        return control;
    }

    private static GridSplitter AddWorkspaceColumnSplitter(Grid grid, int column)
    {
        var splitter = new GridSplitter
        {
            Width = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = BorderColor
        };
        Grid.SetColumn(splitter, column);
        Grid.SetRow(splitter, 0);
        Grid.SetRowSpan(splitter, 3);
        grid.Children.Add(splitter);
        return splitter;
    }

    private static GridSplitter AddWorkspaceRowSplitter(Grid grid, int column)
    {
        var splitter = new GridSplitter
        {
            Height = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = BorderColor
        };
        Grid.SetColumn(splitter, column);
        Grid.SetRow(splitter, 1);
        grid.Children.Add(splitter);
        return splitter;
    }

    private Control CreateSavesPanel()
    {
        var refresh = CommandButton("Refresh Saves");
        refresh.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.RefreshSaveSlotsAsync());
        var delete = CommandButton("Delete Selected");
        delete.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.DeleteSelectedSaveSlotAsync());

        var slots = new ListBox
        {
            MinHeight = 360,
            ItemTemplate = new FuncDataTemplate<SaveSlotViewItem>((slot, _) => SaveSlotRow(slot))
        };
        slots.SelectionChanged += (_, _) =>
        {
            if (slots.SelectedItem is SaveSlotViewItem slot)
            {
                _viewModel.SelectedSaveSlotId = slot.SlotId;
            }
        };
        slots.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("SaveSlots")
            {
                FallbackValue = Array.Empty<SaveSlotViewItem>()
            });

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.2*,360"),
            Margin = new global::Avalonia.Thickness(0, 10, 0, 0)
        };

        AddPanel(
            grid,
            "Save Slots",
            new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            refresh,
                            delete
                        }
                    },
                    slots
                }
            },
            0,
            0);

        AddPanel(
            grid,
            "Selected Save",
            new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    BoundText("SelectedSaveSlot.SlotId", "No save selected", FontWeight.Bold),
                    BoundText("SelectedSaveSlot.DisplayName", "Display name unavailable"),
                    BoundText("SelectedSaveSlot.Location", "Graph location unavailable"),
                    BoundText("SelectedSaveSlot.SavedAtLocal", "Saved time unavailable"),
                    BoundText("StatusMessage", "Open a project to load save slots.")
                }
            },
            1,
            0);

        return grid;
    }

    private Control CreateLocalizationPanel()
    {
        var fillMissing = CommandButton("Fill Missing Values");
        fillMissing.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.FillMissingLocalizationValuesAsync());
        var saveEntry = PrimaryButton("Save Entry");
        saveEntry.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.SaveLocalizationEntryAsync());
        var deleteEntry = CommandButton("Delete Entry");
        deleteEntry.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.DeleteLocalizationEntryAsync());
        var localeEdit = new TextBox
        {
            PlaceholderText = "Locale",
            MinHeight = 34
        };
        localeEdit.Bind(
            TextBox.TextProperty,
            new Binding("LocaleEdit")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        var addLocale = CommandButton("Add Locale");
        addLocale.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.AddSupportedLocaleAsync());
        var deleteLocale = CommandButton("Delete Locale");
        deleteLocale.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.DeleteSelectedLocaleAsync());

        var tableId = new TextBox
        {
            PlaceholderText = "Table id",
            MinHeight = 34
        };
        tableId.Bind(
            TextBox.TextProperty,
            new Binding("LocalizationTableId")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        var key = new TextBox
        {
            PlaceholderText = "String key",
            MinHeight = 34
        };
        key.Bind(
            TextBox.TextProperty,
            new Binding("LocalizationKey")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        var value = new TextBox
        {
            PlaceholderText = "Localized text for selected locale",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 110
        };
        value.Bind(
            TextBox.TextProperty,
            new Binding("LocalizationValue")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        var locales = new ListBox
        {
            MinHeight = 160,
            ItemTemplate = new FuncDataTemplate<LocalizationLocaleItem>((locale, _) => LocalizationLocaleRow(locale))
        };
        locales.SelectionChanged += (_, _) =>
        {
            if (locales.SelectedItem is LocalizationLocaleItem locale)
            {
                _viewModel.SelectedPreviewLocale = locale.Locale;
            }
        };
        locales.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("LocalizationLocales")
            {
                FallbackValue = Array.Empty<LocalizationLocaleItem>()
            });

        var entries = new ListBox
        {
            MinHeight = 420,
            ItemTemplate = new FuncDataTemplate<LocalizationStringEntryItem>((entry, _) => LocalizationEntryRow(entry))
        };
        entries.SelectionChanged += (_, _) =>
        {
            if (entries.SelectedItem is LocalizationStringEntryItem entry)
            {
                _viewModel.SelectLocalizationEntry(entry.TableId, entry.Key);
            }
        };
        entries.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("LocalizationEntries")
            {
                FallbackValue = Array.Empty<LocalizationStringEntryItem>()
            });

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("300,*,360"),
            Margin = new global::Avalonia.Thickness(0, 10, 0, 0)
        };

        AddPanel(
            grid,
            "Locales",
            new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    localeEdit,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            addLocale,
                            deleteLocale
                        }
                    },
                    locales
                }
            },
            0,
            0);
        AddPanel(grid, "String Tables", entries, 1, 0);
        AddPanel(
            grid,
            "Localization Diagnostics",
            new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Entry Editor", Foreground = MutedText },
                    tableId,
                    key,
                    BoundText("SelectedPreviewLocale", "No locale selected", FontWeight.Bold),
                    value,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            saveEntry,
                            deleteEntry
                        }
                    },
                    fillMissing,
                    BoundText("LocalizationDiagnostics.Count", "0"),
                    DiagnosticList("LocalizationDiagnostics")
                }
            },
            2,
            0);

        return grid;
    }

    private Control CreateBuildPanel()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("360,*")
        };
        var profilePicker = new ComboBox
        {
            MinHeight = 34
        };
        profilePicker.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("BuildProfileOptions")
            {
                FallbackValue = Array.Empty<string>()
            });
        profilePicker.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("SelectedBuildProfileName")
            {
                Mode = BindingMode.TwoWay
            });

        var platformPicker = new ComboBox
        {
            MinHeight = 34
        };
        platformPicker.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("BuildPlatformOptions")
            {
                FallbackValue = Array.Empty<PompoTargetPlatform>()
            });
        platformPicker.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("SelectedBuildPlatform")
            {
                Mode = BindingMode.TwoWay
            });

        var releaseCandidate = new CheckBox
        {
            Content = "Force release candidate gates"
        };
        releaseCandidate.Bind(
            ToggleButton.IsCheckedProperty,
            new Binding("BuildReleaseCandidate")
            {
                Mode = BindingMode.TwoWay
            });
        var profileName = new TextBox { PlaceholderText = "Profile name", MinHeight = 34 };
        profileName.Bind(
            TextBox.TextProperty,
            new Binding("BuildProfileNameEdit")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        var appName = new TextBox { PlaceholderText = "App name", MinHeight = 34 };
        appName.Bind(
            TextBox.TextProperty,
            new Binding("BuildProfileAppName")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        var version = new TextBox { PlaceholderText = "Version", MinHeight = 34 };
        version.Bind(
            TextBox.TextProperty,
            new Binding("BuildProfileVersion")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        var packageRuntime = new CheckBox { Content = "Package runtime" };
        packageRuntime.Bind(
            ToggleButton.IsCheckedProperty,
            new Binding("BuildProfilePackageRuntime") { Mode = BindingMode.TwoWay });
        var runSmoke = new CheckBox { Content = "Run smoke test" };
        runSmoke.Bind(
            ToggleButton.IsCheckedProperty,
            new Binding("BuildProfileRunSmokeTest") { Mode = BindingMode.TwoWay });
        var selfContained = new CheckBox { Content = "Self-contained" };
        selfContained.Bind(
            ToggleButton.IsCheckedProperty,
            new Binding("BuildProfileSelfContained") { Mode = BindingMode.TwoWay });

        var buildSelected = PrimaryButton("Build Selected");
        buildSelected.Click += async (_, _) => await BuildProjectToFolderAsync();
        var saveProfile = CommandButton("Save Profile");
        saveProfile.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.SaveBuildProfileAsync());
        var deleteProfile = CommandButton("Delete Profile");
        deleteProfile.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.DeleteSelectedBuildProfileAsync());
        var buildVerifiedRelease = PrimaryButton("Build Verified Release");
        buildVerifiedRelease.Click += async (_, _) => await BuildVerifiedReleaseToFolderAsync();
        var packageRelease = CommandButton("Package Verified Release");
        packageRelease.Click += async (_, _) => await PackageReleaseToFolderAsync();

        AddPanel(
            grid,
            "Build Profile",
            new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Profile", Foreground = MutedText },
                    profilePicker,
                    new TextBlock { Text = "Profile Editor", Foreground = MutedText },
                    profileName,
                    appName,
                    version,
                    new TextBlock { Text = "Platform", Foreground = MutedText },
                    platformPicker,
                    packageRuntime,
                    runSmoke,
                    selfContained,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            saveProfile,
                            deleteProfile
                        }
                    },
                    releaseCandidate,
                    new TextBlock { Text = "Selected Profile", Foreground = MutedText },
                    BoundText("SelectedBuildProfileSummary", "No profile selected"),
                    BoundText("SelectedBuildProfilePath", "No profile path"),
                    new TextBlock { Text = "Output: selected folder from Build button", Foreground = MutedText },
                    BoundText("LastBuildOutputDirectory", "No build output yet"),
                    buildSelected,
                    buildVerifiedRelease,
                    packageRelease,
                    new TextBlock { Text = "Strict release verification requires smoke-tested locales.", Foreground = MutedText },
                    BoundText("LastReleaseArchivePath", "No release archive yet"),
                    BoundText("LastReleaseManifestPath", "No release manifest yet")
                }
            },
            0,
            0);
        AddPanel(
            grid,
            "Build Log",
            new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    BoundText("StatusMessage", "No build run yet"),
                    BoundText("BuildDiagnostics.Count", "0"),
                    new TextBlock { Text = "Last Build Summary", FontWeight = FontWeight.Bold },
                    BuildSummaryList(),
                    new TextBlock { Text = "Build History", FontWeight = FontWeight.Bold },
                    BuildHistoryList(),
                    new TextBlock { Text = "Validate project", Foreground = MutedText },
                    new TextBlock { Text = "Compile graph IR", Foreground = MutedText },
                    new TextBlock { Text = "Copy assets", Foreground = MutedText },
                    new TextBlock { Text = "Write manifest", Foreground = MutedText },
                    DiagnosticList("BuildDiagnostics"),
                    new TextBlock { Text = "Release Diagnostics", FontWeight = FontWeight.Bold },
                    BoundText("ReleaseDiagnostics.Count", "0"),
                    DiagnosticList("ReleaseDiagnostics")
                }
            },
            1,
            0);
        return grid;
    }

    private Control CreateThemePanel()
    {
        var saveTheme = PrimaryButton("Save Theme, Skin, Layout, Animation, Playback");
        saveTheme.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.SaveRuntimeThemeAsync());
        var resetLayout = CommandButton("Reset Layout");
        resetLayout.Click += (_, _) => _viewModel.ResetRuntimeLayoutFields();

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1*,1*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"),
            Margin = new global::Avalonia.Thickness(0, 10, 0, 0)
        };

        var stage = Card("Stage and Dialogue", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                ThemeField("Canvas clear", "RuntimeThemeCanvasClear"),
                ThemeField("Stage fallback", "RuntimeThemeStageFallback"),
                ThemeField("Stage active", "RuntimeThemeStageActiveFallback"),
                ThemeField("Dialogue background", "RuntimeThemeDialogueBackground"),
                ThemeField("Name box", "RuntimeThemeNameBoxBackground"),
                ThemeField("Choice", "RuntimeThemeChoiceBackground"),
                ThemeField("Selected choice", "RuntimeThemeChoiceSelectedBackground")
            }
        });
        Grid.SetColumn(stage, 0);
        Grid.SetRow(stage, 0);
        layout.Children.Add(stage);

        var overlays = Card("Overlays and Text", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                ThemeField("Save menu", "RuntimeThemeSaveMenuBackground"),
                ThemeField("Save slot", "RuntimeThemeSaveSlotBackground"),
                ThemeField("Empty save slot", "RuntimeThemeSaveSlotEmptyBackground"),
                ThemeField("Backlog", "RuntimeThemeBacklogBackground"),
                ThemeField("Text", "RuntimeThemeText"),
                ThemeField("Muted text", "RuntimeThemeMutedText"),
                ThemeField("Accent text", "RuntimeThemeAccentText"),
                ThemeField("Help text", "RuntimeThemeHelpText")
            }
        });
        Grid.SetColumn(overlays, 1);
        Grid.SetRow(overlays, 0);
        layout.Children.Add(overlays);

        var skin = Card("Image Skin Slots", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                SkinField("Dialogue box", "RuntimeSkinDialogueBoxAssetId"),
                SkinField("Name box", "RuntimeSkinNameBoxAssetId"),
                SkinField("Choice box", "RuntimeSkinChoiceBoxAssetId"),
                SkinField("Selected choice", "RuntimeSkinChoiceSelectedBoxAssetId"),
                SkinField("Disabled choice", "RuntimeSkinChoiceDisabledBoxAssetId"),
                SkinField("Save menu panel", "RuntimeSkinSaveMenuPanelAssetId"),
                SkinField("Save slot", "RuntimeSkinSaveSlotAssetId"),
                SkinField("Selected save slot", "RuntimeSkinSaveSlotSelectedAssetId"),
                SkinField("Empty save slot", "RuntimeSkinSaveSlotEmptyAssetId"),
                SkinField("Backlog panel", "RuntimeSkinBacklogPanelAssetId")
            }
        });
        Grid.SetColumn(skin, 0);
        Grid.SetColumnSpan(skin, 2);
        Grid.SetRow(skin, 1);
        layout.Children.Add(skin);

        var geometry = Card("Layout Geometry", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new RuntimeUiLayoutPreview { Workspace = _viewModel },
                LayoutRectFields(
                    "Dialogue text box",
                    "RuntimeLayoutDialogueTextBoxX",
                    "RuntimeLayoutDialogueTextBoxY",
                    "RuntimeLayoutDialogueTextBoxWidth",
                    "RuntimeLayoutDialogueTextBoxHeight"),
                LayoutRectFields(
                    "Dialogue name box",
                    "RuntimeLayoutDialogueNameBoxX",
                    "RuntimeLayoutDialogueNameBoxY",
                    "RuntimeLayoutDialogueNameBoxWidth",
                    "RuntimeLayoutDialogueNameBoxHeight"),
                LayoutRectFields(
                    "Save menu",
                    "RuntimeLayoutSaveMenuX",
                    "RuntimeLayoutSaveMenuY",
                    "RuntimeLayoutSaveMenuWidth",
                    "RuntimeLayoutSaveMenuHeight"),
                LayoutRectFields(
                    "Backlog",
                    "RuntimeLayoutBacklogX",
                    "RuntimeLayoutBacklogY",
                    "RuntimeLayoutBacklogWidth",
                    "RuntimeLayoutBacklogHeight"),
                new StackPanel
                {
                    Spacing = 8,
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        LayoutField("Choice width", "RuntimeLayoutChoiceBoxWidth"),
                        LayoutField("Choice height", "RuntimeLayoutChoiceBoxHeight"),
                        LayoutField("Choice spacing", "RuntimeLayoutChoiceBoxSpacing"),
                        LayoutField("Save slot height", "RuntimeLayoutSaveSlotHeight"),
                        LayoutField("Save slot spacing", "RuntimeLayoutSaveSlotSpacing")
                    }
                },
                resetLayout
            }
        });
        Grid.SetColumn(geometry, 0);
        Grid.SetColumnSpan(geometry, 2);
        Grid.SetRow(geometry, 2);
        layout.Children.Add(geometry);

        var animationEnabled = new CheckBox { Content = "Enable runtime UI animation" };
        animationEnabled.Bind(
            ToggleButton.IsCheckedProperty,
            new Binding("RuntimeAnimationEnabled") { Mode = BindingMode.TwoWay });
        var animationPresetButtons = new StackPanel
        {
            Spacing = 8,
            Orientation = Orientation.Horizontal
        };
        foreach (var preset in _viewModel.RuntimeAnimationPresets)
        {
            var button = CommandButton(preset.DisplayName);
            button.Click += (_, _) => _viewModel.ApplyRuntimeAnimationPreset(preset.PresetId);
            animationPresetButtons.Children.Add(button);
        }

        var animation = Card("Animation", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                animationEnabled,
                new TextBlock
                {
                    Text = "Animation Presets",
                    FontWeight = FontWeight.Bold
                },
                animationPresetButtons,
                BoundText("RuntimeAnimationPresetSummary", "Custom animation values"),
                new StackPanel
                {
                    Spacing = 8,
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        LayoutField("Panel fade ms", "RuntimeAnimationPanelFadeMilliseconds"),
                        LayoutField("Choice pulse ms", "RuntimeAnimationChoicePulseMilliseconds"),
                        LayoutField("Choice pulse strength", "RuntimeAnimationChoicePulseStrength"),
                        LayoutField("Text reveal cps", "RuntimeAnimationTextRevealCharactersPerSecond")
                    }
                },
                new StackPanel
                {
                    Spacing = 8,
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        LayoutField("Auto delay ms", "RuntimePlaybackAutoForwardDelayMilliseconds"),
                        LayoutField("Skip interval ms", "RuntimePlaybackSkipIntervalMilliseconds")
                    }
                }
            }
        });
        Grid.SetColumn(animation, 0);
        Grid.SetColumnSpan(animation, 2);
        Grid.SetRow(animation, 3);
        layout.Children.Add(animation);

        var footer = Card("Runtime Theme", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Colors use #RRGGBB or #RRGGBBAA. Skin slots use image asset IDs. Layout values use virtual-canvas integer coordinates. Animation pulse strength uses 0 to 1. Text reveal, auto delay, and skip interval use 0 or greater.",
                    Foreground = MutedText,
                    TextWrapping = TextWrapping.Wrap
                },
                saveTheme,
                BoundText("StatusMessage", "Open a project before saving the runtime theme.")
            }
        });
        Grid.SetColumn(footer, 0);
        Grid.SetColumnSpan(footer, 2);
        Grid.SetRow(footer, 4);
        layout.Children.Add(footer);

        return new ScrollViewer { Content = layout };
    }

    private static Control LayoutRectFields(
        string label,
        string xBindingPath,
        string yBindingPath,
        string widthBindingPath,
        string heightBindingPath)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1*,1*,1*,1*")
        };

        AddLayoutField(grid, LayoutField("X", xBindingPath), 0);
        AddLayoutField(grid, LayoutField("Y", yBindingPath), 1);
        AddLayoutField(grid, LayoutField("Width", widthBindingPath), 2);
        AddLayoutField(grid, LayoutField("Height", heightBindingPath), 3);

        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = label, FontWeight = FontWeight.SemiBold },
                grid
            }
        };
    }

    private static void AddLayoutField(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private static Control LayoutField(string label, string bindingPath)
    {
        var input = new TextBox
        {
            MinHeight = 34,
            Width = 110,
            PlaceholderText = "0"
        };
        input.Bind(
            TextBox.TextProperty,
            new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label, Foreground = MutedText },
                input
            }
        };
    }

    private static Control SkinField(string label, string bindingPath)
    {
        var input = new TextBox
        {
            MinHeight = 34,
            PlaceholderText = "image asset id"
        };
        input.Bind(
            TextBox.TextProperty,
            new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label, FontWeight = FontWeight.SemiBold },
                input
            }
        };
    }

    private static Control ThemeField(string label, string bindingPath)
    {
        var input = new TextBox
        {
            MinHeight = 34,
            PlaceholderText = "#RRGGBB or #RRGGBBAA"
        };
        input.Bind(
            TextBox.TextProperty,
            new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        var text = new TextBlock
        {
            Text = label,
            Foreground = MutedText,
            VerticalAlignment = VerticalAlignment.Center
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("180,*")
        };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(input, 1);
        grid.Children.Add(text);
        grid.Children.Add(input);
        return grid;
    }

    private static Control DiagnosticList(string bindingPath)
    {
        var list = new ListBox
        {
            MinHeight = 160,
            ItemTemplate = new FuncDataTemplate<EditorDiagnostic>((diagnostic, _) => DiagnosticRow(diagnostic))
        };
        list.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding(bindingPath)
            {
                FallbackValue = Array.Empty<EditorDiagnostic>()
            });
        return list;
    }

    private static Control BuildSummaryList()
    {
        var list = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<BuildSummaryItem>((item, _) => BuildSummaryRow(item))
        };
        list.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("LastBuildSummaryItems")
            {
                FallbackValue = Array.Empty<BuildSummaryItem>()
            });
        return list;
    }

    private static Control BuildSummaryRow(BuildSummaryItem? item)
    {
        if (item is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        var label = new TextBlock
        {
            Text = item.Label,
            Foreground = MutedText,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(label, 0);

        var value = new TextBlock
        {
            Text = item.Value,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(value, 1);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,1.1*"),
            Margin = new global::Avalonia.Thickness(0, 0, 0, 3),
            Children =
            {
                label,
                value
            }
        };
    }

    private static Control BuildHistoryList()
    {
        var list = new ListBox
        {
            MinHeight = 140,
            ItemTemplate = new FuncDataTemplate<BuildHistoryViewItem>((item, _) => BuildHistoryRow(item))
        };
        list.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("BuildHistory")
            {
                FallbackValue = Array.Empty<BuildHistoryViewItem>()
            });
        return list;
    }

    private static Control BuildHistoryRow(BuildHistoryViewItem? item)
    {
        if (item is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        return new Border
        {
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Padding = new global::Avalonia.Thickness(8),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{item.ProfileName} | {item.Platform} | {item.Status}",
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = $"{item.AppVersion} | {item.BuiltAtLocal}",
                        Foreground = MutedText,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = item.OutputDirectory,
                        Foreground = MutedText,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
    }

    private static Control ReadinessList()
    {
        var list = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<EditorReadinessItem>((item, _) => ReadinessRow(item))
        };
        list.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("ProductionReadinessItems")
            {
                FallbackValue = Array.Empty<EditorReadinessItem>()
            });
        return list;
    }

    private static Control ReadinessRow(EditorReadinessItem? item)
    {
        if (item is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        var color = item.IsPassing
            ? Brush.Parse("#166534")
            : item.IsBlocking
                ? Brush.Parse("#b91c1c")
                : Brush.Parse("#a16207");

        var detail = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = item.Title,
                    FontWeight = FontWeight.Bold
                },
                new TextBlock
                {
                    Text = item.Detail,
                    Foreground = MutedText,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
        Grid.SetColumn(detail, 0);

        var status = new TextBlock
        {
            Text = item.Status,
            Foreground = color,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(status, 1);

        return new Border
        {
            Padding = new global::Avalonia.Thickness(8),
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,90"),
                Children =
                {
                    detail,
                    status
                }
            }
        };
    }

    private static Control DiagnosticRow(EditorDiagnostic? diagnostic)
    {
        if (diagnostic is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        return new Border
        {
            Padding = new global::Avalonia.Thickness(8),
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = diagnostic.Code,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brush.Parse("#b91c1c")
                    },
                    new TextBlock
                    {
                        Text = diagnostic.Message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = diagnostic.DocumentPath ?? diagnostic.ElementId ?? string.Empty,
                        Foreground = MutedText,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
    }

    private static Control LocalizationLocaleRow(LocalizationLocaleItem? locale)
    {
        if (locale is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        return new Border
        {
            Padding = new global::Avalonia.Thickness(8),
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = locale.Locale,
                        FontWeight = FontWeight.Bold
                    },
                    new TextBlock
                    {
                        Text = locale.IsPreviewLocale ? "Preview" : string.Empty,
                        Foreground = locale.IsPreviewLocale ? Accent : MutedText,
                        FontSize = 12
                    }
                }
            }
        };
    }

    private static Control LocalizationEntryRow(LocalizationStringEntryItem? entry)
    {
        if (entry is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        var status = entry.HasMissingValues
            ? "Missing locale value"
            : entry.HasUnsupportedValues
                ? "Unsupported locale"
                : "Complete";
        var statusColor = entry.HasMissingValues || entry.HasUnsupportedValues
            ? Brush.Parse("#b91c1c")
            : Brush.Parse("#166534");

        return new Border
        {
            Padding = new global::Avalonia.Thickness(8),
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{entry.TableId}:{entry.Key}",
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = entry.ValuesSummary,
                        Foreground = MutedText,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = status,
                        Foreground = statusColor,
                        FontSize = 12
                    }
                }
            }
        };
    }

    private static Control SceneRow(SceneViewItem? scene)
    {
        if (scene is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        return new Border
        {
            Padding = new global::Avalonia.Thickness(8),
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Background = scene.IsSelected ? Brush.Parse("#eff6ff") : Brushes.Transparent,
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = scene.DisplayName,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = $"{scene.SceneId} -> {scene.StartGraphId}",
                        Foreground = MutedText,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(scene.BackgroundAssetId)
                            ? $"{scene.CharacterCount} character(s)"
                            : $"{scene.BackgroundAssetId} | {scene.CharacterCount} character(s)",
                        Foreground = MutedText,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
    }

    private static Control SceneLayerRow(SceneLayerViewItem? layer)
    {
        if (layer is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        return new TextBlock
        {
            Text = $"{layer.LayerId}: {layer.Layer} asset={layer.AssetId} pos=({layer.X:0.##}, {layer.Y:0.##}) opacity={layer.Opacity:0.##}",
            Foreground = MutedText,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static Control SceneCharacterRow(SceneCharacterPlacementViewItem? character)
    {
        if (character is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        return new Border
        {
            Padding = new global::Avalonia.Thickness(8),
            Background = character.IsSelected ? Brush.Parse("#eff6ff") : Brushes.Transparent,
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Child = new TextBlock
            {
                Text = $"{character.PlacementId}: {character.CharacterId} {character.InitialExpressionId} {character.Layer} ({character.X:0.##}, {character.Y:0.##})",
                Foreground = MutedText,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static Control CharacterRow(CharacterViewItem? character)
    {
        if (character is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        return new Border
        {
            Padding = new global::Avalonia.Thickness(8),
            Background = character.IsSelected ? Brush.Parse("#eff6ff") : Brushes.Transparent,
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = character.DisplayName,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = $"{character.CharacterId} | default={character.DefaultExpression} | expressions={character.ExpressionCount}",
                        Foreground = MutedText,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
    }

    private static Control CharacterExpressionRow(CharacterExpressionViewItem? expression)
    {
        if (expression is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        return new Border
        {
            Padding = new global::Avalonia.Thickness(8),
            Background = expression.IsSelected ? Brush.Parse("#eff6ff") : Brushes.Transparent,
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = expression.ExpressionId,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(expression.Description)
                            ? expression.SpriteAssetId
                            : $"{expression.SpriteAssetId} | {expression.Description}",
                        Foreground = MutedText,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
    }

    private void OpenDetachedSceneWindow()
    {
        if (_detachedSceneWindow is not null)
        {
            _detachedSceneWindow.Activate();
            return;
        }

        var sceneWindow = new Window
        {
            Title = "Pompo Scene",
            Width = 1060,
            Height = 760,
            MinWidth = 820,
            MinHeight = 560,
            DataContext = _viewModel,
            Content = new Border
            {
                Background = AppBackground,
                Padding = new global::Avalonia.Thickness(12),
                Child = Card(
                    "Scene",
                    new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = ScenePanel(allowDetach: false)
                    })
            }
        };
        sceneWindow.Closed += (_, _) => _detachedSceneWindow = null;
        _detachedSceneWindow = sceneWindow;
        sceneWindow.Show(this);
    }

    private Control ScenePanel(bool allowDetach)
    {
        var scenes = new ListBox
        {
            MinHeight = 120,
            ItemTemplate = new FuncDataTemplate<SceneViewItem>((scene, _) => SceneRow(scene))
        };
        scenes.SelectionChanged += (_, _) =>
        {
            if (scenes.SelectedItem is SceneViewItem scene)
            {
                _viewModel.SelectedSceneId = scene.SceneId;
            }
        };
        scenes.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("Scenes")
            {
                FallbackValue = Array.Empty<SceneViewItem>()
            });

        var displayName = new TextBox
        {
            PlaceholderText = "Scene display name",
            MinHeight = 34
        };
        displayName.Bind(
            TextBox.TextProperty,
            new Binding("SceneDisplayName")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        var startGraph = new ComboBox
        {
            MinHeight = 34
        };
        startGraph.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("SceneStartGraphOptions")
            {
                FallbackValue = Array.Empty<string>()
            });
        startGraph.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("SceneStartGraphId")
            {
                Mode = BindingMode.TwoWay
            });

        var background = new ComboBox
        {
            MinHeight = 34
        };
        background.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("SceneBackgroundAssetOptions")
            {
                FallbackValue = Array.Empty<string>()
            });
        background.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("SceneBackgroundAssetId")
            {
                Mode = BindingMode.TwoWay
            });

        var projectCharacters = new ListBox
        {
            MinHeight = 90,
            ItemTemplate = new FuncDataTemplate<CharacterViewItem>((character, _) => CharacterRow(character))
        };
        projectCharacters.SelectionChanged += (_, _) =>
        {
            if (projectCharacters.SelectedItem is CharacterViewItem character)
            {
                _viewModel.SelectedCharacterId = character.CharacterId;
            }
        };
        projectCharacters.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("Characters")
            {
                FallbackValue = Array.Empty<CharacterViewItem>()
            });

        var addCharacter = CommandButton("Add Character");
        addCharacter.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.AddCharacterAsync());
        var deleteCharacter = CommandButton("Delete Character");
        deleteCharacter.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.DeleteSelectedCharacterAsync());
        var saveCharacter = CommandButton("Save Character");
        saveCharacter.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.SaveSelectedCharacterAsync());

        var characterDisplayName = new TextBox
        {
            PlaceholderText = "Character display name",
            MinHeight = 34
        };
        characterDisplayName.Bind(
            TextBox.TextProperty,
            new Binding("CharacterDisplayName")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        var defaultExpression = new ComboBox
        {
            MinHeight = 34
        };
        defaultExpression.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("CharacterDefaultExpressionOptions")
            {
                FallbackValue = Array.Empty<string>()
            });
        defaultExpression.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("CharacterDefaultExpression")
            {
                Mode = BindingMode.TwoWay
            });

        var expressions = new ListBox
        {
            MinHeight = 90,
            ItemTemplate = new FuncDataTemplate<CharacterExpressionViewItem>((expression, _) => CharacterExpressionRow(expression))
        };
        expressions.SelectionChanged += (_, _) =>
        {
            if (expressions.SelectedItem is CharacterExpressionViewItem expression)
            {
                _viewModel.SelectedCharacterExpressionId = expression.ExpressionId;
            }
        };
        expressions.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("CharacterExpressions")
            {
                FallbackValue = Array.Empty<CharacterExpressionViewItem>()
            });

        var addExpression = CommandButton("Add Expression");
        addExpression.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.AddCharacterExpressionAsync());
        var deleteExpression = CommandButton("Delete Expression");
        deleteExpression.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.DeleteSelectedCharacterExpressionAsync());
        var saveExpression = CommandButton("Save Expression");
        saveExpression.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.SaveSelectedCharacterExpressionAsync());

        var expressionSprite = new ComboBox
        {
            MinHeight = 34
        };
        expressionSprite.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("CharacterExpressionSpriteAssetOptions")
            {
                FallbackValue = Array.Empty<string>()
            });
        expressionSprite.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("CharacterExpressionSpriteAssetId")
            {
                Mode = BindingMode.TwoWay
            });

        var expressionDescription = new TextBox
        {
            PlaceholderText = "Expression description",
            MinHeight = 34
        };
        expressionDescription.Bind(
            TextBox.TextProperty,
            new Binding("CharacterExpressionDescription")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        var addScene = CommandButton("Add Scene");
        addScene.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.AddSceneAsync());
        var deleteScene = CommandButton("Delete Scene");
        deleteScene.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.DeleteSelectedSceneAsync());
        var saveScene = PrimaryButton("Save Scene");
        saveScene.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.SaveSelectedSceneAsync());
        var addPlacement = CommandButton("Add Placement");
        addPlacement.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.AddSceneCharacterPlacementAsync());
        var deletePlacement = CommandButton("Delete Placement");
        deletePlacement.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.DeleteSelectedSceneCharacterPlacementAsync());

        var layers = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<SceneLayerViewItem>((layer, _) => SceneLayerRow(layer))
        };
        layers.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("SceneLayerItems")
            {
                FallbackValue = Array.Empty<SceneLayerViewItem>()
            });

        var characters = new ListBox
        {
            MinHeight = 92,
            ItemTemplate = new FuncDataTemplate<SceneCharacterPlacementViewItem>((character, _) => SceneCharacterRow(character))
        };
        characters.SelectionChanged += (_, _) =>
        {
            if (characters.SelectedItem is SceneCharacterPlacementViewItem character)
            {
                _viewModel.SelectedScenePlacementId = character.PlacementId;
            }
        };
        characters.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("SceneCharacterPlacements")
            {
                FallbackValue = Array.Empty<SceneCharacterPlacementViewItem>()
            });

        var characterPicker = new ComboBox
        {
            MinHeight = 34
        };
        characterPicker.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("SceneCharacterOptions")
            {
                FallbackValue = Array.Empty<string>()
            });
        characterPicker.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("ScenePlacementCharacterId")
            {
                Mode = BindingMode.TwoWay
            });

        var expressionPicker = new ComboBox
        {
            MinHeight = 34
        };
        expressionPicker.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("ScenePlacementExpressionOptions")
            {
                FallbackValue = Array.Empty<string>()
            });
        expressionPicker.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("ScenePlacementExpressionId")
            {
                Mode = BindingMode.TwoWay
            });

        var layerPicker = new ComboBox
        {
            MinHeight = 34
        };
        layerPicker.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("ScenePlacementLayerOptions")
            {
                FallbackValue = Array.Empty<RuntimeLayer>()
            });
        layerPicker.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("ScenePlacementLayer")
            {
                Mode = BindingMode.TwoWay
            });

        var placementX = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 1,
            Increment = 0.05m,
            FormatString = "0.00",
            MinHeight = 34
        };
        placementX.Bind(
            NumericUpDown.ValueProperty,
            new Binding("ScenePlacementX")
            {
                Mode = BindingMode.TwoWay
            });

        var placementY = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 1.2m,
            Increment = 0.05m,
            FormatString = "0.00",
            MinHeight = 34
        };
        placementY.Bind(
            NumericUpDown.ValueProperty,
            new Binding("ScenePlacementY")
            {
                Mode = BindingMode.TwoWay
            });

        var placementGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            Children =
            {
                characterPicker,
                expressionPicker,
                layerPicker,
                placementX,
                placementY
            }
        };
        Grid.SetColumn(characterPicker, 0);
        Grid.SetRow(characterPicker, 0);
        Grid.SetColumn(expressionPicker, 1);
        Grid.SetRow(expressionPicker, 0);
        Grid.SetColumn(layerPicker, 0);
        Grid.SetRow(layerPicker, 1);
        Grid.SetColumn(placementX, 0);
        Grid.SetRow(placementX, 2);
        Grid.SetColumn(placementY, 1);
        Grid.SetRow(placementY, 2);

        var sceneEditor = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Scenes", Foreground = MutedText },
                scenes,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        addScene,
                        deleteScene
                    }
                },
                new TextBlock { Text = "Display Name", Foreground = MutedText },
                displayName,
                new TextBlock { Text = "Start Graph", Foreground = MutedText },
                startGraph,
                new TextBlock { Text = "Background Asset", Foreground = MutedText },
                background,
                saveScene,
                new TextBlock { Text = "Project Characters", Foreground = MutedText },
                projectCharacters,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        addCharacter,
                        deleteCharacter
                    }
                },
                new TextBlock { Text = "Character Display Name", Foreground = MutedText },
                characterDisplayName,
                new TextBlock { Text = "Default Expression", Foreground = MutedText },
                defaultExpression,
                saveCharacter,
                new TextBlock { Text = "Expressions", Foreground = MutedText },
                expressions,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        addExpression,
                        deleteExpression
                    }
                },
                new TextBlock { Text = "Expression Sprite", Foreground = MutedText },
                expressionSprite,
                new TextBlock { Text = "Expression Description", Foreground = MutedText },
                expressionDescription,
                saveExpression,
                BoundText("SelectedScene.SceneId", "No scene selected")
            }
        };

        var scenePreview = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Stage Layout", Foreground = MutedText },
                new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    MaxHeight = 260,
                    Child = new SceneStageView
                    {
                        Workspace = _viewModel
                    }
                },
                new TextBlock { Text = "Runtime Preview", Foreground = MutedText },
                PreviewCanvas(allowDetach: true),
                new TextBlock { Text = "Layers", Foreground = MutedText },
                layers,
                new TextBlock { Text = "Characters", Foreground = MutedText },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        addPlacement,
                        deletePlacement
                    }
                },
                characters,
                new TextBlock { Text = "Selected Character Placement", Foreground = MutedText },
                placementGrid
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("300,*")
        };
        Grid.SetColumn(sceneEditor, 0);
        Grid.SetColumn(scenePreview, 1);
        grid.Children.Add(sceneEditor);
        grid.Children.Add(scenePreview);
        if (!allowDetach)
        {
            return grid;
        }

        var detachScene = CommandButton("Detach Scene");
        detachScene.Click += (_, _) => OpenDetachedSceneWindow();
        Grid.SetRow(grid, 1);
        return new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children =
            {
                detachScene,
                grid
            }
        };
    }

    private void OpenDetachedGraphWindow()
    {
        if (_detachedGraphWindow is not null)
        {
            _detachedGraphWindow.Activate();
            return;
        }

        var graphWindow = new Window
        {
            Title = "Pompo Graph",
            Width = 1160,
            Height = 780,
            MinWidth = 860,
            MinHeight = 560,
            DataContext = _viewModel,
            Content = new Border
            {
                Background = AppBackground,
                Padding = new global::Avalonia.Thickness(12),
                Child = Card(
                    "Graph",
                    new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = GraphPanel(allowDetach: false)
                    })
            }
        };
        graphWindow.Closed += (_, _) => _detachedGraphWindow = null;
        _detachedGraphWindow = graphWindow;
        graphWindow.Show(this);
    }

    private Control GraphPanel(bool allowDetach)
    {
        var graphPicker = new ComboBox
        {
            MinWidth = 220,
            MinHeight = 34
        };
        graphPicker.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("GraphOptions")
            {
                FallbackValue = Array.Empty<string>()
            });
        graphPicker.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("SelectedGraphId")
            {
                Mode = BindingMode.TwoWay
            });
        var addGraph = CommandButton("Add Graph");
        addGraph.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.AddGraphAsync());
        var deleteGraph = CommandButton("Delete Graph");
        deleteGraph.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.DeleteSelectedGraphAsync());
        var graphIdEdit = new TextBox
        {
            MinWidth = 220,
            MinHeight = 34,
            PlaceholderText = "Graph id"
        };
        graphIdEdit.Bind(
            TextBox.TextProperty,
            new Binding("GraphIdEdit")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        var renameGraph = CommandButton("Rename Graph");
        renameGraph.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.RenameSelectedGraphAsync());

        var addNarration = CommandButton("Add Narration");
        addNarration.Click += (_, _) => _viewModel.AddNodeToCurrentGraph(GraphNodeKind.Narration);
        var addDialogue = CommandButton("Add Dialogue");
        addDialogue.Click += (_, _) => _viewModel.AddNodeToCurrentGraph(GraphNodeKind.Dialogue);
        var addChoice = CommandButton("Add Choice");
        addChoice.Click += (_, _) => _viewModel.AddNodeToCurrentGraph(GraphNodeKind.Choice);
        var nodeKindPicker = new ComboBox
        {
            MinWidth = 170,
            MinHeight = 34
        };
        nodeKindPicker.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("AddableNodeKinds")
            {
                FallbackValue = Array.Empty<GraphNodeKind>()
            });
        nodeKindPicker.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("SelectedNodeKindToAdd")
            {
                Mode = BindingMode.TwoWay
            });
        var addSelected = CommandButton("Add Node");
        addSelected.Click += (_, _) => _viewModel.AddSelectedNodeKindToCurrentGraph();
        var customNodePicker = new ComboBox
        {
            MinWidth = 210,
            MinHeight = 34
        };
        customNodePicker.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("CustomNodePaletteItems")
            {
                FallbackValue = Array.Empty<CustomNodePaletteItem>()
            });
        customNodePicker.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("SelectedCustomNodeToAdd")
            {
                Mode = BindingMode.TwoWay
            });
        var addCustomNode = CommandButton("Add Custom");
        addCustomNode.Click += (_, _) => _viewModel.AddSelectedCustomNodeToCurrentGraph();
        var undo = CommandButton("Undo");
        undo.Click += (_, _) => _viewModel.UndoCurrentGraphEdit();
        var redo = CommandButton("Redo");
        redo.Click += (_, _) => _viewModel.RedoCurrentGraphEdit();
        var duplicateNode = CommandButton("Duplicate");
        duplicateNode.Click += (_, _) => _viewModel.DuplicateSelectedGraphNode();
        var deleteNode = CommandButton("Delete Node");
        deleteNode.Click += (_, _) => _viewModel.DeleteSelectedGraphNode();
        var saveGraph = PrimaryButton("Save Graph");
        saveGraph.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.SaveCurrentGraphAsync());
        var setSource = CommandButton("Set Source");
        setSource.Click += async (_, _) => await RunEditorActionAsync(() =>
        {
            _viewModel.MarkSelectedGraphNodeAsConnectionSource();
            return Task.CompletedTask;
        });
        var connectTarget = CommandButton("Connect");
        connectTarget.Click += async (_, _) => await RunEditorActionAsync(() =>
        {
            _viewModel.ConnectGraphSourceToSelectedNode();
            return Task.CompletedTask;
        });
        var disconnect = CommandButton("Disconnect");
        disconnect.Click += async (_, _) => await RunEditorActionAsync(() =>
        {
            _viewModel.DisconnectSelectedGraphNodeEdges();
            return Task.CompletedTask;
        });

        var nodes = new ListBox
        {
            MinHeight = 120,
            ItemTemplate = new FuncDataTemplate<GraphNodeViewItem>((node, _) => GraphNodeRow(node))
        };
        nodes.SelectionChanged += (_, _) =>
        {
            if (nodes.SelectedItem is GraphNodeViewItem node)
            {
                _viewModel.GraphEditor?.SelectNode(node.NodeId);
            }
        };
        nodes.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("GraphEditor.Nodes")
            {
                FallbackValue = Array.Empty<GraphNodeViewItem>()
            });

        var edges = new ListBox
        {
            MinHeight = 80,
            ItemTemplate = new FuncDataTemplate<GraphEdgeViewItem>((edge, _) => GraphEdgeRow(edge))
        };
        edges.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("GraphEditor.Edges")
            {
                FallbackValue = Array.Empty<GraphEdgeViewItem>()
            });

        var graphCanvas = new GraphCanvasView
        {
            Background = Brush.Parse("#f8fafc")
        };
        graphCanvas.Bind(
            GraphCanvasView.GraphEditorProperty,
            new Binding("GraphEditor"));

        var graphSurface = new Border
        {
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(1),
            Background = Brush.Parse("#f8fafc"),
            MinHeight = 230,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = graphCanvas
            }
        };

        var graphHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Graph",
                    Foreground = MutedText,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
        if (allowDetach)
        {
            var detachGraph = CommandButton("Detach Graph");
            detachGraph.Click += (_, _) => OpenDetachedGraphWindow();
            graphHeader.Children.Add(detachGraph);
        }

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.1*,1*"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        graphHeader,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Children =
                            {
                                graphPicker,
                                addGraph,
                                deleteGraph
                            }
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Children =
                            {
                                graphIdEdit,
                                renameGraph
                            }
                        },
                        BoundText("GraphEditor.GraphId", "No graph loaded", FontWeight.Bold),
                        BoundText("GraphEditor.IsDirty", "Clean"),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Children =
                            {
                                addNarration,
                                addDialogue,
                                addChoice
                            }
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Children =
                            {
                                nodeKindPicker,
                                addSelected,
                                customNodePicker,
                                addCustomNode,
                                undo,
                                redo,
                                duplicateNode,
                                deleteNode,
                                saveGraph
                            }
                        },
                        BoundText("GraphEditor.CanUndo", "Undo unavailable"),
                        BoundText("GraphEditor.CanRedo", "Redo unavailable"),
                        BoundText("CustomNodePaletteStatus", "No custom script nodes loaded"),
                        new TextBlock
                        {
                            Text = "Canvas: click a node to set source, click another node to connect, drag to move.",
                            Foreground = MutedText,
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap
                        },
                        graphSurface,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Children =
                            {
                                setSource,
                                connectTarget,
                                disconnect
                            }
                        },
                        BoundText("GraphEditor.ConnectionSourceNodeId", "No connection source"),
                        nodes,
                        edges
                    }
                },
                CreateGraphDiagnosticColumn()
            }
        };
    }

    private void OpenDetachedInspectorWindow()
    {
        if (_detachedInspectorWindow is not null)
        {
            _detachedInspectorWindow.Activate();
            return;
        }

        var inspectorWindow = new Window
        {
            Title = "Pompo Inspector",
            Width = 620,
            Height = 680,
            MinWidth = 520,
            MinHeight = 520,
            DataContext = _viewModel,
            Content = new Border
            {
                Background = AppBackground,
                Padding = new global::Avalonia.Thickness(12),
                Child = Card(
                    "Inspector",
                    new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = GraphInspectorPanel(allowDetach: false)
                    })
            }
        };
        inspectorWindow.Closed += (_, _) => _detachedInspectorWindow = null;
        _detachedInspectorWindow = inspectorWindow;
        inspectorWindow.Show(this);
    }

    private Control GraphInspectorPanel(bool allowDetach)
    {
        var text = new TextBox
        {
            PlaceholderText = "Dialogue or narration text",
            AcceptsReturn = true,
            MinHeight = 120,
            TextWrapping = TextWrapping.Wrap
        };
        text.Bind(
            TextBox.TextProperty,
            new Binding("GraphEditor.SelectedNodeText")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        var properties = new TextBox
        {
            PlaceholderText = "{ }",
            AcceptsReturn = true,
            MinHeight = 220,
            FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
            TextWrapping = TextWrapping.NoWrap
        };
        properties.Bind(
            TextBox.TextProperty,
            new Binding("GraphEditor.SelectedNodePropertiesJson")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            });

        var applyProperties = CommandButton("Apply Properties");
        applyProperties.Click += (_, _) => _viewModel.GraphEditor?.ApplySelectedNodePropertiesJson();

        var propertyHints = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<GraphNodePropertyViewItem>((property, _) => GraphPropertyHintRow(property))
        };
        propertyHints.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("GraphEditor.SelectedNodePropertyHints")
            {
                FallbackValue = Array.Empty<GraphNodePropertyViewItem>()
            });

        var content = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                BoundText("GraphEditor.SelectedNodeId", "No node selected", FontWeight.Bold),
                BoundText("GraphEditor.SelectedNodeKind", "Select a graph node"),
                new TextBlock { Text = "Text", Foreground = MutedText },
                text,
                new TextBlock { Text = "Expected Properties", Foreground = MutedText },
                propertyHints,
                new TextBlock { Text = "Raw Properties", Foreground = MutedText },
                properties,
                applyProperties,
                BoundText("GraphEditor.SelectedNodePropertiesJsonError", string.Empty)
            }
        };

        if (allowDetach)
        {
            var detachInspector = CommandButton("Detach Inspector");
            detachInspector.Click += (_, _) => OpenDetachedInspectorWindow();
            content.Children.Insert(0, detachInspector);
        }

        return content;
    }

    private void OpenDetachedConsoleWindow()
    {
        if (_detachedConsoleWindow is not null)
        {
            _detachedConsoleWindow.Activate();
            return;
        }

        var consoleWindow = new Window
        {
            Title = "Pompo Console",
            Width = 640,
            Height = 520,
            MinWidth = 520,
            MinHeight = 420,
            DataContext = _viewModel,
            Content = new Border
            {
                Background = AppBackground,
                Padding = new global::Avalonia.Thickness(12),
                Child = Card("Console", ConsolePanel(allowDetach: false))
            }
        };
        consoleWindow.Closed += (_, _) => _detachedConsoleWindow = null;
        _detachedConsoleWindow = consoleWindow;
        consoleWindow.Show(this);
    }

    private Control ConsolePanel(bool allowDetach)
    {
        var content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                BoundText("Summary.DiagnosticCount", "No diagnostics loaded"),
                BoundText("Summary.BrokenAssetCount", "No resource diagnostics loaded"),
                DiagnosticList("Diagnostics")
            }
        };

        if (!allowDetach)
        {
            return content;
        }

        var detachConsole = CommandButton("Detach Console");
        detachConsole.Click += (_, _) => OpenDetachedConsoleWindow();
        content.Children.Insert(0, detachConsole);
        return content;
    }

    private static Control GraphPropertyHintRow(GraphNodePropertyViewItem? property)
    {
        if (property is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        var required = property.IsRequired ? "required" : "optional";
        var defaultValue = string.IsNullOrWhiteSpace(property.DefaultValueJson)
            ? string.Empty
            : $" default {property.DefaultValueJson}";
        var aliases = string.IsNullOrWhiteSpace(property.AlternativeNames)
            ? string.Empty
            : $" aliases {property.AlternativeNames}";
        return new TextBlock
        {
            Text = $"{property.Name} ({property.ValueType}, {required}){defaultValue}{aliases} - {property.Description}",
            Foreground = MutedText,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static Control CreateGraphDiagnosticColumn()
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                BoundText("GraphEditor.IsValid", "No graph validation state"),
                DiagnosticList("GraphEditor.Diagnostics")
            }
        };
        Grid.SetColumn(panel, 1);
        return panel;
    }

    private static Control GraphNodeRow(GraphNodeViewItem? node)
    {
        if (node is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        return new Border
        {
            Padding = new global::Avalonia.Thickness(8),
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = node.NodeId,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = $"{node.Kind} · ({node.X:0}, {node.Y:0})",
                        Foreground = MutedText,
                        FontSize = 12
                    }
                }
            }
        };
    }

    private static Control GraphEdgeRow(GraphEdgeViewItem? edge)
    {
        if (edge is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        return new TextBlock
        {
            Text = $"{edge.FromNodeId}.{edge.FromPortId} -> {edge.ToNodeId}.{edge.ToPortId}",
            Foreground = MutedText,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static Control SaveSlotRow(SaveSlotViewItem? slot)
    {
        if (slot is null)
        {
            return new TextBlock();
        }

        return new Border
        {
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Padding = new global::Avalonia.Thickness(8),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{slot.DisplayName} ({slot.SlotId})",
                        FontWeight = FontWeight.Bold
                    },
                    new TextBlock
                    {
                        Text = $"{slot.Location}  |  {slot.SavedAtLocal}",
                        Foreground = MutedText,
                        FontSize = 12
                    }
                }
            }
        };
    }

    private static Control RecentProjectRow(RecentProjectViewItem? project)
    {
        if (project is null)
        {
            return new TextBlock();
        }

        return new Border
        {
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Padding = new global::Avalonia.Thickness(8),
            Background = project.IsSelected ? Brush.Parse("#e8f0ff") : Brushes.Transparent,
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = project.IsMissing ? $"{project.ProjectName} (missing)" : project.ProjectName,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = project.ProjectRoot,
                        Foreground = MutedText,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = project.LastOpenedLocal,
                        Foreground = MutedText,
                        FontSize = 12
                    }
                }
            }
        };
    }

    private void OpenDetachedResourceWindow()
    {
        if (_detachedResourceWindow is not null)
        {
            _detachedResourceWindow.Activate();
            return;
        }

        var resourceWindow = new Window
        {
            Title = "Pompo Project",
            Width = 640,
            Height = 760,
            MinWidth = 520,
            MinHeight = 520,
            DataContext = _viewModel,
            Content = new Border
            {
                Background = AppBackground,
                Padding = new global::Avalonia.Thickness(12),
                Child = Card(
                    "Project",
                    new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = ResourceBrowserPanel(allowDetach: false)
                    })
            }
        };
        resourceWindow.Closed += (_, _) => _detachedResourceWindow = null;
        _detachedResourceWindow = resourceWindow;
        resourceWindow.Show(this);
    }

    private Control ResourceBrowserPanel(bool allowDetach)
    {
        var importAsset = CommandButton("Import Asset");
        importAsset.Click += async (_, _) => await ImportAssetFromFileAsync();
        var deleteAsset = CommandButton("Delete Asset");
        deleteAsset.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.DeleteSelectedAssetAsync());

        var search = new TextBox
        {
            PlaceholderText = "Search assets",
            MinHeight = 34
        };
        search.Bind(
            TextBox.TextProperty,
            new Binding("ResourceBrowser.Query")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        var typeFilter = new ComboBox
        {
            MinHeight = 34
        };
        typeFilter.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("ResourceBrowser.TypeFilterOptions")
            {
                FallbackValue = Array.Empty<string>()
            });
        typeFilter.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("ResourceBrowser.SelectedTypeFilter")
            {
                Mode = BindingMode.TwoWay
            });

        var brokenOnly = new CheckBox
        {
            Content = "Broken only"
        };
        brokenOnly.Bind(
            ToggleButton.IsCheckedProperty,
            new Binding("ResourceBrowser.ShowOnlyBroken")
            {
                Mode = BindingMode.TwoWay
            });

        var unusedOnly = new CheckBox
        {
            Content = "Unused only"
        };
        unusedOnly.Bind(
            ToggleButton.IsCheckedProperty,
            new Binding("ResourceBrowser.ShowOnlyUnused")
            {
                Mode = BindingMode.TwoWay
            });

        var resources = new ListBox
        {
            MinHeight = 320,
            ItemTemplate = new FuncDataTemplate<ResourceItem>((resource, _) => ResourceRow(resource))
        };
        resources.SelectionChanged += (_, _) =>
        {
            if (resources.SelectedItem is ResourceItem resource)
            {
                _viewModel.ResourceBrowser.SelectedResourceId = resource.AssetId;
            }
        };
        resources.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("ResourceBrowser.FilteredResources")
            {
                FallbackValue = Array.Empty<ResourceItem>()
            });

        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                importAsset,
                deleteAsset
            }
        };
        if (allowDetach)
        {
            var detachProject = CommandButton("Detach Project");
            detachProject.Click += (_, _) => OpenDetachedResourceWindow();
            commands.Children.Add(detachProject);
        }

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                commands,
                search,
                typeFilter,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        brokenOnly,
                        unusedOnly
                    }
                },
                MetricRow("Assets", "Summary.AssetCount"),
                MetricRow("Scenes", "Summary.SceneCount"),
                MetricRow("Characters", "Summary.CharacterCount"),
                MetricRow("Graphs", "Summary.GraphCount"),
                resources,
                new TextBlock { Text = "Selected Asset", Foreground = MutedText },
                BoundText("ResourceBrowser.SelectedResource.AssetId", "No asset selected", FontWeight.Bold),
                BoundText("ResourceBrowser.SelectedResource.Type", "Type unavailable"),
                BoundText("ResourceBrowser.SelectedResource.SourcePath", "Path unavailable"),
                BoundText("ResourceBrowser.SelectedResource.Hash", "Hash unavailable")
            }
        };
    }

    private static Control ResourceRow(ResourceItem? resource)
    {
        if (resource is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        var status = resource.IsMissing
            ? "Missing"
            : resource.IsHashMismatch
                ? "Hash mismatch"
                : resource.IsUnused
                    ? "Unused"
                    : "Ready";

        return new Border
        {
            Padding = new global::Avalonia.Thickness(8),
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = resource.AssetId,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = $"{resource.Type} · {status}",
                        Foreground = resource.IsBroken ? Brush.Parse("#b91c1c") : MutedText,
                        FontSize = 12
                    },
                    new TextBlock
                    {
                        Text = resource.SourcePath,
                        Foreground = MutedText,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
    }

    private async Task CreateProjectFromFolderAsync(bool sampleTemplate)
    {
        var folder = await PickFolderAsync(sampleTemplate
            ? "Create Pompo sample project in folder"
            : "Create Pompo minimal project in folder");
        if (folder is null)
        {
            return;
        }

        var projectName = new DirectoryInfo(folder).Name;
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = "PompoProject";
        }

        await RunEditorActionAsync(() => sampleTemplate
            ? _viewModel.CreateSampleProjectAsync(folder, projectName)
            : _viewModel.CreateMinimalProjectAsync(folder, projectName));
    }

    private async Task OpenProjectFromFolderAsync()
    {
        var folder = await PickFolderAsync("Open Pompo project folder");
        if (folder is null)
        {
            return;
        }

        await RunEditorActionAsync(() => _viewModel.LoadAsync(folder));
    }

    private async Task BuildProjectToFolderAsync()
    {
        var folder = await PickFolderAsync("Select build output folder");
        if (folder is null)
        {
            return;
        }

        await RunEditorActionAsync(() => _viewModel.BuildSelectedProfileAsync(folder));
    }

    private async Task BuildVerifiedReleaseToFolderAsync()
    {
        var buildFolder = await PickFolderAsync("Select build output folder");
        if (buildFolder is null)
        {
            return;
        }

        var releaseFolder = await PickFolderAsync("Select release output folder");
        if (releaseFolder is null)
        {
            return;
        }

        await RunEditorActionAsync(() => _viewModel.BuildAndPackageSelectedProfileAsync(buildFolder, releaseFolder));
    }

    private async Task PackageReleaseToFolderAsync()
    {
        var folder = await PickFolderAsync("Select release output folder");
        if (folder is null)
        {
            return;
        }

        await RunEditorActionAsync(() => _viewModel.PackageLastBuildAsync(folder));
    }

    private async Task ImportAssetFromFileAsync()
    {
        var file = await PickFileAsync("Import asset");
        if (file is null)
        {
            return;
        }

        await RunEditorActionAsync(() => _viewModel.ImportAssetAsync(file));
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel?.StorageProvider.CanPickFolder != true)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    private async Task<string?> PickFileAsync(string title)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel?.StorageProvider.CanOpen != true)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private async Task RunEditorActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
        }
    }

    private void OpenDetachedPreviewWindow()
    {
        if (_detachedPreviewWindow is not null)
        {
            _detachedPreviewWindow.Activate();
            return;
        }

        var previewWindow = new Window
        {
            Title = "Pompo Preview",
            Width = 960,
            Height = 640,
            MinWidth = 720,
            MinHeight = 520,
            DataContext = _viewModel,
            Content = new Border
            {
                Background = AppBackground,
                Padding = new global::Avalonia.Thickness(12),
                Child = PreviewCanvas(allowDetach: false)
            }
        };
        previewWindow.Closed += (_, _) => _detachedPreviewWindow = null;
        _detachedPreviewWindow = previewWindow;
        previewWindow.Show(this);
    }

    private Control PreviewCanvas(bool allowDetach)
    {
        var runPreview = PrimaryButton("Run Preview");
        runPreview.Click += async (_, _) => await RunEditorActionAsync(() => _viewModel.RunCurrentGraphPreviewAsync());
        var detachPreview = CommandButton("Detach Preview");
        detachPreview.Click += (_, _) => OpenDetachedPreviewWindow();

        var localeSelector = new ComboBox
        {
            MinWidth = 92,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        localeSelector.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("PreviewLocaleOptions")
            {
                FallbackValue = Array.Empty<string>()
            });
        localeSelector.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("SelectedPreviewLocale")
            {
                Mode = BindingMode.TwoWay
            });

        var events = new ListBox
        {
            MinHeight = 140,
            Background = Brushes.Transparent,
            ItemTemplate = new FuncDataTemplate<GraphPreviewEventItem>((item, _) => PreviewEventRow(item))
        };
        events.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("Preview.Events")
            {
                FallbackValue = Array.Empty<GraphPreviewEventItem>()
            });

        var summary = new TextBlock
        {
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        summary.Bind(
            TextBlock.TextProperty,
            new Binding("Preview.Summary")
            {
                FallbackValue = "Run preview to execute the current graph IR.",
                TargetNullValue = "Run preview to execute the current graph IR."
            });

        var variables = new TextBlock
        {
            Foreground = Brush.Parse("#cbd5e1"),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        variables.Bind(
            TextBlock.TextProperty,
            new Binding("Preview.VariablesSummary")
            {
                FallbackValue = "No variables",
                TargetNullValue = "No variables"
            });

        var audio = new TextBlock
        {
            Foreground = Brush.Parse("#9ca3af"),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        audio.Bind(
            TextBlock.TextProperty,
            new Binding("Preview.AudioSummary")
            {
                FallbackValue = "BGM: none; SFX: none",
                TargetNullValue = "BGM: none; SFX: none"
            });

        var header = new Grid
        {
            ColumnDefinitions = allowDetach
                ? new ColumnDefinitions("*,Auto,Auto,Auto")
                : new ColumnDefinitions("*,Auto,Auto"),
            Margin = new global::Avalonia.Thickness(16),
            Children =
            {
                summary,
                localeSelector,
                runPreview
            }
        };
        Grid.SetColumn(localeSelector, 1);
        if (allowDetach)
        {
            header.Children.Add(detachPreview);
            Grid.SetColumn(detachPreview, 2);
            Grid.SetColumn(runPreview, 3);
        }
        else
        {
            Grid.SetColumn(runPreview, 2);
        }

        var center = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "1920 x 1080 IR Preview",
                    Foreground = Brushes.White,
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                variables,
                audio
            }
        };
        Grid.SetRow(center, 1);

        return new Border
        {
            Background = Brush.Parse("#161b22"),
            BorderBrush = Brush.Parse("#30363d"),
            BorderThickness = new global::Avalonia.Thickness(1),
            MinHeight = 420,
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*,170"),
                Children =
                {
                    header,
                    center,
                    DialoguePreview(events)
                }
            }
        };
    }

    private static Control PreviewEventRow(GraphPreviewEventItem? item)
    {
        if (item is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        return new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = $"{item.Kind}: {item.Text}",
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = item.Detail,
                    Foreground = Brush.Parse("#9ca3af"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
    }

    private static Control DialoguePreview(Control events)
    {
        var panel = new Border
        {
            Background = Brush.Parse("#0b0f16cc"),
            Margin = new global::Avalonia.Thickness(42, 0, 42, 24),
            Padding = new global::Avalonia.Thickness(18),
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Trace", Foreground = Brushes.White, FontWeight = FontWeight.Bold },
                    events
                }
            }
        };

        Grid.SetRow(panel, 2);
        return panel;
    }

    private static Control Card(string title, Control body)
    {
        return new Border
        {
            Margin = new global::Avalonia.Thickness(6),
            Padding = new global::Avalonia.Thickness(16),
            BorderBrush = BorderColor,
            BorderThickness = new global::Avalonia.Thickness(1),
            Background = PanelBackground,
            CornerRadius = new global::Avalonia.CornerRadius(8),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = title, FontWeight = FontWeight.Bold, FontSize = 16 },
                    body
                }
            }
        };
    }

    private static Control ListPanel(params string[] lines)
    {
        var stack = new StackPanel { Spacing = 8 };
        foreach (var line in lines)
        {
            stack.Children.Add(new TextBlock
            {
                Text = line,
                Foreground = MutedText,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return stack;
    }

    private static Control MetricRow(string label, string bindingPath)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        var labelText = new TextBlock
        {
            Text = label,
            Foreground = MutedText
        };
        Grid.SetColumn(labelText, 0);
        grid.Children.Add(labelText);

        var valueText = BoundText(bindingPath, "-");
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);
        return grid;
    }

    private static TextBlock BoundText(string path, string fallback, FontWeight? fontWeight = null)
    {
        var text = new TextBlock
        {
            Foreground = MutedText,
            TextWrapping = TextWrapping.Wrap
        };
        if (fontWeight is not null)
        {
            text.FontWeight = fontWeight.Value;
        }

        text.Bind(
            TextBlock.TextProperty,
            new Binding(path)
            {
                FallbackValue = fallback,
                TargetNullValue = fallback
            });
        return text;
    }

    private static Button CommandButton(string text)
    {
        return new Button
        {
            Content = text,
            MinHeight = 34,
            Padding = new global::Avalonia.Thickness(14, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static Button PrimaryButton(string text)
    {
        return new Button
        {
            Content = text,
            Background = Accent,
            Foreground = Brushes.White,
            MinHeight = 34,
            Padding = new global::Avalonia.Thickness(16, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static Control StatusPill(string text)
    {
        return new Border
        {
            Background = Brush.Parse("#dbeafe"),
            CornerRadius = new global::Avalonia.CornerRadius(999),
            Padding = new global::Avalonia.Thickness(10, 4),
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brush.Parse("#1e3a8a"),
                FontSize = 12
            }
        };
    }

    private static Control AddPanel(
        Grid grid,
        string title,
        Control body,
        int column,
        int row,
        int columnSpan = 1,
        int rowSpan = 1)
    {
        var panel = Card(title, body);
        Grid.SetColumn(panel, column);
        Grid.SetRow(panel, row);
        Grid.SetColumnSpan(panel, columnSpan);
        Grid.SetRowSpan(panel, rowSpan);
        grid.Children.Add(panel);
        return panel;
    }
}
