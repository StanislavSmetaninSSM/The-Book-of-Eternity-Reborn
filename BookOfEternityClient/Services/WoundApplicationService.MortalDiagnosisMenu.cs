using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Services;

internal static partial class WoundApplicationService
{
    internal const string MortalDiagnosisActionText = "Выполнить отдельно подтверждённый осмотр физической раны по явно заданной проверке, длительности и расходам. Сам осмотр не лечит; обычное восстановление за прошедшее время сохраняется.";
    private sealed record MortalDiagnosisMenu(WoundCommandState State, WoundPlayerReadContext Context, string LocationId,
        IReadOnlyDictionary<string, CanonicalBeforeImage> Images);

    private static MortalWoundTreatmentAuthority.Context MortalDiagnosisContext(WoundOwnerCoordinate target, WoundOwnerCoordinate provider, string location)
    {
        var parsed = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
        {
            ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = target.OwnerKind, ["targetId"] = target.OwnerId,
            ["providerKind"] = provider.OwnerKind, ["providerId"] = provider.OwnerId, ["currentLocationId"] = location
        }.ToJsonString(), "mortalDiagnosis.currentContext");
        return parsed.IsValid && parsed.Context is not null ? parsed.Context : throw new InvalidDataException("Invalid current Mortal diagnosis context.");
    }

    private static async Task<MortalDiagnosisMenu> BuildMortalDiagnosisMenuAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, string generation, WoundPlayerReadContext context)
    {
        var images = context.CurrentMenuImages.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var roots = context.CurrentMenuRoots.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        async Task<JsonObject?> Read(string path)
        {
            if (roots.TryGetValue(path, out var known)) return known;
            if (!images.TryGetValue(path, out var image))
            {
                var bytes = await files.ReadFileBytesAsync(lease, path); image = new(bytes is not null, bytes); images.Add(path, image);
            }
            if (image.Bytes is not { } current) return null;
            JsonObject parsed;
            if (path == StorageTransportMoveService.VehiclesPath)
            {
                var vehicleIssues = new List<ValidationIssue>();
                if (!MortalWoundTreatmentAcceptedStateAuthority.TryParseProjectionCatalogObject(current, path, vehicleIssues, out var vehicles) || vehicles is null)
                    throw new InvalidDataException("Current vehicle topology is unavailable.");
                parsed = vehicles;
            }
            else parsed = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(current), new JsonSerializerOptions(), path) ??
                throw new InvalidDataException("A present Mortal menu source is not an object: " + path);
            roots.Add(path, parsed); return parsed;
        }
        try
        {
            if (!context.Input.SelfVisible || context.Input.Realm != "mortal_world") throw new InvalidDataException("Current Mortal actor is unavailable.");
            var location = await Read(MortalLocationMaterializationContract.CurrentLocationPath) ?? throw new InvalidDataException("Missing current Mortal location.");
            var locationId = location["locationId"]?.GetValue<string>() ?? throw new InvalidDataException("Missing exact current location identity.");
            var self = new WoundOwnerCoordinate("mortal_world", "player", "player_current", WoundCarrierCatalog.PlayerPath);
            var seedContext = MortalDiagnosisContext(self, self, locationId);
            var issues = new List<ValidationIssue>();
            var selection = MortalWoundTreatmentAcceptedStateAuthority.BuildPathSelection(seedContext, issues);
            foreach (var path in selection.Where(path => path != ResourceMaterializationContract.DefinitionsPath)) _ = await Read(path);
            issues.AddRange(new ValidationService(files, NullLogger<ValidationService>.Instance).ValidateMortalWoundTreatmentDetachedSources(roots));
            if (issues.Count != 0) throw new InvalidDataException("Incomplete or invalid Mortal menu read set.");
            var player = MortalWoundTreatmentAcceptedStateAuthority.ParsePlayerCapabilities(roots, issues);
            var npcs = MortalWoundTreatmentAcceptedStateAuthority.ParseNpcCapabilities(roots["game_state/npcs/npc_core.json"], issues);
            var facts = MortalWoundTreatmentAcceptedCanonicalProjection.ComposeMenuData(seedContext, roots, context.Input.Resources, player, npcs);
            if (issues.Count != 0 || facts.Issues.Count != 0) throw new InvalidDataException("Current canonical Mortal facts are unavailable.");
            var snapshot = new MortalWoundTreatmentAuthority.Snapshot(1, "mortal_diagnosis_menu_data", facts.Items, facts.Resources,
                facts.Actors, facts.Facilities, facts.Locations, facts.Quests,
                MortalWoundTreatmentAcceptedStateAuthority.ComposeRequirementEffects(context.Input.Effects), facts.Environments);
            var actorPaths = MortalDiagnosisActorPaths(roots);
            var actors = facts.Actors.Where(actor => actor.Active && actor.Reachable && actor.Lifecycle == "active" &&
                actor.CurrentLocationId == locationId && actorPaths.ContainsKey((actor.ActorKind, actor.ActorId))).ToArray();
            var knownWounds = context.Catalog.Occurrences.Select(row => row.Wound).Where(wound => wound.Owner.Realm == "mortal_world" &&
                wound.Classification.Domain == "physical" && wound.Lifecycle == "active" && WoundPlayerProjection.Visible(wound.Display.Visibility)).ToLookup(wound => wound.Owner);
            var choices = new List<WoundTreatmentChoice>(); var targets = new List<WoundTargetChoice>();
            foreach (var actor in actors.OrderBy(actor => actor.ActorKind == "player" ? 0 : 1).ThenBy(actor => actor.DisplayName, StringComparer.Ordinal))
            {
                var owner = new WoundOwnerCoordinate("mortal_world", actor.ActorKind, actor.ActorId, actorPaths[(actor.ActorKind, actor.ActorId)]);
                var name = actor.ActorKind == "player" ? "Себя" : actor.DisplayName == actor.ActorId ? "Участник рядом" : WoundPlayerProjection.Text(actor.DisplayName);
                var summaries = knownWounds[owner].Select(wound => new WoundTargetWoundSummary(WoundPlayerProjection.Text(wound.Display.Name),
                    wound.Severity.Value, WoundPlayerProjection.Text(wound.Display.Description), wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))).ToArray();
                var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[] { "mortal_diagnosis_target", "1", owner.Realm, owner.OwnerKind,
                    owner.OwnerId, owner.CarrierPath, locationId, name }.Concat(summaries.SelectMany(value => new[] { value.WoundId, value.Fingerprint })));
                targets.Add(new("target_" + targets.Count, name, "Текущая локация", summaries, owner, null, fingerprint));
                foreach (var wound in knownWounds[owner])
                {
                    var typed = MortalWoundTreatmentContract.ParseProjection(wound.Treatment, "mortalDiagnosis.treatment", wound.Owner.Realm,
                        wound.Owner.OwnerKind, wound.Severity.Rank, wound.Complications, wound.Recovery.DeteriorationPolicy);
                    if (!typed.IsValid || typed.Treatment is null) continue;
                    AddPhysicalTreatmentChoices(choices, context, wound, typed.Treatment, actors, actorPaths,
                        snapshot, player.Concat(npcs).ToArray(), roots, locationId);
                    var currentFacts = wound.Treatment.KnownRouteIds.Select(id => "route:" + id).Concat(wound.Complications.Where(row => WoundPlayerProjection.Visible(row.Visibility))
                        .Select(row => "complication:" + row.ComplicationId)).ToHashSet(StringComparer.Ordinal);
                    foreach (var method in typed.Treatment.DiagnosisPaths.Where(method => method.Visibility != "gm_only" &&
                        (WoundPlayerProjection.Visible(method.Visibility) || method.Visibility == "hidden" && method.RequiresKnownFacts.Length != 0) &&
                        method.RequiresKnownFacts.All(fact => currentFacts.Contains(fact.CanonicalValue))))
                    foreach (var provider in actors)
                    {
                        var providerOwner = new WoundOwnerCoordinate("mortal_world", provider.ActorKind, provider.ActorId, actorPaths[(provider.ActorKind, provider.ActorId)]);
                        var currentContext = MortalDiagnosisContext(owner, providerOwner, locationId);
                        var raw = wound.Treatment.DiagnosisPaths.Single(path => path.DiagnosisPathId == method.DiagnosisPathId);
                        var resolved = MortalWoundTreatmentAuthority.ResolveRequirementMembers(raw.Requirements, raw.SourcePath, currentContext, snapshot);
                        var selected = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
                        { MortalDiagnosis = new(owner, providerOwner, method.DiagnosisPathId, WoundDiagnosisEvidence.ComputePathFingerprint(wound, method.DiagnosisPathId), MortalWoundDiagnosisInputCodec.ContextFingerprint(currentContext)) };
                        var providerName = providerOwner == owner ? "самостоятельно" : provider.ActorKind == "player" ? "с вашей помощью" :
                            "с помощью «" + (provider.DisplayName == provider.ActorId ? "участник рядом" : WoundPlayerProjection.Text(provider.DisplayName)) + "»";
                        var label = WoundPlayerProjection.Text(method.DisplayName) + " — " + WoundPlayerProjection.Text(wound.Display.Name) + " — " + providerName;
                        var available = resolved.Success;
                        choices.Add(new("mortal_diagnosis_" + choices.Count, selected, label, wound.Severity.Value, null, available,
                            MortalDiagnosisDescription(method, context.Input) + (available ? " Все текущие условия подтверждены; окончательная проверка произойдёт перед попыткой." : " Сейчас невозможно подтвердить все условия осмотра.")));
                    }
                }
            }
            var state = new WoundCommandState(generation, choices).WithTargets(targets);
            return new(state, context with { MortalTargets = targets.ToArray() }, locationId, images);
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException && failure is
            (InvalidDataException or InvalidOperationException or JsonException or ArgumentException or KeyNotFoundException or OverflowException))
        { return new(new(generation, []), context, "", images); }
    }

    private static Dictionary<(string Kind, string Id), string> MortalDiagnosisActorPaths(IReadOnlyDictionary<string, JsonObject> roots)
    {
        var result = new Dictionary<(string, string), string> { [("player", "player_current")] = WoundCarrierCatalog.PlayerPath };
        if (roots.TryGetValue("game_state/npcs/npc_core.json", out var npcs))
            foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
                if (npcs[section] is JsonArray rows)
                    foreach (var npc in rows.OfType<JsonObject>())
                        if (GuardianPolicyContracts.TryResolveStrictPermanentNpcId(npc, out var id)) result[("npc", id)] = WoundCarrierCatalog.NpcPath;
        foreach (var (path, field) in new[] { (WoundCarrierCatalog.EnemiesPath, "enemiesData"), (WoundCarrierCatalog.AlliesPath, "alliesData") })
        {
            if (!roots.TryGetValue(path, out var root) || root[field] is not JsonArray rows) continue;
            foreach (var actor in rows.OfType<JsonObject>())
            {
                var members = actor["isGroup"] is JsonValue group && group.TryGetValue<bool>(out var grouped) && grouped && actor["members"] is JsonArray array
                    ? array.OfType<JsonObject>() : new[] { actor };
                foreach (var member in members)
                {
                    var kind = member["memberId"] is JsonValue ? "combatant_member" : "combatant";
                    var id = member[kind == "combatant_member" ? "memberId" : "combatantId"]?.GetValue<string>();
                    if (id is null || !result.TryAdd((kind, id), path)) throw new InvalidDataException("Ambiguous Mortal actor carrier.");
                }
            }
        }
        return result;
    }

    private static string MortalDiagnosisDescription(MortalWoundDiagnosisPathDefinition method, WoundPlayerProjectionInput input)
    {
        var modifier = method.Check.Resolution.ModifierSource is MortalWoundFixedZeroModifierSource ? "без поправки" : "с уровнем заявленного навыка";
        var requirements = method.Requirements.Select(requirement => WoundPlayerProjection.Requirement(requirement, input)).ToArray();
        var cost = method.Check.ResourcePolicy.Mutations.Length == 0 ? "Расходов нет." : "Расход по условиям: " +
            string.Join("; ", method.Check.ResourcePolicy.Mutations.Select(mutation => requirements[mutation.RequirementIndex])) +
            ". Списание при " + string.Join(" / ", method.Check.ResourcePolicy.ConsumeOn.Select(value => value == "success" ? "успехе" : "неудаче")) + ".";
        return "Проверка: 1d20 " + modifier + ", сложность " + method.Check.Resolution.Difficulty.ToString(CultureInfo.InvariantCulture) +
            "; обычные естественные 20/1 и правила Судьбы. Длительность: " + method.Check.DurationMinutes.ToString(CultureInfo.InvariantCulture) +
            " мин. " + cost + " Условия: " + string.Join("; ", requirements) + ". Осмотр сам не лечит. Просмотр и отмена бесплатны и не тратят время.";
    }

    internal static async Task<SpiritualSelfTreatmentStageResult> StageMortalDiagnosisInputAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentStageContext frame, SpiritualSelfTreatmentSelection selected)
    {
        var control = JsonSerializer.SerializeToNode(frame.Progression)?.Deserialize<ProgressionControl>() ?? throw new InvalidDataException("Missing complete diagnosis control.");
        var owned = frame with { Progression = control };
        try
        {
            if (selected.MortalDiagnosis is not { } diagnosis || control.CurrentRealm != "Mortal World") return new(false, false, "mortal_diagnosis_selection_invalid", null);
            var current = await WoundPlayerReadService.LoadContextAsync(files, lease, retainMenuInputs: true) ?? throw new InvalidDataException("Current diagnosis display source is unavailable.");
            var menu = await BuildMortalDiagnosisMenuAsync(files, lease, files.GetOrCreateSessionGeneration(lease), current);
            if (!menu.State.Choices.Any(choice => choice.Available && choice.Selection == selected)) return new(false, false, "mortal_diagnosis_source_changed", null);
            var freshControl = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync(lease, "Mortal World");
            if (!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(control), JsonSerializer.SerializeToNode(freshControl))) return new(false, false, "mortal_diagnosis_control_changed", null);
            var context = MortalDiagnosisContext(diagnosis.Target, diagnosis.Provider, menu.LocationId);
            var input = MortalWoundDiagnosisInputCodec.Create(owned, selected, context);
            var path = AcceptedMechanicsPlan.WoundCommandPath;
            var before = await files.ReadFileBytesAsync(lease, path);
            var json = input.ToJsonString();
            if (before is not null && !before.AsSpan().SequenceEqual(FileSystemManager.EncodeUtf8WithPreamble(json))) return new(false, false, "pending_conflict", null);
            var writes = menu.Images.Where(pair => pair.Key != path).Select(pair => CoordinatedStateWriteHelper.CreateExactGuardWrite(pair.Key, pair.Value)).ToList();
            writes.Add(before is null ? new(path, null, json, RequireCurrentBaseline: true, ExactPrevious: new(false, null))
                : CoordinatedStateWriteHelper.CreateExactGuardWrite(path, new(true, before)));
            if (!await CoordinatedStateWriteHelper.TryCommitAsync(files, lease, writes.ToArray())) return new(false, false, "source_changed", null);
            var readback = await files.ReadFileBytesAsync(lease, path);
            if (readback is null || !readback.AsSpan().SequenceEqual(FileSystemManager.EncodeUtf8WithPreamble(json)))
                throw new CoordinatedStatePublicationUncertainException(new InvalidDataException("Mortal diagnosis staging readback differs."));
            var method = current.Catalog.Occurrences.Single(row => row.Wound.WoundId == selected.WoundId).Wound.Treatment.DiagnosisPaths
                .Single(path => path.DiagnosisPathId == diagnosis.DiagnosisPathId);
            return new(true, before is not null, null, MortalWoundDiagnosisInputCodec.Fingerprint(input))
            { GmContext = new JsonObject
                {
                    ["kind"] = "mortal_diagnosis_projection", ["schemaVersion"] = 1, ["selection"] = selected.ToJson(),
                    ["inputFingerprint"] = MortalWoundDiagnosisInputCodec.Fingerprint(input),
                    ["durationMinutes"] = method.Check.GetProperty("durationMinutes").GetInt32(),
                    ["check"] = JsonNode.Parse(method.Check.GetRawText()),
                    ["requirements"] = new JsonArray(method.Requirements.Select(value => JsonNode.Parse(value.GetRawText())).ToArray()),
                    ["failurePolicy"] = method.FailurePolicy,
                    ["heldRequestPath"] = WoundAcceptedTurnSnapshotContract.PendingResolutionPath
                }
            };
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException && failure is
            (InvalidDataException or InvalidOperationException or JsonException or ArgumentException or KeyNotFoundException or OverflowException))
        { return new(false, false, "mortal_diagnosis_source_invalid", null); }
    }
}
