# Wound Legacy Canonical Vocabulary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admit the already approved independent wound-legacy canonical source and derived lifetime coordinates while preserving non-public source authority, empty authored legacy links and active-wound separation.

**Architecture:** Add exactly `wound_legacy` to the canonical source-kind and link-kind sets. The definition link-kind set stays unchanged: authored legacy drafts require `links=[]`, and source-bound lifetime derives its canonical kind/ID from the source owner without an authored link. Document canonical vocabulary separately from GM selectors without exposing an unfinished heal/reload producer.

**Tech Stack:** C#/.NET 8, xUnit, System.Text.Json, PowerShell 7 bounded verification.

## Global Constraints

- Tracked source: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), open T070 with bounded T074 documentation and T177 verification work in `specs/1536-complete-wound-materialization/tasks.md`.
- `spec.md` FR-017 requires an independent non-GM-materializable `wound_legacy` source; `data-model.md` defines `sourceKind=wound_legacy/sourceId=legacyId`. The effect planner remains the sole effect-ID allocator. Healing groups only exact `sourceKind=wound`, never its legacy source.
- Stay in the existing `1536-complete-wound-materialization` branch/worktree. No migration, new branch, remote action, issue closure or unrelated `.serena/` edit.
- Change only the six runtime/test/GM files in the file map. Parent owns this plan and feature artifacts. Do not implement or choose the unresolved legacy preparation witness/public-shape architecture, recovery receipt schema or spiritual art schema.
- This task is structural vocabulary and its existing catalog boundary, not durable history reload, actual heal publication, terminal provenance persistence, or complete cleanup integration. Keep full T070/T074/T177/#1536 open.
- No new generic source export, GM selector, caller authority, source normalization, alias, schema version, reflection fallback or active-wound cleanup broadening. Preserve every existing rejection and test. Do not add `wound_legacy` to `EffectSourceDefinitionContract.LinkKinds`: approved GM legacy drafts require `links=[]`; no private source-link injection policy is selected here.
- General effect structural acceptance in all three realms does not add a spiritual wound treatment/legacy gameplay operation. The shared catalog already supports all three realms; actual permitted treatment operations remain governed by their adapters.
- Source changes require tests first. One C# owner, no concurrent source edits during runs. Focused and Fast remain bounded at five minutes. One meaningful Fast, then one conditional FullValidation at the existing fifteen-minute bound because shared GM examples/docs are changed. No PreMerge or duplicate control run.
- The GM authors no canonical `wound_legacy` source, link or effect. Shared guide/example text must explicitly distinguish it from the unchanged eleven representative GM selector kinds and explain continuation after an already accepted heal. Do not add a twelfth `sourceSelectors` JSON row or invent an executable heal command.

## File map and source-backed boundary

- Modify `BookOfEternityClient/Services/EffectMaterializationContract.cs`: append the exact literal to `SourceKinds` and `LinkKinds` only.
- Create `BookOfEternityClient.Tests/WoundLegacyCanonicalContractTests.cs`: canonical source/lifetime acceptance, closed-kind rejection, non-public empty-link catalog admission, rejection of authored legacy links in every role, and unchanged ordinary wound controls.
- Create `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.WoundLegacy.cs`: guard the common guide, worked continuation and manifest distinction in the existing partial test class.
- Modify `OtherGuides/Effect_Materialization_Contract.md`: separate canonical client-only vocabulary from GM source selectors.
- Modify `Examples/E_CLI_Effect_Materialization.txt`: add a worked continuation in the existing `effect_mortal_source_families_v1` section without adding a JSON fence or selector.
- Modify `Examples/example_validation_manifest.json`: describe the unchanged eleven-selector coverage and require the client-only continuation text.

