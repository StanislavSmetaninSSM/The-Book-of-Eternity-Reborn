# T070 Detached Selected-Policy Preparation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax. One implementation/C# owner; parent owns acceptance.

**Goal:** Make every T067-selected `apply_deterioration` intent carry a detached,
independently checked private preparation of its exact T069 policy and any nested
complication bindings, without changing its public contract or enabling publication.

**Architecture:** T069 remains the only current-policy authority factory. T067
captures its selected typed policy, original request and zero-based ordinal in an
immutable private packet, reusing the existing T067 complication-binding owner.
Detached consumers compare complete content and reconstruct its bindings; a later
T070 publisher must still re-obtain current T067/T069 authority at common admission.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/{spec,plan,tasks}.md`, T067/T069/T070,
`contracts/mortal-wound-treatment.md` sections Typed outcomes and Natural recovery,
and `contracts/wound-effects-and-atomicity.md` in that feature.
Source baseline: accepted `e4398cdd9eefb2be3c5fd312fc98372a21ff8a5e` (runtime
`9f9f6c86`). Direct-add Task2 and its continuity addendum are parent-accepted.

## Global Constraints

- Stay on `1536-complete-wound-materialization` in
  `E:/Games/worktrees/boe-1536-wound-materialization`; preserve unrelated `.serena/`.
- Keep both existing T069 `Create` factories and their current-admission checks,
  exact public property sets and existing fingerprint byte formulas unchanged.
- The policy intent retains exactly the four common public properties plus
  `PolicyRef` and `DeteriorationAuthorityFingerprint`. No new serialized/public
  property, public constructor, command, receipt field, raw policy input or migration.
- One typed intent per original declared operation; ordinal is zero-based. Never
  replace a policy operation with a fake direct addition or negative reduction.
- Capture all three existing adverse policy kinds, including explicit `death_contour`,
  as evidence only. No selected-policy, death, heal or legacy publisher is enabled.
  IV `increase_severity` remains inapplicable, not an implicit death instruction.
- Nested complication identity/maps/root-operation keys come only from the existing
  T067 `PrepareComplicationBindings`. No effect IDs, hidden ID cache, new coordinate
  formula, suffix-on-collision or fabricated creation packet.
- The private preparation is not a new live-admission capability. T070 common
  admission must continue to recompose T067 intents through registry-current T069;
  detached agreement protects that admitted packet's contents, not current leases.
- The required unfinished legacy RED remains visible. No test deletion, skipping,
  weakened validation or inference of approval for the unanswered legacy choice.
- Use the bounded PowerShell runner, meaningful Focused selections, then one Fast.
  No PreMerge or unrelated Deep/Lifecycle/FullValidation run for this private-only
  change. Any actual shared canonical/afterlife contract change requires a new scope
  decision and the corresponding documentation/verification controls.

## Explicit publication follow-through

This is one independently testable T067 ownership deliverable, not the whole T070
policy feature. The next selected non-death publisher remains tracked under T070:
shared ordered graph; full tagged DirectAddition/PolicyAddition coordinates; exact
canonical policy diagnostic path; final-rank-based rematerialization; one batch and
one atomic wound/effect/resource/history receipt. Existing source constraints are
recorded now so the next task cannot silently discard them:

- `WoundTransitionReducer.cs:879-892` keeps `LastChangeEventRef` unchanged when final
  rank equals original rank. `2366-2379` also requires original same-rank slot budget.
- `MortalWoundTreatmentSeverityReductionPlanner.ApplyReductionScalars` sets budget
  to resulting rank; increase preview preserves the current budget. Net-zero ordered
  sequences need genuine final continuity, not transient rematerialization or an
  automatic budget reset. Viable sequences and before-claim negatives both matter.
- `WoundAcceptedTurnPlanner.cs:1407-1408` currently maps only direct additions by
  local spelling. Policy additions must use the complete tagged working reference.
- `EffectAcceptedTurnPlanner.cs:3588-3589` currently has direct-draft-only paths;
  policy child/root skill failures must name the actual canonical policy draft.
- Death requires a separate typed lifecycle handoff. Existing player
  `CheckLifeTransitions`/soul-resource commit is not a typed T067/NPC/common wound
  consumer, and current archive requires prior healing. Death is not healing.

These remain real subsequent implementation obligations, not removed mechanics.
Do not implement any of these publishers within this task.

### Task 1: Exact private policy preparation attached by the production T067 composer

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundTreatmentDeteriorationPreparation.cs`.
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentResolver.Semantics.cs`
  only the policy branch around771-804 plus an exact shared fingerprint helper.
- Create: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.DeteriorationPreparation.cs`.
- Modify: `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`, `docs/testing.md`,
  `specs/1505-test-suite-performance/research.md` for the new Integration partial,
  reviewed-heavy inventory64→65. No main class/category or runner change.
- Parent-only: this plan and `specs/1536-complete-wound-materialization/tasks.md`.
  Implementer must not stage or mark them complete.

**Interfaces consumed, already present:**

