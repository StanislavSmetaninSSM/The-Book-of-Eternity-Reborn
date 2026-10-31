using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Services;

internal static partial class WoundApplicationService
{
    internal const string MortalTreatmentActionText = "Выполнить отдельно подтверждённое лечение физической раны выбранным известным способом у выбранного участника, соблюдая заявленные условия и расходы.";

    private static void AddPhysicalTreatmentChoices(List<WoundTreatmentChoice> choices, WoundPlayerReadContext current,
        WoundMaterializationEnvelope wound, MortalWoundTreatmentDefinition treatment,
        IReadOnlyList<MortalWoundTreatmentAuthority.Actor> actors, IReadOnlyDictionary<(string Kind, string Id), string> paths,
        MortalWoundTreatmentAuthority.Snapshot snapshot, IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> skills,
        IReadOnlyDictionary<string, JsonObject> roots, string location)
    {
        var minute = roots[EffectAcceptedTurnInputComposer.WorldTimePath]["currentTimeInMinutes"]!.GetValue<long>();
        foreach (var route in treatment.Routes.Where(route => route.Visibility != "gm_only" &&
            treatment.KnownRouteIds.Contains(route.RouteId, StringComparer.Ordinal)))
        foreach (var provider in actors)
        {
            var providerOwner = new WoundOwnerCoordinate("mortal_world", provider.ActorKind, provider.ActorId,
                paths[(provider.ActorKind, provider.ActorId)]);
            var context = MortalDiagnosisContext(wound.Owner, providerOwner, location);
            var raw = wound.Treatment.Routes.Single(value => value.RouteId == route.RouteId);
            var resolved = MortalWoundTreatmentAuthority.ResolveRequirements(raw, context, snapshot);
            var available = resolved.Success;
            int? ordinal = null;
            var modeDescription = route.Mode == "procedure" ? "Одна процедура с заявленной проверкой." :
                route.Mode == "guaranteed" ? "Гарантированный результат при точном подтверждении умения и всех условий." : "";
            if (route is MortalWoundProcedureRouteDefinition procedure)
            {
                available &= procedure.Bands.All(band =>
                {
                    var simulated = MortalWoundTreatmentWorkingWoundSimulator.Simulate(wound, new[] { band.DeclaredResult });
                    return simulated.IsApplicable && (band.Category != "success" || simulated.Improved);
                });
                modeDescription += " Проверка: 1d20, сложность " + procedure.Resolution.Difficulty.ToString(CultureInfo.InvariantCulture) +
                    (procedure.Resolution.ModifierSource is MortalWoundFixedZeroModifierSource ? ", без поправки." : ", с уровнем заявленного навыка.") +
                    " Обычные естественные 20/1 и правила Судьбы.";
            }
            if (route is MortalWoundGuaranteedRouteDefinition guaranteed)
            {
                var sourceOwner = guaranteed.Resolution.ActorRole == "provider" ? providerOwner : wound.Owner;
                var matching = skills.Where(skill => skill.OwnerKind == sourceOwner.OwnerKind && skill.OwnerId == sourceOwner.OwnerId &&
                    skill.Active && skill.Lifecycle == "active").SelectMany(skill => skill.Capabilities)
                    .Where(capability => capability.CapabilityRef == guaranteed.Resolution.CapabilityRef).ToArray();
                available &= sourceOwner.OwnerKind is "player" or "npc" && matching.Length == 1 &&
                    MortalWoundTreatmentCapabilityAuthority.CoversGuaranteedOutcome(wound, guaranteed.Outcome.DeclaredResult, matching[0]);
                var simulated = MortalWoundTreatmentWorkingWoundSimulator.Simulate(wound, new[] { guaranteed.Outcome.DeclaredResult });
                available &= simulated.IsApplicable && simulated.Improved;
            }
            if (route is MortalWoundCourseRouteDefinition course)
            {
                var window = MortalWoundCourseModeAuthority.DescribeCurrentWindow(wound, current.Input.History, course, minute);
                if (window.Status == "InvalidAuthority") continue;
                ordinal = window.Ordinal;
                var commonStatus = MortalWoundTreatmentRequirementAuthorityBundle.ClassifyMenuRequirements(raw.Requirements, raw.SourcePath, resolved);
                var rawMilestone = raw.Outcomes[window.Ordinal - 1];
                var dose = rawMilestone.GetProperty("requirements").EnumerateArray().Select(value => value.Clone()).ToArray();
                var dosePath = raw.SourcePath + ".outcomes[" + (window.Ordinal - 1).ToString(CultureInfo.InvariantCulture) + "]";
                var doseResult = MortalWoundTreatmentAuthority.ResolveRequirementMembers(dose, dosePath, context, snapshot);
                var doseStatus = MortalWoundTreatmentRequirementAuthorityBundle.ClassifyMenuRequirements(dose, dosePath, doseResult);
                var valid = commonStatus != "InvalidAuthority" && doseStatus != "InvalidAuthority";
                var satisfied = commonStatus == "Satisfied" && doseStatus == "Satisfied";
                available = valid && window.Status != "TooEarly" && (satisfied || wound.Care.ActiveCourseId is not null);
                modeDescription = "Этап курса " + window.Ordinal.ToString(CultureInfo.InvariantCulture) + ". Текущее время: " +
                    minute.ToString(CultureInfo.InvariantCulture) + " мин.; окно включительно: " + window.Due.ToString(CultureInfo.InvariantCulture) +
                    "–" + window.Deadline.ToString(CultureInfo.InvariantCulture) + " мин. " +
                    (window.Status == "TooEarly" ? "Ещё рано; ожидание не выполняется автоматически." :
                     window.Status == "DeadlineExceeded" || !satisfied ? "Будет применён заявленный результат прерывания курса." :
                     "Будет выполнен только этот отдельно подтверждённый этап.");
            }
            var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
            {
                MortalTreatment = new(wound.Owner, providerOwner, route.RouteId, route.Mode, ordinal,
                    MortalWoundTreatmentRouteFingerprint.Compute(wound, route.RouteId), MortalWoundGuidedTreatmentInputCodec.ContextFingerprint(context))
            };
            var providerName = providerOwner == wound.Owner ? "самостоятельно" : provider.ActorKind == "player" ? "с вашей помощью" :
                "с помощью «" + (provider.DisplayName == provider.ActorId ? "участник рядом" : WoundPlayerProjection.Text(provider.DisplayName)) + "»";
            var requirements = route.Requirements.Select(requirement => WoundPlayerProjection.Requirement(requirement, current.Input)).ToArray();
            var cost = route.ResourcePolicy.Mutations.Length == 0 ? "Заявленных расходов нет." :
                "Расходуется только заявленное условиями способа; исходы списания: " +
                string.Join(" / ", route.ResourcePolicy.ConsumeOn.Select(value => value == "success" ? "успех" : value == "partial_success" ? "частичный успех" : "неудача")) + ".";
            choices.Add(new("physical_treatment_" + choices.Count, selection,
                WoundPlayerProjection.Text(route.DisplayName) + " — " + WoundPlayerProjection.Text(wound.Display.Name) + " — " + providerName,
                wound.Severity.Value, null, available, modeDescription + " " + cost + " Условия: " + string.Join("; ", requirements) +
                ". Просмотр и отмена ничего не расходуют. " + (available ? "Окончательная проверка перед попыткой." : "Сейчас способ недоступен.")));
        }
    }

