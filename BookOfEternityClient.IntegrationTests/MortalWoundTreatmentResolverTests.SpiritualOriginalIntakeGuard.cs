using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    /// <summary>
    /// Keeps a real treatment resource reservation protected when a separate spiritual item registration has been revoked.
    /// </summary>
    /// <returns>
    /// A task completing after both the read-only intake probe and real capture preserve the exact reservation and files.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualIntake_RevokedPrivateItemsCannotBypassResourceOnlyHold()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        _ = PersistAndRehydrateResourcePublication(
            fixture, scenario, "spiritual_intake_revoked_items_resource_hold");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(fixture.FileSystem, fixture.Lease));
        var reservation = Assert.Single(ReadTreatmentResourceAgreements(fixture)).Value;
        var reservationState = reservation.State;
        var reservationFingerprint = reservation.AgreementFingerprint;
        var requestFingerprint = reservation.PersistedRequestFingerprint;
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundItemIdentityFactory(journal, new MortalItemIdentityFactory());
        var inventory = new JsonObject
        {
            ["items"] = new JsonArray(MortalItemTestFixture.CreateCanonicalRoot("itm_revoked_guard"))
        };
        var catalog = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            inventory, null, null, null, null, new Dictionary<string, JsonObject>()));
        Assert.Empty(catalog.Issues);
        var roots = new Dictionary<string, JsonNode?> { [InventoryEquipmentService.ItemsPath] = inventory };
        MortalItemAcceptedTurnAuthority.RegisterValidatedItems(fixture.FileSystem, fixture.Lease,
            "revoked_item_session", "revoked_item_snapshot", catalog, ["itm_revoked_guard"],
            MortalItemRouteAuthorityCatalog.CreateFrozen(new Dictionary<string, MortalItemRouteAuthority>()),
            null, roots, roots, factory, "revoked_item_request", 42);
        Assert.Single(MortalItemAcceptedTurnAuthority.GetValidatedOwners(fixture.FileSystem, fixture.Lease,
            "revoked_item_session", "revoked_item_snapshot", factory));
        journal.Invalidate();
        Assert.False(factory.IsHealthy);
        Assert.Empty(MortalItemAcceptedTurnAuthority.GetValidatedOwners(fixture.FileSystem, fixture.Lease,
            "revoked_item_session", "revoked_item_snapshot", factory));
        var before = CaptureResolverFixtureTree(fixture.Root);

        Assert.False(AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
            fixture.FileSystem, fixture.Lease, new MortalItemIdentityFactory(), checkPlansAndItems: true));
        var validator = new ValidationService(fixture.FileSystem, NullLogger<ValidationService>.Instance);
        var rejected = await validator.CaptureSpiritualOriginalTurnWithIntakeAsync(fixture.Lease);

        Assert.Null(rejected.Capture);
        Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_original_intake_claim_conflict");
        Assert.Same(reservation, Assert.Single(ReadTreatmentResourceAgreements(fixture)).Value);
        Assert.Equal(reservationState, reservation.State);
        Assert.Equal(reservationFingerprint, reservation.AgreementFingerprint);
        Assert.Equal(requestFingerprint, reservation.PersistedRequestFingerprint);
        Assert.False(ReadTreatmentResourceReservationRegistry(fixture).IsEmpty);
        Assert.Equal(10, ReadPlayerEnergy(fixture));
        AssertResolverFixtureTreeUnchanged(fixture.Root, before);
    }

    /// <summary>
    /// Preserves a resource-only reservation and rejects its later finalized publication claim.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualIntake_ResourceOnlyHoldRejectsBeforeComposition()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture, scenario, "spiritual_intake_resource_only_hold");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem, fixture.Lease));
        Assert.False(ReadTreatmentResourceReservationRegistry(fixture).IsEmpty);
        var before = CaptureResolverFixtureTree(fixture.Root);
        var validator = new ValidationService(
            fixture.FileSystem, NullLogger<ValidationService>.Instance);

        var rejected = await validator.CaptureSpiritualOriginalTurnWithIntakeAsync(fixture.Lease);

        Assert.Null(rejected.Capture);
        Assert.Contains(rejected.Issues,
            issue => issue.Code == "spiritual_original_intake_claim_conflict");
        AssertResolverFixtureTreeUnchanged(fixture.Root, before);
        var plan = ComposeResourcePublication(fixture, flow);
        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        publication.CompleteAtFullPipelineEnd();
        Assert.Equal(8, ReadPlayerEnergy(fixture));
        var finalizedTree = CaptureResolverFixtureTree(fixture.Root);

        var finalizedRejected = await validator.CaptureSpiritualOriginalTurnWithIntakeAsync(fixture.Lease);

        Assert.Null(finalizedRejected.Capture);
        Assert.Contains(finalizedRejected.Issues,
            issue => issue.Code == "spiritual_original_intake_claim_conflict");
        AssertResolverFixtureTreeUnchanged(fixture.Root, finalizedTree);
    }

    /// <summary>
    /// Preserves a real held-treatment publication when named original intake is rejected.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualIntake_OpenHeldTreatmentRejectsWithoutRevokingPublication()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "spiritual_intake_open_held_treatment");
        var plan = ComposeResourcePublication(fixture, flow);
        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        var publishedTree = CaptureResolverFixtureTree(fixture.Root);
        var validator = new ValidationService(
            fixture.FileSystem,
            NullLogger<ValidationService>.Instance);

        var rejected = await validator.CaptureSpiritualOriginalTurnWithIntakeAsync(fixture.Lease);

        Assert.Null(rejected.Capture);
        Assert.Contains(rejected.Issues,
            issue => issue.Code == "spiritual_original_intake_claim_conflict");
        AssertResolverFixtureTreeUnchanged(fixture.Root, publishedTree);
        Assert.Equal(8, ReadPlayerEnergy(fixture));
        publication.CompleteAtFullPipelineEnd();
        Assert.Equal(8, ReadPlayerEnergy(fixture));
    }
}
