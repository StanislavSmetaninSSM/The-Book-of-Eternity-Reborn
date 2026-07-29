using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed class BrowserGenerationFencingSourceTests
{
    [Fact]
    public void BrowserMediaGeneration_KeepsDownloadedBytesInMemoryAndCommitsThroughGenerationFence()
    {
        var source = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserMediaGenerationService.cs"));

        Assert.Contains("SessionOperationContext.RunBoundAsync", source, StringComparison.Ordinal);
        Assert.Contains("WriteFileAtomicBytesAsync", source, StringComparison.Ordinal);
        Assert.Contains("staged.Content", source, StringComparison.Ordinal);
        Assert.Contains("TryGetExistingReferenceAsync", source, StringComparison.Ordinal);
        Assert.Contains(
            "return _mediaService.TryCreateReference",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("browser-media-staging", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StagingPath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GenerateEntityImageAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectAfterlifeGacha_UsesOneLeaseForSpendSnapshotAuthorityAndRequest()
    {
        var writeServiceSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserAfterlifeWriteService.cs"));
        var queueSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserAfterlifeTurnRequestQueue.cs"));

        Assert.Contains("_coordinator.RunBoundTransactionAsync", writeServiceSource, StringComparison.Ordinal);
        var directGacha = ExtractMethod(
            writeServiceSource,
            "ApplyGachaPullAsync");
        Assert.Contains("return await ExecuteAtomicAsync(", directGacha, StringComparison.Ordinal);
        Assert.Contains("FileSystemManager.CanonicalWriteLease", queueSource, StringComparison.Ordinal);
        var writeCalls = System.Text.RegularExpressions.Regex.Matches(
            queueSource,
            @"_fs\.WriteFileAtomicAsync\(\s*(?<firstArgument>\w+)");
        Assert.NotEmpty(writeCalls);
        Assert.All(
            writeCalls.Cast<System.Text.RegularExpressions.Match>(),
            call => Assert.Equal("writeLease", call.Groups["firstArgument"].Value));
    }

    [Fact]
    public void BrowserDirectMultiWriteActions_EnterOneBoundCanonicalTransaction()
    {
        var mortalSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserMortalWorldWriteService.cs"));
        var settingsSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserClientSettingsService.cs"));
        var afterlifeSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserAfterlifeWriteService.cs"));

        Assert.Contains("RunBoundTransactionAsync", mortalSource, StringComparison.Ordinal);
        Assert.Contains("ExecuteAtomicAsync", mortalSource, StringComparison.Ordinal);
        Assert.Contains("RunBoundTransactionAsync", settingsSource, StringComparison.Ordinal);
        Assert.Contains("ExecuteAtomicWithinTransactionAsync", settingsSource, StringComparison.Ordinal);
        Assert.Contains("_coordinator.RunBoundTransactionAsync", afterlifeSource, StringComparison.Ordinal);
        Assert.Contains("ExecuteAtomicWithinTransactionAsync", afterlifeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserSettingsAndAudio_UseOneLockOrderAndGenerationBoundTransactions()
    {
        var audioSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserAudioService.cs"));
        var settingsSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserClientSettingsService.cs"));

        var audioUpdate = ExtractMethod(
            audioSource,
            "UpdateSettingsAsync",
            "public async Task<BrowserAudioSettingsDto>");
        AssertLockOrder(audioUpdate);
        Assert.Contains("_coordinator.RunBoundTransactionAsync", audioUpdate, StringComparison.Ordinal);
        Assert.Contains(
            "_coordinator.ExecuteAtomicWithinTransactionAsync",
            audioSource,
            StringComparison.Ordinal);
        Assert.Contains("LoadSettingsAsync(writeLease)", audioSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveSettingsAsync();", audioSource, StringComparison.Ordinal);

        var settingsUpdate = ExtractMethod(
            settingsSource,
            "UpdateAsync",
            "public async Task<BrowserClientSettingsUpdateResult>");
        AssertLockOrder(settingsUpdate);
        Assert.DoesNotContain(
            "SettingsWriteGate.WaitAsync()",
            ExtractMethod(
                settingsSource,
                "UpdateBoundAsync",
                "private async Task<BrowserClientSettingsUpdateResult>"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserLegacyWriteEntryPoint_DelegatesToAtomicCanonicalTransaction()
    {
        var source = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserLocalWriteCoordinator.cs"));
        var execute = ExtractMethod(
            source,
            "ExecuteAsync",
            "internal async Task<BrowserLocalWriteResult>");

        Assert.Contains("ExecuteAtomicAsync(", execute, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteCoreAsync(", execute, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserCanonicalTransactions_PassLeaseExplicitlyWithoutAmbientAuthority()
    {
        var coordinatorSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserLocalWriteCoordinator.cs"));
        var fileSystemSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Core",
            "FileSystemManager.cs"));

        Assert.Contains(
            "Func<FileSystemManager.CanonicalWriteLease, Task<T>> operation",
            coordinatorSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentWriteLease", coordinatorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_currentWriteLease", coordinatorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_ambientCanonicalWriteLease", fileSystemSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RunWithCanonicalWriteLeaseAsync",
            fileSystemSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LocalInteractionScopeResolvers_MustImplementLeaseAwareReadsExplicitly()
    {
        var productionSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Services",
            "LocalInteractionScopeService.cs"));
        var testResolverSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient.Tests",
            "SequenceLocalInteractionScopeResolver.cs"));

        Assert.DoesNotContain(
            "string? currentRealm = null) =>\n        ResolveAsync(currentRealm);",
            productionSource.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.Contains(
            "FileSystemManager.CanonicalWriteLease writeLease",
            testResolverSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserPlayerActionSarefAndManualSave_BindBeforeAuthoritativeReads()
    {
        var playerSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserPlayerActionService.cs"));
        var sarefSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserSarefStoryWriteService.cs"));
        var menuSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "LocalWebUiMainMenuService.cs"));

        var playerSubmit = ExtractMethod(
            playerSource,
            "SubmitAsync",
            "public async Task<BrowserPlayerActionResult>");
        Assert.Contains("_coordinator.RunBoundTransactionAsync", playerSubmit, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildStatusAsync()", playerSubmit, StringComparison.Ordinal);
        Assert.Contains(
            "ExecuteAtomicWithinTransactionAsync",
            ExtractMethod(
                playerSource,
                "SubmitBoundAsync",
                "private async Task<BrowserPlayerActionResult>"),
            StringComparison.Ordinal);

        var sarefWrite = ExtractMethod(
            sarefSource,
            "ApplyFindWingsAsync",
            "private async Task<BrowserPromptWriteResult>");
        Assert.Contains("_coordinator.ExecuteAtomicAsync", sarefWrite, StringComparison.Ordinal);
        Assert.Contains("RefreshGameStateAsync(writeLease)", sarefWrite, StringComparison.Ordinal);
        Assert.Contains(
            "WriteWingsInfiltrationRequestAsync(_fs, writeLease, request)",
            sarefWrite,
            StringComparison.Ordinal);

        var manualSave = ExtractMethod(
            menuSource,
            "CreateManualSaveAsync",
            "public async Task<BrowserCreateSaveResultDto>");
        Assert.Contains("_writeCoordinator.RunBoundTransactionAsync", manualSave, StringComparison.Ordinal);
        var boundSave = ExtractMethod(
            menuSource,
            "CreateManualSaveBoundAsync",
            "private async Task<BrowserCreateSaveResultDto>");
        Assert.Contains("BuildBoundAsync(writeLease)", boundSave, StringComparison.Ordinal);
        Assert.Contains("ExecuteAtomicWithinTransactionAsync", boundSave, StringComparison.Ordinal);
        Assert.Contains("SaveGameAsync(", boundSave, StringComparison.Ordinal);
        Assert.Contains("writeLease,", boundSave, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserPromptSessionsAndLocalUiLocks_UseGenerationBoundCanonicalAuthority()
    {
        var promptSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "ExplorerWebPromptSessionService.cs"));
        var lockSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Services",
            "LocalUiSessionLockService.cs"));

        Assert.Contains(
            "ExpectedSessionGeneration",
            promptSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "SessionOperationContext.RunBoundAsync",
            promptSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("_fs.ResolvePath(", lockSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists(", lockSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.GetLastWriteTimeUtc(", lockSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new FileStream(", lockSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.CreateDirectory(", lockSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DarenRewardProfile_UsesOnePhysicalAuthorityStoreWithoutRawPathIo()
    {
        var rewardSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Services",
            "DarenQteRewardProfileService.cs"));
        var qteSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Services",
            "QteSceneService.cs"));

        Assert.Contains("DarenRewardProfileFileStore", rewardSource, StringComparison.Ordinal);
        Assert.Contains("DarenRewardProfileFileStore", qteSource, StringComparison.Ordinal);
        foreach (var prohibited in new[]
                 {
                     "File.Exists(",
                     "File.ReadAllTextAsync(",
                     "File.ReadAllBytes(",
                     "File.WriteAllTextAsync(",
                     "File.WriteAllBytes(",
                     "File.Move(",
                     "File.Delete("
                 })
        {
            Assert.DoesNotContain(prohibited, rewardSource, StringComparison.Ordinal);
        }

        var profileRead = ExtractMethod(
            qteSource,
            "ReadDarenRewardProfileAsync",
            "internal async Task<DarenRewardProfileState>");
        Assert.DoesNotContain("File.", profileRead, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "WriteExternalFileAtomic",
            qteSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserQteCanonicalMutations_RunInsideGenerationBoundTransactions()
    {
        var source = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "QteWebInteractionService.cs"));

        Assert.Contains(
            "BrowserLocalWriteCoordinator coordinator",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "BuildStateAsync",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ResolveOfferDecisionBoundAsync",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ResolveActionBoundAsync",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ResolveDarenShowcaseActionBoundAsync",
            source,
            StringComparison.Ordinal);
        Assert.True(
            source.Split("_coordinator.RunBoundTransactionAsync", StringSplitOptions.None).Length - 1 >= 4,
            "Every QTE browser mutation family must enter a generation-bound canonical transaction.");
    }

    [Fact]
    public void AfterlifeDirectMultiWriteActions_UseOneCanonicalLeaseForEveryWrite()
    {
        var source = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserAfterlifeWriteService.cs"));

        AssertAtomicMethod(
            source,
            "ApplyShiningFactionFoundingAsync",
            "WriteFoundingRequestAsync(_fs, writeLease, request)",
            "WriteObjectAsync(writeLease, SoulStatePath, soulRoot)",
            "WriteObjectAsync(writeLease, ShiningAbodeState.StatePath, shiningRoot)");
        AssertAtomicMethod(
            source,
            "ApplyShiningRelicForgeAsync",
            "WriteForgeRequestWithRelicRerollCommitAsync(",
            "writeLease,");
        AssertAtomicMethod(
            source,
            "ApplyShiningTreasuryAsync",
            "WriteObjectAsync(writeLease, ShiningAbodeState.StatePath, shiningRoot)",
            "WriteObjectAsync(writeLease, SoulStatePath, soulRoot)");
        AssertAtomicMethod(
            source,
            "ApplySpiritualArtsAsync",
            "WriteObjectAsync(writeLease, SoulStatePath, soulRoot)",
            "WriteObjectAsync(writeLease, ShiningAbodeState.StatePath, shiningRoot)",
            "WriteObjectAsync(writeLease, AfterlifeEntityProfileState.StatePath, entityProfilesRoot)");
        AssertAtomicMethod(
            source,
            "ApplyInkFeatherRevealFateAsync",
            "GetOrCreateAsync(writeLease)",
            "WriteObjectAsync(writeLease, SoulStatePath, soulRoot)",
            "RevealAsync(writeLease)");
        AssertAtomicMethod(
            source,
            "ApplyInkFeatherRewriteFateAsync",
            "TryReadExistingAsync(writeLease)",
            "WriteObjectAsync(writeLease, SoulStatePath, soulRoot)",
            "RewriteAsync(writeLease)");
        AssertAtomicMethod(
            source,
            "ApplyAbodeOfferingAsync",
            "GuardianAbodeOfferingState.WriteAsync(_fs, writeLease, request)",
            "WriteObjectAsync(writeLease, SoulStatePath, soulRoot)");

        Assert.DoesNotContain(
            "await _stateManager.RefreshGameStateAsync();",
            ExtractMethod(source, "ApplyShiningFactionFoundingAsync"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "await _stateManager.RefreshGameStateAsync();",
            ExtractMethod(source, "ApplyShiningRelicForgeAsync"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifeRuntimeRollbackSnapshot_IsCapturedAfterCanonicalLeaseAcquisition()
    {
        var coordinatorSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserLocalWriteCoordinator.cs"));
        var afterlifeSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserAfterlifeWriteService.cs"));

        Assert.Contains(
            "Func<Action?>? prepareAfterRollback",
            coordinatorSource,
            StringComparison.Ordinal);
        var atomicCore = ExtractMethod(
            coordinatorSource,
            "ExecuteAtomicCoreAsync",
            "private async Task<BrowserLocalWriteResult>");
        Assert.Contains(
            "FileSystemManager.CanonicalWriteLease writeLease",
            atomicCore,
            StringComparison.Ordinal);
        Assert.Contains("prepareAfterRollback?.Invoke()", atomicCore, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "var runtimeSnapshot = _stateManager.CaptureRuntimeSnapshot();\n        var result",
            afterlifeSource.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.Contains(
            "prepareAfterRollback: () =>",
            afterlifeSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ImageService_RemoteGenerationKeepsBytesInMemoryBeforeCanonicalCommit()
    {
        var source = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Services",
            "ImageService.cs"));

        Assert.Contains("SessionOperationContext.RunBoundAsync", source, StringComparison.Ordinal);
        Assert.Contains("WriteFileAtomicBytesAsync", source, StringComparison.Ordinal);
        Assert.Contains("byte[] Content", source, StringComparison.Ordinal);
        Assert.DoesNotContain("browser-media-staging", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalRuntimeStaging", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllBytesAsync(filePath, bytes)", source, StringComparison.Ordinal);
    }

    private static string SourcePath(params string[] segments)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return Path.Combine([repositoryRoot, .. segments]);
    }

    private static void AssertAtomicMethod(
        string source,
        string methodName,
        params string[] requiredFragments)
    {
        var method = ExtractMethod(source, methodName);
        Assert.Contains("return await ExecuteAtomicAsync(", method, StringComparison.Ordinal);
        Assert.Contains("async writeLease =>", method, StringComparison.Ordinal);
        Assert.DoesNotContain("return await ExecuteAsync(", method, StringComparison.Ordinal);
        Assert.All(
            requiredFragments,
            fragment => Assert.Contains(fragment, method, StringComparison.Ordinal));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        return ExtractMethod(
            source,
            methodName,
            "private async Task<BrowserPromptWriteResult>");
    }

    private static string ExtractMethod(
        string source,
        string methodName,
        string signaturePrefix)
    {
        var signature = $"{signaturePrefix} {methodName}(";
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method {methodName} was not found.");

        var bodyStart = source.IndexOf('{', start + signature.Length);
        Assert.True(bodyStart >= 0, $"Method body for {methodName} was not found.");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            depth += source[index] switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0
            };

            if (depth == 0)
                return source[start..(index + 1)];
        }

        throw new Xunit.Sdk.XunitException($"Method boundary after {methodName} was not found.");
    }

    private static void AssertLockOrder(string method)
    {
        var gateIndex = method.IndexOf("SettingsWriteGate.WaitAsync()", StringComparison.Ordinal);
        var transactionIndex = method.IndexOf("_coordinator.RunBoundTransactionAsync", StringComparison.Ordinal);
        Assert.True(gateIndex >= 0, "Settings gate acquisition was not found.");
        Assert.True(transactionIndex >= 0, "Generation-bound transaction was not found.");
        Assert.True(
            gateIndex < transactionIndex,
            "Settings gate must be acquired before the canonical transaction to keep one lock order.");
    }
}
