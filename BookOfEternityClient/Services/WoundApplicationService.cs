using System.Text.Json.Nodes;
using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Services;

/// <summary>Shared guided treatment workflow. Selection is DATA; actual staging owns the draw and authority.</summary>
internal static partial class WoundApplicationService
{
    internal const string ReadSyntaxMessage = "Выберите /раны или действие «История заживших ран» без дополнительных аргументов.";
    internal const string ActionText = "Самостоятельно лечить выбранную духовную рану вне конфликта. Продолжить обычный безопасный цикл мира.";
    internal const string AidActionText = "Помочь текущему согласившемуся Хранителю вылечить выбранную духовную рану вне конфликта. Продолжить обычный безопасный цикл мира.";
    internal const string ResidentAidActionText = "Помочь согласившемуся жителю текущей Обители Хранителя вылечить выбранную духовную рану вне конфликта. Продолжить обычный безопасный цикл мира.";
    internal const string ReceiveAidActionText = "Принять добровольную безвозмездную помощь текущего Хранителя в лечении моей выбранной духовной раны вне конфликта. Продолжить обычный безопасный цикл мира.";
    internal const string ResidentReceiveAidActionText = "Принять добровольную безвозмездную помощь жителя текущей Обители Хранителя в лечении моей выбранной духовной раны вне конфликта. Продолжить обычный безопасный цикл мира.";
    internal const string CombatActionText = "Лечить выбранную собственную духовную рану стандартным Исцелением в текущей духовной дуэли. Это прерываемое противником действие за ОД, без безопасного цикла мира.";
    internal const string ServiceActionText = "Принять платное лечение моей выбранной духовной раны у Элиары по подтверждённой котировке. Оплатить одну принятую попытку и продолжить один обычный безопасный цикл мира.";
    internal const string FactionServiceActionText = "Принять платное лечение моей выбранной духовной раны у выбранного целителя фракции по подтверждённой котировке. Оплатить одну принятую попытку и продолжить один обычный безопасный цикл мира.";
    internal static string ActionFor(SpiritualSelfTreatmentSelection selection) => selection.Service is { } service
        ? service.Quote.Provider.OwnerKind == "resident" ? FactionServiceActionText : ServiceActionText : selection.Combat is not null ? CombatActionText : selection.Aid is null ? ActionText
        : selection.Aid.Compensation is not null ? "Принять помощь выбранного целителя на отдельно подтверждённых договорных условиях. Выполнить ту же попытку лечения и один безопасный цикл мира, без списания Чернильных Перьев."
        : selection.Aid.Resident is not null ? selection.Aid.ReceiveAid ? ResidentReceiveAidActionText : ResidentAidActionText
        : selection.Aid.ReceiveAid ? ReceiveAidActionText : AidActionText;

    internal static async Task<ExplorerCommandResult> OpenAsync(FileSystemManager fs, string command, bool treatment)
    {
        var read = treatment ? null : ParseReadCommand(command);
        var history = read?.Subcommand?.Id == "history";
        command = read?.CanonicalCommand ?? command;
        await using var lease = await fs.AcquireCanonicalWriteLeaseAsync();
        fs.VerifyCurrentSessionOperation(lease);
        var context = await WoundPlayerReadService.LoadContextAsync(fs, lease);
        if (context is null) return ExplorerWoundCommandResultBuilder.Build(command, WoundPlayerProjection.Unavailable(), null, treatment, history);
        var state = treatment ? await CreateStateAsync(fs, lease, fs.GetOrCreateSessionGeneration(lease), context) : null;
        return ExplorerWoundCommandResultBuilder.Build(command, context.Projection, state, treatment, history,
            showElyaraVisit: context.Input.SelfVisible && (context.Input.Realm is "chaos_sea" or "shining_abode") &&
                ElyaraVisitService.CanShowEntry(context.Input.Carriers.AfterlifeProfiles));
    }

