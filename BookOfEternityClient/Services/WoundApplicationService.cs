using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Services;

/// <summary>Shared guided self workflow. Selection is DATA; actual staging owns the draw and authority.</summary>
internal static class WoundApplicationService
{
    internal const string ActionText = "Самостоятельно лечить выбранную духовную рану вне конфликта. Продолжить обычный безопасный цикл мира.";

    internal static async Task<ExplorerCommandResult> OpenAsync(FileSystemManager fs, string command, bool treatment)
    {
        await using var lease = await fs.AcquireCanonicalWriteLeaseAsync();
        fs.VerifyCurrentSessionOperation(lease);
        var context = await WoundPlayerReadService.LoadContextAsync(fs, lease);
        if (context is null) return ExplorerWoundCommandResultBuilder.Build(command, WoundPlayerProjection.Unavailable(), null, treatment);
        var state = CreateState(fs.GetOrCreateSessionGeneration(lease), context);
        return ExplorerWoundCommandResultBuilder.Build(command, context.Projection, state, treatment);
    }

    internal static async Task<SpiritualSelfTreatmentSelection> ConfirmAsync(FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease, WoundCommandState state, string value)
    {
        fs.VerifyCurrentSessionOperation(lease);
        if (!fs.IsCurrentSessionGeneration(lease, state.Generation))
            throw new InvalidDataException("Игровая сессия изменилась; откройте /лечить заново.");
        var selected = state.Resolve(value);
        var current = await WoundPlayerReadService.LoadContextAsync(fs, lease)
            ?? throw new InvalidDataException(WoundPlayerProjection.UnavailableMessage);
        var choices = CreateState(state.Generation, current).Choices;
        if (!choices.Any(c => c.Available && c.Selection == selected.Selection))
            throw new InvalidDataException("Состояние раны или доступность лечения изменились; откройте /лечить заново.");
        return selected.Selection;
    }

    private static WoundCommandState CreateState(string generation, WoundPlayerReadContext context)
    {
        var input = context.Input;
        if (!input.SelfVisible || input.Realm == "mortal_world" || context.SpiritualConflict?["activeConflict"] is not null)
            return new(generation, []);
        var profiles = (input.Carriers.AfterlifeProfiles?["profiles"] as JsonArray)?.OfType<JsonObject>()
            .Where(p => p["actorType"]?.GetValue<string>() == "player_soul" && p["actorId"]?.GetValue<string>() == "player_soul").ToArray();
        if (profiles is not { Length: 1 }) return new(generation, []);
        var choices = new List<WoundTreatmentChoice>();
        foreach (var wound in context.Catalog.Occurrences.Select(o => o.Wound).Where(w =>
            w.Lifecycle == "active" && w.Classification.Domain == "spiritual" && w.Owner.Realm == input.Realm &&
            w.Owner.OwnerKind == "player_soul" && w.Owner.OwnerId == "player_soul" &&
            w.Owner.CarrierPath == WoundCarrierCatalog.AfterlifeProfilesPath && w.Display.Visibility is "public" or "known_to_player")
            .OrderBy(w => w.Origin.CreatedAtTurn).ThenBy(w => w.WoundId, StringComparer.Ordinal))
        {
            var source = SpiritualHealingResolver.PrepareSource(new(wound, wound.Owner, profiles[0], context.Soul, input.Effects)).Source;
            var available = source is not null && source.HealingTier >= wound.Severity.Rank;
            choices.Add(new("wound_" + choices.Count, new(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound)),
                WoundPlayerProjection.Text(wound.Display.Name), wound.Severity.Value, source?.HealingTier ?? 0, available,
                available ? "Без затрат ОД; один обычный цикл мира даже при неудаче." : "Требуется освоенная ступень Исцеления не ниже степени раны; V степень этим способом не лечится."));
        }
        return new(generation, choices);
    }
}