```csharp
MortalWoundDeteriorationPolicyAuthority.Create(
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    MortalWoundTreatmentAttemptCoordinates coordinates, string policyRef);
MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(
    string requestFingerprint, int operationOrdinal,
    MortalWoundComplicationProposalDraft draft, string declaredOperationFingerprint);
MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(
    int ordinal, MortalWoundTreatmentOperation operation);
MortalWoundTreatmentContract.BuildValidatedComplicationDraft(JsonElement value);
```

Keep the original five-argument internal policy-intent `Create` method unchanged.
It remains a bare closed DTO factory, not a source of private policy evidence.
The new distinct `CreatePrepared` name avoids name-only reflection ambiguity.

**Interfaces produced:**

```csharp
internal static bool MortalWoundTreatmentDeteriorationPreparation.TryCreate(
    MortalWoundTreatmentAttemptRequest request, int operationOrdinal,
    MortalWoundApplyDeteriorationOperation operation,
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    out MortalWoundTreatmentDeteriorationPreparation? preparation,
    out IReadOnlyList<ValidationIssue> issues);
internal bool MortalWoundApplyDeteriorationOutcomeIntent.TryGetPreparedDeterioration(
    MortalWoundTreatmentAttemptRequest request,
    MortalWoundApplyDeteriorationOperation operation,
    out MortalWoundTreatmentDeteriorationPreparation? preparation);
internal MortalWoundApplyDeteriorationOutcomeIntent
    MortalWoundApplyDeteriorationOutcomeIntent.DetachedCopy();
```

- [x] **Step 1: Observe a production-intent missing-preparation RED.**

Use the existing private helpers from the same partial test class. The fixture is
not a fake accepted request: it seeds policy and selected row before history and
canonical effects are prepared, then uses the real resolver. Add these helpers
and the first four theory rows before implementation:

```csharp
private static ResolverScenario CreatePolicyPreparationScenario(string resultKind,
    bool effectless = false)
{
    var scenario = CreateAdditionPublicationScenario(true);
    var result = resultKind == "add_complication"
        ? AdditionOperation("policy_preparation", effectless)
        : new JsonObject { ["kind"] = resultKind };
    scenario.Before["recovery"]!["deteriorationPolicy"] = new JsonObject
    {
        ["policyRef"] = "policy_preparation",
        ["unmetConditions"] = new JsonArray("not_stabilized"),
        ["graceMinutes"] = 30L, ["cadenceMinutes"] = 10L,
        ["result"] = result
    };
    scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray()
        .OfType<JsonObject>().Single(row => row["category"]!.GetValue<string>() == "failed_attempt")
        ["result"] = new JsonArray(new JsonObject
        { ["kind"] = "apply_deterioration", ["policyRef"] = "policy_preparation" });
    return PrepareProcedurePublicationScenario(scenario with
    { OperationKey = "operation_t070_policy_preparation", ExpectedIntentCount = 1 });
}

private static object RequirePrivatePolicyPreparation(TreatmentFlow flow)
{
    var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
    var intent = Assert.IsType<MortalWoundApplyDeteriorationOutcomeIntent>(
        Assert.Single(resolution.OutcomeIntents));
    var method = intent.GetType().GetMethod("TryGetPreparedDeterioration",
        BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.True(method is not null, "T067 must carry an independently checked private policy preparation.");
    object?[] args = { flow.Request, Assert.Single(resolution.DeclaredResult), null };
    Assert.True((bool)method!.Invoke(intent, args)!);
    return Assert.IsAssignableFrom<object>(args[2]);
}

private static object ReadPolicyMember(object value, string name)
{
    var property = value.GetType().GetProperty(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    Assert.NotNull(property);
    return Assert.IsAssignableFrom<object>(property!.GetValue(value));
}

[Theory]
[InlineData("increase_severity", false)]
[InlineData("add_complication", false)]
[InlineData("add_complication", true)]
[InlineData("death_contour", false)]
public void PolicyPreparation_ProductionIntentCarriesExactDetachedPolicy(string kind, bool effectless)
{
    var scenario = CreatePolicyPreparationScenario(kind, effectless);
    using var fixture = AcceptedStateFixture.Create(scenario);
    var flow = ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId);
    var prepared = RequirePrivatePolicyPreparation(flow);
    Assert.Equal("policy_preparation", ReadPolicyMember(prepared, "PolicyRef"));
    Assert.Equal(0, ReadPolicyMember(prepared, "OperationOrdinal"));
    Assert.Equal(ReadRequiredProperty(flow.Request, "RequestFingerprint"),
        ReadPolicyMember(prepared, "RequestFingerprint"));
}
```

