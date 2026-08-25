using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static class AcceptedMechanicsAuthorityTestProbe
{
    internal sealed record CommonHandoff(
        AcceptedMechanicsPlanBinding Binding,
        AcceptedMechanicsPlanningResult Result);

    internal sealed record EffectHandoff(
        EffectAcceptedTurnPlanBinding Binding,
        EffectAcceptedTurnPlanningResult Result);

    internal static async Task<bool> HasCommonAsync(FileSystemManager fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        return AcceptedMechanicsPlanAuthority.HasValidated(fileSystem, writeLease);
    }

    internal static async Task<CommonHandoff?> PeekCommonAsync(FileSystemManager fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        return AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fileSystem,
            writeLease,
            out var binding,
            out var result)
            ? new CommonHandoff(binding, result)
            : null;
    }

    internal static async Task<EffectHandoff?> PeekEffectAsync(FileSystemManager fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        return EffectAcceptedTurnPlanAuthority.TryPeekValidated(
            fileSystem,
            writeLease,
            out var binding,
            out var result)
            ? new EffectHandoff(binding, result)
            : null;
    }

    internal static async Task<string?> GetAllocatedItemIdAsync(
        FileSystemManager fileSystem,
        string sessionId,
        string snapshotToken,
        string creationRef)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        return MortalItemAcceptedTurnAuthority.TryGetAllocatedItemId(
            fileSystem,
            writeLease,
            sessionId,
            snapshotToken,
            creationRef,
            out var itemId)
            ? itemId
            : null;
    }

    internal static async Task<bool> HasItemsAsync(FileSystemManager fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        return MortalItemAcceptedTurnAuthority.HasValidatedItems(
            fileSystem,
            writeLease);
    }

    internal static async Task InvalidateItemsAsync(FileSystemManager fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        MortalItemAcceptedTurnAuthority.InvalidateValidatedItems(fileSystem, writeLease);
    }

    internal static async Task RegisterItemsAsync(
        FileSystemManager fileSystem,
        string sessionId,
        string snapshotToken,
        MortalItemCarrierCatalog catalog,
        IEnumerable<string> knownItemIds)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        MortalItemAcceptedTurnAuthority.RegisterValidatedItems(
            fileSystem,
            writeLease,
            sessionId,
            snapshotToken,
            catalog,
            knownItemIds);
    }

    internal static async Task<IReadOnlyList<EffectSourceExport>> GetItemEffectSourcesAsync(
        FileSystemManager fileSystem,
        string sessionId,
        string snapshotToken)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        return MortalItemAcceptedTurnAuthority.GetValidatedEffectSources(
            fileSystem,
            writeLease,
            sessionId,
            snapshotToken);
    }
}
