# Closed Wound-Legacy Source Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Complete #1536 T070's bounded lower-level source-registration prerequisite and make its existing mandatory source test pass without pretending that heal/legacy publication is complete.

**Architecture:** Explicit internal catalog exports may resolve non-public `wound_legacy` definitions. Forged public exports are rejected before any owner/entry registration. The ordinary accepted-turn composer rejects and filters caller-supplied generic legacy exports, keeping fresh legacy authority closed until the separately approved preparation adapter exists.

**Tech Stack:** C#/.NET 8, xUnit, PowerShell 7 bounded runner. No new dependency.

## Global Constraints

- Tracked source: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), open T070 in `specs/1536-complete-wound-materialization/tasks.md`; governance is `AGENTS.md` and `.specify/memory/constitution.md`.
- Use the existing `1536-complete-wound-materialization` branch/worktree. No migration, new branch, remote action, unrelated `.serena/` edit or unresolved legacy/art-schema decision.
- A mechanical legacy has exact client-only, non-public, non-GM-materializable source kind `wound_legacy`; its source ID will be the deterministic legacy ID supplied by the future approved producer.
- Lower-level `EffectSourceAuthorityInput` is an explicit internal catalog input, not proof of accepted game state. This task does not create a legacy producer or allow generic accepted-plan exports to impersonate one.
- Register only this exact kind. A `Materializable=true` legacy export must fail with `effect_source_wound_legacy_public_materialization_forbidden` before owner, alias, reference or entry registration.
- Ordinary public Resolve and ResolveRepairCandidate must not materialize a valid non-public legacy; canonical binding can resolve it independently of an active wound.
- Ordinary accepted-turn composition must reject and exclude caller-supplied generic `wound_legacy` exports with `effect_source_wound_legacy_external_export_forbidden`, regardless of realm, Materializable or SameTurn flags.
- Existing exact `wound` groups, typed wound root binding, active-wound cleanup, and the existing generic-wound injection rejection remain unchanged.
- Full T070 remains open: canonical source/link vocabulary, typed legacy preparation/batches, history reconstruction/cold reload, finalization, post-heal effect removal/provenance and actual atomic publication are not completed by catalog registration.
- One C#/source owner. Focused/Fast bounded at five minutes; complete every run before source edits. Do not skip or weaken the existing mandatory failing source test or extend Fast's bound.

## Source-backed scope and GM boundary

Approved `contracts/mortal-wound-treatment.md` and `wound-canonical-lifecycle.md`
already require the exact non-public legacy source and independent provenance.
The frozen `MortalWoundTreatmentContractTests.WoundLegacySource_SurvivesWithoutActiveWoundButIsNeverPubliclyMaterializable`
fails first because SourceAuthority's supported set lacks that kind. Its second
guard already requires the exact forged-public diagnostic.

Simply adding the kind is insufficient: Compose currently passes externally
supplied legacy exports through. This task closes that injection seam in the same
change. It does not decide how private preparation stores immutable full readable
provenance; the pending three-property preparation choice remains unanswered.

EffectMaterializationContract's canonical SourceKinds/LinkKinds and
EffectSourceDefinitionContract's LinkKinds intentionally remain unchanged here.
There is no working legacy canonical roundtrip or runtime producer yet. No new
public/GM-authorable effect, command, state, pending/control, receipt, report or
afterlife mechanic is enabled. Generic legacy injection was rejected before by
unknown-kind validation and is still rejected, now explicitly before catalog
registration. The dedicated diagnostics describe internal composition mistakes,
not a new GM authoring workflow. Existing Mortal/afterlife prompts, matrix,
examples, manifest and source guards need no change for this bounded prerequisite;
the full T070 integration retains mandatory GM/example synchronization. No
conditional FullValidation for this internal-only slice.

## File map

- Modify `BookOfEternityClient/Services/EffectSourceAuthority.cs`: exact supported kind and early public-export guard only.
- Modify `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`: filter generic legacy exports and add explicit rejection only.
- Create `BookOfEternityClient.Tests/WoundLegacySourceAuthorityTests.cs`: 28 in-memory catalog/composer rows.
- Keep the existing Mortal contract test unchanged. Parent owns plan/task evidence.

