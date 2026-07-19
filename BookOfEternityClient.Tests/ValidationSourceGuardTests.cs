using System.Reflection;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ValidationSourceGuardTests
{
    [Fact]
    public void ActorMaterializationAuthority_MustNotUseProseOrGenreKeywordInference()
    {
        var validationDirectory = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation");
        var sourceFiles = new[]
            {
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "ActorMaterializationContract.cs")
            }
            .Concat(Directory.EnumerateFiles(
                validationDirectory,
                "ValidationService.ActorMaterialization*.cs",
                SearchOption.TopDirectoryOnly))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var source = string.Join(Environment.NewLine, sourceFiles.Select(File.ReadAllText));

        Assert.Contains(
            sourceFiles,
            path => path.EndsWith("ValidationService.ActorMaterializationTradeAuthority.cs", StringComparison.Ordinal));

        var proseAuthorityRead = new Regex(
            "(?:TryGetProperty|ReadActorMaterializationString|TryReadExactNonEmptyString)\\s*\\([^\\r\\n;]*\\\"(?:displayName|name|description|occupation|profession|tags|history)\\\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var proseStringMatching = new Regex(
            "(?:(?:displayName|name|description|occupation|profession|tags|history|role|genre)\\w*\\s*\\.\\s*(?:Contains|StartsWith|EndsWith|IndexOf)\\s*\\(|(?:Contains|StartsWith|EndsWith|IndexOf)\\s*\\(\\s*(?:displayName|name|description|occupation|profession|tags|history|role|genre)\\w*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var genreKeywordTable = new Regex(
            "(?:keyword|genre|fantasy|science.?fiction|post.?apoc|occupation|profession)[^\\r\\n]*(?:Dictionary|HashSet)|(?:Dictionary|HashSet)[^\\r\\n]*(?:keyword|genre|fantasy|science.?fiction|post.?apoc|occupation|profession)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.DoesNotMatch(proseAuthorityRead, source);
        Assert.DoesNotMatch(proseStringMatching, source);
        Assert.DoesNotMatch(genreKeywordTable, source);
    }

    [Fact]
    public void PlayerFacingActorViews_MustNotReferencePrivateMaterializationMetadata()
    {
        var sourceRoots = new[]
        {
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "src")
        };
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".ts", ".tsx", ".js", ".jsx"
        };
        var privateTokens = new[]
        {
            "materializationId",
            "materializedAtTurn",
            "empty_by_design",
            "actor_materialization_"
        };

        foreach (var sourceRoot in sourceRoots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                         .Where(file => extensions.Contains(Path.GetExtension(file))))
            {
                var source = File.ReadAllText(file);
                foreach (var token in privateTokens)
                {
                    Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    [Fact]
    public void ClientOwnedSurfaceFilter_MustCoverAllValidatedAfterlifePendingContracts()
    {
        var method = typeof(ValidationService).GetMethod(
            "IsClientOwnedSurfaceValidationPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var paths = new[]
        {
            GuardianAbodeOfferingState.PendingRequestPath,
            GuardianTradeRequestState.PendingRequestPath,
            PlayerGuardianFoundationState.PendingRequestPath,
            NpcTradeRequestState.PendingRequestPath,
            CraftRequestState.PendingRequestPath,
            AfterlifeArchiveActionState.ConsultationRequestPath,
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
            ActorSocialInteractionRequestState.PendingGuardianRequestPath,
            ActorSocialInteractionRequestState.PendingNpcRequestPath,
            SystemGuardianLibraryService.AttractionRequestPath,
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            ShiningTradeRequestState.PendingRequestsPath,
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            ShiningFactionRequestState.PendingRealignmentsRequestPath,
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            SourceOfLightCapstoneState.PendingRequestPath
        };

        foreach (var path in paths)
        {
            var isClientOwned = Assert.IsType<bool>(method.Invoke(null, new object[] { path }));
            Assert.True(isClientOwned, $"{path} must be excluded from generic tracked-file validation and handled by the client-owned contract validator.");
        }
    }

    [Fact]
    public void PreTurnRealmResolution_MustNotFallbackToCurrentRealm()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.NpcWorldAndMeta.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("return await TryResolveCurrentRealmAsync();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryGates_PreviousLegacyRead_MustUseValidatedSnapshotInsteadOfConventionalSnapshotCopy()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("const string snapshotPath = \"game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json\";", source, StringComparison.Ordinal);
        Assert.Contains("ReadValidatedCurrentPreTurnTrackedFileAsync(\"game_state/meta/soul_state.json\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericPreTurnTrackedReads_MustUseValidatedSnapshotInsteadOfRawRollbackBackups()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("return await ReadValidatedCurrentPreTurnTrackedFileAsync(relativePath);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncPreTurnTrackedReads_MustUseValidatedSnapshotInsteadOfRawRollbackBackups()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("return ReadValidatedCurrentPreTurnTrackedFileSync(relativePath);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BootstrapAndRealmSegregation_MustUseValidatedPendingSnapshotManifestForSourceLabelAuthority()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.BootstrapAndProtocol.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("var manifest = await LoadValidatedCurrentPendingTurnSnapshotManifestAsync();", source, StringComparison.Ordinal);
        Assert.Contains("LoadRequiredValidatedCurrentPendingTurnSnapshotManifestAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffAgainstManifest_MustUseValidatedSnapshotFileInsteadOfRawRollbackBackup()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("DescribeTrackedFileChangeAgainstManifestAsync", source, StringComparison.Ordinal);
        Assert.Contains("var previous = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, relativePath);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("manifest.RollbackBackups.TryGetValue(relativePath, out var backupPath)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffAgainstManifest_MustNotInferChangedFromMissingValidatedBaselineHeuristic()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("if (previous == null)\r\n            return !string.IsNullOrWhiteSpace(current);", source, StringComparison.Ordinal);
        Assert.Contains("ValidatedTrackedFileChangeStatus.MissingValidatedBaseline", source, StringComparison.Ordinal);
        Assert.Contains("if (IsTrackedByValidatedBaseline(manifest, relativePath))", source, StringComparison.Ordinal);
        Assert.Contains("? ValidatedTrackedFileChangeStatus.Changed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatedPendingSnapshotManifest_MustRequireUsableStructure()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(", source, StringComparison.Ordinal);
        Assert.Contains("static snapshotManifest => snapshotManifest.RollbackBaselineFiles", source, StringComparison.Ordinal);
        Assert.Contains("static snapshotManifest => snapshotManifest.RollbackBackups", source, StringComparison.Ordinal);
        Assert.Contains("ReadRelativeFileFromWorkspace", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PendingTurnSnapshotAuthority.TryValidateManifestAgainstAuthority(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SarefMainStoryState_MustBeOptionalCanonicalBaseline()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            "GameEngine.SessionAndSnapshots.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("if (_fs.FileExists(SarefMainStoryState.StatePath))", source, StringComparison.Ordinal);
        Assert.Contains("TryAddOptionalCanonicalBaselineSnapshotAsync(", source, StringComparison.Ordinal);
        Assert.Contains("SarefMainStoryState.StatePath))", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PolicySensitiveSnapshotAuthorityConsumers_MustUseRollbackBackedParity()
    {
        var normalizerPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "CanonicalStateNormalizer",
            "CanonicalStateNormalizer.SoulAndMeta.cs");
        var guardianPowerPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "GuardianPowerEventState.cs");

        var normalizerSource = File.ReadAllText(normalizerPath);
        var guardianPowerSource = File.ReadAllText(guardianPowerPath);

        Assert.Contains("PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(", normalizerSource, StringComparison.Ordinal);
        Assert.Contains("JsonObject? GachaBaseResult", normalizerSource, StringComparison.Ordinal);
        Assert.Contains("static snapshotManifest => snapshotManifest.RollbackBackups", normalizerSource, StringComparison.Ordinal);
        Assert.Contains("relativePath => ReadRelativeFileFromWorkspace(fs, relativePath)", normalizerSource, StringComparison.Ordinal);

        Assert.Contains("PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(", guardianPowerSource, StringComparison.Ordinal);
        Assert.Contains("JsonObject? GachaBaseResult", guardianPowerSource, StringComparison.Ordinal);
        Assert.Contains("static snapshotManifest => snapshotManifest.RollbackBackups", guardianPowerSource, StringComparison.Ordinal);
        Assert.Contains("relativePath => ReadRelativeFileFromWorkspace(fs, relativePath)", guardianPowerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentDetachedAuthority_MustNotFallbackToReadySignalPresence()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("|| _fs.FileExists(\"ready/turn_complete.json\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingResolutionSnapshotRegistration_MustNotTreatRollbackBackupsAsAuthoritySignal()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("manifest.RollbackBackups.ContainsKey(relativePath)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingResolutionContractRead_MustNotUseRawSnapshotEvidenceSignals()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("hasConventionalSnapshotCopy", source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasRawManifestReference", source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasDeletedCurrentRequestSnapshotResidue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("rawManifestJson?.Contains(relativePath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasParseableManifestBaselineEvidence", source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasCorroboratedManifestBaselineEvidence", source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasCorroboratedManifestSnapshotRegistration", source, StringComparison.Ordinal);
        Assert.Contains("hasDeletedCurrentRequestRecoveryBridgeCandidate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RepairReadyValidation_MustHonorStructuredDiagnosticOnlyRepairRequestMetadata()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.BootstrapAndProtocol.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("metadataDiagnosticOnly", source, StringComparison.Ordinal);
        Assert.Contains("repair_ready_against_diagnostic_only_request", source, StringComparison.Ordinal);
        Assert.Contains("BuildInvalidRepairReadyRepairHint(requestJson, requireJsonObject: true)", source, StringComparison.Ordinal);
        Assert.Contains("BuildInvalidRepairReadyRepairHint(requestJson, requireJsonObject: false)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("gmInstructions.Contains(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuredActorExtraction_MustIncludeResidentUpdatesAndCanonicalResidentDiffs()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.NpcWorldAndMeta.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("await CollectStructuredResidentUpdatesAsync(result.Updates);", source, StringComparison.Ordinal);
        Assert.Contains("ActorType = \"Resident\"", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeResidentState.UpdateProperty", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeResidentState.UpdateThoughtJournalProperty", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeResidentState.UpdateInteractionLogProperty", source, StringComparison.Ordinal);
        Assert.Contains("CollectResidentCanonicalDiffStructuredActorTouches", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidentLifecycleValidation_MustRequireCuratedMemoryForMeaningfulDevotionShifts()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("abode_resident_devotion_shift_missing_memory_update", source, StringComparison.Ordinal);
        Assert.Contains("ResidentHasNewThoughtOrInteractionMemory", source, StringComparison.Ordinal);
        Assert.Contains("Math.Abs(currentDevotionLevel - previousDevotionLevel) >= 8", source, StringComparison.Ordinal);
        Assert.Contains("Math.Abs(currentRestlessness - previousRestlessness) >= 8", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidentLifecycleValidation_MustEnforceCanonicalDriftTriggersAndProjection()
    {
        var validationPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var residentStatePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "GuardianAbodeResidentState.cs");

        var validationSource = File.ReadAllText(validationPath);
        var residentStateSource = File.ReadAllText(residentStatePath);

        Assert.Contains("abode_resident_devotion_shift_missing_canonical_trigger", validationSource, StringComparison.Ordinal);
        Assert.Contains("abode_resident_devotion_projection_mismatch", validationSource, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeResidentState.BuildCanonicalDriftContext(", validationSource, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeResidentState.ProjectCanonicalAbodeDrift(", validationSource, StringComparison.Ordinal);

        Assert.Contains("public sealed class ResidentAbodeDriftContext", residentStateSource, StringComparison.Ordinal);
        Assert.Contains("public sealed class ResidentAbodeDriftProjection", residentStateSource, StringComparison.Ordinal);
        Assert.Contains("BuildCanonicalDriftContext(", residentStateSource, StringComparison.Ordinal);
        Assert.Contains("ProjectCanonicalAbodeDrift(", residentStateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidentTransferValidation_MustRequireValidatedPreTurnEligibilityAndAcceptedTransferBypass()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("abode_resident_transfer_invalid_preturn_eligibility", source, StringComparison.Ordinal);
        Assert.Contains("TryResolveEligiblePreTurnTransferResident(", source, StringComparison.Ordinal);
        Assert.Contains("CollectAcceptedTransferArrivalResidentIds(", source, StringComparison.Ordinal);
        Assert.Contains("!acceptedTransferArrivalResidentIds.Contains(residentId)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidentTransferCompetitionMetadata_MustBeValidatedAndAllowedByLifecycle()
    {
        var lifecyclePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var guardiansPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.GuardiansAndAfterlife.cs");
        var lifecycleSource = File.ReadAllText(lifecyclePath);
        var guardiansSource = File.ReadAllText(guardiansPath);

        Assert.Contains("\"selectionMode\", \"competitionScore\", \"competitionLabel\", \"competitionReason\"", lifecycleSource, StringComparison.Ordinal);
        Assert.Contains("pending_abode_resident_transfer_invalid_selection_mode", guardiansSource, StringComparison.Ordinal);
        Assert.Contains("pending_abode_resident_transfer_invalid_competition_label", guardiansSource, StringComparison.Ordinal);
        Assert.Contains("pending_abode_resident_transfer_inconsistent_selection_metadata", guardiansSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CompanionSeedValidation_MustHonorResidentPersonalityAndAbodeSnapshotFields()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.QuestsRivalsFactionsAndWorld.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("ValidateResidentCompanionSnapshotFields(companionSeed, $\"{context}.companionSeed\", issues, section);", source, StringComparison.Ordinal);
        Assert.Contains("companion_seed_invalid_power_sensitivity", source, StringComparison.Ordinal);
        Assert.Contains("companion_seed_abode_devotion_tier_mismatch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingManifestationRequestValidation_MustHonorResidentPersonalityAndAbodeSnapshotFields()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.GuardiansAndAfterlife.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("ValidateResidentCompanionSnapshotFields(request, requestContext, issues);", source, StringComparison.Ordinal);
        Assert.Contains("ValidateResidentPersonalityProfileObject(personalityProfile", source, StringComparison.Ordinal);
        Assert.Contains("ValidateResidentAbodeDispositionObject(abodeDisposition", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningPendingRequestValidation_MustAcceptRussianRealmAlias()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.ShiningAbode.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("IsSupportedShiningRealm", source, StringComparison.Ordinal);
        Assert.Contains("Сияющая Обитель", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!string.Equals(currentRealm, \"Shining Abode\", StringComparison.OrdinalIgnoreCase) ||", source, StringComparison.Ordinal);
    }
}
