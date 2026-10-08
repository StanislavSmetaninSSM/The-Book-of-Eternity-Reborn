using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class QteSceneServiceTests
{
    [Theory]
    [InlineData("carrier")]
    [InlineData("refresh")]
    public async Task ActualRestorationUncertaintyRetainsQteDecisionWithoutRefresh(string observation)
    {
        const string target = "game_state/player/experience.json";
        var baseline = Encoding.UTF8.GetBytes("{\"totalExperience\":10}");
        await _fs.WriteFileAtomicBytesAsync(target, baseline);
        var reached = 0;
        var forwardPublished = 0;
        var laterReads = 0;
        var earlierReads = 0;
        byte[] unknown = [0xFF, 0x41];
        byte[]? journal = null;
        FileSystemManager? files = null;
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = _ =>
                {
                    if (reached > 0) laterReads++;
                    else earlierReads++;
                    return Task.CompletedTask;
                },
                LocalPublicationObserver = (phase, index) =>
                {
                    if (phase != TrustedLocalPublicationPhase.MemberPublished || reached > 0) return;
                    var journalPath = Path.Combine(files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
                    using var document = JsonDocument.Parse(File.ReadAllBytes(journalPath));
                    var member = document.RootElement.GetProperty("Members")[index];
                    if (member.GetProperty("Path").GetString() != files.ResolvePath(target)) return;
                    var bytes = File.ReadAllBytes(files.ResolvePath(target));
                    if (!bytes.SequenceEqual(baseline))
                    {
                        var jsonBytes = bytes.AsMemory();
                        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) jsonBytes = jsonBytes[3..];
                        using var experience = JsonDocument.Parse(jsonBytes);
                        var total = experience.RootElement.GetProperty("totalExperience").GetInt32();
                        if (total == 15) forwardPublished++;
                        return;
                    }
                    if (forwardPublished == 0) return;
                    reached++;
                    File.WriteAllBytes(files.ResolvePath(target), unknown);
                    journal = File.ReadAllBytes(journalPath);
                    throw new RestorationOutcomeCutFailure();
                }
            });
        var service = CreateRuntimeCapableService(files);
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_invalid", Title = "Invalid outcome", FinalNarrative = "Исход применён.",
            GmSummary = "Restoration outcome fixture.",
            ResponseFragment = new JsonObject
            {
                ["response"] = "Исход применён.", ["experienceGained"] = 5,
                ["playerCharacterNameChange"] = "Новая личность"
            }
        };
        var failure = await Record.ExceptionAsync(() => service.ApplyTerminalOutcomeValidatedStateChangesAsync(outcome));
        Assert.True(reached == 1, $"Restoration cut hits={reached}; forward publications={forwardPublished}; failure={failure}");
        Assert.True(earlierReads > 0);
        Assert.True(forwardPublished > 0);
        Assert.Equal(unknown, File.ReadAllBytes(files.ResolvePath(target)));
        Assert.Equal(journal, File.ReadAllBytes(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        Assert.NotEmpty(Directory.GetFiles(files.ResolvePath(QteNormalizerBackupDirectory), "*", SearchOption.AllDirectories));
        if (observation == "carrier")
        {
            var uncertain = Assert.IsType<CoordinatedStatePublicationUncertainException>(failure);
            var original = Assert.IsType<InvalidOperationException>(uncertain.Data["QteOperationFailure"]);
            Assert.Contains("Локальный QTE outcome нарушил контракт состояния", original.Message, StringComparison.Ordinal);
        }
        else Assert.Equal(0, laterReads);
    }

    [Fact]
    public async Task ActualPostRestorationMirrorPublicationRetainsQteUncertaintyAndPrimaryCause()
    {
        const string experiencePath = "game_state/player/experience.json";
        var experience = Encoding.UTF8.GetBytes("{\"totalExperience\":10}");
        await _fs.WriteFileAtomicBytesAsync(experiencePath, experience);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
            {"soulName":"Искра","currentRealm":"Chaos Sea","currentIncarnation":1,"inkFeathers":{"current":17,"total":17},
             "enlightenment":{"experience":0,"level":0},"afterlifeCombatProfile":{"spiritFocusTier":0,"artTiers":{"guard":0}}}
            """);
        var staleProfile = Encoding.UTF8.GetBytes("""
            {"schemaVersion":1,"profiles":[{"actorType":"player_soul","actorId":"player_soul","displayName":"Искра",
              "realm":"Chaos Sea","gmRevision":"preserved","currencies":{"inkFeathers":99,"lightSparks":0},
              "progression":{},"standardArts":{"guard":4},"progressionLedger":[]}]}
            """);
        await _fs.WriteFileAtomicBytesAsync(AfterlifeEntityProfileState.StatePath, staleProfile);
        var forwardRepairs = 0;
        var restoredProfile = false;
        var reached = 0;
        byte[] unknown = [0xFF, 0x61];
        byte[]? journal = null;
        FileSystemManager? files = null;
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (phase != TrustedLocalPublicationPhase.MemberPublished || reached > 0) return;
                    var journalPath = Path.Combine(files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
                    using var document = JsonDocument.Parse(File.ReadAllBytes(journalPath));
                    var member = document.RootElement.GetProperty("Members")[index];
                    if (member.GetProperty("Path").GetString() != files.ResolvePath(AfterlifeEntityProfileState.StatePath)) return;
                    var bytes = File.ReadAllBytes(files.ResolvePath(AfterlifeEntityProfileState.StatePath));
                    if (bytes.SequenceEqual(staleProfile))
                    {
                        if (forwardRepairs > 0) restoredProfile = true;
                        return;
                    }
                    var jsonBytes = bytes.AsMemory();
                    if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) jsonBytes = jsonBytes[3..];
                    using var profile = JsonDocument.Parse(jsonBytes);
                    var feathers = profile.RootElement.GetProperty("profiles")[0].GetProperty("currencies").GetProperty("inkFeathers").GetInt32();
                    if (feathers != 17) return;
                    if (!restoredProfile) { forwardRepairs++; return; }
                    Assert.Equal(experience, File.ReadAllBytes(files.ResolvePath(experiencePath)));
                    reached++;
                    File.WriteAllBytes(files.ResolvePath(AfterlifeEntityProfileState.StatePath), unknown);
                    journal = File.ReadAllBytes(journalPath);
                    throw new RestorationOutcomeCutFailure();
                }
            });
        var service = CreateRuntimeCapableService(files);
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_invalid", Title = "Invalid outcome", FinalNarrative = "Исход применён.",
            GmSummary = "Post-restoration mirror fixture.",
            ResponseFragment = new JsonObject
            {
                ["response"] = "Исход применён.", ["experienceGained"] = 5,
                ["playerCharacterNameChange"] = "Новая личность"
            }
        };
        var failure = await Record.ExceptionAsync(() => service.ApplyTerminalOutcomeValidatedStateChangesAsync(outcome));
        Assert.True(reached == 1, $"Mirror cut hits={reached}; forward repairs={forwardRepairs}; restored={restoredProfile}; failure={failure}");
        Assert.True(forwardRepairs > 0);
        Assert.True(restoredProfile);
        Assert.Equal(unknown, File.ReadAllBytes(files.ResolvePath(AfterlifeEntityProfileState.StatePath)));
        Assert.Equal(journal, File.ReadAllBytes(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        Assert.NotEmpty(Directory.GetFiles(files.ResolvePath(QteNormalizerBackupDirectory), "*", SearchOption.AllDirectories));
        var uncertain = Assert.IsType<CoordinatedStatePublicationUncertainException>(failure);
        var original = Assert.IsType<InvalidOperationException>(uncertain.Data["QteOperationFailure"]);
        Assert.Contains("Локальный QTE outcome нарушил контракт состояния", original.Message, StringComparison.Ordinal);
    }

    private sealed class RestorationOutcomeCutFailure : Exception { }
}
