using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Binds signed presence/absence and independent candidate receipt DATA through the actual deferred source reader.
    /// No source preparation, resource execution, current owner or publication authority is manufactured.
    /// </summary>
    /// <param name="signedPresent">Whether the authenticated original contains receipt bytes.</param>
    /// <param name="candidatePresent">Whether the current candidate contains receipt bytes.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SourceOwner_ReceiptObservedImagesAreBound(bool signedPresent, bool candidatePresent)
    {
        var path = SpiritualWoundOpportunityReceiptState.StatePath;
        // The closed v1 shape permits empty history; v2/v3 require healing/comparison rows.
        const string originalJson = "{\"schemaVersion\":1,\"nextInstanceOrdinal\":1,\"nextClosureOrdinal\":1," +
            "\"nextSourceOrdinal\":1,\"nextDecisionOrdinal\":1,\"instances\":[]," +
            "\"closures\":[],\"sources\":[],\"decisions\":[]}\n";
        const string candidateJson = " \n" + originalJson + "\n";
        var originalReceipt = SpiritualWoundOpportunityReceiptState.Parse(originalJson, path);
        var candidateReceipt = SpiritualWoundOpportunityReceiptState.Parse(candidateJson, path);
        var receiptDiagnostics = JsonSerializer.Serialize(new
        {
            original = new { originalReceipt.IsValid, originalReceipt.Issues },
            candidate = new { candidateReceipt.IsValid, candidateReceipt.Issues }
        });
        Assert.True(originalReceipt.IsValid, receiptDiagnostics);
        Assert.True(candidateReceipt.IsValid, receiptDiagnostics);
        var context = await ResourceMaterializationTestContext.CreateAsync();
        bool selected = false;
        bool covered = false;
        bool absent = false;
        string? original = null;
        string? candidate = null;
        JsonObject? binding = null;
        await using (context)
        {
            var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
            Assert.True(bootstrap.IsValid, string.Join("\n", bootstrap.Issues));
            await context.WriteExactJsonAsync(ValidationService.SpiritualWoundSourceSession.SoulPath,
                SoulState(0).ToJsonString());
            await context.WriteExactJsonAsync(ResourceMaterializationContract.DefinitionsPath,
                bootstrap.Definitions!.ToCanonicalJson());
            await context.WriteExactJsonAsync(ResourceMaterializationContract.StatePath,
                bootstrap.State!.ToCanonicalJson());
            await context.WriteExactJsonAsync(ResourceMaterializationContract.HistoryPath,
                bootstrap.History!.ToCanonicalJson());
            // Structurally valid empty authority DATA; this deferred reader does not interpret or grant it.
            await context.WriteExactJsonAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                "{\"schemaVersion\":1,\"historicalOwners\":[],\"capacityDrafts\":[]}");
            if (signedPresent) await context.WriteExactJsonAsync(path, originalJson);
            await context.CaptureValidatedPendingSnapshotAsync(currentRealm: "Chaos Sea",
                additionalTrackedPaths: [path], sessionId: "receipt_observation_session",
                requestId: "receipt_observation_request");
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            if (candidatePresent)
                await context.FileSystem.WriteFileAtomicBytesAsync(lease, path, Encoding.UTF8.GetBytes(candidateJson));
            else context.FileSystem.DeleteFile(lease, path);
            var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
            var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            var prepared = await ValidationService.SpiritualWoundSourceSession.AcquireAsync(
                context.Validator, lease, awaitOriginalPrefix: true);
            AssertNoConflictFrameErrors(prepared.Issues);
            var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
            try
            {
                Assert.Empty(source.Sources);
                Assert.False(source.IsCurrentOwner);
                selected = source.SelectedPaths.Contains(path, StringComparer.Ordinal);
                binding = source.BuildInputBinding();
                if (selected)
                {
                    covered = source.HasOriginalBytes(path);
                    absent = source.IsOriginalAbsent(path);
                    original = source.ReadOriginal(path);
                    candidate = source.ReadCandidate(path);
                }
                AssertExistingPressureInputInert(context, lease, files, generation);
            }
            finally { source.Revoke(); }
        }
        Assert.False(Directory.Exists(context.RootPath));
        Assert.True(selected, "RECON1_RECEIPT_SELECTED_INPUT: receipt must participate in the existing source read set.");
        Assert.Equal(signedPresent, covered);
        Assert.Equal(!signedPresent, absent);
        Assert.Equal(signedPresent ? originalJson : null, original);
        Assert.Equal(candidatePresent ? candidateJson : null, candidate);
        var originals = Assert.IsType<JsonObject>(binding!["originals"]);
        var candidates = Assert.IsType<JsonObject>(binding["candidates"]);
        Assert.True(originals.ContainsKey(path));
        Assert.True(candidates.ContainsKey(path));
        Assert.Equal(original, originals[path]?.GetValue<string>());
        Assert.Equal(candidate, candidates[path]?.GetValue<string>());
    }
}