Use `using System.Reflection; using System.Text.Json.Nodes;` and the existing
Services/xUnit namespaces. No reference to the absent preparation type is needed
for the initial RED. Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.PolicyPreparation_ProductionIntentCarriesExactDetachedPolicy"
```

Expected: the actual resolver succeeds and the missing-method assertion fails.
If a fixture is rejected earlier, repair only its canonical setup before claiming
semantic RED. Do not call a selected publisher in this preparation-only test.

- [ ] **Step 2: Introduce the immutable complete packet, not an authority flag.**

Implement the following complete core in the new Services file. It uses no raw
route parsing: the existing policy parser receives the already canonical request
wound. Full policy/draft/map data are recomputed, not just digest strings. Copy
construction must preserve the original seal; never reseal copied mutable data.

```csharp
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundTreatmentDeteriorationPreparation
{
    private readonly string _requestFingerprint, _coordinatesFingerprint, _beforeCanonical;
    private readonly string _woundSourcePath, _authorityFingerprint, _declaredFingerprint, _intentFingerprint;
    private readonly int _ordinal;
    private readonly MortalWoundDeteriorationPolicyDefinition _policy;
    private readonly MortalWoundComplicationProposalDraft? _draft;
    private readonly MortalWoundTreatmentComplicationBindingPreparation? _binding;
    private readonly string _fingerprint;

    private MortalWoundTreatmentDeteriorationPreparation(MortalWoundTreatmentAttemptRequest request,
        int ordinal, MortalWoundApplyDeteriorationOperation operation,
        MortalWoundDeteriorationPolicyAuthority authority, string woundSourcePath)
    {
        _requestFingerprint = request.RequestFingerprint;
        _coordinatesFingerprint = request.Coordinates.CoordinatesFingerprint;
        _beforeCanonical = WoundMaterializationContract.SerializeCanonical(request.RouteSourceWound);
        _woundSourcePath = woundSourcePath;
        _ordinal = ordinal;
        _policy = authority.Policy with { Result = authority.Policy.Result.Clone() };
        _authorityFingerprint = authority.AuthorityFingerprint;
        _declaredFingerprint = MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(ordinal, operation);
        _intentFingerprint = MortalWoundTreatmentOutcomeIntentComposer.DeteriorationIntentFingerprint(
            request.RequestFingerprint, ordinal, operation, _declaredFingerprint, _authorityFingerprint);
        _draft = DraftFor(_policy);
        _binding = _draft is null ? null : MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(
            _requestFingerprint, ordinal, _draft, _declaredFingerprint);
        _fingerprint = ComputeFingerprint();
    }

    private MortalWoundTreatmentDeteriorationPreparation(MortalWoundTreatmentDeteriorationPreparation source)
    {
        _requestFingerprint = source._requestFingerprint;
        _coordinatesFingerprint = source._coordinatesFingerprint;
        _beforeCanonical = source._beforeCanonical;
        _woundSourcePath = source._woundSourcePath;
        _authorityFingerprint = source._authorityFingerprint;
        _declaredFingerprint = source._declaredFingerprint;
        _intentFingerprint = source._intentFingerprint;
        _ordinal = source._ordinal;
        _policy = source._policy with { Result = source._policy.Result.Clone() };
        _draft = CopyDraft(source._draft);
        _binding = CopyBinding(source._binding);
        _fingerprint = source._fingerprint;
    }

    internal int OperationOrdinal => _ordinal;
    internal string RequestFingerprint => _requestFingerprint;
    internal string PolicyRef => _policy.PolicyRef;
    internal string DeteriorationAuthorityFingerprint => _authorityFingerprint;
    internal string DeclaredOperationFingerprint => _declaredFingerprint;
    internal string IntentFingerprint => _intentFingerprint;
    internal string WoundSourcePath => _woundSourcePath;
    internal string Fingerprint => _fingerprint;
    internal MortalWoundDeteriorationPolicyDefinition Policy => _policy with { Result = _policy.Result.Clone() };
    internal MortalWoundComplicationProposalDraft? Draft => CopyDraft(_draft);
    internal MortalWoundTreatmentComplicationBindingPreparation? ComplicationBinding => CopyBinding(_binding);
    internal MortalWoundTreatmentDeteriorationPreparation DetachedCopy() => new(this);

    internal static bool TryCreate(MortalWoundTreatmentAttemptRequest request, int operationOrdinal,
        MortalWoundApplyDeteriorationOperation operation,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        out MortalWoundTreatmentDeteriorationPreparation? preparation,
        out IReadOnlyList<ValidationIssue> issues)
    {
        preparation = null;
        issues = Array.Empty<ValidationIssue>();
        try
        {
            if (request is null || operation is null || acceptedState is null ||
                operationOrdinal is < 0 or >= MortalWoundTreatmentContract.MaxOutcomeOperations ||
                !MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
                !request.Coordinates.MatchesAcceptedState(acceptedState) ||
                !acceptedState.MatchesCurrentWound(request.RouteSourceWound))
                throw new InvalidOperationException("The policy request is not the current accepted wound.");
            var current = MortalWoundDeteriorationPolicyAuthority.Create(acceptedState,
                request.Coordinates, operation.PolicyRef);
            if (!current.IsValid || current.Authority is null)
            {
                issues = current.Issues.ToArray();
                return false;
            }
            var value = new MortalWoundTreatmentDeteriorationPreparation(request, operationOrdinal,
                operation, current.Authority, acceptedState.WoundSourcePath);
            if (!value.SnapshotAgrees(request, operation))
                throw new InvalidOperationException("The selected policy preparation does not match its source.");
            preparation = value;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
            JsonException or OverflowException)
        {
            issues = new[] { new ValidationIssue("treatmentAttempt.result", IssueSeverity.Error,
                "The selected Mortal wound policy could not be prepared.",
                code: "mortal_wound_treatment_policy_preparation_invalid", actor: "Client",
                section: "wound_materialization", expected: "one exact T067/T069 selected policy preparation",
                actual: exception.Message) };
            return false;
        }
    }

    internal bool AgreesWith(MortalWoundTreatmentAttemptRequest request,
        MortalWoundApplyDeteriorationOperation operation, MortalWoundApplyDeteriorationOutcomeIntent intent)
    {
        try
        {
            return intent is not null && SnapshotAgrees(request, operation) &&
                intent.Kind == "apply_deterioration" && intent.OperationOrdinal == _ordinal &&
                intent.DeclaredOperationFingerprint == _declaredFingerprint &&
                intent.IntentFingerprint == _intentFingerprint && intent.PolicyRef == _policy.PolicyRef &&
                intent.DeteriorationAuthorityFingerprint == _authorityFingerprint;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
            JsonException or OverflowException)
        { return false; }
    }

    private bool SnapshotAgrees(MortalWoundTreatmentAttemptRequest request,
        MortalWoundApplyDeteriorationOperation operation)
    {
        if (request is null || operation is null || _policy is null ||
            _ordinal is < 0 or >= MortalWoundTreatmentContract.MaxOutcomeOperations ||
            !BindingShapeIsValid(_binding) || !MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
            request.RequestFingerprint != _requestFingerprint ||
            request.Coordinates.CoordinatesFingerprint != _coordinatesFingerprint ||
            WoundMaterializationContract.SerializeCanonical(request.RouteSourceWound) != _beforeCanonical ||
            operation.PolicyRef != _policy.PolicyRef ||
            _policy.Classification != MortalWoundDeteriorationPolicyClassification.StrictlyWorsening ||
            _policy.ResultKind is not (MortalWoundDeteriorationResultKind.IncreaseSeverity or
                MortalWoundDeteriorationResultKind.AddComplication or MortalWoundDeteriorationResultKind.DeathContour) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(_authorityFingerprint) ||
            _declaredFingerprint != MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(_ordinal, operation) ||
            _intentFingerprint != MortalWoundTreatmentOutcomeIntentComposer.DeteriorationIntentFingerprint(
                _requestFingerprint, _ordinal, operation, _declaredFingerprint, _authorityFingerprint) ||
            request.RouteSourceWound.Recovery.DeteriorationPolicy is not { } policyJson)
            return false;
        var parsed = MortalWoundDeteriorationPolicyContract.Parse(policyJson,
            _woundSourcePath + ".recovery.deteriorationPolicy", request.RouteSourceWound.Owner.Realm,
            WoundMaterializationContract.ResolveEffectTargetKind(request.RouteSourceWound.Owner.OwnerKind),
            request.RouteSourceWound.Severity.Rank);
        if (!parsed.IsValid || parsed.Policy is null || Canonical(parsed.Policy) != Canonical(_policy)) return false;
        var draft = DraftFor(parsed.Policy);
        var binding = draft is null ? null : MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(
            _requestFingerprint, _ordinal, draft, _declaredFingerprint);
        return Canonical(draft) == Canonical(_draft) &&
            BindingFields(binding).SequenceEqual(BindingFields(_binding), StringComparer.Ordinal) &&
            _fingerprint == ComputeFingerprint();
    }

    private string ComputeFingerprint() => WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
    {
        "book_of_eternity.mortal_wound_treatment.selected_policy_preparation", "1",
        _requestFingerprint, _coordinatesFingerprint, _beforeCanonical, _woundSourcePath,
        _ordinal.ToString(CultureInfo.InvariantCulture), _authorityFingerprint,
        _declaredFingerprint, _intentFingerprint, Canonical(_policy), Canonical(_draft)
    }.Concat(BindingFields(_binding)));

    private static string? Canonical<T>(T value) =>
        WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(value));

    private static MortalWoundComplicationProposalDraft? DraftFor(MortalWoundDeteriorationPolicyDefinition policy) =>
        policy.ResultKind == MortalWoundDeteriorationResultKind.AddComplication
            ? MortalWoundTreatmentContract.BuildValidatedComplicationDraft(policy.Result.GetProperty("complicationDraft")) : null;

    private static MortalWoundComplicationProposalDraft? CopyDraft(MortalWoundComplicationProposalDraft? draft) =>
        draft is null ? null : draft with
        {
            Complication = draft.Complication with { },
            ConsequenceDefinitions = draft.ConsequenceDefinitions.Select(row => row with
            { Definition = row.Definition.Clone(), Root = row.Root is null ? null : row.Root with
              { Slots = row.Root.Slots.Select(slot => slot with { }).ToImmutableArray() } }).ToImmutableArray()
        };

    private static MortalWoundTreatmentComplicationBindingPreparation? CopyBinding(
        MortalWoundTreatmentComplicationBindingPreparation? binding) => binding is null ? null :
        MortalWoundTreatmentComplicationBindingPreparation.Create(binding.ComplicationRef, binding.ComplicationId,
            binding.DefinitionReferenceBindings, binding.ApplicationReferenceBindings,
            binding.PreparationFingerprint, binding.Roots);

    private static bool BindingShapeIsValid(MortalWoundTreatmentComplicationBindingPreparation? binding) =>
        binding is null || (!binding.DefinitionReferenceBindings.IsDefault &&
            !binding.ApplicationReferenceBindings.IsDefault && !binding.Roots.IsDefault &&
            binding.DefinitionReferenceBindings.All(row => row is not null) &&
            binding.ApplicationReferenceBindings.All(row => row is not null) &&
            binding.Roots.All(row => row is not null));

    private static IEnumerable<string?> BindingFields(MortalWoundTreatmentComplicationBindingPreparation? binding)
    {
        if (binding is null) { yield return null; yield break; }
        yield return "complication";
        yield return binding.ComplicationRef; yield return binding.ComplicationId;
        yield return binding.PreparationFingerprint;
        yield return binding.DefinitionReferenceBindings.Length.ToString(CultureInfo.InvariantCulture);
        foreach (var row in binding.DefinitionReferenceBindings)
        { yield return row.LocalRef; yield return row.NamespacedRef; }
        yield return binding.ApplicationReferenceBindings.Length.ToString(CultureInfo.InvariantCulture);
        foreach (var row in binding.ApplicationReferenceBindings)
        { yield return row.LocalRef; yield return row.NamespacedRef; }
        yield return binding.Roots.Length.ToString(CultureInfo.InvariantCulture);
        foreach (var row in binding.Roots)
        { yield return row.LocalDefinitionRef; yield return row.DefinitionRef;
          yield return row.ApplicationRef; yield return row.OperationKey; }
    }
}
```

The implementation may factor small repetitions without changing these contracts.
Its seal and complete agreement are mandatory, including zero-definition/root drafts.
Fail-closed handling must also cover deliberately corrupted private null/default
members at the access boundary without crashing; use explicit guards rather than
blanket swallowing unrelated exceptions. Add their exact regression before the guard.

- [x] **Step 3: Attach the packet without adding a public property.**

Place this partial-class extension in the same new Services file. The existing
original constructor/factory remains untouched in its current files. The private
copy retains the original packet seal and the public six-member DTO shape.

```csharp
internal sealed partial class MortalWoundApplyDeteriorationOutcomeIntent
{
    private readonly MortalWoundTreatmentDeteriorationPreparation? _preparedDeterioration;

