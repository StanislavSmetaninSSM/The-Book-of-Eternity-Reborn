using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    internal static async Task<MortalWoundTreatmentPublicationResult> ComposeMortalDiagnosisPublicationAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, GameResponse response,
        MortalWoundTreatmentAcceptedStateAuthority accepted, MortalWoundTreatmentAttemptRequest request)
    {
        object? reservation = null;
        try
        {
            accepted.RequireOriginalDiagnosisRequest(request);
            if (!accepted.IsLeaseBoundTo(files, lease) || request.Mode != "diagnosis" || request.Coordinates.Purpose != "diagnosis")
                throw new InvalidDataException("Diagnosis publication requires its actual original owner, lease and purpose.");
            if (AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var cached) && cached.Success &&
                cached.Plan?.TreatmentResourcePublicationAuthority is { } existing &&
                existing.ContinuationAuthority is MortalDiagnosisContinuation prior && prior.Publication is not null &&
                AcceptedTurnAuthorityRegistry.CanReuseMortalDiagnosisPublication(files, lease, accepted, request, existing))
            {
                if (!existing.HasValidSeal()) throw new InvalidDataException("The retained diagnosis common publication seal changed.");
                return MortalWoundTreatmentPublicationResult.Valid(cached.Plan);
            }
            var held = accepted.ReadHeldDiagnosisRequest(request.Coordinates);
            if (held is null) await accepted.RetainDiagnosisRequestAsync(request);
            else if (held.RequestFingerprint != request.RequestFingerprint) throw new InvalidDataException("Another retained diagnosis owns these coordinates.");
            var outcome = MortalWoundDiagnosisOutcome.Resolve(accepted, new(accepted.History, Array.Empty<ValidationIssue>()), request);
            var working = MortalWoundDiagnosisWorkingProjection.Build(accepted, outcome);
            var progression = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
                .PrepareDiagnosisOutcomeAsync(lease, working);
            if (outcome.Resolution.CriticalReactionIntent is not null && response.EffectEventReports is not null)
                return CrossSurfaceFateDuplicateFailure();
            var skill = FreezeTreatmentSkillCommands(response, ignoreLegacyFateDuplicate: false);
            if (skill.Envelope is null) return MortalWoundTreatmentPublicationResult.Invalid(skill.Issues);
            var items = FreezeTreatmentItemCommands(response);
            if (items.Envelope is null) return MortalWoundTreatmentPublicationResult.Invalid(items.Issues);
            var shell = ValidateTreatmentPublicationShell(accepted, request, outcome.Resolution);
            if (shell.Finalization is null || shell.Issues.Count != 0) return MortalWoundTreatmentPublicationResult.Invalid(shell.Issues);
            var critical = MortalWoundCriticalReactionPublicationPlanner.Compose(accepted, request, outcome.Resolution);
            if (!critical.IsValid) return MortalWoundTreatmentPublicationResult.Invalid(critical.Issues);
            var baseFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            {
                "book_of_eternity.mortal_diagnosis.publication", "1", accepted.AcceptedStateFingerprint,
                request.RequestFingerprint, outcome.Resolution.ResultFingerprint, working.Fingerprint,
                progression.MutationFingerprint, critical.Fingerprint
            });
            var semantic = ComputeTreatmentPublicationEnvelopeFingerprint(baseFingerprint, skill.Envelope, items.Envelope);
            var reserved = AcceptedTurnAuthorityRegistry.ReserveMortalWoundTreatmentPublication(files, lease, accepted, semantic);
            if (reserved.Status != MortalWoundTreatmentPublicationReservationStatus.Reserved || reserved.Authority is null)
                throw new InvalidDataException("Diagnosis cannot replace an existing publication reservation or restart blocker.");
            reservation = reserved.Authority;
            var projection = CreateTreatmentSkillProjection(files, lease, skill.Envelope,
                ComputeTreatmentSkillSemanticFingerprint(skill.Envelope), reservation, suppliedNpcSemanticBaseline: null);
            if (projection.Authority is null) return MortalWoundTreatmentPublicationResult.Invalid(projection.Issues);
            if (!MortalItemAcceptedTurnAuthority.HasValidatedItems(files, lease))
            {
                var issues = await new ValidationService(files, NullLogger<ValidationService>.Instance)
                    .ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease);
                if (issues.Count != 0) return MortalWoundTreatmentPublicationResult.Invalid(issues);
            }
            var publication = new MortalDiagnosisPublicationBinding(reservation, semantic, shell.Finalization,
                items.Envelope, projection.Authority, critical);
            var continuation = MortalDiagnosisContinuation.Admit(accepted, outcome, working, progression, publication);
            var input = continuation.Input;
            var prepared = WoundAcceptedTurnPlanAuthority.GetOrBuildTreatmentContinuationPreparedValidated(
                files, lease, input, continuation, reservation);
            if (!prepared.Success || prepared.Plan is null) return MortalWoundTreatmentPublicationResult.Invalid(prepared.Issues);
            var effectInput = ComposeMortalDiagnosisEffectInput(prepared.Plan);
            var effects = WoundAcceptedTurnPlanAuthority.GetOrBuildTreatmentContinuationEffectValidated(
                files, lease, prepared.Plan, effectInput, continuation, reservation);
            if (!effects.Success || effects.Plan is null) return MortalWoundTreatmentPublicationResult.Invalid(effects.Issues);
            var finalized = WoundAcceptedTurnPlanAuthority.GetOrBuildTreatmentContinuationFinalValidated(
                files, lease, prepared.Plan, effects, continuation, reservation);
            if (!finalized.Success || finalized.Plan is null) return MortalWoundTreatmentPublicationResult.Invalid(finalized.Issues);
            var bundle = new AcceptedMechanicsWoundStageBundle(input, prepared.Plan, effects.Plan, finalized.Plan);
            var common = AcceptedMechanicsWoundCommonInputComposer.ComposeMortalDiagnosisContinuation(files, lease, bundle);
            if (!common.Success || common.Input is null) return MortalWoundTreatmentPublicationResult.Invalid(common.Issues);
            var result = AcceptedMechanicsPlanAuthority.GetOrBuildMortalWoundTreatmentValidated(files, lease, common.Input,
                bundle, accepted, request, outcome.Resolution, semantic, continuation, reservation);
            return result.Success && result.Plan is not null ? MortalWoundTreatmentPublicationResult.Valid(result.Plan) :
                MortalWoundTreatmentPublicationResult.Invalid(result.Issues);
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (InvalidDataException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException or OverflowException))
        {
            return PublicationFailure("mortal_diagnosis_publication_invalid", "one current original diagnosis, exact retained claims and complete common plan", failure.Message);
        }
        finally { AcceptedTurnAuthorityRegistry.AbortMortalWoundTreatmentPublication(files, lease, reservation); }
    }
}
