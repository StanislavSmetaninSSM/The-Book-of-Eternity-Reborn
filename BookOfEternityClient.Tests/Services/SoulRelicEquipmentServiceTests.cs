using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests.Services;

public sealed class SoulRelicEquipmentServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public SoulRelicEquipmentServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-soul-relic-equipment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_rootPath, recursive: true); } catch { }
    }

    private async Task<FileSystemManager> SeedAsync(IEnumerable<string> storedIds, IEnumerable<(string Id, string Slot)> equipped)
    {
        var stored = storedIds.Select(id => (object)new
        {
            relicId = id,
            name = $"Relic {id}",
            rarity = "rare"
        }).ToArray();
        var equippedList = equipped.Select(e => (object)new
        {
            relicId = e.Id,
            name = $"Relic {e.Id}",
            rarity = "rare",
            gameplayStatus = new { equipped = true, currentSlot = e.Slot }
        }).ToArray();
        var payload = new
        {
            soulRelics = new
            {
                stored,
                equipped = equippedList
            }
        };
        await _fs.WriteFileAtomicAsync(
            SoulRelicEquipmentService.SoulStatePath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return _fs;
    }

    [Fact]
    public async Task ReadContextAsync_LoadsStoredAndEquippedRelics()
    {
        var fs = await SeedAsync(
            storedIds: new[] { "r1" },
            equipped: new[] { ("r2", "body") });

        var ctx = await SoulRelicEquipmentService.ReadContextAsync(fs);

        Assert.NotNull(ctx);
        Assert.Single(ctx.Stored);
        Assert.Single(ctx.Equipped);
        Assert.Equal("r1", ctx.Stored[0].RelicId);
        Assert.Equal("r2", ctx.Equipped[0].RelicId);
        Assert.Equal("body", ctx.Equipped[0].CurrentSlot);
    }

    [Fact]
    public async Task EquipAsync_MovesRelicFromStoredToEquipped()
    {
        var fs = await SeedAsync(storedIds: new[] { "r1" }, equipped: Array.Empty<(string, string)>());

        var outcome = await SoulRelicEquipmentService.EquipAsync(fs, "r1", "mainHand");

        Assert.True(outcome.Success, outcome.Message);
        var ctx = await SoulRelicEquipmentService.ReadContextAsync(fs);
        Assert.Empty(ctx.Stored);
        Assert.Single(ctx.Equipped);
        Assert.Equal("r1", ctx.Equipped[0].RelicId);
        Assert.True(ctx.Equipped[0].IsEquipped);
        Assert.Equal("mainHand", ctx.Equipped[0].CurrentSlot);
    }

    [Fact]
    public async Task EquipAsync_WhenRelicNotInStored_ReturnsFailure()
    {
        var fs = await SeedAsync(storedIds: Array.Empty<string>(), equipped: new[] { ("r1", "head") });

        var outcome = await SoulRelicEquipmentService.EquipAsync(fs, "missing", "mainHand");

        Assert.False(outcome.Success);
        Assert.Contains("не найдена", outcome.Message);
    }

    [Fact]
    public async Task EquipAsync_WhenAlreadyEquipped_ReturnsFailure()
    {
        var fs = await SeedAsync(storedIds: Array.Empty<string>(), equipped: new[] { ("r1", "head") });

        var outcome = await SoulRelicEquipmentService.EquipAsync(fs, "r1", "mainHand");

        Assert.False(outcome.Success);
        Assert.Contains("уже экипирована", outcome.Message);
    }

    [Fact]
    public async Task UnequipAsync_MovesRelicFromEquippedToStored()
    {
        var fs = await SeedAsync(storedIds: Array.Empty<string>(), equipped: new[] { ("r1", "head") });

        var outcome = await SoulRelicEquipmentService.UnequipAsync(fs, "head");

        Assert.True(outcome.Success, outcome.Message);
        var ctx = await SoulRelicEquipmentService.ReadContextAsync(fs);
        Assert.Empty(ctx.Equipped);
        Assert.Single(ctx.Stored);
        Assert.Equal("r1", ctx.Stored[0].RelicId);
        Assert.False(ctx.Stored[0].IsEquipped);
    }

    [Fact]
    public async Task UnequipAsync_WhenSlotEmpty_ReturnsFailure()
    {
        var fs = await SeedAsync(storedIds: new[] { "r1" }, equipped: Array.Empty<(string, string)>());

        var outcome = await SoulRelicEquipmentService.UnequipAsync(fs, "head");

        Assert.False(outcome.Success);
        Assert.Contains("нет экипированной реликвии", outcome.Message);
    }

    [Fact]
    public async Task UnequipAsync_WhenInvalidSlot_ReturnsFailure()
    {
        var fs = await SeedAsync(storedIds: Array.Empty<string>(), equipped: new[] { ("r1", "head") });

        var outcome = await SoulRelicEquipmentService.UnequipAsync(fs, "wing");

        Assert.False(outcome.Success);
        Assert.Contains("слот", outcome.Message);
    }

    [Fact]
    public async Task ReadContextAsync_NormalizesLegacyFlatArray()
    {
        var payload = new
        {
            soulRelics = new object[]
            {
                new { relicId = "r1", name = "A", rarity = "rare", gameplayStatus = new { equipped = false } },
                new { relicId = "r2", name = "B", rarity = "rare", gameplayStatus = new { equipped = true, currentSlot = "head" } }
            }
        };
        await _fs.WriteFileAtomicAsync(
            SoulRelicEquipmentService.SoulStatePath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        var ctx = await SoulRelicEquipmentService.ReadContextAsync(_fs);

        Assert.NotNull(ctx);
        Assert.Single(ctx.Stored);
        Assert.Single(ctx.Equipped);
        Assert.Equal("r1", ctx.Stored[0].RelicId);
        Assert.Equal("r2", ctx.Equipped[0].RelicId);
        Assert.Equal("head", ctx.Equipped[0].CurrentSlot);
    }

    [Fact]
    public async Task ReadContextAsync_MissingFile_ReturnsNull()
    {
        var ctx = await SoulRelicEquipmentService.ReadContextAsync(_fs);
        Assert.Null(ctx);
    }

    [Fact]
    public async Task EquipAsync_PreservesAllRelicFields()
    {
        var payload = new
        {
            soulRelics = new
            {
                stored = new object[]
                {
                    new
                    {
                        relicId = "r1",
                        name = "Огненный Шар",
                        rarity = "rare",
                        gameplayStatus = new { equipped = false, currentSlot = (string?)null }
                    }
                },
                equipped = Array.Empty<object>()
            }
        };
        await _fs.WriteFileAtomicAsync(
            SoulRelicEquipmentService.SoulStatePath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        await SoulRelicEquipmentService.EquipAsync(_fs, "r1", "mainHand");

        var ctx = await SoulRelicEquipmentService.ReadContextAsync(_fs);
        Assert.Single(ctx.Equipped);
        Assert.Equal("rare", ctx.Equipped[0].Rarity);
        Assert.Equal("Огненный Шар", ctx.Equipped[0].Name);
    }
}
