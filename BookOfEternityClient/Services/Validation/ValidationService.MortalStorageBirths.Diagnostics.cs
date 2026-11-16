using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Diagnostics alone are rebased. Occurrence paths, allocation proofs and routes
    // keep their effective canonical carrier paths throughout admission/publication.
    private static void RemapOffscreenStorageBirthDiagnostics(List<ValidationIssue> issues,
        MortalLocationAcceptedTurnPlan plan, MortalItemCarrierCatalog catalog)
    {
        var births = plan.StorageBirthDrafts.Where(d => d.OperationRef.StartsWith("worldMapUpdates.storagesToAdd[", StringComparison.Ordinal)).ToArray();
        if (births.Length == 0) return;
        var occurrencePaths = new Dictionary<string, (string RawPath, MortalLocationStorageBirthDraft Birth)>(StringComparer.Ordinal);
        foreach (var birth in births)
        {
            if (birth.CloneRawStorage()["contents"] is not JsonArray contents) continue;
            for (var itemIndex = 0; itemIndex < contents.Count; itemIndex++)
            {
                var reference = contents[itemIndex]?["creationRef"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
                if (reference is not null && catalog.ByCreationRef.TryGetValue(reference, out var matches) && matches.Count == 1)
                    occurrencePaths.TryAdd(matches[0].JsonPath, (birth.RawStoragePath + $".contents[{itemIndex}]", birth));
            }
        }
        for (var index = 0; index < issues.Count; index++)
        {
            var issue = issues[index];
            var itemPath = occurrencePaths.Keys.Where(path => issue.FilePath == path || issue.FilePath.StartsWith(path + ".", StringComparison.Ordinal))
                .OrderByDescending(path => path.Length).FirstOrDefault();
            if (itemPath is not null)
            {
                var targets = issue.RepairTargetFiles.Where(path => path != MortalLocationStorageContentsState.StatePath)
                    .Append(MortalLocationMaterializationContract.WorldMapPath).Distinct(StringComparer.Ordinal).ToArray();
                var provenance = occurrencePaths[itemPath];
                var rawPath = provenance.RawPath + issue.FilePath[itemPath.Length..];
                var remapped = CopyStorageBirthIssue(issue, MortalLocationMaterializationContract.WorldMapPath + "." +
                    rawPath, issue.Actor, "MortalLocationMaterialization", targets);
                remapped.MortalItemRepairContext = issue.MortalItemRepairContext is { } context
                    ? context with { RequiredCompanionTargets = context.RequiredCompanionTargets
                        .Where(path => path != MortalLocationStorageContentsState.StatePath)
                        .Append(MortalLocationMaterializationContract.WorldMapPath).Distinct(StringComparer.Ordinal).ToArray() }
                    : null;
                // Reuse the existing exact storage-addition retry owner so an item
                // repair cannot silently switch parent, child identity or raw ordinal.
                remapped.MortalLocationRepairContext = new(provenance.Birth.OperationRef, "mortal_location", null, null,
                    new[] { rawPath[(provenance.Birth.OperationRef.Length + 1)..] }, provenance.Birth.LocationId,
                    ExpectedStorageId: provenance.Birth.StorageId);
                issues[index] = remapped;
                continue;
            }
            var birth = births.FirstOrDefault(d => issue.FilePath == d.RawStoragePath || issue.FilePath.StartsWith(d.RawStoragePath + ".", StringComparison.Ordinal));
            if (birth is null) continue;
            var contextual = CopyStorageBirthIssue(issue, MortalLocationMaterializationContract.WorldMapPath + "." + issue.FilePath,
                "mortal_location:existing:" + birth.LocationId, "MortalLocationMaterialization",
                new[] { MortalLocationMaterializationContract.WorldMapPath });
            contextual.MortalLocationRepairContext = new(birth.OperationRef, "mortal_location", null, null,
                new[] { issue.FilePath[(birth.OperationRef.Length + 1)..] }, birth.LocationId,
                ExpectedStorageId: birth.StorageId);
            issues[index] = contextual;
        }
    }

    private static ValidationIssue CopyStorageBirthIssue(ValidationIssue issue, string path, string? actor,
        string? section, IReadOnlyList<string> targets) => new(path, issue.Severity, issue.Message,
        issue.Code, actor, section, issue.Expected, issue.Actual, issue.RepairHint,
        // Re-infer the input-field category after rebasing from a protected projected
        // companion. Intrinsic client-owned/history codes remain protected by code.
        issue.Category == IssueCategory.ClientOwnedSurface ? null : issue.Category, targets)
    {
        FactionRepairClassification = issue.FactionRepairClassification,
        MortalLocationRepairContext = issue.MortalLocationRepairContext,
        EffectRepairContext = issue.EffectRepairContext,
        WoundRepairContext = issue.WoundRepairContext
    };
}