    private MortalWoundApplyDeteriorationOutcomeIntent(MortalWoundTreatmentDeteriorationPreparation prepared)
        : this(prepared.OperationOrdinal, "apply_deterioration", prepared.DeclaredOperationFingerprint,
            prepared.IntentFingerprint, prepared.PolicyRef, prepared.DeteriorationAuthorityFingerprint)
    { _preparedDeterioration = prepared.DetachedCopy(); }

    internal static MortalWoundApplyDeteriorationOutcomeIntent CreatePrepared(
        MortalWoundTreatmentDeteriorationPreparation prepared) => new(prepared);

    internal bool TryGetPreparedDeterioration(MortalWoundTreatmentAttemptRequest request,
        MortalWoundApplyDeteriorationOperation operation,
        out MortalWoundTreatmentDeteriorationPreparation? preparation)
    {
        preparation = null;
        if (_preparedDeterioration is null || !_preparedDeterioration.AgreesWith(request, operation, this)) return false;
        preparation = _preparedDeterioration.DetachedCopy();
        return true;
    }

    private MortalWoundApplyDeteriorationOutcomeIntent(MortalWoundApplyDeteriorationOutcomeIntent source)
        : this(source.OperationOrdinal, source.Kind, source.DeclaredOperationFingerprint,
            source.IntentFingerprint, source.PolicyRef, source.DeteriorationAuthorityFingerprint)
    { _preparedDeterioration = source._preparedDeterioration?.DetachedCopy(); }