    // Resolve only the existing read command. A link is navigation, never retained DATA
    // or permission; OpenAsync obtains a new coherent context for every invocation.
    internal static ExplorerParsedCommand ParseReadCommand(string command)
    {
        var parsed = ExplorerCommandParser.Parse(command);
        if (!parsed.Success || parsed.Descriptor?.Id != "wounds" || parsed.Arguments.Length != 0 ||
            parsed.Subcommand is not null && parsed.Subcommand.Id != "history")
            throw new InvalidDataException(ReadSyntaxMessage);
        return parsed;
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
        var fresh = await CreateStateAsync(fs, lease, state.Generation, current);
        if (state.SelectedTarget is { } target) _ = WoundTargetSelectionService.RequireCurrent(target, fresh.Targets);
        if (!fresh.Choices.Any(c => c.Available && c.Selection == selected.Selection))
            throw new InvalidDataException("Состояние раны или доступность лечения изменились; откройте /лечить заново.");
        return selected.Selection.Service is { } service
            ? selected.Selection with { Service = service with { PlayerConfirmed = true } }
            : selected.Selection.Aid is { ReceiveAid: true } aid
            ? selected.Selection with { Aid = aid with { PlayerConfirmed = true } } : selected.Selection;
    }

    internal static async Task<ExplorerCommandResult> SelectTargetAsync(FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease, WoundCommandState state, string value, string command)
    {
        fs.VerifyCurrentSessionOperation(lease);
        if (!fs.IsCurrentSessionGeneration(lease, state.Generation))
            throw new InvalidDataException("Игровая сессия изменилась; откройте /лечить заново.");
        var previous = state.ResolveTarget(value); // The old value names its frozen owner, not a new index.
        var context = await WoundPlayerReadService.LoadContextAsync(fs, lease)
            ?? throw new InvalidDataException(WoundPlayerProjection.UnavailableMessage);
        var fresh = await CreateStateAsync(fs, lease, state.Generation, context);
        var target = WoundTargetSelectionService.RequireCurrent(previous, fresh.Targets);
        var projection = target.Owner.OwnerKind is "player" or "player_soul" ? context.Projection
            : WoundPlayerProjection.ProjectKnownTarget(context, target);
        if (!projection.IsAvailable) throw new InvalidDataException(WoundPlayerProjection.UnavailableMessage);
        return ExplorerWoundCommandResultBuilder.Build(command, projection, fresh.ForTarget(target), treatment: true,
            showElyaraVisit: context.Input.SelfVisible && (context.Input.Realm is "chaos_sea" or "shining_abode") &&
                ElyaraVisitService.CanShowEntry(context.Input.Carriers.AfterlifeProfiles));
    }

