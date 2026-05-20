using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
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

    [Fact]
    public void RegistryClientOwnedSurfacesAreAcceptedByValidationFilter()
    {
        var method = typeof(ValidationService).GetMethod(
            "IsClientOwnedSurfaceValidationPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        foreach (var surface in AfterlifeContractRegistry.All.Where(surface => surface.IsKnownClientOwnedSurface))
        {
            var isClientOwned = Assert.IsType<bool>(method.Invoke(null, new object[] { surface.Path }));
            Assert.True(isClientOwned, $"{surface.Path} must be covered by the validation client-owned surface filter.");
        }
    }

    [Fact]
    public void RuntimePendingControlSurfaceStringsAreRegisteredOrExplicitlyExcluded()
    {
        using var inventory = LoadInventory();
        var registeredOrExcludedFileNames = inventory.RootElement
            .GetProperty("surfaces")
            .EnumerateArray()
            .Select(surface => Path.GetFileName(surface.GetProperty("path").GetString()))
            .Concat(inventory.RootElement
                .GetProperty("intentionalExclusions")
                .EnumerateArray()
                .Select(exclusion => Path.GetFileName(exclusion.GetProperty("path").GetString()?.TrimEnd('*').TrimEnd('/'))))
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var runtimePendingFiles = Directory
            .EnumerateFiles(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"pending_[A-Za-z0-9_]+\.json"))
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var pendingFile in runtimePendingFiles)
            Assert.Contains(pendingFile, registeredOrExcludedFileNames);
    }

    [Fact]
    public void InventoryDocAnchorsResolveToExistingRepoFiles()
    {
        using var inventory = LoadInventory();
        var anchors = inventory.RootElement
            .GetProperty("surfaces")
            .EnumerateArray()
            .SelectMany(surface => surface.GetProperty("docAnchors").EnumerateArray())
            .Select(anchor => anchor.GetString())
            .Where(anchor => !string.IsNullOrWhiteSpace(anchor))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(anchors);

        foreach (var anchor in anchors)
        {
            var path = anchor!.Split('#', 2)[0].Replace('/', Path.DirectorySeparatorChar);
            Assert.True(
                File.Exists(Path.Combine(TestRepoPaths.RepoRoot, path)),
                $"Inventory doc anchor points to missing file: {anchor}");
        }
    }

    [Fact]
    public void StatusAuditCoversRegistryPendingContractsAndSkipsEmptyRequestWrappers()
    {
        var field = typeof(ExplorerMode).GetField(
            "AfterlifePendingContractDefinitions",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        var definitions = Assert.IsAssignableFrom<IEnumerable<object>>(field.GetValue(null));
        var statusAuditPaths = definitions
            .Select(definition => definition.GetType().GetProperty("Path")?.GetValue(definition)?.ToString())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredStatusPaths = AfterlifeContractRegistry.All
            .Where(surface => surface.IsKnownClientOwnedSurface)
            .Where(surface =>
            {
                var fileName = Path.GetFileName(surface.Path);
                return fileName.StartsWith("pending_", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(surface.Path, SystemGuardianLibraryService.AttractionRequestPath, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(surface.Path, AfterlifeReturnGuardService.GuardPath, StringComparison.OrdinalIgnoreCase);
            })
            .Select(surface => surface.Path)
            .ToArray();

        foreach (var path in requiredStatusPaths)
            Assert.Contains(path, statusAuditPaths);

        var statusAuditSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.Afterlife.StatusAudit.cs"));
        Assert.Contains("requests.Count == 0", statusAuditSource, StringComparison.Ordinal);
        Assert.Contains("continue;", ExtractStatusAuditEmptyRequestsBlock(statusAuditSource), StringComparison.Ordinal);
    }

    private static JsonDocument LoadInventory()
    {
        var inventoryJson = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "OtherGuides",
            "Afterlife_Pending_Control_Surface_Inventory.json"));
        return JsonDocument.Parse(inventoryJson);
    }

    private static string ExtractStatusAuditEmptyRequestsBlock(string source)
    {
        const string marker = "if (requests.Count == 0)";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing status-audit empty requests guard: {marker}");

        var end = source.IndexOf("for (var i = 0;", start, StringComparison.Ordinal);
        Assert.True(end > start, "Missing request iteration after empty requests guard.");

        return source[start..end];
    }
}
