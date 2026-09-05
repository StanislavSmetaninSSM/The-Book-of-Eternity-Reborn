using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

internal sealed record EffectPlayerFact(
    string Kind,
    string Label,
    string Value);

internal sealed record EffectPlayerAction(
    string Selector,
    string Kind,
    string Label,
    string? Description);

internal sealed record EffectPlayerEntry(
    string Selector,
    string Name,
    string? Summary,
    string State,
    IReadOnlyList<EffectPlayerFact> Facts,
    IReadOnlyList<EffectPlayerAction> Actions)
{
    internal string Realm { get; init; } = string.Empty;

    internal string TargetKind { get; init; } = string.Empty;

    internal string TargetId { get; init; } = string.Empty;
}

internal sealed record EffectPlayerProjectionInput(
    EffectMechanicsSnapshot Snapshot,
    string? Realm = null,
    string? TargetKind = null,
    string? TargetId = null);

internal sealed record EffectPlayerProjectionResult(
    bool IsAvailable,
    IReadOnlyList<EffectPlayerEntry> Entries,
    string StatusMessage)
{
    internal int VisibleCount => Entries.Count;

    internal IReadOnlyList<EffectPlayerAction> Actions =>
        Entries.SelectMany(static entry => entry.Actions).ToArray();
}

internal sealed record EffectPlayerActionResolution(
    string EffectId,
    string Realm,
    string TargetKind,
    string TargetId,
    string Operation,
    string AuthorityKind,
    string AuthorityValue);

internal static class EffectPlayerProjection
{
    internal const string UnavailableMessage =
        "Сейчас невозможно надёжно определить действующие эффекты.";

    private const string EmptyMessage = "Сейчас нет видимых действующих эффектов.";

    internal static EffectPlayerProjectionResult Build(EffectPlayerProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Snapshot);

        if (!input.Snapshot.IsAccepted)
            return Unavailable();

