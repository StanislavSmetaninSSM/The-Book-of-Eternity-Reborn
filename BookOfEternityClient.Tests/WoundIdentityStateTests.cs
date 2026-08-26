using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundIdentityStateTests
{
    private const string Path = "woundIdentity";
    private const string EntryPath = Path + ".entries[0]";

    public static TheoryData<string> InvalidIdentifiers => new()
    {
        "",
        " exact",
        "exact ",
        "e\u0301",
        "exact\u0001id",
        "exact\u200Bid",
        "exact\u2028id",
        "exact\u2029id"
    };

    [Fact]
    public void Parse_CompleteActiveIndex_ReturnsImmutableTypedStateAndExactLookup()
    {
        var callerOwned = WoundContractTestData.CreateIdentityIndex(
            WoundContractTestData.CreateIdentityEntry());

        var result = Parse(callerOwned);

        Assert.True(result.IsValid, DescribeIssues(result));
        var state = Assert.IsType<WoundIdentityState>(result.State);
        var entry = Assert.Single(state.Entries);
        Assert.Equal("wound_test_torn_side", entry.WoundId);
        Assert.Equal("mortal_world", entry.Realm);
        Assert.Equal("player", entry.OwnerKind);
        Assert.Equal("player_current", entry.OwnerId);
        Assert.Equal("game_state/player/wounds.json", entry.CarrierPath);
        Assert.Equal("physical", entry.Domain);
        Assert.Equal("active", entry.Status);
        Assert.Equal(42, entry.CreatedAtTurn);
        Assert.Equal("turn_42:wound_opened", entry.CreatedEventRef);
        Assert.Equal(1, entry.LastTransitionOrdinal);
        Assert.Null(entry.TerminalTransitionId);
        Assert.True(state.TryGetEntry(entry.WoundId, out var found));
        Assert.Same(entry, found);

        callerOwned["entries"]![0]!["ownerId"] = "forged_owner";
        Assert.Equal("player_current", entry.OwnerId);
    }

    [Fact]
    public void Parse_RootIsClosedCompleteExactVersionOneAndReturnsNoPartialState()
    {
        foreach (var json in new string?[] { null, "", " ", "[]", "null", "{" })
            AssertInvalid(WoundIdentityState.Parse(json, Path), Path);

        foreach (var field in new[] { "schemaVersion", "entries" })
        {
            var root = WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry());
            root.Remove(field);
            AssertInvalid(Parse(root), Path + "." + field, "wound_identity_missing_field");
        }

        foreach (var invalidVersion in new JsonNode?[] { 0, 2, "1", null })
        {
            var root = WoundContractTestData.CreateIdentityIndex();
            root["schemaVersion"] = invalidVersion?.DeepClone();
            AssertInvalid(Parse(root), Path + ".schemaVersion", "wound_identity_invalid_field");
        }

        var rootWithUnknown = WoundContractTestData.CreateIdentityIndex();
        rootWithUnknown["legacyEntries"] = new JsonArray();
        AssertInvalid(
            Parse(rootWithUnknown),
            Path + ".legacyEntries",
            "wound_identity_unknown_field");

        var entriesAsObject = WoundContractTestData.CreateIdentityIndex();
        entriesAsObject["entries"] = new JsonObject();
        AssertInvalid(
            Parse(entriesAsObject),
            Path + ".entries",
            "wound_identity_invalid_field");
    }

    [Fact]
    public void Parse_EntryIsClosedCompleteAndRequiresExactMemberKinds()
    {
        var required = new[]
        {
            "woundId", "realm", "ownerKind", "ownerId", "carrierPath", "domain",
            "status", "createdAtTurn", "createdEventRef", "lastTransitionOrdinal",
            "terminalTransitionId", "semanticFingerprint"
        };
        foreach (var field in required)
        {
            var entry = WoundContractTestData.CreateIdentityEntry();
            entry.Remove(field);
            AssertInvalid(
                Parse(WoundContractTestData.CreateIdentityIndex(entry)),
                EntryPath + "." + field,
                "wound_identity_missing_field");
        }

        var unknown = WoundContractTestData.CreateIdentityEntry();
        unknown["state"] = "active";
        AssertInvalid(
            Parse(WoundContractTestData.CreateIdentityIndex(unknown)),
            EntryPath + ".state",
            "wound_identity_unknown_field");

        var nonObject = WoundContractTestData.CreateIdentityIndex();
        nonObject["entries"] = new JsonArray("invalid");
        AssertInvalid(
            Parse(nonObject),
            EntryPath,
            "wound_identity_invalid_field");

        var wrongString = WoundContractTestData.CreateIdentityEntry();
        wrongString["ownerId"] = 7;
        AssertInvalid(
            Parse(WoundContractTestData.CreateIdentityIndex(wrongString)),
            EntryPath + ".ownerId",
            "wound_identity_invalid_identifier");

        var wrongNumber = WoundContractTestData.CreateIdentityEntry();
        wrongNumber["createdAtTurn"] = "42";
        AssertInvalid(
            Parse(WoundContractTestData.CreateIdentityIndex(wrongNumber)),
            EntryPath + ".createdAtTurn",
            "wound_identity_invalid_field");
    }

    [Fact]
    public void Parse_DuplicateRawPropertiesAreRejectedBeforeNodeConversion()
    {
        var json = WoundContractTestData.CreateIdentityIndex(
            WoundContractTestData.CreateIdentityEntry()).ToJsonString();
        var duplicateRoot = json.Replace(
            "\"schemaVersion\":1",
            "\"schemaVersion\":1,\"schemaVersion\":1",
            StringComparison.Ordinal);
        var duplicateEntry = json.Replace(
            "\"woundId\":\"wound_test_torn_side\"",
            "\"woundId\":\"wound_test_torn_side\",\"woundId\":\"forged\"",
            StringComparison.Ordinal);

        AssertInvalid(
            WoundIdentityState.Parse(duplicateRoot, Path),
            Path + ".schemaVersion",
            "wound_identity_duplicate_property");
        AssertInvalid(
            WoundIdentityState.Parse(duplicateEntry, Path),
            EntryPath + ".woundId",
            "wound_identity_duplicate_property");
    }

    [Theory]
    [MemberData(nameof(InvalidIdentifiers))]
    public void Parse_AllPermanentAndReferenceFieldsRequireExactIdentifiers(string invalid)
    {
        foreach (var field in new[]
                 {
                     "woundId", "ownerId", "carrierPath", "createdEventRef"
                 })
        {
            var entry = WoundContractTestData.CreateIdentityEntry();
            entry[field] = invalid;
            AssertInvalid(
                Parse(WoundContractTestData.CreateIdentityIndex(entry)),
                EntryPath + "." + field,
                "wound_identity_invalid_identifier");
        }

        var terminal = WoundContractTestData.CreateIdentityEntry(
            status: "healed",
            terminalTransitionId: invalid);
        AssertInvalid(
            Parse(WoundContractTestData.CreateIdentityIndex(terminal)),
            EntryPath + ".terminalTransitionId",
            "wound_identity_invalid_identifier");
    }

    [Theory]
    [InlineData("wound_A", "wound_A", "wound_identity_duplicate_id")]
    [InlineData("wound_a", "WOUND_A", "wound_identity_confusable_id")]
    [InlineData("wound_A", "wound_А", "wound_identity_confusable_id")]
    [InlineData("wound_A", "wound_Α", "wound_identity_confusable_id")]
    public void Parse_WoundIdsAreGloballyUniqueByExactAndConfusableIdentity(
        string firstId,
        string secondId,
        string expectedCode)
    {
        var first = WoundContractTestData.CreateIdentityEntry(woundId: firstId);
        var second = WoundContractTestData.CreateIdentityEntry(
            woundId: secondId,
            ownerId: "player_other",
            createdEventRef: "turn_43:wound_opened");

        var result = Parse(WoundContractTestData.CreateIdentityIndex(first, second));

        AssertInvalid(result, Path + ".entries[1].woundId", expectedCode);
    }

    [Fact]
    public void TryGetEntry_UsesExactOrdinalIdentityOnly()
    {
        var state = Assert.IsType<WoundIdentityState>(Parse(
            WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry(woundId: "wound_Exact"))).State);

        Assert.True(state.TryGetEntry("wound_Exact", out _));
        Assert.False(state.TryGetEntry("WOUND_EXACT", out _));
        Assert.False(state.TryGetEntry("wound_Еxact", out _));
    }

    [Fact]
    public void Parse_RealmOwnerDomainAndStatusAreOrdinalClosedValues()
    {
        foreach (var realm in new[] { "mortal_world", "chaos_sea", "shining_abode" })
            AssertValidEntry(WoundContractTestData.CreateIdentityEntry(realm: realm));
        foreach (var ownerKind in new[]
                 {
                     "player", "npc", "combatant", "combatant_member", "player_soul",
                     "guardian", "resident", "radiant_actor", "afterlife_actor"
                 })
        {
            AssertValidEntry(WoundContractTestData.CreateIdentityEntry(ownerKind: ownerKind));
        }
        foreach (var domain in new[] { "physical", "spiritual" })
            AssertValidEntry(WoundContractTestData.CreateIdentityEntry(domain: domain));
        AssertValidEntry(WoundContractTestData.CreateIdentityEntry(status: "active"));
        AssertValidEntry(WoundContractTestData.CreateIdentityEntry(
            status: "healed",
            terminalTransitionId: "wound_transition_terminal"));

        foreach (var (field, invalid) in new[]
                 {
                     ("realm", "Mortal_World"),
                     ("ownerKind", "named_npc"),
                     ("domain", "mental"),
                     ("status", "archived")
                 })
        {
            var entry = WoundContractTestData.CreateIdentityEntry();
            entry[field] = invalid;
            AssertInvalid(
                Parse(WoundContractTestData.CreateIdentityIndex(entry)),
                EntryPath + "." + field,
                "wound_identity_invalid_field");
        }
    }

    [Fact]
    public void Parse_StatusRequiresExactInternalTerminalEvidence()
    {
        AssertValidEntry(WoundContractTestData.CreateIdentityEntry(
            status: "active",
            terminalTransitionId: null));
        AssertValidEntry(WoundContractTestData.CreateIdentityEntry(
            status: "healed",
            terminalTransitionId: "wound_transition_terminal"));

        AssertInvalid(
            Parse(WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry(
                    status: "active",
                    terminalTransitionId: "wound_transition_terminal"))),
            EntryPath + ".terminalTransitionId",
            "wound_identity_terminal_evidence_mismatch");
        AssertInvalid(
            Parse(WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry(
                    status: "healed",
                    terminalTransitionId: null))),
            EntryPath + ".terminalTransitionId",
            "wound_identity_terminal_evidence_mismatch");
    }

    [Theory]
    [InlineData("sha256:000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("SHA256:0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("sha256:gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    [InlineData("wound-fingerprint-test-001")]
    public void Parse_SemanticFingerprintUsesExactLowercaseSha256Authority(string fingerprint)
    {
        var entry = WoundContractTestData.CreateIdentityEntry(
            semanticFingerprint: fingerprint);

        AssertInvalid(
            Parse(WoundContractTestData.CreateIdentityIndex(entry)),
            EntryPath + ".semanticFingerprint",
            "wound_identity_invalid_fingerprint");
    }

    [Fact]
    public void Parse_IntegerFieldsRequireExactLexemesAndBounds()
    {
        AssertValidEntry(WoundContractTestData.CreateIdentityEntry(createdAtTurn: 0));
        AssertValidEntry(WoundContractTestData.CreateIdentityEntry(createdAtTurn: int.MaxValue));
        AssertValidEntry(WoundContractTestData.CreateIdentityEntry(lastTransitionOrdinal: 1));
        AssertValidEntry(WoundContractTestData.CreateIdentityEntry(lastTransitionOrdinal: int.MaxValue));

        foreach (var (field, valid, invalid) in new[]
                 {
                     ("schemaVersion", "1", "1.0"),
                     ("schemaVersion", "1", "1e0"),
                     ("createdAtTurn", "42", "42.0"),
                     ("createdAtTurn", "42", "42e0"),
                     ("createdAtTurn", "42", "2147483648"),
                     ("lastTransitionOrdinal", "1", "1.0"),
                     ("lastTransitionOrdinal", "1", "1e0"),
                     ("lastTransitionOrdinal", "1", "2147483648")
                 })
        {
            var json = WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry()).ToJsonString();
            var token = $"\"{field}\":{valid}";
            Assert.Contains(token, json, StringComparison.Ordinal);
            var result = WoundIdentityState.Parse(
                json.Replace(token, $"\"{field}\":{invalid}", StringComparison.Ordinal),
                Path);
            AssertInvalid(result, PathFor(field), "wound_identity_invalid_field");
        }

        foreach (var (field, invalid) in new[]
                 {
                     ("createdAtTurn", -1),
                     ("lastTransitionOrdinal", 0)
                 })
        {
            var entry = WoundContractTestData.CreateIdentityEntry();
            entry[field] = invalid;
            AssertInvalid(
                Parse(WoundContractTestData.CreateIdentityIndex(entry)),
                EntryPath + "." + field,
                "wound_identity_invalid_field");
        }
    }

    [Fact]
    public void ValidateActiveAgreement_AcceptsEveryExactActiveEnvelopeCoordinate()
    {
        var (entry, wound) = CreateAlignedPair();

        var issues = WoundIdentityState.ValidateActiveAgreement(entry, wound, EntryPath);

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateActiveAgreement_RejectsNonNullTerminalEvidenceForAlignedActivePair()
    {
        var (entry, wound) = CreateAlignedPair();
        entry = entry with { TerminalTransitionId = "terminal_transition" };

        var issues = WoundIdentityState.ValidateActiveAgreement(entry, wound, EntryPath);

        var issue = Assert.Single(issues);
        Assert.Equal("wound_identity_active_agreement_mismatch", issue.Code);
        Assert.Equal(EntryPath + ".terminalTransitionId", issue.FilePath);
    }

    [Fact]
    public void ValidateActiveAgreement_RejectsTerminalPairInsteadOfTreatingItAsActive()
    {
        var (entry, wound) = CreateAlignedPair();
        wound = wound with { Lifecycle = "healed" };
        entry = entry with
        {
            Status = "healed",
            TerminalTransitionId = "wound_transition_terminal",
            SemanticFingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound)
        };

        var issues = WoundIdentityState.ValidateActiveAgreement(entry, wound, EntryPath);

        Assert.Contains(issues, issue =>
            issue.Code == "wound_identity_active_agreement_mismatch" &&
            issue.FilePath == EntryPath + ".status");
    }

    [Theory]
    [InlineData("woundId", "wound_identity_active_agreement_mismatch", ".woundId")]
    [InlineData("status", "wound_identity_active_agreement_mismatch", ".status")]
    [InlineData("realm", "wound_identity_active_agreement_mismatch", ".realm")]
    [InlineData("ownerKind", "wound_identity_active_agreement_mismatch", ".ownerKind")]
    [InlineData("ownerId", "wound_identity_active_agreement_mismatch", ".ownerId")]
    [InlineData("carrierPath", "wound_identity_active_agreement_mismatch", ".carrierPath")]
    [InlineData("domain", "wound_identity_active_agreement_mismatch", ".domain")]
    [InlineData("createdAtTurn", "wound_identity_active_agreement_mismatch", ".createdAtTurn")]
    [InlineData("createdEventRef", "wound_identity_active_agreement_mismatch", ".createdEventRef")]
    [InlineData("lastTransitionOrdinal", "wound_identity_active_agreement_mismatch", ".lastTransitionOrdinal")]
    [InlineData("semanticFingerprint", "wound_identity_active_agreement_mismatch", ".semanticFingerprint")]
    public void ValidateActiveAgreement_ReportsEveryMismatchFamilyAtExactEntryPath(
        string mismatch,
        string expectedCode,
        string expectedSuffix)
    {
        var (entry, wound) = CreateAlignedPair();
        (entry, wound) = mismatch switch
        {
            "woundId" => (entry with { WoundId = "wound_other" }, wound),
            "status" => (entry with { Status = "healed", TerminalTransitionId = "transition_terminal" }, wound),
            "realm" => (entry with { Realm = "chaos_sea" }, wound),
            "ownerKind" => (entry with { OwnerKind = "npc" }, wound),
            "ownerId" => (entry with { OwnerId = "owner_other" }, wound),
            "carrierPath" => (entry with { CarrierPath = "game_state/npcs/npc_wounds.json" }, wound),
            "domain" => (entry with { Domain = "spiritual" }, wound),
            "createdAtTurn" => (entry with { CreatedAtTurn = 43 }, wound),
            "createdEventRef" => (entry with { CreatedEventRef = "turn_43:other" }, wound),
            "lastTransitionOrdinal" => (entry with { LastTransitionOrdinal = 2 }, wound),
            "semanticFingerprint" => (entry with { SemanticFingerprint = ValidFingerprint('f') }, wound),
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, null)
        };

        var issues = WoundIdentityState.ValidateActiveAgreement(entry, wound, EntryPath);

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode &&
            issue.FilePath == EntryPath + expectedSuffix);
    }

    [Fact]
    public void ComputeSemanticFingerprint_IsIndependentSha256OfCanonicalWoundUtf8()
    {
        var wound = ParseWound(WoundContractTestData.CreateActiveWound());
        var canonical = WoundMaterializationContract.SerializeCanonical(wound);
        var independentlyComputed = "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        Assert.Equal(
            independentlyComputed,
            WoundIdentityState.ComputeSemanticFingerprint(wound));
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(independentlyComputed));
    }

    [Fact]
    public void CanonicalSerialization_IsFixedSortedDetachedAndByteStable()
    {
        var entryB = WoundContractTestData.CreateIdentityEntry(
            woundId: "wound_b",
            ownerId: "owner_b",
            createdEventRef: "turn_42:b",
            semanticFingerprint: ValidFingerprint('b'));
        var entryA = WoundContractTestData.CreateIdentityEntry(
            woundId: "wound_a",
            ownerId: "owner_a",
            createdEventRef: "turn_42:a",
            semanticFingerprint: ValidFingerprint('a'));
        var callerOwned = WoundContractTestData.CreateIdentityIndex(entryB, entryA);
        var state = Assert.IsType<WoundIdentityState>(Parse(callerOwned).State);
        var first = WoundIdentityState.SerializeCanonical(state);

        callerOwned["entries"]![0]!["ownerId"] = "forged";
        Assert.Equal(first, WoundIdentityState.SerializeCanonical(state));

        var reordered = new JsonObject
        {
            ["entries"] = new JsonArray(
                ReverseObject(entryA),
                ReverseObject(entryB)),
            ["schemaVersion"] = 1
        };
        var reorderedState = Assert.IsType<WoundIdentityState>(Parse(reordered).State);
        Assert.Equal(first, WoundIdentityState.SerializeCanonical(reorderedState));

        var canonical = JsonNode.Parse(first)!.AsObject();
        Assert.Equal(new[] { "schemaVersion", "entries" }, canonical.Select(static item => item.Key));
        Assert.Equal(
            new[] { "wound_a", "wound_b" },
            canonical["entries"]!.AsArray().Select(item => item!["woundId"]!.GetValue<string>()));
        Assert.Equal(
            new[]
            {
                "woundId", "realm", "ownerKind", "ownerId", "carrierPath", "domain",
                "status", "createdAtTurn", "createdEventRef", "lastTransitionOrdinal",
                "terminalTransitionId", "semanticFingerprint"
            },
            canonical["entries"]![0]!.AsObject().Select(static item => item.Key));

        var reparsed = WoundIdentityState.Parse(first, Path);
        Assert.True(reparsed.IsValid, DescribeIssues(reparsed));
        Assert.Equal(first, WoundIdentityState.SerializeCanonical(reparsed.State!));
    }

    private static (WoundIdentityEntry Entry, WoundMaterializationEnvelope Wound) CreateAlignedPair()
    {
        var wound = ParseWound(WoundContractTestData.CreateActiveWound());
        var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound);
        var state = Assert.IsType<WoundIdentityState>(Parse(
            WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry(
                    semanticFingerprint: fingerprint))).State);
        Assert.True(state.TryGetEntry(wound.WoundId, out var entry));
        return (entry, wound);
    }

    private static WoundMaterializationEnvelope ParseWound(JsonObject json)
    {
        var result = WoundMaterializationContract.Parse(json.ToJsonString(), "wound");
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        return result.Wound!;
    }

    private static WoundIdentityParseResult Parse(JsonObject root) =>
        WoundIdentityState.Parse(root.ToJsonString(), Path);

    private static void AssertValidEntry(JsonObject entry)
    {
        var result = Parse(WoundContractTestData.CreateIdentityIndex(entry));
        Assert.True(result.IsValid, DescribeIssues(result));
        Assert.NotNull(result.State);
        Assert.Empty(result.Issues);
    }

    private static void AssertInvalid(
        WoundIdentityParseResult result,
        string? path = null,
        string? code = null)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.State);
        Assert.NotEmpty(result.Issues);
        if (path is not null)
            Assert.Contains(result.Issues, issue =>
                issue.FilePath == path && (code is null || issue.Code == code));
    }

    private static string DescribeIssues(WoundIdentityParseResult result) =>
        string.Join(
            Environment.NewLine,
            result.Issues.Select(issue =>
                $"{issue.Code} {issue.FilePath}: {issue.Expected} / {issue.Actual}"));

    private static string PathFor(string field) =>
        field == "schemaVersion" ? Path + ".schemaVersion" : EntryPath + "." + field;

    private static string ValidFingerprint(char digit) =>
        "sha256:" + new string(digit, 64);

    private static JsonObject ReverseObject(JsonObject source)
    {
        var result = new JsonObject();
        foreach (var property in source.Reverse())
            result[property.Key] = property.Value?.DeepClone();
        return result;
    }
}
