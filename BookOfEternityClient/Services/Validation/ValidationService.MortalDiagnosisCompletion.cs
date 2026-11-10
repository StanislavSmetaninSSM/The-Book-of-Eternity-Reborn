using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Applies complete existing definition and registered-owner validation even
    /// when no current effect instantiates a malformed source.
    /// </summary>
    internal static IReadOnlyList<ValidationIssue> ValidateMortalDiagnosisEffectSources(
        IReadOnlyDictionary<string, JsonNode?> roots)
    {
        var issues = new List<ValidationIssue>();
        ValidateEffectSourceDefinitions(roots, issues);
        issues.AddRange(EffectAcceptedTurnInputComposer.ValidateRegisteredSourceOwners(roots));
        return issues;
    }

    /// <summary>
    /// Applies the existing canonical item/index/companion checks to one already
    /// parsed current read. It neither reads files nor issues item authority.
    /// </summary>
    internal static IReadOnlyList<ValidationIssue> ValidateMortalDiagnosisCurrentItems(
        MortalItemCarrierCatalog catalog, MortalItemIdentityParseResult identity)
    {
        var issues = new List<ValidationIssue>(identity.Issues);
        AddCatalogIssues(catalog, issues);
        foreach (var occurrence in catalog.Occurrences)
            AddContractIssues(occurrence, MortalItemMaterializationPhase.CanonicalPostSeal, issues);
        ValidateCanonicalCatalogAgainstIndex(catalog, identity, issues);
        ValidateMortalItemCompanionReferences(catalog, MortalItemMaterializationPhase.CanonicalPostSeal, issues);
        return issues;
    }
}