        try
        {
            var entries = input.Snapshot.Effects
                .Where(effect => MatchesScope(effect, input))
                .OrderBy(static effect => effect.EffectId, StringComparer.Ordinal)
                .Select(effect => BuildEntry(effect, input.Snapshot.SkillScopeAuthority))
                .Where(static entry => entry != null)
                .Cast<EffectPlayerEntry>()
                .ToArray();

            return new EffectPlayerProjectionResult(
                true,
                entries,
                entries.Length == 0 ? EmptyMessage : string.Empty);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            return Unavailable();
        }
    }

    internal static JsonNode? SanitizeSemanticValue(JsonNode? value) =>
        MortalItemPlayerProjection.CloneEffectSemanticValue(value);

    internal static bool TryResolveAction(
        EffectPlayerProjectionInput input,
        string? selector,
        out EffectPlayerActionResolution resolution)
    {
        resolution = null!;
        if (string.IsNullOrWhiteSpace(selector) || !input.Snapshot.IsAccepted)
            return false;

        var projection = Build(input);
        if (!projection.IsAvailable)
            return false;

        foreach (var accepted in input.Snapshot.Effects.Where(effect => MatchesScope(effect, input)))
        {
            var entry = BuildEntry(accepted, input.Snapshot.SkillScopeAuthority);
            if (entry == null)
                continue;

            var effect = accepted.CanonicalEffect;
            if (!string.Equals(ReadString(effect, "state"), "active", StringComparison.Ordinal) ||
                !effect.TryGetProperty("removal", out var removal) ||
                removal.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var candidate in EnumerateActionAuthorities(accepted, removal))
            {
                if (!string.Equals(candidate.Action.Selector, selector, StringComparison.Ordinal))
                    continue;

                resolution = new EffectPlayerActionResolution(
                    accepted.EffectId,
                    accepted.Realm,
                    accepted.TargetKind,
                    accepted.TargetId,
                    candidate.Operation,
                    candidate.AuthorityKind,
                    candidate.AuthorityValue);
                return true;
            }
        }

        return false;
    }

    private static bool MatchesScope(
        EffectAcceptedInstance effect,
        EffectPlayerProjectionInput input) =>
        (input.Realm == null || string.Equals(effect.Realm, input.Realm, StringComparison.Ordinal)) &&
        (input.TargetKind == null || string.Equals(effect.TargetKind, input.TargetKind, StringComparison.Ordinal)) &&
        (input.TargetId == null || string.Equals(effect.TargetId, input.TargetId, StringComparison.Ordinal));

    private static EffectPlayerEntry? BuildEntry(
        EffectAcceptedInstance accepted,
        EffectRollSkillScopeAuthority skillScopeAuthority)
    {
        var effect = accepted.CanonicalEffect;
        if (!effect.TryGetProperty("display", out var display) ||
            display.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(display, "visibility"), "visible", StringComparison.Ordinal))
        {
            return null;
        }

        var name = ReadString(display, "name") ?? "Эффект";
        var summary = ReadString(display, "description");
        var state = ReadString(effect, "state") ?? "active";
        var facts = new List<EffectPlayerFact>
        {
            new("category", "Категория", DescribeCategory(ReadString(display, "category")))
        };

        var sourceLabel = ReadString(display, "sourceLabel");
        if (!string.IsNullOrWhiteSpace(sourceLabel))
            facts.Add(new EffectPlayerFact("source", "Источник", sourceLabel));

        if (effect.TryGetProperty("components", out var components) &&
            components.ValueKind == JsonValueKind.Array)
        {
            var target = new EffectTargetKey(
                accepted.Realm,
                accepted.TargetKind,
                accepted.TargetId);
            foreach (var component in components.EnumerateArray()
                         .OrderBy(static component => ReadInt(component, "priority")))
            {
                facts.Add(ProjectComponent(component, target, skillScopeAuthority));
            }
        }

        if (effect.TryGetProperty("triggers", out var triggers) &&
            triggers.ValueKind == JsonValueKind.Array)
        {
            foreach (var trigger in triggers.EnumerateArray()
                         .OrderBy(static trigger => ReadInt(trigger, "priority")))
            {
                facts.Add(new EffectPlayerFact(
                    "trigger",
                    "Срабатывание",
                    DescribeTrigger(trigger)));
            }
        }

        if (effect.TryGetProperty("stacking", out var stacking) &&
            stacking.ValueKind == JsonValueKind.Object)
        {
            facts.Add(new EffectPlayerFact(
                "stacks",
                "Слои",
                $"{ReadInt(stacking, "currentStacks")}/{ReadInt(stacking, "maxStacks")}"));
        }

        if (effect.TryGetProperty("lifetime", out var lifetime) &&
            lifetime.ValueKind == JsonValueKind.Object)
        {
            facts.Add(new EffectPlayerFact("lifetime", "Срок действия", DescribeLifetime(lifetime)));
        }

        if (!string.Equals(state, "active", StringComparison.Ordinal))
            facts.Add(new EffectPlayerFact("state", "Состояние", DescribeState(state)));

        AddSafeLinkFacts(effect, sourceLabel, facts);

        var actions = Array.Empty<EffectPlayerAction>();
        if (string.Equals(state, "active", StringComparison.Ordinal) &&
            effect.TryGetProperty("removal", out var removal) &&
            removal.ValueKind == JsonValueKind.Object)
        {
            actions = EnumerateActionAuthorities(accepted, removal)
                .Select(static candidate => candidate.Action)
                .ToArray();
        }

        return new EffectPlayerEntry(
            CreateSelector("effect_view", accepted, "detail", string.Empty),
            name,
            summary,
            state,
            facts,
            actions)
        {
            Realm = accepted.Realm,
            TargetKind = accepted.TargetKind,
            TargetId = accepted.TargetId
        };
    }

    private static EffectPlayerFact ProjectComponent(
        JsonElement component,
        EffectTargetKey target,
        EffectRollSkillScopeAuthority skillScopeAuthority)
    {
        var profile = ReadString(component, "profile") ?? "effect";
        if (!component.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Accepted effect component has no payload.");
        }

        if (SpiritualWoundEffectProfileCatalog.TryGetProfile(
                profile,
                out var spiritualProfile))
        {
            return ProjectSpiritualWoundComponent(payload, spiritualProfile);
        }

        return profile switch
        {
            "characteristic_modifier" => new(
                profile,
                "Характеристика",
                $"{DescribeToken(ReadString(payload, "characteristic"))}: {DescribeSignedNumber(payload, "value")} ({DescribeModifierOperation(ReadString(payload, "operation"))}){DescribeOptionalCap(payload)}"),
            "roll_modifier" => new(
                profile,
                "Проверки",
                DescribeRollModifier(payload, target, skillScopeAuthority)),
            "resistance_modifier" => new(
                profile,
                "Сопротивление",
                $"{DescribeToken(ReadString(payload, "resistance"))}: {DescribeSignedNumber(payload, "value")} ({DescribeModifierOperation(ReadString(payload, "operation"))}){DescribeOptionalCap(payload)}"),
            "periodic_damage" => new(
                profile,
                "Периодический урон",
                $"{DescribeNumber(payload, "amount")} ед. ресурса «{DescribeToken(ReadString(payload, "resource"))}», тип: {DescribeToken(ReadString(payload, "damageType"))}; граница: {DescribeToken(ReadString(payload, "floorPolicy"))}"),
            "periodic_restore" => new(
                profile,
                "Периодическое восстановление",
                $"{DescribeNumber(payload, "amount")} ед. ресурса «{DescribeToken(ReadString(payload, "resource"))}»; граница: {DescribeToken(ReadString(payload, "capPolicy"))}"),
            "action_control" => new(
                profile,
                "Действие",
                DescribeActionControl(payload)),
            "event_reaction" => new(
                profile,
                "Реакция",
                $"На событие «{DescribeToken(ReadString(payload, "eventType"))}»: {DescribeToken(ReadString(payload, "resultKind"))}; порядок: {DescribeToken(ReadString(payload, "dependency"))}"),
            "wound_consequence" => new(
                profile,
                "Последствие раны",
                $"{DescribeToken(ReadString(payload, "symptom"))}: {DescribeToken(ReadString(payload, "consequence"))}. Снятие эффекта не лечит рану."),
            "afterlife_combat_condition" => new(
                profile,
                "Условие духовного конфликта",
                DescribeAfterlifeCondition(payload)),
            _ => throw new InvalidOperationException("Accepted effect contains an unregistered component profile.")
        };
    }

    private static string DescribeRollModifier(
        JsonElement payload,
        EffectTargetKey target,
        EffectRollSkillScopeAuthority skillScopeAuthority)
    {
        var contribution = ReadString(payload, "contribution") switch
        {
            "advantage" => "Преимущество",
            "disadvantage" => "Помеха",
            _ => "Изменение броска"
        };

        if (!payload.TryGetProperty("scope", out var scope) ||
            scope.ValueKind != JsonValueKind.Object)
        {
            return $"{contribution} на проверки конкретного недоступного навыка — сейчас не действует";
        }

        var scopeKind = ReadString(scope, "kind");
        if (string.Equals(scopeKind, "all", StringComparison.Ordinal) &&
            HasExactlySkillCheckOperation(payload))
            return $"{contribution} на все проверки навыков";

        if (string.Equals(scopeKind, "all", StringComparison.Ordinal))
        {
            return $"{DescribeStringArray(payload, "operations")}: " +
                   DescribeToken(ReadString(payload, "contribution"));
        }

        if (!string.Equals(scopeKind, "skill", StringComparison.Ordinal) ||
            ReadString(scope, "skillId") is not { } skillId)
        {
            return $"{contribution} на проверки конкретного недоступного навыка — сейчас не действует";
        }

        var resolution = skillScopeAuthority.ResolveCurrent(
            target,
            skillId,
            "effect_player_projection.scope.skillId");
        if (resolution.IsUsable && IsReadableSkillName(resolution.DisplayName, skillId))
            return $"{contribution} на проверки навыка «{resolution.DisplayName}»";

        if (resolution.State == EffectRollSkillScopeState.Unavailable &&
            IsReadableSkillName(resolution.DisplayName, skillId))
        {
            return $"{contribution} на проверки навыка «{resolution.DisplayName}» — сейчас не действует";
        }

        return $"{contribution} на проверки конкретного недоступного навыка — сейчас не действует";
    }

    private static bool IsReadableSkillName(string? displayName, string skillId) =>
        !string.IsNullOrWhiteSpace(displayName) &&
        !string.Equals(displayName, skillId, StringComparison.Ordinal) &&
        !displayName.Contains("skill_", StringComparison.OrdinalIgnoreCase);

    private static bool HasExactlySkillCheckOperation(JsonElement payload) =>
        payload.TryGetProperty("operations", out var operations) &&
        operations.ValueKind == JsonValueKind.Array &&
        operations.GetArrayLength() == 1 &&
        operations[0].ValueKind == JsonValueKind.String &&
        string.Equals(operations[0].GetString(), "skill_check", StringComparison.Ordinal);

    private static EffectPlayerFact ProjectSpiritualWoundComponent(
        JsonElement payload,
        SpiritualWoundProfileDescriptor profile)
    {
        var operationKey = ReadString(payload, "operation");
        if (operationKey == null ||
            !profile.LegalOperations.Contains(operationKey) ||
            !SpiritualWoundEffectProfileCatalog.TryGetOperation(
                operationKey,
                out var operation) ||
            !string.Equals(
                ReadString(payload, "axis"),
                profile.Axis,
                StringComparison.Ordinal) ||
            !payload.TryGetProperty("magnitude", out var magnitude) ||
            !profile.IsMagnitudeValid(magnitude))
        {
            throw new InvalidOperationException(
                "Accepted spiritual wound component violates its registered projection contract.");
        }

        var mechanic = profile.ProjectionKind switch
        {
            SpiritualWoundProjectionKind.RollHindrance =>
                "бросок совершается с помехой.",
            SpiritualWoundProjectionKind.ActionCostBurden =>
                $"стоимость духовного действия увеличена на {ReadMagnitudeInteger(profile, magnitude)}.",
            SpiritualWoundProjectionKind.PositionBurden =>
                $"позиция ухудшена на {DescribeSteps(ReadMagnitudeInteger(profile, magnitude))}.",
            SpiritualWoundProjectionKind.ControlBurden =>
                $"контроль ухудшен на {DescribeSteps(ReadMagnitudeInteger(profile, magnitude))}.",
            SpiritualWoundProjectionKind.StrainBurden =>
                $"напряжение стороны увеличено на {DescribeSteps(ReadMagnitudeInteger(profile, magnitude))}.",
            SpiritualWoundProjectionKind.TempoBurden =>
                "получение одного преимущества темпа запрещено.",
            SpiritualWoundProjectionKind.CounterBurden =>
                "результат контрдействия снижен на 1 ступень.",
            SpiritualWoundProjectionKind.ArtRestriction =>
                string.Equals(magnitude.GetString(), "restrict", StringComparison.Ordinal)
                    ? "духовное искусство ограничено."
                    : "духовное искусство запрещено.",
            _ => throw new InvalidOperationException(
                "Accepted spiritual wound component has no player projection.")
        };

        return new EffectPlayerFact(
            profile.Profile,
            profile.PlayerLabel,
            $"{operation.PlayerLabel}: {mechanic}");
    }

    private static int ReadMagnitudeInteger(
        SpiritualWoundProfileDescriptor profile,
        JsonElement magnitude)
    {
        if (profile.TryReadMagnitudeInteger(magnitude, out var value))
            return value;
        throw new InvalidOperationException(
            "Accepted spiritual wound component has no exact integer magnitude.");
    }

    private static string DescribeSteps(int value) =>
        value == 1 ? "1 ступень" : $"{value} ступени";

    private static string DescribeActionControl(JsonElement payload)
    {
        var action = DescribeToken(ReadString(payload, "action"));
        var operation = DescribeToken(ReadString(payload, "operation"));
        return payload.TryGetProperty("modifier", out var modifier) && modifier.ValueKind == JsonValueKind.Number
            ? $"{action}: {operation}, {FormatNumber(modifier)}"
            : $"{action}: {operation}";
    }

    private static string DescribeAfterlifeCondition(JsonElement payload)
    {
        var kind = DescribeToken(ReadString(payload, "conditionKind"));
        var targetSide = DescribeToken(ReadString(payload, "targetSide"));
        var operations = DescribeStringArray(payload, "operations");
        var axes = DescribeStringArray(payload, "axes");
        var counterplay = DescribeStringArray(payload, "counterplay");
        var payoff = DescribeToken(ReadString(payload, "payoff"));
        return $"{kind}; цель: {targetSide}; действия: {operations}; направления: {axes}; противодействие: {counterplay}; исход: {payoff}";
    }

    private static string DescribeLifetime(JsonElement lifetime)
    {
        var displayText = ReadString(lifetime, "displayText");
        var mechanical = ReadString(lifetime, "mode") switch
        {
            "turns" => $"Осталось ходов: {ReadInt(lifetime, "remainingTurns")}; обновление: {DescribeToken(ReadString(lifetime, "advancePhase"))}",
            "uses" => $"Осталось применений: {ReadInt(lifetime, "remainingUses")}",
            "until_time" => $"До отметки времени {ReadLong(lifetime, "deadline")}",
            "scene" =>
                $"До завершения текущей сцены; при потере сцены: {DescribeToken(ReadString(lifetime, "onSceneExit"))}",
            "source_bound" =>
                $"Пока действует связанный источник ({DescribeToken(ReadString(lifetime, "activePredicate"))}); при потере источника: {DescribeToken(ReadString(lifetime, "onSourceLoss"))}",
            "condition_bound" =>
                $"Пока выполняется связанное условие; при потере условия: {DescribeToken(ReadString(lifetime, "onConditionLoss"))}",
            "permanent" => "Постоянный эффект",
            "manual" => "До явного снятия",
            _ => "Срок определяется источником"
        };

        return string.IsNullOrWhiteSpace(displayText)
            ? mechanical
            : mechanical + ". " + displayText.Trim();
    }

    private static string DescribeTrigger(JsonElement trigger)
    {
        var value = $"{DescribeToken(ReadString(trigger, "eventType"))}; разрешение: {DescribeToken(ReadString(trigger, "resolutionMode"))}";
        return trigger.TryGetProperty("consumeUses", out var consumeUses) &&
               consumeUses.ValueKind == JsonValueKind.True
            ? value + "; расходует одно применение"
            : value;
    }

    private static string DescribeOptionalCap(JsonElement payload)
    {
        if (!payload.TryGetProperty("cap", out var cap) ||
            cap.ValueKind != JsonValueKind.Object ||
            !cap.TryGetProperty("minimum", out var minimum) ||
            !cap.TryGetProperty("maximum", out var maximum) ||
            minimum.ValueKind != JsonValueKind.Number ||
            maximum.ValueKind != JsonValueKind.Number)
        {
            return string.Empty;
        }

        return $"; предел: от {FormatNumber(minimum)} до {FormatNumber(maximum)}";
    }

    private static void AddSafeLinkFacts(
        JsonElement effect,
        string? sourceLabel,
        List<EffectPlayerFact> facts)
    {
        if (!effect.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
            return;

        var sourceKind = effect.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Object
            ? ReadString(source, "kind")
            : null;
        var visibleKinds = links.EnumerateArray()
            .Where(static link => link.ValueKind == JsonValueKind.Object)
            .Select(static link => ReadString(link, "kind"))
            .Where(static kind => !string.IsNullOrWhiteSpace(kind))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static kind => kind, StringComparer.Ordinal);

        foreach (var kind in visibleKinds)
        {
            var label = string.Equals(kind, sourceKind, StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(sourceLabel)
                ? sourceLabel
                : DescribeLinkKind(kind);
            facts.Add(new EffectPlayerFact("link", "Связанный контекст", label!));
        }
    }

    private static IEnumerable<ActionAuthority> EnumerateActionAuthorities(
        EffectAcceptedInstance accepted,
        JsonElement removal)
    {
        var woundBoundary = IsWoundDerived(accepted.CanonicalEffect)
            ? "Действие подавляет только последствие эффекта и само по себе не лечит связанную рану; лечение раны выполняется отдельно."
            : null;
        var revalidatedDescription =
            "Доступность будет повторно проверена перед действием." +
            (woundBoundary == null ? string.Empty : " " + woundBoundary);
        foreach (var value in EnumerateStrings(removal, "dispelCategories"))
        {
            yield return CreateActionAuthority(
                accepted,
                "dispel",
                "dispel_category",
                value,
                $"Противодействовать: {DescribeToken(value)}",
                revalidatedDescription);
        }

        foreach (var value in EnumerateStrings(removal, "cureKinds"))
        {
            yield return CreateActionAuthority(
                accepted,
                "remove",
                "cure_kind",
                value,
                $"Ослабить последствие: {DescribeToken(value)}",
                woundBoundary == null
                    ? "Это снимает только выбранный эффект."
                    : woundBoundary);
        }

        foreach (var value in EnumerateStrings(removal, "manualAuthorities"))
        {
            yield return CreateActionAuthority(
                accepted,
                "remove",
                "manual_authority",
                value,
                $"Снять эффект: {DescribeToken(value)}",
                revalidatedDescription);
        }
    }

    private static bool IsWoundDerived(JsonElement effect)
    {
        if (effect.TryGetProperty("source", out var source) &&
            source.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(source, "kind"), "wound", StringComparison.Ordinal))
        {
            return true;
        }

        if (effect.TryGetProperty("components", out var components) &&
            components.ValueKind == JsonValueKind.Array &&
            components.EnumerateArray().Any(static component =>
                string.Equals(
                    ReadString(component, "profile"),
                    "wound_consequence",
                    StringComparison.Ordinal)))
        {
            return true;
        }

        return effect.TryGetProperty("links", out var links) &&
               links.ValueKind == JsonValueKind.Array &&
               links.EnumerateArray().Any(static link =>
                   string.Equals(ReadString(link, "kind"), "wound", StringComparison.Ordinal) &&
                   string.Equals(ReadString(link, "role"), "source", StringComparison.Ordinal));
    }

    private static ActionAuthority CreateActionAuthority(
        EffectAcceptedInstance accepted,
        string operation,
        string authorityKind,
        string authorityValue,
        string label,
        string description) =>
        new(
            new EffectPlayerAction(
                CreateSelector("effect_action", accepted, authorityKind, authorityValue),
                operation,
                label,
                description),
            operation,
            authorityKind,
            authorityValue);

    private static string CreateSelector(
        string prefix,
        EffectAcceptedInstance accepted,
        string authorityKind,
        string authorityValue)
    {
        var material = string.Join(
            '\u001f',
            "effect-player-selector-v1",
            accepted.EffectId,
            accepted.Realm,
            accepted.TargetKind,
            accepted.TargetId,
            authorityKind,
            authorityValue);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return prefix + "_" + Convert.ToHexString(digest.AsSpan(0, 12)).ToLowerInvariant();
    }

    private static IEnumerable<string> EnumerateStrings(JsonElement root, string field)
    {
        if (!root.TryGetProperty(field, out var values) || values.ValueKind != JsonValueKind.Array)
            yield break;
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text)
                yield return text;
        }
    }

    private static string DescribeStringArray(JsonElement root, string field) =>
        string.Join(", ", EnumerateStrings(root, field).Select(DescribeToken));

    private static string DescribeCategory(string? value) => value switch
    {
        "buff" => "Усиление",
        "debuff" => "Ослабление",
        "condition" => "Состояние",
        "environmental" => "Воздействие среды",
        "mixed" => "Смешанный эффект",
        _ => "Эффект"
    };

    private static string DescribeState(string value) => value switch
    {
        "active" => "Действует",
        "suspended" => "Приостановлен",
        _ => "Недоступен"
    };

    private static string DescribeModifierOperation(string? value) => value switch
    {
        "flat" => "плоское изменение",
        "percent" => "процентное изменение",
        _ => DescribeToken(value)
    };

    private static string DescribeLinkKind(string? value) => value switch
    {
        "wound" => "Рана",
        "skill" => "Навык",
        "spiritual_art" => "Духовное искусство",
        "item" => "Предмет",
        "quest" => "Квест",
        "location" => "Локация",
        "hazard" => "Опасность",
        "faction" => "Фракция",
        "world_event" => "Событие мира",
        "fate_card" => "Карта судьбы",
        "combat" => "Бой",
        _ => "Связанный источник"
    };

    private static string DescribeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "не указано";

        var normalized = value.Trim();
        if (Characteristics.RussianNames.TryGetValue(normalized.ToLowerInvariant(), out var characteristic))
            return characteristic;

        return normalized switch
        {
            "attack_roll" => "бросок атаки",
            "defense_roll" => "бросок защиты",
            "skill_check" => "проверка навыка",
            "saving_throw" => "спасбросок",
            "damage_roll" => "бросок урона",
            "initiative_roll" => "бросок инициативы",
            "advantage" => "преимущество",
            "disadvantage" => "помеха",
            "physical" => "физический урон",
            "magical" => "магический урон",
            "fire" => "огонь",
            "frost" => "мороз",
            "poison" => "яд",
            "bleeding" => "кровотечение",
            "radiant" => "сияние",
            "necrotic" => "некротическая энергия",
            "spiritual" => "духовное воздействие",
            "health" => "здоровье",
            "energy" => "энергия",
            "poise" => "стойкость",
            "durability" => "прочность",
            "charges" => "заряды",
            "ammunition" => "боезапас",
            "spiritual_action_points" => "очки духовного действия",
            "gacha_attempts" => "попытки призыва",
            "blessing_rerolls" => "перебросы благословения",
            "registered_resource_floor" => "нижний предел ресурса",
            "may_reach_zero" => "может достичь нуля",
            "cannot_reduce_below_one" => "не ниже единицы",
            "registered_resource_cap" => "верхний предел ресурса",
            "may_exceed_soft_cap" => "может превысить мягкий предел",
            "cannot_exceed_maximum" => "не выше максимума",
            "movement" => "перемещение",
            "attack" => "атака",
            "defend" => "защита",
            "use_item" => "использование предмета",
            "cast" => "применение способности",
            "interact" => "взаимодействие",
            "escape" => "отступление",
            "grant" => "разрешить",
            "restrict" => "ограничить",
            "forbid" => "запретить",
            "cost_modifier" => "изменить стоимость",
            "owner_turn_start" => "начало хода владельца",
            "owner_turn_end" => "завершение хода владельца",
            "world_turn_start" => "начало мирового хода",
            "world_turn_end" => "завершение мирового хода",
            "owner_damaged" => "владелец получает урон",
            "owner_restored" => "владелец восстанавливает ресурс",
            "owner_action_started" => "владелец начинает действие",
            "owner_action_completed" => "владелец завершает действие",
            "owner_critical_failure" => "владелец получает критический провал",
            "afterlife_exchange_end" => "завершение обмена духовного конфликта",
            "scene_started" => "начало сцены",
            "scene_ended" => "завершение сцены",
            "source_state_changed" => "изменение состояния источника",
            "condition_changed" => "изменение связанного условия",
            "resource_damaged" => "ресурс получает урон",
            "resource_restored" => "ресурс восстанавливается",
            "resource_spent" => "ресурс расходуется",
            "resource_gained" => "ресурс пополняется",
            "resource_depleted" => "ресурс исчерпан",
            "resource_filled" => "ресурс заполнен",
            "apply_definition" => "применить связанный эффект",
            "trigger_component" => "запустить связанный компонент",
            "bounded_receipt" => "получить ограниченный исход",
            "suspend" => "приостановить эффект",
            "expire" => "завершить эффект",
            "no_change" => "сохранить без изменения",
            "remove" => "снять эффект",
            "before_current_event" => "до текущего события",
            "after_current_event" => "после текущего события",
            "after_component" => "после связанного компонента",
            "deterministic" => "автоматически",
            "pain" => "боль",
            "weakness" => "слабость",
            "restricted_movement" => "ограничение движения",
            "infection_risk" => "риск заражения",
            "periodic_damage" => "периодический урон",
            "characteristic_modifier" => "изменение характеристики",
            "roll_modifier" => "изменение броска",
            "action_control" => "ограничение действия",
            "mark" => "метка",
            "ward" => "оберег",
            "burden" => "бремя",
            "opening" => "брешь",
            "vow" => "клятва",
            "player" => "сторона души",
            "opposition" => "противостоящая сторона",
            "pressure" => "давление",
            "counter" => "контрприём",
            "guard" => "защита",
            "maneuver" => "манёвр",
            "binding" => "оковы",
            "break_binding" => "разрыв оков",
            "force_binding" => "принуждение оковами",
            "force_incarnation" => "принуждение к воплощению",
            "incarnation_resistance" => "сопротивление воплощению",
            "champion_coordination" => "координация чемпиона",
            "recover_spiritual_power" => "восстановление духовной силы",
            "withdraw" => "отход",
            "surrender" => "сдача",
            "negotiate" => "переговоры",
            "rollMode" => "режим броска",
            "conflictPosition" => "позиция конфликта",
            "controlState" => "состояние контроля",
            "playerSideStrain" => "напряжение стороны души",
            "oppositionSideStrain" => "напряжение противостоящей стороны",
            "tempoAdvantage" => "преимущество темпа",
            "counterPayoff" => "исход контрприёма",
            "actionCostAudit" => "стоимость действия",
            "actionCostAudit.player" => "стоимость действия души",
            "actionCostAudit.opposition" => "стоимость действия противника",
            "specialArtAudit.effectNote" => "влияние особого искусства",
            "specialArtAudits.effectNote" => "влияния особых искусств",
            "purification" => "очищение",
            "attrition" => "истощение",
            "impose_disadvantage" => "наложить помеху",
            "increase_action_cost" => "повысить стоимость действия",
            "physical_treatment" => "физическое лечение",
            "stop_bleeding" => "остановить кровотечение",
            "active" => "активен",
            "carried" => "при персонаже",
            "equipped" => "экипирован",
            "unlocked" => "открыт",
            _ => normalized.Replace('_', ' ')
        };
    }

    private static string DescribeNumber(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number
            ? FormatNumber(value)
            : "0";

    private static string DescribeSignedNumber(JsonElement root, string field)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Number)
            return "0";
        var formatted = FormatNumber(value);
        return value.TryGetDouble(out var number) && number > 0
            ? "+" + formatted
            : formatted;
    }

    private static string FormatNumber(JsonElement value)
    {
        if (value.TryGetDecimal(out var decimalValue))
            return decimalValue.ToString(CultureInfo.InvariantCulture);
        if (value.TryGetDouble(out var doubleValue) && double.IsFinite(doubleValue))
            return doubleValue.ToString("G17", CultureInfo.InvariantCulture);
        throw new FormatException("Accepted effect contains an unreadable finite number.");
    }

    private static string? ReadString(JsonElement root, string field) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(field, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int ReadInt(JsonElement root, string field) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(field, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var result)
            ? result
            : 0;

    private static long ReadLong(JsonElement root, string field) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(field, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var result)
            ? result
            : 0;

    private static EffectPlayerProjectionResult Unavailable() =>
        new(false, Array.Empty<EffectPlayerEntry>(), UnavailableMessage);

    private sealed record ActionAuthority(
        EffectPlayerAction Action,
        string Operation,
        string AuthorityKind,
        string AuthorityValue);
}
