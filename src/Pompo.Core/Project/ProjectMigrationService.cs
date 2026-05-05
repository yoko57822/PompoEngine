namespace Pompo.Core.Project;

public sealed record ProjectMigrationResult(
    PompoProjectDocument Document,
    IReadOnlyList<string> AppliedMigrations);

public sealed class ProjectMigrationService
{
    public ProjectMigrationResult Migrate(PompoProjectDocument document)
    {
        if (document.SchemaVersion > ProjectConstants.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Project schema {document.SchemaVersion} is newer than supported schema {ProjectConstants.CurrentSchemaVersion}.");
        }

        var migrations = new List<string>();
        var migrated = document;

        if (migrated.SchemaVersion < 1)
        {
            migrated = migrated with
            {
                SchemaVersion = 1,
                EngineVersion = string.IsNullOrWhiteSpace(migrated.EngineVersion) ? "0.1.0" : migrated.EngineVersion,
                VirtualWidth = migrated.VirtualWidth <= 0 ? 1920 : migrated.VirtualWidth,
                VirtualHeight = migrated.VirtualHeight <= 0 ? 1080 : migrated.VirtualHeight,
                SupportedLocales = migrated.SupportedLocales.Count == 0 ? ["ko", "en"] : migrated.SupportedLocales
            };
            migrations.Add("schema-0-to-1");
        }

        if (migrated.VirtualWidth <= 0 || migrated.VirtualHeight <= 0)
        {
            migrated = migrated with
            {
                VirtualWidth = migrated.VirtualWidth <= 0 ? 1920 : migrated.VirtualWidth,
                VirtualHeight = migrated.VirtualHeight <= 0 ? 1080 : migrated.VirtualHeight
            };
            migrations.Add("normalize-virtual-size");
        }

        if (migrated.SupportedLocales.Count == 0)
        {
            migrated = migrated with { SupportedLocales = ["ko", "en"] };
            migrations.Add("normalize-locales");
        }

        if (migrated.SchemaVersion < 2)
        {
            migrated = migrated with
            {
                SchemaVersion = 2,
                RuntimeUiTheme = migrated.RuntimeUiTheme ?? new PompoRuntimeUiTheme()
            };
            migrations.Add("schema-1-to-2-runtime-ui-theme");
        }

        if (migrated.SchemaVersion < 3)
        {
            migrated = migrated with
            {
                SchemaVersion = 3,
                RuntimeUiTheme = migrated.RuntimeUiTheme ?? new PompoRuntimeUiTheme(),
                RuntimeUiSkin = migrated.RuntimeUiSkin ?? new PompoRuntimeUiSkin()
            };
            migrations.Add("schema-2-to-3-runtime-ui-skin");
        }

        if (migrated.SchemaVersion < 4)
        {
            migrated = migrated with
            {
                SchemaVersion = 4,
                RuntimeUiTheme = migrated.RuntimeUiTheme ?? new PompoRuntimeUiTheme(),
                RuntimeUiSkin = migrated.RuntimeUiSkin ?? new PompoRuntimeUiSkin(),
                RuntimeUiLayout = migrated.RuntimeUiLayout ?? new PompoRuntimeUiLayoutSettings()
            };
            migrations.Add("schema-3-to-4-runtime-ui-layout");
        }

        if (migrated.SchemaVersion < 5)
        {
            migrated = migrated with
            {
                SchemaVersion = 5,
                RuntimeUiTheme = migrated.RuntimeUiTheme ?? new PompoRuntimeUiTheme(),
                RuntimeUiSkin = migrated.RuntimeUiSkin ?? new PompoRuntimeUiSkin(),
                RuntimeUiLayout = migrated.RuntimeUiLayout ?? new PompoRuntimeUiLayoutSettings(),
                RuntimeUiAnimation = migrated.RuntimeUiAnimation ?? new PompoRuntimeUiAnimationSettings()
            };
            migrations.Add("schema-4-to-5-runtime-ui-animation");
        }

        if (migrated.SchemaVersion < 6)
        {
            migrated = migrated with
            {
                SchemaVersion = 6,
                RuntimeUiTheme = migrated.RuntimeUiTheme ?? new PompoRuntimeUiTheme(),
                RuntimeUiSkin = migrated.RuntimeUiSkin ?? new PompoRuntimeUiSkin(),
                RuntimeUiLayout = migrated.RuntimeUiLayout ?? new PompoRuntimeUiLayoutSettings(),
                RuntimeUiAnimation = migrated.RuntimeUiAnimation ?? new PompoRuntimeUiAnimationSettings(),
                RuntimePlayback = migrated.RuntimePlayback ?? new PompoRuntimePlaybackSettings()
            };
            migrations.Add("schema-5-to-6-runtime-playback");
        }

        if (migrated.SchemaVersion < 7)
        {
            migrated = migrated with
            {
                SchemaVersion = 7,
                RuntimeUiTheme = migrated.RuntimeUiTheme ?? new PompoRuntimeUiTheme(),
                RuntimeUiSkin = migrated.RuntimeUiSkin ?? new PompoRuntimeUiSkin(),
                RuntimeUiLayout = migrated.RuntimeUiLayout ?? new PompoRuntimeUiLayoutSettings(),
                RuntimeUiAnimation = migrated.RuntimeUiAnimation ?? new PompoRuntimeUiAnimationSettings(),
                RuntimePlayback = migrated.RuntimePlayback ?? new PompoRuntimePlaybackSettings()
            };
            migrations.Add("schema-6-to-7-disabled-choice-skin");
        }

        if (migrated.RuntimeUiTheme is null ||
            migrated.RuntimeUiSkin is null ||
            migrated.RuntimeUiLayout is null ||
            migrated.RuntimeUiAnimation is null ||
            migrated.RuntimePlayback is null)
        {
            migrated = migrated with
            {
                RuntimeUiTheme = migrated.RuntimeUiTheme ?? new PompoRuntimeUiTheme(),
                RuntimeUiSkin = migrated.RuntimeUiSkin ?? new PompoRuntimeUiSkin(),
                RuntimeUiLayout = migrated.RuntimeUiLayout ?? new PompoRuntimeUiLayoutSettings(),
                RuntimeUiAnimation = migrated.RuntimeUiAnimation ?? new PompoRuntimeUiAnimationSettings(),
                RuntimePlayback = migrated.RuntimePlayback ?? new PompoRuntimePlaybackSettings()
            };
            migrations.Add("normalize-runtime-appearance-playback");
        }

        if (migrated.SchemaVersion != ProjectConstants.CurrentSchemaVersion)
        {
            migrated = migrated with { SchemaVersion = ProjectConstants.CurrentSchemaVersion };
            migrations.Add("normalize-schema-version");
        }

        return new ProjectMigrationResult(migrated, migrations);
    }
}
