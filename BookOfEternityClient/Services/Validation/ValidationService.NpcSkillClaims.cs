using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private const string NpcClaimCorePath = "game_state/npcs/npc_core.json";
    private const string NpcClaimSkillsPath = "game_state/npcs/npc_skills.json";

    /// <summary>Checks explicit Mortal NPC declarations without inferring actors from prose or writing state.</summary>
    /// <param name="narrative">The current player-facing narrative.</param>
    /// <param name="issues">The current invocation's bounded diagnostics.</param>
    /// <param name="source">The existing source-observation lifetime, when capturing a reusable pass.</param>
    private async Task ValidateMortalNpcSkillClaimsAsync(string narrative, List<ValidationIssue> issues,
        NarrativeInterfaceSourceSession? source)
    {
        using var request = NpcClaimDocument(await ReadNarrativeSourceTextAsync("input/turn_request.json", source));
        using var soul = NpcClaimDocument(await ReadNarrativeSourceTextAsync("game_state/meta/soul_state.json", source));
        var realm = request?.RootElement.TryGetProperty("progressionControl", out var progression) == true &&
            progression.ValueKind == JsonValueKind.Object ? NpcClaimText(progression, "currentRealm") : null;
        var soulRealm = soul is null ? null : NpcClaimText(soul.RootElement, "currentRealm");
        if (!IsMortalRealmName(realm) && !IsMortalRealmName(soulRealm)) return;

        using var debug = NpcClaimDocument(await ReadNarrativeSourceTextAsync("output/debug_logs.json", source));
        if (debug is null || !debug.RootElement.TryGetProperty("npcSkillClaims", out var claims) || claims.ValueKind != JsonValueKind.Array)
        {
            NpcClaimIssue(issues, null, "report_invalid", "Mortal turn requires npcSkillClaims array, including [] when none.");
            return;
        }
        if (claims.GetArrayLength() == 0) return;
        using var core = NpcClaimDocument(await ReadNarrativeSourceTextAsync(NpcClaimCorePath, source));
        var skillJson = await ReadNarrativeSourceTextAsync(NpcClaimSkillsPath, source);
        using var skills = NpcClaimDocument(skillJson);
        if (core is null || (skillJson is not null && skills is null))
        {
            NpcClaimIssue(issues, null, "state_unreadable", "Current NPC skill ownership cannot be read."); return;
        }
        var current = NpcClaimState.Read(core.RootElement, skills?.RootElement);
        // No original is needed to prove current ownership for an explicit no-progress use.
        var needsOriginal = claims.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object && NpcClaimText(row, "resolution") == "stateDelta");
        NpcClaimState? original = null;
        if (needsOriginal)
        {
            var oldCore = await ReadNpcClaimOriginalAsync(NpcClaimCorePath, source);
            var oldSkills = await ReadNpcClaimOriginalAsync(NpcClaimSkillsPath, source);
            using var coreDoc = NpcClaimDocument(oldCore.Json);
            using var skillDoc = NpcClaimDocument(oldSkills.Json);
            if (oldCore.Proven && oldSkills.Proven && (oldCore.Json is null || coreDoc is not null) &&
                (oldSkills.Json is null || skillDoc is not null))
                original = NpcClaimState.Read(coreDoc?.RootElement, skillDoc?.RootElement);
        }
        var seen = new HashSet<(NpcClaimKey Key, string Type, string Quote)>();
        var index = 0;
        foreach (var row in claims.EnumerateArray())
        {
            var i = index++;
            if (row.ValueKind != JsonValueKind.Object || row.EnumerateObject().Any(p =>
                p.Name is not ("actorKey" or "skillName" or "skillKind" or "claimType" or "narrativeQuote" or "resolution" or "reason")))
            { NpcClaimIssue(issues, i, "row_invalid", "Use only the documented claim fields."); continue; }
            var actor = NpcClaimText(row, "actorKey"); var name = NpcClaimText(row, "skillName");
            var kind = NpcClaimText(row, "skillKind"); var type = NpcClaimText(row, "claimType");
            var quote = NpcClaimText(row, "narrativeQuote"); var resolution = NpcClaimText(row, "resolution");
            if (actor is null || name is null || kind is not ("active" or "passive") ||
                type is not ("use" or "learning" or "mastery") || quote is null ||
                resolution is not ("stateDelta" or "noProgress") || !narrative.Contains(quote, StringComparison.Ordinal))
            { NpcClaimIssue(issues, i, "row_invalid", "Provide exact actorKey/skillName, kind, claim type, current narrative quote and resolution."); continue; }
            var key = new NpcClaimKey(actor, kind, name);
            if (!seen.Add((key, type, quote)))
            { NpcClaimIssue(issues, i, "duplicate", "Duplicate declared claim.", actor); continue; }
            if (!current.TryGet(key, out var now))
            { NpcClaimIssue(issues, i, "ownership_invalid", "No unambiguous current exact owner/kind/skill binding.", actor); continue; }
            if (resolution == "noProgress")
            {
                if (type != "use" || NpcClaimText(row, "reason") is null)
                    NpcClaimIssue(issues, i, "rationale_invalid", "Only use admits explicit nonempty no-progress reasoning.", actor);
                continue;
            }
            if (original is null || !original.Valid)
            { NpcClaimIssue(issues, i, "original_unproven", "A readable authenticated pre-turn NPC snapshot is required; filesModified is not evidence.", actor); continue; }
            var existed = original.TryGet(key, out var before);
            var newSkill = !existed && !original.Contains(key);
            if (type == "learning" ? !newSkill : !(newSkill || (existed && now.ProgressedFrom(before))))
                NpcClaimIssue(issues, i, "delta_missing", "This same NPC skill has no proven new creation or changed numeric mastery/progress; unrelated/cosmetic/stale edits do not count.", actor);
        }
    }

    /// <summary>Requires authenticated original files; the existing closed absence contract excludes NPC core/skills.</summary>
    private async Task<(bool Proven, string? Json)> ReadNpcClaimOriginalAsync(string path, NarrativeInterfaceSourceSession? source)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync(source: source, narrativeRolePath: path);
        source?.Policy(path, "npc-claim-original-lookup", lookup.Status.ToString());
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest is null)
            return (false, null);
        var manifest = lookup.Manifest;
        if (manifest.Files?.ContainsKey(path) != true)
            return (false, null);
        var json = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, path, source: source, treatBlankAsMissing: false);
        return (json is not null, json);
    }

    /// <summary>Parses a closed JSON object without silently accepting duplicate properties.</summary>
    private static JsonDocument? NpcClaimDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && NpcClaimUnique(doc.RootElement)) return doc;
            doc.Dispose(); return null;
        }
        catch (JsonException) { return null; }
    }
    private static bool NpcClaimUnique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().All(NpcClaimUnique);
        if (value.ValueKind != JsonValueKind.Object) return true;
        var names = new HashSet<string>(StringComparer.Ordinal);
        return value.EnumerateObject().All(p => names.Add(p.Name) && NpcClaimUnique(p.Value));
    }
    private static string? NpcClaimText(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String &&
        value.GetString() is { } text && !string.IsNullOrWhiteSpace(text) ? text : null;

    private static void NpcClaimIssue(List<ValidationIssue> issues, int? index, string code, string detail, string? actor = null) =>
        issues.Add(new ValidationIssue("output/debug_logs.json.npcSkillClaims" + (index is null ? "" : $"[{index}]"),
            IssueSeverity.Error, detail, code: "npc_skill_claim_" + code, actor: actor, section: "ProseStateDelta",
            expected: "explicit current exact NPC skill declaration and its same-owner state evidence or allowed no-progress rationale",
            actual: detail, repairHint: "Исправь только указанную строку npcSkillClaims и соответствующий реальный NPC/навык. Не угадывай владельца, не создавай прогресс ради проверки, не удаляй утверждение из отчёта без исправления прозы. Если связь не доказана, остановись и уточни."));

    /// <summary>Preserves existing case-insensitive actor identity and ordinal kind/skill identity.</summary>
    private readonly record struct NpcClaimKey(string Actor, string Kind, string Skill)
    {
        public bool Equals(NpcClaimKey other) => StringComparer.OrdinalIgnoreCase.Equals(Actor, other.Actor) && Kind == other.Kind && Skill == other.Skill;
        public override int GetHashCode() => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Actor), Kind, Skill);
    }
    private sealed record NpcClaimProgress(decimal? Level, decimal? Progress)
    {
        internal bool ProgressedFrom(NpcClaimProgress old) =>
            (Level.HasValue && old.Level.HasValue && Level != old.Level) ||
            (Progress.HasValue && old.Progress.HasValue && Progress != old.Progress);
    }
    /// <summary>Detached, invocation-local current/original skill DATA, never publication authority.</summary>
    private sealed class NpcClaimState
    {
        private readonly Dictionary<NpcClaimKey, NpcClaimProgress> _skills = new();
        private readonly HashSet<string> _owners = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<NpcClaimKey> _ambiguous = new();
        internal bool Valid { get; private set; } = true;
        internal bool Contains(NpcClaimKey key) => _skills.ContainsKey(key) || _ambiguous.Contains(key);
        internal bool TryGet(NpcClaimKey key, out NpcClaimProgress value)
        {
            value = null!;
            return Valid && !_ambiguous.Contains(key) && _skills.TryGetValue(key, out value!);
        }
        internal static NpcClaimState Read(JsonElement? core, JsonElement? changes)
        {
            var state = new NpcClaimState();
            if (core is { } c)
            {
                foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
                {
                    if (!c.TryGetProperty(section, out var actors)) continue;
                    if (actors.ValueKind != JsonValueKind.Array) { state.Valid = false; continue; }
                    foreach (var actor in actors.EnumerateArray())
                    {
                        if (actor.ValueKind != JsonValueKind.Object || BuildNpcIdentityKey(actor) is not { } owner)
                        { state.Valid = false; continue; }
                        state._owners.Add(owner);
                        foreach (var kind in new[] { "active", "passive" })
                            state.Profiles(actor, kind + "Skills", kind, false);
                    }
                }
            }
            if (changes is { } d)
                foreach (var kind in new[] { "active", "passive" })
                {
                    var groups = kind == "active" ? "NPCActiveSkillChanges" : "NPCPassiveSkillChanges";
                    if (d.TryGetProperty(groups, out var rows))
                    {
                        if (rows.ValueKind != JsonValueKind.Array) { state.Valid = false; continue; }
                        var seen = new HashSet<NpcClaimKey>();
                        foreach (var group in rows.EnumerateArray())
                        {
                            state.Profiles(group, "skillChanges", kind, true, seen);
                            state.Remove(group, kind);
                        }
                    }
                    var section = kind == "active" ? "NPCSkillMasteryChanges" : "NPCPassiveSkillMasteryChanges";
                    if (!d.TryGetProperty(section, out var mastery)) continue;
                    if (mastery.ValueKind != JsonValueKind.Array) { state.Valid = false; continue; }
                    var seenMastery = new HashSet<NpcClaimKey>();
                    foreach (var row in mastery.EnumerateArray())
                    {
                        if (row.ValueKind != JsonValueKind.Object) { state.Valid = false; continue; }
                        var owner = BuildNpcIdentityKey(row); var name = NpcClaimText(row, "skillName");
                        if (owner is null || name is null) { state.Valid = false; continue; }
                        var key = new NpcClaimKey(owner, kind, name);
                        if (!seenMastery.Add(key)) state._ambiguous.Add(key);
                        if (state._skills.ContainsKey(key)) state._skills[key] = Progress(row, true);
                    }
                }
            return state;
        }
        private void Profiles(JsonElement actor, string property, string kind, bool overlay, HashSet<NpcClaimKey>? seen = null)
        {
            if (actor.ValueKind != JsonValueKind.Object) { Valid = false; return; }
            var owner = BuildNpcIdentityKey(actor);
            if (owner is null || !_owners.Contains(owner)) { Valid = false; return; }
            if (!actor.TryGetProperty(property, out var profiles)) return;
            if (profiles.ValueKind != JsonValueKind.Array) { Valid = false; return; }
            seen ??= new HashSet<NpcClaimKey>();
            foreach (var profile in profiles.EnumerateArray())
            {
                if (profile.ValueKind != JsonValueKind.Object || NpcClaimText(profile, "skillName") is not { } name)
                { Valid = false; continue; }
                var key = new NpcClaimKey(owner, kind, name);
                if (!seen.Add(key)) _ambiguous.Add(key);
                var value = Progress(profile, false);
                if (!overlay && _skills.TryGetValue(key, out var previous) && previous != value) _ambiguous.Add(key);
                _skills[key] = value;
            }
        }
        /// <summary>Applies existing owner-local removal commands after profile changes, never recreating removed authority.</summary>
        private void Remove(JsonElement group, string kind)
        {
            if (group.ValueKind != JsonValueKind.Object || !group.TryGetProperty("skillsToRemove", out var removals)) return;
            var owner = BuildNpcIdentityKey(group);
            if (owner is null || removals.ValueKind != JsonValueKind.Array) { Valid = false; return; }
            foreach (var value in removals.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                { Valid = false; continue; }
                _skills.Remove(new NpcClaimKey(owner, kind, value.GetString()!));
            }
        }
        private static NpcClaimProgress Progress(JsonElement row, bool mastery) => new(
            Number(row, mastery ? "newMasteryLevel" : "masteryLevel") ?? (mastery ? null : Number(row, "currentMasteryLevel")),
            Number(row, mastery ? "newCurrentMasteryProgress" : "currentMasteryProgress"));
        private static decimal? Number(JsonElement row, string field) => row.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) && number >= 0 ? number : null;
    }
}
