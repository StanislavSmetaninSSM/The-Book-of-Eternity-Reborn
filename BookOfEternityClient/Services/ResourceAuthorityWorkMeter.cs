namespace BookOfEternityClient.Services;

internal sealed class ResourceAuthorityWorkMeter
{
    internal long DefinitionDescriptorVisitCount { get; private set; }
    internal long DefinitionConstructionVisitCount { get; private set; }
    internal long DefinitionSortComparisonCount { get; private set; }
    internal long DefinitionIndexVisitCount { get; private set; }
    internal long DefinitionMaterializationVisitCount { get; private set; }
    internal long DefinitionCatalogBuildCount { get; private set; }

    internal long OwnerDescriptorVisitCount { get; private set; }
    internal long OwnerCapabilityVisitCount { get; private set; }
    internal long OwnerConstructionVisitCount { get; private set; }
    internal long OwnerSortComparisonCount { get; private set; }
    internal long OwnerFingerprintVisitCount { get; private set; }

    internal long StateDescriptorVisitCount { get; private set; }
    internal long StateConstructionVisitCount { get; private set; }
    internal long StateSortComparisonCount { get; private set; }
    internal long StateIndexVisitCount { get; private set; }
    internal long StateFingerprintVisitCount { get; private set; }

    internal long InitialHistoryInputVisitCount { get; private set; }
    internal long InitialHistoryValidationVisitCount { get; private set; }
    internal long InitialHistoryChainVisitCount { get; private set; }
    internal long InitialHistorySortComparisonCount { get; private set; }
    internal long InitialHistoryIndexVisitCount { get; private set; }
    internal long InitialHistoryFingerprintVisitCount { get; private set; }

    internal long StateAgreementHistoryVisitCount { get; private set; }
    internal long StateAgreementStateVisitCount { get; private set; }
    internal long StateAgreementSortComparisonCount { get; private set; }

    internal long OwnerTerminalStateIndexVisitCount { get; private set; }
    internal long OwnerTerminalLookupCount { get; private set; }
    internal long OwnerTerminalEntryVisitCount { get; private set; }

    internal long DefinitionWorkUnits =>
        DefinitionDescriptorVisitCount +
        DefinitionConstructionVisitCount +
        DefinitionSortComparisonCount +
        DefinitionIndexVisitCount +
        DefinitionMaterializationVisitCount +
        DefinitionCatalogBuildCount;

    internal long OwnerWorkUnits =>
        OwnerDescriptorVisitCount +
        OwnerCapabilityVisitCount +
        OwnerConstructionVisitCount +
        OwnerSortComparisonCount +
        OwnerFingerprintVisitCount;

    internal long StateWorkUnits =>
        StateDescriptorVisitCount +
        StateConstructionVisitCount +
        StateSortComparisonCount +
        StateIndexVisitCount +
        StateFingerprintVisitCount;

    internal long InitialHistoryWorkUnits =>
        InitialHistoryInputVisitCount +
        InitialHistoryValidationVisitCount +
        InitialHistoryChainVisitCount +
        InitialHistorySortComparisonCount +
        InitialHistoryIndexVisitCount +
        InitialHistoryFingerprintVisitCount;

    internal long StateAgreementWorkUnits =>
        StateAgreementHistoryVisitCount +
        StateAgreementStateVisitCount +
        StateAgreementSortComparisonCount;

    internal long OwnerTerminalCleanupWorkUnits =>
        OwnerTerminalStateIndexVisitCount +
        OwnerTerminalLookupCount +
        OwnerTerminalEntryVisitCount;

    internal void VisitDefinitionDescriptor() => DefinitionDescriptorVisitCount++;
    internal void VisitDefinitionConstruction() => DefinitionConstructionVisitCount++;
    internal void CompareDefinitions() => DefinitionSortComparisonCount++;
    internal void VisitDefinitionIndex() => DefinitionIndexVisitCount++;
    internal void VisitDefinitionMaterialization() =>
        DefinitionMaterializationVisitCount++;
    internal void BuildDefinitionCatalog() => DefinitionCatalogBuildCount++;

    internal void VisitOwnerDescriptor() => OwnerDescriptorVisitCount++;
    internal void VisitOwnerCapability() => OwnerCapabilityVisitCount++;
    internal void VisitOwnerConstruction(long count = 1) => OwnerConstructionVisitCount += count;
    internal void CompareOwners() => OwnerSortComparisonCount++;
    internal void VisitOwnerFingerprint() => OwnerFingerprintVisitCount++;

    internal void VisitStateDescriptor() => StateDescriptorVisitCount++;
    internal void VisitStateConstruction() => StateConstructionVisitCount++;
    internal void CompareStateEntries() => StateSortComparisonCount++;
    internal void VisitStateIndex() => StateIndexVisitCount++;
    internal void VisitStateFingerprint() => StateFingerprintVisitCount++;

    internal void VisitInitialHistoryInput(long count = 1) =>
        InitialHistoryInputVisitCount += count;
    internal void VisitInitialHistoryValidation() => InitialHistoryValidationVisitCount++;
    internal void VisitInitialHistoryChain() => InitialHistoryChainVisitCount++;
    internal void CompareInitialHistory() => InitialHistorySortComparisonCount++;
    internal void VisitInitialHistoryIndex(long count = 1) =>
        InitialHistoryIndexVisitCount += count;
    internal void VisitInitialHistoryFingerprint() => InitialHistoryFingerprintVisitCount++;

    internal void VisitStateAgreementHistory(long count = 1) =>
        StateAgreementHistoryVisitCount += count;
    internal void VisitStateAgreementState() => StateAgreementStateVisitCount++;
    internal void CompareStateAgreementHistory() => StateAgreementSortComparisonCount++;

    internal void VisitOwnerTerminalStateIndex() =>
        OwnerTerminalStateIndexVisitCount++;
    internal void LookupOwnerTerminal() => OwnerTerminalLookupCount++;
    internal void VisitOwnerTerminalEntry() => OwnerTerminalEntryVisitCount++;
}

internal sealed class ResourceAuthorityCountingComparer<T> : IComparer<T>
{
    private readonly IComparer<T> _inner;
    private readonly Action _countComparison;

    internal ResourceAuthorityCountingComparer(
        IComparer<T> inner,
        Action countComparison)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _countComparison = countComparison ??
            throw new ArgumentNullException(nameof(countComparison));
    }

    public int Compare(T? left, T? right)
    {
        _countComparison();
        return _inner.Compare(left!, right!);
    }
}
