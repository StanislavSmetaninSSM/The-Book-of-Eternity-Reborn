using System.Reflection;
using System.Runtime.ExceptionServices;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private static readonly string[] MetaShapePaths = [
        "game_state/control/life_transitions.json",
        "game_state/control/incarnation_trigger.json",
        "game_state/control/ascension.json",
        "game_state/control/pending_shining_faction_foundings.json",
        "game_state/control/pending_shining_faction_realignments.json",
        "game_state/control/pending_shining_faction_leadership_transitions.json",
        "game_state/control/pending_npc_trade_inventory_requests.json",
        "game_state/control/pending_guardian_abode_residents_request.json",
        "game_state/control/pending_guardian_abode_resident_transfers.json",
        "game_state/control/pending_guardian_abode_resident_interactions.json",
        "game_state/control/pending_resident_companion_manifestation_request.json"
    ];
    private static readonly GameStateValidationSelection MetaShapeSelection =
        new(GameStateValidationPhase.MetaMiscStateFiles, MetaShapePaths);

    private static IReadOnlyDictionary<string, string> MetaShapeMixedInputs() => new Dictionary<string, string> {
        [MetaShapePaths[0]] = "17",
        [MetaShapePaths[1]] = "{",
        [MetaShapePaths[2]] = "[{},\"élan\"]",
        [MetaShapePaths[3]] = "{\"requests\":[{\"requestId\":\"fixed_request\",\"charter\":{\"favoredArchetype\":\"unknown\",\"patronEffectFamily\":\"bad\"}}],\"wrong\":true}",
        [MetaShapePaths[4]] = "",
        [MetaShapePaths[5]] = " \t\r\n",
        [MetaShapePaths[6]] = "{\"requests\":[],\"_fixture\":\"ёж🌙\"}",
        [MetaShapePaths[10]] = "{\"requests\":[{\"requestId\":\"fixed_manifestation\",\"manifestationSource\":\"bad\",\"relicId\":\"fixed_relic\",\"personalityProfile\":{\"coreValues\":[],\"personalityTraits\":[{\"value\":0}]},\"abodeDisposition\":{\"powerSensitivity\":\"bad\"},\"abodeDevotionLevel\":101,\"restlessness\":101,\"abodeDevotionTier\":\"bad\",\"migrationState\":\"bad\"}]}"
    };

    // The actual private synchronous issuer installs its own admission/ExecutionContext.
    // Reflection only selects the DEBUG component entry, never creates an owner or token.
    private static Task<List<ValidationIssue>> InvokeMetaShapeComponent(
        ValidationService validator, GameStateValidationSelection? selection = null)
    {
        var entry = typeof(ValidationService).GetMethod("ValidateMetaControlShapesComponentAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(entry);
        try { return Assert.IsAssignableFrom<Task<List<ValidationIssue>>>(entry.Invoke(validator, [selection ?? MetaShapeSelection])); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("mixed")]
    public async Task MetaControlShapes_WholeEntryProofCeilingPreservesOrderedIssues(string layout)
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            if (layout == "absent")
                Assert.All(MetaShapePaths, path => Assert.False(File.Exists(probe.Files.ResolvePath(path))));
            var before = WorldInputImages(probe.Files);
            var scalar = await InvokeMetaShapeComponent(probe.Validator);
            Assert.Equal(before, WorldInputImages(probe.Files));
            if (layout == "absent") Assert.Empty(scalar);
            else
            {
                Assert.Equal(new[] { "flexible_state_invalid_root", "strict_state_invalid_root" },
                    scalar.Where(issue => issue.FilePath == MetaShapePaths[0]).Select(issue => issue.Code));
                Assert.Equal(new[] { "flexible_state_invalid_json", "strict_state_invalid_json" },
                    scalar.Where(issue => issue.FilePath == MetaShapePaths[1]).Select(issue => issue.Code));
                Assert.Equal(new[] { "strict_state_invalid_root" },
                    scalar.Where(issue => issue.FilePath == MetaShapePaths[2]).Select(issue => issue.Code));
                Assert.DoesNotContain(scalar, issue => issue.FilePath == MetaShapePaths[4] || issue.FilePath == MetaShapePaths[5]);
                Assert.Equal(System.Text.Encoding.UTF8.GetPreamble().Concat(
                    System.Text.Encoding.UTF8.GetBytes(MetaShapeMixedInputs()[MetaShapePaths[6]])).ToArray(),
                    File.ReadAllBytes(probe.Files.ResolvePath(MetaShapePaths[6])));
                Assert.Contains(scalar, issue => issue.Code == "shining_abode_invalid_favored_archetype");
                Assert.Contains(scalar, issue => issue.Code == "guardian_abode_resident_personality_trait_value_out_of_bounds");
            }
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            probe.Arm(); // Before real synchronous admission, through returned issue list and finally.
            List<ValidationIssue>? actual = null;
            Exception? failure = null;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { actual = await InvokeMetaShapeComponent(probe.Validator); }
            catch (Exception error) { failure = error; throw; }
            finally { WorldEvidence(probe, new { MetaControlShapes = layout, before, SourceDeclaredReadCalls = 22,
                InvocationSeconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds,
                ScalarIssues = WorldIssues(scalar), ActualIssues = actual == null ? null : WorldIssues(actual),
                Failure = failure?.ToString(), GuardContextPresent = MetaShapeContextRefs(probe.Validator).Select(value => value != null).ToArray() }); }
            Assert.Equal(before, WorldInputImages(probe.Files));
            Assert.Equal(WorldIssues(scalar), WorldIssues(actual!));
            Assert.False(WorldHasAmbientLease(probe.Files));
            Assert.InRange(probe.PhysicalProofSpans, 1, 6);
        }, additionalInputs: layout == "mixed" ? MetaShapeMixedInputs() : null);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("cancel")]
    public async Task MetaControlShapes_AsyncFailureIsImmediateOriginalAndDoesNotPublish(string kind)
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            Exception primary = kind == "read" ? new IOException("owned Meta async read cut") :
                new OperationCanceledException("owned Meta cancellation cut", new CancellationToken(true));
            var ids = new HashSet<string> { "prior_id" };
            var names = new HashSet<string> { "Prior name" };
            SeedMetaShapeOpeningCaches(probe, ids, names);
            var reached = 0;
            probe.OnRead = path => path == MetaShapePaths[0] && ++reached == 1
                ? Task.FromException(primary) : Task.CompletedTask;
            probe.Arm();
            List<ValidationIssue>? result = null;
            var failure = await Record.ExceptionAsync(async () => { result = await InvokeMetaShapeComponent(probe.Validator); });
            WorldEvidence(probe, new { kind, reached, Failure = failure?.ToString(), SamePrimary = ReferenceEquals(primary, failure), ResultPublished = result != null });
            Assert.Equal(1, reached);
            Assert.Same(primary, failure);
            Assert.Contains("ReadOrdinaryFileSnapshotCoreAsync", failure!.StackTrace);
            Assert.Null(result);
            Assert.True(probe.SeededOpenings > 0);
            Assert.Same(ids, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache"));
            Assert.Same(names, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache"));
            Assert.Equal(new[] { "prior_id" }, ids);
            Assert.Equal(new[] { "Prior name" }, names);
            Assert.False(WorldHasAmbientLease(probe.Files));
        }, additionalInputs: MetaShapeMixedInputs());
    }

    [Fact]
    public async Task MetaControlShapes_RepeatedCurrentDriftNeverPublishesIssues()
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var path = probe.Files.ResolvePath(MetaShapePaths[0]);
            var before = File.ReadAllBytes(path);
            var reads = 0;
            probe.OnAfterRead = relative =>
            {
                // Existing after-attempt hook runs after the first data capture; mutate before strict read.
                if (relative == MetaShapePaths[0] && ++reads == 1)
                    File.WriteAllText(path, "{\"reason\":\"Voluntary\",\"summary\":\"changed\"}");
                return Task.CompletedTask;
            };
            probe.Arm();
            List<ValidationIssue>? result = null;
            Exception? failure;
            try { failure = await Record.ExceptionAsync(async () => { result = await InvokeMetaShapeComponent(probe.Validator); }); }
            finally { probe.OnAfterRead = null; File.WriteAllBytes(path, before); }
            WorldEvidence(probe, new { reads, Failure = failure?.ToString(), ResultPublished = result != null });
            Assert.Null(result);
            Assert.IsAssignableFrom<IOException>(failure);
            Assert.False(WorldHasAmbientLease(probe.Files));
        }, additionalInputs: MetaShapeMixedInputs());
    }

    [Fact]
    public async Task MetaControlShapes_PhaseLeavesNonNullFactionCachesAndGuardianStateUntouched()
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var ids = new HashSet<string> { "prior_id" };
            var names = new HashSet<string> { "Prior name" };
            SeedMetaShapeOpeningCaches(probe, ids, names);
            var outer = MetaShapeContextRefs(probe.Validator);
            probe.Arm();
            var issues = await InvokeMetaShapeComponent(probe.Validator);
            WorldEvidence(probe, new { OrderedIssues = WorldIssues(issues), GuardContextPresent = MetaShapeContextRefs(probe.Validator).Select(value => value != null).ToArray() });
            Assert.True(probe.SeededOpenings > 0);
            Assert.Same(ids, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache"));
            Assert.Same(names, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache"));
            Assert.Equal(new[] { "prior_id" }, ids);
            Assert.Equal(new[] { "Prior name" }, names);
            var after = MetaShapeContextRefs(probe.Validator);
            for (var i = 0; i < outer.Length; i++) Assert.Same(outer[i], after[i]);
            Assert.False(WorldHasAmbientLease(probe.Files));
        }, additionalInputs: MetaShapeMixedInputs());
    }

    [Theory]
    [InlineData("wrong-phase")]
    [InlineData("unknown-path")]
    [InlineData("unrestricted")]
    public async Task MetaControlShapes_ComponentRejectsWidenedDispatchBeforeAdmission(string kind)
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var selection = kind == "wrong-phase" ? new GameStateValidationSelection(GameStateValidationPhase.RequiredFiles, MetaShapePaths) :
                kind == "unknown-path" ? new GameStateValidationSelection(GameStateValidationPhase.MetaMiscStateFiles,
                    MetaShapePaths.Concat(["game_state/control/pending_turn_snapshot.json"])) :
                new GameStateValidationSelection(GameStateValidationPhase.MetaMiscStateFiles);
            probe.Arm();
            var failure = await Record.ExceptionAsync(() => InvokeMetaShapeComponent(probe.Validator, selection));
            WorldEvidence(probe, new { kind, Failure = failure?.ToString() });
            Assert.IsType<ArgumentException>(failure);
            Assert.Equal(0, probe.PhysicalProofSpans);
            Assert.False(WorldHasAmbientLease(probe.Files));
            // Rejected fixed dispatch must not install a real admission or leak shared call count.
            Assert.Empty(await InvokeMetaShapeComponent(probe.Validator));
        });
    }

    [Fact]
    public void MetaControlShapes_SourceClosurePinsRequireReview()
    {
        // Conservative concrete declarations, callbacks, primitive/type initialization,
        // issue const-path classification and real authority boundaries. Not a method roster/analyzer.
        (string Path, string Hash)[] pins = [
            ("BookOfEternityClient/Configuration/SharedJsonOptions.cs", "AF3A89A08074879DE8F9CCA1E1F94BCCADFB944D4D75B8FAA861ED5A0D2562E2"),
            ("BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs", "1DECB1BEB44D2FD0466199E58FF06FB6B39E7795341493FE8DA09A977EC8D202"),
            ("BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs", "384F9F65D33E1C70C6F7E337D96AEC6E71664D142612546139345BF274923A14"),
            ("BookOfEternityClient/Core/FileSystemManager.OrdinaryReads.cs", "03D8FEF5770D2B88C71378249F5A6129BFFEB98E5A1CAA0B422CC919BC4ED4E8"),
            ("BookOfEternityClient/Core/FileSystemManager.WorkerCanonicalFence.cs", "7A857EC50550365F6576B8E2CCC056E662C87622E1A32F652952760761D3E753"),
            ("BookOfEternityClient/Core/FileSystemManager.WorldValidationRead.cs", "A486A6229C80BA88E918E669E3FCE153201DDD29E67C868607FD74F8200AC14A"),
            ("BookOfEternityClient/Core/FileSystemManager.cs", "DA3A5022C5C5AE5A57B7BCE07B01200121576521E18EB7BB742C91894608715D"),
            ("BookOfEternityClient/Core/IncarnationTriggerContract.cs", "D406204075B1C12EA02DF1E0AAEE371CABD7E0887D2A21FC97B156FF6F0E3894"),
            ("BookOfEternityClient/Services/ActorSocialInteractionRequestState.cs", "03E254BE75F21A61C6D045A2121B83DCA89C7F9D9FEB25F98E00361E514867B7"),
            ("BookOfEternityClient/Services/AfterlifeActiveThreatState.cs", "2D3F5A41E9C16E9F00A7380FF91156778907548E4FD4BBCD836CD0437C58E048"),
            ("BookOfEternityClient/Services/AfterlifeArchiveActionState.cs", "5EB55664C0E5BAF335E79DBE534F6438DBAC68AB39DAC512393BCA8691A39246"),
            ("BookOfEternityClient/Services/AfterlifeArchiveCandidateService.cs", "AB38D24FA9EE1BD4CD683BAD5CCF425B5768400477B972942D8EB1B2F26B7B24"),
            ("BookOfEternityClient/Services/AfterlifeChronicleState.cs", "89349D326AA8387D5CD60E41B0BDC9AE115016B6D251D2AEE0CFBC0B830B1E90"),
            ("BookOfEternityClient/Services/AfterlifeContractRegistry.cs", "BAF73C92D63578063E92904277503DCC3322CD57C7DB38F47734FA5C4A181C7A"),
            ("BookOfEternityClient/Services/AfterlifeEntityProfileState.cs", "13B716101E4F111435E5A7BF5FE5FA0083B4904B60A0CEC920D9512B6EFCC885"),
            ("BookOfEternityClient/Services/AfterlifeGlobalFlagState.cs", "A691456B31CEF9901855C1351BB936084BDA65724CD0A055C6353FF8C9ECF171"),
            ("BookOfEternityClient/Services/AfterlifeNotificationState.cs", "E2A7998461720231E3A8F708AE5BD899419034485FC49B7EEED6E703E80991C2"),
            ("BookOfEternityClient/Services/AfterlifeReturnGuardService.cs", "1E10DDC29700AA3C312DE010A89A3A033891518F3200BBB5C589B313DC601EE3"),
            ("BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs", "5D6B770BDF7A4BDE1429ADDBB89CD8354FEE99303716DC4EF8106E128EEC5EBC"),
            ("BookOfEternityClient/Services/AfterlifeStoryOutlineState.cs", "397E7AFB201297296F2289A11222F19B0679A82CCBD36C5BB5110325091C8911"),
            ("BookOfEternityClient/Services/ChaosSeaGuardianPoliticsState.cs", "97EA600156C907B064C57E1154DCB2A486207F59C859015083DD069EBF8445B0"),
            ("BookOfEternityClient/Services/CraftRequestState.cs", "4C9324BF07052F145E34483F958306D1A04D111C56DDFC515526048D91EA15DB"),
            ("BookOfEternityClient/Services/GuardianAbodeOfferingState.cs", "94CAA7D66133344BB088050CE87F5CD1815E72B19109BF21446B918392803DB4"),
            ("BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs", "4C7BCDB716281E961B5882B10D1D378C5804BC52C2A7285F56EC1E329E80EBE7"),
            ("BookOfEternityClient/Services/GuardianAbodeResidentState.cs", "84ED91EFEA8EB31538CAC667100A99F70AAB2EBA2F86D5F3153022796544BC42"),
            ("BookOfEternityClient/Services/GuardianCorrectionService.cs", "4BB3F34DE7DC2341CF7C7481A5787158B557085F091571F726BE8D0963F113B5"),
            ("BookOfEternityClient/Services/GuardianTradeRequestState.cs", "5B69B4A71660BD9BCC899B6B184AACCBFA877E0C325DB5E9A58B738A22822B3D"),
            ("BookOfEternityClient/Services/MortalItemIdentityState.cs", "50034BA7D600EA21E8DA0C34C878719B6CDFC2BB28FB532FE869E3094FD801D2"),
            ("BookOfEternityClient/Services/MortalItemRepairPacketBuilder.cs", "7FD2A3E3AE98F9D5BEBAE6A31CBFB95625A26733E105B870A7789A6F46E9F741"),
            ("BookOfEternityClient/Services/MortalLocationIdentityState.cs", "CCFB17A09FD2591179E502FE8B0CDA3FC10F74B6E50D7A593FEAE78ABE35C19C"),
            ("BookOfEternityClient/Services/MortalLocationStorageContentsState.cs", "27D01910E64B2E8E13CFED19E2FCE7FFBADABA7BBC78BFED71F415A07EFF2A25"),
            ("BookOfEternityClient/Services/NpcTradeRequestState.cs", "24304EB1D54546701601E77C94267A639F324073553C907F15701682D9738C49"),
            ("BookOfEternityClient/Services/NpcTradeService.Data.cs", "F1E9C6141476CC4FF815BADCCB7F6CCD29F84F9E7CAC0AA6B96C631119D3FBE4"),
            ("BookOfEternityClient/Services/NpcTradeService.cs", "C7F569A8E7E5C2B0732D935E2642FA5B6BEB8F3EFA252C3B3EBC67469025BA57"),
            ("BookOfEternityClient/Services/PendingTurnSnapshotAuthority.cs", "F36C095DE1746DA11BA50DC0296CAE9B238C3A11AD1B40E144C4DC0212267B13"),
            ("BookOfEternityClient/Services/PlayerGuardianFoundationState.cs", "0BBF9E8D3D810CEBF3E302A0D9CDC30E836C697B2B32E5F16F07A382A728BB8D"),
            ("BookOfEternityClient/Services/ProgressionScheduleService.cs", "CAE67D35831C5CD67DF85FC049657FB51ED400BA1749B984F0E4C6331AFBEAF9"),
            ("BookOfEternityClient/Services/QteSceneService.cs", "5A5FC696E72DF7B4C0970EAB6A5B371918F749A94AD915485C1356CD8484CEBA"),
            ("BookOfEternityClient/Services/RealmSegregationAutoRollbackService.cs", "DA6FBD7AD4792D9FD7A53F3BC4D9ADCBD7E3C943850773CE6F91182997629289"),
            ("BookOfEternityClient/Services/SarefMainStoryState.cs", "E0FCEB10F412B90FD07A696D55362C66DA4272A730B6095A9F9E09E516861D2C"),
            ("BookOfEternityClient/Services/ScenarioCoreService.cs", "9C5A20E4DB01DC66C6F38908AF0F541611D6AFC0E7FA6D239424B48D4F5F4088"),
            ("BookOfEternityClient/Services/ShiningAbodeState.FactionPoliticalMemory.cs", "0FB30EFDFD32BF4D68A91984A8DCDD07559D99D9E685A7C3E1C70D8DF139FCC4"),
            ("BookOfEternityClient/Services/ShiningAbodeState.Gacha.cs", "DC58B9E08F6BA3E49EEDD26440ED863675DA95D0AF56A1B964F3E7026F56364B"),
            ("BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs", "40B4BBBED214A25FAC9DB938D43DA32ED1B6EB4322C1A8C8A95696CDD5E9DD47"),
            ("BookOfEternityClient/Services/ShiningAbodeState.TradeAndForge.cs", "9415D4EEB4F77829A5BDF3F944479D15F89D3AD76BDACA8452639CC1054A2B52"),
            ("BookOfEternityClient/Services/ShiningAbodeState.Treasury.cs", "EAEB1A199079A697DEE1AAD48962AF509D3B482C72D19129DA9EB5A4CFCA1817"),
            ("BookOfEternityClient/Services/ShiningAbodeState.cs", "940ED36904BE677527CFB8D5FD714BA53F5A20AB7700B54407903907E6D19873"),
            ("BookOfEternityClient/Services/ShiningCoreActionRequestState.cs", "39F804C0CCD0681D18E6D4022702EFBCE1C307F0F107401A264E86685B9A6374"),
            ("BookOfEternityClient/Services/ShiningFactionRequestState.cs", "5D6F11EAC11A25FEC640F5377FA31701F72B3D5686BC16876EC038442FF98DC1"),
            ("BookOfEternityClient/Services/ShiningTradeRequestState.cs", "D24FC24C1CAF0E8C84088DB3AB507BC9E34537A97EE4E42DC3F7F0AE2E1A992E"),
            ("BookOfEternityClient/Services/SourceOfLightCapstoneState.cs", "E24F7A29B56C3342DA93370E90D387555F63F7C6BC1476D63029FB6603778164"),
            ("BookOfEternityClient/Services/SpiritualWoundCaptureCheckpointState.cs", "CD0FB7B5EB34C2F6D60973CE2EE2C56C1C50ACA2797438B9613B6C576A2B7EEE"),
            ("BookOfEternityClient/Services/SpiritualWoundDecisionPendingState.cs", "6FFCACA917ACC7FFBA066EBC9440EE0FEE08C230F869A3C0C140B82EBFB58DC7"),
            ("BookOfEternityClient/Services/SpiritualWoundOpportunityReceiptState.cs", "891DD804D226782D86CAB7C7A81D739EAFF2BB1366730D2E64913187549D7608"),
            ("BookOfEternityClient/Services/SystemGuardianLibraryService.cs", "74CAB4A0350770FCBEB1B1D783C3BCA603BAB7CB78660829DF902E1C4BE5F1A5"),
            ("BookOfEternityClient/Services/TrainingRequestState.cs", "524B30A6C574B22C19274120E45D55683B3BED98163107C10E5A314F334F1DF8"),
            ("BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs", "F6CE6B4767D130BB8F71C7A77742CB00AEAD9A3BF4525352203C9E085C164918"),
            ("BookOfEternityClient/Services/Validation/ValidationService.GuardiansAndAfterlife.cs", "0CC60E658442A70540C489FAE2E0E69644CA1AAE9D8DAE987232899C95C0B6AE"),
            ("BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs", "C494D49C017336596DF49F7A44E6D9A10172FD035268E9A3DD92B43B5F78A938"),
            ("BookOfEternityClient/Services/Validation/ValidationService.MetaControlShapeReadPhase.cs", "C2AC27AC1A5C731D3E90101E2413AA5C9CEB9F1D9AA105443E9D7D1001205F35"),
            ("BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs", "9581356324418C0867A0F947D88CF3347D06D8B635C1075A9B3004B1C8384635"),
            ("BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs", "28D7114A73FEFA461D4E8502FD88D2D661C4A664F26A9767840556D1B0F37C31"),
            ("BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs", "54222A4E3EDE3574E4A489C88F8C085A015408DDEE4C6F9531ECF87D0D5AF907"),
            ("BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs", "96042BF2E790C80932810689D5DC0EE2F13ED567C2BE47487EFFA05C6730C0A0"),
            ("BookOfEternityClient/Services/Validation/ValidationService.SameTurnOwnerReadView.cs", "E2A4B238596AF1FE336B25BD340CB0A2199758F476DB9BD76A16D45EA22ACAC7"),
            ("BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs", "081B4FD60653F4AC130B4598C232AB7CBA233EBEC17722D766DDC92461269B35"),
            ("BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs", "314858FA04D4A401066489BB4664455BB1CC81154FBD6AE1E4E24F2AE3281555"),
            ("BookOfEternityClient/Services/Validation/ValidationService.WorldReadPhase.cs", "74D310EA39A294A102FB4EF19EAADBE52DBD21581457E2C6E96114D1FF8AD869"),
            ("BookOfEternityClient/Services/ValidationService.cs", "58511D428841000B0BF4AA4AAF5E28B239457DF199D25AE91AB37F85358E9062"),
            ("BookOfEternityClient/Services/WorldDirectiveService.cs", "1E272E2A48AC49EB82894C917FA3476D24C568BFE9C903B358CB56FE3529C2C0"),
        ];
        foreach (var (path, expected) in pins)
            Assert.Equal(expected, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(FindRepoFile(path)))));
        var source = File.ReadAllText(FindRepoFile("BookOfEternityClient/Services/Validation/ValidationService.MetaControlShapeReadPhase.cs"));
        foreach (var path in MetaShapePaths) Assert.Contains('"' + path + '"', source);
    }

    private static void SeedMetaShapeOpeningCaches(WorldReadProbe probe, HashSet<string> ids, HashSet<string> names)
    {
        SeedWorldOpeningCaches(probe, ids, names);
        var seed = probe.OnOpened!;
        probe.OnOpened = () =>
        {
            var entered = MetaShapeContextRefs(probe.Validator);
            // Observation only: this callback does NOT assign an AsyncLocal or claim propagation.
            Assert.Null(entered[0]);
            Assert.Null(entered[1]);
            return seed();
        };
    }

    private static object?[] MetaShapeContextRefs(ValidationService validator)
    {
        object? Value(string name)
        {
            var local = GetPrivateFieldValue<object>(validator, name);
            return local.GetType().GetProperty("Value")!.GetValue(local);
        }
        return [Value("_gameStateValidationGuardianPolicyContextCache"),
            Value("_gameStateValidationGuardianProjectTrackerPolicyContextCache"),
            GetPrivateFieldValue<object?>(validator, "_guardianPolicyContextInProgress"), Value("_guardianMusingsPublication")];
    }
}