Current canonical structural sets omit the kind even though `EffectSourceAuthority` now
registers it, rejects `Materializable=true` before catalog insertion and refuses
ordinary apply/repair resolution. `EffectAcceptedTurnInputComposer` rejects generic
external legacy exports. Existing wound-only catalog snapshots filter exact `wound`.
Canonical `source_bound` lifetime uses a link kind while a source definition carries
only its registered predicate; the source owner supplies the canonical kind/ID.
`EffectSourcePredicateCatalog` allows `active` for legacy, not item/skill predicates.
This task does not change these production guards or infer reload authority from JSON.
Parent read the complete bounded inventory and verified the distinction: approved
`data-model.md:2415-2418` and treatment contract360-364 require authored `links=[]`.
`EffectAcceptedTurnPlanner.cs:8066-8071` and canonical binding revalidation derive
the exact source-bound lifetime kind/ID independently of definition links. Adding
a new definition link kind would broaden all link roles without an approved need;
that provisional plan expansion was removed during self-review before any code.

The complete common guide is already required by Mortal and afterlife GM entrypoints.
No pending/control file, afterlife action type, response field, service or daemon
entrypoint changes. `Afterlife_Contract_Matrix.md` and `E_CLI_Afterlife_Turns.txt` need
no new action/example entry for this internal vocabulary; their existing shared-guide
coverage is checked with `AfterlifeDocumentationCoverageTests`. The existing shared
example manifest entry covers all three realms and will be updated in place.

### Task 1: Close the source/link schema gap without opening GM materialization

**Interfaces:** Existing `EffectMaterializationContract.Validate(JsonElement,string,EffectMaterializationPhase)`, `EffectSourceDefinitionContract.ValidateArray(JsonElement,string,string)`, `EffectSourceAuthority.Build(EffectSourceAuthorityInput)`, canonical binding and public/repair resolution. No new production API.

- [X] **Step 1: Add the structural and catalog tests.**

