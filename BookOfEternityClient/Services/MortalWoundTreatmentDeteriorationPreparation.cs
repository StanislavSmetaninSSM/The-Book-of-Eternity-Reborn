using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundTreatmentDeteriorationPreparation
{
    private readonly string _requestFingerprint;
    private readonly string _coordinatesFingerprint;
    private readonly string _beforeCanonical;
    private readonly string _woundSourcePath;
    private readonly string _authorityFingerprint;
    private readonly string _declaredFingerprint;
    private readonly string _intentFingerprint;
    private readonly int _ordinal;
    private readonly MortalWoundDeteriorationPolicyDefinition _policy;
    private readonly MortalWoundComplicationProposalDraft? _draft;
    private readonly MortalWoundTreatmentComplicationBindingPreparation? _binding;
    private readonly string _fingerprint;

    private MortalWoundTreatmentDeteriorationPreparation(
        MortalWoundTreatmentAttemptRequest request,
        int ordinal,
        MortalWoundApplyDeteriorationOperation operation,
        MortalWoundDeteriorationPolicyAuthority authority,
        string woundSourcePath)
    {
        _requestFingerprint = request.RequestFingerprint;
        _coordinatesFingerprint = request.Coordinates.CoordinatesFingerprint;
        _beforeCanonical = WoundMaterializationContract.SerializeCanonical(
            request.RouteSourceWound);
        _woundSourcePath = woundSourcePath;
        _ordinal = ordinal;
        _policy = authority.Policy with { Result = authority.Policy.Result.Clone() };
        _authorityFingerprint = authority.AuthorityFingerprint;
        _declaredFingerprint = MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(
            ordinal,
            operation);
        _intentFingerprint = MortalWoundTreatmentOutcomeIntentComposer
            .DeteriorationIntentFingerprint(
                request.RequestFingerprint,
                ordinal,
                operation,
                _declaredFingerprint,
                _authorityFingerprint);
        _draft = DraftFor(_policy);
        _binding = _draft is null
            ? null
            : MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(
                _requestFingerprint,
                ordinal,
                _draft,
                _declaredFingerprint);
        _fingerprint = ComputeFingerprint();
    }

    private MortalWoundTreatmentDeteriorationPreparation(
        MortalWoundTreatmentDeteriorationPreparation source)
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
    internal MortalWoundDeteriorationPolicyDefinition Policy =>
        _policy with { Result = _policy.Result.Clone() };
    internal MortalWoundComplicationProposalDraft? Draft => CopyDraft(_draft);
    internal MortalWoundTreatmentComplicationBindingPreparation? ComplicationBinding =>
        CopyBinding(_binding);

    internal MortalWoundTreatmentDeteriorationPreparation DetachedCopy() => new(this);

    internal static bool TryCreate(
        MortalWoundTreatmentAttemptRequest request,
        int operationOrdinal,
        MortalWoundApplyDeteriorationOperation operation,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        out MortalWoundTreatmentDeteriorationPreparation? preparation,
        out IReadOnlyList<ValidationIssue> issues)
    {
        preparation = null;
        issues = Array.Empty<ValidationIssue>();
        try
        {
            if (request is null ||
                operation is null ||
                acceptedState is null ||
                operationOrdinal is < 0 or >= MortalWoundTreatmentContract.MaxOutcomeOperations ||
                !MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
                !request.Coordinates.MatchesAcceptedState(acceptedState) ||
                !acceptedState.MatchesCurrentWound(request.RouteSourceWound))
            {
                throw new InvalidOperationException(
                    "The policy request is not the current accepted wound.");
            }

            var current = MortalWoundDeteriorationPolicyAuthority.Create(
                acceptedState,
                request.Coordinates,
                operation.PolicyRef);
            if (!current.IsValid || current.Authority is null)
            {
                issues = current.Issues.ToArray();
                return false;
            }

            var value = new MortalWoundTreatmentDeteriorationPreparation(
                request,
                operationOrdinal,
                operation,
                current.Authority,
                acceptedState.WoundSourcePath);
            if (!value.SnapshotAgrees(request, operation))
            {
                throw new InvalidOperationException(
                    "The selected policy preparation does not match its source.");
            }

            preparation = value;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                InvalidOperationException or
                JsonException or
                OverflowException)
        {
            issues = new[]
            {
                new ValidationIssue(
                    "treatmentAttempt.result",
                    IssueSeverity.Error,
                    "The selected Mortal wound policy could not be prepared.",
                    code: "mortal_wound_treatment_policy_preparation_invalid",
                    actor: "Client",
                    section: "wound_materialization",
                    expected: "one exact T067/T069 selected policy preparation",
                    actual: exception.Message)
            };
            return false;
        }
    }

    internal bool AgreesWith(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundApplyDeteriorationOperation operation,
        MortalWoundApplyDeteriorationOutcomeIntent intent)
    {
        try
        {
            return intent is not null &&
                SnapshotAgrees(request, operation) &&
                intent.Kind == "apply_deterioration" &&
                intent.OperationOrdinal == _ordinal &&
                intent.DeclaredOperationFingerprint == _declaredFingerprint &&
                intent.IntentFingerprint == _intentFingerprint &&
                intent.PolicyRef == _policy.PolicyRef &&
                intent.DeteriorationAuthorityFingerprint == _authorityFingerprint;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                InvalidOperationException or
                JsonException or
                OverflowException)
        {
            return false;
        }
    }

    private bool SnapshotAgrees(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundApplyDeteriorationOperation operation)
    {
        if (request is null ||
            operation is null ||
            _policy is null ||
            _ordinal is < 0 or >= MortalWoundTreatmentContract.MaxOutcomeOperations ||
            !BindingShapeIsValid(_binding) ||
            !MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
            request.RequestFingerprint != _requestFingerprint ||
            request.Coordinates.CoordinatesFingerprint != _coordinatesFingerprint ||
            WoundMaterializationContract.SerializeCanonical(request.RouteSourceWound) !=
                _beforeCanonical ||
            operation.PolicyRef != _policy.PolicyRef ||
            _policy.Classification !=
                MortalWoundDeteriorationPolicyClassification.StrictlyWorsening ||
            _policy.ResultKind is not (
                MortalWoundDeteriorationResultKind.IncreaseSeverity or
                MortalWoundDeteriorationResultKind.AddComplication or
                MortalWoundDeteriorationResultKind.DeathContour) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(_authorityFingerprint) ||
            _declaredFingerprint != MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(
                _ordinal,
                operation) ||
            _intentFingerprint != MortalWoundTreatmentOutcomeIntentComposer
                .DeteriorationIntentFingerprint(
                    _requestFingerprint,
                    _ordinal,
                    operation,
                    _declaredFingerprint,
                    _authorityFingerprint) ||
            request.RouteSourceWound.Recovery.DeteriorationPolicy is not { } policyJson)
        {
            return false;
        }

        var parsed = MortalWoundDeteriorationPolicyContract.Parse(
            policyJson,
            _woundSourcePath + ".recovery.deteriorationPolicy",
            request.RouteSourceWound.Owner.Realm,
            WoundMaterializationContract.ResolveEffectTargetKind(
                request.RouteSourceWound.Owner.OwnerKind),
            request.RouteSourceWound.Severity.Rank);
        if (!parsed.IsValid ||
            parsed.Policy is null ||
            Canonical(parsed.Policy) != Canonical(_policy))
        {
            return false;
        }

        var draft = DraftFor(parsed.Policy);
        var binding = draft is null
            ? null
            : MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(
                _requestFingerprint,
                _ordinal,
                draft,
                _declaredFingerprint);
        return Canonical(draft) == Canonical(_draft) &&
            BindingFields(binding).SequenceEqual(
                BindingFields(_binding),
                StringComparer.Ordinal) &&
            _fingerprint == ComputeFingerprint();
    }

    private string ComputeFingerprint() => WoundAcceptedTurnFingerprintWriter.Compute(
        new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.selected_policy_preparation",
            "1",
            _requestFingerprint,
            _coordinatesFingerprint,
            _beforeCanonical,
            _woundSourcePath,
            _ordinal.ToString(CultureInfo.InvariantCulture),
            _authorityFingerprint,
            _declaredFingerprint,
            _intentFingerprint,
            Canonical(_policy),
            Canonical(_draft)
        }.Concat(BindingFields(_binding)));

    private static string? Canonical<T>(T value) =>
        WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(value));

    private static MortalWoundComplicationProposalDraft? DraftFor(
        MortalWoundDeteriorationPolicyDefinition policy) =>
        policy.ResultKind == MortalWoundDeteriorationResultKind.AddComplication
            ? MortalWoundTreatmentContract.BuildValidatedComplicationDraft(
                policy.Result.GetProperty("complicationDraft"))
            : null;

    private static MortalWoundComplicationProposalDraft? CopyDraft(
        MortalWoundComplicationProposalDraft? draft) =>
        draft is null
            ? null
            : draft with
            {
                Complication = draft.Complication with { },
                ConsequenceDefinitions = draft.ConsequenceDefinitions
                    .Select(row => row with
                    {
                        Definition = row.Definition.Clone(),
                        Root = row.Root is null
                            ? null
                            : row.Root with
                            {
                                Slots = row.Root.Slots
                                    .Select(slot => slot with { })
                                    .ToImmutableArray()
                            }
                    })
                    .ToImmutableArray()
            };

    private static MortalWoundTreatmentComplicationBindingPreparation? CopyBinding(
        MortalWoundTreatmentComplicationBindingPreparation? binding) =>
        binding is null
            ? null
            : MortalWoundTreatmentComplicationBindingPreparation.Create(
                binding.ComplicationRef,
                binding.ComplicationId,
                binding.DefinitionReferenceBindings,
                binding.ApplicationReferenceBindings,
                binding.PreparationFingerprint,
                binding.Roots);

    private static bool BindingShapeIsValid(
        MortalWoundTreatmentComplicationBindingPreparation? binding) =>
        binding is null ||
        (!binding.DefinitionReferenceBindings.IsDefault &&
            !binding.ApplicationReferenceBindings.IsDefault &&
            !binding.Roots.IsDefault &&
            binding.DefinitionReferenceBindings.All(row => row is not null) &&
            binding.ApplicationReferenceBindings.All(row => row is not null) &&
            binding.Roots.All(row => row is not null));

    private static IEnumerable<string?> BindingFields(
        MortalWoundTreatmentComplicationBindingPreparation? binding)
    {
        if (binding is null)
        {
            yield return null;
            yield break;
        }

        yield return "complication";
        yield return binding.ComplicationRef;
        yield return binding.ComplicationId;
        yield return binding.PreparationFingerprint;
        yield return binding.DefinitionReferenceBindings.Length.ToString(
            CultureInfo.InvariantCulture);
        foreach (var row in binding.DefinitionReferenceBindings)
        {
            yield return row.LocalRef;
            yield return row.NamespacedRef;
        }

        yield return binding.ApplicationReferenceBindings.Length.ToString(
            CultureInfo.InvariantCulture);
        foreach (var row in binding.ApplicationReferenceBindings)
        {
            yield return row.LocalRef;
            yield return row.NamespacedRef;
        }

        yield return binding.Roots.Length.ToString(CultureInfo.InvariantCulture);
        foreach (var row in binding.Roots)
        {
            yield return row.LocalDefinitionRef;
            yield return row.DefinitionRef;
            yield return row.ApplicationRef;
            yield return row.OperationKey;
        }
    }
}

