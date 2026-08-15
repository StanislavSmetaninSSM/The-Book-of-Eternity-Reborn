using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    internal async Task<AcceptedMechanicsPlan?> NormalizeAcceptedMechanicsAsync(
        IReadOnlyDictionary<string, string>? backups,
        MortalLocationAcceptedTurnPlan? mortalLocationPlan = null)
    {
        if (!AcceptedMechanicsPlanAuthority.TryPeekValidated(
                _fs,
                out var validatedBinding,
                out var peeked))
        {
            if (!CanonicalFileExists(ResourceMaterializationContract.CommandPath) &&
                !CanonicalFileExists(EffectAcceptedTurnPlan.CommandPath))
            {
                return null;
            }
            throw new InvalidDataException(
                "Accepted mechanics publication requires one validated common plan.");
        }
        if (_writeLease == null)
        {
            throw new InvalidOperationException(
                "Accepted mechanics publication requires the owning canonical write lease.");
        }
        if (!peeked.Success || peeked.Plan == null)
            throw new InvalidDataException("Validated accepted mechanics plan is incomplete.");

        var plan = peeked.Plan;
        try
        {
            await ValidateAcceptedMechanicsBeforeImagesAsync(plan);
            if (plan.EffectPlan != null)
                await ValidateEffectPlanPublicationBindingAsync(plan.EffectPlan);
        }
        catch
        {
            AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs);
            throw;
        }
        if (!AcceptedMechanicsPlanAuthority.TryTakeValidated(
                _fs,
                validatedBinding,
                out var taken) ||
            !taken.Success || taken.Plan == null ||
            !ReferenceEquals(plan, taken.Plan))
        {
            throw new InvalidDataException(
                "Accepted mechanics validated handoff changed before publication.");
        }

        var writes = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            [ResourceMaterializationContract.DefinitionsPath] = plan.DefinitionAfterImage,
            [ResourceMaterializationContract.StatePath] = plan.StateAfterImage,
            [ResourceMaterializationContract.HistoryPath] = plan.HistoryAfterImage,
            [EffectAcceptedTurnPlan.IdentityIndexPath] = plan.EffectIdentityAfterImage
        };
        foreach (var pair in plan.OwnerCompanionAfterImages)
            writes[pair.Key] = pair.Value;
        foreach (var pair in plan.EffectCarrierAfterImages)
            writes[pair.Key] = pair.Value;
        foreach (var pair in plan.PendingAfterImages)
        {
            if (pair.Value != null)
                writes[pair.Key] = pair.Value;
        }

        foreach (var pair in writes.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            await WriteCanonicalFileAtomicAsync(
                pair.Key,
                pair.Value.ToJsonString(JsonOpts));
        }
        foreach (var path in plan.PendingAfterImages
                     .Where(static pair => pair.Value == null)
                     .Select(static pair => pair.Key)
                     .Concat(plan.ConsumedPaths)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(static path => path, StringComparer.Ordinal))
        {
            _fs.DeleteFile(_writeLease, path);
        }

        await ValidatePublishedResourceAfterImagesAsync(plan);
        return plan;
    }

    private async Task ValidateAcceptedMechanicsBeforeImagesAsync(
        AcceptedMechanicsPlan plan)
    {
        foreach (var pair in plan.BeforeImages.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            var current = await _fs.ReadFileBytesAsync(_writeLease!, pair.Key);
            var exact = pair.Value.Existed == (current != null) &&
                        (pair.Value.Bytes == null
                            ? current == null
                            : current != null && pair.Value.Bytes.AsSpan().SequenceEqual(current));
            if (!exact)
            {
                AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs);
                throw new InvalidDataException(
                    $"Accepted mechanics authority at '{pair.Key}' changed after validation.");
            }
        }
    }

    private async Task ValidatePublishedResourceAfterImagesAsync(
        AcceptedMechanicsPlan plan)
    {
        var definitionsJson = await ReadExactPublishedAfterImageAsync(
            ResourceMaterializationContract.DefinitionsPath,
            plan.DefinitionAfterImage);
        var stateJson = await ReadExactPublishedAfterImageAsync(
            ResourceMaterializationContract.StatePath,
            plan.StateAfterImage);
        var historyJson = await ReadExactPublishedAfterImageAsync(
            ResourceMaterializationContract.HistoryPath,
            plan.HistoryAfterImage);
        if (_fs.FileExists(
                _writeLease!,
                ResourceMaterializationContract.CommandPath))
        {
            throw new InvalidDataException(
                "Accepted mechanics left the consumed resource command root published.");
        }

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            definitionsJson,
            allowMissingPristine: false);
        if (definitions.Catalog == null || definitions.Issues.Count != 0)
            throw InvalidPublishedResource(definitions.Issues);
        var state = ResourceStateContract.ParseCanonical(
            stateJson,
            definitions.Catalog,
            allowMissingPristine: false);
        var history = ResourceHistoryState.ParseCanonical(
            historyJson,
            definitions.Catalog,
            allowMissingPristine: false);
        var issues = state.Issues.Concat(history.Issues).ToList();
        if (state.Ledger != null && history.History != null)
        {
            issues.AddRange(history.History.ValidateStateAgreement(state.Ledger));
            issues.AddRange(plan.OwnerAuthority.ValidateCanonicalAgreement(
                state.Ledger,
                history.History));
        }
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            throw InvalidPublishedResource(issues);
    }

    private async Task<string> ReadExactPublishedAfterImageAsync(
        string path,
        JsonObject plannedAfterImage)
    {
        var current = await _fs.ReadFileBytesAsync(_writeLease!, path);
        var expectedJson = plannedAfterImage.ToJsonString(JsonOpts);
        var body = Encoding.UTF8.GetBytes(expectedJson);
        var preamble = Encoding.UTF8.GetPreamble();
        var expected = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, expected, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, expected, preamble.Length, body.Length);
        if (current == null || !current.AsSpan().SequenceEqual(expected))
        {
            throw new InvalidDataException(
                $"Accepted mechanics publication at '{path}' differs from its validated after-image.");
        }
        return expectedJson;
    }

    private static InvalidDataException InvalidPublishedResource(
        IReadOnlyList<ValidationIssue> issues)
    {
        var issue = issues.FirstOrDefault();
        return new InvalidDataException(issue == null
            ? "Accepted mechanics produced an invalid resource after-image."
            : $"Accepted mechanics produced invalid resource state: {issue.Code} at {issue.FilePath}.");
    }
}
