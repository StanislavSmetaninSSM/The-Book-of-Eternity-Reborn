using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Immutable observations of the two real lifetime passes. This is evidence,
/// not permission to publish, close a turn or import an accepted execution.
/// Only the planner's private issuer may create or extend it.
/// </summary>
internal sealed class EffectLifecycleExecutionLedger
{
    private readonly JsonObject _image;
    private readonly object _execution;
    private readonly string? _planPayload;

    internal EffectLifecycleExecutionLedger(object issuer, JsonObject image)
    {
        RequireIssuer(issuer);
        _image = image.DeepClone().AsObject();
        _execution = new object();
    }

    private EffectLifecycleExecutionLedger(JsonObject image, object execution, string? planPayload)
    {
        _image = image.DeepClone().AsObject();
        _execution = execution;
        _planPayload = planPayload;
    }

    internal string Fingerprint => WoundAcceptedTurnFingerprintWriter.Compute(new[]
    {
        "book_of_eternity.effect.lifecycle_execution_ledger", "1",
        WoundAcceptedTurnFingerprintWriter.CanonicalJson(_image)
    });

    internal bool IsComplete => _planPayload is not null &&
        _image["passes"] is JsonArray { Count: 2 } &&
        _image["completedPlanRef"]?.GetValue<string>() == _planPayload;

    internal JsonObject ToJsonObject() => _image.DeepClone().AsObject();

    // The legacy payload excludes this ledger, so the reference is acyclic.
    // Copies may retain an already bound ledger only for the exact same payload.
    internal EffectLifecycleExecutionLedger BindPlan(object? issuer, string payload)
    {
        if (_planPayload is not null)
        {
            if (_planPayload != payload)
                throw new InvalidDataException("Lifecycle evidence belongs to a different effect result.");
            return this;
        }
        RequireIssuer(issuer);
        var image = ToJsonObject();
        image[IsFinalPass(image) ? "completedPlanRef" : "initialPlanRef"] = payload;
        return new EffectLifecycleExecutionLedger(image, _execution, payload);
    }

    internal EffectLifecycleExecutionLedger AppendFinalPass(object issuer, JsonObject pass)
    {
        RequireIssuer(issuer);
        if (_planPayload is null || _image["passes"] is not JsonArray { Count: 1 } ||
            pass["kind"]?.GetValue<string>() != "final_lifetime")
            throw new InvalidDataException("Only a genuine initial lifetime pass may be completed once.");
        var image = ToJsonObject();
        image["passes"]!.AsArray().Add(pass.DeepClone());
        return new EffectLifecycleExecutionLedger(image, _execution, planPayload: null);
    }

    internal bool MatchesInitialPlan(EffectAcceptedTurnPlan initial) =>
        IsComplete && initial.LifecycleExecutionLedger is { IsComplete: false } ledger &&
        ReferenceEquals(_execution, ledger._execution) &&
        _image["initialPlanRef"]?.GetValue<string>() == ledger._planPayload &&
        ledger._planPayload == WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(initial);

    private static bool IsFinalPass(JsonObject image) => image["passes"] is JsonArray { Count: 2 };

    private static void RequireIssuer(object? issuer)
    {
        if (!EffectAcceptedTurnPlanner.IsLifecycleLedgerIssuer(issuer))
            throw new InvalidOperationException("Lifecycle evidence requires its private executing planner.");
    }
}

internal static partial class EffectAcceptedTurnPlanner
{
    private static readonly object LifecycleLedgerIssuer = new();
    internal static bool IsLifecycleLedgerIssuer(object? issuer) => ReferenceEquals(issuer, LifecycleLedgerIssuer);

    private static string LifecycleArtifactRef(string kind, JsonNode image) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.effect.lifecycle_execution_ledger.artifact", "1", kind,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(image)
        });

    private static EffectLifecycleExecutionLedger CreateInitialLifecycleLedger(
        EffectAcceptedTurnInput input, string inputFingerprint, string carrierFingerprint,
        EffectTargetAuthority targets, JsonObject pass) => new(LifecycleLedgerIssuer, new JsonObject
        {
            ["version"] = 1,
            ["inputFingerprint"] = inputFingerprint,
            ["carrierAuthorityFingerprint"] = carrierFingerprint,
            ["sourceAuthorityFingerprint"] = input.SourceAuthority.CanonicalFingerprint,
            ["targetAuthorityFingerprint"] = targets.CanonicalFingerprint,
            ["eventInputRef"] = LifecycleArtifactRef("event_input", input.EventInput),
            ["initialPlanRef"] = null,
            ["completedPlanRef"] = null,
            ["passes"] = new JsonArray(pass.DeepClone())
        });

    private static JsonObject LifecycleSubjectImage(EffectCarrierOccurrence occurrence) => new()
    {
        ["effectId"] = occurrence.EffectId,
        ["filePath"] = occurrence.FilePath,
        ["jsonPath"] = occurrence.JsonPath,
        ["coordinate"] = new JsonObject
        {
            ["kind"] = occurrence.Coordinate.Kind,
            ["ownerId"] = occurrence.Coordinate.OwnerId,
            ["path"] = occurrence.Coordinate.Path,
            ["category"] = occurrence.Coordinate.Category
        },
        ["beforeImage"] = occurrence.Effect.DeepClone(),
        ["beforeImageRef"] = LifecycleArtifactRef("effect_image", occurrence.Effect)
    };

    private static JsonObject LifecycleEventImage(EffectLifecycleEvent value) => new()
    {
        ["eventRef"] = value.EventRef, ["turn"] = value.Turn,
        ["phase"] = value.Phase, ["triggerId"] = value.TriggerId,
        ["currentTime"] = value.CurrentTime, ["currentSceneId"] = value.CurrentSceneId,
        ["sceneClosed"] = value.SceneClosed, ["sourceSatisfied"] = value.SourceSatisfied,
        ["conditionSatisfied"] = value.ConditionSatisfied, ["currentRealm"] = value.CurrentRealm,
        ["causalEventRef"] = value.CausalEventRef, ["targetSatisfied"] = value.TargetSatisfied
    };

    private sealed record LifecycleReductionObservation(
        EffectLifetimeReductionResult Reduction, string? TransitionId);
}