Create `BookOfEternityClient.Tests/WoundLegacyCanonicalContractTests.cs` exactly:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundLegacyCanonicalContractTests
{
    private const string LegacyKind = "wound_legacy";
    private const string LegacyId = "wound_legacy_contract_001";
    private const string DefinitionKey = "legacy_contract_definition";
    private const string SourceRef = "legacy_ref_contract";

    [Theory]
    [InlineData("mortal_world", false, "active")]
    [InlineData("mortal_world", false, "suspended")]
    [InlineData("mortal_world", true, "active")]
    [InlineData("mortal_world", true, "suspended")]
    [InlineData("chaos_sea", false, "active")]
    [InlineData("chaos_sea", false, "suspended")]
    [InlineData("chaos_sea", true, "active")]
    [InlineData("chaos_sea", true, "suspended")]
    [InlineData("shining_abode", false, "active")]
    [InlineData("shining_abode", false, "suspended")]
    [InlineData("shining_abode", true, "active")]
    [InlineData("shining_abode", true, "suspended")]
    public void CanonicalContract_AcceptsIndependentLegacyCoordinates(
        string realm, bool sourceBound, string state)
    {
        var effect = Effect(realm, LegacyKind, sourceBound, state);
        var original = effect.ToJsonString();
        using var document = JsonDocument.Parse(original);

        Assert.Empty(EffectMaterializationContract.Validate(
            document.RootElement, "effects[0]",
            EffectMaterializationPhase.CanonicalActive));
        Assert.Equal(original, effect.ToJsonString());
        Assert.Equal(LegacyKind, document.RootElement.GetProperty("source")
            .GetProperty("kind").GetString());
        Assert.Equal(LegacyId, document.RootElement.GetProperty("source")
            .GetProperty("sourceId").GetString());
        Assert.Empty(document.RootElement.GetProperty("links").EnumerateArray());
        if (sourceBound)
        {
            Assert.Equal(LegacyKind, document.RootElement.GetProperty("lifetime")
                .GetProperty("linkKind").GetString());
            Assert.Equal(LegacyId, document.RootElement.GetProperty("lifetime")
                .GetProperty("targetId").GetString());
        }
    }

    [Theory]
    [InlineData("mortal_world", false, false)]
    [InlineData("mortal_world", false, true)]
    [InlineData("mortal_world", true, false)]
    [InlineData("mortal_world", true, true)]
    [InlineData("chaos_sea", false, false)]
    [InlineData("chaos_sea", false, true)]
    [InlineData("chaos_sea", true, false)]
    [InlineData("chaos_sea", true, true)]
    [InlineData("shining_abode", false, false)]
    [InlineData("shining_abode", false, true)]
    [InlineData("shining_abode", true, false)]
    [InlineData("shining_abode", true, true)]
    public void EmptyLinkDefinition_ResolvesOnlyThroughNonPublicCatalogBinding(
        string realm, bool sourceBound, bool sameTurn)
    {
        var definitions = new JsonArray(Definition(realm, LegacyKind, sourceBound));
        using var document = JsonDocument.Parse(definitions.ToJsonString());
        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement, "source.activeEffectDefinitions", realm));

        var source = new EffectSourceExport(
            realm, LegacyKind, LegacyId, definitions,
            Materializable: false, Active: true, SameTurn: sameTurn,
            SourceRef: sameTurn ? SourceRef : null);
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            sameTurn ? Array.Empty<EffectSourceExport>() : new[] { source },
            sameTurn ? new[] { source } : Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));
        Assert.Empty(authority.Issues);
        var binding = authority.ResolveCanonicalBinding(
            new EffectSourceKey(realm, LegacyKind, LegacyId, DefinitionKey), "player");
        Assert.True(binding.Success);
        Assert.NotNull(binding.Source);
        Assert.False(binding.Source.Materializable);
        Assert.True(JsonNode.DeepEquals(definitions[0], binding.Source.Definition));

        var selector = new JsonObject
        {
            ["kind"] = LegacyKind,
            [sameTurn ? "sourceRef" : "sourceId"] = sameTurn ? SourceRef : LegacyId,
            ["definitionKey"] = DefinitionKey
        };
        var apply = authority.Resolve(selector, realm, "player", new JsonObject());
        Assert.False(apply.Success);
        Assert.Contains(apply.Issues, static issue =>
            issue.Code == "effect_source_not_materializable");
        var repair = authority.ResolveRepairCandidate(selector, realm, "player");
        Assert.False(repair.Success);
        Assert.Contains(repair.Issues, static issue =>
            issue.Code == "effect_source_not_materializable");
        Assert.Empty(authority.SnapshotSameTurnWoundEntries());
        Assert.Empty(authority.SnapshotSameTurnWoundGroups());
        Assert.Empty(authority.SnapshotWoundGroupAuthorities());
    }

    public static IEnumerable<object[]> InvalidCanonicalKinds()
    {
        foreach (var field in new[] { "source", "lifetime", "links" })
        foreach (var kind in new[]
                 { "Wound_Legacy", "wound_legacy ", "wоund_legacy", "wound_legacy_extra" })
            yield return new object[] { field, kind };
    }

    [Theory]
    [MemberData(nameof(InvalidCanonicalKinds))]
    public void CanonicalContract_RejectsEveryNonExactKind(string field, string kind)
    {
        var effect = Effect("mortal_world", "wound", true, "active");
        var expectedPath = field switch
        {
            "source" => "effects[0].source.kind",
            "lifetime" => "effects[0].lifetime.linkKind",
            "links" => "effects[0].links[0].kind",
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        if (field == "links")
            effect["links"]![0]!["kind"] = kind;
        else
            effect[field]![field == "source" ? "kind" : "linkKind"] = kind;
        using var document = JsonDocument.Parse(effect.ToJsonString());

        var issue = Assert.Single(EffectMaterializationContract.Validate(
            document.RootElement, "effects[0]",
            EffectMaterializationPhase.CanonicalActive));
        Assert.Equal("effect_materialization_invalid_field", issue.Code);
        Assert.Equal(expectedPath, issue.FilePath);
    }

    [Theory]
    [InlineData("Wound_Legacy")]
    [InlineData("wound_legacy ")]
    [InlineData("wоund_legacy")]
    [InlineData("wound_legacy_extra")]
    public void DefinitionContract_RejectsEveryNonExactLinkKind(string kind)
    {
        var definition = Definition("mortal_world", kind, true);
        using var document = JsonDocument.Parse(new JsonArray(definition).ToJsonString());
        var issue = Assert.Single(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement, "source.activeEffectDefinitions", "mortal_world"));
        Assert.Equal("effect_source_definition_invalid_field", issue.Code);
        Assert.Equal("source.activeEffectDefinitions[0].links[0].kind", issue.FilePath);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("condition")]
    [InlineData("context")]
    [InlineData("cleanup_companion")]
    public void DefinitionContract_DoesNotOpenAuthoredLegacyLinks(string role)
    {
        var definition = Definition("mortal_world", LegacyKind, true);
        definition["links"] = Links(LegacyKind);
        definition["links"]![0]!["role"] = role;
        using var document = JsonDocument.Parse(new JsonArray(definition).ToJsonString());
        var issue = Assert.Single(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement, "source.activeEffectDefinitions", "mortal_world"));
        Assert.Equal("effect_source_definition_invalid_field", issue.Code);
        Assert.Equal("source.activeEffectDefinitions[0].links[0].kind", issue.FilePath);
    }

    [Theory]
    [InlineData("mortal_world")]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void ExistingWoundKind_RemainsStructurallyValid(string realm)
    {
        using var effect = JsonDocument.Parse(
            Effect(realm, "wound", true, "active").ToJsonString());
        using var definitions = JsonDocument.Parse(
            new JsonArray(Definition(realm, "wound", true)).ToJsonString());
        Assert.Empty(EffectMaterializationContract.Validate(
            effect.RootElement, "effects[0]", EffectMaterializationPhase.CanonicalActive));
        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            definitions.RootElement, "source.activeEffectDefinitions", realm));
    }

    private static JsonObject Effect(
        string realm, string kind, bool sourceBound, string state)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "action_control");
        effect["realm"] = realm;
        effect["state"] = state;
        effect["source"] = new JsonObject
        {
            ["kind"] = kind,
            ["sourceId"] = LegacyId,
            ["definitionKey"] = DefinitionKey
        };
        effect["links"] = kind == LegacyKind ? new JsonArray() : Links(kind);
        effect["lifetime"] = sourceBound
            ? new JsonObject
            {
                ["mode"] = "source_bound", ["linkKind"] = kind,
                ["targetId"] = LegacyId, ["activePredicate"] = "active",
                ["onSourceLoss"] = "expire"
            }
            : new JsonObject { ["mode"] = "permanent" };
        return effect;
    }

    private static JsonObject Definition(string realm, string kind, bool sourceBound)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("action_control");
        definition["definitionKey"] = DefinitionKey;
        definition["allowedRealms"] = new JsonArray(realm);
        definition["allowedTargetKinds"] = new JsonArray("player");
        definition["links"] = kind == LegacyKind ? new JsonArray() : Links(kind);
        definition["lifetime"] = sourceBound
            ? new JsonObject
            {
                ["mode"] = "source_bound", ["activePredicate"] = "active",
                ["onSourceLoss"] = "expire"
            }
            : new JsonObject { ["mode"] = "permanent" };
        return definition;
    }

    private static JsonArray Links(string kind) => new(new JsonObject
    {
        ["kind"] = kind, ["targetId"] = LegacyId, ["role"] = "source"
    });
}
```

The lookalike `wоund_legacy` contains Cyrillic U+043E, not Latin U+006F. Preserve
it exactly. The invalid canonical rows start from the existing `wound` vocabulary
so each tests exactly one bad field independently of the missing new literal.
These are structural/catalog tests, not a forged accepted legacy preparation.
Canonical legacy instances and definitions deliberately use empty links. The
four exact legacy-kind definition rejection rows preserve the unchanged authored
link boundary for every role; no new definition link capability is needed.

Create `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.WoundLegacy.cs`:

```csharp
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PromptDocumentationCoverageTests
{
    [Fact]
    public void InternalWoundLegacyVocabulary_IsClientOwnedAcrossSharedDocs()
    {
        var common = ReadRepoFile("OtherGuides", "Effect_Materialization_Contract.md");
        var example = ReadRepoFile("Examples", "E_CLI_Effect_Materialization.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");
        Assert.Contains("Client-owned `wound_legacy`", common, StringComparison.Ordinal);
        Assert.Contains("not an additional GM `effectChanges[].source` selector", common,
            StringComparison.Ordinal);
        Assert.Contains("lifetime.linkKind", common, StringComparison.Ordinal);
        Assert.Contains("Worked continuation after healing", example, StringComparison.Ordinal);
        Assert.Contains("Do not apply, resend, or reconstruct the legacy effect.", example,
            StringComparison.Ordinal);
        Assert.Contains("wound_legacy", manifest, StringComparison.Ordinal);
    }
}
```

- [X] **Step 2: Observe semantic RED before production/docs changes.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~WoundLegacyCanonicalContractTests|FullyQualifiedName~InternalWoundLegacyVocabulary_IsClientOwnedAcrossSharedDocs"
```

