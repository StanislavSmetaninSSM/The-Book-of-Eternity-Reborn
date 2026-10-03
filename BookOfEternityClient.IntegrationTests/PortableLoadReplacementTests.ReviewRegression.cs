using System.IO.Compression;
using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableLoadReplacementTests
{
    [Theory]
    [InlineData(SoulPath, false)]
    [InlineData(ResourceMaterializationContract.DefinitionsPath, false)]
    [InlineData(ResourceMaterializationContract.StatePath, false)]
    [InlineData(ResourceMaterializationContract.HistoryPath, false)]
    [InlineData(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, false)]
    [InlineData("config.json", false)]
    [InlineData(AfterlifeEntityProfileState.StatePath, false)]
    [InlineData(ShiningAbodeState.StatePath, false)]
    [InlineData(SoulPath, true)]
    [InlineData(ResourceMaterializationContract.StatePath, true)]
    [InlineData(AfterlifeEntityProfileState.StatePath, true)]
    [InlineData(ShiningAbodeState.StatePath, true)]
    public async Task FixedAuthorityCaseAliasesMaterializeCanonicalNamesAndState(string canonical,
        bool includeDirectoryCase)
    {
        if (canonical == AfterlifeEntityProfileState.StatePath)
            Put(canonical, Encoding.UTF8.GetBytes("""{"profiles":[]}"""));
        if (canonical == ShiningAbodeState.StatePath)
            Put(canonical, Encoding.UTF8.GetBytes(ShiningAbodeState.CreateDefaultState().ToJsonString()));
        var source = await PrepareCurrentArchiveAsync(archiveHasConfig: canonical == "config.json");
        var alias = includeDirectoryCase ? canonical.ToUpperInvariant() :
            canonical[..(canonical.LastIndexOf('/') + 1)] + Path.GetFileName(canonical).ToUpperInvariant();
        var saved = RenameArchiveEntry(source, canonical, alias);
        var protectedFiles = SnapshotLibraryAndSource(source);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.False(result.ContinuationBlocked);
        Assert.False(result.NeedsFollowUp);
        Assert.Equal(1, _prepared);
        Assert.Single(_phases.Where(value => value.Phase == TrustedLocalPublicationPhase.Committed));
        Assert.Equal(saved, File.ReadAllBytes(_files.ResolvePath(canonical)));
        Assert.DoesNotContain(Directory.EnumerateFiles(Path.GetDirectoryName(_files.ResolvePath(canonical))!),
            path => Path.GetFileName(path).Equals(Path.GetFileName(alias), StringComparison.Ordinal));
        Assert.Equal("Loaded soul", _state.CurrentState.SoulName);
        Assert.Equal(20, _state.Settings.ConsoleFontSize);
        AssertPreserved(protectedFiles);
        AssertOwnedScratchEmpty();
    }

    [Fact]
    public async Task InvalidCaseAliasedConfigRejectsBeforeLifecycleMutation()
    {
        var source = await PrepareCurrentArchiveAsync(archiveHasConfig: true);
        RenameArchiveEntry(source, "config.json", "CONFIG.JSON", Encoding.UTF8.GetBytes("null"));
        var before = Snapshot(_files.GameSessionPath);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        var failure = Assert.IsType<InvalidDataException>(result.Failure);
        Assert.Contains("config.json", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, _prepared);
        Assert.Equal(0, _lifecycleOpen);
        Assert.Empty(_phases);
        Assert.Equal(before.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
        AssertPreserved(before);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(28, _state.Settings.ConsoleFontSize);
        AssertOwnedScratchEmpty();
    }

    [Fact]
    public async Task CanonicalizedAuthorityCannotReplaceTheSelectedArchive()
    {
        var source = await PrepareCurrentArchiveAsync(selectedRelativePath: "config.json", collision: "CONFIG.JSON");
        var before = Snapshot(_files.GameSessionPath);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        var failure = Assert.IsType<InvalidDataException>(result.Failure);
        Assert.Contains("selected archive", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _prepared);
        Assert.Equal(0, _lifecycleOpen);
        Assert.Empty(_phases);
        Assert.Equal(before.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
        AssertPreserved(before);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        AssertOwnedScratchEmpty();
    }

    [Fact]
    public async Task OriginalManifestHashRejectsBeforeCanonicalizedLoadPreparation()
    {
        var source = await PrepareCurrentArchiveAsync();
        var modifiedSoul = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(_loadedSoul).Replace("Loaded soul", "Altered one", StringComparison.Ordinal));
        Assert.Equal(_loadedSoul.Length, modifiedSoul.Length);
        RenameArchiveEntry(source, SoulPath, SoulPath.ToUpperInvariant(), modifiedSoul, preserveManifest: true);
        var before = Snapshot(_files.GameSessionPath);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        Assert.Contains("SHA-256", Assert.IsType<InvalidDataException>(result.Failure).Message);
        Assert.Equal(0, _prepared);
        Assert.Equal(0, _lifecycleOpen);
        Assert.Empty(_phases);
        AssertPreserved(before);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal("Old live soul", _state.CurrentState.SoulName);
        AssertOwnedScratchEmpty();
    }

    [Theory]
    [InlineData("game_state/core/player_status.json")]
    [InlineData("game_state/world/world_time.json")]
    [InlineData("lore/codex_entries.json")]
    [InlineData(WorldDirectiveService.ActiveDirectivesPath)]
    public async Task PreparedInventoryUsesDeclaredFixedNamesAndPreservesArbitraryNames(string canonical)
    {
        var fixedBytes = Encoding.UTF8.GetBytes("{}\n");
        const string arbitrary = "lore/MyCustom.JSON";
        byte[] arbitraryBytes = [41, 0, 42, 255];
        Put(canonical, fixedBytes);
        Put(arbitrary, arbitraryBytes);
        var source = await PrepareCurrentArchiveAsync();
        RenameArchiveEntry(source, canonical, canonical.ToUpperInvariant(), preserveManifest: true);
        var protectedFiles = SnapshotLibraryAndSource(source);

        await using (var candidate = await _service.PrepareLoadArchiveAsync(source))
        {
            Assert.Contains(candidate.Images.Keys, path => path.Equals(canonical, StringComparison.Ordinal));
            Assert.DoesNotContain(candidate.Images.Keys, path => path.Equals(canonical.ToUpperInvariant(), StringComparison.Ordinal));
            Assert.Contains(candidate.Images.Keys, path => path.Equals(arbitrary, StringComparison.Ordinal));
            using var fixedInput = candidate.Images[canonical].OpenRead();
            using var fixedContent = new MemoryStream();
            fixedInput.CopyTo(fixedContent);
            Assert.Equal(fixedBytes, fixedContent.ToArray());
            using var arbitraryInput = candidate.Images[arbitrary].OpenRead();
            using var arbitraryContent = new MemoryStream();
            arbitraryInput.CopyTo(arbitraryContent);
            Assert.Equal(arbitraryBytes, arbitraryContent.ToArray());
        }

        AssertPreserved(protectedFiles);
        Assert.Equal(0, _lifecycleOpen);
        Assert.Empty(_phases);
        AssertOwnedScratchEmpty();
    }

    private static byte[] RenameArchiveEntry(string source, string canonical, string alias,
        byte[]? replacement = null, bool preserveManifest = false)
    {
        using var archive = ZipFile.Open(source, ZipArchiveMode.Update);
        if (!preserveManifest) archive.GetEntry("save_manifest.json")!.Delete();
        var original = Assert.IsType<ZipArchiveEntry>(archive.GetEntry(canonical));
        using var content = new MemoryStream();
        using (var input = original.Open()) input.CopyTo(content);
        original.Delete();
        var bytes = replacement ?? content.ToArray();
        using var output = archive.CreateEntry(alias, CompressionLevel.NoCompression).Open();
        output.Write(bytes);
        return bytes;
    }

    [Fact]
    public async Task PriorJournalConflictDuringAcquisitionIsUncertainAndBlocksContinuation()
    {
        var source = await PrepareCurrentArchiveAsync();
        var journal = Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
        {
            var generation = _files.ReadLocalGenerationSnapshot(lease).Binding;
            var path = _files.ResolvePath(MarkerPath);
            var publisher = new TrustedLocalFilePublication(_files, new TrustedLocalFileScope([_root]));
            Assert.Throws<IOException>(() => publisher.Publish(lease, generation,
                [new(path, File.ReadAllBytes(path), [1, 2, 3])], (phase, _) =>
                {
                    if (phase != TrustedLocalPublicationPhase.IntentPublished) return;
                    File.WriteAllBytes(path, [91, 92, 93]);
                    throw new IOException("retained load admission journal cut");
                }));
        }
        var before = Snapshot(_files.GameSessionPath);
        var journalHash = Hash(journal);
        var generationBytes = File.ReadAllBytes(_files.SessionGenerationPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(1, _prepared);
        Assert.Equal(LoadReplacementDisposition.Uncertain, result.Disposition);
        Assert.True(result.NeedsFollowUp);
        Assert.True(result.ContinuationBlocked);
        Assert.Null(result.EstablishedGeneration);
        Assert.NotNull(result.Failure);
        Assert.Empty(_phases);
        AssertPreserved(before);
        Assert.Equal(journalHash, Hash(journal));
        Assert.Equal(generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal("Old live soul", _state.CurrentState.SoulName);
    }

    [Fact]
    public async Task PreparationAndCleanupFailureRetainsKnownPrivateDebt()
    {
        var source = await PrepareCurrentArchiveAsync();
        var before = Snapshot(_files.GameSessionPath);
        string? retainedRoot = null;
        var extraction = 0;
        var cleanup = 0;
        _afterExtraction = path =>
        {
            Assert.NotEmpty(Directory.GetFiles(path, "*", SearchOption.AllDirectories));
            retainedRoot = path;
            extraction++;
            throw new InvalidDataException("detached preparation cut");
        };
        _beforePreparationCleanup = path =>
        {
            Assert.Equal(retainedRoot, path);
            cleanup++;
            throw new IOException("owned preparation cleanup cut");
        };

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(1, extraction);
        Assert.Equal(1, cleanup);
        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        Assert.True(result.NeedsFollowUp);
        Assert.False(result.ContinuationBlocked);
        Assert.Contains("detached preparation cut", result.Failure!.ToString());
        Assert.Contains("owned preparation cleanup cut", result.Failure.ToString());
        var debt = Assert.IsType<LoadPreparationCleanupException>(result.Failure);
        Assert.Equal(retainedRoot, debt.StagingRoot);
        Assert.Equal(source, debt.SourcePath);
        Assert.Equal(source, result.SelectedSourcePath);
        Assert.True(Directory.Exists(retainedRoot));
        Assert.NotEmpty(Directory.GetFiles(retainedRoot!, "*", SearchOption.AllDirectories));
        Assert.Equal(0, _prepared);
        Assert.Equal(0, _lifecycleOpen);
        Assert.Empty(_phases);
        AssertPreserved(before);
        Assert.Equal("Old live soul", _state.CurrentState.SoulName);
    }

    [Fact]
    public async Task ConflictAfterGenerationPublicationDoesNotClaimAnEstablishedGeneration()
    {
        var source = await PrepareCurrentArchiveAsync();
        var protectedFiles = SnapshotLibraryAndSource(source);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var cuts = 0;
        _fault = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.CommitStaged) return;
            Assert.False(generation.SequenceEqual(File.ReadAllBytes(_files.SessionGenerationPath)));
            cuts++;
            Put(MarkerPath, [91, 92, 93]);
            throw new InvalidOperationException("load conflict after generation publication");
        };

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(1, cuts);
        Assert.Equal(LoadReplacementDisposition.Uncertain, result.Disposition);
        Assert.True(result.ContinuationBlocked);
        Assert.True(result.NeedsFollowUp);
        Assert.Null(result.EstablishedGeneration);
        Assert.False(generation.SequenceEqual(File.ReadAllBytes(_files.SessionGenerationPath)));
        Assert.Equal(new byte[] { 91, 92, 93 }, File.ReadAllBytes(_files.ResolvePath(MarkerPath)));
        Assert.True(File.Exists(Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        AssertPreserved(protectedFiles);
        Assert.Equal("Old live soul", _state.CurrentState.SoulName);
    }

    [Fact]
    public async Task ConfirmedCommitRetainsIdentityWhenRequiredRefreshFails()
    {
        var source = await PrepareCurrentArchiveAsync();
        var protectedFiles = SnapshotLibraryAndSource(source);
        var cuts = 0;
        _afterPublication = () => { cuts++; throw new IOException("committed load refresh cut"); };

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(1, cuts);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.True(result.NeedsFollowUp);
        Assert.True(result.ContinuationBlocked);
        Assert.NotNull(result.EstablishedGeneration);
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
            Assert.Equal(result.EstablishedGeneration, _files.ReadLocalGenerationSnapshot(lease).Binding.Id);
        Assert.Equal(_loadedSoul, File.ReadAllBytes(_files.ResolvePath(SoulPath)));
        Assert.Single(_phases.Where(value => value.Phase == TrustedLocalPublicationPhase.Committed));
        AssertPreserved(protectedFiles);
        AssertOwnedScratchEmpty();
    }
}