    internal MortalWoundApplyDeteriorationOutcomeIntent DetachedCopy() => new(this);
}
```

In the existing T067 composer, replace ONLY the policy branch with:

```csharp
if (operation is MortalWoundApplyDeteriorationOperation deterioration)
{
    if (!MortalWoundTreatmentDeteriorationPreparation.TryCreate(request, ordinal,
            deterioration, acceptedState, out var preparedPolicy, out var policyIssues))
    {
        failures.AddRange(policyIssues);
        continue;
    }
    intent = MortalWoundApplyDeteriorationOutcomeIntent.CreatePrepared(preparedPolicy!);
}
```

Add this method to the same existing composer class; its expression is exactly the
old policy-intent formula, not a version or meaning change:

```csharp
internal static string DeteriorationIntentFingerprint(string requestFingerprint, int ordinal,
    MortalWoundApplyDeteriorationOperation operation, string declaredFingerprint, string authorityFingerprint) =>
    WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
    {
        "book_of_eternity.mortal_wound_treatment.deterioration_intent", "1",
        IntentFingerprint(requestFingerprint, ordinal, operation, declaredFingerprint),
        operation.PolicyRef, authorityFingerprint
    });
```

No `HasSupportedGrammar`, effect stage, finalizer, canonical parser, reducer, current
authority registry or published receipt change belongs in this task.

- [ ] **Step 4: Prove full agreement, deterministic nested identities and detachment.**

Expand the initial production test after observing RED. Inspect the actual packet's
typed Policy, exact declared/public intent fingerprints, and nested binding (or exact
null on increase/death). Derive the expected nested binding with the existing owning
helper, using the ORIGINAL policy operation's declared fingerprint, not a fabricated
`add_complication` operation. Test root and child definition mapping order, root-only
application mapping, and effectless0/0. Concrete core assertion:

```csharp
var request = (MortalWoundTreatmentAttemptRequest)flow.Request;
var resolution = (MortalWoundTreatmentResolution)flow.Resolution;
var operation = (MortalWoundApplyDeteriorationOperation)resolution.DeclaredResult[0];
var intent = (MortalWoundApplyDeteriorationOutcomeIntent)resolution.OutcomeIntents[0];
Assert.True(intent.TryGetPreparedDeterioration(request, operation, out var prepared));
var authority = MortalWoundDeteriorationPolicyAuthority.Create(
    (MortalWoundTreatmentAcceptedStateAuthority)flow.AcceptedState, request.Coordinates, operation.PolicyRef);
