namespace Pompo.Core;

public static class ProjectConstants
{
    public const int CurrentSchemaVersion = 7;
    public const string ProjectFileName = "project.pompo.json";

    public static readonly string[] RequiredFolders =
    [
        "Assets/Images",
        "Assets/Audio",
        "Assets/Fonts",
        "Scenes",
        "Characters",
        "Graphs",
        "Scripts",
        "BuildProfiles",
        "Settings"
    ];
}