### Task 1: Register non-public legacy sources without opening generic authority

- [ ] **Step 1: Add the complete 28-row regression fixture.**

Create the following file. It uses the existing definition fixture with its
active-wound link removed, as the already-frozen contract test does. No actual
wound/history/accepted legacy authority is fabricated.
```csharp
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundLegacySourceAuthorityTests
{
    private const string LegacyId = "wound_legacy_healed_catalog";
    private const string DefinitionKey = "legacy_residual_tremor";
    private const string LegacyRef = "legacyref_catalog";

    private static EffectSourceExport Source(
        string realm = "mortal_world", bool sameTurn = false)
    {
        var definition = WoundContractTestData.CreateOwnedEffectDefinition(
            "already_healed_wound", realm, DefinitionKey, "action_control");
        definition["links"] = new JsonArray();
        return new EffectSourceExport(
            realm, "wound_legacy", LegacyId, new JsonArray(definition),
            Materializable: false, Active: true, SameTurn: sameTurn,
            SourceRef: sameTurn ? LegacyRef : null);
    }

    private static EffectSourceKey Key(string realm) =>
        new(realm, "wound_legacy", LegacyId, DefinitionKey);

    private static JsonObject Selector(EffectSourceExport source) => new()
    {
        ["kind"] = source.Kind,
        [source.SameTurn ? "sourceRef" : "sourceId"] =
            source.SameTurn ? source.SourceRef : source.SourceId,
        ["definitionKey"] = DefinitionKey
    };

    private static EffectSourceAuthority Build(
        bool sameTurn, params EffectSourceExport[] sources) =>
        EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            sameTurn ? Array.Empty<EffectSourceExport>() : sources,
            sameTurn ? sources : Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));

    private static EffectAcceptedTurnInput Compose(
        string realm, IReadOnlyList<EffectSourceExport> exports)
    {
        var empty = new EffectCarrierCatalogInput(
            null, null, null, null, null, null);
        return EffectAcceptedTurnInputComposer.Compose(
            "session_legacy_catalog", "snapshot_legacy_catalog", 1,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(),
            empty, empty, null, new Dictionary<string, JsonNode?>(),
            acceptedPlanSourceExports: exports, realm: realm);
    }

    [Theory]
    [InlineData("mortal_world", false)]
    [InlineData("mortal_world", true)]
    [InlineData("chaos_sea", false)]
    [InlineData("chaos_sea", true)]
    [InlineData("shining_abode", false)]
    [InlineData("shining_abode", true)]
    public void ExplicitNonPublicCatalogSource_ResolvesWithoutActiveWound(
        string realm, bool sameTurn)
    {
        var source = Source(realm, sameTurn);
        var authority = Build(sameTurn, source);

        Assert.Empty(authority.Issues);
        var canonical = authority.ResolveCanonicalBinding(Key(realm), "player");
        Assert.True(canonical.Success);
        Assert.NotNull(canonical.Source);
        Assert.Equal(Key(realm), canonical.Source.Key);
        Assert.False(canonical.Source.Materializable);
        Assert.Equal(sameTurn, canonical.Source.SameTurn);
        Assert.True(JsonNode.DeepEquals(
            source.Definitions[0], canonical.Source.Definition));

        var selector = Selector(source);
        var publicAttempt = authority.Resolve(
            selector, realm, "player", new JsonObject());
        Assert.False(publicAttempt.Success);
        Assert.Contains(publicAttempt.Issues, static issue =>
            issue.Code == "effect_source_not_materializable");
        var repairAttempt = authority.ResolveRepairCandidate(
            selector, realm, "player");
        Assert.False(repairAttempt.Success);
        Assert.Contains(repairAttempt.Issues, static issue =>
            issue.Code == "effect_source_not_materializable");

        Assert.Empty(authority.SnapshotSameTurnWoundEntries());
        Assert.Empty(authority.SnapshotSameTurnWoundGroups());
        Assert.Empty(authority.SnapshotWoundGroupAuthorities());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ForgedPublicExport_IsRejectedBeforeItCanPoisonValidSibling(
        bool sameTurn, bool forgedFirst)
    {
        var valid = Source(sameTurn: sameTurn);
        var forged = valid with { Materializable = true };
        var sources = forgedFirst
            ? new[] { forged, valid }
            : new[] { valid, forged };

        var authority = Build(sameTurn, sources);

        var issue = Assert.Single(authority.Issues);
        Assert.Equal(
            "effect_source_wound_legacy_public_materialization_forbidden",
            issue.Code);
        var canonical = authority.ResolveCanonicalBinding(Key("mortal_world"), "player");
        Assert.True(canonical.Success);
        Assert.NotNull(canonical.Source);
        Assert.False(canonical.Source.Materializable);
        var attempt = authority.Resolve(
            Selector(valid), "mortal_world", "player", new JsonObject());
        Assert.False(attempt.Success);
        Assert.Contains(attempt.Issues, static value =>
            value.Code == "effect_source_not_materializable");
    }

    [Theory]
    [InlineData("WOUND_LEGACY")]
    [InlineData("wound_legacy ")]
    [InlineData("wound_legacY")]
    public void LookalikeKind_IsNotRegistered(string kind)
    {
        var source = Source() with { Kind = kind };
        var authority = Build(false, source);

        Assert.Contains(authority.Issues, static issue =>
            issue.Code == "effect_source_authority_invalid_export");
        Assert.False(authority.ResolveCanonicalBinding(
            new EffectSourceKey("mortal_world", kind, LegacyId, DefinitionKey),
            "player").Success);
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
    public void Composer_RejectsAndExcludesGenericLegacyExports(
        string realm, bool sameTurn, bool materializable)
    {
        var source = Source(realm, sameTurn) with
        {
            Materializable = materializable
        };

        var input = Compose(realm, new[] { source });

        Assert.Contains(input.SourceAuthority.Issues, static issue =>
            issue.Code == "effect_source_wound_legacy_external_export_forbidden");
        Assert.False(input.SourceAuthority.ResolveCanonicalBinding(
            Key(realm), "player").Success);
        Assert.DoesNotContain(input.SourceAuthority.Issues, static issue =>
            issue.Code is "effect_source_authority_invalid_export"
                or "effect_source_wound_legacy_public_materialization_forbidden");
        Assert.Empty(input.SourceAuthority.SnapshotSameTurnWoundEntries());
        Assert.Empty(input.SourceAuthority.SnapshotSameTurnWoundGroups());
    }

    [Theory]
    [InlineData("mortal_world")]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void Composer_WithoutLegacyInjection_PreservesEmptyControl(string realm)
    {
        var input = Compose(realm, Array.Empty<EffectSourceExport>());

        Assert.Empty(input.SourceAuthority.Issues);
        Assert.False(input.SourceAuthority.ResolveCanonicalBinding(
            Key(realm), "player").Success);
    }
}
```