Assert.True(authority.IsValid);
Assert.Equal(authority.Authority!.AuthorityFingerprint, prepared!.DeteriorationAuthorityFingerprint);
Assert.Equal(authority.Authority.Policy.CanonicalProjection, prepared.Policy.CanonicalProjection);
Assert.Equal(MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(0, operation), prepared.DeclaredOperationFingerprint);
Assert.Equal(intent.IntentFingerprint, prepared.IntentFingerprint);
if (prepared.Draft is { } draft)
{
    var expected = MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(
        request.RequestFingerprint, 0, draft, prepared.DeclaredOperationFingerprint);
    var actual = prepared.ComplicationBinding!;
    Assert.Equal(expected.ComplicationId, actual.ComplicationId);
    Assert.Equal(expected.PreparationFingerprint, actual.PreparationFingerprint);
    Assert.Equal(expected.DefinitionReferenceBindings.Select(row => (row.LocalRef, row.NamespacedRef)).ToArray(),
        actual.DefinitionReferenceBindings.Select(row => (row.LocalRef, row.NamespacedRef)).ToArray());
    Assert.Equal(expected.ApplicationReferenceBindings.Select(row => (row.LocalRef, row.NamespacedRef)).ToArray(),
        actual.ApplicationReferenceBindings.Select(row => (row.LocalRef, row.NamespacedRef)).ToArray());
    Assert.Equal(expected.Roots.ToArray(), actual.Roots.ToArray());
}
else Assert.Null(prepared.ComplicationBinding);
Assert.False(MortalWoundApplyDeteriorationOutcomeIntent.Create(intent.OperationOrdinal,
    intent.DeclaredOperationFingerprint, intent.IntentFingerprint, intent.PolicyRef,
    intent.DeteriorationAuthorityFingerprint).TryGetPreparedDeterioration(request, operation, out _));
