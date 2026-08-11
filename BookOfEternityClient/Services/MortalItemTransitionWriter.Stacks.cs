using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalItemTransitionWriter
{
    private async Task<MortalItemTransitionResult> ExecuteStackMutationAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalItemTransitionIntent intent,
        MortalItemTransitionMutation? mutation)
    {
        if (mutation != null)
        {
            return MortalItemTransitionResult.Failed(
                "Стековый переход не принимает несвязанную дополнительную мутацию.");
        }

        var intentError = ValidateStackMutationIntent(intent);
        if (intentError != null)
            return MortalItemTransitionResult.Failed(intentError);

        LoadedState state;
        try
        {
            state = await LoadAsync(writeLease, additionalObjectPaths: null);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return MortalItemTransitionResult.Failed(exception.Message);
        }

        var beforeCatalog = BuildCatalog(state);
        var beforeIndex = MortalItemIdentityState.Parse(state.IdentityIndexJson);
        var baselineError = ValidateComposedState(beforeCatalog, beforeIndex);
        if (baselineError != null)
            return MortalItemTransitionResult.Failed(baselineError);

        return intent.Kind switch
        {
            MortalItemTransitionKind.Split => await ExecuteSplitAsync(
                writeLease,
                intent,
                state,
                beforeCatalog,
                beforeIndex),
            MortalItemTransitionKind.Merge => await ExecuteMergeAsync(
                writeLease,
                intent,
                state,
                beforeCatalog,
                beforeIndex),
            MortalItemTransitionKind.Destroy => await ExecuteDestroyAsync(
                writeLease,
                intent,
                state,
                beforeCatalog,
                beforeIndex),
            _ => MortalItemTransitionResult.Failed("Неподдерживаемый стековый переход.")
        };
    }

    private async Task<MortalItemTransitionResult> ExecuteSplitAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalItemTransitionIntent intent,
        LoadedState state,
        MortalItemCarrierCatalog beforeCatalog,
        MortalItemIdentityParseResult beforeIndex)
    {
        var carrier = intent.SourceCarrier!;
        var items = ResolveCarrierArray(state, carrier, createIfMissing: false, out var carrierError);
        if (items == null)
            return MortalItemTransitionResult.Failed(carrierError!);

        var sourceId = intent.SourceItemIds[0];
        var source = ResolveActiveItem(
            beforeCatalog,
            beforeIndex,
            items,
            carrier,
            sourceId,
            out var sourceError);
        if (source == null)
            return MortalItemTransitionResult.Failed(sourceError!);
        if (intent.Quantity >= source.Quantity)
        {
            return MortalItemTransitionResult.Failed(
                $"Для разделения требуется количество от 1 до {source.Quantity - 1}.");
        }

        var childId = NewUniqueItemId(beforeCatalog, beforeIndex);
        var parentReceiptBefore = source.Item[MortalItemMaterializationContract.ReceiptProperty]!.DeepClone();
        source.Item["count"] = source.Quantity - intent.Quantity;

        var child = source.Item.DeepClone().AsObject();
        child["itemId"] = childId;
        child["existedId"] = childId;
        child["count"] = intent.Quantity;
        child.Remove("id");
        child.Remove("initialId");
        child.Remove("creationRef");
        var childReceipt = MortalItemIdentityState.CreateSplitReceipt(
            source.Item,
            childId,
            intent.Turn);
        child[MortalItemMaterializationContract.ReceiptProperty] = childReceipt;

        using (var childDocument = JsonDocument.Parse(child.ToJsonString()))
        {
            var childIssues = MortalItemMaterializationContract.Validate(
                childDocument.RootElement,
                "local_mortal_item_split",
                MortalItemMaterializationPhase.CanonicalPostSeal);
            if (childIssues.Count > 0)
                return MortalItemTransitionResult.Failed(childIssues[0].Message);
        }
        items.Add(child);

        var currentIndex = MortalItemIdentityState.Parse(beforeIndex.Root.DeepClone());
        var currentParentEntry = currentIndex.EntriesByItemId[sourceId];
        var carrierNode = CreateCarrierNode(carrier);
        MortalItemIdentityState.AppendTransition(
            currentParentEntry,
            MortalItemIdentityState.CreateTransition(
                "split",
                intent.Turn,
                new[] { sourceId },
                carrierNode,
                carrierNode,
                source.Quantity,
                source.Quantity - intent.Quantity,
                intent.AuthorityKind,
                intent.AuthorityId));

        currentIndex.Root["entries"]!.AsArray().Add(new JsonObject
        {
            ["itemId"] = childId,
            ["receiptId"] = childReceipt["receiptId"]!.DeepClone(),
            ["state"] = "active",
            ["currentCarrier"] = carrierNode.DeepClone(),
            ["originMaterializationIds"] = currentParentEntry["originMaterializationIds"]!.DeepClone(),
            ["parentItemIds"] = new JsonArray(sourceId),
            ["mergedIntoItemId"] = null,
            ["transitions"] = new JsonArray(
                MortalItemIdentityState.CreateTransition(
                    "split",
                    intent.Turn,
                    new[] { sourceId },
                    carrierNode,
                    carrierNode,
                    source.Quantity,
                    intent.Quantity,
                    intent.AuthorityKind,
                    intent.AuthorityId))
        });

        if (!JsonNode.DeepEquals(
                parentReceiptBefore,
                source.Item[MortalItemMaterializationContract.ReceiptProperty]))
        {
            return MortalItemTransitionResult.Failed(
                "Разделение попыталось изменить root receipt исходной стопки.");
        }

        var normalizedIndex = MortalItemIdentityState.Parse(currentIndex.Root);
        var validationError = ValidateMutationResult(
            state,
            beforeIndex,
            normalizedIndex,
            childId,
            carrier);
        if (validationError != null)
            return MortalItemTransitionResult.Failed(validationError);

        var committed = await CommitSingleCarrierAsync(
            writeLease,
            state,
            carrier,
            normalizedIndex.Root);
        return committed
            ? new MortalItemTransitionResult(
                true,
                sourceId,
                childId,
                "Стопка разделена с сохранением происхождения.")
            : MortalItemTransitionResult.Failed(
                "Игровое состояние изменилось во время разделения; исходные данные сохранены.");
    }

    private async Task<MortalItemTransitionResult> ExecuteMergeAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalItemTransitionIntent intent,
        LoadedState state,
        MortalItemCarrierCatalog beforeCatalog,
        MortalItemIdentityParseResult beforeIndex)
    {
        var carrier = intent.SourceCarrier!;
        var items = ResolveCarrierArray(state, carrier, createIfMissing: false, out var carrierError);
        if (items == null)
            return MortalItemTransitionResult.Failed(carrierError!);

        var sourceIds = intent.SourceItemIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var resolved = new Dictionary<string, ActiveItem>(StringComparer.Ordinal);
        foreach (var sourceId in sourceIds)
        {
            var active = ResolveActiveItem(
                beforeCatalog,
                beforeIndex,
                items,
                carrier,
                sourceId,
                out var sourceError);
            if (active == null)
                return MortalItemTransitionResult.Failed(sourceError!);
            resolved.Add(sourceId, active);
        }

        var survivorId = intent.SurvivorItemId!;
        var survivor = resolved[survivorId];
        var survivorProjection = CreateStackSemanticProjection(survivor.Item);
        foreach (var sourceId in sourceIds)
        {
            if (!MortalItemMaterializationContract.ImmutableEvidenceEquals(
                    survivorProjection,
                    CreateStackSemanticProjection(resolved[sourceId].Item)))
            {
                return MortalItemTransitionResult.Failed(
                    "Объединяемые стопки различаются по управляемой семантике предмета.");
            }
        }

        foreach (var sourceId in sourceIds)
        {
            if (HasUnsafeCompanionReference(beforeCatalog, sourceId))
            {
                return MortalItemTransitionResult.Failed(
                    "Стопка имеет equipment, container, quest, bond или другой companion и не может быть поглощена объединением.");
            }
        }

        var totalQuantityLong = resolved.Values.Sum(item => (long)item.Quantity);
        if (totalQuantityLong > int.MaxValue || totalQuantityLong != intent.Quantity)
        {
            return MortalItemTransitionResult.Failed(
                "Итоговое количество объединения устарело или выходит за допустимый диапазон.");
        }
        var totalQuantity = (int)totalQuantityLong;
        var survivorReceiptBefore = survivor.Item[MortalItemMaterializationContract.ReceiptProperty]!.DeepClone();
        survivor.Item["count"] = totalQuantity;

        var removalIndices = resolved
            .Where(pair => !string.Equals(pair.Key, survivorId, StringComparison.Ordinal))
            .Select(pair => items.IndexOf(pair.Value.Item))
            .OrderByDescending(index => index)
            .ToArray();
        if (removalIndices.Any(index => index < 0))
            return MortalItemTransitionResult.Failed("Одна из объединяемых стопок изменилась до записи.");
        foreach (var index in removalIndices)
            items.RemoveAt(index);

        var currentIndex = MortalItemIdentityState.Parse(beforeIndex.Root.DeepClone());
        var currentSurvivorEntry = currentIndex.EntriesByItemId[survivorId];
        var origins = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var sourceId in sourceIds)
        {
            foreach (var origin in currentIndex.EntriesByItemId[sourceId]["originMaterializationIds"]!
                         .AsArray()
                         .Select(node => ReadExactString(node))
                         .Where(value => value != null))
            {
                origins.Add(origin!);
            }
        }
        currentSurvivorEntry["originMaterializationIds"] = new JsonArray(
            origins.Select(origin => (JsonNode?)JsonValue.Create(origin)).ToArray());
        var carrierNode = CreateCarrierNode(carrier);
        MortalItemIdentityState.AppendTransition(
            currentSurvivorEntry,
            MortalItemIdentityState.CreateTransition(
                "merge",
                intent.Turn,
                sourceIds,
                carrierNode,
                carrierNode,
                survivor.Quantity,
                totalQuantity,
                intent.AuthorityKind,
                intent.AuthorityId));

        foreach (var contributorId in sourceIds.Where(value =>
                     !string.Equals(value, survivorId, StringComparison.Ordinal)))
        {
            var contributorEntry = currentIndex.EntriesByItemId[contributorId];
            contributorEntry["state"] = "merged";
            contributorEntry["currentCarrier"] = null;
            contributorEntry["mergedIntoItemId"] = survivorId;
            MortalItemIdentityState.AppendTransition(
                contributorEntry,
                MortalItemIdentityState.CreateTransition(
                    "merge",
                    intent.Turn,
                    sourceIds,
                    carrierNode,
                    destinationCarrier: null,
                    resolved[contributorId].Quantity,
                    quantityAfter: 0,
                    intent.AuthorityKind,
                    intent.AuthorityId));
        }

        if (!JsonNode.DeepEquals(
                survivorReceiptBefore,
                survivor.Item[MortalItemMaterializationContract.ReceiptProperty]))
        {
            return MortalItemTransitionResult.Failed(
                "Объединение попыталось изменить receipt выбранной стопки.");
        }

        var normalizedIndex = MortalItemIdentityState.Parse(currentIndex.Root);
        var validationError = ValidateMutationResult(
            state,
            beforeIndex,
            normalizedIndex,
            survivorId,
            carrier);
        if (validationError != null)
            return MortalItemTransitionResult.Failed(validationError);
        foreach (var contributorId in sourceIds.Where(value =>
                     !string.Equals(value, survivorId, StringComparison.Ordinal)))
        {
            if (BuildCatalog(state).ByItemId.ContainsKey(contributorId))
                return MortalItemTransitionResult.Failed("Поглощённая стопка осталась у физического носителя.");
        }

        var committed = await CommitSingleCarrierAsync(
            writeLease,
            state,
            carrier,
            normalizedIndex.Root);
        return committed
            ? MortalItemTransitionResult.Completed(
                survivorId,
                "Стопки объединены; выбранная идентичность сохранена.")
            : MortalItemTransitionResult.Failed(
                "Игровое состояние изменилось во время объединения; исходные данные сохранены.");
    }

    private async Task<MortalItemTransitionResult> ExecuteDestroyAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalItemTransitionIntent intent,
        LoadedState state,
        MortalItemCarrierCatalog beforeCatalog,
        MortalItemIdentityParseResult beforeIndex)
    {
        var carrier = intent.SourceCarrier!;
        var items = ResolveCarrierArray(state, carrier, createIfMissing: false, out var carrierError);
        if (items == null)
            return MortalItemTransitionResult.Failed(carrierError!);

        var itemId = intent.SourceItemIds[0];
        var source = ResolveActiveItem(
            beforeCatalog,
            beforeIndex,
            items,
            carrier,
            itemId,
            out var sourceError);
        if (source == null)
            return MortalItemTransitionResult.Failed(sourceError!);
        if (source.Quantity != intent.Quantity)
            return MortalItemTransitionResult.Failed("Количество удаляемого предмета изменилось.");
        if (HasUnsafeCompanionReference(beforeCatalog, itemId, allowInlineEquipment: true))
        {
            return MortalItemTransitionResult.Failed(
                "Предмет связан с container, quest, bond или другим companion и не может быть уничтожен локально.");
        }

        ClearInlineEquipmentReference(state, carrier, itemId);
        var itemIndex = items.IndexOf(source.Item);
        if (itemIndex < 0)
            return MortalItemTransitionResult.Failed("Удаляемый предмет изменился до записи.");
        items.RemoveAt(itemIndex);

        var currentIndex = MortalItemIdentityState.Parse(beforeIndex.Root.DeepClone());
        var currentEntry = currentIndex.EntriesByItemId[itemId];
        currentEntry["state"] = "destroyed";
        currentEntry["currentCarrier"] = null;
        currentEntry["mergedIntoItemId"] = null;
        MortalItemIdentityState.AppendTransition(
            currentEntry,
            MortalItemIdentityState.CreateTransition(
                "destroy",
                intent.Turn,
                new[] { itemId },
                CreateCarrierNode(carrier),
                destinationCarrier: null,
                source.Quantity,
                quantityAfter: 0,
                intent.AuthorityKind,
                intent.AuthorityId));

        var normalizedIndex = MortalItemIdentityState.Parse(currentIndex.Root);
        var validationError = ValidateMutationResult(
            state,
            beforeIndex,
            normalizedIndex,
            expectedActiveItemId: null,
            expectedCarrier: null);
        if (validationError != null)
            return MortalItemTransitionResult.Failed(validationError);
        var afterCatalog = BuildCatalog(state);
        if (afterCatalog.ByItemId.ContainsKey(itemId) ||
            afterCatalog.ByCompanionReference.ContainsKey(itemId))
        {
            return MortalItemTransitionResult.Failed(
                "Уничтоженная идентичность осталась в физическом или companion-состоянии.");
        }

        var committed = await CommitSingleCarrierAsync(
            writeLease,
            state,
            carrier,
            normalizedIndex.Root);
        return committed
            ? MortalItemTransitionResult.Completed(itemId, "Предмет уничтожен без создания ground loot.")
            : MortalItemTransitionResult.Failed(
                "Игровое состояние изменилось во время уничтожения; исходные данные сохранены.");
    }

    private static ActiveItem? ResolveActiveItem(
        MortalItemCarrierCatalog catalog,
        MortalItemIdentityParseResult index,
        JsonArray carrierItems,
        MortalItemCarrierCoordinate carrier,
        string itemId,
        out string? error)
    {
        error = null;
        if (!catalog.ByItemId.TryGetValue(itemId, out var occurrences) ||
            occurrences.Count != 1 ||
            !SameCarrier(occurrences[0].Carrier, carrier))
        {
            error = "Предмет не найден ровно у выбранного носителя.";
            return null;
        }
        if (!index.EntriesByItemId.TryGetValue(itemId, out var entry) ||
            !string.Equals(ReadExactString(entry["state"]), "active", StringComparison.Ordinal) ||
            !CarrierNodeEquals(entry["currentCarrier"], carrier))
        {
            error = "Идентичность предмета не активна у выбранного носителя.";
            return null;
        }

        var matches = carrierItems.OfType<JsonObject>()
            .Where(item => string.Equals(ReadExactString(item["itemId"]), itemId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 || !TryReadPositiveInt(matches[0]["count"], out var quantity))
        {
            error = "Предмет не найден по точному itemId или его count повреждён.";
            return null;
        }
        if (entry["transitions"] is not JsonArray { Count: > 0 } transitions ||
            transitions[^1] is not JsonObject lastTransition ||
            !TryReadPositiveInt(lastTransition["quantityAfter"], out var recordedQuantity) ||
            recordedQuantity != quantity)
        {
            error = "Текущее количество предмета расходится с identity history.";
            return null;
        }

        return new ActiveItem(matches[0], entry, quantity);
    }

    private static string? ValidateMutationResult(
        LoadedState state,
        MortalItemIdentityParseResult beforeIndex,
        MortalItemIdentityParseResult currentIndex,
        string? expectedActiveItemId,
        MortalItemCarrierCoordinate? expectedCarrier)
    {
        if (currentIndex.Issues.Count > 0)
            return currentIndex.Issues[0].Message;
        var continuityIssues = MortalItemIdentityState.ValidateAgainst(beforeIndex, currentIndex);
        if (continuityIssues.Count > 0)
            return continuityIssues[0].Message;

        state.IdentityIndexRoot = currentIndex.Root;
        var catalog = BuildCatalog(state);
        var composedError = ValidateComposedState(catalog, currentIndex);
        if (composedError != null)
            return composedError;
        if (expectedActiveItemId == null)
            return null;
        if (!catalog.ByItemId.TryGetValue(expectedActiveItemId, out var occurrences) ||
            occurrences.Count != 1 ||
            expectedCarrier == null ||
            !SameCarrier(occurrences[0].Carrier, expectedCarrier))
        {
            return "Стековый переход не оставил ожидаемую активную идентичность у одного носителя.";
        }
        return null;
    }

    private async Task<bool> CommitSingleCarrierAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        LoadedState state,
        MortalItemCarrierCoordinate carrier,
        JsonObject indexRoot)
    {
        state.IdentityIndexRoot = indexRoot;
        return await CoordinatedStateWriteHelper.TryCommitAsync(
            _fs,
            writeLease,
            BuildWrites(state, new[] { PathForCarrier(carrier) }, mutation: null));
    }

    private static JsonObject CreateStackSemanticProjection(JsonObject item)
    {
        var projection = item.DeepClone().AsObject();
        foreach (var property in new[]
                 {
                     "itemId", "id", "initialId", "existedId", "creationRef",
                     "count", "quantity", MortalItemMaterializationContract.ReceiptProperty
                 })
        {
            projection.Remove(property);
        }
        if (projection[MortalItemMaterializationContract.EnvelopeProperty] is JsonObject envelope)
        {
            projection[MortalItemMaterializationContract.EnvelopeProperty] = new JsonObject
            {
                ["sections"] = envelope["sections"]?.DeepClone()
            };
        }
        return projection;
    }

    internal static bool StackSemanticsEqual(JsonObject left, JsonObject right) =>
        MortalItemMaterializationContract.ImmutableEvidenceEquals(
            CreateStackSemanticProjection(left),
            CreateStackSemanticProjection(right));

    private static bool HasUnsafeCompanionReference(
        MortalItemCarrierCatalog catalog,
        string itemId,
        bool allowInlineEquipment = false)
    {
        if (!catalog.ByCompanionReference.TryGetValue(itemId, out var references))
            return false;
        return references.Any(reference =>
            !allowInlineEquipment || !IsInlineEquipmentReference(reference));
    }

    private static bool IsInlineEquipmentReference(MortalItemCompanionReference reference) =>
        string.Equals(
            reference.FilePath,
            StorageTransportMoveService.InventoryPath,
            StringComparison.OrdinalIgnoreCase) &&
        (reference.JsonPath.Contains(".equipment", StringComparison.OrdinalIgnoreCase) ||
         reference.JsonPath.Contains(".equippedItems", StringComparison.OrdinalIgnoreCase));

    private static string NewUniqueItemId(
        MortalItemCarrierCatalog catalog,
        MortalItemIdentityParseResult index)
    {
        string itemId;
        do
        {
            itemId = "itm_" + Guid.NewGuid().ToString("N");
        } while (catalog.ByItemId.ContainsKey(itemId) || index.EntriesByItemId.ContainsKey(itemId));
        return itemId;
    }

    private static string? ValidateStackMutationIntent(MortalItemTransitionIntent intent)
    {
        if (intent.Kind is not (MortalItemTransitionKind.Split or
            MortalItemTransitionKind.Merge or
            MortalItemTransitionKind.Destroy))
        {
            return "Неподдерживаемый стековый переход.";
        }
        if (intent.SourceItemIds.Count == 0 ||
            intent.SourceItemIds.Any(sourceId => !IsExactIdentity(sourceId)) ||
            intent.SourceItemIds.Distinct(StringComparer.Ordinal).Count() != intent.SourceItemIds.Count ||
            intent.SourceCarrier == null ||
            !IsValidCarrier(intent.SourceCarrier) ||
            intent.Quantity <= 0 ||
            intent.Turn < 1 ||
            !IsExactIdentity(intent.AuthorityKind) ||
            !IsExactIdentity(intent.AuthorityId))
        {
            return "Стековый переход содержит неверную identity, carrier, quantity, turn или authority.";
        }

        if (intent.Kind == MortalItemTransitionKind.Destroy)
        {
            return intent.SourceItemIds.Count == 1 && intent.DestinationCarrier == null &&
                   intent.SurvivorItemId == null
                ? null
                : "Destroy требует один source item и не допускает destination или survivor.";
        }
        if (intent.DestinationCarrier == null ||
            !SameCarrier(intent.SourceCarrier, intent.DestinationCarrier))
        {
            return "Split и merge должны оставаться в том же carrier и container path.";
        }
        if (intent.Kind == MortalItemTransitionKind.Split)
        {
            return intent.SourceItemIds.Count == 1 && intent.SurvivorItemId == null
                ? null
                : "Split требует одну source identity и не принимает survivor.";
        }

        return intent.SourceItemIds.Count >= 2 &&
               intent.SurvivorItemId is { } survivor &&
               IsExactIdentity(survivor) &&
               intent.SourceItemIds.Contains(survivor, StringComparer.Ordinal)
            ? null
            : "Merge требует не менее двух identities и точный выбранный survivor.";
    }

    private sealed record ActiveItem(JsonObject Item, JsonObject Entry, int Quantity);
}
