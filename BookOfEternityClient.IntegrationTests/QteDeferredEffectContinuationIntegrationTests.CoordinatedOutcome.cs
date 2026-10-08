using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class QteDeferredEffectContinuationIntegrationTests
{
    [Fact]
    public async Task CoordinatedUncertaintyRetainsTheAcceptedMechanicsCutWithoutDomainCompensation()
    {
        Action<TrustedLocalPublicationPhase, int>? observer = null;
        var cutReached = false;
        var mutationAttemptsAfterCut = 0;
        await using var context = await EffectMaterializationTestContext.CreateAsync(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) => observer?.Invoke(phase, index),
            BeforeCanonicalMutationBoundaryAsync = _ =>
            {
                if (cutReached) mutationAttemptsAfterCut++;
                return Task.CompletedTask;
            }
        });
        var scenario = await PrepareAwaitingReceiptAsync(context, "session_qte_coordinated_uncertain");
        await WriteReceiptTransportAsync(context, scenario.Request, scenario.PendingRequestId, amount: 2m);
        var files = context.FileSystem;
        var journalPath = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        var backupRoot = files.ResolvePath(QteSceneService.QteNormalizerBackupDirectory);
        byte[]? evidence = null;
        Dictionary<string, byte[]?>? publishedImages = null;
        Dictionary<string, byte[]>? backups = null;
        var cutHits = 0;
        observer = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished || index != 0 || cutReached) return;
            using var journal = JsonDocument.Parse(File.ReadAllBytes(journalPath));
            var members = journal.RootElement.GetProperty("Members").EnumerateArray()
                .Select(member => member.GetProperty("Path").GetString()!).ToArray();
            // Normalizer backups and the earlier continuation/transport writes
            // have their own publications. Target the actual accepted plan.
            if (!members.Contains(files.ResolvePath(ResourceMaterializationContract.StatePath), StringComparer.Ordinal) ||
                !members.Contains(files.ResolvePath(EffectAcceptedTurnPlan.IdentityIndexPath), StringComparer.Ordinal)) return;
            Assert.True(members.Length > 1);
            backups = Directory.EnumerateFiles(backupRoot, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes);
            Assert.NotEmpty(backups);
            cutHits++;
            File.WriteAllBytes(members[^1], [13, 37, 42]);
            evidence = File.ReadAllBytes(journalPath);
            publishedImages = members.ToDictionary(path => path,
                path => File.Exists(path) ? File.ReadAllBytes(path) : null);
            cutReached = true;
            throw new IOException("Injected reached accepted-mechanics publication cut.");
        };
        var service = CreateQteService(files);

        var error = await Record.ExceptionAsync(async () =>
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            await SessionOperationContext.RunBoundAsync(files, scenario.Generation, lease,
                () => service.ResumeDeferredEffectResolutionAsync(lease, 42, allowPreexistingStateIssues: true));
        });

        Assert.Equal(1, cutHits);
        Assert.Equal(evidence, File.ReadAllBytes(journalPath));
        foreach (var pair in publishedImages!)
            Assert.Equal(pair.Value, File.Exists(pair.Key) ? File.ReadAllBytes(pair.Key) : null);
        Assert.Equal(backups!.Keys.Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(backupRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        foreach (var pair in backups) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        Assert.True(mutationAttemptsAfterCut == 0,
            $"Uncertain publication triggered {mutationAttemptsAfterCut} later canonical mutation attempts; propagated {error?.GetType().Name ?? "no exception"}.");
        Assert.IsType<CoordinatedStatePublicationUncertainException>(error);
    }
}