    /// <summary>
    /// Stages only a freshly confirmed immutable physical selection before the
    /// official original; no request, roll, reservation or repair wave is created.
    /// </summary>
    internal static async Task<SpiritualSelfTreatmentStageResult> StageMortalTreatmentInputAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentStageContext frame, SpiritualSelfTreatmentSelection selected)
    {
        if (selected.MortalTreatment is not { } physical || frame.Progression.CurrentRealm != "Mortal World")
            return new(false, false, "mortal_treatment_selection_invalid", null);
        var current = await WoundPlayerReadService.LoadContextAsync(files, lease, retainMenuInputs: true) ??
            throw new InvalidDataException("Current physical selection source is unavailable.");
        var menu = await BuildMortalDiagnosisMenuAsync(files, lease, files.GetOrCreateSessionGeneration(lease), current);
        if (!menu.State.Choices.Any(choice => choice.Available && choice.Selection == selected))
            return new(false, false, "mortal_treatment_selection_changed", null);
        var freshControl = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
            .BuildControlForNextTurnAsync(lease, "Mortal World");
        if (!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(frame.Progression), JsonSerializer.SerializeToNode(freshControl)))
            return new(false, false, "mortal_treatment_control_changed", null);
        var input = MortalWoundGuidedTreatmentInputCodec.Create(frame, selected,
            MortalDiagnosisContext(physical.Target, physical.Provider, menu.LocationId));
        var path = AcceptedMechanicsPlan.WoundCommandPath;
        var before = await files.ReadFileBytesAsync(lease, path);
        var json = input.ToJsonString();
        if (before is not null && !before.AsSpan().SequenceEqual(FileSystemManager.EncodeUtf8WithPreamble(json)))
            return new(false, false, "pending_conflict", null);
        var writes = menu.Images.Where(pair => pair.Key != path && pair.Key != ProgressionScheduleService.SchedulePath)
            .Select(pair => CoordinatedStateWriteHelper.CreateExactGuardWrite(pair.Key, pair.Value)).ToList();
        var schedule = await files.ReadFileBytesAsync(lease, ProgressionScheduleService.SchedulePath);
        writes.Add(CoordinatedStateWriteHelper.CreateExactGuardWrite(ProgressionScheduleService.SchedulePath,
            new(schedule is not null, schedule)));
        writes.Add(before is null ? new(path, null, json, RequireCurrentBaseline: true, ExactPrevious: new(false, null)) :
            CoordinatedStateWriteHelper.CreateExactGuardWrite(path, new(true, before)));
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(files, lease, writes.ToArray()))
            return new(false, false, "source_changed", null);
        var readback = await files.ReadFileBytesAsync(lease, path);
        if (readback is null || !readback.AsSpan().SequenceEqual(FileSystemManager.EncodeUtf8WithPreamble(json)))
            throw new CoordinatedStatePublicationUncertainException(new InvalidDataException("Physical staging readback differs."));
        return new(true, before is not null, null, MortalWoundGuidedTreatmentInputCodec.Fingerprint(input))
        { GmContext = new JsonObject { ["kind"] = MortalWoundGuidedTreatmentInputCodec.Kind,
            ["schemaVersion"] = 1, ["selection"] = selected.ToJson(), ["inputFingerprint"] = MortalWoundGuidedTreatmentInputCodec.Fingerprint(input),
            ["heldRequestPath"] = WoundAcceptedTurnSnapshotContract.PendingResolutionPath } };
    }
}
