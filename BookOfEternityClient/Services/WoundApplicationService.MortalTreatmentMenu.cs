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
        var woundFingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound);
        foreach (var route in treatment.Routes.Where(route => route.Visibility != "gm_only" &&
            treatment.KnownRouteIds.Contains(route.RouteId, StringComparer.Ordinal)))
        {
            // These calculations depend only on this immutable wound/route and
            // current History/clock. Actor-specific requirements remain below.
            var raw = wound.Treatment.Routes.Single(value => value.RouteId == route.RouteId);
            var routeFingerprint = MortalWoundTreatmentRouteFingerprint.Compute(wound, route.RouteId);
            var procedureApplicable = route is not MortalWoundProcedureRouteDefinition procedure || procedure.Bands.All(band =>
            {
                var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(wound, new[] { band.DeclaredResult });
                return simulation.IsApplicable && (band.Category != "success" || simulation.Improved);
            });
            var guaranteeApplicable = true;
            if (route is MortalWoundGuaranteedRouteDefinition guaranteedRoute)
            {
                var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(wound, new[] { guaranteedRoute.Outcome.DeclaredResult });
                guaranteeApplicable = simulation.IsApplicable && simulation.Improved;
            }
            (string Status, int Ordinal, long Due, long Deadline)? window = route is MortalWoundCourseRouteDefinition courseRoute
                ? MortalWoundCourseModeAuthority.DescribeCurrentWindow(wound, current.Input.History, courseRoute, minute) : null;
            if (window?.Status == "InvalidAuthority") continue;
            var ordinal = window?.Ordinal;
            var course = route as MortalWoundCourseRouteDefinition;
            var milestone = ordinal is { } number ? course!.Milestones[number - 1] : null;
            var dose = ordinal is { } doseOrdinal ? raw.Outcomes[doseOrdinal - 1].GetProperty("requirements")
                .EnumerateArray().Select(value => value.Clone()).ToArray() : Array.Empty<JsonElement>();
            var dosePath = raw.SourcePath + ".outcomes[" + ((ordinal ?? 1) - 1).ToString(CultureInfo.InvariantCulture) + "]";
            var requirements = route.Requirements.Select(value => WoundPlayerProjection.Requirement(value, current.Input)).ToArray();
            var doseText = milestone is null ? "" : " Условия этого этапа: " +
                (milestone.Requirements.Length == 0 ? "нет дополнительных условий" : string.Join("; ",
                    milestone.Requirements.Select(value => WoundPlayerProjection.Requirement(value, current.Input)))) + ".";
            var selectedCosts = route.ResourcePolicy.Mutations.Where(value => value.Scope == "common" ||
                value.Scope == "course_milestone" && value.MilestoneOrdinal == ordinal).Select(value =>
                    WoundPlayerProjection.Requirement(value.Scope == "common" ? route.Requirements[value.RequirementIndex] :
                        milestone!.Requirements[value.RequirementIndex], current.Input)).ToArray();
            var cost = selectedCosts.Length == 0 ? "Заявленных расходов на эту попытку нет." :
                "Заявленные расходы на эту попытку: " + string.Join("; ", selectedCosts) + ". Исходы списания: " +
                string.Join(" / ", route.ResourcePolicy.ConsumeOn.Select(value => value == "success" ? "успех" :
                    value == "partial_success" ? "частичный успех" : "неудача")) +
                ". При отмене, отказе проверки или откате удержание освобождается.";
            foreach (var provider in actors)
            {
                var providerOwner = new WoundOwnerCoordinate("mortal_world", provider.ActorKind, provider.ActorId,
                    paths[(provider.ActorKind, provider.ActorId)]);
                var context = MortalDiagnosisContext(wound.Owner, providerOwner, location);
                var resolved = MortalWoundTreatmentAuthority.ResolveRequirements(raw, context, snapshot);
                var available = resolved.Success && procedureApplicable && guaranteeApplicable;
                var modeDescription = route.Mode == "procedure" ? "Одна процедура с заявленной проверкой." :
                    route.Mode == "guaranteed" ? "Гарантированный результат при точном подтверждении умения и всех условий." : "";
                if (route is MortalWoundProcedureRouteDefinition declaredProcedure)
                    modeDescription += " Проверка: 1d20, сложность " + declaredProcedure.Resolution.Difficulty.ToString(CultureInfo.InvariantCulture) +
                        (declaredProcedure.Resolution.ModifierSource is MortalWoundFixedZeroModifierSource ? ", без поправки." : ", с уровнем заявленного навыка.") +
                        " Обычные естественные 20/1 и правила Судьбы.";
                if (route is MortalWoundGuaranteedRouteDefinition guaranteed)
                {
                    var sourceOwner = guaranteed.Resolution.ActorRole == "provider" ? providerOwner : wound.Owner;
                    var matching = skills.Where(skill => skill.OwnerKind == sourceOwner.OwnerKind && skill.OwnerId == sourceOwner.OwnerId &&
                        skill.Active && skill.Lifecycle == "active").SelectMany(skill => skill.Capabilities)
                        .Where(capability => capability.CapabilityRef == guaranteed.Resolution.CapabilityRef).ToArray();
                    available &= sourceOwner.OwnerKind is "player" or "npc" && matching.Length == 1 &&
                        MortalWoundTreatmentCapabilityAuthority.CoversGuaranteedOutcome(wound, guaranteed.Outcome.DeclaredResult, matching[0]);
                }
                if (window is { } currentWindow)
                {
                    var commonStatus = MortalWoundTreatmentRequirementAuthorityBundle.ClassifyMenuRequirements(raw.Requirements, raw.SourcePath, resolved);
                    var doseResult = MortalWoundTreatmentAuthority.ResolveRequirementMembers(dose, dosePath, context, snapshot);
                    var doseStatus = MortalWoundTreatmentRequirementAuthorityBundle.ClassifyMenuRequirements(dose, dosePath, doseResult);
                    var valid = commonStatus != "InvalidAuthority" && doseStatus != "InvalidAuthority";
                    var satisfied = commonStatus == "Satisfied" && doseStatus == "Satisfied";
                    available = valid && currentWindow.Status != "TooEarly" && (satisfied || wound.Care.ActiveCourseId is not null);
                    modeDescription = "Этап курса " + currentWindow.Ordinal.ToString(CultureInfo.InvariantCulture) + ". Текущее время: " +
                        minute.ToString(CultureInfo.InvariantCulture) + " мин.; окно включительно: " + currentWindow.Due.ToString(CultureInfo.InvariantCulture) +
                        "–" + currentWindow.Deadline.ToString(CultureInfo.InvariantCulture) + " мин. " +
                        (currentWindow.Status == "TooEarly" ? "Ещё рано; ожидание не выполняется автоматически." :
                         currentWindow.Status == "DeadlineExceeded" || !satisfied ? "Будет применён заявленный результат прерывания курса." :
                         "Будет выполнен только этот отдельно подтверждённый этап.");
                }
                var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, woundFingerprint)
                {
                    MortalTreatment = new(wound.Owner, providerOwner, route.RouteId, route.Mode, ordinal,
                        routeFingerprint, MortalWoundGuidedTreatmentInputCodec.ContextFingerprint(context))
                };
                var providerName = providerOwner == wound.Owner ? "самостоятельно" : provider.ActorKind == "player" ? "с вашей помощью" :
                    "с помощью «" + (provider.DisplayName == provider.ActorId ? "участник рядом" : WoundPlayerProjection.Text(provider.DisplayName)) + "»";
                choices.Add(new("physical_treatment_" + choices.Count, selection,
                    WoundPlayerProjection.Text(route.DisplayName) + " — " + WoundPlayerProjection.Text(wound.Display.Name) + " — " + providerName,
                    wound.Severity.Value, null, available, modeDescription + " " + cost + " Общие условия: " +
                    (requirements.Length == 0 ? "нет" : string.Join("; ", requirements)) + "." + doseText +
                    " Просмотр и отмена ничего не расходуют. " + (available ? "Окончательная проверка перед попыткой." : "Сейчас способ недоступен.")));
            }
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
        { GmContext = MortalWoundGuidedTreatmentInputCodec.CreateGmContext(input) };
    }
}
