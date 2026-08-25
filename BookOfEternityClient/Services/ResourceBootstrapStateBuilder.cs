namespace BookOfEternityClient.Services;

internal sealed record ResourceBootstrapStateResult(
    ResourceDefinitionCatalog? Definitions,
    ResourceStateLedger? State,
    ResourceHistoryState? History,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid =>
        Definitions != null && State != null && History != null && Issues.Count == 0;
}

internal static class ResourceBootstrapStateBuilder
{
    private const string Realm = "mortal_world";
    private const string PlayerId = "player_current";

    internal static ResourceBootstrapStateResult BuildPristine()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var history = ResourceHistoryState.ParseCanonical(
            "{\"schemaVersion\":1,\"entries\":[]}",
            definitions,
            allowMissingPristine: false);
        return history.IsValid && history.History != null
            ? new ResourceBootstrapStateResult(
                definitions,
                new ResourceStateLedger(Array.Empty<ResourceStateEntry>()),
                history.History,
                Array.Empty<ValidationIssue>())
            : new ResourceBootstrapStateResult(null, null, null, history.Issues);
    }

    internal static ResourceBootstrapStateResult BuildMortalPlayer(
        int incarnationNumber,
        int turn,
        int permanentStrength,
        int permanentConstitution,
        int permanentIntelligence,
        int permanentWisdom,
        int permanentFaith)
    {
        var pristine = BuildPristine();
        if (!pristine.IsValid || pristine.Definitions == null ||
            pristine.State == null || pristine.History == null)
        {
            return pristine;
        }
        return BuildMortalPlayer(
            pristine.Definitions,
            pristine.State,
            pristine.History,
            incarnationNumber,
            turn,
            permanentStrength,
            permanentConstitution,
            permanentIntelligence,
            permanentWisdom,
            permanentFaith);
    }

    internal static ResourceBootstrapStateResult BuildMortalPlayer(
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger existingState,
        ResourceHistoryState existingHistory,
        int incarnationNumber,
        int turn,
        int permanentStrength,
        int permanentConstitution,
        int permanentIntelligence,
        int permanentWisdom,
        int permanentFaith)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(existingState);
        ArgumentNullException.ThrowIfNull(existingHistory);
        if (incarnationNumber <= 0 || turn <= 0 ||
            permanentStrength < 0 || permanentConstitution < 0 ||
            permanentIntelligence < 0 || permanentWisdom < 0 || permanentFaith < 0)
        {
            return Failure(
                "resource_bootstrap_input_invalid",
                "positive incarnation/turn and non-negative permanent characteristics",
                $"incarnation={incarnationNumber};turn={turn}");
        }
        var existingAgreement = existingHistory.ValidateStateAgreement(existingState);
        if (existingAgreement.Count != 0)
        {
            return new ResourceBootstrapStateResult(
                null,
                null,
                null,
                existingAgreement);
        }

        var owner = new ResourceFormulaOwner(
            Realm,
            ResourceOwnerKind.Player,
            PlayerId);
        var ownerFingerprint = CreateOwnerFingerprint(
            incarnationNumber,
            permanentStrength,
            permanentConstitution,
            permanentIntelligence,
            permanentWisdom,
            permanentFaith);
        var formulaInputs = new Dictionary<string, ResourceFormulaInput>(StringComparer.Ordinal)
        {
            ["health"] = new PlayerHealthCapacityFormulaInput(
                owner,
                ownerFingerprint,
                permanentStrength,
                permanentConstitution),
            ["energy"] = new PlayerEnergyCapacityFormulaInput(
                owner,
                ownerFingerprint,
                permanentConstitution,
                permanentIntelligence,
                permanentWisdom,
                permanentFaith),
            ["poise"] = new PlayerPoiseCapacityFormulaInput(
                owner,
                ownerFingerprint,
                permanentStrength,
                permanentConstitution,
                permanentIntelligence,
                permanentWisdom)
        };
        var issues = new List<ValidationIssue>();
        var transitions = new List<ResourceCapacityIntent>();
        var ordinal = 0;
        foreach (var resourceKey in new[] { "health", "energy", "poise" })
        {
            ordinal++;
            if (!definitions.TryResolveExact(resourceKey, out var definition) ||
                definition == null)
            {
                issues.Add(Issue(
                    "resource_bootstrap_definition_missing",
                    "sealed built-in player resource definition",
                    resourceKey));
                continue;
            }

            var coordinate = new ResourceCoordinate(
                Realm,
                ResourceOwnerKind.Player,
                PlayerId,
                resourceKey);
            var capacity = ResolvedResourceCapacity.Resolve(
                definition,
                coordinate,
                new RegisteredFormulaCapacityInput(formulaInputs[resourceKey]),
                instanceAuthorityKey: null,
                includeInitialization: true);
            if (!capacity.IsValid || capacity.Capacity == null)
            {
                issues.AddRange(capacity.Issues);
                continue;
            }

            var eventRef = $"turn_{turn}:resource_bootstrap:{ordinal}";
            var sourceId = $"mortal_incarnation_{incarnationNumber}";
            transitions.Add(new ResourceCapacityIntent(
                eventRef,
                OriginKind: "bootstrap_materialization",
                OriginId: sourceId,
                coordinate,
                ResourceCapacityOperation.Initialize,
                capacity.Capacity,
                ResourceCurrentDisposition.InitializeFromDefinition,
                ResourceMutationPhase.RegisteredSystemOutcome,
                Priority: 40,
                new ResourceSourceEvidence(
                    "bootstrap_materialization",
                    sourceId,
                    ownerFingerprint),
                capacity.Capacity.Initialization!.AuthorityFingerprint,
                ReceiptId: null));
        }
        if (issues.Count != 0)
            return new ResourceBootstrapStateResult(null, null, null, issues);

        var sources = ResourceMutationSourceCatalog.Create(
            Array.Empty<ResourceMutationSourceExport>());
        if (!sources.IsValid || sources.Catalog == null)
            return new ResourceBootstrapStateResult(null, null, null, sources.Issues);
        var planned = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                turn,
                definitions,
                existingState,
                existingHistory,
                sources.Catalog,
                Array.Empty<ResourceMutationIntent>(),
                transitions),
            new AcceptedMechanicsIdentityFactory());
        return planned.IsValid && planned.StateAfterImage != null &&
               planned.HistoryAfterImage != null
            ? new ResourceBootstrapStateResult(
                definitions,
                planned.StateAfterImage,
                planned.HistoryAfterImage,
                Array.Empty<ValidationIssue>())
            : new ResourceBootstrapStateResult(null, null, null, planned.Issues);
    }

    private static string CreateOwnerFingerprint(
        int incarnationNumber,
        int strength,
        int constitution,
        int intelligence,
        int wisdom,
        int faith)
    {
        using var fingerprint = new ResourceFingerprintBuilder(
            "mortal-player-bootstrap-owner-v1");
        fingerprint.Append(Realm);
        fingerprint.Append(PlayerId);
        fingerprint.Append(incarnationNumber);
        fingerprint.Append(strength);
        fingerprint.Append(constitution);
        fingerprint.Append(intelligence);
        fingerprint.Append(wisdom);
        fingerprint.Append(faith);
        return fingerprint.Build();
    }

    private static ResourceBootstrapStateResult Failure(
        string code,
        string expected,
        string actual) =>
        new(null, null, null, new[] { Issue(code, expected, actual) });

    private static ValidationIssue Issue(
        string code,
        string expected,
        string actual) =>
        new(
            ResourceMaterializationContract.StatePath,
            IssueSeverity.Error,
            "Mortal resource bootstrap could not produce one exact initial ledger.",
            code: code,
            section: "resource_bootstrap",
            expected: expected,
            actual: actual,
            repairHint: "Restore the validated permanent-characteristic state and retry the incarnation bootstrap.");
}