Expected 48 executed: 12 canonical source/lifetime acceptance failures plus one
missing-doc guard failure, with 12 empty-link catalog controls, 16 non-exact
controls, four authored legacy-link rejection controls and three ordinary-wound
controls passing.
Inspect actual issues and test failures. A compile/fixture failure is not semantic
RED; report any mismatch before changing the agreed production surface.

- [X] **Step 3: Add exactly the two canonical closed vocabulary entries.**

In `EffectMaterializationContract.cs`, replace `SourceKinds` with:

```csharp
    private static readonly HashSet<string> SourceKinds = Set(
        "skill", "spiritual_art", "item", "wound", "quest", "location", "hazard", "faction",
        "world_event", "fate_card", "combat_action", "wound_legacy");
```

In `EffectMaterializationContract.cs` only, replace `LinkKinds` with:

```csharp
    private static readonly HashSet<string> LinkKinds = Set(
        "wound", "skill", "spiritual_art", "item", "quest", "location", "hazard", "faction",
        "world_event", "fate_card", "combat", "wound_legacy");
```

No other production code change. The canonical source-bound link uses the source
owner's exact kind; this adds neither a `linkKind` input to definition lifetime
nor a new permitted definition `links[].kind` value.

- [X] **Step 4: Synchronize shared GM guidance and its existing worked example.**

