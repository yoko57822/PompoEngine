using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pompo.Core.Assets;

namespace Pompo.Editor.Avalonia.ViewModels;

public sealed record ResourceItem(
    string AssetId,
    string SourcePath,
    PompoAssetType Type,
    string Hash,
    int UsageCount,
    bool IsMissing,
    bool IsHashMismatch)
{
    public bool IsBroken => IsMissing || IsHashMismatch;
    public bool IsUnused => UsageCount == 0;
}

public sealed class ResourceBrowserViewModel : INotifyPropertyChanged
{
    private const string AllTypesFilter = "All";
    private string _query = string.Empty;
    private PompoAssetType? _typeFilter;
    private bool _showOnlyBroken;
    private bool _showOnlyUnused;
    private string? _selectedResourceId;

    public ResourceBrowserViewModel(IReadOnlyList<ResourceItem> resources)
    {
        Resources = resources;
        _selectedResourceId = resources
            .OrderBy(resource => resource.Type)
            .ThenBy(resource => resource.AssetId, StringComparer.Ordinal)
            .FirstOrDefault()?.AssetId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<ResourceItem> Resources { get; }

    public IReadOnlyList<string> TypeFilterOptions { get; } =
    [
        AllTypesFilter,
        PompoAssetType.Image.ToString(),
        PompoAssetType.Audio.ToString(),
        PompoAssetType.Font.ToString(),
        PompoAssetType.Script.ToString(),
        PompoAssetType.Data.ToString()
    ];

    public string Query
    {
        get => _query;
        set
        {
            if (_query == value)
            {
                return;
            }

            _query = value;
            NotifyFilterChanged();
        }
    }

    public PompoAssetType? TypeFilter
    {
        get => _typeFilter;
        set
        {
            if (_typeFilter == value)
            {
                return;
            }

            _typeFilter = value;
            NotifyFilterChanged();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTypeFilter)));
        }
    }

    public string SelectedTypeFilter
    {
        get => TypeFilter?.ToString() ?? AllTypesFilter;
        set
        {
            TypeFilter = string.Equals(value, AllTypesFilter, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(value)
                    ? null
                    : Enum.Parse<PompoAssetType>(value, ignoreCase: true);
        }
    }

    public bool ShowOnlyBroken
    {
        get => _showOnlyBroken;
        set
        {
            if (_showOnlyBroken == value)
            {
                return;
            }

            _showOnlyBroken = value;
            NotifyFilterChanged();
        }
    }

    public bool ShowOnlyUnused
    {
        get => _showOnlyUnused;
        set
        {
            if (_showOnlyUnused == value)
            {
                return;
            }

            _showOnlyUnused = value;
            NotifyFilterChanged();
        }
    }

    public IReadOnlyList<ResourceItem> FilteredResources => Resources
        .Where(Matches)
        .OrderBy(resource => resource.Type)
        .ThenBy(resource => resource.AssetId, StringComparer.Ordinal)
        .ToArray();

    public string? SelectedResourceId
    {
        get => _selectedResourceId;
        set
        {
            if (_selectedResourceId == value)
            {
                return;
            }

            _selectedResourceId = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedResourceId)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedResource)));
        }
    }

    public ResourceItem? SelectedResource => SelectedResourceId is null
        ? null
        : Resources.FirstOrDefault(resource => string.Equals(resource.AssetId, SelectedResourceId, StringComparison.Ordinal));

    private bool Matches(ResourceItem resource)
    {
        if (TypeFilter is not null && resource.Type != TypeFilter)
        {
            return false;
        }

        if (ShowOnlyBroken && !resource.IsBroken)
        {
            return false;
        }

        if (ShowOnlyUnused && !resource.IsUnused)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(Query) ||
            resource.AssetId.Contains(Query, StringComparison.OrdinalIgnoreCase) ||
            resource.SourcePath.Contains(Query, StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyFilterChanged([CallerMemberName] string? propertyName = null)
    {
        EnsureSelectedResourceVisible();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilteredResources)));
    }

    private void EnsureSelectedResourceVisible()
    {
        if (SelectedResourceId is not null &&
            FilteredResources.Any(resource => string.Equals(resource.AssetId, SelectedResourceId, StringComparison.Ordinal)))
        {
            return;
        }

        var nextSelected = FilteredResources.FirstOrDefault()?.AssetId;
        if (string.Equals(SelectedResourceId, nextSelected, StringComparison.Ordinal))
        {
            return;
        }

        _selectedResourceId = nextSelected;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedResourceId)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedResource)));
    }
}
