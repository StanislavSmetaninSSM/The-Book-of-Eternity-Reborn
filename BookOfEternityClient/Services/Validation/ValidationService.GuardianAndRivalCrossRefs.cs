using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
public partial class ValidationService
{
    private async Task ValidateGuardianCrossReferencesAsync(List<ValidationIssue> issues)
    {
        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        if (!guardianPolicyContext.CurrentStateReadable || !guardianPolicyContext.HasCurrentRoot)
            return;

        try
        {
            var guardiansById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var guardianStateById = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            if (guardianPolicyContext.CurrentRoot.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var guardian in guardians.EnumerateArray())
                {
                    var guardianContext = $"game_state/meta/guardians.json.guardians[{index}]";
                    var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
                    if (!string.IsNullOrWhiteSpace(guardianId))
                    {
                        guardiansById[guardianId] = guardianContext;
                        guardianStateById[guardianId] = guardian.Clone();
                    }

                    index++;
                }
            }

            if (guardianPolicyContext.HasCurrentActiveGuardian)
            {
                var activeGuardian = guardianPolicyContext.CurrentActiveGuardian;
                var activeGuardianId = GetFirstNonEmptyString(activeGuardian, "guardianId", "id");
                var activeGuardianName = GuardianManifestation.GetDisplayName(activeGuardian);
                var found = !string.IsNullOrWhiteSpace(activeGuardianId) && guardiansById.ContainsKey(activeGuardianId);

                if (!found)
                {
                    var activeGuardianRef = !string.IsNullOrWhiteSpace(activeGuardianId)
                        ? $"guardianId={activeGuardianId}"
                        : !string.IsNullOrWhiteSpace(activeGuardianName)
                            ? $"name={activeGuardianName}"
                            : "unknown activeGuardian reference";
                    issues.Add(new ValidationIssue(
                        "game_state/meta/guardians.json",
                        IssueSeverity.Error,
                        $"Активный хранитель '{activeGuardianName ?? activeGuardianId ?? "unknown"}' не найден в массиве guardians",
                        code: "active_guardian_missing_in_guardians_array",
                        section: "Guardians",
                        expected: "activeGuardian.guardianId matches an entry inside guardians[]",
                        actual: activeGuardianRef,
                        repairHint: "Синхронно обновляй activeGuardian как strict mirror той же guardian entry из guardians[]. Не оставляй activeGuardian со stale guardianId, даже если имя визуально совпадает."));
                }
                else if (!string.IsNullOrWhiteSpace(activeGuardianId) &&
                         guardianStateById.TryGetValue(activeGuardianId, out var canonicalGuardian))
                {
                    var activeAbodeId = TryReadGuardianAbodeId(activeGuardian);
                    var canonicalAbodeId = TryReadGuardianAbodeId(canonicalGuardian);
                    if (!string.IsNullOrWhiteSpace(activeAbodeId) &&
                        !string.IsNullOrWhiteSpace(canonicalAbodeId) &&
                        !string.Equals(activeAbodeId, canonicalAbodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/meta/guardians.json.activeGuardian.abode.abodeId",
                            IssueSeverity.Error,
                            "activeGuardian должен зеркалить canonical abodeId из guardians[].",
                            code: "active_guardian_mirror_abode_mismatch",
                            section: "Guardians",
                            expected: canonicalAbodeId,
                            actual: activeAbodeId,
                            repairHint: "Синхронизируй activeGuardian.abode.abodeId с canonical guardian entry из guardians[]. Не держи stale mirror для policy-sensitive abode context."));
                    }

                    var activeReputation = TryReadGuardianCurrentReputation(activeGuardian);
                    var canonicalReputation = TryReadGuardianCurrentReputation(canonicalGuardian);
                    if (activeReputation.HasValue &&
                        canonicalReputation.HasValue &&
                        activeReputation.Value != canonicalReputation.Value)
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/meta/guardians.json.activeGuardian.relationshipData.currentReputation",
                            IssueSeverity.Error,
                            "activeGuardian должен зеркалить canonical currentReputation из guardians[].",
                            code: "active_guardian_mirror_reputation_mismatch",
                            section: "Guardians",
                            expected: canonicalReputation.Value.ToString(),
                            actual: activeReputation.Value.ToString(),
                            repairHint: "Синхронизируй activeGuardian.relationshipData.currentReputation с canonical guardian entry из guardians[]. Mirror не должен расходиться с authoritative guardian state."));
                    }
                }
            }

            var currentPendingPresetId = TryReadPendingSystemGuardianPresetId(guardianPolicyContext.CurrentRoot);
            if (!string.IsNullOrWhiteSpace(currentPendingPresetId) &&
                TryGetGuardianBaselineFailureKind(guardianPolicyContext, out var guardianBaselineFailureKind))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json.pendingGuardianCreation",
                    IssueSeverity.Error,
                    "Client-selected system guardian preset требует kernel-backed validated pre-turn guardians baseline и не может проверяться по current guardians[] fallback.",
                    code: "system_guardian_pending_preset_missing_validated_guardians_snapshot",
                    section: "SystemGuardianPresets",
                    expected: "validated pre-turn guardians.json snapshot with pendingGuardianCreation.mode=system_preset",
                    actual: DescribeGuardianPreTurnBaselineFailure(guardianPolicyContext),
                    repairHint: $"Для system_preset flows сохраняй usable validated snapshot copy game_state/meta/guardians.json. Preset materialization нельзя подтверждать только по current state; baseline failure = {guardianBaselineFailureKind}."));
            }

            var pendingPresetId = HasResolvedStrictPreTurnGuardianAuthority(guardianPolicyContext)
                ? TryReadPendingSystemGuardianPresetId(guardianPolicyContext.PreTurnAuthorityRoot)
                : null;
            if (!string.IsNullOrWhiteSpace(pendingPresetId))
            {
                if (!TryGetCurrentAuthorityActiveGuardian(guardianPolicyContext, out var selectedActiveGuardian))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/guardians.json.activeGuardian",
                        IssueSeverity.Error,
                        "Системный guardian preset был выбран клиентом, но kernel-authoritative activeGuardian не materialized.",
                        code: "system_guardian_pending_preset_missing_active_guardian",
                        section: "SystemGuardianPresets",
                        expected: $"activeGuardian.sourcePreset.presetId = {pendingPresetId}",
                        actual: guardianPolicyContext.HasCurrentActiveGuardian
                            ? $"raw mirror without current guardian authority ({DescribeCurrentGuardianAuthorityFailure(guardianPolicyContext)})"
                            : DescribeCurrentGuardianAuthorityFailure(guardianPolicyContext)));
                }
                else
                {
                    var activePresetId = TryReadGuardianSourcePresetId(selectedActiveGuardian);
                    if (!string.Equals(activePresetId, pendingPresetId, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/meta/guardians.json.activeGuardian.sourcePreset.presetId",
                            IssueSeverity.Error,
                            "GM проигнорировал client-selected system guardian preset при materialization Хранителя.",
                            code: "system_guardian_pending_preset_not_materialized",
                            section: "SystemGuardianPresets",
                            expected: pendingPresetId,
                            actual: string.IsNullOrWhiteSpace(activePresetId) ? "missing/empty" : activePresetId,
                            repairHint: "При system-preset guardian creation materialize именно выбранного Хранителя и запиши matching sourcePreset metadata."));
                    }
                }
            }

            await ValidatePlayerFoundedGuardianContinuityAsync(issues, guardianPolicyContext, guardianStateById);
        }
        catch
        {
            // ignored
        }
    }

    private async Task ValidatePlayerFoundedGuardianContinuityAsync(
        List<ValidationIssue> issues,
        GuardianPolicyContext guardianPolicyContext,
        IReadOnlyDictionary<string, JsonElement> guardianStateById)
    {
        var foundedGuardians = guardianStateById
            .Where(pair => string.Equals(
                GetFirstNonEmptyString(pair.Value, "originType"),
                PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (foundedGuardians.Count > 1)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json.guardians",
                IssueSeverity.Error,
                "V1 поддерживает только одного player-founded guardian на save",
                code: "player_guardian_foundation_multiple_founded_guardians",
                section: "PlayerGuardianFoundation",
                expected: "at most one guardian with originType=player_founded_ascended_soul",
                actual: foundedGuardians.Count.ToString(),
                repairHint: "Не materialize второй player-founded guardian в том же save. Сохраняй single-use foundation branch."));
        }

        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            using var soulDoc = JsonDocument.Parse(soulJson);
            var linkedGuardianId = GetFirstNonEmptyString(soulDoc.RootElement, PlayerGuardianFoundationState.SoulStateGuardianIdProperty);
            var foundationStatus = GetFirstNonEmptyString(soulDoc.RootElement, PlayerGuardianFoundationState.SoulStateFoundationStatusProperty);
            if (!string.IsNullOrWhiteSpace(linkedGuardianId))
            {
                if (!guardianStateById.TryGetValue(linkedGuardianId, out var linkedGuardian))
                {
                    issues.Add(new ValidationIssue(
                        $"game_state/meta/soul_state.json.{PlayerGuardianFoundationState.SoulStateGuardianIdProperty}",
                        IssueSeverity.Error,
                        "playerFoundedGuardianId должен ссылаться на существующего guardian из guardians[]",
                        code: "player_guardian_foundation_unknown_soul_link_guardian",
                        section: "PlayerGuardianFoundation",
                        expected: "guardianId from current guardians[]",
                        actual: linkedGuardianId,
                        repairHint: "Синхронизируй soul_state.playerFoundedGuardianId с реально materialized player-founded guardian."));
                }
                else if (!string.Equals(
                             GetFirstNonEmptyString(linkedGuardian, "originType"),
                             PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
                             StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"game_state/meta/soul_state.json.{PlayerGuardianFoundationState.SoulStateGuardianIdProperty}",
                        IssueSeverity.Error,
                        "playerFoundedGuardianId должен ссылаться именно на player-founded guardian",
                        code: "player_guardian_foundation_soul_link_not_player_founded",
                        section: "PlayerGuardianFoundation",
                        expected: PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
                        actual: GetFirstNonEmptyString(linkedGuardian, "originType") ?? "missing"));
                }
            }

            if (foundedGuardians.Count == 1)
            {
                var foundedGuardianId = foundedGuardians[0].Key;
                if (!string.Equals(linkedGuardianId, foundedGuardianId, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"game_state/meta/soul_state.json.{PlayerGuardianFoundationState.SoulStateGuardianIdProperty}",
                        IssueSeverity.Error,
                        "Player-founded guardian требует matching soul link в soul_state.json",
                        code: "player_guardian_foundation_missing_soul_link",
                        section: "PlayerGuardianFoundation",
                        expected: foundedGuardianId,
                        actual: linkedGuardianId ?? "missing",
                        repairHint: $"Сохраняй soul_state.{PlayerGuardianFoundationState.SoulStateGuardianIdProperty} с guardianId основанного Хранителя."));
                }

                if (!string.Equals(foundationStatus, PlayerGuardianFoundationState.SoulStateFoundationStatusFounded, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"game_state/meta/soul_state.json.{PlayerGuardianFoundationState.SoulStateFoundationStatusProperty}",
                        IssueSeverity.Error,
                        "Player-founded guardian требует additive soul-side foundation status founded",
                        code: "player_guardian_foundation_missing_soul_status",
                        section: "PlayerGuardianFoundation",
                        expected: PlayerGuardianFoundationState.SoulStateFoundationStatusFounded,
                        actual: foundationStatus ?? "missing",
                        repairHint: $"Сохраняй soul_state.{PlayerGuardianFoundationState.SoulStateFoundationStatusProperty} = {PlayerGuardianFoundationState.SoulStateFoundationStatusFounded} после successful foundation resolution."));
                }
            }
            else if (!string.IsNullOrWhiteSpace(foundationStatus))
            {
                issues.Add(new ValidationIssue(
                    $"game_state/meta/soul_state.json.{PlayerGuardianFoundationState.SoulStateFoundationStatusProperty}",
                    IssueSeverity.Error,
                    "playerGuardianFoundationStatus не должен существовать без canonical player-founded guardian",
                    code: "player_guardian_foundation_orphaned_soul_status",
                    section: "PlayerGuardianFoundation",
                    expected: "missing status or a matching founded guardian",
                    actual: foundationStatus,
                    repairHint: $"Убирай soul_state.{PlayerGuardianFoundationState.SoulStateFoundationStatusProperty}, если foundation route ещё не materialized canonically."));
            }

            if (guardianPolicyContext.CurrentRoot.TryGetProperty(PlayerGuardianFoundationState.HistoryProperty, out var history) &&
                history.ValueKind == JsonValueKind.Array &&
                foundedGuardians.Count == 1)
            {
                var foundedGuardianId = foundedGuardians[0].Key;
                var matchingHistoryEntries = history.EnumerateArray().Where(entry =>
                    entry.ValueKind == JsonValueKind.Object &&
                    string.Equals(GetFirstNonEmptyString(entry, "guardianId"), foundedGuardianId, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matchingHistoryEntries.Count == 0)
                {
                    issues.Add(new ValidationIssue(
                        $"game_state/meta/guardians.json.{PlayerGuardianFoundationState.HistoryProperty}",
                        IssueSeverity.Error,
                        "Player-founded guardian требует matching foundation history receipt",
                        code: "player_guardian_foundation_missing_history_link",
                        section: "PlayerGuardianFoundation",
                        expected: $"history receipt for guardianId {foundedGuardianId}",
                        actual: "no matching history entry",
                        repairHint: $"Append-ь matching receipt в guardians.json.{PlayerGuardianFoundationState.HistoryProperty} при materialization player-founded guardian."));
                }
                else
                {
                    var formerPatronGuardianId = GetFirstNonEmptyString(matchingHistoryEntries[^1], "formerPatronGuardianId") ??
                                                 GetFirstNonEmptyString(foundedGuardians[0].Value, "formerPatronGuardianId");
                    var guardiansWithFormerPatronRole = guardianStateById
                        .Where(pair => string.Equals(
                            PlayerGuardianFoundationState.TryReadGuardianRoleToPlayer(pair.Value),
                            PlayerGuardianFoundationState.GuardianRoleFormerPatron,
                            StringComparison.OrdinalIgnoreCase))
                        .Select(pair => pair.Key)
                        .ToList();

                    if (!string.IsNullOrWhiteSpace(formerPatronGuardianId))
                    {
                        if (!guardianStateById.TryGetValue(formerPatronGuardianId, out var formerPatronGuardian))
                        {
                            issues.Add(new ValidationIssue(
                                $"game_state/meta/guardians.json.{PlayerGuardianFoundationState.HistoryProperty}",
                                IssueSeverity.Error,
                                "Foundation history ссылается на несуществующего former patron guardian",
                                code: "player_guardian_foundation_unknown_former_patron",
                                section: "PlayerGuardianFoundation",
                                expected: "existing guardianId in guardians[]",
                                actual: formerPatronGuardianId));
                        }
                        else if (!string.Equals(
                                     PlayerGuardianFoundationState.TryReadGuardianRoleToPlayer(formerPatronGuardian),
                                     PlayerGuardianFoundationState.GuardianRoleFormerPatron,
                                     StringComparison.OrdinalIgnoreCase))
                        {
                            issues.Add(new ValidationIssue(
                                $"game_state/meta/guardians.json.guardians[{formerPatronGuardianId}].relationshipData.{PlayerGuardianFoundationState.GuardianRoleToPlayerProperty}",
                                IssueSeverity.Error,
                                "Прежний покровитель должен сохранять canonical role former_patron после foundation branch",
                                code: "player_guardian_foundation_missing_former_patron_role",
                                section: "PlayerGuardianFoundation",
                                expected: PlayerGuardianFoundationState.GuardianRoleFormerPatron,
                                actual: PlayerGuardianFoundationState.TryReadGuardianRoleToPlayer(formerPatronGuardian) ?? "missing"));
                        }
                    }

                    if (guardiansWithFormerPatronRole.Count > 1)
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/meta/guardians.json.guardians",
                            IssueSeverity.Error,
                            "V1 foundation branch не должна оставлять больше одного guardian с ролью former_patron",
                            code: "player_guardian_foundation_multiple_former_patrons",
                            section: "PlayerGuardianFoundation",
                            expected: "at most one guardianRoleToPlayer=former_patron",
                            actual: guardiansWithFormerPatronRole.Count.ToString()));
                    }
                    else if (guardiansWithFormerPatronRole.Count == 1 &&
                             !string.IsNullOrWhiteSpace(formerPatronGuardianId) &&
                             !string.Equals(guardiansWithFormerPatronRole[0], formerPatronGuardianId, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/meta/guardians.json.guardians",
                            IssueSeverity.Error,
                            "guardianRoleToPlayer=former_patron должен указывать именно на прежнего activeGuardian из foundation history",
                            code: "player_guardian_foundation_former_patron_role_mismatch",
                            section: "PlayerGuardianFoundation",
                            expected: formerPatronGuardianId,
                            actual: guardiansWithFormerPatronRole[0]));
                    }
                }
            }
        }
        catch
        {
            // malformed soul state reported elsewhere
        }
    }

    private static string? TryReadPendingSystemGuardianPresetId(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (!root.TryGetProperty("pendingGuardianCreation", out var pending) ||
            pending.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var mode = GetFirstNonEmptyString(pending, "mode");
        if (!string.Equals(mode, "system_preset", StringComparison.OrdinalIgnoreCase))
            return null;

        return GetFirstNonEmptyString(pending, "presetId");
    }

    private static string? TryReadGuardianSourcePresetId(JsonElement guardian)
    {
        if (!guardian.TryGetProperty("sourcePreset", out var sourcePreset) ||
            sourcePreset.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetFirstNonEmptyString(sourcePreset, "presetId");
    }


    private static HashSet<string> ReadGuardianIdsFromJson(string? guardiansJson)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return ids;

        try
        {
            using var doc = JsonDocument.Parse(guardiansJson);
            if (doc.RootElement.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
            {
                foreach (var guardian in guardians.EnumerateArray())
                {
                    var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
                    if (!string.IsNullOrWhiteSpace(guardianId))
                        ids.Add(guardianId);
                }
            }
        }
        catch
        {
            // ignored
        }

        return ids;
    }


    private Task<HashSet<string>> ReadKnownGuardianIdsAsync()
    {
        var state = ReadGuardianIdentityValidationState();
        return Task.FromResult(new HashSet<string>(state.KnownGuardianIds, StringComparer.OrdinalIgnoreCase));
    }

    private sealed record GuardianReferenceValidationState(
        HashSet<string> Ids,
        HashSet<string> Names,
        GuardianBaselineFailureKind BaselineFailureKind,
        GuardianTrackedSnapshotFileStatus SnapshotFileStatus,
        string BaselineFailureDescription);

    private Task<GuardianReferenceValidationState> ReadKnownGuardianReferencesAsync()
    {
        var state = ReadGuardianIdentityValidationState();
        var guardianPolicyContext = ResolveGuardianPolicyContextSync();
        var failureKind = ResolveGuardianBaselineFailureKind(guardianPolicyContext);
        return Task.FromResult(new GuardianReferenceValidationState(
            new HashSet<string>(state.KnownGuardianIds, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(state.KnownGuardianNames, StringComparer.OrdinalIgnoreCase),
            failureKind,
            guardianPolicyContext.PreTurnGuardiansSnapshot.FileStatus,
            DescribeGuardianPreTurnBaselineFailure(guardianPolicyContext)));
    }


    private async Task ValidateGuardianNpcBoundaryAsync(List<ValidationIssue> issues, GuardianReferenceValidationState knownGuardianReferences)
    {
        var json = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var hasNpcSurface = false;
            foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
            {
                if (doc.RootElement.TryGetProperty(sectionName, out var sectionArray) &&
                    sectionArray.ValueKind == JsonValueKind.Array &&
                    sectionArray.GetArrayLength() > 0)
                {
                    hasNpcSurface = true;
                    break;
                }
            }

            if (knownGuardianReferences.Ids.Count == 0 &&
                knownGuardianReferences.Names.Count == 0 &&
                knownGuardianReferences.BaselineFailureKind != GuardianBaselineFailureKind.None)
            {
                if (hasNpcSurface)
                {
                    issues.Add(new ValidationIssue(
                        "game_state/npcs/npc_core.json",
                        IssueSeverity.Error,
                        "Guardian/NPC boundary validation требует kernel-backed validated pre-turn guardians baseline и не может silently disappear при broken guardian provenance.",
                        code: "guardian_npc_boundary_missing_validated_preturn_guardians_snapshot",
                        section: "Guardians",
                        expected: "validated pre-turn guardians baseline for guardian/NPC collision checks",
                        actual: knownGuardianReferences.BaselineFailureDescription,
                        repairHint: "Сохраняй readable validated snapshot copy game_state/meta/guardians.json, чтобы boundary validator мог отличить guardians от NPC surfaces."));
                }

                return;
            }

            if (knownGuardianReferences.Ids.Count == 0 && knownGuardianReferences.Names.Count == 0)
                return;

            foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
            {
                if (!doc.RootElement.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;

                var index = 0;
                foreach (var npc in arr.EnumerateArray())
                {
                    var context = $"game_state/npcs/npc_core.json.{sectionName}[{index++}]";
                    if (npc.ValueKind != JsonValueKind.Object)
                        continue;

                    var npcId = GetFirstNonEmptyString(npc, "NPCId", "npcId", "id");
                    var npcName = GetFirstNonEmptyString(npc, "name", "npcName", "NPCName");
                    var idCollision = !string.IsNullOrWhiteSpace(npcId) && knownGuardianReferences.Ids.Contains(npcId);
                    var nameCollision = !string.IsNullOrWhiteSpace(npcName) && knownGuardianReferences.Names.Contains(npcName);
                    if (!idCollision && !nameCollision)
                        continue;

                    issues.Add(new ValidationIssue(
                        context,
                        IssueSeverity.Error,
                        "Guardians не должны попадать в NPC surfaces",
                        code: "guardian_leaked_into_npc_surface",
                        section: "Guardians",
                        expected: "Guardians only in UpdateGuardians / guardians.json",
                        actual: idCollision
                            ? $"guardianId collision: {npcId}"
                            : $"guardian name collision: {npcName}",
                        repairHint: "Не используй UpdateNPCs или NPCsInScene для Хранителей. Перенеси сущность в UpdateGuardians / game_state/meta/guardians.json."));
                }
            }
        }
        catch
        {
            // ignored
        }
    }


    private static void CollectGuardianReferencesFromStateRoot(JsonElement root, HashSet<string> ids, HashSet<string> names, bool includeCommandSurfaces = true)
    {
        if (root.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
        {
            foreach (var guardian in guardians.EnumerateArray())
                RegisterGuardianReference(guardian, ids, names);
        }

        if (root.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object)
            RegisterGuardianReference(activeGuardian, ids, names);

        if (includeCommandSurfaces &&
            root.TryGetProperty("UpdateGuardians", out var updates) &&
            updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var command in updates.EnumerateArray())
            {
                if (command.ValueKind != JsonValueKind.Object)
                    continue;

                var commandName = GetFirstNonEmptyString(command, "command");
                if (string.Equals(commandName, "create", StringComparison.OrdinalIgnoreCase) &&
                    command.TryGetProperty("data", out var data) &&
                    data.ValueKind == JsonValueKind.Object)
                {
                    RegisterGuardianReference(data, ids, names);
                    continue;
                }

                RegisterGuardianReference(command, ids, names);
            }
        }
    }


    private static void RegisterGuardianReference(JsonElement guardian, HashSet<string> ids, HashSet<string> names)
    {
        var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
        var guardianName = GuardianManifestation.GetDisplayName(guardian);
        var canonicalName = GuardianManifestation.GetCanonicalName(guardian);
        if (!string.IsNullOrWhiteSpace(guardianId))
            ids.Add(guardianId);
        if (!string.IsNullOrWhiteSpace(guardianName))
            names.Add(guardianName);
        if (!string.IsNullOrWhiteSpace(canonicalName))
            names.Add(canonicalName);
    }


    private static void CollectCreatedGuardianIdsFromStateRoot(JsonElement root, HashSet<string> ids)
    {
        if (!root.TryGetProperty("UpdateGuardians", out var updates) || updates.ValueKind != JsonValueKind.Array)
            return;

        foreach (var command in updates.EnumerateArray())
        {
            if (command.ValueKind != JsonValueKind.Object)
                continue;

            var commandName = GetFirstNonEmptyString(command, "command");
            if (!string.Equals(commandName, "create", StringComparison.OrdinalIgnoreCase) ||
                !command.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var guardianId = GetFirstNonEmptyString(data, "guardianId", "id");
            if (!string.IsNullOrWhiteSpace(guardianId))
                ids.Add(guardianId);
        }
    }


    private static void CollectCreatedGuardianReferencesFromStateRoot(JsonElement root, HashSet<string> ids, HashSet<string> names)
    {
        if (!root.TryGetProperty("UpdateGuardians", out var updates) || updates.ValueKind != JsonValueKind.Array)
            return;

        foreach (var command in updates.EnumerateArray())
        {
            if (command.ValueKind != JsonValueKind.Object)
                continue;

            var commandName = GetFirstNonEmptyString(command, "command");
            if (!string.Equals(commandName, "create", StringComparison.OrdinalIgnoreCase) ||
                !command.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            RegisterGuardianReference(data, ids, names);
        }
    }


    private static void CollectCodexEntryIdsFromRoot(JsonElement root, HashSet<string> ids, bool includeStoredEntries)
    {
        if (includeStoredEntries &&
            root.TryGetProperty("entries", out var entries) &&
            entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                var entryId = GetFirstNonEmptyString(entry, "entryId");
                if (!string.IsNullOrWhiteSpace(entryId))
                    ids.Add(entryId);
            }
        }

        if (root.TryGetProperty("loreCodexUpdates", out var updates) && updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in updates.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var command = GetFirstNonEmptyString(item, "command");
                var isSameTurnAdd = string.Equals(command, "add", StringComparison.OrdinalIgnoreCase);

                if (!isSameTurnAdd)
                    continue;

                if (item.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.Object)
                {
                    var entryId = GetFirstNonEmptyString(entry, "entryId");
                    if (!string.IsNullOrWhiteSpace(entryId))
                        ids.Add(entryId);
                }
            }
        }
    }


    private async Task ValidateSoulQuestGuardianCrossReferencesAsync(List<ValidationIssue> issues, HashSet<string> knownGuardianIds)
    {
        var json = await _fs.ReadFileAsync("game_state/quests/soul_quests.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            string questCollectionName;
            if (doc.RootElement.TryGetProperty("quests", out var quests))
            {
                questCollectionName = "quests";
            }
            else if (doc.RootElement.TryGetProperty("UpdateSoulQuests", out quests))
            {
                questCollectionName = "UpdateSoulQuests";
            }
            else
            {
                return;
            }

            if (quests.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var quest in quests.EnumerateArray())
            {
                var guardianId = GetFirstNonEmptyString(quest, "guardianId");
                if (!string.IsNullOrWhiteSpace(guardianId) && !knownGuardianIds.Contains(guardianId))
                {
                    issues.Add(new ValidationIssue(
                        $"game_state/quests/soul_quests.json.{questCollectionName}[{index}].guardianId",
                        IssueSeverity.Error,
                        $"Soul Quest ссылается на неизвестного guardianId '{guardianId}'",
                        code: "soul_quest_unknown_guardian_id",
                        section: "CrossReferences",
                        expected: "existing guardianId from canonical guardians state",
                        actual: guardianId,
                        repairHint: "Для Soul Quest используй существующий guardianId из canonical guardians state. Если Хранитель создаётся впервые, сначала сохрани его через UpdateGuardians.create, а затем ссылайся на его permanent guardianId."));
                }
                index++;
            }
        }
        catch
        {
            // ignored
        }
    }


    private async Task<HashSet<string>> ReadKnownSystemGuardianPresetIdsAsync()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootDirectory = Path.Combine(_fs.BasePath, SystemGuardianLibraryService.RootDirectoryName);
        if (!Directory.Exists(rootDirectory))
            return result;

        foreach (var manifestPath in Directory.EnumerateFiles(rootDirectory, "manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    continue;

                var presetId = GetFirstNonEmptyString(doc.RootElement, "presetId");
                if (!string.IsNullOrWhiteSpace(presetId))
                    result.Add(presetId);
            }
            catch
            {
                // ignored
            }
        }

        return result;
    }


    private async Task ValidateRivalSoulArcCrossReferencesAsync(
        List<ValidationIssue> issues,
        HashSet<string> knownGuardianIds,
        HashSet<string> knownSystemGuardianPresetIds)
    {
        var rivalState = await TryReadCurrentRivalValidationDocumentAsync();
        using var rivalDocScope = rivalState.Document;
        switch (rivalState.Kind)
        {
            case CurrentOwnerStateReadKind.MissingOrWhitespace:
            case CurrentOwnerStateReadKind.ReadableButNoRelevantCollection:
                return;
            case CurrentOwnerStateReadKind.UnreadableJson:
            case CurrentOwnerStateReadKind.ContractInvalidTopLevel:
            case CurrentOwnerStateReadKind.NonObjectRoot:
                AddInvalidCurrentRivalValidationIssue(issues, rivalState.Actual ?? "current rival_soul_arcs.json unreadable or malformed");
                return;
            case CurrentOwnerStateReadKind.InvalidCollectionShape:
                AddInvalidCurrentRivalValidationIssue(issues, rivalState.Actual ?? $"current rival_soul_arcs.json.{rivalState.CollectionName} has invalid shape");
                return;
            case CurrentOwnerStateReadKind.ReadableWithArrayCollection:
                break;
            default:
                return;
        }

        if (rivalState.Document is null || rivalState.CollectionName is null)
            return;

        JsonDocument? worldEventsDoc = null;
        JsonElement worldEvents = default;
        string? worldEventCollectionName = null;
        var relatedWorldEventsByArcId = new Dictionary<string, List<JsonElement>>(StringComparer.OrdinalIgnoreCase);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");
        var hasCurrentWorldEventsFile = _fs.FileExists("game_state/world/world_events.json");
        var knownArcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownArcSponsorGuardianIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var visibleBonusClueUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var countedVisibleBonusClueRevealKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hostileClueContractArcs = new List<(JsonElement Arc, string ArcContext)>();
        var bonusClueTrackerAuthorityResolutionAttempted = false;
        var hasBonusClueTrackerAuthority = false;
        JsonElement bonusClueTrackerAuthorityRoot = default;
        GuardianProjectTrackerPolicyContext? bonusClueTrackerAuthorityContext = null;
        var bonusClueCurrentIncarnationResolutionAttempted = false;
        var hasBonusClueCurrentIncarnation = false;
        var bonusClueCurrentIncarnation = 0;
        var index = 0;
        foreach (var arc in rivalState.Collection.EnumerateArray())
        {
            var arcContext = $"{RivalSoulArcService.StatePath}.{rivalState.CollectionName}[{index}]";
            var arcId = GetFirstNonEmptyString(arc, "arcId");
            if (!string.IsNullOrWhiteSpace(arcId))
                knownArcIds.Add(arcId);
            hostileClueContractArcs.Add((arc, arcContext));

            if (arc.ValueKind != JsonValueKind.Object ||
                !arc.TryGetProperty("sponsorGuardianRef", out var sponsorRef) ||
                sponsorRef.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            var mode = GetFirstNonEmptyString(sponsorRef, "mode");
            var sponsorGuardianId = string.Empty;
            if (string.Equals(mode, "guardianId", StringComparison.OrdinalIgnoreCase))
            {
                sponsorGuardianId = GetFirstNonEmptyString(sponsorRef, "guardianId");
                if (!string.IsNullOrWhiteSpace(sponsorGuardianId) && !knownGuardianIds.Contains(sponsorGuardianId))
                {
                    issues.Add(new ValidationIssue(
                        $"{arcContext}.sponsorGuardianRef.guardianId",
                        IssueSeverity.Error,
                        $"Rival soul arc ссылается на неизвестный guardianId '{sponsorGuardianId}'",
                        code: "rival_arc_unknown_guardian_id",
                        section: "RivalSoulArcs",
                        expected: "existing guardianId from canonical guardians state",
                        actual: sponsorGuardianId,
                        repairHint: "Для sponsorGuardianRef.mode=guardianId используй guardianId уже существующего Хранителя из canonical guardians state."));
                }
                else if (!string.IsNullOrWhiteSpace(arcId) && !string.IsNullOrWhiteSpace(sponsorGuardianId))
                {
                    knownArcSponsorGuardianIds[arcId] = sponsorGuardianId;
                }
            }
            else if (string.Equals(mode, "eternalPreset", StringComparison.OrdinalIgnoreCase))
            {
                var presetId = GetFirstNonEmptyString(sponsorRef, "presetId");
                if (!string.IsNullOrWhiteSpace(presetId) && !knownSystemGuardianPresetIds.Contains(presetId))
                {
                    issues.Add(new ValidationIssue(
                        $"{arcContext}.sponsorGuardianRef.presetId",
                        IssueSeverity.Error,
                        $"Rival soul arc ссылается на неизвестный Eternal Guardian preset '{presetId}'",
                        code: "rival_arc_unknown_eternal_guardian_preset",
                        section: "RivalSoulArcs",
                        expected: "existing presetId from system_guardians library",
                        actual: presetId,
                        repairHint: "Для sponsorGuardianRef.mode=eternalPreset используй реальный presetId из библиотеки извечных хранителей."));
                }
            }

            if (arc.ValueKind == JsonValueKind.Object &&
                arc.TryGetProperty("publicSignals", out var publicSignals) &&
                publicSignals.ValueKind == JsonValueKind.Array)
            {
                var signalIndex = 0;
                foreach (var signal in publicSignals.EnumerateArray())
                {
                    var signalContext = $"{arcContext}.publicSignals[{signalIndex++}]";
                    var sourceProjectId = GetFirstNonEmptyString(signal, "bonusClueSourceProjectId");
                    if (string.IsNullOrWhiteSpace(sourceProjectId))
                        continue;

                    var visibleToPlayer = signal.TryGetProperty("visibleToPlayer", out var visibleNode) &&
                                          visibleNode.ValueKind == JsonValueKind.True;
                    if (!string.Equals(mode, "guardianId", StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(sponsorGuardianId))
                    {
                        issues.Add(new ValidationIssue(
                            $"{signalContext}.bonusClueSourceProjectId",
                            IssueSeverity.Error,
                            "Bonus clue от lore_research допустим только для rival arc со sponsorGuardianRef.mode=guardianId",
                            code: "rival_arc_bonus_clue_requires_guardian_sponsor",
                            section: "RivalSoulArcs",
                            repairHint: "Используй bonusClueSourceProjectId только там, где sponsorGuardianRef указывает на materialized guardianId."));
                        continue;
                    }

                    if (!visibleToPlayer)
                        continue;

                    var clueCost = TryReadIntField(signal, "bonusClueCost", out var parsedCost) ? Math.Max(1, parsedCost) : 1;
                    var revealKey = BuildVisibleBonusClueRevealKey(arcId!, signal, isWorldEvent: false);
                    if (!countedVisibleBonusClueRevealKeys.Add(revealKey))
                        continue;

                    var usageKey = $"{sponsorGuardianId}::{sourceProjectId}";
                    visibleBonusClueUsage[usageKey] = visibleBonusClueUsage.GetValueOrDefault(usageKey) + clueCost;
                }
            }

            index++;
        }

        var requiresCurrentWorldEventsForHostileClueContract = hostileClueContractArcs.Any(context => HostileDirectTargetRivalArcMayNeedWorldEvents(context.Arc));
        var requiresCurrentWorldEventsForRelatedRivalWorldValidation =
            !string.IsNullOrWhiteSpace(worldEventsJson) &&
            worldEventsJson.Contains("\"relatedRivalArcId\"", StringComparison.OrdinalIgnoreCase);
        var requiresCurrentWorldEventsForBonusClueValidation = false;
        if (knownArcSponsorGuardianIds.Count > 0 &&
            JsonNode.Parse(rivalState.Collection.GetRawText()) is JsonArray currentBonusClueArcs)
            {
                bonusClueTrackerAuthorityResolutionAttempted = true;
                if (TryResolveGuardianProjectTrackerValidationRoot(
                        $"{RivalSoulArcService.StatePath}.{rivalState.CollectionName}",
                        "Rival arc bonus clue validation требует readable current guardian project tracker authority и не использует isolated pre-turn tracker baseline как authority fallback.",
                        "rival_arc_bonus_clue_missing_current_tracker_authority",
                        "RivalSoulArcs",
                        $"Исправь current {GuardianProjectState.TrackerPath} и validated tracker baseline так, чтобы validator построил guardian-backed current tracker authority перед validating lore_research-derived bonus clues.",
                        issues,
                        out bonusClueTrackerAuthorityRoot,
                        out var trackerContext))
                {
                    hasBonusClueTrackerAuthority = true;
                    bonusClueTrackerAuthorityContext = trackerContext;

                    if (TryParseJsonObject(bonusClueTrackerAuthorityRoot) is JsonObject bonusClueTrackerRootObject)
                    {
                        var currentBonusClueArcsRoot = new JsonObject { ["arcs"] = currentBonusClueArcs };
                        var requiresCurrentIncarnationForBonusClueValidation =
                            CanonicalStateNormalizer.RequiresCurrentIncarnationForVisibleRivalCluePreflight(
                                currentBonusClueArcsRoot,
                                bonusClueTrackerRootObject,
                                hasCurrentWorldEventsFile,
                                worldEventsJson);
                        if (requiresCurrentIncarnationForBonusClueValidation)
                        {
                            bonusClueCurrentIncarnationResolutionAttempted = true;
                            hasBonusClueCurrentIncarnation = TryResolveVisibleRivalClueCurrentIncarnation(
                                trackerContext,
                                $"{RivalSoulArcService.StatePath}.{rivalState.CollectionName}",
                                issues,
                                out bonusClueCurrentIncarnation);
                            if (hasBonusClueCurrentIncarnation)
                            {
                                requiresCurrentWorldEventsForBonusClueValidation =
                                    CanonicalStateNormalizer.RequiresCurrentWorldEventsForVisibleRivalClueConsumption(
                                        currentBonusClueArcsRoot,
                                        bonusClueTrackerRootObject,
                                        bonusClueCurrentIncarnation,
                                        worldEventsJson);
                            }
                        }
                    }
                }
            }

            var canValidateHostileDirectTargetClueContract = true;

            if (requiresCurrentWorldEventsForHostileClueContract ||
                requiresCurrentWorldEventsForRelatedRivalWorldValidation ||
                requiresCurrentWorldEventsForBonusClueValidation)
            {
                var (document, collection, parsedWorldEventCollectionName, hasInvalidCurrentState, _) =
                    await TryReadCurrentWorldEventValidationDocumentAsync(
                        issues,
                        requiresCurrentWorldEventsForBonusClueValidation,
                        requiresCurrentWorldEventsForHostileClueContract || requiresCurrentWorldEventsForRelatedRivalWorldValidation,
                        treatMissingCurrentStateAsInvalid: true);
                worldEventsDoc = document;
                worldEvents = collection;
                worldEventCollectionName = parsedWorldEventCollectionName;

                if (hasInvalidCurrentState && requiresCurrentWorldEventsForHostileClueContract)
                    canValidateHostileDirectTargetClueContract = false;

                if (worldEventCollectionName != null && worldEvents.ValueKind == JsonValueKind.Array)
                    relatedWorldEventsByArcId = BuildRelatedWorldEventsByArcId(worldEvents);
            }

            if (canValidateHostileDirectTargetClueContract)
            {
                foreach (var (arc, arcContext) in hostileClueContractArcs)
                    ValidateHostileDirectTargetRivalArcClueContract(arc, arcContext, issues, relatedWorldEventsByArcId);
            }

            var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
            var knownGuardianAbodes = BuildGuardianAbodeMap(guardianPolicyContext);

            var knownResidentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var residentLinkedQuestIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var residentGrantedRelicIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var (residentDoc, hasInvalidResidentValidationState, isMissingResidentValidationState) =
                await TryReadCurrentResidentValidationDocumentAsync(issues);
            using var residentDocScope = residentDoc;
            if (residentDoc is not null)
            {
                foreach (var resident in residentDoc.RootElement.ValueKind == JsonValueKind.Object
                             ? residentDoc.RootElement.TryGetProperty(GuardianAbodeResidentState.EntriesProperty, out var entries) && entries.ValueKind == JsonValueKind.Array
                                 ? entries.EnumerateArray()
                                 : Enumerable.Empty<JsonElement>()
                             : Enumerable.Empty<JsonElement>())
                {
                    var residentId = GetFirstNonEmptyString(resident, "residentId");
                    var residentGuardianId = GetFirstNonEmptyString(resident, "guardianId");
                    var residentAbodeId = GetFirstNonEmptyString(resident, "abodeId");
                    if (!string.IsNullOrWhiteSpace(residentId))
                    {
                        knownResidentIds.Add(residentId);
                        var linkedSoulQuestId = GetFirstNonEmptyString(resident, "linkedSoulQuestId");
                        if (!string.IsNullOrWhiteSpace(linkedSoulQuestId))
                            residentLinkedQuestIds[residentId] = linkedSoulQuestId;

                        var grantedRelicId = GetFirstNonEmptyString(resident, "grantedRelicId");
                        if (!string.IsNullOrWhiteSpace(grantedRelicId))
                            residentGrantedRelicIds[residentId] = grantedRelicId;
                    }

                    if (!string.IsNullOrWhiteSpace(residentGuardianId) && !knownGuardianAbodes.ContainsKey(residentGuardianId))
                    {
                        issues.Add(new ValidationIssue(
                            $"{GuardianAbodeResidentState.StatePath}.entries[{residentId}].guardianId",
                            IssueSeverity.Error,
                            $"Afterlife resident ссылается на неизвестного guardian '{residentGuardianId}'",
                            code: "guardian_abode_resident_unknown_guardian_id",
                            section: "AfterlifeResidents",
                            expected: "existing guardianId from guardians.json",
                            actual: residentGuardianId,
                            repairHint: "Для resident.guardianId используй существующий guardianId из game_state/meta/guardians.json."));
                    }
                    else if (!string.IsNullOrWhiteSpace(residentGuardianId) &&
                             knownGuardianAbodes.TryGetValue(residentGuardianId, out var expectedAbodeId) &&
                             !string.IsNullOrWhiteSpace(expectedAbodeId) &&
                             !string.Equals(expectedAbodeId, residentAbodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{GuardianAbodeResidentState.StatePath}.entries[{residentId}].abodeId",
                            IssueSeverity.Error,
                            "resident.abodeId должен совпадать с canonical abodeId этого guardian",
                            code: "guardian_abode_resident_abode_mismatch",
                            section: "AfterlifeResidents",
                            expected: expectedAbodeId,
                            actual: residentAbodeId,
                            repairHint: "Синхронизируй resident.abodeId с guardian.abode.abodeId из canonical guardians state."));
                    }
                }

                if (residentDoc.RootElement.ValueKind == JsonValueKind.Object &&
                    residentDoc.RootElement.TryGetProperty(GuardianAbodeResidentState.RosterReceiptsProperty, out var rosterReceipts) &&
                    rosterReceipts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var receipt in rosterReceipts.EnumerateArray())
                    {
                        var guardianId = GetFirstNonEmptyString(receipt, "guardianId");
                        var abodeId = GetFirstNonEmptyString(receipt, "abodeId");
                        var requestId = GetFirstNonEmptyString(receipt, "requestId");
                        if (!string.IsNullOrWhiteSpace(guardianId) && !knownGuardianAbodes.ContainsKey(guardianId))
                        {
                            issues.Add(new ValidationIssue(
                                $"{GuardianAbodeResidentState.StatePath}.rosterReceipts[{requestId}].guardianId",
                                IssueSeverity.Error,
                                $"Resident roster receipt ссылается на неизвестного guardian '{guardianId}'",
                                code: "guardian_abode_resident_roster_receipt_unknown_guardian_id",
                                section: "AfterlifeResidents",
                                expected: "existing guardianId from guardians.json",
                                actual: guardianId,
                                repairHint: "Для rosterReceipts.guardianId используй существующий guardianId из game_state/meta/guardians.json."));
                        }
                        else if (!string.IsNullOrWhiteSpace(guardianId) &&
                                 knownGuardianAbodes.TryGetValue(guardianId, out var expectedAbodeId) &&
                                 !string.IsNullOrWhiteSpace(expectedAbodeId) &&
                                 !string.Equals(expectedAbodeId, abodeId, StringComparison.OrdinalIgnoreCase))
                        {
                            issues.Add(new ValidationIssue(
                                $"{GuardianAbodeResidentState.StatePath}.rosterReceipts[{requestId}].abodeId",
                                IssueSeverity.Error,
                                "resident roster receipt должен ссылаться на canonical abodeId этого guardian",
                                code: "guardian_abode_resident_roster_receipt_abode_mismatch",
                                section: "AfterlifeResidents",
                                expected: expectedAbodeId,
                                actual: abodeId,
                                repairHint: "Синхронизируй rosterReceipts.abodeId с guardian.abode.abodeId из canonical guardians state."));
                        }
                    }
                }

                if (residentDoc.RootElement.ValueKind == JsonValueKind.Object &&
                    residentDoc.RootElement.TryGetProperty(GuardianAbodeResidentState.InteractionReceiptsProperty, out var interactionReceipts) &&
                    interactionReceipts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var receipt in interactionReceipts.EnumerateArray())
                    {
                        var residentId = GetFirstNonEmptyString(receipt, "residentId");
                        if (string.IsNullOrWhiteSpace(residentId) || knownResidentIds.Contains(residentId))
                            continue;

                        issues.Add(new ValidationIssue(
                            $"{GuardianAbodeResidentState.StatePath}.interactionReceipts[{residentId}].residentId",
                            IssueSeverity.Error,
                            $"Resident interaction receipt ссылается на неизвестного resident '{residentId}'",
                            code: "guardian_abode_resident_receipt_unknown_resident_id",
                            section: "AfterlifeResidents",
                            expected: $"existing residentId from {GuardianAbodeResidentState.StatePath}",
                            actual: residentId,
                            repairHint: "Для interactionReceipts.residentId используй существующий residentId из guardian_abode_residents.json."));
                    }
                }

                if (residentDoc.RootElement.ValueKind == JsonValueKind.Object &&
                    residentDoc.RootElement.TryGetProperty(GuardianAbodeResidentState.HistoryLogProperty, out var historyLog) &&
                    historyLog.ValueKind == JsonValueKind.Array)
                {
                    foreach (var historyEntry in historyLog.EnumerateArray())
                    {
                        var residentId = GetFirstNonEmptyString(historyEntry, "residentId");
                        if (string.IsNullOrWhiteSpace(residentId) || knownResidentIds.Contains(residentId))
                            continue;

                        issues.Add(new ValidationIssue(
                            $"{GuardianAbodeResidentState.StatePath}.historyLog[{residentId}].residentId",
                            IssueSeverity.Error,
                            $"Resident historyLog entry ссылается на неизвестного resident '{residentId}'",
                            code: "guardian_abode_resident_history_unknown_resident_id",
                            section: "AfterlifeResidents",
                            expected: $"existing residentId from {GuardianAbodeResidentState.StatePath}",
                            actual: residentId,
                            repairHint: "Для historyLog.residentId используй существующий residentId из guardian_abode_residents.json."));
                    }
                }
            }

            var manifestedCompanionSourceRelicIds =
                await CollectManifestedCompanionSourceRelicValidationSurfaceAsync(issues);
            var hasCurrentReverseSoulRelicResidentValidationDependency =
                await HasReverseSoulRelicResidentValidationDependencyAsync(includeValidatedPreTurnFallback: false);
            var hasReverseSoulRelicResidentValidationDependency =
                await HasReverseSoulRelicResidentValidationDependencyAsync();
            var (soulQuestDoc, hasInvalidSoulQuestValidationState, isMissingSoulQuestValidationState) =
                await TryReadCurrentSoulQuestValidationDocumentAsync(issues);
            using var soulQuestDocScope = soulQuestDoc;
            var hasNonParticipatingCurrentSoulQuestState =
                soulQuestDoc is not null &&
                !TryGetSoulQuestCollection(soulQuestDoc.RootElement, out _);
            var hasResidentLinkedSoulQuestValidationDependency =
                residentLinkedQuestIds.Count > 0 ||
                SoulQuestDocumentHasResidentLinkedValidationSurface(soulQuestDoc);
            var hasQuestOwnedSoulQuestValidationDependency =
                await HasQuestOwnedSoulQuestValidationDependencyAsync(soulQuestDoc);
            if (isMissingResidentValidationState &&
                (hasCurrentReverseSoulRelicResidentValidationDependency || hasResidentLinkedSoulQuestValidationDependency))
            {
                AddMissingCurrentResidentValidationIssue(issues);
                hasInvalidResidentValidationState = true;
            }

            if (isMissingSoulQuestValidationState &&
                (residentLinkedQuestIds.Count > 0 || hasQuestOwnedSoulQuestValidationDependency))
            {
                AddMissingCurrentSoulQuestValidationIssue(issues);
                hasInvalidSoulQuestValidationState = true;
            }
            else if (!hasInvalidSoulQuestValidationState &&
                     hasNonParticipatingCurrentSoulQuestState &&
                     (residentLinkedQuestIds.Count > 0 || hasQuestOwnedSoulQuestValidationDependency))
            {
                AddNonParticipatingCurrentSoulQuestValidationIssue(issues);
                hasInvalidSoulQuestValidationState = true;
            }

            var canValidateResidentLinkedCrossReferences =
                residentDoc is not null && !hasInvalidResidentValidationState;
            var hasSoulRelicResidentValidationDependency =
                residentGrantedRelicIds.Count > 0 ||
                manifestedCompanionSourceRelicIds.Count > 0 ||
                (canValidateResidentLinkedCrossReferences && hasReverseSoulRelicResidentValidationDependency);
            var hasReadableSoulRelicValidationState = false;
            var knownSoulRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var relicSourceResidentIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (hasSoulRelicResidentValidationDependency)
            {
                (hasReadableSoulRelicValidationState, knownSoulRelicIds, relicSourceResidentIds) =
                    await ReadCurrentSoulRelicResidentValidationStateAsync(issues);
            }

            if (hasReadableSoulRelicValidationState)
            {
                ValidateManifestedCompanionSourceRelicIds(
                    manifestedCompanionSourceRelicIds,
                    knownSoulRelicIds,
                    issues);
            }
            if (soulQuestDoc is not null)
            {
                var knownSoulQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var questResidentLinks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                JsonElement quests;
                string? questCollectionName = null;
                if (soulQuestDoc.RootElement.TryGetProperty("quests", out quests))
                {
                    questCollectionName = "quests";
                }
                else if (soulQuestDoc.RootElement.TryGetProperty("UpdateSoulQuests", out quests))
                {
                    questCollectionName = "UpdateSoulQuests";
                }
                else
                {
                    quests = default;
                }

                if (questCollectionName != null && quests.ValueKind == JsonValueKind.Array)
                {
                    var questIndex = 0;
                    foreach (var quest in quests.EnumerateArray())
                    {
                        var questId = GetFirstNonEmptyString(quest, "questId", "id");
                        if (!string.IsNullOrWhiteSpace(questId))
                            knownSoulQuestIds.Add(questId);

                        var relatedArcId = GetFirstNonEmptyString(quest, "relatedRivalArcId");
                        if (!string.IsNullOrWhiteSpace(relatedArcId) && !knownArcIds.Contains(relatedArcId))
                        {
                            issues.Add(new ValidationIssue(
                                $"game_state/quests/soul_quests.json.{questCollectionName}[{questIndex}].relatedRivalArcId",
                                IssueSeverity.Error,
                                $"Soul quest ссылается на неизвестный rival arc '{relatedArcId}'",
                                code: "soul_quest_unknown_rival_arc_id",
                                section: "RivalSoulArcs",
                                expected: "existing arcId from canonical rival_soul_arcs state",
                                actual: relatedArcId,
                                repairHint: "Для relatedRivalArcId используй существующий arcId из game_state/world/rival_soul_arcs.json."));
                        }

                        if (canValidateResidentLinkedCrossReferences && !hasInvalidSoulQuestValidationState)
                        {
                            var relatedResidentId = GetFirstNonEmptyString(quest, "relatedAfterlifeResidentId");
                            if (!string.IsNullOrWhiteSpace(relatedResidentId) && !knownResidentIds.Contains(relatedResidentId))
                            {
                                issues.Add(new ValidationIssue(
                                    $"game_state/quests/soul_quests.json.{questCollectionName}[{questIndex}].relatedAfterlifeResidentId",
                                    IssueSeverity.Error,
                                    $"Soul quest ссылается на неизвестного afterlife resident '{relatedResidentId}'",
                                    code: "soul_quest_unknown_afterlife_resident_id",
                                    section: "SoulQuests",
                                    expected: $"existing residentId from {GuardianAbodeResidentState.StatePath}",
                                    actual: relatedResidentId,
                                    repairHint: $"Для relatedAfterlifeResidentId используй residentId из {GuardianAbodeResidentState.StatePath}."));
                            }
                            else if (!string.IsNullOrWhiteSpace(relatedResidentId) && !string.IsNullOrWhiteSpace(questId))
                            {
                                questResidentLinks[relatedResidentId] = questId;
                            }
                        }

                        questIndex++;
                    }

                    if (canValidateResidentLinkedCrossReferences && !hasInvalidSoulQuestValidationState)
                    {
                        foreach (var residentQuestLink in residentLinkedQuestIds)
                        {
                            if (!knownSoulQuestIds.Contains(residentQuestLink.Value))
                            {
                                issues.Add(new ValidationIssue(
                                    $"{GuardianAbodeResidentState.StatePath}.entries[{residentQuestLink.Key}].linkedSoulQuestId",
                                    IssueSeverity.Error,
                                    $"Afterlife resident ссылается на неизвестный soul quest '{residentQuestLink.Value}'",
                                    code: "guardian_abode_resident_unknown_linked_soul_quest_id",
                                    section: "SoulQuests",
                                    expected: "existing questId from game_state/quests/soul_quests.json",
                                    actual: residentQuestLink.Value,
                                    repairHint: "Если resident хранит linkedSoulQuestId, используй существующий questId из canonical soul_quests state."));
                                continue;
                            }

                            if (!questResidentLinks.TryGetValue(residentQuestLink.Key, out var relatedQuestId) ||
                                !string.Equals(relatedQuestId, residentQuestLink.Value, StringComparison.OrdinalIgnoreCase))
                            {
                                issues.Add(new ValidationIssue(
                                    $"{GuardianAbodeResidentState.StatePath}.entries[{residentQuestLink.Key}].linkedSoulQuestId",
                                    IssueSeverity.Error,
                                    "linkedSoulQuestId должен указывать на soul quest, который ссылается обратно на этого resident через relatedAfterlifeResidentId",
                                    code: "guardian_abode_resident_linked_soul_quest_mismatch",
                                    section: "SoulQuests",
                                    expected: $"soul quest with relatedAfterlifeResidentId={residentQuestLink.Key}",
                                    actual: residentQuestLink.Value,
                                    repairHint: "Синхронизируй resident.linkedSoulQuestId и soul quest.relatedAfterlifeResidentId."));
                            }
                        }
                    }
                }
            }

            if (hasReadableSoulRelicValidationState)
            {
                foreach (var residentRelicLink in residentGrantedRelicIds)
                {
                    if (!knownSoulRelicIds.Contains(residentRelicLink.Value))
                    {
                        issues.Add(new ValidationIssue(
                            $"{GuardianAbodeResidentState.StatePath}.entries[{residentRelicLink.Key}].grantedRelicId",
                            IssueSeverity.Error,
                            $"Afterlife resident ссылается на неизвестную soul relic '{residentRelicLink.Value}'",
                            code: "guardian_abode_resident_unknown_granted_relic_id",
                            section: "AfterlifeResidents",
                            expected: "existing relicId from soul_state.json",
                            actual: residentRelicLink.Value,
                            repairHint: "Если resident хранит grantedRelicId, соответствующая soul relic должна существовать в soul_state.json."));
                        continue;
                    }

                    if (relicSourceResidentIds.TryGetValue(residentRelicLink.Value, out var sourceResidentId) &&
                        !string.Equals(sourceResidentId, residentRelicLink.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{GuardianAbodeResidentState.StatePath}.entries[{residentRelicLink.Key}].grantedRelicId",
                            IssueSeverity.Error,
                            "grantedRelicId указывает на реликвию, привязанную к другому resident",
                            code: "guardian_abode_resident_granted_relic_resident_mismatch",
                            section: "AfterlifeResidents",
                            expected: residentRelicLink.Key,
                            actual: sourceResidentId,
                            repairHint: "Синхронизируй resident.grantedRelicId с sourceResidentId внутри companionSeed реликвии связи."));
                    }
                }

                if (canValidateResidentLinkedCrossReferences)
                {
                    foreach (var relicResidentLink in relicSourceResidentIds)
                    {
                        if (knownResidentIds.Contains(relicResidentLink.Value))
                            continue;

                        issues.Add(new ValidationIssue(
                            $"game_state/meta/soul_state.json.soulRelics[{relicResidentLink.Key}].companionSeed.sourceResidentId",
                            IssueSeverity.Error,
                            $"Soul Relic ссылается на неизвестного afterlife resident '{relicResidentLink.Value}'",
                            code: "companion_echo_unknown_source_resident_id",
                            section: "AfterlifeResidents",
                            expected: $"existing residentId from {GuardianAbodeResidentState.StatePath}",
                            actual: relicResidentLink.Value,
                            repairHint: "Для companionSeed.sourceResidentId используй существующий residentId из guardian_abode_residents.json."));
                    }
                }
            }

            if (worldEventCollectionName != null && worldEvents.ValueKind == JsonValueKind.Array)
            {
                var eventIndex = 0;
                foreach (var worldEvent in worldEvents.EnumerateArray())
                {
                    var eventContext = $"game_state/world/world_events.json.{worldEventCollectionName}[{eventIndex}]";
                    ValidateOptionalString(worldEvent, eventContext, issues, "bonusClueSourceProjectId");
                    ValidateOptionalString(worldEvent, eventContext, issues, "bonusClueRevealId");
                    if (worldEvent.TryGetProperty("bonusClueCost", out _))
                        ValidateNonNegativeIntegerField(worldEvent, eventContext, issues, "bonusClueCost", "RivalSoulArcs");

                    var relatedArcId = GetFirstNonEmptyString(worldEvent, "relatedRivalArcId");
                    ValidateWorldEventRivalArcLink(
                        worldEventCollectionName,
                        eventIndex,
                        worldEvent,
                        knownArcIds,
                        issues);

                    var sourceProjectId = GetFirstNonEmptyString(worldEvent, "bonusClueSourceProjectId");
                    if (!string.IsNullOrWhiteSpace(sourceProjectId))
                    {
                        var revealId = GetFirstNonEmptyString(worldEvent, "bonusClueRevealId");
                        if (string.IsNullOrWhiteSpace(revealId))
                        {
                            issues.Add(new ValidationIssue(
                                $"{eventContext}.bonusClueRevealId",
                                IssueSeverity.Error,
                                "world event lore_research bonus clue должен иметь bonusClueRevealId для cross-surface dedupe",
                                code: "world_event_bonus_clue_missing_reveal_id",
                                section: "RivalSoulArcs",
                                repairHint: "Для linked world event clue с bonusClueSourceProjectId всегда передавай stable bonusClueRevealId. Если тот же clue mirrored в publicSignals, используй тот же reveal id."));
                        }

                        if (string.IsNullOrWhiteSpace(relatedArcId))
                        {
                            issues.Add(new ValidationIssue(
                                $"{eventContext}.relatedRivalArcId",
                                IssueSeverity.Error,
                                "world event lore_research bonus clue требует relatedRivalArcId",
                                code: "world_event_bonus_clue_missing_related_arc",
                                section: "RivalSoulArcs",
                                repairHint: "Если world event тратит lore_research bonus clue, привяжи его к существующему rival arc через relatedRivalArcId."));
                        }
                        else if (knownArcSponsorGuardianIds.TryGetValue(relatedArcId, out var sponsorGuardianId))
                        {
                            if (IsPlayerVisibleRivalWorldEvent(worldEvent))
                            {
                                var clueCost = TryReadIntField(worldEvent, "bonusClueCost", out var parsedCost) ? Math.Max(1, parsedCost) : 1;
                                var revealKey = BuildVisibleBonusClueRevealKey(relatedArcId, worldEvent, isWorldEvent: true);
                                if (countedVisibleBonusClueRevealKeys.Add(revealKey))
                                {
                                    var usageKey = $"{sponsorGuardianId}::{sourceProjectId}";
                                    visibleBonusClueUsage[usageKey] = visibleBonusClueUsage.GetValueOrDefault(usageKey) + clueCost;
                                }
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(relatedArcId))
                        {
                            issues.Add(new ValidationIssue(
                                $"{eventContext}.bonusClueSourceProjectId",
                                IssueSeverity.Error,
                                "Bonus clue от lore_research допустим только для world event, связанного с rival arc со sponsorGuardianRef.mode=guardianId",
                                code: "world_event_bonus_clue_requires_guardian_sponsor",
                                section: "RivalSoulArcs",
                                repairHint: "Используй bonusClueSourceProjectId только там, где relatedRivalArcId указывает на rival arc со sponsorGuardianRef.mode=guardianId."));
                        }
                    }

                    eventIndex++;
                }
            }

            if (visibleBonusClueUsage.Count > 0)
            {
                JsonElement trackerRoot;
                GuardianProjectTrackerPolicyContext trackerContext;
                if (hasBonusClueTrackerAuthority)
                {
                    trackerRoot = bonusClueTrackerAuthorityRoot;
                    trackerContext = bonusClueTrackerAuthorityContext!;
                }
                else if (bonusClueTrackerAuthorityResolutionAttempted)
                {
                }
                else if (!TryResolveGuardianProjectTrackerValidationRoot(
                             $"{RivalSoulArcService.StatePath}.{rivalState.CollectionName}",
                             "Rival arc bonus clue validation требует readable current guardian project tracker authority и не использует isolated pre-turn tracker baseline как authority fallback.",
                             "rival_arc_bonus_clue_missing_current_tracker_authority",
                             "RivalSoulArcs",
                             $"Исправь current {GuardianProjectState.TrackerPath} и validated tracker baseline так, чтобы validator построил guardian-backed current tracker authority перед validating lore_research-derived bonus clues.",
                             issues,
                             out trackerRoot,
                             out trackerContext))
                {
                }
                else
                {
                    var canValidateVisibleBonusClues = false;
                    var currentIncarnation = 0;
                    if (hasBonusClueCurrentIncarnation)
                    {
                        currentIncarnation = bonusClueCurrentIncarnation;
                        canValidateVisibleBonusClues = true;
                    }
                    else if (bonusClueCurrentIncarnationResolutionAttempted)
                    {
                    }
                    else if (!TryResolveVisibleRivalClueCurrentIncarnation(
                                 trackerContext,
                                 $"{RivalSoulArcService.StatePath}.{rivalState.CollectionName}",
                                 issues,
                                 out currentIncarnation))
                    {
                    }

                    else
                    {
                        canValidateVisibleBonusClues = true;
                    }

                    if (canValidateVisibleBonusClues)
                    {
                        foreach (var usage in visibleBonusClueUsage)
                        {
                            var parts = usage.Key.Split(new[] { "::" }, 2, StringSplitOptions.None);
                            if (parts.Length != 2)
                                continue;

                            var clueBudget = ReadGrantedLoreResearchVisibleClueBudget(trackerRoot, parts[0], parts[1], currentIncarnation);
                            if (!clueBudget.HasProject)
                            {
                                issues.Add(new ValidationIssue(
                                    $"{RivalSoulArcService.StatePath}.{rivalState.CollectionName}",
                                    IssueSeverity.Error,
                                    $"Rival arc signals используют bonus clue sourceProjectId '{parts[1]}', но у guardian '{parts[0]}' нет completed lore_research с clue budget",
                                    code: "rival_arc_bonus_clue_unknown_source_project",
                                    section: "RivalSoulArcs",
                                    repairHint: "Для bonusClueSourceProjectId используй completed lore_research projectId того же guardian sponsor-а."));
                                continue;
                            }

                            if (!clueBudget.IsCurrentLifeApplicable)
                            {
                                issues.Add(new ValidationIssue(
                                    $"{RivalSoulArcService.StatePath}.{rivalState.CollectionName}",
                                    IssueSeverity.Error,
                                    $"Rival arc signals используют bonus clue sourceProjectId '{parts[1]}', но его lore_research budget не активен в текущей инкарнации",
                                    code: "rival_arc_bonus_clue_inactive_source_project",
                                    section: "RivalSoulArcs",
                                    repairHint: "Используй lore_research projectId, чей targetIncarnation совпадает с текущей жизнью, либо перенеси bonusClueSourceProjectId на ту инкарнацию, где проект активен."));
                                continue;
                            }

                            if (clueBudget.GrantedBudget <= 0)
                            {
                                issues.Add(new ValidationIssue(
                                    $"{RivalSoulArcService.StatePath}.{rivalState.CollectionName}",
                                    IssueSeverity.Error,
                                    $"Rival arc signals используют bonus clue sourceProjectId '{parts[1]}', но у guardian '{parts[0]}' нет completed lore_research с clue budget",
                                    code: "rival_arc_bonus_clue_unknown_source_project",
                                    section: "RivalSoulArcs",
                                    repairHint: "Для bonusClueSourceProjectId используй completed lore_research projectId того же guardian sponsor-а."));
                                continue;
                            }

                            if (usage.Value > clueBudget.GrantedBudget)
                            {
                                issues.Add(new ValidationIssue(
                                    $"{RivalSoulArcService.StatePath}.{rivalState.CollectionName}",
                                    IssueSeverity.Error,
                                    "Rival arc bonus clue usage превышает granted lore_research visible clue budget",
                                    code: "rival_arc_bonus_clue_budget_exceeded",
                                    section: "RivalSoulArcs",
                                    expected: $"<= {clueBudget.GrantedBudget}",
                                    actual: usage.Value.ToString(),
                                    repairHint: "Не раскрывай через bonusClueSourceProjectId больше player-visible extra clues, чем даёт completed lore_research project."));
                            }
                        }
                    }
                }
            }

            worldEventsDoc?.Dispose();
    }


    private static string BuildVisibleBonusClueRevealKey(string arcId, JsonElement source, bool isWorldEvent)
    {
        var revealId = GetFirstNonEmptyString(source, "bonusClueRevealId");
        if (!string.IsNullOrWhiteSpace(revealId))
            return $"{arcId}::reveal::{revealId}";

        if (!isWorldEvent)
        {
            var signalId = GetFirstNonEmptyString(source, "signalId");
            if (!string.IsNullOrWhiteSpace(signalId))
                return $"{arcId}::signal::{signalId}";

            return $"{arcId}::signal::{GetIntOrDefault(source, "stage")}::{GetFirstNonEmptyString(source, "source")}::{GetFirstNonEmptyString(source, "description")}";
        }

        var worldEventId = GetFirstNonEmptyString(source, "eventId");
        if (!string.IsNullOrWhiteSpace(worldEventId))
            return $"{arcId}::world_event::{worldEventId}";

        return $"{arcId}::world_event::{GetFirstNonEmptyString(source, "eventTitle", "title", "name")}::{GetFirstNonEmptyString(source, "summary", "description")}";
    }


    private static Dictionary<string, List<JsonElement>> BuildRelatedWorldEventsByArcId(JsonElement worldEvents)
    {
        var result = new Dictionary<string, List<JsonElement>>(StringComparer.OrdinalIgnoreCase);
        if (worldEvents.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var worldEvent in worldEvents.EnumerateArray())
        {
            if (worldEvent.ValueKind != JsonValueKind.Object)
                continue;

            var relatedArcId = GetFirstNonEmptyString(worldEvent, "relatedRivalArcId");
            if (string.IsNullOrWhiteSpace(relatedArcId))
                continue;

            if (!result.TryGetValue(relatedArcId, out var entries))
            {
                entries = new List<JsonElement>();
                result[relatedArcId] = entries;
            }

            entries.Add(worldEvent);
        }

        return result;
    }


    private static bool IsPlayerVisibleRivalWorldEvent(JsonElement worldEvent)
    {
        if (worldEvent.ValueKind != JsonValueKind.Object)
            return false;

        var visibility = GetFirstNonEmptyString(worldEvent, "visibility");
        return string.Equals(visibility, "Public", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "Regional", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "player_known", StringComparison.OrdinalIgnoreCase);
    }


    private static bool IsRecognizedRivalWorldEventVisibility(string? visibility)
    {
        return string.Equals(visibility, "Public", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "Regional", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "Secret", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "Faction-Internal", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "player_known", StringComparison.OrdinalIgnoreCase);
    }


    private static int CountPlayerVisibleRivalClues(
        JsonElement arc,
        string arcId,
        IReadOnlyDictionary<string, List<JsonElement>> relatedWorldEventsByArcId)
    {
        var clueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (arc.TryGetProperty("publicSignals", out var publicSignals) &&
            publicSignals.ValueKind == JsonValueKind.Array)
        {
            foreach (var signal in publicSignals.EnumerateArray())
            {
                if (signal.ValueKind != JsonValueKind.Object ||
                    !signal.TryGetProperty("visibleToPlayer", out var visibleNode) ||
                    visibleNode.ValueKind != JsonValueKind.True)
                {
                    continue;
                }

                clueKeys.Add(BuildVisibleBonusClueRevealKey(arcId, signal, isWorldEvent: false));
            }
        }

        if (!string.IsNullOrWhiteSpace(arcId) &&
            relatedWorldEventsByArcId.TryGetValue(arcId, out var relatedWorldEvents))
        {
            foreach (var worldEvent in relatedWorldEvents)
            {
                if (!IsPlayerVisibleRivalWorldEvent(worldEvent))
                    continue;

                clueKeys.Add(BuildVisibleBonusClueRevealKey(arcId, worldEvent, isWorldEvent: true));
            }
        }

        return clueKeys.Count;
    }

    private static bool HostileDirectTargetRivalArcMayNeedWorldEvents(JsonElement arc)
    {
        if (arc.ValueKind != JsonValueKind.Object ||
            !arc.TryGetProperty("playerIntersection", out var playerIntersection) ||
            playerIntersection.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var targetsPlayerDirectly =
            playerIntersection.TryGetProperty("targetsPlayerDirectly", out var targetsNode) &&
            targetsNode.ValueKind == JsonValueKind.True;
        var arcType = GetFirstNonEmptyString(arc, "arcType");
        var status = GetFirstNonEmptyString(arc, "status");
        if (!targetsPlayerDirectly ||
            !string.Equals(arcType, "hostile_hunt", StringComparison.OrdinalIgnoreCase) ||
            (!string.Equals(status, "intersecting", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(status, "resolved", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return CountPlayerVisiblePublicRivalClues(arc, GetFirstNonEmptyString(arc, "arcId") ?? string.Empty) < 2;
    }

    private static int CountPlayerVisiblePublicRivalClues(JsonElement arc, string arcId)
    {
        var clueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (arc.TryGetProperty("publicSignals", out var publicSignals) &&
            publicSignals.ValueKind == JsonValueKind.Array)
        {
            foreach (var signal in publicSignals.EnumerateArray())
            {
                if (signal.ValueKind != JsonValueKind.Object ||
                    !signal.TryGetProperty("visibleToPlayer", out var visibleNode) ||
                    visibleNode.ValueKind != JsonValueKind.True)
                {
                    continue;
                }

                clueKeys.Add(BuildVisibleBonusClueRevealKey(arcId, signal, isWorldEvent: false));
            }
        }

        return clueKeys.Count;
    }

    private async Task ValidateResidentCrossReferencesWhenRivalArcPassSkippedAsync(List<ValidationIssue> issues)
    {
        var rivalState = await TryReadCurrentRivalValidationDocumentAsync();
        using var rivalDocScope = rivalState.Document;
        if (rivalState.Kind == CurrentOwnerStateReadKind.ReadableWithArrayCollection)
            return;

        var canUseSkippedRivalArcReferenceFallback =
            rivalState.Kind == CurrentOwnerStateReadKind.MissingOrWhitespace ||
            rivalState.Kind == CurrentOwnerStateReadKind.ReadableButNoRelevantCollection;

        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        var knownGuardianAbodes = BuildGuardianAbodeMap(guardianPolicyContext);

        var knownResidentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var residentLinkedQuestIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var residentGrantedRelicIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var (residentDoc, hasInvalidResidentValidationState, isMissingResidentValidationState) =
            await TryReadCurrentResidentValidationDocumentAsync(issues);
        using var residentDocScope = residentDoc;
        if (residentDoc is not null)
        {
            if (residentDoc.RootElement.ValueKind == JsonValueKind.Object &&
                residentDoc.RootElement.TryGetProperty(GuardianAbodeResidentState.EntriesProperty, out var entries) &&
                entries.ValueKind == JsonValueKind.Array)
            {
                foreach (var resident in entries.EnumerateArray())
                {
                    var residentId = GetFirstNonEmptyString(resident, "residentId");
                    var residentGuardianId = GetFirstNonEmptyString(resident, "guardianId");
                    var residentAbodeId = GetFirstNonEmptyString(resident, "abodeId");
                    if (!string.IsNullOrWhiteSpace(residentId))
                    {
                        knownResidentIds.Add(residentId);
                        var linkedSoulQuestId = GetFirstNonEmptyString(resident, "linkedSoulQuestId");
                        if (!string.IsNullOrWhiteSpace(linkedSoulQuestId))
                            residentLinkedQuestIds[residentId] = linkedSoulQuestId;

                        var grantedRelicId = GetFirstNonEmptyString(resident, "grantedRelicId");
                        if (!string.IsNullOrWhiteSpace(grantedRelicId))
                            residentGrantedRelicIds[residentId] = grantedRelicId;
                    }

                    if (!string.IsNullOrWhiteSpace(residentGuardianId) && !knownGuardianAbodes.ContainsKey(residentGuardianId))
                    {
                        issues.Add(new ValidationIssue(
                            $"{GuardianAbodeResidentState.StatePath}.entries[{residentId}].guardianId",
                            IssueSeverity.Error,
                            $"Afterlife resident ссылается на неизвестного guardian '{residentGuardianId}'",
                            code: "guardian_abode_resident_unknown_guardian_id",
                            section: "AfterlifeResidents",
                            expected: "existing guardianId from guardians.json",
                            actual: residentGuardianId,
                            repairHint: "Для resident.guardianId используй существующий guardianId из game_state/meta/guardians.json."));
                    }
                    else if (!string.IsNullOrWhiteSpace(residentGuardianId) &&
                             knownGuardianAbodes.TryGetValue(residentGuardianId, out var expectedAbodeId) &&
                             !string.IsNullOrWhiteSpace(expectedAbodeId) &&
                             !string.Equals(expectedAbodeId, residentAbodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{GuardianAbodeResidentState.StatePath}.entries[{residentId}].abodeId",
                            IssueSeverity.Error,
                            "resident.abodeId должен совпадать с canonical abodeId этого guardian",
                            code: "guardian_abode_resident_abode_mismatch",
                            section: "AfterlifeResidents",
                            expected: expectedAbodeId,
                            actual: residentAbodeId,
                            repairHint: "Синхронизируй resident.abodeId с guardian.abode.abodeId из canonical guardians state."));
                    }
                }
            }

            if (residentDoc.RootElement.ValueKind == JsonValueKind.Object &&
                residentDoc.RootElement.TryGetProperty(GuardianAbodeResidentState.InteractionReceiptsProperty, out var interactionReceipts) &&
                interactionReceipts.ValueKind == JsonValueKind.Array)
            {
                foreach (var receipt in interactionReceipts.EnumerateArray())
                {
                    var residentId = GetFirstNonEmptyString(receipt, "residentId");
                    if (string.IsNullOrWhiteSpace(residentId) || knownResidentIds.Contains(residentId))
                        continue;

                    issues.Add(new ValidationIssue(
                        $"{GuardianAbodeResidentState.StatePath}.interactionReceipts[{residentId}].residentId",
                        IssueSeverity.Error,
                        $"Resident interaction receipt ссылается на неизвестного resident '{residentId}'",
                        code: "guardian_abode_resident_receipt_unknown_resident_id",
                        section: "AfterlifeResidents",
                        expected: $"existing residentId from {GuardianAbodeResidentState.StatePath}",
                        actual: residentId,
                        repairHint: "Для interactionReceipts.residentId используй существующий residentId из guardian_abode_residents.json."));
                }
            }

            if (residentDoc.RootElement.ValueKind == JsonValueKind.Object &&
                residentDoc.RootElement.TryGetProperty(GuardianAbodeResidentState.HistoryLogProperty, out var historyLog) &&
                historyLog.ValueKind == JsonValueKind.Array)
            {
                foreach (var historyEntry in historyLog.EnumerateArray())
                {
                    var residentId = GetFirstNonEmptyString(historyEntry, "residentId");
                    if (string.IsNullOrWhiteSpace(residentId) || knownResidentIds.Contains(residentId))
                        continue;

                    issues.Add(new ValidationIssue(
                        $"{GuardianAbodeResidentState.StatePath}.historyLog[{residentId}].residentId",
                        IssueSeverity.Error,
                        $"Resident historyLog entry ссылается на неизвестного resident '{residentId}'",
                        code: "guardian_abode_resident_history_unknown_resident_id",
                        section: "AfterlifeResidents",
                        expected: $"existing residentId from {GuardianAbodeResidentState.StatePath}",
                        actual: residentId,
                        repairHint: "Для historyLog.residentId используй существующий residentId из guardian_abode_residents.json."));
                }
            }
        }

        var manifestedCompanionSourceRelicIds =
            await CollectManifestedCompanionSourceRelicValidationSurfaceAsync(issues);
        var hasCurrentReverseSoulRelicResidentValidationDependency =
            await HasReverseSoulRelicResidentValidationDependencyAsync(includeValidatedPreTurnFallback: false);
        var hasReverseSoulRelicResidentValidationDependency =
            await HasReverseSoulRelicResidentValidationDependencyAsync();
        var (soulQuestDoc, hasInvalidSoulQuestValidationState, isMissingSoulQuestValidationState) =
            await TryReadCurrentSoulQuestValidationDocumentAsync(issues);
        using var soulQuestDocScope = soulQuestDoc;
        var hasNonParticipatingCurrentSoulQuestState =
            soulQuestDoc is not null &&
            !TryGetSoulQuestCollection(soulQuestDoc.RootElement, out _);
        var hasResidentLinkedSoulQuestValidationDependency =
            residentLinkedQuestIds.Count > 0 ||
            SoulQuestDocumentHasResidentLinkedValidationSurface(soulQuestDoc);
        var skippedRivalKnownArcIds = canUseSkippedRivalArcReferenceFallback
            ? await ReadKnownRivalArcIdsForSkippedRivalLinkValidationAsync()
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var canValidateSkippedRivalArcLinks = skippedRivalKnownArcIds.Count > 0;
        var hasQuestOwnedSoulQuestValidationDependency =
            await HasSkippedRivalSoulQuestValidationDependencyAsync(
                soulQuestDoc,
                canValidateSkippedRivalArcLinks);
        if (isMissingResidentValidationState &&
            (hasCurrentReverseSoulRelicResidentValidationDependency || hasResidentLinkedSoulQuestValidationDependency))
        {
            AddMissingCurrentResidentValidationIssue(issues);
            hasInvalidResidentValidationState = true;
        }

        if (isMissingSoulQuestValidationState &&
            (residentLinkedQuestIds.Count > 0 || hasQuestOwnedSoulQuestValidationDependency))
        {
            AddMissingCurrentSoulQuestValidationIssue(issues);
            hasInvalidSoulQuestValidationState = true;
        }
        else if (!hasInvalidSoulQuestValidationState &&
                 hasNonParticipatingCurrentSoulQuestState &&
                 (residentLinkedQuestIds.Count > 0 || hasQuestOwnedSoulQuestValidationDependency))
        {
            AddNonParticipatingCurrentSoulQuestValidationIssue(issues);
            hasInvalidSoulQuestValidationState = true;
        }

        var canValidateResidentLinkedCrossReferences =
            residentDoc is not null && !hasInvalidResidentValidationState;
        var hasSoulRelicResidentValidationDependency =
            residentGrantedRelicIds.Count > 0 ||
            manifestedCompanionSourceRelicIds.Count > 0 ||
            (canValidateResidentLinkedCrossReferences && hasReverseSoulRelicResidentValidationDependency);
        var hasReadableSoulRelicValidationState = false;
        var knownSoulRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var relicSourceResidentIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (hasSoulRelicResidentValidationDependency)
        {
            (hasReadableSoulRelicValidationState, knownSoulRelicIds, relicSourceResidentIds) =
                await ReadCurrentSoulRelicResidentValidationStateAsync(issues);
        }

        if (soulQuestDoc is not null &&
            !hasInvalidSoulQuestValidationState)
        {
            var knownSoulQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var questResidentLinks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            JsonElement quests;
            string? questCollectionName = null;
            if (soulQuestDoc.RootElement.TryGetProperty("quests", out quests))
            {
                questCollectionName = "quests";
            }
            else if (soulQuestDoc.RootElement.TryGetProperty("UpdateSoulQuests", out quests))
            {
                questCollectionName = "UpdateSoulQuests";
            }
            else
            {
                quests = default;
            }

            if (questCollectionName != null && quests.ValueKind == JsonValueKind.Array)
            {
                var questIndex = 0;
                foreach (var quest in quests.EnumerateArray())
                {
                    var questId = GetFirstNonEmptyString(quest, "questId", "id");
                    if (!string.IsNullOrWhiteSpace(questId))
                        knownSoulQuestIds.Add(questId);

                    var relatedArcId = GetFirstNonEmptyString(quest, "relatedRivalArcId");
                    if (canValidateSkippedRivalArcLinks &&
                        !string.IsNullOrWhiteSpace(relatedArcId) &&
                        !skippedRivalKnownArcIds.Contains(relatedArcId))
                    {
                        issues.Add(new ValidationIssue(
                            $"game_state/quests/soul_quests.json.{questCollectionName}[{questIndex}].relatedRivalArcId",
                            IssueSeverity.Error,
                            $"Soul quest ссылается на неизвестный rival arc '{relatedArcId}'",
                            code: "soul_quest_unknown_rival_arc_id",
                            section: "RivalSoulArcs",
                            expected: "existing arcId from canonical rival_soul_arcs state",
                            actual: relatedArcId,
                            repairHint: "Для relatedRivalArcId используй существующий arcId из game_state/world/rival_soul_arcs.json."));
                    }

                    if (canValidateResidentLinkedCrossReferences)
                    {
                        var relatedResidentId = GetFirstNonEmptyString(quest, "relatedAfterlifeResidentId");
                        if (!string.IsNullOrWhiteSpace(relatedResidentId) && !knownResidentIds.Contains(relatedResidentId))
                        {
                            issues.Add(new ValidationIssue(
                                $"game_state/quests/soul_quests.json.{questCollectionName}[{questIndex}].relatedAfterlifeResidentId",
                                IssueSeverity.Error,
                                $"Soul quest ссылается на неизвестного afterlife resident '{relatedResidentId}'",
                                code: "soul_quest_unknown_afterlife_resident_id",
                                section: "SoulQuests",
                                expected: $"existing residentId from {GuardianAbodeResidentState.StatePath}",
                                actual: relatedResidentId,
                                repairHint: $"Для relatedAfterlifeResidentId используй residentId из {GuardianAbodeResidentState.StatePath}."));
                        }
                        else if (!string.IsNullOrWhiteSpace(relatedResidentId) && !string.IsNullOrWhiteSpace(questId))
                        {
                            questResidentLinks[relatedResidentId] = questId;
                        }
                    }

                    questIndex++;
                }

                if (canValidateResidentLinkedCrossReferences)
                {
                    foreach (var residentQuestLink in residentLinkedQuestIds)
                    {
                        if (!knownSoulQuestIds.Contains(residentQuestLink.Value))
                        {
                            issues.Add(new ValidationIssue(
                                $"{GuardianAbodeResidentState.StatePath}.entries[{residentQuestLink.Key}].linkedSoulQuestId",
                                IssueSeverity.Error,
                                $"Afterlife resident ссылается на неизвестный soul quest '{residentQuestLink.Value}'",
                                code: "guardian_abode_resident_unknown_linked_soul_quest_id",
                                section: "SoulQuests",
                                expected: "existing questId from game_state/quests/soul_quests.json",
                                actual: residentQuestLink.Value,
                                repairHint: "Если resident хранит linkedSoulQuestId, используй существующий questId из canonical soul_quests state."));
                            continue;
                        }

                        if (!questResidentLinks.TryGetValue(residentQuestLink.Key, out var relatedQuestId) ||
                            !string.Equals(relatedQuestId, residentQuestLink.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            issues.Add(new ValidationIssue(
                                $"{GuardianAbodeResidentState.StatePath}.entries[{residentQuestLink.Key}].linkedSoulQuestId",
                                IssueSeverity.Error,
                                "linkedSoulQuestId должен указывать на soul quest, который ссылается обратно на этого resident через relatedAfterlifeResidentId",
                                code: "guardian_abode_resident_linked_soul_quest_mismatch",
                                section: "SoulQuests",
                                expected: $"soul quest with relatedAfterlifeResidentId={residentQuestLink.Key}",
                                actual: residentQuestLink.Value,
                                repairHint: "Синхронизируй resident.linkedSoulQuestId и soul quest.relatedAfterlifeResidentId."));
                        }
                    }
                }
            }
        }

        if (hasReadableSoulRelicValidationState)
        {
            ValidateManifestedCompanionSourceRelicIds(
                manifestedCompanionSourceRelicIds,
                knownSoulRelicIds,
                issues);

            foreach (var residentRelicLink in residentGrantedRelicIds)
            {
                if (!knownSoulRelicIds.Contains(residentRelicLink.Value))
                {
                    issues.Add(new ValidationIssue(
                        $"{GuardianAbodeResidentState.StatePath}.entries[{residentRelicLink.Key}].grantedRelicId",
                        IssueSeverity.Error,
                        $"Afterlife resident ссылается на неизвестную soul relic '{residentRelicLink.Value}'",
                        code: "guardian_abode_resident_unknown_granted_relic_id",
                        section: "AfterlifeResidents",
                        expected: "existing relicId from soul_state.json",
                        actual: residentRelicLink.Value,
                        repairHint: "Если resident хранит grantedRelicId, соответствующая soul relic должна существовать в soul_state.json."));
                    continue;
                }

                if (relicSourceResidentIds.TryGetValue(residentRelicLink.Value, out var sourceResidentId) &&
                    !string.Equals(sourceResidentId, residentRelicLink.Key, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"{GuardianAbodeResidentState.StatePath}.entries[{residentRelicLink.Key}].grantedRelicId",
                        IssueSeverity.Error,
                        "grantedRelicId указывает на реликвию, привязанную к другому resident",
                        code: "guardian_abode_resident_granted_relic_resident_mismatch",
                        section: "AfterlifeResidents",
                        expected: residentRelicLink.Key,
                        actual: sourceResidentId,
                        repairHint: "Синхронизируй resident.grantedRelicId с sourceResidentId внутри companionSeed реликвии связи."));
                }
            }

            if (canValidateResidentLinkedCrossReferences)
            {
                foreach (var relicResidentLink in relicSourceResidentIds)
                {
                    if (knownResidentIds.Contains(relicResidentLink.Value))
                        continue;

                    issues.Add(new ValidationIssue(
                        $"game_state/meta/soul_state.json.soulRelics[{relicResidentLink.Key}].companionSeed.sourceResidentId",
                        IssueSeverity.Error,
                        $"Soul Relic ссылается на неизвестного afterlife resident '{relicResidentLink.Value}'",
                        code: "companion_echo_unknown_source_resident_id",
                        section: "AfterlifeResidents",
                        expected: $"existing residentId from {GuardianAbodeResidentState.StatePath}",
                        actual: relicResidentLink.Value,
                        repairHint: "Для companionSeed.sourceResidentId используй существующий residentId из guardian_abode_residents.json."));
                }
            }
        }

        if (canValidateSkippedRivalArcLinks)
            await ValidateSkippedRivalWorldEventRelatedRivalArcLinksAsync(issues, skippedRivalKnownArcIds);
    }


    private async Task<(bool HasReadableState, HashSet<string> KnownSoulRelicIds, Dictionary<string, string> RelicSourceResidentIds)>
        ReadCurrentSoulRelicResidentValidationStateAsync(List<ValidationIssue> issues)
    {
        var knownSoulRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var relicSourceResidentIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var soulState = await TryReadCurrentSoulRelicResidentValidationDocumentAsync();
        switch (soulState.Kind)
        {
            case CurrentOwnerStateReadKind.MissingOrWhitespace:
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "Afterlife resident validation требует current soul_state.json и не может проверять resident/relic cross-references без current soul state.",
                    code: "afterlife_resident_invalid_current_soul_state",
                    section: "AfterlifeResidents",
                    expected: "readable current soul_state.json",
                    actual: "current soul_state.json is missing",
                    repairHint: "Восстанови current soul_state.json перед validation resident.grantedRelicId, companionSeed и manifested companion cross-references."));
                return (false, knownSoulRelicIds, relicSourceResidentIds);
            case CurrentOwnerStateReadKind.UnreadableJson:
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "Afterlife resident validation требует readable current soul_state.json и не может проверять resident/relic cross-references поверх malformed current soul state.",
                    code: "afterlife_resident_invalid_current_soul_state",
                    section: "AfterlifeResidents",
                    expected: "readable current soul_state.json",
                    actual: soulState.Actual ?? "current soul_state.json unreadable or malformed",
                    repairHint: "Сделай current soul_state.json корректным JSON перед validation resident.grantedRelicId, companionSeed и manifested companion cross-references."));
                soulState.Document?.Dispose();
                return (false, knownSoulRelicIds, relicSourceResidentIds);
            case CurrentOwnerStateReadKind.NonObjectRoot:
            case CurrentOwnerStateReadKind.ContractInvalidTopLevel:
            case CurrentOwnerStateReadKind.InvalidCollectionShape:
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "Afterlife resident validation требует contract-valid current soul_state.json и не может проверять resident/relic cross-references поверх broken current soul state.",
                    code: "afterlife_resident_invalid_current_soul_state",
                    section: "AfterlifeResidents",
                    expected: "readable lifecycle-compatible current soul_state.json with canonical policy-sensitive roots/transients and canonical soulRelics equipped/stored arrays when present",
                    actual: soulState.Actual ?? "current soul_state.json violates resident/relic validation contract",
                    repairHint: "Сделай current soul_state.json корректным lifecycle-compatible object-root state. Исправь malformed sibling roots/transients и, если присутствует soulRelics, используй canonical equipped/stored arrays и fully canonical companion/imprint relic payloads перед validation resident.grantedRelicId, companionSeed и manifested companion cross-references."));
                soulState.Document?.Dispose();
                return (false, knownSoulRelicIds, relicSourceResidentIds);
            case CurrentOwnerStateReadKind.ReadableWithArrayCollection:
            case CurrentOwnerStateReadKind.ReadableButNoRelevantCollection:
                break;
            default:
                soulState.Document?.Dispose();
                return (false, knownSoulRelicIds, relicSourceResidentIds);
        }

        using (soulState.Document)
        {
            if (soulState.Document is not null)
            {
                CollectSoulRelicResidentValidationState(
                    soulState.Document.RootElement,
                    knownSoulRelicIds,
                    relicSourceResidentIds);
            }
        }

        return (true, knownSoulRelicIds, relicSourceResidentIds);
    }

    private async Task<CurrentOwnerStateReadResult> TryReadCurrentSoulRelicResidentValidationDocumentAsync()
    {
        var soulStateJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulStateJson))
            return new(CurrentOwnerStateReadKind.MissingOrWhitespace, null, null, default, null);

        JsonDocument soulStateDoc;
        try
        {
            soulStateDoc = JsonDocument.Parse(soulStateJson);
        }
        catch
        {
            return new(CurrentOwnerStateReadKind.UnreadableJson, null, null, default, "current soul_state.json unreadable or malformed");
        }

        if (soulStateDoc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new(
                CurrentOwnerStateReadKind.NonObjectRoot,
                soulStateDoc,
                null,
                default,
                $"current soul_state.json root is {soulStateDoc.RootElement.ValueKind}");
        }

        JsonObject? soulStateRootNode;
        try
        {
            soulStateRootNode = JsonNode.Parse(soulStateJson) as JsonObject;
        }
        catch
        {
            soulStateRootNode = null;
        }

        if (soulStateRootNode == null)
        {
            soulStateDoc.Dispose();
            return new(CurrentOwnerStateReadKind.UnreadableJson, null, null, default, "current soul_state.json unreadable or malformed");
        }

        var hasCanonicalTriggerLifeEnd = HasLifecycleAuthorizedCurrentTriggerLifeEndSync();

        if (GuardianPolicyContracts.TryDescribeInvalidPolicySensitiveReadableSoulStateRoot(
                soulStateRootNode,
                hasCanonicalTriggerLifeEnd,
                out var invalidSoulStateFailure))
        {
            return new(
                CurrentOwnerStateReadKind.InvalidCollectionShape,
                soulStateDoc,
                "soulRelics",
                default,
                invalidSoulStateFailure);
        }

        if (soulStateDoc.RootElement.TryGetProperty("soulRelics", out _))
            return new(CurrentOwnerStateReadKind.ReadableWithArrayCollection, soulStateDoc, "soulRelics", default, null);

        return new(CurrentOwnerStateReadKind.ReadableButNoRelevantCollection, soulStateDoc, null, default, null);
    }

    private static readonly HashSet<string> ResidentValidationAllowedTopLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        GuardianAbodeResidentState.UpdateProperty,
        GuardianAbodeResidentState.EntriesProperty,
        GuardianAbodeResidentState.UpdateRosterReceiptsProperty,
        GuardianAbodeResidentState.RosterReceiptsProperty,
        GuardianAbodeResidentState.UpdateInteractionReceiptsProperty,
        GuardianAbodeResidentState.InteractionReceiptsProperty,
        GuardianAbodeResidentState.UpdateTransferReceiptsProperty,
        GuardianAbodeResidentState.TransferReceiptsProperty,
        GuardianAbodeResidentState.UpdateHistoryLogProperty,
        GuardianAbodeResidentState.HistoryLogProperty,
        GuardianAbodeResidentState.UpdateThoughtJournalProperty,
        GuardianAbodeResidentState.ThoughtJournalProperty,
        GuardianAbodeResidentState.UpdateInteractionLogProperty,
        GuardianAbodeResidentState.InteractionLogProperty
    };

    private static readonly string[] ResidentValidationCollectionNames =
    {
        GuardianAbodeResidentState.UpdateProperty,
        GuardianAbodeResidentState.EntriesProperty,
        GuardianAbodeResidentState.UpdateRosterReceiptsProperty,
        GuardianAbodeResidentState.RosterReceiptsProperty,
        GuardianAbodeResidentState.UpdateInteractionReceiptsProperty,
        GuardianAbodeResidentState.InteractionReceiptsProperty,
        GuardianAbodeResidentState.UpdateTransferReceiptsProperty,
        GuardianAbodeResidentState.TransferReceiptsProperty,
        GuardianAbodeResidentState.UpdateHistoryLogProperty,
        GuardianAbodeResidentState.HistoryLogProperty,
        GuardianAbodeResidentState.UpdateThoughtJournalProperty,
        GuardianAbodeResidentState.ThoughtJournalProperty,
        GuardianAbodeResidentState.UpdateInteractionLogProperty,
        GuardianAbodeResidentState.InteractionLogProperty
    };

    private static readonly HashSet<string> SoulQuestValidationAllowedTopLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "UpdateSoulQuests",
        "quests"
    };

    private static readonly string[] SoulQuestValidationCollectionNames =
    {
        "quests",
        "UpdateSoulQuests"
    };

    private static readonly HashSet<string> WorldEventValidationAllowedTopLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "worldEventsLog"
    };

    private static readonly string[] WorldEventValidationCollectionNames =
    {
        "worldEventsLog"
    };

    private static readonly string[] RivalValidationCollectionNames =
    {
        "arcs",
        "UpdateRivalSoulArcs"
    };

    private static readonly HashSet<string> RivalValidationAllowedTopLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "arcs",
        "UpdateRivalSoulArcs"
    };

    private enum CurrentOwnerStateReadKind
    {
        MissingOrWhitespace,
        ReadableWithArrayCollection,
        ReadableButNoRelevantCollection,
        UnreadableJson,
        NonObjectRoot,
        ContractInvalidTopLevel,
        InvalidCollectionShape
    }

    private readonly record struct CurrentOwnerStateReadResult(
        CurrentOwnerStateReadKind Kind,
        JsonDocument? Document,
        string? CollectionName,
        JsonElement Collection,
        string? Actual);

    private async Task<CurrentOwnerStateReadResult> TryReadCurrentTopLevelArrayCollectionOwnerStateAsync(
        string path,
        HashSet<string> allowedTopLevelKeys,
        IReadOnlyList<string> preferredCollectionNames,
        bool allowArrayRoot = false,
        bool allowReadableButNoRelevantCollection = true)
    {
        var currentJson = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(currentJson))
            return new(CurrentOwnerStateReadKind.MissingOrWhitespace, null, null, default, null);

        JsonDocument currentDoc;
        try
        {
            currentDoc = JsonDocument.Parse(currentJson);
        }
        catch
        {
            return new(CurrentOwnerStateReadKind.UnreadableJson, null, null, default, $"current {path} unreadable or malformed");
        }

        if (allowArrayRoot && currentDoc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return new(
                CurrentOwnerStateReadKind.ReadableWithArrayCollection,
                currentDoc,
                preferredCollectionNames[0],
                currentDoc.RootElement,
                null);
        }

        if (currentDoc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new(
                CurrentOwnerStateReadKind.NonObjectRoot,
                currentDoc,
                null,
                default,
                $"current {path} root is {currentDoc.RootElement.ValueKind}");
        }

        var visibleProps = currentDoc.RootElement.EnumerateObject()
            .Where(prop => !prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var unsupportedVisibleProps = visibleProps
            .Where(prop => !allowedTopLevelKeys.Contains(prop.Name))
            .Select(prop => prop.Name)
            .ToList();
        if (unsupportedVisibleProps.Count > 0)
        {
            return new(
                CurrentOwnerStateReadKind.ContractInvalidTopLevel,
                currentDoc,
                null,
                default,
                $"unsupported visible top-level keys: {string.Join(", ", unsupportedVisibleProps)}");
        }

        if (TryGetFirstVisibleAllowedTopLevelKeyWithInvalidArrayShape(
                currentDoc.RootElement,
                allowedTopLevelKeys,
                out var invalidPropName,
                out var invalidValueKind))
        {
            return new(
                CurrentOwnerStateReadKind.InvalidCollectionShape,
                currentDoc,
                invalidPropName,
                default,
                $"{invalidPropName} is {invalidValueKind}");
        }

        foreach (var collectionName in preferredCollectionNames)
        {
            if (currentDoc.RootElement.TryGetProperty(collectionName, out var collection) &&
                collection.ValueKind == JsonValueKind.Array)
            {
                return new(
                    CurrentOwnerStateReadKind.ReadableWithArrayCollection,
                    currentDoc,
                    collectionName,
                    collection,
                    null);
            }
        }

        if (allowReadableButNoRelevantCollection)
        {
            return new(
                CurrentOwnerStateReadKind.ReadableButNoRelevantCollection,
                currentDoc,
                null,
                default,
                visibleProps.Count == 0
                    ? null
                    : $"visible top-level keys: {string.Join(", ", visibleProps.Select(prop => prop.Name))}");
        }

        return new(
            CurrentOwnerStateReadKind.ContractInvalidTopLevel,
            currentDoc,
            null,
            default,
            visibleProps.Count == 0
                ? "no visible top-level keys"
                : $"visible top-level keys: {string.Join(", ", visibleProps.Select(prop => prop.Name))}");
    }

    private async Task<(JsonDocument? Document, bool HasInvalidCurrentState, bool IsMissingCurrentState)> TryReadCurrentResidentValidationDocumentAsync(List<ValidationIssue> issues)
    {
        var residentState = await TryReadCurrentTopLevelArrayCollectionOwnerStateAsync(
            GuardianAbodeResidentState.StatePath,
            ResidentValidationAllowedTopLevelKeys,
            ResidentValidationCollectionNames);
        switch (residentState.Kind)
        {
            case CurrentOwnerStateReadKind.MissingOrWhitespace:
                return (null, false, true);
            case CurrentOwnerStateReadKind.ReadableWithArrayCollection:
            case CurrentOwnerStateReadKind.ReadableButNoRelevantCollection:
                return (residentState.Document, false, false);
            case CurrentOwnerStateReadKind.UnreadableJson:
                issues.Add(new ValidationIssue(
                    GuardianAbodeResidentState.StatePath,
                    IssueSeverity.Error,
                    "Afterlife resident validation требует readable current guardian_abode_residents.json и не может проверять resident/relic cross-references поверх malformed current resident state.",
                    code: "afterlife_resident_invalid_current_resident_state",
                    section: "AfterlifeResidents",
                    expected: $"readable current {GuardianAbodeResidentState.StatePath}",
                    actual: residentState.Actual ?? "current guardian_abode_residents.json unreadable or malformed",
                    repairHint: "Сделай current guardian_abode_residents.json корректным JSON перед validation resident/receipt/history/relic cross-references."));
                residentState.Document?.Dispose();
                return (null, true, false);
            case CurrentOwnerStateReadKind.InvalidCollectionShape:
                issues.Add(new ValidationIssue(
                    GuardianAbodeResidentState.StatePath,
                    IssueSeverity.Error,
                    "Afterlife resident validation требует array-shaped canonical resident collections и не может проверять resident/relic cross-references поверх shape-invalid current resident state.",
                    code: "afterlife_resident_invalid_current_resident_state",
                    section: "AfterlifeResidents",
                    expected: $"array-shaped current {GuardianAbodeResidentState.StatePath}.{residentState.CollectionName}",
                    actual: residentState.Actual ?? "resident collection has invalid shape",
                    repairHint: "Сохрани canonical resident collections как arrays. Для current guardian_abode_residents.json не используй object/scalar вместо entries, interactionReceipts, historyLog или resident journal arrays."));
                residentState.Document?.Dispose();
                return (null, true, false);
            case CurrentOwnerStateReadKind.ContractInvalidTopLevel:
                issues.Add(new ValidationIssue(
                    GuardianAbodeResidentState.StatePath,
                    IssueSeverity.Error,
                    "Afterlife resident validation требует current guardian_abode_residents.json с допустимыми top-level resident keys и не может проверять resident/relic cross-references поверх contract-invalid current resident state.",
                    code: "afterlife_resident_invalid_current_resident_state",
                    section: "AfterlifeResidents",
                    expected: $"readable current {GuardianAbodeResidentState.StatePath} object with one of: {string.Join(", ", ResidentValidationAllowedTopLevelKeys.OrderBy(x => x))}",
                    actual: residentState.Actual ?? "visible top-level keys are not part of resident contract",
                    repairHint: "Удали посторонние top-level keys и используй canonical resident contract keys вроде entries, interactionReceipts, historyLog или их Update* aliases."));
                residentState.Document?.Dispose();
                return (null, true, false);
            case CurrentOwnerStateReadKind.NonObjectRoot:
                issues.Add(new ValidationIssue(
                    GuardianAbodeResidentState.StatePath,
                    IssueSeverity.Error,
                    "Afterlife resident validation требует current guardian_abode_residents.json в object-root форме и не может проверять resident/relic cross-references поверх non-object resident state.",
                    code: "afterlife_resident_invalid_current_resident_state",
                    section: "AfterlifeResidents",
                    expected: $"readable current {GuardianAbodeResidentState.StatePath} object",
                    actual: residentState.Actual ?? "current guardian_abode_residents.json root is not an object",
                    repairHint: "Сохрани current guardian_abode_residents.json как корректный JSON object перед validation resident/receipt/history/relic cross-references."));
                residentState.Document?.Dispose();
                return (null, true, false);
            default:
                residentState.Document?.Dispose();
                return (null, true, false);
        }
    }

    private async Task<(JsonDocument? Document, bool HasInvalidCurrentState, bool IsMissingCurrentState)> TryReadCurrentSoulQuestValidationDocumentAsync(List<ValidationIssue> issues)
    {
        var soulQuestState = await TryReadCurrentTopLevelArrayCollectionOwnerStateAsync(
            "game_state/quests/soul_quests.json",
            SoulQuestValidationAllowedTopLevelKeys,
            SoulQuestValidationCollectionNames);
        switch (soulQuestState.Kind)
        {
            case CurrentOwnerStateReadKind.MissingOrWhitespace:
                return (null, false, true);
            case CurrentOwnerStateReadKind.ReadableWithArrayCollection:
            case CurrentOwnerStateReadKind.ReadableButNoRelevantCollection:
                return (soulQuestState.Document, false, false);
            case CurrentOwnerStateReadKind.UnreadableJson:
                issues.Add(new ValidationIssue(
                    "game_state/quests/soul_quests.json",
                    IssueSeverity.Error,
                    "Soul quest cross-reference validation требует readable current soul_quests.json и не может проверять resident/arc back-links поверх malformed current soul quest state.",
                    code: "soul_quest_invalid_current_state",
                    section: "SoulQuests",
                    expected: "readable current game_state/quests/soul_quests.json",
                    actual: soulQuestState.Actual ?? "current soul_quests.json unreadable or malformed",
                    repairHint: "Сделай current soul_quests.json корректным JSON перед validation relatedRivalArcId, relatedAfterlifeResidentId и linkedSoulQuestId cross-references."));
                soulQuestState.Document?.Dispose();
                return (null, true, false);
            case CurrentOwnerStateReadKind.InvalidCollectionShape:
                issues.Add(new ValidationIssue(
                    "game_state/quests/soul_quests.json",
                    IssueSeverity.Error,
                    "Soul quest cross-reference validation требует array-shaped canonical soul quest collections и не может проверять resident/arc back-links поверх shape-invalid current soul quest state.",
                    code: "soul_quest_invalid_current_state",
                    section: "SoulQuests",
                    expected: $"array-shaped current game_state/quests/soul_quests.json.{soulQuestState.CollectionName}",
                    actual: soulQuestState.Actual ?? "soul quest collection has invalid shape",
                    repairHint: "Сохраняй quests и UpdateSoulQuests как arrays. Не подменяй canonical soul quest collection object/scalar значением."));
                soulQuestState.Document?.Dispose();
                return (null, true, false);
            case CurrentOwnerStateReadKind.ContractInvalidTopLevel:
                issues.Add(new ValidationIssue(
                    "game_state/quests/soul_quests.json",
                    IssueSeverity.Error,
                    "Soul quest cross-reference validation требует current soul_quests.json с допустимыми top-level quest keys и не может проверять resident/arc back-links поверх contract-invalid current soul quest state.",
                    code: "soul_quest_invalid_current_state",
                    section: "SoulQuests",
                    expected: $"readable current game_state/quests/soul_quests.json object with one of: {string.Join(", ", SoulQuestValidationAllowedTopLevelKeys.OrderBy(x => x))}",
                    actual: soulQuestState.Actual ?? "visible top-level keys are not part of soul quest contract",
                    repairHint: "Используй canonical soul quest top-level keys quests или UpdateSoulQuests и убери произвольные aliases."));
                soulQuestState.Document?.Dispose();
                return (null, true, false);
            case CurrentOwnerStateReadKind.NonObjectRoot:
                issues.Add(new ValidationIssue(
                    "game_state/quests/soul_quests.json",
                    IssueSeverity.Error,
                    "Soul quest cross-reference validation требует current soul_quests.json в object-root форме и не может проверять resident/arc back-links поверх non-object soul quest state.",
                    code: "soul_quest_invalid_current_state",
                    section: "SoulQuests",
                    expected: "readable current game_state/quests/soul_quests.json object",
                    actual: soulQuestState.Actual ?? "current soul_quests.json root is not an object",
                    repairHint: "Сохрани current soul_quests.json как корректный JSON object перед validation relatedRivalArcId, relatedAfterlifeResidentId и linkedSoulQuestId cross-references."));
                soulQuestState.Document?.Dispose();
                return (null, true, false);
            default:
                soulQuestState.Document?.Dispose();
                return (null, true, false);
        }
    }

    private static void AddMissingCurrentResidentValidationIssue(List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            GuardianAbodeResidentState.StatePath,
            IssueSeverity.Error,
            "Afterlife resident validation требует current guardian_abode_residents.json и не может проверять resident/relic cross-references без current resident state.",
            code: "afterlife_resident_invalid_current_resident_state",
            section: "AfterlifeResidents",
            expected: $"readable current {GuardianAbodeResidentState.StatePath}",
            actual: "current guardian_abode_residents.json is missing",
            repairHint: "Восстанови current guardian_abode_residents.json перед validation resident/receipt/history/relic cross-references."));
    }

    private static void AddMissingCurrentSoulQuestValidationIssue(List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            "game_state/quests/soul_quests.json",
            IssueSeverity.Error,
            "Soul quest cross-reference validation требует current soul_quests.json и не может проверять resident/arc back-links без current soul quest state.",
            code: "soul_quest_invalid_current_state",
            section: "SoulQuests",
            expected: "readable current game_state/quests/soul_quests.json",
            actual: "current soul_quests.json is missing",
            repairHint: "Восстанови current soul_quests.json перед validation relatedRivalArcId, relatedAfterlifeResidentId и linkedSoulQuestId cross-references."));
    }

    private static void AddNonParticipatingCurrentSoulQuestValidationIssue(List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            "game_state/quests/soul_quests.json",
            IssueSeverity.Error,
            "Soul quest cross-reference validation требует current soul_quests.json с canonical quests/UpdateSoulQuests collection и не может проверять resident/arc back-links поверх non-participating current quest state.",
            code: "soul_quest_invalid_current_state",
            section: "SoulQuests",
            expected: "current game_state/quests/soul_quests.json with quests or UpdateSoulQuests array",
            actual: "current soul_quests.json has no canonical quests/UpdateSoulQuests collection",
            repairHint: "Для authority-relevant soul quest validation передай current soul_quests.json с quests или UpdateSoulQuests array. Не заменяй current quest state пустым object-root without canonical collection."));
    }

    private async Task<(JsonDocument? Document, JsonElement Collection, string? CollectionName, bool HasInvalidCurrentState, bool IsMissingCurrentState)>
        TryReadCurrentWorldEventValidationDocumentAsync(
            List<ValidationIssue> issues,
            bool requiresBonusClueIssue,
            bool requiresRivalIssue,
            bool treatMissingCurrentStateAsInvalid)
    {
        var worldEventState = await TryReadCurrentTopLevelArrayCollectionOwnerStateAsync(
            "game_state/world/world_events.json",
            WorldEventValidationAllowedTopLevelKeys,
            WorldEventValidationCollectionNames,
            allowArrayRoot: true,
            allowReadableButNoRelevantCollection: true);
        switch (worldEventState.Kind)
        {
            case CurrentOwnerStateReadKind.MissingOrWhitespace:
                if (treatMissingCurrentStateAsInvalid)
                {
                    AddInvalidCurrentWorldEventValidationIssues(
                        issues,
                        requiresBonusClueIssue,
                        requiresRivalIssue,
                        "current world_events.json missing or empty");
                    return (null, default, null, requiresBonusClueIssue || requiresRivalIssue, true);
                }

                return (null, default, null, false, true);
            case CurrentOwnerStateReadKind.ReadableWithArrayCollection:
                return (worldEventState.Document, worldEventState.Collection, worldEventState.CollectionName, false, false);
            case CurrentOwnerStateReadKind.ReadableButNoRelevantCollection:
                return (worldEventState.Document, default, null, false, false);
            case CurrentOwnerStateReadKind.UnreadableJson:
            case CurrentOwnerStateReadKind.NonObjectRoot:
            case CurrentOwnerStateReadKind.ContractInvalidTopLevel:
            case CurrentOwnerStateReadKind.InvalidCollectionShape:
                AddInvalidCurrentWorldEventValidationIssues(
                    issues,
                    requiresBonusClueIssue,
                    requiresRivalIssue,
                    worldEventState.Actual ?? "current world_events.json is broken");
                worldEventState.Document?.Dispose();
                return (null, default, null, requiresBonusClueIssue || requiresRivalIssue, false);
            default:
                worldEventState.Document?.Dispose();
                return (null, default, null, requiresBonusClueIssue || requiresRivalIssue, false);
        }
    }

    private static void AddInvalidCurrentWorldEventValidationIssues(
        List<ValidationIssue> issues,
        bool requiresBonusClueIssue,
        bool requiresRivalIssue,
        string actual)
    {
        if (requiresBonusClueIssue)
        {
            issues.Add(new ValidationIssue(
                "game_state/world/world_events.json",
                IssueSeverity.Error,
                "Rival/world bonus clue validation требует contract-valid current world_events.json и не может доказывать linked lore clue contracts поверх broken current world event state.",
                code: "world_event_bonus_clue_invalid_current_state",
                section: "RivalSoulArcs",
                expected: "readable current world_events.json with worldEventsLog array or array root",
                actual: actual,
                repairHint: "Сделай current world_events.json корректным JSON и используй canonical worldEventsLog array перед validation linked world-event bonus clue contracts."));
        }

        if (requiresRivalIssue)
        {
            issues.Add(new ValidationIssue(
                "game_state/world/world_events.json",
                IssueSeverity.Error,
                "Rival soul arc validation требует contract-valid current world_events.json и не может проверять linked world-event rivalry contracts поверх broken current world event state.",
                code: "rival_arc_world_event_invalid_current_state",
                section: "RivalSoulArcs",
                expected: "readable current world_events.json with worldEventsLog array or array root",
                actual: actual,
                repairHint: "Сделай current world_events.json корректным JSON и используй canonical worldEventsLog array перед validation linked rival world-event contracts."));
        }
    }

    private static bool SoulQuestDocumentHasResidentLinkedValidationSurface(JsonDocument? soulQuestDoc)
    {
        if (soulQuestDoc is null || soulQuestDoc.RootElement.ValueKind != JsonValueKind.Object)
            return false;

        if (!TryGetSoulQuestCollection(soulQuestDoc.RootElement, out var quests))
            return false;

        foreach (var quest in quests.EnumerateArray())
        {
            if (!string.IsNullOrWhiteSpace(GetFirstNonEmptyString(quest, "relatedAfterlifeResidentId")))
                return true;
        }

        return false;
    }

    private static bool TryGetSoulQuestCollection(JsonElement root, out JsonElement quests)
    {
        if (root.TryGetProperty("quests", out quests) && quests.ValueKind == JsonValueKind.Array)
            return true;

        if (root.TryGetProperty("UpdateSoulQuests", out quests) && quests.ValueKind == JsonValueKind.Array)
            return true;

        quests = default;
        return false;
    }

    private static bool TryGetWorldEventCollection(JsonElement root, out JsonElement worldEvents, out string? collectionName)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("worldEventsLog", out worldEvents) &&
            worldEvents.ValueKind == JsonValueKind.Array)
        {
            collectionName = "worldEventsLog";
            return true;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            worldEvents = root;
            collectionName = "worldEventsLog";
            return true;
        }

        worldEvents = default;
        collectionName = null;
        return false;
    }

    private static bool TryGetFirstVisibleAllowedTopLevelKeyWithInvalidArrayShape(
        JsonElement root,
        HashSet<string> allowedKeys,
        out string invalidPropName,
        out JsonValueKind invalidValueKind)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase) ||
                !allowedKeys.Contains(prop.Name))
            {
                continue;
            }

            if (prop.Value.ValueKind == JsonValueKind.Array)
                continue;

            invalidPropName = prop.Name;
            invalidValueKind = prop.Value.ValueKind;
            return true;
        }

        invalidPropName = string.Empty;
        invalidValueKind = default;
        return false;
    }

    private async Task<bool> HasQuestOwnedSoulQuestValidationDependencyAsync(JsonDocument? soulQuestDoc)
    {
        if (SoulQuestDocumentHasQuestOwnedValidationSurface(soulQuestDoc))
            return true;

        var preTurnSoulQuestJson = await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/quests/soul_quests.json");
        if (string.IsNullOrWhiteSpace(preTurnSoulQuestJson))
            return false;

        try
        {
            using var preTurnSoulQuestDoc = JsonDocument.Parse(preTurnSoulQuestJson);
            return SoulQuestDocumentHasQuestOwnedValidationSurface(preTurnSoulQuestDoc);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> HasSkippedRivalSoulQuestValidationDependencyAsync(
        JsonDocument? soulQuestDoc,
        bool canValidateRivalArcLinks)
    {
        if (SoulQuestDocumentHasQuestOwnedValidationSurface(soulQuestDoc, canValidateRivalArcLinks))
            return true;

        var preTurnSoulQuestJson = await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/quests/soul_quests.json");
        if (string.IsNullOrWhiteSpace(preTurnSoulQuestJson))
            return false;

        try
        {
            using var preTurnSoulQuestDoc = JsonDocument.Parse(preTurnSoulQuestJson);
            return SoulQuestDocumentHasQuestOwnedValidationSurface(preTurnSoulQuestDoc, canValidateRivalArcLinks);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> HasSoulRelicResidentValidationDependencyAsync(
        bool hasResidentGrantedRelicSurface,
        bool hasManifestedCompanionSourceRelicSurface)
    {
        if (hasResidentGrantedRelicSurface || hasManifestedCompanionSourceRelicSurface)
            return true;

        return await HasReverseSoulRelicResidentValidationDependencyAsync();
    }

    private async Task<bool> HasReverseSoulRelicResidentValidationDependencyAsync(bool includeValidatedPreTurnFallback = true)
    {
        var currentSoulStateJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (SoulStateMayContainReverseResidentValidationLinks(currentSoulStateJson))
            return true;

        if (!includeValidatedPreTurnFallback)
            return false;

        var preTurnSoulStateJson = await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/meta/soul_state.json");
        return SoulStateMayContainReverseResidentValidationLinks(preTurnSoulStateJson);
    }

    private static bool SoulStateMayContainReverseResidentValidationLinks(string? soulStateJson)
    {
        if (string.IsNullOrWhiteSpace(soulStateJson))
            return false;

        try
        {
            using var soulStateDoc = JsonDocument.Parse(soulStateJson);
            var knownSoulRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var relicSourceResidentIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CollectSoulRelicResidentValidationState(
                soulStateDoc.RootElement,
                knownSoulRelicIds,
                relicSourceResidentIds);
            return relicSourceResidentIds.Count > 0;
        }
        catch
        {
            return RawSoulStateMayContainReverseResidentValidationLinks(soulStateJson);
        }
    }

    private static bool RawSoulStateMayContainReverseResidentValidationLinks(string soulStateJson)
    {
        var trimmed = soulStateJson.Trim();
        if (trimmed.Length == 0)
            return false;

        return (trimmed.Contains("\"soulRelics\"", StringComparison.OrdinalIgnoreCase) &&
                (trimmed.Contains("\"companionSeed\"", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.Contains("\"companionS", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.Contains("\"sourceResidentId\"", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.Contains("\"sourceRes", StringComparison.OrdinalIgnoreCase))) ||
               trimmed.Contains("\"sourceResidentId\"", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("\"sourceRes", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HashSet<string>> ReadKnownRivalArcIdsForSkippedRivalLinkValidationAsync()
    {
        var preTurnRivalJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(RivalSoulArcService.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnRivalJson))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var preTurnRivalDoc = JsonDocument.Parse(preTurnRivalJson);
            return CollectKnownRivalArcIds(preTurnRivalDoc.RootElement);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static HashSet<string> CollectKnownRivalArcIds(JsonElement root)
    {
        var knownArcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetRivalArcCollection(root, out var arcs))
            return knownArcIds;

        foreach (var arc in arcs.EnumerateArray())
        {
            var arcId = GetFirstNonEmptyString(arc, "arcId");
            if (!string.IsNullOrWhiteSpace(arcId))
                knownArcIds.Add(arcId);
        }

        return knownArcIds;
    }

    private static bool TryGetRivalArcCollection(JsonElement root, out JsonElement arcs)
    {
        if (root.TryGetProperty("arcs", out arcs) && arcs.ValueKind == JsonValueKind.Array)
            return true;

        if (root.TryGetProperty("UpdateRivalSoulArcs", out arcs) && arcs.ValueKind == JsonValueKind.Array)
            return true;

        arcs = default;
        return false;
    }

    private Task<CurrentOwnerStateReadResult> TryReadCurrentRivalValidationDocumentAsync()
    {
        return TryReadCurrentTopLevelArrayCollectionOwnerStateAsync(
            RivalSoulArcService.StatePath,
            RivalValidationAllowedTopLevelKeys,
            RivalValidationCollectionNames);
    }

    private static void ValidateWorldEventRelatedRivalArcId(
        string worldEventCollectionName,
        int eventIndex,
        string? relatedArcId,
        HashSet<string> knownArcIds,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(relatedArcId) || knownArcIds.Contains(relatedArcId))
            return;

        issues.Add(new ValidationIssue(
            $"game_state/world/world_events.json.{worldEventCollectionName}[{eventIndex}].relatedRivalArcId",
            IssueSeverity.Error,
            $"World event ссылается на неизвестный rival arc '{relatedArcId}'",
            code: "world_event_unknown_rival_arc_id",
            section: "RivalSoulArcs",
            expected: "existing arcId from canonical rival_soul_arcs state",
            actual: relatedArcId,
            repairHint: "Если world event является сигналом чужой нити судьбы, используй существующий relatedRivalArcId из game_state/world/rival_soul_arcs.json."));
    }

    private static void ValidateWorldEventRivalArcLink(
        string worldEventCollectionName,
        int eventIndex,
        JsonElement worldEvent,
        HashSet<string> knownArcIds,
        List<ValidationIssue> issues)
    {
        var relatedArcId = GetFirstNonEmptyString(worldEvent, "relatedRivalArcId");
        ValidateWorldEventRelatedRivalArcId(
            worldEventCollectionName,
            eventIndex,
            relatedArcId,
            knownArcIds,
            issues);

        if (string.IsNullOrWhiteSpace(relatedArcId))
            return;

        var eventContext = $"game_state/world/world_events.json.{worldEventCollectionName}[{eventIndex}]";
        var visibility = GetFirstNonEmptyString(worldEvent, "visibility");
        if (string.IsNullOrWhiteSpace(visibility))
        {
            issues.Add(new ValidationIssue(
                $"{eventContext}.visibility",
                IssueSeverity.Error,
                "World event, связанный с rival soul arc, обязан явно указывать visibility",
                code: "world_event_rival_arc_missing_visibility",
                section: "RivalSoulArcs",
                expected: "Public | Regional | Secret | Faction-Internal | player_known",
                repairHint: "Для linked rival-thread world event всегда указывай visibility. Используй Public/Regional для обычных новостей, Secret/Faction-Internal для скрытых событий и player_known, если игрок уже добыл эту информацию через игру."));
            return;
        }

        if (!IsRecognizedRivalWorldEventVisibility(visibility))
        {
            issues.Add(new ValidationIssue(
                $"{eventContext}.visibility",
                IssueSeverity.Error,
                "World event, связанный с rival soul arc, использует неподдерживаемое visibility",
                code: "world_event_rival_arc_invalid_visibility",
                section: "RivalSoulArcs",
                expected: "Public | Regional | Secret | Faction-Internal | player_known",
                actual: visibility,
                repairHint: "Для linked rival-thread world event используй только Public, Regional, Secret, Faction-Internal или player_known. Если игрок реально узнал о скрытом событии, переведи его в player_known."));
        }
    }

    private async Task ValidateSkippedRivalWorldEventRelatedRivalArcLinksAsync(
        List<ValidationIssue> issues,
        HashSet<string> knownArcIds)
    {
        if (knownArcIds.Count == 0)
            return;

        var (worldEventsDoc, worldEvents, worldEventCollectionName, _, _) =
            await TryReadCurrentWorldEventValidationDocumentAsync(
                issues,
                requiresBonusClueIssue: false,
                requiresRivalIssue: true,
                treatMissingCurrentStateAsInvalid: false);
        using (worldEventsDoc)
        {
            if (worldEventsDoc is null ||
                worldEventCollectionName is null ||
                worldEvents.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var eventIndex = 0;
            foreach (var worldEvent in worldEvents.EnumerateArray())
            {
                ValidateWorldEventRivalArcLink(
                    worldEventCollectionName,
                    eventIndex,
                    worldEvent,
                    knownArcIds,
                    issues);
                eventIndex++;
            }
        }
    }

    private static void AddInvalidCurrentRivalValidationIssue(List<ValidationIssue> issues, string actual)
    {
        issues.Add(new ValidationIssue(
            RivalSoulArcService.StatePath,
            IssueSeverity.Error,
            "Rival soul arc validation требует readable current rival_soul_arcs.json с array-shaped arcs/UpdateRivalSoulArcs и не может доказывать rival-owned cross-reference contracts поверх broken current rival state.",
            code: "rival_arc_invalid_current_state",
            section: "RivalSoulArcs",
            expected: "readable current rival_soul_arcs.json with arcs/UpdateRivalSoulArcs array",
            actual: actual,
            repairHint: "Сделай current rival_soul_arcs.json корректным JSON и сохрани arcs/UpdateRivalSoulArcs как массив перед validation rival soul arc contracts и quest/world-event cross-references."));
    }

    private static bool SoulQuestDocumentHasQuestOwnedValidationSurface(
        JsonDocument? soulQuestDoc,
        bool includeRivalArcLinks = true)
    {
        if (soulQuestDoc is null || !TryGetSoulQuestCollection(soulQuestDoc.RootElement, out var quests))
            return false;

        foreach (var quest in quests.EnumerateArray())
        {
            if ((includeRivalArcLinks &&
                 !string.IsNullOrWhiteSpace(GetFirstNonEmptyString(quest, "relatedRivalArcId"))) ||
                !string.IsNullOrWhiteSpace(GetFirstNonEmptyString(quest, "relatedAfterlifeResidentId")))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<List<(string Context, string SourceRelicId)>> CollectManifestedCompanionSourceRelicValidationSurfaceAsync(
        List<ValidationIssue> issues)
    {
        var manifestedCompanionSourceRelicIds = new List<(string Context, string SourceRelicId)>();
        var npcCoreJson = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        if (string.IsNullOrWhiteSpace(npcCoreJson))
            return manifestedCompanionSourceRelicIds;

        var npcState = await TryReadCurrentTopLevelArrayCollectionOwnerStateAsync(
            "game_state/npcs/npc_core.json",
            GuardianPolicyContracts.NpcCoreLifecycleTopLevelSections,
            GuardianPolicyContracts.ManifestedCompanionNpcCarrierSections);
        using var npcDocScope = npcState.Document;

        switch (npcState.Kind)
        {
            case CurrentOwnerStateReadKind.MissingOrWhitespace:
            case CurrentOwnerStateReadKind.ReadableButNoRelevantCollection:
                return manifestedCompanionSourceRelicIds;
            case CurrentOwnerStateReadKind.UnreadableJson:
                if (GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(npcCoreJson))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/npcs/npc_core.json",
                        IssueSeverity.Error,
                        "Afterlife resident validation требует readable current npc_core.json и не может проверять manifested companion source relic links поверх malformed current NPC state.",
                        code: "afterlife_resident_invalid_current_npc_state",
                        section: "AfterlifeResidents",
                        expected: "readable current npc_core.json",
                        actual: npcState.Actual ?? "current npc_core.json unreadable or malformed",
                        repairHint: "Сделай current npc_core.json корректным JSON перед validation manifested companion sourceCompanionRelicId/sourceAfterlifeResidentId cross-references."));
                }

                return manifestedCompanionSourceRelicIds;
            case CurrentOwnerStateReadKind.NonObjectRoot:
                if (GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(npcCoreJson))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/npcs/npc_core.json",
                        IssueSeverity.Error,
                        "Afterlife resident validation требует readable current npc_core.json и не может проверять manifested companion source relic links поверх non-object current NPC state.",
                        code: "afterlife_resident_invalid_current_npc_state",
                        section: "AfterlifeResidents",
                        expected: "readable current npc_core.json object",
                        actual: npcState.Actual ?? "current npc_core.json root is not an object",
                        repairHint: "Сохрани current npc_core.json как корректный JSON object перед validation manifested companion sourceCompanionRelicId/sourceAfterlifeResidentId cross-references."));
                }

                return manifestedCompanionSourceRelicIds;
            case CurrentOwnerStateReadKind.ContractInvalidTopLevel:
                if (GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(npcCoreJson))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/npcs/npc_core.json",
                        IssueSeverity.Error,
                        "Afterlife resident validation требует current npc_core.json с допустимыми top-level NPC sections и не может проверять manifested companion source relic links поверх contract-invalid current NPC state.",
                        code: "afterlife_resident_invalid_current_npc_state",
                        section: "AfterlifeResidents",
                        expected: $"readable current game_state/npcs/npc_core.json object with one of: {string.Join(", ", GuardianPolicyContracts.NpcCoreLifecycleTopLevelSections.OrderBy(x => x))}",
                        actual: npcState.Actual ?? "visible top-level keys are not part of NPC companion contract",
                        repairHint: $"Используй lifecycle-approved npc_core top-level keys {string.Join(", ", GuardianPolicyContracts.NpcCoreLifecycleTopLevelSections.OrderBy(x => x))} и убери неподдерживаемые top-level keys. Companion-carrying NPC objects записывай только в {string.Join("/", GuardianPolicyContracts.ManifestedCompanionNpcCarrierSections)}."));
                }

                return manifestedCompanionSourceRelicIds;
            case CurrentOwnerStateReadKind.InvalidCollectionShape:
                if (GuardianPolicyContracts.ProbeManifestedCompanionNpcDependencySurface(npcCoreJson))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/npcs/npc_core.json",
                        IssueSeverity.Error,
                        "Afterlife resident validation требует array-shaped NPC collections и не может проверять manifested companion source relic links поверх shape-invalid current NPC state.",
                        code: "afterlife_resident_invalid_current_npc_state",
                        section: "AfterlifeResidents",
                        expected: $"array-shaped current game_state/npcs/npc_core.json.{npcState.CollectionName}",
                        actual: npcState.Actual ?? "NPC collection has invalid shape",
                        repairHint: $"Сохрани lifecycle-approved npc_core top-level sections как arrays. Companion-carrying NPC objects держи только в {string.Join("/", GuardianPolicyContracts.ManifestedCompanionNpcCarrierSections)}; rename payload и trade receipt updates остаются отдельными lifecycle-approved arrays."));
                }

                return manifestedCompanionSourceRelicIds;
            case CurrentOwnerStateReadKind.ReadableWithArrayCollection:
                break;
            default:
                return manifestedCompanionSourceRelicIds;
        }

        if (npcState.Document is null)
            return manifestedCompanionSourceRelicIds;

        var seenRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var npcCollectionName in GuardianPolicyContracts.ManifestedCompanionNpcCarrierSections)
        {
            if (!npcState.Document.RootElement.TryGetProperty(npcCollectionName, out var npcs) || npcs.ValueKind != JsonValueKind.Array)
                continue;

            var npcIndex = 0;
            foreach (var npc in npcs.EnumerateArray())
            {
                var npcContext = $"game_state/npcs/npc_core.json.{npcCollectionName}[{npcIndex++}]";
                var sourceRelicId = GetFirstNonEmptyString(npc, "sourceCompanionRelicId");
                var sourceResidentId = GetFirstNonEmptyString(npc, "sourceAfterlifeResidentId");
                var sourceImprintId = GetFirstNonEmptyString(npc, "sourceSoulImprintId");

                if ((!string.IsNullOrWhiteSpace(sourceResidentId) || !string.IsNullOrWhiteSpace(sourceImprintId)) &&
                    string.IsNullOrWhiteSpace(sourceRelicId))
                {
                    issues.Add(new ValidationIssue(
                        $"{npcContext}.sourceCompanionRelicId",
                        IssueSeverity.Error,
                        "Manifested companion NPC должен хранить sourceCompanionRelicId для однозначного closure",
                        code: "manifested_companion_missing_source_relic_id",
                        section: "AfterlifeResidents",
                        repairHint: "Когда companion manifestation fully materializes mortal NPC, всегда записывай sourceCompanionRelicId вместе с sourceAfterlifeResidentId/sourceSoulImprintId."));
                }

                if (!string.IsNullOrWhiteSpace(sourceRelicId) && !seenRelicIds.Add(sourceRelicId))
                {
                    issues.Add(new ValidationIssue(
                        $"{npcContext}.sourceCompanionRelicId",
                        IssueSeverity.Error,
                        "Несколько manifested NPC не должны делить один sourceCompanionRelicId",
                        code: "manifested_companion_duplicate_source_relic_id",
                        section: "AfterlifeResidents",
                        expected: "unique sourceCompanionRelicId",
                        actual: sourceRelicId,
                        repairHint: "Один companion-carrying relic должен materialize максимум один mortal companion path/NPC."));
                }

                if (!string.IsNullOrWhiteSpace(sourceRelicId))
                    manifestedCompanionSourceRelicIds.Add((npcContext, sourceRelicId));
            }
        }

        return manifestedCompanionSourceRelicIds;
    }

    private static void ValidateManifestedCompanionSourceRelicIds(
        IEnumerable<(string Context, string SourceRelicId)> manifestedCompanionSourceRelicIds,
        HashSet<string> knownSoulRelicIds,
        List<ValidationIssue> issues)
    {
        foreach (var (npcContext, sourceRelicId) in manifestedCompanionSourceRelicIds)
        {
            if (knownSoulRelicIds.Contains(sourceRelicId))
                continue;

            issues.Add(new ValidationIssue(
                $"{npcContext}.sourceCompanionRelicId",
                IssueSeverity.Error,
                $"Manifested companion NPC ссылается на неизвестную soul relic '{sourceRelicId}'",
                code: "manifested_companion_unknown_source_relic_id",
                section: "AfterlifeResidents",
                expected: "existing relicId from soul_state.json",
                actual: sourceRelicId,
                repairHint: "Для sourceCompanionRelicId используй реальную экипированную или хранимую soul relic из soul_state.json."));
        }
    }

    private static void CollectSoulRelicResidentValidationState(
        JsonElement soulStateRoot,
        HashSet<string> knownSoulRelicIds,
        Dictionary<string, string> relicSourceResidentIds)
    {
        if (soulStateRoot.ValueKind != JsonValueKind.Object ||
            !soulStateRoot.TryGetProperty("soulRelics", out var soulRelics))
        {
            return;
        }

        IEnumerable<JsonElement> EnumerateRelics()
        {
            if (soulRelics.ValueKind == JsonValueKind.Array)
            {
                foreach (var relic in soulRelics.EnumerateArray())
                    yield return relic;
                yield break;
            }

            if (soulRelics.ValueKind != JsonValueKind.Object)
                yield break;

            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (!soulRelics.TryGetProperty(collectionName, out var collection) || collection.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var relic in collection.EnumerateArray())
                    yield return relic;
            }
        }

        foreach (var relic in EnumerateRelics())
        {
            if (relic.ValueKind != JsonValueKind.Object)
                continue;

            var relicId = GetFirstNonEmptyString(relic, "relicId", "id");
            if (!string.IsNullOrWhiteSpace(relicId))
                knownSoulRelicIds.Add(relicId);

            if (relic.TryGetProperty("companionSeed", out var companionSeed) && companionSeed.ValueKind == JsonValueKind.Object)
            {
                var sourceResidentId = GetFirstNonEmptyString(companionSeed, "sourceResidentId");
                if (!string.IsNullOrWhiteSpace(relicId) && !string.IsNullOrWhiteSpace(sourceResidentId))
                    relicSourceResidentIds[relicId] = sourceResidentId;
            }
        }
    }


    private bool TryResolveVisibleRivalClueCurrentIncarnation(
        GuardianProjectTrackerPolicyContext trackerContext,
        string path,
        List<ValidationIssue> issues,
        out int currentIncarnation)
    {
        currentIncarnation = 0;
        var currentSoulJson = ReadCurrentTrackedFileSync("game_state/meta/soul_state.json");
        var preTurnSoulJson = ReadValidatedPendingTurnSnapshotSoulStateJsonSync(trackerContext.PreTurnTrackerSnapshot.Manifest);
        var currentTurn = ReadCurrentTurnNumberForProjectAuthority();
        if (CanonicalStateNormalizer.TryResolveGuardianProjectAuthoritySoulContext(
                currentSoulJson,
                preTurnSoulJson,
                ReadCurrentTrackedFileSync("game_state/control/life_transitions.json"),
                currentTurn,
                new GuardianProjectSoulContextRequirements(
                    RequiresCurrentIncarnation: true,
                    RequiresCurrentRealm: false),
                out currentIncarnation,
                out _,
                out var failureDescription))
        {
            return true;
        }

        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Rival arc bonus clue validation требует readable current soul_state для current-life lore budget resolution",
            code: "rival_arc_bonus_clue_invalid_current_soul_state",
            section: "RivalSoulArcs",
            expected: "readable current soul_state with valid currentIncarnation for lore_research bonus clue validation",
            actual: failureDescription,
            repairHint: "Сделай current soul_state.json читаемым и сохрани в нём valid currentIncarnation; если current soul_state partial, validated pending turn snapshot должен содержать usable soul baseline для current-life lore budget resolution."));
        return false;
    }

    private static void ValidateHostileDirectTargetRivalArcClueContract(
        JsonElement arc,
        string arcContext,
        List<ValidationIssue> issues,
        IReadOnlyDictionary<string, List<JsonElement>> relatedWorldEventsByArcId)
    {
        if (arc.ValueKind != JsonValueKind.Object ||
            !arc.TryGetProperty("playerIntersection", out var playerIntersection) ||
            playerIntersection.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var targetsPlayerDirectly =
            playerIntersection.TryGetProperty("targetsPlayerDirectly", out var targetsNode) &&
            targetsNode.ValueKind == JsonValueKind.True;
        var arcType = GetFirstNonEmptyString(arc, "arcType");
        var status = GetFirstNonEmptyString(arc, "status");
        if (!targetsPlayerDirectly ||
            !string.Equals(arcType, "hostile_hunt", StringComparison.OrdinalIgnoreCase) ||
            (!string.Equals(status, "intersecting", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(status, "resolved", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var visibleClues = CountPlayerVisibleRivalClues(arc, GetFirstNonEmptyString(arc, "arcId") ?? string.Empty, relatedWorldEventsByArcId);
        if (visibleClues >= 2)
            return;

        issues.Add(new ValidationIssue(
            $"{arcContext}.playerIntersection",
            IssueSeverity.Error,
            "Hostile rival soul arc, напрямую нацеленный на игрока, обязан оставить минимум два видимых следа до прямого столкновения",
            code: "rival_arc_hostile_direct_target_needs_two_visible_signals",
            section: "RivalSoulArcs",
            expected: ">= 2 player-visible clues across publicSignals/worldEventsLog with world event visibility Public|Regional|player_known before intersecting/resolved hostile collision",
            actual: visibleClues.ToString(),
            repairHint: "Добавь минимум два player-visible clue до прямого столкновения: publicSignals, связанные worldEventsLog с visibility=Public|Regional|player_known или их сочетание. Если игрок узнал secret/faction-internal событие через игру, переведи linked world event в visibility=player_known. Если один и тот же clue mirrored на обеих поверхностях, reuse bonusClueRevealId."));
    }
}