In `OtherGuides/Effect_Materialization_Contract.md`, change the heading sentence
`The exact source kind is one of:` to `The representative GM source selector kind is one of:`.
Immediately after the existing eleven-kind code block, add exactly:

```text
Client-owned `wound_legacy` is a separate canonical source kind, not an additional GM `effectChanges[].source` selector. A lasting mechanical consequence of an accepted heal uses `source.kind=wound_legacy` and `source.sourceId=legacyId`; a source-bound canonical lifetime derives `lifetime.linkKind=wound_legacy` and `targetId=legacyId` from that source owner. Authored legacy definitions require `links=[]` and retain their existing lifetime policy, never canonical `linkKind` or `targetId` lifetime fields. Structural validity is not creation authority: only the sealed wound treatment/effect transaction may create this source and its effect, and accepted typed legacy history supplies durable reload authority. Healing cleans up exact `sourceKind=wound` effects, not the independent legacy source; later effect removal does not erase legacy provenance. Never write, copy, or repair these canonical coordinates manually. The T070 heal/history/reload producer remains pending; this structural vocabulary alone does not enable that flow.
```

This documents the approved authority contract, not a claim that its unfinished
T070 preparation/history/publication implementation is complete.

In `Examples/E_CLI_Effect_Materialization.txt`, change the existing source-family
intro to `Representative exact GM source selector kinds are` followed by the same
unchanged eleven inline literals and `Representative selectors:`. Keep the JSON
catalog exactly eleven rows. After that catalog's closing fence and before
`The source must exist, be current/active/unlocked/equipped as required...`, insert:

