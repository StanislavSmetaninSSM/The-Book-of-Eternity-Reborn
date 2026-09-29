using System.Text.Json;
using System.Text.Json.Nodes;
using SpiritualPublicationReceipt = BookOfEternityClient.Services.ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationReceipt;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task ValidateEffectPlanPublicationBindingAsync(
        EffectAcceptedTurnPlan plan,
        bool normalizedAcceptedCarrierBaselines,
        AcceptedMechanicsWoundStageBundle? woundStageBundle,
        bool allowDirectWoundBootstrap,
        MortalWoundTreatmentResourcePublicationAuthority?
            resourcePublicationAuthority,
        SpiritualPublicationReceipt? spiritualPublicationReceipt = null)
    {
        var liveCarriers = await ReadEffectPublicationCarriersAsync();
        var expectedCarrierFingerprint = normalizedAcceptedCarrierBaselines
            ? EffectCarrierCatalog.CreateAuthorityFingerprint(
                plan.AcceptedCarrierBaselines)
            : plan.CarrierAuthorityFingerprint;
        if (spiritualPublicationReceipt is not null)
        {
            RequireCurrentSpiritualPublication(spiritualPublicationReceipt, spiritualPublicationReceipt.Plan);
            if (!ReferenceEquals(spiritualPublicationReceipt.Plan.EffectPlan, plan))
                throw StaleEffectPlan("spiritual completed effect plan");
            await ValidateSpiritualPublicationCarriersAsync(spiritualPublicationReceipt, liveCarriers);
        }
        else if (!string.Equals(
                expectedCarrierFingerprint,
                EffectCarrierCatalog.CreateAuthorityFingerprint(liveCarriers),
                StringComparison.Ordinal))
        {
            throw StaleEffectPlan("effect carrier catalog");
        }
        if (!normalizedAcceptedCarrierBaselines && spiritualPublicationReceipt is null)
        {
            foreach (var (path, beforeImage) in plan.CarrierBeforeImages)
            {
                if (!JsonNode.DeepEquals(
                        beforeImage,
                        GetEffectCarrierRoot(liveCarriers, path)))
                {
                    throw StaleEffectPlan(path);
                }
            }
        }

        var liveIdentityIndex = await ReadEffectPublicationNodeAsync(
            EffectAcceptedTurnPlan.IdentityIndexPath);
        if (!JsonNode.DeepEquals(
                plan.IdentityIndexBeforeImage,
                liveIdentityIndex) &&
            !(allowDirectWoundBootstrap && liveIdentityIndex is null &&
              IsPristineEffectIdentityIndex(plan.IdentityIndexBeforeImage)) &&
            !(spiritualPublicationReceipt is not null && liveIdentityIndex is null &&
              spiritualPublicationReceipt.Authority.PublicationInputs.TryGetValue(
                  EffectAcceptedTurnPlan.IdentityIndexPath, out var originalIndex) && !originalIndex.Existed &&
              IsPristineEffectIdentityIndex(plan.IdentityIndexBeforeImage)))
        {
            throw StaleEffectPlan(EffectAcceptedTurnPlan.IdentityIndexPath);
        }

        var afterImages = plan.CarrierAfterImages.ToDictionary(
            static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        if (spiritualPublicationReceipt is not null)
        {
            foreach (var pair in spiritualPublicationReceipt.Plan.EffectCarrierAfterImages
                         .Concat(spiritualPublicationReceipt.Plan.WoundCarrierAfterImages)
                         .Concat(spiritualPublicationReceipt.Plan.OwnerCompanionAfterImages))
                afterImages[pair.Key] = pair.Value;
        }
        var effectiveCarriers = new EffectCarrierCatalogInput(
            AfterImageOrLive(EffectCarrierCatalog.PlayerPath, liveCarriers.PlayerEffects),
            AfterImageOrLive(EffectCarrierCatalog.NpcPath, liveCarriers.NpcEffects),
            AfterImageOrLive(EffectCarrierCatalog.EnemiesPath, liveCarriers.EnemyCombatants),
            AfterImageOrLive(EffectCarrierCatalog.AlliesPath, liveCarriers.AllyCombatants),
            AfterImageOrLive(EffectCarrierCatalog.AfterlifeProfilesPath, liveCarriers.AfterlifeProfiles),
            AfterImageOrLive(EffectCarrierCatalog.SpiritualConflictPath, liveCarriers.SpiritualConflict));
        var sourceRoots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var path in EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
        {
            sourceRoots[path] = spiritualPublicationReceipt is null
                ? await ReadEffectPublicationNodeAsync(path)
                : await ReadSpiritualFinalSourceAsync(spiritualPublicationReceipt.Plan, path);
        }

        var catalog = EffectCarrierCatalog.Build(effectiveCarriers);
        var sourceAuthority =
            EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
                sourceRoots,
                woundStageBundle?.PreparedPlan);
        if (spiritualPublicationReceipt is not null)
        {
            ValidateSpiritualWoundPublicationSources(spiritualPublicationReceipt, sourceRoots);
            sourceAuthority = spiritualPublicationReceipt.Authority.ComposePublicationSourceAuthority(sourceAuthority, plan);
        }
        var targetAuthority =
            EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(
                effectiveCarriers,
                sourceRoots);
        if (!string.Equals(
                plan.SourceAuthorityFingerprint,
                sourceAuthority.CanonicalFingerprint,
                StringComparison.Ordinal) &&
            !(allowDirectWoundBootstrap &&
              plan.SourceAuthority.IsCanonicalPublicationSubsetOf(
                  sourceAuthority)) &&
            resourcePublicationAuthority?.ProvesNormalizedEffectSourceTransition(
                plan.SourceAuthorityFingerprint,
                sourceAuthority.CanonicalFingerprint) != true)
        {
            throw new InvalidDataException("Effect authority at 'effect source authority catalog' changed after effect validation: " +
                plan.SourceAuthority.DescribeCanonicalDifferences(sourceAuthority));
        }
        if (!string.Equals(
                plan.TargetAuthorityFingerprint,
                targetAuthority.CanonicalFingerprint,
                StringComparison.Ordinal) &&
            !(allowDirectWoundBootstrap &&
              plan.TargetAuthority.IsCanonicalPublicationSubsetOf(
                  targetAuthority)))
        {
            throw StaleEffectPlan("effect target authority catalog");
        }
        var issues = new List<ValidationIssue>();
        issues.AddRange(catalog.Issues);
        issues.AddRange(sourceAuthority.Issues);
        issues.AddRange(targetAuthority.Issues);
        issues.AddRange(targetAuthority.ValidateNamedCombatantBindings(
            effectiveCarriers));
        using var plannedIndexDocument = JsonDocument.Parse(
            plan.IdentityIndexAfterImage.ToJsonString());
        var plannedIndex = EffectIdentityState.Parse(
            plannedIndexDocument.RootElement,
            EffectAcceptedTurnPlan.IdentityIndexPath);
        issues.AddRange(plannedIndex.Issues);
        if (plannedIndex.State != null)
        {
            ValidationService.ValidateEffectCarrierIndexAgreement(
                catalog,
                plannedIndex.State,
                issues);
        }
        ValidationService.ValidateCanonicalEffectBindings(
            catalog,
            sourceAuthority,
            targetAuthority,
            issues);

        foreach (var effect in plan.ActiveEffects)
        {
            var source = effect["source"] as JsonObject;
            var target = effect["target"] as JsonObject;
            var realm = ReadExactEffectString(effect["realm"]);
            var sourceKind = ReadExactEffectString(source?["kind"]);
            var sourceId = ReadExactEffectString(source?["sourceId"]);
            var definitionKey = ReadExactEffectString(source?["definitionKey"]);
            var targetKind = ReadExactEffectString(target?["kind"]);
            if (realm == null || sourceKind == null || sourceId == null ||
                definitionKey == null || targetKind == null)
            {
                continue;
            }

            var key = new EffectSourceKey(
                realm,
                sourceKind,
                sourceId,
                definitionKey);
            var expectedBinding = plan.SourceBindings.SingleOrDefault(
                binding => binding.Key == key);
            var currentBinding = sourceAuthority.ResolveCanonicalBinding(
                key,
                targetKind);
            if (expectedBinding != null &&
                currentBinding.Source != null &&
                !JsonNode.DeepEquals(
                    expectedBinding.Definition,
                    currentBinding.Source.Definition))
            {
                issues.Add(new ValidationIssue(
                    "effectChanges.source",
                    IssueSeverity.Error,
                    "Effect source definition changed after accepted effect validation.",
                    code: "effect_plan_source_changed_after_validation",
                    section: "effect_materialization",
                    expected: expectedBinding.Definition.ToJsonString(),
                    actual: currentBinding.Source.Definition.ToJsonString()));
            }
        }

        var firstError = issues.FirstOrDefault(
            static issue => issue.Severity == IssueSeverity.Error);
        if (firstError != null)
        {
            throw new InvalidDataException(
                $"Effect authority at '{firstError.FilePath}' changed after effect validation: " +
                $"{firstError.Code}; expected={firstError.Expected}; actual={firstError.Actual}.");
        }

        JsonObject? AfterImageOrLive(string path, JsonObject? live) =>
            afterImages.TryGetValue(path, out var afterImage)
                ? afterImage.DeepClone().AsObject()
                : live;
    }

    private static bool IsPristineEffectIdentityIndex(JsonObject? root)
    {
        if (root is null)
            return false;
        try
        {
            using var document = JsonDocument.Parse(root.ToJsonString());
            var parsed = EffectIdentityState.Parse(
                document.RootElement,
                EffectAcceptedTurnPlan.IdentityIndexPath);
            return parsed.State is not null && parsed.Issues.Count == 0 &&
                   parsed.State.Entries.Count == 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<EffectCarrierCatalogInput> ReadEffectPublicationCarriersAsync() =>
        new(
            await ReadEffectPublicationObjectAsync(EffectCarrierCatalog.PlayerPath),
            await ReadEffectPublicationObjectAsync(EffectCarrierCatalog.NpcPath),
            await ReadEffectPublicationObjectAsync(EffectCarrierCatalog.EnemiesPath),
            await ReadEffectPublicationObjectAsync(EffectCarrierCatalog.AlliesPath),
            await ReadEffectPublicationObjectAsync(EffectCarrierCatalog.AfterlifeProfilesPath),
            await ReadEffectPublicationObjectAsync(EffectCarrierCatalog.SpiritualConflictPath));

    private async Task<JsonObject?> ReadEffectPublicationObjectAsync(string path)
    {
        var node = await ReadEffectPublicationNodeAsync(path);
        return node == null
            ? null
            : node as JsonObject ?? throw StaleEffectPlan(path);
    }

    private async Task<JsonNode?> ReadEffectPublicationNodeAsync(string path)
    {
        var json = await ReadCanonicalFileAsync(path);
        if (json == null)
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Null)
                throw StaleEffectPlan(path);
            EnsureEffectPublicationJsonHasUniqueProperties(
                document.RootElement,
                path);
            return JsonNode.Parse(document.RootElement.GetRawText());
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException(
                $"Effect authority at '{path}' changed after effect validation.",
                exception);
        }
    }

    private static void EnsureEffectPublicationJsonHasUniqueProperties(
        JsonElement element,
        string path,
        bool commandRoot = false)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    if (commandRoot)
                    {
                        throw new InvalidDataException(
                            $"Effect command root contains a duplicate JSON property at '{path}.{property.Name}'.");
                    }
                    throw StaleEffectPlan(path + "." + property.Name);
                }
                EnsureEffectPublicationJsonHasUniqueProperties(
                    property.Value,
                    path + "." + property.Name,
                    commandRoot);
            }
            return;
        }

        if (element.ValueKind != JsonValueKind.Array)
            return;
        var index = 0;
        foreach (var item in element.EnumerateArray())
            EnsureEffectPublicationJsonHasUniqueProperties(
                item,
                $"{path}[{index++}]",
                commandRoot);
    }

    private static JsonObject? GetEffectCarrierRoot(
        EffectCarrierCatalogInput carriers,
        string path) => path switch
    {
        EffectCarrierCatalog.PlayerPath => carriers.PlayerEffects,
        EffectCarrierCatalog.NpcPath => carriers.NpcEffects,
        EffectCarrierCatalog.EnemiesPath => carriers.EnemyCombatants,
        EffectCarrierCatalog.AlliesPath => carriers.AllyCombatants,
        EffectCarrierCatalog.AfterlifeProfilesPath => carriers.AfterlifeProfiles,
        EffectCarrierCatalog.SpiritualConflictPath => carriers.SpiritualConflict,
        _ => throw StaleEffectPlan(path)
    };

    private static InvalidDataException StaleEffectPlan(string path) =>
        new($"Effect authority at '{path}' changed after effect validation.");

    private static string? ReadExactEffectString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) &&
        text.Length > 0 && string.Equals(text, text.Trim(), StringComparison.Ordinal)
            ? text
            : null;

}
