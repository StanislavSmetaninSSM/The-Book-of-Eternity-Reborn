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
            "BookOfEternityClient.TestSupport",
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
        var storeSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Services",
            "DarenRewardProfileFileStore.cs"));

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
        foreach (var prohibited in new[]
                 {
                     "File.Exists(",
                     "Directory.Exists(",
                     "File.Move(",
                     "Directory.Move("
                 })
        {
            Assert.DoesNotContain(
                prohibited,
                storeSource,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BrowserPendingAndRecoveryAuthority_ContainsNoPathnameAbsenceFallback()
    {
        var inspectorSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "BrowserPendingTurnInspector.cs"));
        var publicationSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Core",
            "ReversibleFilePublication.cs"));
        var rollbackSource = ExtractMethod(
            publicationSource,
            "RollBack",
            "private static bool");
        var recoverySource = ExtractMethod(
            publicationSource,
            "RecoverPending",
            "internal static void");
        var candidateSource = ExtractMethod(
            publicationSource,
            "OpenIdentityFromCandidates",
            "private static SafeFileHandle?");
        var publicationIntentSource = ExtractMethod(
            publicationSource,
            "ReadIntent",
            "private static PublicationIntent");
        var rollbackManifestSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Services",
            "ExplorerLocalTurnRollbackArtifacts.cs"));
        var browserRestoreSource = ExtractMethod(
            rollbackManifestSource,
            "RestoreBrowserWriteTransactionAsync",
            "internal static async Task");
        var browserRecoverySource = ExtractMethod(
            rollbackManifestSource,
            "RecoverInterruptedBrowserWriteTransactionsAsync",
            "internal static async Task");
        var fileSystemSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Core",
            "FileSystemManager.cs"));
        var synchronousReadSource = ExtractMethod(
            fileSystemSource,
            "ReadFileSnapshotCore",
            "private CanonicalFileReadSnapshot?");
        var asynchronousReadSource = ExtractMethod(
            fileSystemSource,
            "OpenCanonicalReadStreamAsync",
            "private async Task<StableReadFile?>");
        var loadJournalSource = ExtractMethod(
            fileSystemSource,
            "ReadLoadTransactionJournal",
            "private LoadTransactionJournal");
        var workerJournalSource = ExtractMethod(
            fileSystemSource,
            "ReadWorkerApplyJournal",
            "private WorkerApplyTransactionJournal");
        var workerRecoverySource = ExtractMethod(
            fileSystemSource,
            "RecoverInterruptedWorkerApplyTransactionAsync",
            "private async Task");

        Assert.Contains(
            "fs.FileExists(writeLease, path)",
            inspectorSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "fs.DirectoryHasContent(writeLease, path)",
            inspectorSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Directory.Exists(",
            inspectorSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "File.Exists(",
            inspectorSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProbeNamespaceEntry(",
            rollbackSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "File.Exists(",
            rollbackSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Directory.Exists(",
            rollbackSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProbeNamespaceEntryFromRoot(",
            recoverySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Directory.Exists(",
            recoverySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProbeNamespaceEntry(",
            candidateSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "File.Exists(",
            candidateSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Directory.Exists(",
            browserRestoreSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "fs.DirectoryExists(",
            browserRestoreSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "fs.DeleteDirectoryTree(writeLease, cleanupDirectory)",
            browserRestoreSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "File.Exists(",
            synchronousReadSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "File.Exists(",
            asynchronousReadSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "StrictJsonAuthority.Deserialize<PublicationIntent>",
            publicationIntentSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "StrictJsonAuthority.Deserialize<BrowserWriteRollbackManifest>",
            browserRecoverySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "StrictJsonAuthority.Deserialize<LoadTransactionJournal>",
            loadJournalSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "StrictJsonAuthority.Deserialize<WorkerApplyTransactionJournal>",
            workerJournalSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "StrictJsonAuthority.Deserialize<WorkerApplyTransactionManifest>",
            workerRecoverySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "manifest.SchemaVersion < 4 && sourceExternalEntries.Count > 0",
            rollbackManifestSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalMutationObservation_UsesAtomicTransitionsAndSnapshots()
    {
        var source = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Core",
            "FileSystemManager.cs"));
        var beginMutation = ExtractMethod(
            source,
            "BeginMutation",
            "internal void");
        var endMutation = ExtractMethod(
            source,
            "EndMutation",
            "internal void");
        var captureMutationState = ExtractMethod(
            source,
            "CaptureSnapshot",
            "internal InProcessMutationSnapshot");
        var fileExists = ExtractMethod(
            source,
            "FileExists",
            "public bool");

        Assert.Contains("lock (this)", beginMutation, StringComparison.Ordinal);
        Assert.Contains("Version", beginMutation, StringComparison.Ordinal);
        Assert.Contains("ActiveMutationCount", beginMutation, StringComparison.Ordinal);
        Assert.DoesNotContain("Interlocked.", beginMutation, StringComparison.Ordinal);

        Assert.Contains("lock (this)", endMutation, StringComparison.Ordinal);
        Assert.Contains("Version", endMutation, StringComparison.Ordinal);
        Assert.Contains("ActiveMutationCount", endMutation, StringComparison.Ordinal);
        Assert.DoesNotContain("Interlocked.", endMutation, StringComparison.Ordinal);

        Assert.Contains("lock (this)", captureMutationState, StringComparison.Ordinal);
        Assert.Contains(
            "new InProcessMutationSnapshot(",
            captureMutationState,
            StringComparison.Ordinal);
        Assert.Contains("Version", captureMutationState, StringComparison.Ordinal);
        Assert.Contains("ActiveMutationCount > 0", captureMutationState, StringComparison.Ordinal);

        Assert.Contains("_state.BeginMutation();", source, StringComparison.Ordinal);
        Assert.Contains("state.EndMutation();", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Interlocked.Increment(ref _state.Version)",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Interlocked.Increment(ref _state.ActiveMutationCount)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("mutationBeforeProbe", fileExists, StringComparison.Ordinal);
        Assert.Contains("mutationAfterProbe", fileExists, StringComparison.Ordinal);
        Assert.Equal(
            2,
            fileExists.Split(
                "observation.CaptureMutationState()",
                StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(
            "observation.Version",
            fileExists,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "observation.MutationActive",
            fileExists,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Phase42AuthorityBoundaries_KeepStrictClassificationAndBookkeeping()
    {
        var fileSystemSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Core",
            "FileSystemManager.cs"));
        var fileExistsCore = ExtractMethod(
            fileSystemSource,
            "FileExistsCore",
            "private bool");
        var readSessionGeneration = ExtractMethod(
            fileSystemSource,
            "ReadSessionGeneration",
            "private string");
        var releaseAmbientLease = ExtractMethod(
            fileSystemSource,
            "ReleaseAmbientCanonicalLease",
            "private void");
        var hasAmbientLease = ExtractMethod(
            fileSystemSource,
            "HasAmbientCanonicalLease",
            "private bool");
        var compactAmbientLeaseStart = fileSystemSource.IndexOf(
            "private AmbientCanonicalLeaseRegistration?",
            StringComparison.Ordinal);
        Assert.True(compactAmbientLeaseStart >= 0);
        var hasAmbientLeaseStart = fileSystemSource.IndexOf(
            "private bool HasAmbientCanonicalLease(",
            compactAmbientLeaseStart,
            StringComparison.Ordinal);
        Assert.True(hasAmbientLeaseStart > compactAmbientLeaseStart);
        var compactAmbientLease =
            fileSystemSource[compactAmbientLeaseStart..hasAmbientLeaseStart];
        var physicalAuthoritySource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Core",
            "PhysicalFileAuthority.cs"));
        var strictDirectoryDelete = ExtractMethod(
            physicalAuthoritySource,
            "TryDeleteDirectoryTree",
            "internal static bool");

        Assert.Contains(
            "ProbeNamespaceEntryFromRoot(",
            fileExistsCore,
            StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists(", fileExistsCore, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Exists(", fileExistsCore, StringComparison.Ordinal);

        Assert.Contains(
            "StrictJsonAuthority.Deserialize<SessionGenerationDocument>",
            readSessionGeneration,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JsonSerializer.Deserialize<SessionGenerationDocument>",
            readSessionGeneration,
            StringComparison.Ordinal);

        var ambientRegistrationIndex = fileSystemSource.IndexOf(
            "new AmbientCanonicalLeaseRegistration(",
            StringComparison.Ordinal);
        Assert.True(ambientRegistrationIndex >= 0);
        var acquisitionCompactionIndex = fileSystemSource.IndexOf(
            "CompactAmbientCanonicalLeaseHead()",
            ambientRegistrationIndex,
            StringComparison.Ordinal);
        Assert.InRange(
            acquisitionCompactionIndex - ambientRegistrationIndex,
            1,
            200);
        Assert.Contains(
            "CompactAmbientCanonicalLeaseHead()",
            releaseAmbientLease,
            StringComparison.Ordinal);
        Assert.Contains("current.Inactive", compactAmbientLease, StringComparison.Ordinal);
        Assert.Contains(
            "cursor.PruneInactivePredecessors()",
            compactAmbientLease,
            StringComparison.Ordinal);
        Assert.Contains(
            "!previous.RegisterSuccessor(this)",
            fileSystemSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "successor.PruneInactivePredecessors();",
            fileSystemSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("!current.Active", compactAmbientLease, StringComparison.Ordinal);
        Assert.Contains("if (current.Active)", hasAmbientLease, StringComparison.Ordinal);
        Assert.Contains("current = current.Previous", hasAmbientLease, StringComparison.Ordinal);
        var registerSuccessor = ExtractMethod(
            fileSystemSource,
            "RegisterSuccessor",
            "private bool");
        Assert.DoesNotContain(
            "PruneInactivePredecessors",
            registerSuccessor,
            StringComparison.Ordinal);

        Assert.Contains(
            "requirePhysicalDirectory: true",
            strictDirectoryDelete,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Phase43AuthorityBoundaries_KeepOwnedPostImagesManifestedSavesAndAncestorFencing()
    {
        var fileSystemSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Core",
            "FileSystemManager.cs"));
        var rollbackSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Services",
            "ExplorerLocalTurnRollbackArtifacts.cs"));
        var physicalAuthoritySource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Core",
            "PhysicalFileAuthority.cs"));
        var reversiblePublicationSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Core",
            "ReversibleFilePublication.cs"));
        var saveLoadSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Services",
            "SaveLoadService.cs"));
        var actorContractSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Services",
            "ActorMaterializationContract.cs"));

        var canonicalWrite = ExtractMethod(
            fileSystemSource,
            "WriteFileAtomicBytesAsync",
            "internal async Task");
        Assert.True(
            canonicalWrite.IndexOf(
                "RecordCanonicalMutationIntentAsync(",
                StringComparison.Ordinal) <
            canonicalWrite.IndexOf(
                "WriteFileAtomicBytesCoreAsync(",
                StringComparison.Ordinal));
        var compareExchange = ExtractMethod(
            fileSystemSource,
            "CompareExchangeFileBytesAsync",
            "internal async Task<CanonicalFileMutationResult>");
        Assert.True(
            compareExchange.IndexOf(
                "RecordCanonicalMutationIntentAsync(",
                StringComparison.Ordinal) <
            compareExchange.IndexOf(
                "DeleteFileCore(",
                StringComparison.Ordinal));
        var canonicalDelete = ExtractMethod(
            fileSystemSource,
            "DeleteFile",
            "internal void");
        Assert.True(
            canonicalDelete.IndexOf(
                "RecordCanonicalMutationIntentAsync(",
                StringComparison.Ordinal) <
            canonicalDelete.IndexOf(
                "DeleteFileCore(",
                StringComparison.Ordinal));

        Assert.Contains(
            "SchemaVersion: 5",
            rollbackSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "PublishedSha256s",
            rollbackSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "DeletionIntended",
            rollbackSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "WriteFileAtomicBytesIfCurrentOwnedAsync(",
            rollbackSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "DeleteFileIfCurrentOwnedAsync(",
            rollbackSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "IsTransactionOwnedPostImage(entry, current)",
            rollbackSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "allowedDestinationSha256s",
            reversiblePublicationSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "denyConcurrentWrites:",
            reversiblePublicationSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "denyConcurrentWrites",
            physicalAuthoritySource,
            StringComparison.Ordinal);

        var observationStart = fileSystemSource.IndexOf(
            "private sealed class InProcessMutationObservation",
            StringComparison.Ordinal);
        var mutationStateStart = fileSystemSource.IndexOf(
            "private sealed class InProcessMutationState",
            observationStart,
            StringComparison.Ordinal);
        Assert.True(observationStart >= 0);
        Assert.True(mutationStateStart > observationStart);
        var observation =
            fileSystemSource[observationStart..mutationStateStart];
        Assert.Contains(
            "string canonicalRoot",
            observation,
            StringComparison.Ordinal);
        Assert.Contains(
            "while (true)",
            observation,
            StringComparison.Ordinal);
        Assert.Contains(
            "AcquireInProcessMutationState(current)",
            observation,
            StringComparison.Ordinal);
        Assert.Contains(
            "current = Path.GetDirectoryName(current)",
            observation,
            StringComparison.Ordinal);

        Assert.Contains(
            "save_manifest.json",
            saveLoadSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "SHA256.HashDataAsync",
            saveLoadSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "StrictJsonAuthority.Deserialize<SaveIntegrityManifest>",
            saveLoadSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "game_state/meta/soul_state.json",
            saveLoadSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "await ValidateArchiveStructureAsync(",
            saveLoadSource,
            StringComparison.Ordinal);

        var activeSkillStart = actorContractSource.IndexOf(
            "private static bool IsUsableMortalActiveCombatSkill",
            StringComparison.Ordinal);
        var passiveSkillStart = actorContractSource.IndexOf(
            "private static bool IsUsableMortalPassiveCombatSkill",
            activeSkillStart,
            StringComparison.Ordinal);
        var teacherSkillStart = actorContractSource.IndexOf(
            "private static bool IsUsableMortalTeacherSkill",
            passiveSkillStart,
            StringComparison.Ordinal);
        Assert.True(activeSkillStart >= 0);
        Assert.True(passiveSkillStart > activeSkillStart);
        Assert.True(teacherSkillStart > passiveSkillStart);
        var activeCombatSkill =
            actorContractSource[activeSkillStart..passiveSkillStart];
        var passiveCombatSkill =
            actorContractSource[passiveSkillStart..teacherSkillStart];
        Assert.DoesNotContain(
            "HasLegacyMortalSkillIdentity",
            activeCombatSkill,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "HasLegacyMortalSkillIdentity",
            passiveCombatSkill,
            StringComparison.Ordinal);
        Assert.Contains(
            "ValidationService.IsProductionValidMortalActiveSkill(skill)",
            activeCombatSkill,
            StringComparison.Ordinal);
        Assert.Contains(
            "ValidationService.IsProductionValidMortalPassiveSkill(skill)",
            passiveCombatSkill,
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
    public void BrowserQteTurnAuthority_UsesPersistedOfferAndRuntimeInsteadOfTurnRequest()
    {
        var webSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "WebUi",
            "QteWebInteractionService.cs"));
        var lifecycleSource = File.ReadAllText(SourcePath(
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            "GameEngine.TurnLifecycle.cs"));
        var offerDecision = ExtractMethod(
            webSource,
            "ResolveOfferDecisionBoundAsync",
            "private async Task<QteWebStateDto>");
        var actionDecision = ExtractMethod(
            webSource,
            "ResolveActionBoundAsync",
            "private async Task<QteWebStateDto>");
        var acceptedOffer = ExtractMethod(
            lifecycleSource,
            "HandleAcceptedQteOfferAsync",
            "private async Task<(bool EarlyExit, GameResponse Response)>");

        Assert.Contains(
            "offer.SourceTurnNumber",
            offerDecision,
            StringComparison.Ordinal);
        Assert.Contains(
            "activeScene.AcceptedAtTurn",
            actionDecision,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ReadCurrentTurnNumberAsync",
            webSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "BindAcceptedTurnAuthorityAsync",
            acceptedOffer,
            StringComparison.Ordinal);
        Assert.Contains(
            "snapshotContext.TurnNumber",
            acceptedOffer,
            StringComparison.Ordinal);
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