```

Required additional test methods under `PolicyPreparation_`:

1. `ExactRetryAndPersistedRehydrationKeepPrivatePacket`: resolve exact same request,
   then `PersistAndRehydrateTreatmentPublication(fixture, flow, "policy preparation")`;
   require both original and restored packet, equal private fingerprints, equal
   complete public resolution/intent projection and unchanged wound/index/history.
   Persistence reobtains T067/T069; do not serialize the private packet.
2. `RepeatedEffectlessPolicyKeepsDistinctOrdinalBindings`: replace failed result with
   two identical policy-ref operations using an effectless nested complication.
   Both private packets valid; ordinals0/1 and distinct complication IDs and preparation
   fingerprints, zero maps/roots. Repeated policy spelling is not a forbidden repeated
   direct-add local selector. Add direct+policy same local spelling as a separate
   positive using the same effectless draft and operation ordinals0/1.
3. `BorrowedRequestAndChangedPolicyReject`: another real request operation key has
   different request/coordinate authority. The original packet cannot be retrieved
   against that request; a changed operation.PolicyRef also rejects. A packet from a
   same-ref policy with different grace/cadence/result cannot be borrowed into the
   original. Use separate real fixtures for different canonical policies.
4. `PrivatePayloadTamperRejects`: clone the packet with the reflection helper below,
   alter each of `_policy` ResultKind/Result/CanonicalProjection, `_draft`, `_binding`
   ComplicationId/definition-map order/application-map order/private root OperationKey,
   `_authorityFingerprint`, `_woundSourcePath`, `_ordinal`, `_fingerprint`, and mandatory
   private null/defaults. Preserve the predecessor seal. Call `AgreesWith` and require
   false, no exception. Construct multi-definition/root draft for real order negatives;
   reversing a singleton is not a regression. Its graph must remain canonical-valid.
5. `ReturnedCopiesCannotChangeTheAttachedPacket`: corrupt a returned packet and its
   returned binding/draft copies using reflection; fresh retrieval from original intent
   remains valid and byte/field equal to original. Copies preserve stale seals, not
   recomputed seals, so structurally shaped corruption also remains detectable after
   `DetachedCopy`. Separately alter public intent fields and prove its `DetachedCopy`
   preserves those altered fields and still rejects; it cannot reconstruct clean public
   fields from the private packet and thereby launder a forged predecessor. Malformed
   private null/defaults are checked through `AgreesWith`/`TryGetPreparedDeterioration`
   before any trusted copy utility is called, not by dereferencing corrupt getters.
6. `PublicShapeAndPublicationBoundaryRemainClosed`: exact six public members,
   independent old-formula assertions below and no private
   packet in JSON/receipt. Call the existing selected outcome `Prepare` against the
   real resolution; it still rejects with `mortal_wound_treatment_publication_slice_unsupported`.
   No effect ID or canonical mutation is permitted by this preparation alone.
7. `PartialAndInterruptedCourseKeepPolicyPreparation`: exercise a real partial
   procedure with recovery then policy (policy ordinal1), and a real course's second
   interrupted milestone (policy ordinal0). This protects the existing legal modes
   against accidentally narrowing the new factory to a pristine procedure request.
   For partial, use `CreateRecoveryPublicationScenario("procedure", "partial_success",
   "a1")`, install the same policy as the preparation helper, append the policy to
   only the partial row, and reseed history before `AcceptedStateFixture.Create`.
   The complete interrupted-course setup is:

```csharp
var scenario = CreateScalarCoursePublicationScenario();
scenario.Before["recovery"]!["deteriorationPolicy"] = new JsonObject
{
    ["policyRef"] = "policy_preparation", ["unmetConditions"] = new JsonArray("not_stabilized"),
    ["graceMinutes"] = 30L, ["cadenceMinutes"] = 10L,
    ["result"] = new JsonObject { ["kind"] = "increase_severity" }
};
scenario.Before["treatment"]!["routes"]![0]!["interruption"]!["result"] =
    new JsonArray(new JsonObject { ["kind"] = "apply_deterioration", ["policyRef"] = "policy_preparation" });
scenario = PrepareProcedurePublicationScenario(scenario);
using var fixture = AcceptedStateFixture.Create(scenario);
var initial = ResolveCurrentTreatment(fixture, "course", scenario.OperationKey, scenario.RouteId);
ComposeAndPublishTreatment(fixture, initial);
fixture.PrepareNextTurn(43, 1_081, "policy_preparation_interruption");
var interrupted = ResolveCurrentTreatment(fixture, "course", scenario.OperationKey + "_interrupt", scenario.RouteId);
var resolution = (MortalWoundTreatmentResolution)interrupted.Resolution;
Assert.True(resolution.Interruption);
Assert.Equal("interrupted", resolution.CourseDisposition);
RequirePrivatePolicyPreparation(interrupted);
// Only the already-supported first milestone was published; do not publish the policy.
```

Do not call the existing `AssertClosedOutcomeIntent` on the new fixture: that helper
also asserts a hardcoded `t061_strict_deterioration` ref. Use its existing underlying
`AssertClosedProperties(intent, new[] { "OperationOrdinal", "Kind",
"DeclaredOperationFingerprint", "IntentFingerprint", "PolicyRef",
"DeteriorationAuthorityFingerprint" })` and this independent original-byte oracle:

```csharp
var ordinal = intent.OperationOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
var baseIntent = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
{
    "book_of_eternity.mortal_wound_treatment.outcome_intent", "1", request.RequestFingerprint,
    ordinal, "apply_deterioration", intent.DeclaredOperationFingerprint, operation.PolicyRef
});
Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
{
    "book_of_eternity.mortal_wound_treatment.deterioration_intent", "1", baseIntent,
    operation.PolicyRef, authority.Authority!.AuthorityFingerprint
}), intent.IntentFingerprint);
```

Use this mechanical clone helper for private-payload negatives; it never rewrites
accepted files or relaxes a source guard:

```csharp
private static T ClonePolicyField<T>(T source, string fieldName, object? value) where T : class
{
    var clone = (T)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(source, null)!;
    FieldInfo? field = null;
    for (Type? type = source.GetType(); type is not null && field is null; type = type.BaseType)
        field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field!.SetValue(clone, value);
    return clone;
}
```

Keep each negative independent by copying from the original, never from a preceding
forged packet. Add the meaningful semantic RED before its corresponding guard.
Use existing T067 helper tests for complete typed draft rows rather than inventing
unsupported profiles/stack keys. Full tree equality applies to rejection and
nonpublishing packet operations; successful persisted-command tests intentionally
change command/claim roots but not canonical wound/effect/history roots.

- [x] **Step 5: Verify focused preservation and truthful documentation boundary.**

Run smallest changed rows first, then this coherent set (split a measured oversized
selection into disjoint owners;10m is the implementation default,15m needs evidence):

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.PolicyPreparation_|FullyQualifiedName~MortalWoundTreatmentResolverTests.DeteriorationPolicyAuthority_|FullyQualifiedName~MortalWoundTreatmentResolverTests.OutcomeIntents_AreProductionDerivedOneForEachDeclaredOperation|FullyQualifiedName~MortalWoundTreatmentResolverTests.DeteriorationHandoff_UsesTheExactT069TypedAuthorityFactory|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseContinuation_InterruptionReservesNoCurrentOrFutureDose"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentComplicationBindingTests|FullyQualifiedName~FastTestBoundaryTests|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatment"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
git diff --check
```

