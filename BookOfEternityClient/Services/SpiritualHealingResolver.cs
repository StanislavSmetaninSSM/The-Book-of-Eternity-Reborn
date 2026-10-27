using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Detached source DATA for the healing check; this type grants no mutation or publication authority.</summary>
internal sealed record SpiritualHealingResolverInput(
    WoundMaterializationEnvelope Before,
    WoundOwnerCoordinate Healer,
    JsonObject HealerProfile,
    JsonObject? PlayerSoul,
    EffectMechanicsSnapshot Effects,
    string SealedRollMode,
    IReadOnlyList<int> SealedRolls);

/// <summary>Recomputable healing decision, separate from accepted-turn authority.</summary>
internal sealed record SpiritualHealingResolution(
    string WoundId,
    WoundOwnerCoordinate Target,
    WoundOwnerCoordinate Healer,
    SpiritualHealingCalculation Calculation,
    IReadOnlyList<int> SourceRolls,
    IReadOnlyList<EffectRollContributionEvidence> RollContributions);

/// <summary>Either a complete healing decision or source validation failures.</summary>
internal sealed record SpiritualHealingResolutionResult(
    SpiritualHealingResolution? Resolution,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Resolution is not null && Issues.Count == 0;
}

/// <summary>Resolves a spiritual healing check from exact actor/art/effect and retained die data.</summary>
internal static class SpiritualHealingResolver
{
    /// <summary>Checks the real source inputs before using the existing healing arithmetic.</summary>
    /// <param name="input">Detached data exported by the current healing authority.</param>
    /// <returns>A recomputable result without permission to publish or mutate.</returns>
    internal static SpiritualHealingResolutionResult Resolve(SpiritualHealingResolverInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var before = input.Before;
        var healer = input.Healer;
        if (before is null || healer is null || before.Owner is null ||
            before.Classification is null || before.Severity is null ||
            !ResourceMaterializationContract.IsExactIdentifier(before.WoundId) ||
            before.Classification.Domain != "spiritual" || before.Lifecycle != "active" ||
            before.Severity.Rank is < 1 or > 4 ||
            before.Owner.Realm is not ("chaos_sea" or "shining_abode") ||
            before.Owner.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath ||
            healer.Realm != before.Owner.Realm ||
            healer.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath ||
            !WoundEffectCarrierAdapter.TryCreateTargetKey(before.Owner, out _) ||
            !WoundEffectCarrierAdapter.TryCreateTargetKey(healer, out var rollActor))
            return Invalid("spiritual_healing_target_invalid");

        if (input.HealerProfile is null ||
            !WoundCarrierCollectionAuthority.AfterlifeProfileMatches(input.HealerProfile, healer))
            return Invalid("spiritual_healing_actor_mismatch");
        if (input.HealerProfile["standardArts"] is not JsonObject arts ||
            !TryTier(arts[AfterlifeSpiritualConflictState.SpiritualHealingArtId],
                out var tier))
            return Invalid("spiritual_healing_art_invalid");
        if (healer.OwnerKind == "player_soul")
        {
            if (healer.OwnerId != "player_soul" || input.PlayerSoul is not { } soul ||
                soul["currentRealm"] is not JsonValue realmNode ||
                !realmNode.TryGetValue<string>(out var realm) ||
                !AfterlifeEntityProfileState.TryNormalizeEffectRealm(realm, out var normalizedRealm) ||
                normalizedRealm != healer.Realm ||
                soul["afterlifeCombatProfile"] is not JsonObject soulProfile ||
                soulProfile["artTiers"] is not JsonObject soulArts ||
                !TryTier(soulArts[AfterlifeSpiritualConflictState.SpiritualHealingArtId],
                    out var soulTier))
                return Invalid("spiritual_healing_player_source_invalid");
            if (tier != soulTier)
                return Invalid("spiritual_healing_player_art_mismatch");
            tier = soulTier;
        }

        if (input.Effects is null)
            return Invalid("spiritual_healing_effect_source_invalid");
        // The registered common roll modifier contributes advantage/disadvantage.
        // Standard spiritual arts are not Mortal skill IDs; a scoped Mortal skill
        // modifier cannot grant a healing bonus. No registered flat healing bonus
        // exists in this contour, so caller-authored numeric modifiers are absent.
        var roll = EffectRollContributionResolver.Resolve(input.Effects,
            new EffectRollContext(rollActor.Realm, rollActor.Kind, rollActor.TargetId, "skill_check", null));
        if (!roll.IsValid)
            return new(null, roll.Issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray());
        if (input.SealedRollMode != roll.RollMode)
            return Invalid("spiritual_healing_roll_mode_mismatch");
        var requiredDice = roll.RollMode == "normal" ? 1 : 2;
        if (input.SealedRolls is null || input.SealedRolls.Count != requiredDice ||
            input.SealedRolls.Any(static value => value is < 1 or > 20))
            return Invalid("spiritual_healing_draw_invalid");
        var draws = Array.AsReadOnly(input.SealedRolls.ToArray());
        var die = roll.RollMode switch
        {
            "normal" => draws[0],
            "advantage" => Math.Max(draws[0], draws[1]),
            "disadvantage" => Math.Min(draws[0], draws[1]),
            _ => 0
        };
        if (before.Complications is null || before.Complications.Count > 16 ||
            before.Complications.Any(static value => value is null ||
                value.State is not ("active" or "resolved") ||
                value.TreatmentDifficultyModifier is < 0 or > 4))
            return Invalid("spiritual_healing_complications_invalid");
        long complications = 0;
        foreach (var complication in before.Complications.Where(static value => value.State == "active"))
            complications = checked(complications + complication.TreatmentDifficultyModifier);
        var calculation = SpiritualHealingOutcomeMath.Calculate(new(tier, before.Severity.Rank,
            die, ValidatedModifiers: 0, complications));
        return calculation is null
            ? Invalid("spiritual_healing_calculation_invalid")
            : new(new(before.WoundId, before.Owner, healer, calculation, draws,
                Array.AsReadOnly(roll.Contributions.ToArray())), Array.Empty<ValidationIssue>());
    }

    private static bool TryTier(JsonNode? node, out int tier)
    {
        tier = 0;
        return node is JsonValue value && value.TryGetValue<int>(out tier) && tier is >= 0 and <= 5;
    }

    private static SpiritualHealingResolutionResult Invalid(string code) => new(null,
        Array.AsReadOnly(new[] { new ValidationIssue("spiritualHealing", IssueSeverity.Error,
            "Источник проверки духовного исцеления не соответствует текущему запросу.", code: code) }));
}