    // Detached menu DATA only; confirmation and staging revalidate the current source.
    internal static WoundCommandState CreateState(string generation, WoundPlayerReadContext context)
    {
        var input = context.Input;
        if (!input.SelfVisible || input.Realm == "mortal_world")
            return new(generation, []);
        SpiritualCombatTreatmentSelection? combat = null;
        if (context.SpiritualConflict?["activeConflict"] is JsonObject active)
        {
            if (input.Realm != "chaos_sea" || active["exchangeLog"] is not JsonArray log ||
                active["conflictId"] is not JsonValue id || !id.TryGetValue<string>(out var conflictId) ||
                !ResourceMaterializationContract.IsExactIdentifier(conflictId) || log.Count == int.MaxValue)
                return new(generation, []);
            combat = new(conflictId!, SpiritualCombatHealingIntent.ConflictFingerprint(active), log.Count + 1);
            if (!AfterlifeWoundTreatmentAuthority.IsSelectedCombatConflict(active, combat)) return new(generation, []);
        }
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
            var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound)) { Combat = combat };
            var reason = source is null ? "Сейчас невозможно надёжно оценить самостоятельное лечение этой раны."
                : available ? "Без затрат ОД; один обычный цикл мира даже при неудаче."
                : $"Исцеление: {source.HealingTier}. Для снижения раны {wound.Severity.Value} степени требуется ступень {wound.Severity.Rank} или выше. Сейчас доступна только оценка: самостоятельное лечение этой раны недоступно.";
            if (combat is not null && available)
            {
                AfterlifeActionCostRules.TryGetDefinition(AfterlifeActionCostRules.SpiritualHealingOperation, out var cost);
                reason = "Прерываемое действие: " + AfterlifeActionCostRules.ResolveStandardEffectiveCost(cost, source!.HealingTier) +
                    " ОД. При прерывании цена сохраняется; безопасный цикл и естественное восстановление не проходят.";
            }
            choices.Add(new("wound_" + choices.Count, selection,
                WoundPlayerProjection.Text(wound.Display.Name), wound.Severity.Value, source?.HealingTier, available,
                reason));
        }
        return new(generation, choices);
    }

    private static async Task<WoundCommandState> CreateStateAsync(FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease, string generation, WoundPlayerReadContext context)
    {
        var self = CreateState(generation, context);
        if (!context.Input.SelfVisible || context.Input.Realm is not ("chaos_sea" or "shining_abode") || context.SpiritualConflict?["activeConflict"] is not null)
            return self;
        async Task<JsonObject?> Root(string path)
        {
            var bytes = await fs.ReadFileBytesAsync(lease, path);
            return bytes is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                new JsonSerializerOptions(), path);
        }
        var profiles = (context.Input.Carriers.AfterlifeProfiles?["profiles"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        var visibleActors = profiles.Where(p => p["actorType"]?.GetValue<string>() is "guardian" or "resident" &&
            AfterlifeProfileVisibility.IsVisibleToPlayer(p)).ToArray();
        if (visibleActors.Length == 0) return self;
        var candidates = visibleActors.Where(p => p[SpiritualTreatmentParticipation.RecordProperty] is JsonObject).ToArray();
        var navigation = self;
        try
        {
            var guardians = await Root(AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0]);
            JsonObject? residents = null;
            try { residents = await Root(GuardianAbodeResidentState.StatePath); }
            catch (Exception failure) when (context.Input.Realm == "shining_abode" &&
                failure is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
            { /* Optional malformed faction DATA cannot create a provider or change Elyara's dependencies. */ }
            var shining = context.Input.Realm == "shining_abode" ? await Root(ShiningAbodeState.StatePath) : null;
            // Share these parsed locality roots and the caller's existing catalog.
            // Target visibility needs no grant, schedule, attempt or treatment authority.
            navigation = new WoundCommandState(generation, BuildPublicServiceChoices(context, guardians, self.Choices, shining, residents));
            if (context.Input.Realm == "chaos_sea")
                navigation = navigation.WithTargets(WoundTargetSelectionService.Build(context, guardians, residents));
            if (candidates.Length == 0) return navigation;
            var scheduleBytes = await fs.ReadFileBytesAsync(lease, ProgressionScheduleService.SchedulePath);
            if (context.Input.Realm == "chaos_sea" && guardians is null || scheduleBytes is null || fs.FileExists(lease, AcceptedMechanicsPlan.WoundCommandPath) ||
                fs.FileExists(lease, ProgressionScheduleService.ReportPath)) return navigation;
            if (context.Input.Realm == "chaos_sea")
            {
                var currentGuardianId = guardians!["chaosSeaNavigation"]?["currentGuardianId"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(currentGuardianId)) return navigation;
                _ = SpiritualTreatmentParticipation.RequireCurrentGuardianScope(guardians, residents, currentGuardianId);
                candidates = candidates.Where(p => p["actorType"]?.GetValue<string>() == "resident" ||
                    SpiritualTreatmentParticipation.ProfileId(p) == currentGuardianId).ToArray();
                if (candidates.Count(p => p["actorType"]?.GetValue<string>() == "guardian") > 1) return navigation;
            }
            var healer = profiles.SingleOrDefault(p => p["actorType"]?.GetValue<string>() == "player_soul" &&
                SpiritualTreatmentParticipation.ProfileId(p) == "player_soul");
            if (healer is null) return navigation;
            var choices = navigation.Choices.ToList();
            foreach (var target in candidates)
            {
                var record = target[SpiritualTreatmentParticipation.RecordProperty]!.AsObject();
                SpiritualTreatmentParticipation.ValidateRecord(record);
                if (SpiritualTreatmentParticipation.RecordRealm(record) != context.Input.Realm ||
                    context.Input.Realm == "shining_abode" && !SpiritualTreatmentParticipation.IsProviderOffer(record) ||
                    !SpiritualTreatmentParticipation.IsAvailable(record) || SpiritualTreatmentParticipation.HasUnsettledChange(context.Input.Carriers.AfterlifeProfiles)) continue;
                var targetId = SpiritualTreatmentParticipation.ProfileId(target);
                var receiveAid = SpiritualTreatmentParticipation.IsProviderOffer(record);
                if (SpiritualTreatmentParticipation.RecordOwnerId(record) != targetId ||
                    target["actorType"]?.GetValue<string>() != SpiritualTreatmentParticipation.RecordOwner(record).OwnerKind) continue;
                var primary = context.Input.History.Transitions.SingleOrDefault(t => t.TransitionId == record["primaryTransitionId"]!.GetValue<string>());
                if (primary?.TransitionResult is not SpiritualHealingPersistedResult { IsNatural: true } grant ||
                    grant.SchemaVersion != SpiritualTreatmentParticipation.ResultVersion(record) || grant.ParticipationOutcomes is not JsonArray outcomes || outcomes.Count != 1 || !JsonNode.DeepEquals(outcomes[0]?["record"], record)) continue;
                var result = grant.ToCanonicalJson();
                if (result["scheduleAfterFingerprint"]!.GetValue<string>() != new CanonicalBeforeImage(true, scheduleBytes).Fingerprint) continue;
                var wound = context.Catalog.Occurrences.Select(o => o.Wound).SingleOrDefault(w => w.WoundId == record["woundId"]!.GetValue<string>());
                if (wound is not { Lifecycle: "active", Classification.Domain: "spiritual" } ||
                    wound.Owner != SpiritualTreatmentParticipation.RecordTarget(record) ||
                    wound.Display.Visibility is not ("public" or "known_to_player")) continue;
                var aid = SpiritualTreatmentParticipation.Selection(record);
                var scope = SpiritualTreatmentParticipation.RequireAidScope(guardians, residents, context.Input.Carriers.AfterlifeProfiles!, aid, wound, shining);
                if (target["locationId"]?.GetValue<string>() != scope.LocationId ||
                    SpiritualTreatmentParticipation.RecordLocality(record).LocationId != scope.LocationId) continue;
                var source = SpiritualHealingResolver.PrepareSource(new(wound,
                    SpiritualTreatmentParticipation.RecordHealer(record),
                    receiveAid ? target : healer, context.Soul, context.Input.Effects)).Source;
                if (source is null || source.HealingTier < wound.Severity.Rank) continue;
                var name = WoundPlayerProjection.Text(target["name"]?.GetValue<string>() ?? target["displayName"]?.GetValue<string>());
                choices.Add(new("wound_" + choices.Count,
                    new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
                        { Aid = aid },
                    receiveAid ? "Себя: " + WoundPlayerProjection.Text(wound.Display.Name) + (aid.Resident is null ? " — помощь Хранителя" : context.Input.Realm == "shining_abode" ? " — помощь целителя фракции" : " — помощь жителя Обители") + (name.Length == 0 ? "" : " «" + name + "»")
                        : (aid.Resident is not null ? "Житель Обители" : "Хранитель") + (name.Length == 0 ? "" : " «" + name + "»") + ": " + WoundPlayerProjection.Text(wound.Display.Name),
                    wound.Severity.Value, source.HealingTier, true,
                    receiveAid ? aid.Compensation is { } compensation
                        ? "Условия помощи: " + WoundPlayerProjection.Text(compensation.ReadableTerms) + ". Подтвердите отдельно: условия принятой попытки действуют и при неудаче. Один безопасный цикл мира; Чернильные Перья не списываются."
                        : (aid.Resident is null ? "Хранитель" : context.Input.Realm == "shining_abode" ? "Целитель фракции" : "Житель Обители") + " готов помочь бесплатно. Примите помощь подтверждением; один безопасный цикл мира, без затрат ОД."
                        : "Согласие получено на следующий безопасный цикл. Без затрат ОД; перед броском согласие и близость проверяются заново."));
            }
            return new WoundCommandState(generation, choices).WithTargets(navigation.Targets);
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException))
        {
            return navigation; // A read-only target is never permission to skip aid admission.
        }
    }
}
