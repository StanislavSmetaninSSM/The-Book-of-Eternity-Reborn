using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundHistoryStateTests
{
    private const string Path = "history";
    private const string FirstRowPath = Path + ".transitions[0]";

    public static TheoryData<string> TransitionKinds => new()
    {
        "create",
        "worsen",
        "complicate",
        "diagnose",
        "author_alternative_treatment",
        "stabilize",
        "treat",
        "recover",
        "heal",
        "legacy",
        "archive"
    };

    [Fact]
    public void Parse_EmptyVersionOneHistoryHasExactNextOrdinalAndIndexedEmptyState()
    {
        var result = Parse(History());

        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        var state = Assert.IsType<WoundHistoryState>(result.State);
        Assert.Equal(1, state.NextOrdinal);
        Assert.Empty(state.Transitions);
        Assert.False(state.TryResolveExactTransition("missing", out _));
        Assert.False(state.TryResolveExactOperation("missing", out _));
    }

    [Fact]
    public void Parse_RootAndRowsAreClosedCompleteVersionOneObjects()
    {
        foreach (var json in new string?[] { null, string.Empty, " ", "[]", "null", "{" })
            AssertInvalid(WoundHistoryState.Parse(json, Path), Path);

        foreach (var field in new[] { "schemaVersion", "nextOrdinal", "transitions" })
        {
            var root = History(Transition());
            root.Remove(field);
            AssertInvalid(Parse(root), Path + "." + field);
        }

        foreach (var invalidVersion in new JsonNode?[] { 0, 2, "1", null })
        {
            var root = History();
            root["schemaVersion"] = invalidVersion?.DeepClone();
            AssertInvalid(Parse(root), Path + ".schemaVersion", "wound_history_invalid_field");
        }

        var unknownRoot = History();
        unknownRoot["entries"] = new JsonArray();
        AssertInvalid(Parse(unknownRoot), Path + ".entries", "wound_history_unknown_field");

        var transitionsAsObject = History();
        transitionsAsObject["transitions"] = new JsonObject();
        AssertInvalid(Parse(transitionsAsObject), Path + ".transitions", "wound_history_invalid_field");

        foreach (var invalidNextOrdinal in new JsonNode?[] { 0, -1, 1.5, "1", null })
        {
            var root = History();
            root["nextOrdinal"] = invalidNextOrdinal?.DeepClone();
            AssertInvalid(Parse(root), Path + ".nextOrdinal", "wound_history_invalid_field");
        }

        var requiredRowFields = new[]
        {
            "transitionId", "woundId", "ordinal", "woundTransitionOrdinal", "kind",
            "turn", "eventRef", "operationKey", "beforeFingerprint", "afterFingerprint",
            "sourceFingerprint", "attemptId", "courseId", "courseMilestoneOrdinal",
            "cycleKey", "paymentFingerprint", "outputFingerprint", "readableSummary",
            "terminal"
        };
        foreach (var field in requiredRowFields)
        {
            var row = Transition();
            row.Remove(field);
            AssertInvalid(Parse(History(row)), FirstRowPath + "." + field);
        }

        var unknownRow = Transition();
        unknownRow["legacyState"] = "active";
        AssertInvalid(
            Parse(History(unknownRow)),
            FirstRowPath + ".legacyState",
            "wound_history_unknown_field");

        var nonObjectRow = History();
        nonObjectRow["nextOrdinal"] = 2;
        nonObjectRow["transitions"] = new JsonArray("invalid");
        AssertInvalid(Parse(nonObjectRow), FirstRowPath, "wound_history_invalid_transition");
    }

    [Fact]
    public void Parse_DuplicateRawJsonPropertiesAreRejectedRecursively()
    {
        var json = History(Transition()).ToJsonString();
        var duplicateRoot = json.Replace(
            "\"schemaVersion\":1",
            "\"schemaVersion\":1,\"schemaVersion\":1",
            StringComparison.Ordinal);
        var duplicateRow = json.Replace(
            "\"transitionId\":\"transition_1\"",
            "\"transitionId\":\"transition_1\",\"transitionId\":\"forged\"",
            StringComparison.Ordinal);

        AssertInvalid(
            WoundHistoryState.Parse(duplicateRoot, Path),
            Path + ".schemaVersion",
            "wound_history_duplicate_property");
        AssertInvalid(
            WoundHistoryState.Parse(duplicateRow, Path),
            FirstRowPath + ".transitionId",
            "wound_history_duplicate_property");
    }

    [Fact]
    public void Parse_RejectsWrongScalarTypesAndLegacyFingerprintShapes()
    {
        foreach (var (field, invalid) in new (string, JsonNode?)[]
                 {
                     ("ordinal", "1"),
                     ("woundTransitionOrdinal", 1.5),
                     ("turn", -1),
                     ("terminal", "false"),
                     ("attemptId", new JsonObject()),
                     ("courseId", new JsonObject()),
                     ("courseMilestoneOrdinal", 0),
                     ("cycleKey", new JsonObject()),
                     ("paymentFingerprint", "sha256:invalid"),
                     ("outputFingerprint", null),
                     ("beforeFingerprint", null),
                     ("afterFingerprint", "wound-fingerprint-test-001"),
                     ("sourceFingerprint", "source-fingerprint-test-001")
                 })
        {
            var row = Transition();
            row[field] = invalid?.DeepClone();
            AssertInvalid(Parse(History(row)), FirstRowPath + "." + field);
        }
    }

    [Fact]
    public void Parse_CourseMilestoneRequiresExactCourseIdentity()
    {
        var row = Transition(courseMilestoneOrdinal: 1);

        AssertInvalid(
            Parse(History(row)),
            FirstRowPath + ".courseId",
            "wound_history_course_coordinate_incomplete");
    }

    [Fact]
    public void Parse_CourseIdentityRequiresExactMilestoneOrdinal()
    {
        var row = Transition(courseId: "mortal_wound_course_test_001");

        AssertInvalid(
            Parse(History(row)),
            FirstRowPath + ".courseMilestoneOrdinal",
            "wound_history_course_coordinate_incomplete");
    }

    [Fact]
    public void Parse_GlobalOrdinalsAndNextOrdinalAreExactContiguousAndInArrayOrder()
    {
        var first = Transition();
        var second = Transition(
            transitionId: "transition_2",
            operationKey: "operation_2",
            ordinal: 2,
            woundTransitionOrdinal: 2,
            kind: "diagnose",
            turn: 43,
            beforeFingerprint: Fingerprint('a'),
            afterFingerprint: Fingerprint('b'));

        Assert.True(Parse(History(first, second)).IsValid);

        foreach (var invalid in new[]
                 {
                     HistoryWithNextOrdinal(2, first, second),
                     HistoryWithNextOrdinal(4, first, second),
                     History(
                         Transition(ordinal: 2),
                         Transition(
                             transitionId: "transition_2",
                             operationKey: "operation_2",
                             ordinal: 1,
                             woundTransitionOrdinal: 2,
                             kind: "diagnose",
                             turn: 43,
                             beforeFingerprint: Fingerprint('a'),
                             afterFingerprint: Fingerprint('b'))),
                     History(
                         first,
                         Transition(
                             transitionId: "transition_2",
                             operationKey: "operation_2",
                             ordinal: 3,
                             woundTransitionOrdinal: 2,
                             kind: "diagnose",
                             turn: 43,
                             beforeFingerprint: Fingerprint('a'),
                             afterFingerprint: Fingerprint('b')))
                 })
        {
            AssertInvalid(Parse(invalid), Path, "wound_history_ordinal_discontinuity");
        }
    }

    [Fact]
    public void Parse_PerWoundOrdinalsRemainContiguousWhenRowsInterleave()
    {
        var a1 = Transition(woundId: "wound_a");
        var b1 = Transition(
            transitionId: "transition_2",
            woundId: "wound_b",
            operationKey: "operation_2",
            ordinal: 2);
        var a2 = Transition(
            transitionId: "transition_3",
            woundId: "wound_a",
            operationKey: "operation_3",
            ordinal: 3,
            woundTransitionOrdinal: 2,
            kind: "treat",
            turn: 43,
            beforeFingerprint: Fingerprint('a'),
            afterFingerprint: Fingerprint('b'));
        var b2 = Transition(
            transitionId: "transition_4",
            woundId: "wound_b",
            operationKey: "operation_4",
            ordinal: 4,
            woundTransitionOrdinal: 2,
            kind: "recover",
            turn: 44,
            beforeFingerprint: Fingerprint('a'),
            afterFingerprint: Fingerprint('c'));

        Assert.True(Parse(History(a1, b1, a2, b2)).IsValid);

        b2["woundTransitionOrdinal"] = 3;
        AssertInvalid(
            Parse(History(a1, b1, a2, b2)),
            Path,
            "wound_history_wound_ordinal_discontinuity");
    }

    [Fact]
    public void Parse_GlobalAppendChronologyNeverMovesToAnEarlierTurn()
    {
        var later = Transition(woundId: "wound_a", turn: 43);
        var earlier = Transition(
            transitionId: "transition_2",
            woundId: "wound_b",
            operationKey: "operation_2",
            ordinal: 2,
            turn: 42);

        AssertInvalid(
            Parse(History(later, earlier)),
            Path,
            "wound_history_turn_regression");
    }

    [Fact]
    public void Parse_FirstRowUsesWoundBoundNonexistentFingerprintAndChainIsExact()
    {
        var first = Transition(woundId: "wound_a");
        var second = Transition(
            transitionId: "transition_2",
            woundId: "wound_a",
            operationKey: "operation_2",
            ordinal: 2,
            woundTransitionOrdinal: 2,
            kind: "complicate",
            beforeFingerprint: Fingerprint('a'),
            afterFingerprint: Fingerprint('b'));

        Assert.True(Parse(History(first, second)).IsValid);

        var wrongFirst = (JsonObject)first.DeepClone();
        wrongFirst["beforeFingerprint"] = Fingerprint('f');
        AssertInvalid(
            Parse(History(wrongFirst)),
            Path,
            "wound_history_nonexistent_before_mismatch");

        var transferredFirst = (JsonObject)first.DeepClone();
        transferredFirst["woundId"] = "wound_b";
        AssertInvalid(
            Parse(History(transferredFirst)),
            Path,
            "wound_history_nonexistent_before_mismatch");

        second["beforeFingerprint"] = Fingerprint('c');
        AssertInvalid(Parse(History(first, second)), Path, "wound_history_chain_mismatch");
    }

    [Theory]
    [MemberData(nameof(TransitionKinds))]
    public void Parse_RecognizesOnlyExactClosedTransitionKinds(string kind)
    {
        var rows = CompleteLegalHistoryEndingIn(kind);
        Assert.True(Parse(History(rows)).IsValid, kind);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("healed")]
    [InlineData("reopen")]
    [InlineData("legacy_transition")]
    public void Parse_RejectsUnknownOrInexactTransitionKinds(string kind)
    {
        var row = Transition(kind: kind);

        AssertInvalid(
            Parse(History(row)),
            FirstRowPath + ".kind",
            "wound_history_invalid_kind");
    }

    [Theory]
    [InlineData("transition_Exact", "TRANSITION_EXACT", "wound_history_confusable_transition_id")]
    [InlineData("transition_A", "transition_А", "wound_history_confusable_transition_id")]
    [InlineData("transition_A", "transition_A", "wound_history_duplicate_transition_id")]
    public void Parse_TransitionIdsAreGloballyExactAndConfusableUnique(
        string firstId,
        string secondId,
        string expectedCode)
    {
        var first = Transition(transitionId: firstId, woundId: "wound_a");
        var second = Transition(
            transitionId: secondId,
            woundId: "wound_b",
            operationKey: "operation_2",
            ordinal: 2);

        AssertInvalid(Parse(History(first, second)), Path, expectedCode);
    }

    [Theory]
    [InlineData("operation_Exact", "OPERATION_EXACT", "wound_history_confusable_operation_key")]
    [InlineData("operation_A", "operation_А", "wound_history_confusable_operation_key")]
    [InlineData("operation_A", "operation_A", "wound_history_duplicate_operation_key")]
    public void Parse_OperationKeysAreGloballyExactAndConfusableUnique(
        string firstKey,
        string secondKey,
        string expectedCode)
    {
        var first = Transition(operationKey: firstKey, woundId: "wound_a");
        var second = Transition(
            transitionId: "transition_2",
            woundId: "wound_b",
            operationKey: secondKey,
            ordinal: 2);

        AssertInvalid(Parse(History(first, second)), Path, expectedCode);
    }

    [Fact]
    public void Parse_WoundIdsRejectInvalidAndConfusableCrossIdentityAliases()
    {
        var invalid = Transition(
            woundId: " wound_a",
            beforeFingerprint: Fingerprint('f'));
        AssertInvalid(
            Parse(History(invalid)),
            FirstRowPath + ".woundId",
            "wound_history_invalid_identifier");

        var first = Transition(woundId: "wound_A");
        var second = Transition(
            transitionId: "transition_2",
            woundId: "wound_А",
            operationKey: "operation_2",
            ordinal: 2);
        AssertInvalid(Parse(History(first, second)), Path, "wound_history_confusable_wound_id");
    }

    [Fact]
    public void Parse_AllIdentityAndReferenceFieldsRequireExactIdentifiers()
    {
        foreach (var field in new[]
                 {
                     "transitionId", "woundId", "eventRef", "operationKey", "attemptId",
                     "courseId", "cycleKey"
                 })
        {
            var row = Transition(beforeFingerprint: Fingerprint('f'));
            row[field] = " padded";
            AssertInvalid(
                Parse(History(row)),
                FirstRowPath + "." + field,
                "wound_history_invalid_identifier");
        }
    }

    [Fact]
    public void Parse_RequiresValidAuthorityFingerprintsAndBoundedReadableSummary()
    {
        foreach (var field in new[]
                 {
                     "beforeFingerprint", "afterFingerprint", "sourceFingerprint",
                     "paymentFingerprint", "outputFingerprint"
                 })
        {
            var row = Transition();
            row[field] = "sha256:" + new string('A', 64);
            AssertInvalid(
                Parse(History(row)),
                FirstRowPath + "." + field,
                "wound_history_invalid_fingerprint");
        }

        foreach (var summary in new[]
                 {
                     string.Empty,
                     " padded",
                     new string('а', WoundMaterializationContract.MaxReadableTextLength + 1)
                 })
        {
            var row = Transition();
            row["readableSummary"] = summary;
            AssertInvalid(
                Parse(History(row)),
                FirstRowPath + ".readableSummary",
                "wound_history_invalid_summary");
        }
    }

    [Fact]
    public void Parse_ReadableSummaryUsesTheCommonBoundedUnicodeTextContract()
    {
        var row = Transition(
            readableSummary: "Рана описана.\nОтмечен символ исцеления ✨");

        var result = Parse(History(row));

        Assert.True(result.IsValid, DescribeIssues(result.Issues));
    }

    [Fact]
    public void Parse_EnforcesVersionOneTwentyThousandRowBoundBeforeBuildingState()
    {
        var rows = Enumerable.Range(1, WoundHistoryState.MaxTransitions + 1)
            .Select(index => TypedTransition(
                transitionId: "transition_" + index,
                woundId: "wound_" + index,
                operationKey: "operation_" + index,
                ordinal: index))
            .ToArray();

        var result = WoundHistoryState.CreateValidated(rows.Length + 1, rows);

        AssertInvalid(
            result,
            WoundHistoryState.HistoryPath + ".transitions",
            "wound_history_limit_exceeded");
    }

    [Fact]
    public void CreateValidated_StopsAtTheFirstRowBeyondTheVersionOneBound()
    {
        var enumerated = 0;

        IEnumerable<WoundHistoryTransition> GuardedRows()
        {
            for (var index = 1; index <= WoundHistoryState.MaxTransitions + 1; index++)
            {
                enumerated++;
                yield return TypedTransition(
                    transitionId: "guarded_transition_" + index,
                    woundId: "guarded_wound_" + index,
                    operationKey: "guarded_operation_" + index,
                    ordinal: index);
            }

            throw new InvalidOperationException(
                "CreateValidated enumerated beyond the first over-limit row.");
        }

        var result = WoundHistoryState.CreateValidated(
            WoundHistoryState.MaxTransitions + 1,
            GuardedRows());

        AssertInvalid(
            result,
            WoundHistoryState.HistoryPath + ".transitions",
            "wound_history_limit_exceeded");
        Assert.Equal(WoundHistoryState.MaxTransitions + 1, enumerated);
    }

    [Fact]
    public void CreateValidated_RechecksTypedRowsAndNeverReturnsPartialAuthority()
    {
        var invalid = TypedTransition() with { Kind = "Create", TransitionId = " forged" };

        var result = WoundHistoryState.CreateValidated(2, new[] { invalid });

        AssertInvalid(result, WoundHistoryState.HistoryPath);
    }

    [Fact]
    public void CreateValidated_ForgedNullAndDefaultRowsFailClosedWithoutThrowing()
    {
        var allNull = new WoundHistoryTransition(
            null!,
            null!,
            0,
            0,
            null!,
            -1,
            null!,
            null!,
            null!,
            null!,
            null!,
            null,
            null,
            null,
            null,
            null,
            null!,
            null!,
            false);
        var validWoundWithNullChain = TypedTransition() with
        {
            TransitionId = null!,
            Kind = null!,
            EventRef = null!,
            OperationKey = null!,
            BeforeFingerprint = null!,
            AfterFingerprint = null!,
            SourceFingerprint = null!,
            ReadableSummary = null!
        };

        foreach (var forged in new[]
                 {
                     new WoundHistoryTransition[] { null! },
                     new[] { allNull },
                     new[] { validWoundWithNullChain }
                 })
        {
            var result = WoundHistoryState.CreateValidated(2, forged);

            AssertInvalid(result, WoundHistoryState.HistoryPath);
        }
    }

    [Fact]
    public void ResolveReplay_DistinguishesExactSemanticReplayFromEveryConflict()
    {
        var state = AssertValidState(History(Transition(attemptId: "attempt_1")));
        var existing = Assert.Single(state.Transitions);
        var exact = WoundHistoryState.CreateReplayProbe(existing);

        var replay = state.ResolveReplay(exact);

        Assert.Equal(WoundHistoryReplayDisposition.Exact, replay.Disposition);
        Assert.Same(existing, replay.Transition);
        Assert.Empty(replay.Issues);
        Assert.True(WoundHistoryState.ReplaySemanticsMatch(existing, exact));

        var mismatches = new[]
        {
            exact with { WoundId = "wound_other" },
            exact with { Kind = "diagnose" },
            exact with { Turn = exact.Turn + 1 },
            exact with { EventRef = "event_other" },
            exact with { BeforeFingerprint = Fingerprint('b') },
            exact with { AfterFingerprint = Fingerprint('c') },
            exact with { SourceFingerprint = Fingerprint('d') },
            exact with { AttemptId = "attempt_other" },
            exact with { CourseId = "course_other" },
            exact with { CourseMilestoneOrdinal = 2 },
            exact with { CycleKey = "cycle_other" },
            exact with { PaymentFingerprint = Fingerprint('e') },
            exact with { OutputFingerprint = Fingerprint('0') },
            exact with { ReadableSummary = "Иное принятое значение." },
            exact with { Terminal = true }
        };
        foreach (var mismatch in mismatches)
        {
            var conflict = state.ResolveReplay(mismatch);
            Assert.Equal(WoundHistoryReplayDisposition.Conflict, conflict.Disposition);
            Assert.Same(existing, conflict.Transition);
            Assert.Contains(
                conflict.Issues,
                issue => issue.Code == "wound_history_conflicting_replay");
            Assert.False(WoundHistoryState.ReplaySemanticsMatch(existing, mismatch));
        }
    }

    [Fact]
    public void ExactLookupsAndReplayNeverCaseFoldOrUseConfusableFallback()
    {
        var state = AssertValidState(History(Transition(
            transitionId: "transition_Exact",
            operationKey: "operation_Exact")));
        var existing = Assert.Single(state.Transitions);

        Assert.True(state.TryResolveExactTransition("transition_Exact", out var transition));
        Assert.Same(existing, transition);
        Assert.True(state.TryResolveExactOperation("operation_Exact", out var operation));
        Assert.Same(existing, operation);
        Assert.False(state.TryResolveExactTransition("TRANSITION_EXACT", out _));
        Assert.False(state.TryResolveExactTransition("transition_Еxact", out _));
        Assert.False(state.TryResolveExactOperation("OPERATION_EXACT", out _));
        Assert.False(state.TryResolveExactOperation("operation_Еxact", out _));

        var none = state.ResolveReplay(
            WoundHistoryState.CreateReplayProbe(existing) with { OperationKey = "OPERATION_EXACT" });
        Assert.Equal(WoundHistoryReplayDisposition.None, none.Disposition);
        Assert.Null(none.Transition);
        Assert.Empty(none.Issues);
    }

    [Fact]
    public void Parse_TerminalEvidenceBlocksReopenButAllowsIndependentLegacyAndArchiveAudit()
    {
        var create = Transition();
        var heal = Transition(
            transitionId: "transition_2",
            operationKey: "operation_2",
            ordinal: 2,
            woundTransitionOrdinal: 2,
            kind: "heal",
            turn: 43,
            beforeFingerprint: Fingerprint('a'),
            afterFingerprint: Fingerprint('b'),
            terminal: true);
        var legacy = Transition(
            transitionId: "transition_3",
            operationKey: "operation_3",
            ordinal: 3,
            woundTransitionOrdinal: 3,
            kind: "legacy",
            turn: 44,
            beforeFingerprint: Fingerprint('b'),
            afterFingerprint: Fingerprint('b'));
        var archive = Transition(
            transitionId: "transition_4",
            operationKey: "operation_4",
            ordinal: 4,
            woundTransitionOrdinal: 4,
            kind: "archive",
            turn: 45,
            beforeFingerprint: Fingerprint('b'),
            afterFingerprint: Fingerprint('b'));

        var auditResult = Parse(History(create, heal, legacy, archive));
        Assert.True(auditResult.IsValid, DescribeIssues(auditResult.Issues));
        var auditState = Assert.IsType<WoundHistoryState>(auditResult.State);
        Assert.Equal(
            "transition_2",
            Assert.Single(auditState.Transitions, static row => row.Terminal).TransitionId);
        Assert.All(
            auditState.Transitions.Where(static row => row.Kind is "legacy" or "archive"),
            row =>
            {
                Assert.False(row.Terminal);
                Assert.Equal(row.BeforeFingerprint, row.AfterFingerprint);
            });

        foreach (var kind in new[]
                 {
                     "create", "worsen", "complicate", "diagnose", "stabilize",
                     "treat", "recover", "heal"
                 })
        {
            var reopening = Transition(
                transitionId: "transition_3",
                operationKey: "operation_3",
                ordinal: 3,
                woundTransitionOrdinal: 3,
                kind: kind,
                turn: 44,
                beforeFingerprint: Fingerprint('b'),
                afterFingerprint: Fingerprint('c'),
                terminal: kind == "heal");
            AssertInvalid(
                Parse(History(create, heal, reopening)),
                Path,
                "wound_history_transition_after_terminal");
        }

        var mutatingLegacy = (JsonObject)legacy.DeepClone();
        mutatingLegacy["afterFingerprint"] = Fingerprint('c');
        AssertInvalid(
            Parse(History(create, heal, mutatingLegacy)),
            Path,
            "wound_history_terminal_state_changed");

        var secondTerminal = (JsonObject)archive.DeepClone();
        secondTerminal["terminal"] = true;
        AssertInvalid(
            Parse(History(create, heal, legacy, secondTerminal)),
            Path,
            "wound_history_multiple_terminal_rows");
    }

    [Fact]
    public void Parse_TerminalFlagAndLifecycleKindsMustAgree()
    {
        var createTerminal = Transition(terminal: true);
        AssertInvalid(Parse(History(createTerminal)), Path, "wound_history_invalid_terminal_kind");

        var create = Transition();
        var healWithoutTerminalEvidence = Transition(
            transitionId: "transition_2",
            operationKey: "operation_2",
            ordinal: 2,
            woundTransitionOrdinal: 2,
            kind: "heal",
            beforeFingerprint: Fingerprint('a'),
            afterFingerprint: Fingerprint('b'),
            terminal: false);
        AssertInvalid(
            Parse(History(create, healWithoutTerminalEvidence)),
            Path,
            "wound_history_missing_terminal_flag");
    }

    [Fact]
    public void Parse_CreateCannotShortcutDirectlyToTerminalArchive()
    {
        var create = Transition();
        foreach (var terminal in new[] { false, true })
        {
            var archive = Transition(
                transitionId: "transition_2",
                operationKey: "operation_2",
                ordinal: 2,
                woundTransitionOrdinal: 2,
                kind: "archive",
                turn: 43,
                beforeFingerprint: Fingerprint('a'),
                afterFingerprint: Fingerprint('b'),
                terminal: terminal);

            AssertInvalid(
                Parse(History(create, archive)),
                Path,
                "wound_history_archive_before_terminal");
        }
    }

    [Fact]
    public void ValidateAgreement_ActiveIdentityCarrierAndLatestHistoryMustMatchExactly()
    {
        var woundNode = WoundContractTestData.CreateActiveWound();
        var wound = ParseWound(woundNode);
        var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound);
        var state = AssertValidState(History(Transition(
            transitionId: "wound_transition_test_001",
            afterFingerprint: fingerprint)));
        var identities = ParseIdentities(WoundContractTestData.CreateIdentityEntry(
            semanticFingerprint: fingerprint));
        var carriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            WoundContractTestData.CreatePlayerCarrier(woundNode),
            null,
            null,
            null,
            null));

        Assert.Empty(state.ValidateAgreement(identities, carriers));

        foreach (var mutate in new Action<JsonObject>[]
                 {
                     entry => entry["lastTransitionOrdinal"] = 2,
                     entry => entry["semanticFingerprint"] = Fingerprint('f'),
                     entry => entry["createdAtTurn"] = 41,
                     entry => entry["createdEventRef"] = "event_other"
                 })
        {
            var identity = WoundContractTestData.CreateIdentityEntry(
                semanticFingerprint: fingerprint);
            mutate(identity);
            Assert.NotEmpty(state.ValidateAgreement(ParseIdentities(identity), carriers));
        }

        var wrongCarrierNode = (JsonObject)woundNode.DeepClone();
        wrongCarrierNode["display"]!["name"] = "Иное состояние раны";
        var wrongCarriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            WoundContractTestData.CreatePlayerCarrier(wrongCarrierNode),
            null,
            null,
            null,
            null));
        Assert.Contains(
            state.ValidateAgreement(identities, wrongCarriers),
            issue => issue.Code == "wound_history_active_fingerprint_mismatch");
    }

    [Fact]
    public void ValidateAgreement_HealedIdentityHasNoCarrierAndExactlyOneMatchingTerminalRow()
    {
        var terminalFingerprint = Fingerprint('b');
        var create = Transition();
        var heal = Transition(
            transitionId: "transition_heal",
            operationKey: "operation_heal",
            ordinal: 2,
            woundTransitionOrdinal: 2,
            kind: "heal",
            turn: 43,
            beforeFingerprint: Fingerprint('a'),
            afterFingerprint: terminalFingerprint,
            terminal: true);
        var state = AssertValidState(History(create, heal));
        var healed = ParseIdentities(WoundContractTestData.CreateIdentityEntry(
            status: "healed",
            lastTransitionOrdinal: 2,
            terminalTransitionId: "transition_heal",
            semanticFingerprint: terminalFingerprint));
        var emptyCarriers = EmptyCatalog();

        Assert.Empty(state.ValidateAgreement(healed, emptyCarriers));

        var activeCarrier = WoundContractTestData.CreateActiveWound();
        activeCarrier["display"]!["name"] = "Недопустимый оставшийся носитель";
        var withCarrier = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            WoundContractTestData.CreatePlayerCarrier(activeCarrier),
            null,
            null,
            null,
            null));
        Assert.Contains(
            state.ValidateAgreement(healed, withCarrier),
            issue => issue.Code == "wound_history_healed_carrier_present");

        var wrongTerminal = ParseIdentities(WoundContractTestData.CreateIdentityEntry(
            status: "healed",
            lastTransitionOrdinal: 2,
            terminalTransitionId: "transition_other",
            semanticFingerprint: terminalFingerprint));
        Assert.Contains(
            state.ValidateAgreement(wrongTerminal, emptyCarriers),
            issue => issue.Code == "wound_history_terminal_evidence_mismatch");
    }

    [Fact]
    public void ValidateAgreement_PostHealLegacyAndArchivePreserveTheOriginalTerminalEvidence()
    {
        var terminalFingerprint = Fingerprint('b');
        var state = AssertValidState(History(
            Transition(),
            Transition(
                transitionId: "transition_heal",
                operationKey: "operation_heal",
                ordinal: 2,
                woundTransitionOrdinal: 2,
                kind: "heal",
                turn: 43,
                beforeFingerprint: Fingerprint('a'),
                afterFingerprint: terminalFingerprint,
                terminal: true),
            Transition(
                transitionId: "transition_legacy",
                operationKey: "operation_legacy",
                ordinal: 3,
                woundTransitionOrdinal: 3,
                kind: "legacy",
                turn: 44,
                beforeFingerprint: terminalFingerprint,
                afterFingerprint: terminalFingerprint),
            Transition(
                transitionId: "transition_archive",
                operationKey: "operation_archive",
                ordinal: 4,
                woundTransitionOrdinal: 4,
                kind: "archive",
                turn: 45,
                beforeFingerprint: terminalFingerprint,
                afterFingerprint: terminalFingerprint)));
        var healed = ParseIdentities(WoundContractTestData.CreateIdentityEntry(
            status: "healed",
            lastTransitionOrdinal: 4,
            terminalTransitionId: "transition_heal",
            semanticFingerprint: terminalFingerprint));

        Assert.Empty(state.ValidateAgreement(healed, EmptyCatalog()));
    }

    [Fact]
    public void ValidateAgreement_RejectsMissingExtraAndWrongLifecycleAuthority()
    {
        var activeFingerprint = Fingerprint('a');
        var activeHistory = AssertValidState(History(Transition(afterFingerprint: activeFingerprint)));
        var healedIdentity = ParseIdentities(WoundContractTestData.CreateIdentityEntry(
            status: "healed",
            terminalTransitionId: "transition_1",
            semanticFingerprint: activeFingerprint));
        Assert.Contains(
            activeHistory.ValidateAgreement(healedIdentity, EmptyCatalog()),
            issue => issue.Code == "wound_history_terminal_evidence_missing");

        var orphanHistory = AssertValidState(History(Transition(woundId: "wound_orphan")));
        var emptyIdentities = ParseIdentities();
        Assert.Contains(
            orphanHistory.ValidateAgreement(emptyIdentities, EmptyCatalog()),
            issue => issue.Code == "wound_history_identity_missing");

        var identityWithoutHistory = ParseIdentities(WoundContractTestData.CreateIdentityEntry(
            woundId: "wound_missing_history",
            semanticFingerprint: activeFingerprint));
        Assert.Contains(
            WoundHistoryState.CreateValidated(1, Array.Empty<WoundHistoryTransition>())
                .State!
                .ValidateAgreement(identityWithoutHistory, EmptyCatalog()),
            issue => issue.Code == "wound_history_missing_for_identity");
    }

    [Fact]
    public void SerializeCanonical_IsDeterministicRoundTripsAndDetachesFromMutableInput()
    {
        var source = new List<WoundHistoryTransition>
        {
            TypedTransition(woundId: "wound_b", transitionId: "transition_1", operationKey: "operation_1"),
            TypedTransition(woundId: "wound_a", transitionId: "transition_2", operationKey: "operation_2", ordinal: 2)
        };
        var created = WoundHistoryState.CreateValidated(3, source);
        Assert.True(created.IsValid, DescribeIssues(created.Issues));
        var state = Assert.IsType<WoundHistoryState>(created.State);
        var first = WoundHistoryState.SerializeCanonical(state);

        source.Clear();
        var second = WoundHistoryState.SerializeCanonical(state);
        var reparsed = WoundHistoryState.Parse(first, Path);

        Assert.Equal(first, second);
        Assert.True(reparsed.IsValid, DescribeIssues(reparsed.Issues));
        Assert.Equal(first, WoundHistoryState.SerializeCanonical(reparsed.State!));
        Assert.StartsWith(
            "{\"schemaVersion\":1,\"nextOrdinal\":3,\"transitions\":[{\"transitionId\":\"transition_1\"",
            first,
            StringComparison.Ordinal);
    }

    private static JsonObject[] CompleteLegalHistoryEndingIn(string kind)
    {
        if (kind == "create")
            return new[] { Transition() };

        var create = Transition();
        if (kind is "legacy" or "archive")
        {
            var heal = Transition(
                transitionId: "transition_2",
                operationKey: "operation_2",
                ordinal: 2,
                woundTransitionOrdinal: 2,
                kind: "heal",
                beforeFingerprint: Fingerprint('a'),
                afterFingerprint: Fingerprint('b'),
                terminal: true);
            return new[]
            {
                create,
                heal,
                Transition(
                    transitionId: "transition_3",
                    operationKey: "operation_3",
                    ordinal: 3,
                    woundTransitionOrdinal: 3,
                    kind: kind,
                    beforeFingerprint: Fingerprint('b'),
                    afterFingerprint: Fingerprint('b'))
            };
        }

        return new[]
        {
            create,
            Transition(
                transitionId: "transition_2",
                operationKey: "operation_2",
                ordinal: 2,
                woundTransitionOrdinal: 2,
                kind: kind,
                beforeFingerprint: Fingerprint('a'),
                afterFingerprint: Fingerprint('b'),
                terminal: kind == "heal")
        };
    }

    private static JsonObject History(params JsonObject[] rows) => HistoryWithNextOrdinal(
        rows.Length + 1,
        rows);

    private static JsonObject HistoryWithNextOrdinal(int nextOrdinal, params JsonObject[] rows) => new()
    {
        ["schemaVersion"] = 1,
        ["nextOrdinal"] = nextOrdinal,
        ["transitions"] = new JsonArray(rows.Select(static row => row.DeepClone()).ToArray())
    };

    private static JsonObject Transition(
        string transitionId = "transition_1",
        string woundId = "wound_test_torn_side",
        int ordinal = 1,
        int woundTransitionOrdinal = 1,
        string kind = "create",
        int turn = 42,
        string eventRef = "turn_42:wound_opened",
        string operationKey = "operation_1",
        string? beforeFingerprint = null,
        string? afterFingerprint = null,
        string? sourceFingerprint = null,
        string? attemptId = null,
        string? courseId = null,
        int? courseMilestoneOrdinal = null,
        string? cycleKey = null,
        string? paymentFingerprint = null,
        string? outputFingerprint = null,
        string readableSummary = "Рана зафиксирована после подтверждённого события.",
        bool terminal = false)
    {
        var row = new JsonObject
        {
            ["transitionId"] = transitionId,
            ["woundId"] = woundId,
            ["ordinal"] = ordinal,
            ["woundTransitionOrdinal"] = woundTransitionOrdinal,
            ["kind"] = kind,
            ["turn"] = turn,
            ["eventRef"] = eventRef,
            ["operationKey"] = operationKey,
            ["beforeFingerprint"] = beforeFingerprint ?? WoundHistoryState.ComputeNonexistentBeforeFingerprint(woundId),
            ["afterFingerprint"] = afterFingerprint ?? Fingerprint('a'),
            ["sourceFingerprint"] = sourceFingerprint ?? Fingerprint('e'),
            ["attemptId"] = attemptId,
            ["courseId"] = courseId,
            ["courseMilestoneOrdinal"] = courseMilestoneOrdinal,
            ["cycleKey"] = cycleKey,
            ["paymentFingerprint"] = paymentFingerprint,
            ["outputFingerprint"] = outputFingerprint ?? Fingerprint('f'),
            ["readableSummary"] = readableSummary,
            ["terminal"] = terminal
        };
        if (HistoryResult(kind) is { } result)
            row["transitionResult"] = result.ToCanonicalJson();
        return row;
    }

    private static WoundHistoryTransition TypedTransition(
        string transitionId = "transition_1",
        string woundId = "wound_test_torn_side",
        int ordinal = 1,
        int woundTransitionOrdinal = 1,
        string kind = "create",
        int turn = 42,
        string eventRef = "turn_42:wound_opened",
        string operationKey = "operation_1",
        string? beforeFingerprint = null,
        string? afterFingerprint = null,
        string? sourceFingerprint = null,
        string? attemptId = null,
        string? courseId = null,
        int? courseMilestoneOrdinal = null,
        string? cycleKey = null,
        string? paymentFingerprint = null,
        string? outputFingerprint = null,
        string readableSummary = "Рана зафиксирована после подтверждённого события.",
        bool terminal = false) => new(
        transitionId,
        woundId,
        ordinal,
        woundTransitionOrdinal,
        kind,
        turn,
        eventRef,
        operationKey,
        beforeFingerprint ?? WoundHistoryState.ComputeNonexistentBeforeFingerprint(woundId),
        afterFingerprint ?? Fingerprint('a'),
        sourceFingerprint ?? Fingerprint('e'),
        attemptId,
        courseId,
        courseMilestoneOrdinal,
        cycleKey,
        paymentFingerprint,
        outputFingerprint ?? Fingerprint('f'),
        readableSummary,
        terminal,
        HistoryResult(kind));

    private static WoundTransitionResult? HistoryResult(string kind)
    {
        if (kind == "diagnose")
        {
            var canonical = WoundTransitionResultTests.Diagnosis();
            return new WoundDiagnosisTransitionResult("path_exact", "failure", Array.Empty<string>(),
                canonical["resultFingerprint"]!.GetValue<string>());
        }
        if (kind == "author_alternative_treatment")
        {
            var canonical = WoundTransitionResultTests.Alternative();
            return new WoundAlternativeTreatmentTransitionResult("authoring_public", "route_exact",
                null, Fingerprint('a'), null, canonical["resultFingerprint"]!.GetValue<string>());
        }
        return null;
    }

    private static WoundHistoryParseResult Parse(JsonObject root) =>
        WoundHistoryState.Parse(root.ToJsonString(), Path);

    private static WoundHistoryState AssertValidState(JsonObject root)
    {
        var result = Parse(root);
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        return Assert.IsType<WoundHistoryState>(result.State);
    }

    private static WoundMaterializationEnvelope ParseWound(JsonObject root)
    {
        var result = WoundMaterializationContract.Parse(root.ToJsonString(), "wound");
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        return result.Wound!;
    }

    private static WoundIdentityState ParseIdentities(params JsonObject[] entries)
    {
        var result = WoundIdentityState.Parse(
            WoundContractTestData.CreateIdentityIndex(entries).ToJsonString(),
            WoundIdentityState.StatePath);
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        return result.State!;
    }

    private static WoundCarrierCatalog EmptyCatalog() => WoundCarrierCatalog.Build(
        new WoundCarrierCatalogInput(null, null, null, null, null));

    private static void AssertInvalid(
        WoundHistoryParseResult result,
        string expectedPath,
        string? expectedCode = null)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.State);
        Assert.Contains(
            result.Issues,
            issue => issue.FilePath.StartsWith(expectedPath, StringComparison.Ordinal) &&
                     (expectedCode is null || issue.Code == expectedCode));
    }

    private static string Fingerprint(char character) => "sha256:" + new string(character, 64);

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) => string.Join(
        Environment.NewLine,
        issues.Select(issue => $"{issue.FilePath}: {issue.Code} ({issue.Expected}; {issue.Actual})"));
}
