using Pompo.Core;
using Pompo.Core.Runtime;

namespace Pompo.Tests;

public sealed class RuntimeSaveStoreTests
{
    [Fact]
    public async Task SaveLoadAndList_RoundTripsRuntimeSaveData()
    {
        var root = CreateTempDirectory();
        var store = new RuntimeSaveStore();
        var data = CreateSaveData("graph_intro", "node_1");

        await store.SaveAsync(root, "manual_1", "Manual 1", data);
        var loaded = await store.LoadAsync(root, "manual_1");
        var slots = await store.ListAsync(root);

        Assert.Equal("manual_1", loaded.Metadata.SlotId);
        Assert.Equal("Manual 1", loaded.Metadata.DisplayName);
        Assert.Equal("graph_intro", loaded.Data.GraphId);
        Assert.Equal("node_1", loaded.Data.NodeId);
        Assert.Equal(42, loaded.Data.Variables["score"]);
        Assert.Single(slots);
        Assert.Equal("manual_1", slots[0].SlotId);
        Assert.True(File.Exists(RuntimeSaveStore.GetSlotPath(root, "manual_1")));
    }

    [Fact]
    public async Task SaveAsync_ReplacesExistingSlotAtomically()
    {
        var root = CreateTempDirectory();
        var store = new RuntimeSaveStore();

        await store.SaveAsync(root, "quick", "Quick", CreateSaveData("g1", "n1"));
        await store.SaveAsync(root, "quick", "Quick", CreateSaveData("g2", "n2"));

        var loaded = await store.LoadAsync(root, "quick");
        Assert.Equal("g2", loaded.Data.GraphId);
        Assert.Equal("n2", loaded.Data.NodeId);
        Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesSlot()
    {
        var root = CreateTempDirectory();
        var store = new RuntimeSaveStore();
        await store.SaveAsync(root, "auto", "Auto", CreateSaveData("g", "n"));

        await store.DeleteAsync(root, "auto");

        Assert.False(File.Exists(RuntimeSaveStore.GetSlotPath(root, "auto")));
        Assert.Empty(await store.ListAsync(root));
    }

    [Fact]
    public async Task ListAsync_SkipsCorruptSaveFiles()
    {
        var root = CreateTempDirectory();
        var store = new RuntimeSaveStore();
        await store.SaveAsync(root, "manual_1", "Manual 1", CreateSaveData("graph_intro", "node_1"));
        await File.WriteAllTextAsync(Path.Combine(root, "broken.pompo-save.json"), "{ this is not valid json");

        var slots = await store.ListAsync(root);

        var slot = Assert.Single(slots);
        Assert.Equal("manual_1", slot.SlotId);
    }

    [Fact]
    public async Task ListAsync_SkipsSaveFilesWithUnsafeMetadataSlotIds()
    {
        var root = CreateTempDirectory();
        var store = new RuntimeSaveStore();
        await store.SaveAsync(root, "manual_1", "Manual 1", CreateSaveData("graph_intro", "node_1"));
        var unsafeSave = new RuntimeSaveFile(
            new RuntimeSaveSlotMetadata("../outside", "Unsafe", DateTimeOffset.UtcNow, "g", "n"),
            CreateSaveData("g", "n"));
        await using (var stream = File.Create(Path.Combine(root, "unsafe.pompo-save.json")))
        {
            await System.Text.Json.JsonSerializer.SerializeAsync(
                stream,
                unsafeSave,
                Pompo.Core.Project.ProjectFileService.CreateJsonOptions());
        }

        var slots = await store.ListAsync(root);

        var slot = Assert.Single(slots);
        Assert.Equal("manual_1", slot.SlotId);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("bad slot")]
    [InlineData("slot.name")]
    public async Task SaveLoadAndDelete_RejectUnsafeSlotIds(string slotId)
    {
        var root = CreateTempDirectory();
        var store = new RuntimeSaveStore();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(root, slotId, "Unsafe", CreateSaveData("g", "n")));
        await Assert.ThrowsAsync<ArgumentException>(() => store.LoadAsync(root, slotId));
        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(root, slotId));
        Assert.Throws<ArgumentException>(() => RuntimeSaveStore.GetSlotPath(root, slotId));
    }

    private static RuntimeSaveData CreateSaveData(string graphId, string nodeId)
    {
        return new RuntimeSaveData(
            ProjectConstants.CurrentSchemaVersion,
            graphId,
            nodeId,
            [],
            new Dictionary<string, object?> { ["score"] = 42 },
            "bg",
            [new RuntimeCharacterState("hero", "smile", RuntimeLayer.Character, 0.5f, 0.9f, true)],
            new RuntimeAudioState("bgm", ["sfx"]),
            ["choice-a"]);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pompo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