- [ ] **Step 2: Observe semantic RED before production changes.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~WoundLegacySourceAuthorityTests|FullyQualifiedName~WoundLegacySource_SurvivesWithoutActiveWoundButIsNeverPubliclyMaterializable"
```

Expected 29 executed: 23 semantic failures (6 supported catalog, 4 forged sibling,
12 explicit composer rejection and 1 existing source test), 6 passing negative/
empty controls. Inspect actual messages: missing supported source / specific
guard, not compile or fixture errors. If expected semantic counts differ, explain
the actual cause before editing production; do not fabricate RED evidence.

- [ ] **Step 3: Register the kind with an early forged-public guard.**

Add only `"wound_legacy"` beside `"wound"` in EffectSourceAuthority.SourceKinds.
In Builder.AddExport, insert this block immediately after supported-kind/exact
realm/id validation, before RequiredApplicationAuthority validation and before
normalized export, owner or entry creation:
```csharp
            if (string.Equals(export.Kind, "wound_legacy", StringComparison.Ordinal) &&
                export.Materializable)
            {
                Issues.Add(NewIssue(
                    sourcePath,
                    "effect_source_wound_legacy_public_materialization_forbidden",
                    "non-public wound legacy source export",
                    export.ToString()));
                return;
            }
```

Do not broaden SnapshotSameTurnWoundEntries/Groups or typed active-wound binding.
Do not add canonical effect/link vocabulary yet or relax definition validation.

- [ ] **Step 4: Close the ordinary composer injection seam.**

In Compose's planSourceExports pipeline, keep the existing exact wound filter
and add this filter immediately afterward, before Concat(preparedWounds.Exports):
```csharp
            .Where(static export => !string.Equals(
                export.Kind,
                "wound_legacy",
                StringComparison.Ordinal))
