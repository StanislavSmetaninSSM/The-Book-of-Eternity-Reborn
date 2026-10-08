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
    [InlineData("create-unknown")]
    [InlineData("create-debt")]
    [InlineData("accepted-cleanup")]
    public async Task ActualBackupUncertaintyStopsQteBeforeExperienceNormalizationAndCompensation(string cut)
    {
        const string target = "game_state/world/weather.json";
        var baseline = Encoding.UTF8.GetBytes("{\"marker\":\"baseline\"}");
        File.WriteAllBytes(_fs.ResolvePath(target), baseline);
        await using (var seedLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            _fs.GetOrCreateSessionGeneration(seedLease);
        var reached = 0; var laterMutations = 0; byte[]? evidence = null; string? backup = null;
        FileSystemManager? files = null;
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = _ => { if (reached > 0) laterMutations++; return Task.CompletedTask; },
                LocalPublicationObserver = (phase, _) =>
                {
                    if (reached > 0 || phase != (cut == "create-debt" ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.MemberPublished)) return;
                    var journalPath = Path.Combine(files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
                    using var doc = JsonDocument.Parse(File.ReadAllBytes(journalPath));
                    var members = doc.RootElement.GetProperty("Members");
                    if (members.GetArrayLength() != 1) return;
                    var member = members[0]; var path = member.GetProperty("Path").GetString()!;
                    if (!path.Contains("weather.json.backup.", StringComparison.Ordinal)) return;
                    var deleting = !member.GetProperty("After").GetProperty("Exists").GetBoolean();
                    if ((cut == "accepted-cleanup") != deleting) return;
                    backup = path; reached++; File.WriteAllBytes(path, [99]);
                    evidence = File.ReadAllBytes(journalPath);
                    throw new BackupCutFailure();
                }
            });
        var service = CreateRuntimeCapableService(files);
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            ResponseFragment = new JsonObject { ["weatherChange"] = new JsonObject { ["description"] = "accepted weather" }, ["experienceGained"] = 10 }
        };
        await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => service.ApplyTerminalOutcomeStateChangesAsync(outcome));
        Assert.Equal(1, reached); Assert.Equal(0, laterMutations);
        Assert.NotNull(backup); Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(backup));
        Assert.Equal(evidence, File.ReadAllBytes(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        if (cut == "accepted-cleanup") Assert.Contains("accepted weather", File.ReadAllText(files.ResolvePath(target)), StringComparison.Ordinal);
        else Assert.Equal(baseline, File.ReadAllBytes(files.ResolvePath(target)));
        Assert.True(Directory.Exists(files.ResolvePath(QteNormalizerBackupDirectory)));
    }

    private sealed class BackupCutFailure : Exception { }
}
