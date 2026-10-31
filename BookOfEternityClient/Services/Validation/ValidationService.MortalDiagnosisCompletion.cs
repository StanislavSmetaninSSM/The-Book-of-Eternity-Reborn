using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
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
