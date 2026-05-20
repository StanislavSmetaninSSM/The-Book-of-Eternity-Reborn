using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeContractRegistryTests
{
    [Fact]
    public void RegistryPathsMatchMachineReadableInventory()
    {
        var inventoryJson = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "OtherGuides",
            "Afterlife_Pending_Control_Surface_Inventory.json"));
        using var document = JsonDocument.Parse(inventoryJson);
        var inventoryPaths = document.RootElement
            .GetProperty("surfaces")
            .EnumerateArray()
            .Select(surface => surface.GetProperty("path").GetString())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var registryPaths = AfterlifeContractRegistry.All
            .Select(surface => surface.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(inventoryPaths.Order(StringComparer.OrdinalIgnoreCase), registryPaths.Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegistryMarksValidatedClientOwnedAfterlifeSurfaces()
    {
        var paths = new[]
        {
            GuardianAbodeOfferingState.PendingRequestPath,
            GuardianTradeRequestState.PendingRequestPath,
            PlayerGuardianFoundationState.PendingRequestPath,
            NpcTradeRequestState.PendingRequestPath,
            AfterlifeArchiveActionState.ConsultationRequestPath,
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
            ActorSocialInteractionRequestState.PendingGuardianRequestPath,
            ActorSocialInteractionRequestState.PendingNpcRequestPath,
            SystemGuardianLibraryService.AttractionRequestPath,
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            ShiningTradeRequestState.PendingRequestsPath,
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            ShiningFactionRequestState.PendingRealignmentsRequestPath,
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            SourceOfLightCapstoneState.PendingRequestPath,
            AfterlifeReturnGuardService.GuardPath
        };

        foreach (var path in paths)
        {
            Assert.True(
                AfterlifeContractRegistry.IsKnownClientOwnedSurface(path),
                $"{path} must be represented as a client-owned afterlife surface.");
        }
    }
}