```text
Worked continuation after healing: an earlier accepted treatment has healed a torn muscle but left a lasting movement restriction. The client-owned source is `source.kind=wound_legacy`, with the independent `legacyId`; the healed wound is no longer an active source. The GM describes the remaining restriction in the ordinary narrative and leaves its already accepted mechanics alone. Do not apply, resend, or reconstruct the legacy effect. In particular, do not add a twelfth `sourceSelectors` entry or invent an `effectChanges[].source` selector for `wound_legacy`. A later permitted dispel/removal uses the existing effect lifecycle route and does not delete the healed wound's legacy/history provenance. This is a continuation example, not a new heal command or permission to author canonical source/link coordinates.
```

In `Examples/example_validation_manifest.json`, modify only the existing
`effect_mortal_source_families_v1` entry's three fields to these exact values;
keep every other field and row unchanged:

```json
{
  "description": "Representative exact GM selectors cover eleven source families; the worked continuation distinguishes client-owned wound_legacy without granting a new apply selector.",
  "coverageLimit": "Exact eleven-kind representative GM source catalog and sourceId/definitionKey triples, plus client-only wound_legacy continuation guidance; structural/catalog tests own canonical source/lifetime validation and closed authored links, and T070 owns heal/history/reload publication.",
  "requiredText": ["effect_mortal_source_families_v1", "skill", "spiritual_art", "item", "wound", "quest", "location", "hazard", "faction", "world_event", "fate_card", "combat_action", "wound_legacy", "Worked continuation after healing", "Do not apply, resend, or reconstruct the legacy effect."]
}
```

- [X] **Step 5: Verify the new boundary, relevant owners and shared docs.**

