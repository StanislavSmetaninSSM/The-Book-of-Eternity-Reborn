using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualPressureMultiParentDataTests
{
    /// <summary>Complete mixed-rank groups and one later-turn larger list remain detached DATA after saturation.</summary>
    /// <param name="ranks">Independent complete canonical-position severities, never a count scalar supplied to production.</param>
    /// <param name="turn">Every retained birth must precede this actual source/exchange turn.</param>
    [Theory]
    [InlineData("3,4,3", 46)] [InlineData("4,3,4,3", 46)]
    [InlineData("4,4,4,4", 46)] [InlineData("3,4,3,4,3,4,3,4", 60)]
    public void MultiParentPressure_MixedAndLargerCompleteData(string ranks, int turn)
    {
        var expected = ranks.Split(',').Select(int.Parse).ToArray();
        var fixture = SpiritualPressureMultiParentData.Create(expected.Length, expected, turn);
        var before = SpiritualPressureMultiParentData.Serialize(fixture);
        var admissible = SpiritualPressureRetainedDataAdmissibility.Check(fixture.Retained, fixture.Profiles);
        Assert.True(admissible.IsValid, JsonSerializer.Serialize(admissible));
        var data = ReadSupportedMultiParent(fixture);
        Assert.Equal(expected, data.Parents.Select(parent => parent.ParentWound.Severity.Rank));
        Assert.All(data.Parents, parent => Assert.True(parent.ParentWound.Origin.CreatedAtTurn < turn));
        Assert.Equal(turn, fixture.Source["turnEvidence"]!["turn"]!.GetValue<int>());
        Assert.Equal(turn, data.ReadActualExchange()["exchangeAtTurn"]!.GetValue<int>());
        if (expected.Length > 4)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpiritualPressureMultiParentData.Create(expected.Length, expected));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpiritualPressureMultiParentData.Create(expected.Length, expected, 40 + expected.Length));
        }
        Assert.Equal(before, SpiritualPressureMultiParentData.Serialize(fixture));
    }

    /// <summary>Every final-parent refusal follows supported N4 DATA, so the strain cap cannot hide an unvalidated final graph.</summary>
    /// <param name="change">One encoding, complete graph, ownership, chronology or selected component contradiction.</param>
    [Theory]
    [InlineData("base64")] [InlineData("utf8")] [InlineData("duplicate-json")]
    [InlineData("owner")] [InlineData("realm")] [InlineData("future")]
    [InlineData("effect")] [InlineData("component")] [InlineData("operation")]
    [InlineData("magnitude")] [InlineData("max-stacks")] [InlineData("profile-entry")]
    public void MultiParentPressure_FinalParentRefusesMalformedOrContradictoryData(string change)
    {
        var fixture = SpiritualPressureMultiParentData.Create(4);
        var previous = ReadSupportedMultiParent(fixture);
        var row = MultiProof(fixture.Source)["parents"]![3]!.AsObject();
        var parent = fixture.Retained.Parents[3].DeepClone().AsObject();
        var definition = parent["consequences"]!["ownedEffectSources"]!["definitions"]![0]!.AsObject();
        switch (change)
        {
            case "base64": row["parentWoundJsonBase64"] = "!not-base64!"; break;
            case "utf8": row["parentWoundJsonBase64"] = Convert.ToBase64String(new byte[] { 0x7b, 0x22, 0xc3, 0x28, 0x22, 0x3a, 0x31, 0x7d }); break;
            case "duplicate-json": row["parentWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"nested\":[{\"key\":1,\"key\":2}]}")); break;
            case "owner": parent["owner"]!["ownerId"] = "foreign_soul"; break;
            case "realm": parent["owner"]!["realm"] = "shining_abode"; definition["allowedRealms"] = new JsonArray("shining_abode"); break;
            case "future":
                parent["lastTransition"]!["turn"] = 47; parent["lastTransition"]!["kind"] = "complicate";
                parent["lastTransition"]!["ordinal"] = 2; break;
            case "effect": row["effectId"] = "effect_foreign"; break;
            case "component": row["componentId"] = "component_foreign"; break;
            case "operation": definition["components"]![0]!["payload"]!["operation"] = "maneuver"; break;
            case "magnitude": definition["components"]![0]!["payload"]!["magnitude"] = 2; break;
            case "max-stacks": definition["stacking"]!["maxStacks"] = 2; break;
            case "profile-entry": parent["consequences"]!["entries"]!.AsArray().RemoveAt(0); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        if (change is not ("base64" or "utf8" or "duplicate-json"))
        {
            if (change is "owner" or "realm" or "future" or "effect" or "component" or "operation") AssertValidMultiParent(parent);
            SetMultiParent(fixture.Source, 3, parent);
        }
        AssertMultiParentRejected(fixture, previous);
    }

    /// <summary>Closed count/version roles, canonical order and every globally owned root remain distinct across the whole list.</summary>
    /// <param name="change">A single list, role or cross-parent identity mutation after supported N3/N4 success.</param>
    [Theory]
    [InlineData("count1")] [InlineData("count2")] [InlineData("v4-count3")]
    [InlineData("duplicate")] [InlineData("reversed")] [InlineData("role")]
    [InlineData("wound-case")] [InlineData("wound-confusable")]
    [InlineData("root-exact")] [InlineData("root-confusable")] [InlineData("sibling-root")]
    public void MultiParentPressure_CompleteListIdentityAndWireRefusals(string change)
    {
        var fixture = SpiritualPressureMultiParentData.Create(change == "v4-count3" ? 3 : 4);
        var previous = ReadSupportedMultiParent(fixture);
        var proof = MultiProof(fixture.Source); var rows = proof["parents"]!.AsArray(); var last = rows.Count - 1;
        var parent = fixture.Retained.Parents[last].DeepClone().AsObject();
        switch (change)
        {
            case "count1": while (rows.Count > 1) rows.RemoveAt(rows.Count - 1); break;
            case "count2": while (rows.Count > 2) rows.RemoveAt(rows.Count - 1); break;
            case "v4-count3":
                // This independently lawful pair arithmetic would accept at count2; only the closed v4 cardinality may reject count3.
                fixture.Ordinary["after"]!["playerSideStrain"] = "fractured";
                proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Ordinary);
                proof["ordinaryCalculation"]!["traumaPressure"] = 13L; proof["ordinaryCalculation"]!["maximumSeverityRank"] = 2;
                proof["version"] = 4; proof["inputRoleRevision"] = "existing_pressure_ordinary_pair_v1";
                var lawfulPair = fixture.Source.DeepClone().AsObject();
                MultiProof(lawfulPair)["parents"]!.AsArray().RemoveAt(2); SpiritualExistingPressureTestData.Seal(lawfulPair);
                Assert.Equal(SpiritualPressureMultiParentData.ActualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(lawfulPair, out var pairData));
                Assert.Equal(4, pairData!.Version); Assert.Equal(2, pairData.Parents.Count);
                break;
            case "duplicate": rows[last] = rows[0]!.DeepClone(); break;
            case "reversed":
                var reversed = rows.Reverse().Select(row => row!.DeepClone()).ToArray(); rows.Clear();
                foreach (var row in reversed) rows.Add(row); break;
            case "role": proof["inputRoleRevision"] = "existing_pressure_ordinary_pair_v1"; break;
            case "wound-case": case "wound-confusable":
                var woundId = fixture.Retained.Parents[0]["woundId"]!.GetValue<string>();
                var alias = change == "wound-case" ? woundId.ToUpperInvariant() : woundId.Replace("owner", "\u043ewner", StringComparison.Ordinal);
                Assert.NotEqual(woundId, alias);
                Assert.Equal(MortalLocationIdentityState.BuildConfusableKey(woundId), MortalLocationIdentityState.BuildConfusableKey(alias));
                parent = ReplaceMultiIdentity(parent, parent["woundId"]!.GetValue<string>(), alias);
                AssertValidMultiParent(parent); SetMultiParent(fixture.Source, last, parent);
                var ordered = rows.Select(row => (Id: DecodeMultiObject(row!["parentWoundJsonBase64"])["woundId"]!.GetValue<string>(), Row: row.DeepClone()))
                    .OrderBy(row => row.Id, StringComparer.Ordinal).ToArray(); rows.Clear();
                foreach (var row in ordered) rows.Add(row.Row); break;
            case "root-exact": case "root-confusable":
                var firstRoot = rows[0]!["effectId"]!.GetValue<string>();
                var rootAlias = change == "root-exact" ? firstRoot : firstRoot.Replace("owner", "\u043ewner", StringComparison.Ordinal);
                Assert.Equal(MortalLocationIdentityState.BuildConfusableKey(firstRoot), MortalLocationIdentityState.BuildConfusableKey(rootAlias));
                parent = ReplaceMultiIdentity(parent, rows[last]!["effectId"]!.GetValue<string>(), rootAlias);
                AssertValidMultiParent(parent); SetMultiParent(fixture.Source, last, parent); rows[last]!["effectId"] = rootAlias; break;
            case "sibling-root":
                SplitMultiSiblingRoot(parent, rows[0]!["effectId"]!.GetValue<string>());
                AssertValidMultiParent(parent); SetMultiParent(fixture.Source, last, parent); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        AssertMultiParentRejected(fixture, previous);
    }

    /// <summary>Exact ordinary siblings, actual arithmetic and raw-comparator shape stay binding after a supported N4 read.</summary>
    /// <param name="change">One independently resealed contradiction, not an old unsupported-version refusal.</param>
    [Theory]
    [InlineData("ordinary-summary")] [InlineData("opposition-strain")]
    [InlineData("player-cost")] [InlineData("opposition-cost")] [InlineData("dice")]
    [InlineData("ordinary-trauma")] [InlineData("ordinary-maximum")] [InlineData("ordinary-IV")]
    [InlineData("actual-margin")] [InlineData("actual-maximum")] [InlineData("actual-zero")]
    [InlineData("raw-path")] [InlineData("raw-absent")] [InlineData("raw-hash")] [InlineData("raw-pointer")]
    public void MultiParentPressure_ExchangeCalculationAndComparatorContradictions(string change)
    {
        var fixture = SpiritualPressureMultiParentData.Create(4); var previous = ReadSupportedMultiParent(fixture);
        var proof = MultiProof(fixture.Source); var source = fixture.Source;
        switch (change)
        {
            case "ordinary-summary": fixture.Ordinary["summary"] = "Иное полное обычное описание."; break;
            case "opposition-strain": fixture.Ordinary["after"]!["oppositionSideStrain"] = "strained"; break;
            case "player-cost": fixture.Ordinary["actionCostAudit"]!["player"]!["max"] = 11; break;
            case "opposition-cost": fixture.Ordinary["actionCostAudit"]!["opposition"]!["max"] = 10; break;
            case "dice": fixture.Ordinary["diceAudit"]!["diceUsed"]![0]!["value"] = 5; break;
            case "ordinary-trauma": proof["ordinaryCalculation"]!["traumaPressure"] = 9L; break;
            case "ordinary-maximum": proof["ordinaryCalculation"]!["maximumSeverityRank"] = 2; break;
            case "ordinary-IV":
                fixture.Ordinary["after"]!["playerSideStrain"] = "broken";
                proof["ordinaryCalculation"]!["traumaPressure"] = 23L; proof["ordinaryCalculation"]!["maximumSeverityRank"] = 4; break;
            case "actual-margin": source["harmfulMargin"] = 9; break;
            case "actual-maximum": source["maximumSeverityRank"] = 3; break;
            case "actual-zero":
                source["dangerMode"] = "training"; source["maximumSeverityRank"] = 0;
                proof["ordinaryCalculation"]!["maximumSeverityRank"] = 0;
                var standalone = source.DeepClone().AsObject(); standalone.Remove("strainPreimage"); SpiritualExistingPressureTestData.Seal(standalone);
                Assert.Equal(0, SpiritualWoundSourceWitness.Validate(standalone).MaximumSeverityRank); break;
            case "raw-path": proof["originalInput"]!["path"] = "game_state/meta/foreign.json"; break;
            case "raw-absent": proof["originalInput"]!["existed"] = false; break;
            case "raw-hash": proof["originalInput"]!["contentFingerprint"] = "not-a-fingerprint"; break;
            case "raw-pointer": proof["originalInput"]!["exchangePointer"] = "/activeConflict/exchangeLog/00"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Ordinary);
        AssertMultiParentRejected(fixture, previous);
    }

    /// <summary>Otherwise valid last-exchange source coordinates do not widen the closed v6 first-exchange/single-source contour.</summary>
    /// <param name="exchangeOrdinal">Zero or one, with complete matching bounds and die claims.</param>
    /// <param name="sourceOrdinal">A nonzero otherwise in-range source ordinal; v4 coordinate behavior is unchanged.</param>
    [Theory]
    [InlineData(0, 1)] [InlineData(1, 2)]
    public void MultiParentPressure_RefusesOtherwiseValidNonzeroCoordinates(int exchangeOrdinal, int sourceOrdinal)
    {
        var fixture = SpiritualPressureMultiParentData.Create(3); var previous = ReadSupportedMultiParent(fixture);
        var source = fixture.Source; var evidence = source["turnEvidence"]!;
        source["exchangeOrdinal"] = exchangeOrdinal; source["sourceOrdinal"] = sourceOrdinal;
        source["coordinate"] = "activeConflict.exchangeLog[" + exchangeOrdinal + "].player";
        evidence["bounds"]!["exchangeCount"] = exchangeOrdinal + 1;
        evidence["bounds"]!["sourceSlotCount"] = 2 * (exchangeOrdinal + 1);
        foreach (var claim in evidence["diceClaims"]!.AsArray().OfType<JsonObject>())
        {
            claim["exchangeOrdinal"] = exchangeOrdinal;
            claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim);
        }
        // Remove only the comparison for this prerequisite. The real complete source/dice/bounds reader must accept it.
        var standalone = source.DeepClone().AsObject(); Assert.True(standalone.Remove("strainPreimage"));
        SpiritualExistingPressureTestData.Seal(standalone);
        Assert.Equal(SpiritualPressureMultiParentData.ActualCalculation,
            SpiritualWoundSourceWitness.ValidateAndReadStrainData(standalone, out var noComparison)); Assert.Null(noComparison);
        AssertMultiParentRejected(fixture, previous);
    }

    /// <summary>All parent facts and exchanges are detached and immutable; singular aliases never select a convenient first parent.</summary>
    /// <param name="count">The complete N3/N4 list, including the fourth parent after scalar saturation.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void MultiParentPressure_ParentsAndExchangesAreDetached(int count)
    {
        var fixture = SpiritualPressureMultiParentData.Create(count); var data = ReadSupportedMultiParent(fixture);
        var images = data.Parents.Select(parent => WoundMaterializationContract.SerializeCanonical(parent.ParentWound)).ToArray();
        var fingerprints = data.Parents.Select(parent => parent.ParentSemanticFingerprint).ToArray();
        AssertMultiReadOnly(data.Parents);
        foreach (var parent in data.Parents)
        {
            AssertMultiReadOnly(parent.ParentWound.Consequences.Entries);
            AssertMultiReadOnly(parent.ParentWound.Consequences.OwnedEffectSources.Definitions);
            AssertMultiReadOnly(parent.ParentWound.Consequences.OwnedEffectSources.RootBindings);
            AssertMultiReadOnly(parent.ParentWound.Consequences.OwnedEffectSources.DefinitionFacts);
        }
        Assert.Throws<FormatException>(() => data.ParentWound); Assert.Throws<FormatException>(() => data.ParentSemanticFingerprint);
        Assert.Throws<FormatException>(() => data.EffectId); Assert.Throws<FormatException>(() => data.ComponentId); Assert.Throws<FormatException>(() => data.ParentSourceId);
        var expectedActual = fixture.Actual.DeepClone().AsObject(); var expectedOrdinaryAfter = fixture.Ordinary["after"]!.DeepClone();
        foreach (var parent in fixture.Retained.Parents) parent.Clear();
        fixture.Source.Clear(); fixture.Ordinary.Clear(); fixture.Actual.Clear();
        data.ReadActualExchange()["summary"] = "Changed detached export.";
        data.ReadOrdinaryAfter(expectedActual)["playerSideStrain"] = "clear";
        Assert.Equal(images, data.Parents.Select(parent => WoundMaterializationContract.SerializeCanonical(parent.ParentWound)));
        Assert.Equal(fingerprints, data.Parents.Select(parent => parent.ParentSemanticFingerprint));
        Assert.True(JsonNode.DeepEquals(expectedActual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(expectedOrdinaryAfter, data.ReadOrdinaryAfter(expectedActual)));
        expectedActual["summary"] = "Other otherwise well-shaped actual.";
        Assert.Throws<FormatException>(() => data.ReadOrdinaryAfter(expectedActual));
    }

    /// <summary>A lawful capped subset is intrinsic DATA only; its reader cannot authenticate completeness against an absent current group.</summary>
    [Fact]
    public void MultiParentPressure_LawfulSubsetDoesNotProveCompleteCurrentGroup()
    {
        var fixture = SpiritualPressureMultiParentData.Create(4); _ = ReadSupportedMultiParent(fixture);
        MultiProof(fixture.Source)["parents"]!.AsArray().RemoveAt(3); SpiritualExistingPressureTestData.Seal(fixture.Source);
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var subset);
        Assert.Equal(SpiritualPressureMultiParentData.ActualCalculation, calculation); Assert.Equal(3, subset!.Parents.Count);
        Assert.Equal(4, fixture.Retained.Parents.Count); // Only a genuine current/signed owner can reject this omission.
        MultiProof(fixture.Source)["contributionCount"] = 4;
        AssertMultiParentRejected(fixture, subset); // A scalar cannot restore a missing complete parent.
    }

    /// <summary>A well-formed raw hash is retained comparator DATA, never original-byte membership or permission to publish.</summary>
    [Fact]
    public void MultiParentPressure_RawComparatorDoesNotAuthenticateOriginalBytes()
    {
        var fixture = SpiritualPressureMultiParentData.Create(3); _ = ReadSupportedMultiParent(fixture);
        var fingerprint = new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint;
        MultiProof(fixture.Source)["originalInput"]!["contentFingerprint"] = "sha256:" + new string('f', 64);
        SpiritualExistingPressureTestData.Seal(fixture.Source);
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var changed);
        Assert.NotEqual(fingerprint, changed!.OriginalInputFingerprint);
        Assert.Equal(fingerprint, fixture.Source["turnEvidence"]!["originalSnapshotFingerprint"]!.GetValue<string>());
        Assert.Equal(SpiritualPressureMultiParentData.ActualCalculation, changed.ActualCalculation);
    }

    /// <summary>Requires one production witness read before each cheap mutation; the original success rows separately prove the convenience path.</summary>
    private static SpiritualStrainPreimageData ReadSupportedMultiParent(SpiritualPressureMultiParentFixture fixture)
    {
        var before = SpiritualPressureMultiParentData.Serialize(fixture);
        Assert.Equal(SpiritualPressureMultiParentData.ActualCalculation,
            SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var retained));
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        AssertSupported(fixture, fixture.Retained.Parents.Count, data);
        Assert.Equal(before, SpiritualPressureMultiParentData.Serialize(fixture)); return data;
    }

    /// <summary>Every failure must clear an already populated out slot and leave every supplied input unchanged.</summary>
    private static void AssertMultiParentRejected(SpiritualPressureMultiParentFixture fixture, SpiritualStrainPreimageData previous)
    {
        SpiritualExistingPressureTestData.Seal(fixture.Source); var before = SpiritualPressureMultiParentData.Serialize(fixture);
        SpiritualStrainPreimageData? retained = previous;
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out retained)); Assert.Null(retained);
        Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual));
        Assert.Equal(before, SpiritualPressureMultiParentData.Serialize(fixture));
    }

    /// <summary>Selects the current caller-owned wire object, never retained immutable DATA.</summary>
    private static JsonObject MultiProof(JsonObject source) => source["strainPreimage"]!.AsObject();
    /// <summary>Uses production strict JSON/base64 decoding for independently changed embedded fixture inputs.</summary>
    private static JsonObject DecodeMultiObject(JsonNode? encoded) => SpiritualWoundStateJson.Parse(
        SpiritualWoundStateJson.DecodeUtf8JsonText(SpiritualWoundStateJson.DecodeJsonObjectBytes(encoded)));
    /// <summary>Replaces one complete encoded fixture envelope without changing another independent graph or History input.</summary>
    private static void SetMultiParent(JsonObject source, int index, JsonObject parent) =>
        MultiProof(source)["parents"]![index]!["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent);
    /// <summary>Changes all exact references to one fixture identity before proving individual graph validity.</summary>
    private static JsonObject ReplaceMultiIdentity(JsonObject parent, string oldId, string newId) =>
        SpiritualWoundStateJson.Parse(parent.ToJsonString().Replace(oldId, newId, StringComparison.Ordinal));
    /// <summary>Proves selected cross-parent refusals are not merely malformed individual envelope failures.</summary>
    private static void AssertValidMultiParent(JsonObject parent)
    {
        var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "multiParent.changedCompleteParent");
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
    }
    /// <summary>Rejects mutable collection exports without reflection or production-private access.</summary>
    private static void AssertMultiReadOnly<T>(IReadOnlyList<T> values)
    {
        if (values is ICollection<T> mutable) { Assert.True(mutable.IsReadOnly); Assert.Throws<NotSupportedException>(() => mutable.Add(values.Count == 0 ? default! : values[0])); }
        if (values is IList<T> indexed && values.Count != 0) Assert.Throws<NotSupportedException>(() => indexed[0] = values[0]);
    }

    /// <summary>Authors a valid separate nonselected sibling root to expose a collision outside the final parent's selected pressure root.</summary>
    private static void SplitMultiSiblingRoot(JsonObject parent, string siblingEffectId)
    {
        var graph = parent["consequences"]!["ownedEffectSources"]!.AsObject(); var definition = graph["definitions"]![0]!.AsObject();
        var sibling = definition.DeepClone().AsObject(); const string key = "definition_multi_parent_sibling";
        sibling["definitionKey"] = key; sibling["stacking"]!["stackKey"] = "stack_multi_parent_sibling";
        sibling["components"] = new JsonArray(definition["components"]![1]!.DeepClone(), definition["components"]![2]!.DeepClone());
        sibling["triggers"]![0]!["componentIds"] = new JsonArray("component_002", "component_003");
        definition["components"] = new JsonArray(definition["components"]![0]!.DeepClone());
        definition["triggers"]![0]!["componentIds"] = new JsonArray("component_001"); graph["definitions"]!.AsArray().Add(sibling);
        graph["rootBindings"]!.AsArray().Add(WoundContractTestData.CreateRootBinding(siblingEffectId, key));
        parent["consequences"]!["entries"]![1]!["effectId"] = siblingEffectId; parent["consequences"]!["entries"]![2]!["effectId"] = siblingEffectId;
    }
}