```

In ComposePreparedWoundSources, after the existing injectedWounds rejection
block and before `if (prepared is null)`, insert:
```csharp
        var injectedLegacies = (externallySuppliedExports ??
                Array.Empty<EffectSourceExport>())
            .Count(static export => export is not null && string.Equals(
                export.Kind,
                "wound_legacy",
                StringComparison.Ordinal));
        if (injectedLegacies != 0)
        {
            issues.Add(WoundCompositionIssue(
                "effect_source_wound_legacy_external_export_forbidden",
                "Wound legacy source exports require private approved legacy preparation.",
                "no caller-supplied generic wound legacy exports",
                injectedLegacies.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)));
        }
```

This is an explicit rejection, not a private preparation implementation. Preserve
all existing prepared wound binding validation and existing wound rejection text.

- [ ] **Step 5: Confirm GREEN, affected owners and one bounded Fast.**

Run Step 2's exact command: expected29/29 PASS, including the unchanged mandatory
legacy source test. Then:
```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~EffectSourceAuthorityTests|FullyQualifiedName~EffectAcceptedTurnInputComposerWoundTests|FullyQualifiedName~WoundMaterializationSourceGuardTests"
.\scripts\test-csharp.ps1 -Lane Fast
git diff --check
```

One Fast only, no new timeout or diagnostic lane. If Fast surfaces another
unfinished feature test, record its exact name/artifact/count rather than fixing
unrelated work, claiming all GREEN or suppressing it. Distinguish discovery log
rows from completed TRX Total; report arithmetic uncompleted difference only.
Audit actual semantic outcomes, build warnings/errors, cleanup, timeouts and
duplicates. Finish every test command before editing source.

- [ ] **Step 6: Self-review, exact commit and independent acceptance.**

Commit only the three named code/test files. Parent inspects exact recorded task
BASE..candidate, actual artifacts, and fresh independent Spec Compliance plus
Quality review. Keep full T070 and T177 open. Record the no-GM-update rationale
and remaining canonical/history/publication boundaries in the report. Do not
resolve the pending legacy preparation decision, create a hash metadata cache or
manufacture a typed accepted legacy authority to get a test green.

## Parent self-review

- Source-backed defect is exact: lower-level SourceKinds lacks the required
  source; AddExport registers owners before later definition validation, so the
  public guard must precede registration. Valid plus forged same-key siblings
  in both orders test absence of duplicate/alias poisoning as well as rejection.
- Adding the catalog kind alone would open a generic source path. The paired
  composer filter and explicit rejection are mandatory, tested across all three
  realms and both SameTurn/Materializable flags. Existing wound injection tests
  remain part of the focused owner control.
- New fixture counts are six valid + four forged + three lookalike + twelve
  composer injection + three empty controls =28, plus one frozen test =29.
  Before implementation, 22 new failures + existing failure =23; six controls
  already pass. All helpers/types/signatures were verified against current source.
  Parent also checked the actual composer test class name (it differs from its
  filename) and uses EffectAcceptedTurnInputComposerWoundTests in the owner filter.
- Generic catalog resolution is not an accepted legacy roundtrip; no active
  wound/history fixture, synthetic private preparation, GM surface or parallel
  publisher is introduced. All unresolved full T070 obligations stay tracked.