Record exact actual selected methods, summaries, all TRX outcomes/counters, build
warnings/errors, cleanup/timeout/duplicate facts and fail-fast omission if any.
No Fast probe is a second suite run. Do not rerun broad lanes merely for counts.

GM decision: this task changes only private client-owned T067 evidence. The public
authoring/policy/receipt language and selected publication boundary are unchanged;
no new GM example, Mortal prompt or afterlife matrix/manifest change is required.
The existing worked example and source guards must still truthfully say selected
policy/heal publication is unfinished. Record this rationale in the report. The
next actual policy publisher must update its worked example/guide/guards together.

- [x] **Step 6: Scoped commit and independent task gate.**

Inspect only the allowlist, then stage the two implementation files, new Integration
partial and three test-inventory files. The new inventory entry is
`MortalWoundTreatmentResolverTests.DeteriorationPreparation.cs` in both existing
FastTestBoundary arrays and the reviewed-heavy source table. Commit:

```powershell
git add -- BookOfEternityClient/Services/MortalWoundTreatmentDeteriorationPreparation.cs BookOfEternityClient/Services/MortalWoundTreatmentResolver.Semantics.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.DeteriorationPreparation.cs BookOfEternityClient.Tests/FastTestBoundaryTests.cs docs/testing.md specs/1505-test-suite-performance/research.md
git diff --cached --check
git commit -m "feat(wounds): prepare exact selected deterioration policy evidence (#1536) [skip ci]"
```

Report exact BASE/HEAD, source changes, observed REDs and actual GREEN controls,
remaining required legacy RED, no publication/GM contract change and concerns.
Parent generates an exact BASE..HEAD review package and obtains independent Spec
Compliance and Code Quality verdicts before accepting this task. Parent verifies
the actual artifacts and owns checkboxes; T070/T177/#1536 remain open.

## Parent self-review

### Bounded functional acceptance — 2026-09-07

Parent accepts the functional deliverable at `caa2ef1708af04c666746310d8e7e3a7a25a8d90`
from BASE `81c903700494d647c2462120c37b9a2933afbceb`, after exact source review,
actual artifact inspection and independent corrective Spec/Code Quality review.
Code Quality is Approved with no current code/test blocker. Steps 2 and 4 retain
their unchecked process status deliberately: their implementation and current
regression coverage are verified, but the required detailed RED-before-guard
chronology was not followed. Do not repeat completed implementation to turn these
historical process boxes green; this is a recorded deviation, not pending code.

Actual final Integration control: 34/34 GREEN (`031913`); preservation: 23/23
GREEN (`031026`). Four later isolated counterfactual guard checks each failed
semantically as expected, and the restored-runtime test-only final delta passed
2/2 (`033956`, 00:01:08.9341715). The delta adds one direct copy-seal assertion;
runtime remains byte-identical to `177a734e`. Counterfactual checks establish
current sensitivity only and do not constitute historical TDD compliance.

One Fast (`031300`) completed 6,016 rows: 6,015 passed and the required legacy
authority/registration RED failed; 1,287 discovered rows did not complete under
fail-fast. No complete Fast success, publication support, top-level T070/T177
closure or whole-#1536 completion is claimed. This private-only handoff changes
no GM-authored contract; current examples/guards correctly retain unfinished
publisher wording. Next work is the selected non-death policy publisher.

Detailed parent audit, implementer chronology and both independent verdicts are
in worktree-local `sdd/t070-selected-policy-preparation-*` metadata reports.

### Original pre-dispatch self-review

This bounded plan covers the missing T067-owned policy body/binding handoff, not
the remaining feature. Public T067/T069 shapes/formulas and current authority stay
unchanged. New preparation receives no caller-supplied fingerprint/policy JSON.
Its complete fields, cloning and fresh/live versus detached authority split are
explicit. Original selected policy/death grammar and unfinished publishers remain
visible. Next final-rank/budget/reference/diagnostic/lifecycle obligations are
source-pinned above; no game mechanic has been excluded to simplify this task.
Source preflight `sdd/t070-selected-policy-test-fixture-preflight.md` was fully read
by the parent, and actual helpers were checked. Parent corrected public-only test
reflection for the internal packet, reference-object map comparisons, private
null/default guards and exact copy semantics before dispatch. In particular an
intent copy preserves corrupted public fields; it cannot regenerate trusted-looking
values from its private preparation. The existing six-member public shape and
original fingerprint formula are independently asserted rather than self-compared.