internal sealed partial class MortalWoundApplyDeteriorationOutcomeIntent
{
    private readonly MortalWoundTreatmentDeteriorationPreparation? _preparedDeterioration;

    private MortalWoundApplyDeteriorationOutcomeIntent(
        MortalWoundTreatmentDeteriorationPreparation prepared)
        : this(
            prepared.OperationOrdinal,
            "apply_deterioration",
            prepared.DeclaredOperationFingerprint,
            prepared.IntentFingerprint,
            prepared.PolicyRef,
            prepared.DeteriorationAuthorityFingerprint)
    {
        _preparedDeterioration = prepared.DetachedCopy();
    }

    internal static MortalWoundApplyDeteriorationOutcomeIntent CreatePrepared(
        MortalWoundTreatmentDeteriorationPreparation prepared) => new(prepared);

    internal bool TryGetPreparedDeterioration(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundApplyDeteriorationOperation operation,
        out MortalWoundTreatmentDeteriorationPreparation? preparation)
    {
        preparation = null;
        if (_preparedDeterioration is null ||
            !_preparedDeterioration.AgreesWith(request, operation, this))
        {
            return false;
        }

        preparation = _preparedDeterioration.DetachedCopy();
        return true;
    }

    private MortalWoundApplyDeteriorationOutcomeIntent(
        MortalWoundApplyDeteriorationOutcomeIntent source)
        : this(
            source.OperationOrdinal,
            source.Kind,
            source.DeclaredOperationFingerprint,
            source.IntentFingerprint,
            source.PolicyRef,
            source.DeteriorationAuthorityFingerprint)
    {
        _preparedDeterioration = source._preparedDeterioration?.DetachedCopy();
    }

    internal MortalWoundApplyDeteriorationOutcomeIntent DetachedCopy() => new(this);
}
