using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundTreatmentAuthority
{
    internal const string ElyaraId = "guard_system_elyara_001";
    internal const string ElyaraAbodeId = "abode_system_elyara_001";
    internal static IReadOnlyList<string> ServiceLocalityPaths { get; } = Array.AsReadOnly(new[] { AidGuardiansPath });
    private sealed record ServiceSourceData(SelectedWoundSourceData Selected,
        AfterlifeHealingServiceQuote Quote, IReadOnlyList<SpiritualWoundRecoveryMember> Members);
    internal sealed record ServiceTurnStageResult(ProgressionControl? Control, SpiritualSelfTreatmentStageResult Stage);

    // The existing source reader owns one parsed DATA set. Control changes only
    // its schedule member; all other original images close freshly before entropy.
    internal static async Task<ServiceTurnStageResult> StagePlayerServiceTurnAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, ProgressionScheduleService scheduler,
        string sessionId, string requestId, int turn, SpiritualSelfTreatmentSelection selection,
        Func<int, int[]>? drawDice = null)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        ProgressionControl? control = null;
        try
        {
            selection = SpiritualSelfTreatmentSelection.Parse(selection.ToJson());
            if (selection is not { Service.PlayerConfirmed: true, Aid: null, Combat: null } ||
                !ResourceMaterializationContract.IsExactIdentifier(sessionId) ||
                !ResourceMaterializationContract.IsExactIdentifier(requestId) || turn < 1)
                return new(null, Refuse("service_context_invalid"));
            var pending = await ReadSourceAsync(files, lease, AcceptedMechanicsPlan.WoundCommandPath);
            if (pending.Image.Existed && (pending.Root is not { } existing || String(existing["sessionId"]) != sessionId ||
                String(existing["requestId"]) != requestId || Integer(existing["turn"]) != turn))
                return new(null, Refuse("pending_conflict"));
            var sources = new List<SourceFile>();
            foreach (var path in SourcePaths.Concat(ServiceLocalityPaths))
                sources.Add(await ReadSourceAsync(files, lease, path));
            var prepared = PrepareServiceSelectionSource(selection, sources, turn);
            await RequireAidReadImagesCurrentAsync(files, lease, sources.Append(pending));
            if (pending.Root is { } retained)
            {
                if (!SpiritualSelfTreatmentRequestCodec.IsServiceHeader(retained)) return new(null, Refuse("pending_conflict"));
                control = retained["control"]?.Deserialize<ProgressionControl>() ?? throw new InvalidDataException("Service retry lacks its retained control DATA.");
                // Building a new control would rewrite schedule.LastUpdatedUtc
                // and change the original source fingerprint after the draw.
                // BindPreparedServiceSource and IsExactActive re-prove the old
                // complete control against the current prepared cycle/images.
            }
            else
            {
                control = await scheduler.BuildControlForNextTurnAsync(lease, activeTurnRealm: null);
                var schedule = await ReadSourceAsync(files, lease, ProgressionScheduleService.SchedulePath);
                await RequireAidReadImagesCurrentAsync(files, lease, sources.Append(pending), ProgressionScheduleService.SchedulePath);
                sources[sources.FindIndex(s => s.Path == schedule.Path)] = schedule;
            }
            var context = new SpiritualSelfTreatmentStageContext(sessionId, requestId, turn, control);
            var data = BindPreparedServiceSource(context, selection, sources, prepared);
            return new(control, await StageAdmittedActiveTreatmentAsync(files, lease, context,
                selection, sources, pending, data, drawDice));
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        { return new(control, Refuse("service_admission_invalid")); }
    }

    internal static SourceResult PrepareServiceSourceData(SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, IReadOnlyList<SourceFile> sources)
    {
        var prepared = PrepareServiceSelectionSource(selection, sources, context.Turn);
        return new(BindPreparedServiceSource(context, selection, sources, prepared), null);
    }

    private static ServiceSourceData PrepareServiceSelectionSource(SpiritualSelfTreatmentSelection selection,
        IReadOnlyList<SourceFile> sources, int turn)
    {
        if (selection is not { Service.PlayerConfirmed: true, Aid: null, Combat: null })
            throw new InvalidDataException("Service admission requires actual player confirmation.");
        var quote = AfterlifeHealingServiceContract.ParseQuote(selection.Service.Quote.ToJson());
        JsonObject? Root(string path) => sources.Single(s => s.Path == path).Root;
        var soul = Root(SoulPath);
        if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(String(soul?["currentRealm"]), out var realm) || realm != "chaos_sea" ||
            Root(AfterlifeSpiritualConflictState.StatePath)?["activeConflict"] is not null ||
            SpiritualTreatmentParticipation.HasUnsettledChange(Root(WoundCarrierCatalog.AfterlifeProfilesPath)))
            throw new InvalidDataException("Public treatment requires the current safe Chaos realm.");
        var scope = RequireServiceScope(Root(AidGuardiansPath) ?? throw new InvalidDataException("Missing service locality."),
            Root(WoundCarrierCatalog.AfterlifeProfilesPath) ?? throw new InvalidDataException("Missing service profiles."), quote);
        var selected = PrepareSelectedWoundSource(selection, sources, realm, quote.Provider, requireWholeCohortLineage: true).Data
            ?? throw new InvalidDataException("Public treatment lost its exact current wound, healer or effect lineage.");
        var actor = selected.Carriers.AfterlifeProfiles!["profiles"]!.AsArray().OfType<JsonObject>().Single(p =>
            WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, quote.Provider));
        var profile = AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(quote.Provider, actor)!;
        var current = AfterlifeHealingServiceContract.CreateQuote(profile, selected.Wound, selected.Source, scope.LocationId);
        if (current != quote || !GuardianPolicyContracts.TryPrepareHealingFeatherWallet(soul!, quote.Amount, out _))
            throw new InvalidDataException("Displayed service terms, actual check or available Feathers changed.");
        var members = SpiritualHealingRequestProjection.ReadNaturalMembers(selected.Catalog, selected.Carriers.AfterlifeProfiles, soul, realm);
        if (members.Any(m => !selected.Identities.TryGetEntry(m.Wound.WoundId, out var identity) || identity.CreatedAtTurn >= turn))
            throw new InvalidDataException("Public treatment requires the complete earlier accepted wound cohort.");
        return new(selected, quote, members);
    }

    internal static LocalInteractionScope RequireServiceScope(JsonObject guardians, JsonObject profiles,
        AfterlifeHealingServiceQuote quote)
    {
        if (quote.Provider != new WoundOwnerCoordinate("chaos_sea", "guardian", ElyaraId, WoundCarrierCatalog.AfterlifeProfilesPath))
            throw new InvalidDataException("This executable service route is the current public Elyara route.");
        var scope = SpiritualTreatmentParticipation.RequireCurrentGuardianScope(guardians, null, ElyaraId);
        var matching = (profiles["profiles"] as JsonArray)?.OfType<JsonObject>()
            .Where(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, quote.Provider)).ToArray();
        if (matching is not { Length: 1 } ||
            AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(quote.Provider, matching[0]) is not { } current ||
            current.Fingerprint != quote.ServiceFingerprint || current.LocationRef != scope.LocationId || quote.LocationRef != scope.LocationId)
            throw new InvalidDataException("Elyara is no longer alive, visible, available and co-located at the quoted public terms.");
        return scope;
    }

    private static SourceData BindPreparedServiceSource(SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection, IReadOnlyList<SourceFile> sources, ServiceSourceData prepared)
    {
        JsonObject? Root(string path) => sources.Single(s => s.Path == path).Root;
        var realm = "chaos_sea";
        if (context.Progression.AfterlifeCatchupRequired ||
            !AfterlifeEntityProfileState.TryNormalizeEffectRealm(context.Progression.CurrentRealm, out var controlRealm) || controlRealm != realm ||
            !ProgressionScheduleService.TryBindPreparedSpiritualCycle(Root(ProgressionScheduleService.SchedulePath),
                sources.Single(s => s.Path == EffectAcceptedTurnInputComposer.WorldTimePath).Image.Bytes,
                Root(ShiningAbodeState.StatePath), context.Progression, realm, out var ordinal))
            throw new InvalidDataException("Service has no exact single prepared safe cycle.");
        var cycle = CycleKey(realm, ordinal);
        if (prepared.Members.Any(m => m.Wound.Recovery.LastTickKey == cycle) || prepared.Selected.History.Transitions.Any(t =>
                t.WoundId == selection.WoundId && t.AttemptId is not null && t.CycleKey == cycle))
            throw new InvalidDataException("Service would repeat an accepted attempt or natural cycle.");
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "spiritual_public_service_source", "7", context.SessionId, context.RequestId, context.Turn.ToString(CultureInfo.InvariantCulture),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(context.Progression)), prepared.Quote.Fingerprint
        }.Concat(SourcePaths.Concat(ServiceLocalityPaths).SelectMany(path =>
        {
            var image = sources.Single(s => s.Path == path).Image;
            return new[] { path, image.Fingerprint };
        })));
        var payment = AfterlifeHealingServicePlanner.PreparePayment(prepared.Quote,
            SpiritualSelfTreatmentRequestCodec.ServiceAttemptId(context, prepared.Quote, realm, ordinal, fingerprint), Root(SoulPath)!)
            ?? throw new InvalidDataException("Service payment no longer matches its original strict wallet.");
        var selected = prepared.Selected;
        return new(selected.Wound, selected.Source, realm, ordinal, fingerprint, selected.Carriers, selected.Effects,
            selected.Identities, selected.History, selected.Catalog, prepared.Members,
            ServiceQuote: prepared.Quote, ServicePayment: payment);
    }
}