Run Step 2's exact filter: expected 48/48 GREEN. Then:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~WoundLegacyCanonicalContractTests|FullyQualifiedName~WoundLegacySourceAuthorityTests|FullyQualifiedName~EffectMaterializationContractTests|FullyQualifiedName~EffectSourceDefinitionContractTests|FullyQualifiedName~InternalWoundLegacyVocabulary_IsClientOwnedAcrossSharedDocs|FullyQualifiedName~AfterlifeDocumentationCoverageTests"
.\scripts\test-csharp.ps1 -Lane Fast
.\scripts\test-csharp.ps1 -Lane FullValidation -TimeoutMinutes 15
```

Execute sequentially; finish a run before editing. The Focused selection includes
the required afterlife documentation class. FullValidation checks the existing
example's still-exact eleven-selector JSON and updated manifest without inventing
a new fence. Prior Fast was 7,580/7,580 in3:13.691; expected new Fast total7,628
if no unrelated source change. Counts are expectations, not substitutes for evidence.

Audit actual summary plus every TRX counter/error, build diagnostics, cleanup,
timeouts and duplicate execution/cross-TRX test IDs. Record discovery entries
separately from executed rows; non-enumerated theories may expand at runtime,
so never label their raw arithmetic difference a missing/uncompleted set. A
failing/partial run is not GREEN, and no unrelated failure is waived. No extra
Fast, PreMerge, full-solution `dotnet test`, lane migration or timeout increase.

- [X] **Step 6: Self-review, exact-file commit and independent acceptance.**

Run `git diff --check`, inspect only the six owned paths, and commit those
exact paths (never `git add .`). Report source/test/GM changes and all actual
run artifacts under the provided `sdd/` report path. Parent builds one review
package from the original recorded task BASE through the final candidate,
inspects the diff/source and actual evidence, and obtains fresh Spec Compliance
and Quality review before checking this bounded task. Full T070/T074/T177 and
#1536 remain open; no remote action follows this checkpoint.

## Acceptance evidence — 2026-09-07

Bounded task accepted at `642eb4e24b298428bf9d31ebf02678344a9d58b6`, exact
BASE `d4a0a06c5a97cbc02d8f746c4ff4ab3fd946faa8`. Parent inspected the complete
six-file diff, full new tests, unchanged exact `source.kind=wound` cleanup,
source-bound derivation and canonical binding checks. Independent task review:
Spec Compliant / Quality Approved, zero Critical/Important/Minor findings.

Actual artifacts under `TestResults/test-lanes/`:

| Run | Artifact directory | Executed outcome | Wall time |
| --- | --- | --- | --- |
| Semantic RED | `20260907-102154-436-1528-bf6b0461ecfe40e9a7807124f9ffadc2-focused` | 35 PASS / 13 intended FAIL / 48 executed | 1:10.058 |
| Owning GREEN | `20260907-102402-790-25760-612b6b9c6b224c27bdb5624a73b9d4e4-focused` | 371/371 PASS, including all 48 new rows | 1:03.483 |
| One Fast | `20260907-102512-602-48800-b33b12795f1d4953ac2a98ee390b8069-fast` | 7,628/7,628 PASS; 26 TRXs | 3:23.368 |
| Conditional FullValidation | `20260907-102841-447-42936-1a7c065bb47247b6b328899e0996c6d6-fullvalidation` | 1,857/1,857 PASS; 11 TRXs | 10:46.920 |
| Final documentation guard | `20260907-103949-556-34068-21466d5781164cbf8e5f592d0e52770b-focused` | 1/1 PASS | 0:14.694 |
| Final manifest/selector consumers | `20260907-104527-188-45336-5d065decab904883b75cc3b56f04d811-focused` | 2/2 PASS | 1:18.524 |

The prescribed standalone 48-row GREEN command was not repeated: parent matched
all 48 passing results in the 371-row owning run. After FullValidation the child
removed one stale duplicate manifest description; the replacement values were
already present. Both actual manifest/selector consumers then passed on the final
file, resolving the review's second evidence caveat. Parent recursively checked
the final manifest for duplicate properties and checked the complete BASE..HEAD
diff. No duplicate properties or whitespace errors remain.

Parent read every summary, TRX and relevant build/error log: all builds have zero
warnings/errors; no timeout, skipped row, duplicate execution/cross-TRX test ID, or
cleanup failure. Focused/Fast retained five minutes; FullValidation used the
documented fifteen-minute diagnostic budget. Discovery is from discovery logs,
not execution summaries: Fast 7,574 -> 7,628 through seven theory expansions
(+54); FullValidation 1,747 -> 1,857 through five (+110). Method-level comparison
matches all other methods. No additional Fast, FullValidation or PreMerge ran.

Shared Mortal/afterlife guide, worked continuation, manifest and source guard were
updated together. There is no new afterlife command/action/pending root, so no new
matrix entry or afterlife turn example was required. Full T070/T074/T177/#1536,
durable legacy publication and all unresolved architecture choices remain open;
top-level task count remains 77/177. No push, PR, merge or issue closure.

### Pre-implementation review

- The two canonical sets are required by the approved source identity and its
  already-derived source-bound lifetime. The broader definition link set stays
  closed; source-bound definition shape and the predicate catalog are unchanged.
- FR-017's source separation and non-public authority are tested through the real
  catalog, including public and repair rejection with no active wound registered.
- Tests reuse complete common `action_control` fixtures, not wound-consequence
  payloads that would deliberately require an active wound. Ordinary `wound`
  controls and every unrelated negative assertion remain in place.
- The forty-seven runtime rows and one documentation guard are explicit. The
  negative rows isolate a single field from a valid old-kind control, so their
  initial PASS cannot mask the new source/link acceptance RED.
- Shared docs distinguish canonical representation from ordinary GM input. No
  twelfth selector, new JSON fence, pending file, action type or healing producer
  is introduced. FullValidation is conditional on this actual docs/example change.
- The unresolved preparation/receipt/art choices are untouched. No structural
  test is described as durable reload, terminal cleanup or full healing integration.
