using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class WoundEffectCarrierAdapter
{
    internal static bool TryCreateTargetKey(
        WoundOwnerCoordinate? owner,
        out EffectTargetKey target)
    {
        target = null!;
        if (owner is null ||
            !ResourceMaterializationContract.IsExactIdentifier(owner.Realm) ||
            !ResourceMaterializationContract.IsExactIdentifier(owner.OwnerKind) ||
            !ResourceMaterializationContract.IsExactIdentifier(owner.OwnerId))
        {
            return false;
        }

        var kind = owner.Realm switch
        {
            "mortal_world" => owner.OwnerKind switch
            {
                "player" => "player",
                "combatant" or "combatant_member" => "combatant",
                "npc" => "npc",
                _ => string.Empty
            },
            "chaos_sea" or "shining_abode" => owner.OwnerKind switch
            {
                "player_soul" => "player",
                "guardian" or "resident" or "radiant_actor" or
                    "afterlife_actor" => owner.OwnerKind,
                _ => string.Empty
            },
            _ => string.Empty
        };
        if (kind.Length == 0)
            return false;

        target = new EffectTargetKey(owner.Realm, kind, owner.OwnerId);
        return true;
    }

    internal static bool TryCreateCarrierCoordinate(
        WoundOwnerCoordinate? owner,
        EffectTargetKey? target,
        JsonObject? definition,
        out EffectCarrierCoordinate coordinate)
    {
        coordinate = null!;
        if (!TryCreateTargetKey(owner, out var expectedTarget) ||
            target is null ||
            target != expectedTarget)
        {
            return false;
        }

        if (string.Equals(target.Kind, "player", StringComparison.Ordinal))
        {
            if (string.Equals(target.Realm, "mortal_world", StringComparison.Ordinal))
            {
                if (!string.Equals(
                        owner!.CarrierPath,
                        WoundCarrierCatalog.PlayerPath,
                        StringComparison.Ordinal) ||
                    !string.Equals(target.TargetId, "player_current", StringComparison.Ordinal))
                {
                    return false;
                }

                coordinate = new EffectCarrierCoordinate(
                    "player",
                    "player_current",
                    EffectCarrierCatalog.PlayerPath,
                    null);
                return true;
            }

            if (target.Realm is not ("chaos_sea" or "shining_abode") ||
                !string.Equals(owner!.OwnerKind, "player_soul", StringComparison.Ordinal) ||
                !string.Equals(
                    owner.CarrierPath,
                    WoundCarrierCatalog.AfterlifeProfilesPath,
                    StringComparison.Ordinal))
            {
                return false;
            }

            coordinate = new EffectCarrierCoordinate(
                "afterlife_profile",
                target.TargetId,
                EffectCarrierCatalog.AfterlifeProfilesPath,
                null);
            return true;
        }

        if (string.Equals(target.Kind, "npc", StringComparison.Ordinal))
        {
            if (!string.Equals(target.Realm, "mortal_world", StringComparison.Ordinal) ||
                !string.Equals(
                    owner!.CarrierPath,
                    WoundCarrierCatalog.NpcPath,
                    StringComparison.Ordinal))
            {
                return false;
            }

            coordinate = new EffectCarrierCoordinate(
                "npc",
                target.TargetId,
                EffectCarrierCatalog.NpcPath,
                null);
            return true;
        }

        if (string.Equals(target.Kind, "combatant", StringComparison.Ordinal))
        {
            if (!string.Equals(target.Realm, "mortal_world", StringComparison.Ordinal) ||
                owner!.CarrierPath is not (
                    WoundCarrierCatalog.EnemiesPath or WoundCarrierCatalog.AlliesPath) ||
                definition?["display"] is not JsonObject display ||
                display["category"] is not JsonValue categoryValue ||
                !categoryValue.TryGetValue<string>(out var category) ||
                category is not ("buff" or "debuff"))
            {
                return false;
            }

            coordinate = new EffectCarrierCoordinate(
                "combatant",
                target.TargetId,
                owner.CarrierPath,
                category);
            return true;
        }

        if (target.Kind is not (
                "guardian" or "resident" or "radiant_actor" or "afterlife_actor") ||
            target.Realm is not ("chaos_sea" or "shining_abode") ||
            !string.Equals(
                owner!.CarrierPath,
                WoundCarrierCatalog.AfterlifeProfilesPath,
                StringComparison.Ordinal))
        {
            return false;
        }

        coordinate = new EffectCarrierCoordinate(
            "afterlife_profile",
            target.TargetId,
            EffectCarrierCatalog.AfterlifeProfilesPath,
            null);
        return true;
    }
}
