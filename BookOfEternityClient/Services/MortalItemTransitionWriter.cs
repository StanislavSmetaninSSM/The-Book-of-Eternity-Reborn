using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal enum MortalItemTransitionKind
{
    Transfer,
    Split,
    Merge,
    Consume,
    Destroy,
    SemanticUpdate
}

internal sealed record MortalItemTransitionIntent(
    MortalItemTransitionKind Kind,
    IReadOnlyList<string> SourceItemIds,
    MortalItemCarrierCoordinate? SourceCarrier,
    MortalItemCarrierCoordinate? DestinationCarrier,
    int Quantity,
    int Turn,
    string AuthorityKind,
    string AuthorityId,
    string? SurvivorItemId = null);

internal sealed record MortalItemTransitionResult(
    bool Success,
    string? ItemId,
    string? DerivedItemId,
    string Message)
{
    internal static MortalItemTransitionResult Completed(string itemId, string message) =>
        new(true, itemId, null, message);

    internal static MortalItemTransitionResult Failed(string message) =>
        new(false, null, null, message);
}

internal sealed record MortalItemTransitionMutation(
    IReadOnlyList<string> AdditionalObjectPaths,
    Func<MortalItemTransitionMutationContext, string?> Apply,
    IReadOnlyList<CoordinatedStateWriteHelper.PlannedWrite>? GuardWrites = null);

internal sealed class MortalItemTransitionMutationContext
{
    private readonly IReadOnlyDictionary<string, JsonObject> _rootsByPath;

    internal MortalItemTransitionMutationContext(
        JsonObject item,
        IReadOnlyDictionary<string, JsonObject> rootsByPath)
    {
        Item = item;
        _rootsByPath = rootsByPath;
    }

    internal JsonObject Item { get; }

    internal JsonObject GetRequiredRoot(string path) =>
        _rootsByPath.TryGetValue(path, out var root)
            ? root
            : throw new InvalidOperationException($"Transition mutation did not load {path}.");
}

/// <summary>
/// Applies client-owned Mortal item identity transitions as one guarded write
/// set. Callers provide operation authority, never carrier JSON or an index.
/// </summary>
internal sealed class MortalItemTransitionWriter
{
    private static readonly string[] CompanionPaths =
    {
        "game_state/inventory/item_resources.json",
        "game_state/inventory/item_bonds.json",
        "game_state/inventory/item_text_updates.json",
        "game_state/inventory/recipes.json",
        "game_state/npcs/item_journals.json",
        "game_state/quests/quest_history.json"
    };

    private readonly FileSystemManager _fs;

    internal MortalItemTransitionWriter(FileSystemManager fs)
    {
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
    }

    internal async Task<MortalItemTransitionResult> ExecuteAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalItemTransitionIntent intent,
        MortalItemTransitionMutation? mutation = null)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(intent);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);

        var intentError = ValidateTransferIntent(intent);
        if (intentError != null)
            return MortalItemTransitionResult.Failed(intentError);

        LoadedState state;
        try
        {
            state = await LoadAsync(writeLease, mutation?.AdditionalObjectPaths);
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

        var itemId = intent.SourceItemIds[0];
        var occurrence = beforeCatalog.ByItemId[itemId][0];
        if (!SameCarrier(occurrence.Carrier, intent.SourceCarrier!))
        {
            return MortalItemTransitionResult.Failed(
                "Предмет больше не находится у выбранного исходного владельца. Откройте действие заново.");
        }

        var entry = beforeIndex.EntriesByItemId[itemId];
        if (!string.Equals(ReadExactString(entry["state"]), "active", StringComparison.Ordinal) ||
            !CarrierNodeEquals(entry["currentCarrier"], intent.SourceCarrier!))
        {
            return MortalItemTransitionResult.Failed(
                "Идентичность предмета не активна у выбранного исходного владельца.");
        }

        var sourceArray = ResolveCarrierArray(
            state,
            intent.SourceCarrier!,
            createIfMissing: false,
            out var sourceError);
        if (sourceArray == null)
            return MortalItemTransitionResult.Failed(sourceError!);
        var sourceMatches = sourceArray.OfType<JsonObject>()
            .Where(item => string.Equals(
                ReadExactString(item["itemId"]),
                itemId,
                StringComparison.Ordinal))
            .ToArray();
        if (sourceMatches.Length != 1)
        {
            return MortalItemTransitionResult.Failed(
                "Исходный предмет не найден по точному itemId или найден неоднозначно.");
        }
        var item = sourceMatches[0];

        if (!TryReadPositiveInt(item["count"], out var itemQuantity) ||
            itemQuantity != intent.Quantity)
        {
            return MortalItemTransitionResult.Failed(
                "Количество переносимого предмета не совпадает с его принятой записью.");
        }

        var destinationArray = ResolveCarrierArray(
            state,
            intent.DestinationCarrier!,
            createIfMissing: true,
            out var destinationError);
        if (destinationArray == null)
            return MortalItemTransitionResult.Failed(destinationError!);

        if (!ValidateDestinationContainerPath(
                destinationArray,
                itemId,
                intent.DestinationCarrier!.ContainerPath,
                out destinationError))
        {
            return MortalItemTransitionResult.Failed(destinationError!);
        }

        var sourceIndex = sourceArray.IndexOf(item);
        if (sourceIndex < 0)
            return MortalItemTransitionResult.Failed("Исходный предмет изменился до переноса.");

        var immutableEnvelope = item[MortalItemMaterializationContract.EnvelopeProperty]?.DeepClone();
        var immutableReceipt = item[MortalItemMaterializationContract.ReceiptProperty]?.DeepClone();
        ClearInlineEquipmentReference(state, intent.SourceCarrier!, itemId);
        sourceArray.RemoveAt(sourceIndex);
        item["contentsPath"] = intent.DestinationCarrier.ContainerPath.Count == 0
            ? null
            : new JsonArray(intent.DestinationCarrier.ContainerPath
                .Select(value => (JsonNode?)JsonValue.Create(value))
                .ToArray());
        destinationArray.Add(item);

        var currentIndexRoot = beforeIndex.Root.DeepClone().AsObject();
        var currentIndex = MortalItemIdentityState.Parse(currentIndexRoot);
        var currentEntry = currentIndex.EntriesByItemId[itemId];
        currentEntry["currentCarrier"] = CreateCarrierNode(intent.DestinationCarrier);
        MortalItemIdentityState.AppendTransition(
            currentEntry,
            MortalItemIdentityState.CreateTransition(
                "transfer",
                intent.Turn,
                intent.SourceItemIds,
                CreateCarrierNode(intent.SourceCarrier!),
                CreateCarrierNode(intent.DestinationCarrier),
                intent.Quantity,
                intent.Quantity,
                intent.AuthorityKind,
                intent.AuthorityId));

        if (mutation != null)
        {
            var mutationError = mutation.Apply(CreateMutationContext(state, item));
            if (mutationError != null)
                return MortalItemTransitionResult.Failed(mutationError);
        }

        if (!JsonNode.DeepEquals(
                immutableEnvelope,
                item[MortalItemMaterializationContract.EnvelopeProperty]) ||
            !JsonNode.DeepEquals(
                immutableReceipt,
                item[MortalItemMaterializationContract.ReceiptProperty]))
        {
            return MortalItemTransitionResult.Failed(
                "Перенос попытался изменить неизменяемое свидетельство материализации предмета.");
        }

        var normalizedCurrentIndex = MortalItemIdentityState.Parse(currentIndex.Root);
        if (normalizedCurrentIndex.Issues.Count > 0)
            return MortalItemTransitionResult.Failed(normalizedCurrentIndex.Issues[0].Message);
        var continuityIssues = MortalItemIdentityState.ValidateAgainst(
            beforeIndex,
            normalizedCurrentIndex);
        if (continuityIssues.Count > 0)
            return MortalItemTransitionResult.Failed(continuityIssues[0].Message);

        state.IdentityIndexRoot = normalizedCurrentIndex.Root;
        var afterCatalog = BuildCatalog(state);
        var composedError = ValidateComposedState(afterCatalog, normalizedCurrentIndex);
        if (composedError != null)
            return MortalItemTransitionResult.Failed(composedError);
        if (!afterCatalog.ByItemId.TryGetValue(itemId, out var afterOccurrences) ||
            afterOccurrences.Count != 1 ||
            !SameCarrier(afterOccurrences[0].Carrier, intent.DestinationCarrier))
        {
            return MortalItemTransitionResult.Failed(
                "Перенос не завершился ровно одним активным носителем предмета.");
        }

        var writes = BuildWrites(
            state,
            intent.SourceCarrier!,
            intent.DestinationCarrier!,
            mutation);
        var committed = await CoordinatedStateWriteHelper.TryCommitAsync(
            _fs,
            writeLease,
            writes);
        return committed
            ? MortalItemTransitionResult.Completed(itemId, "Предмет перенесён с сохранением идентичности.")
            : MortalItemTransitionResult.Failed(
                "Игровое состояние изменилось во время переноса; исходные данные сохранены.");
    }

    internal async Task<MortalItemTransitionResult> CreateAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        JsonObject rawItem,
        MortalItemCarrierCoordinate destination,
        int acceptedTurn,
        string authorityKind,
        string authorityId,
        MortalItemTransitionMutation? mutation = null)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(rawItem);
        ArgumentNullException.ThrowIfNull(destination);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);

        if (!IsValidCarrier(destination) || acceptedTurn < 1 ||
            !IsExactIdentity(authorityKind) || !IsExactIdentity(authorityId))
        {
            return MortalItemTransitionResult.Failed(
                "Создание предмета содержит неверный носитель, ход или authority.");
        }

        using (var rawDocument = JsonDocument.Parse(rawItem.ToJsonString()))
        {
            var rawIssues = MortalItemMaterializationContract.Validate(
                rawDocument.RootElement,
                "local_mortal_item_creation",
                MortalItemMaterializationPhase.RawPreSeal);
            if (rawIssues.Count > 0)
                return MortalItemTransitionResult.Failed(rawIssues[0].Message);
        }

        var envelope = rawItem[MortalItemMaterializationContract.EnvelopeProperty]!.AsObject();
        if (!TryReadPositiveInt(envelope["sourceTurn"], out var sourceTurn) ||
            sourceTurn != acceptedTurn)
        {
            return MortalItemTransitionResult.Failed(
                "Создание предмета должно принимать точный sourceTurn GM-envelope.");
        }

        LoadedState state;
        try
        {
            state = await LoadAsync(writeLease, mutation?.AdditionalObjectPaths);
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

        var destinationArray = ResolveCarrierArray(
            state,
            destination,
            createIfMissing: true,
            out var destinationError);
        if (destinationArray == null)
            return MortalItemTransitionResult.Failed(destinationError!);

        var knownItemIds = new HashSet<string>(beforeIndex.EntriesByItemId.Keys, StringComparer.Ordinal);
        foreach (var occurrence in beforeCatalog.Occurrences)
        {
            if (occurrence.ItemId != null)
                knownItemIds.Add(occurrence.ItemId);
        }
        string itemId;
        do
        {
            itemId = "itm_" + Guid.NewGuid().ToString("N");
        } while (!knownItemIds.Add(itemId));

        var canonicalItem = rawItem.DeepClone().AsObject();
        canonicalItem["contentsPath"] = destination.ContainerPath.Count == 0
            ? null
            : new JsonArray(destination.ContainerPath
                .Select(value => (JsonNode?)JsonValue.Create(value))
                .ToArray());
        var receipt = MortalItemIdentityState.CreateRootReceipt(
            canonicalItem,
            itemId,
            acceptedTurn);
        canonicalItem["itemId"] = itemId;
        canonicalItem["existedId"] = itemId;
        canonicalItem.Remove("creationRef");
        canonicalItem[MortalItemMaterializationContract.ReceiptProperty] = receipt;

        using (var canonicalDocument = JsonDocument.Parse(canonicalItem.ToJsonString()))
        {
            var canonicalIssues = MortalItemMaterializationContract.Validate(
                canonicalDocument.RootElement,
                "local_mortal_item_creation",
                MortalItemMaterializationPhase.CanonicalPostSeal);
            if (canonicalIssues.Count > 0)
                return MortalItemTransitionResult.Failed(canonicalIssues[0].Message);
        }
        if (!TryReadPositiveInt(canonicalItem["count"], out var quantity))
            return MortalItemTransitionResult.Failed("Создаваемый предмет требует положительный count.");
        if (!ValidateDestinationContainerPath(
                destinationArray,
                itemId,
                destination.ContainerPath,
                out destinationError))
        {
            return MortalItemTransitionResult.Failed(destinationError!);
        }

        destinationArray.Add(canonicalItem);
        var currentIndexRoot = beforeIndex.Root.DeepClone().AsObject();
        var carrierNode = CreateCarrierNode(destination);
        var materializationId = ReadExactString(envelope["materializationId"]);
        if (materializationId == null)
            return MortalItemTransitionResult.Failed("GM-envelope не содержит точный materializationId.");
        currentIndexRoot["entries"]!.AsArray().Add(new JsonObject
        {
            ["itemId"] = itemId,
            ["receiptId"] = receipt["receiptId"]!.DeepClone(),
            ["state"] = "active",
            ["currentCarrier"] = carrierNode.DeepClone(),
            ["originMaterializationIds"] = new JsonArray(materializationId),
            ["parentItemIds"] = new JsonArray(),
            ["mergedIntoItemId"] = null,
            ["transitions"] = new JsonArray(
                MortalItemIdentityState.CreateTransition(
                    "create",
                    acceptedTurn,
                    Array.Empty<string>(),
                    sourceCarrier: null,
                    destinationCarrier: carrierNode,
                    quantityBefore: 0,
                    quantityAfter: quantity,
                    authorityKind,
                    authorityId))
        });

        if (mutation != null)
        {
            var mutationError = mutation.Apply(CreateMutationContext(state, canonicalItem));
            if (mutationError != null)
                return MortalItemTransitionResult.Failed(mutationError);
        }

        var normalizedCurrentIndex = MortalItemIdentityState.Parse(currentIndexRoot);
        if (normalizedCurrentIndex.Issues.Count > 0)
            return MortalItemTransitionResult.Failed(normalizedCurrentIndex.Issues[0].Message);
        var continuityIssues = MortalItemIdentityState.ValidateAgainst(beforeIndex, normalizedCurrentIndex);
        if (continuityIssues.Count > 0)
            return MortalItemTransitionResult.Failed(continuityIssues[0].Message);

        state.IdentityIndexRoot = normalizedCurrentIndex.Root;
        var afterCatalog = BuildCatalog(state);
        var composedError = ValidateComposedState(afterCatalog, normalizedCurrentIndex);
        if (composedError != null)
            return MortalItemTransitionResult.Failed(composedError);
        if (!afterCatalog.ByItemId.TryGetValue(itemId, out var occurrences) ||
            occurrences.Count != 1 ||
            !SameCarrier(occurrences[0].Carrier, destination))
        {
            return MortalItemTransitionResult.Failed(
                "Создание не завершилось ровно одним активным носителем предмета.");
        }

        var writes = BuildWrites(
            state,
            new[] { PathForCarrier(destination) },
            mutation);
        var committed = await CoordinatedStateWriteHelper.TryCommitAsync(
            _fs,
            writeLease,
            writes);
        return committed
            ? MortalItemTransitionResult.Completed(
                itemId,
                "Предмет материализован с постоянной идентичностью.")
            : MortalItemTransitionResult.Failed(
                "Игровое состояние изменилось во время materialization; исходные данные сохранены.");
    }

    private static string? ValidateTransferIntent(MortalItemTransitionIntent intent)
    {
        if (intent.Kind != MortalItemTransitionKind.Transfer)
            return "Этот writer пока принимает только перенос существующего предмета.";
        if (intent.SourceItemIds is not { Count: 1 } ||
            !IsExactIdentity(intent.SourceItemIds[0]))
        {
            return "Перенос должен ссылаться на один точный itemId.";
        }
        if (intent.SourceCarrier == null || intent.DestinationCarrier == null ||
            !IsValidCarrier(intent.SourceCarrier) || !IsValidCarrier(intent.DestinationCarrier) ||
            SameCarrier(intent.SourceCarrier, intent.DestinationCarrier))
        {
            return "Перенос должен задавать разные поддерживаемые исходный и целевой носители.";
        }
        if (intent.Quantity <= 0 || intent.Turn < 0 ||
            !IsExactIdentity(intent.AuthorityKind) || !IsExactIdentity(intent.AuthorityId))
        {
            return "Перенос содержит неверное количество, ход или authority.";
        }
        return null;
    }

    private async Task<LoadedState> LoadAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyList<string>? additionalObjectPaths)
    {
        var inventory = await LoadObjectAsync(
            writeLease,
            StorageTransportMoveService.InventoryPath,
            required: false);
        var npcCore = await LoadObjectAsync(
            writeLease,
            NpcCoreChangesContract.NpcCorePath,
            required: false);
        var location = await LoadObjectAsync(
            writeLease,
            StorageTransportMoveService.CurrentLocationPath,
            required: false);
        var vehicles = await LoadVehiclesAsync(writeLease);
        var indexJson = await _fs.ReadFileAsync(writeLease, MortalItemIdentityState.StatePath);
        if (string.IsNullOrWhiteSpace(indexJson))
            throw new InvalidDataException("Индекс идентичности предметов отсутствует.");

        var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in CompanionPaths)
        {
            var document = await LoadObjectAsync(writeLease, path, required: false);
            if (document?.CatalogRoot != null)
                companions.Add(path, document.CatalogRoot);
        }

        var additionalDocuments = new Dictionary<string, StateDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in additionalObjectPaths ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(path) ||
                string.Equals(path, MortalItemIdentityState.StatePath, StringComparison.OrdinalIgnoreCase) ||
                additionalDocuments.ContainsKey(path) ||
                IsStandardDocumentPath(path))
            {
                continue;
            }

            var document = await LoadObjectAsync(writeLease, path, required: true) ??
                           throw new InvalidDataException($"Отсутствует обязательный файл {path}.");
            additionalDocuments.Add(path, document);
        }

        return new LoadedState(
            inventory,
            npcCore,
            location,
            vehicles,
            companions,
            additionalDocuments,
            indexJson,
            MortalItemIdentityState.Parse(indexJson).Root);
    }

    private static bool IsStandardDocumentPath(string path) =>
        string.Equals(path, StorageTransportMoveService.InventoryPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, NpcCoreChangesContract.NpcCorePath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, StorageTransportMoveService.CurrentLocationPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, StorageTransportMoveService.VehiclesPath, StringComparison.OrdinalIgnoreCase);

    private async Task<StateDocument?> LoadObjectAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string path,
        bool required)
    {
        var json = await _fs.ReadFileAsync(writeLease, path);
        if (string.IsNullOrWhiteSpace(json))
        {
            if (required)
                throw new InvalidDataException($"Отсутствует обязательный файл {path}.");
            return null;
        }

        var node = JsonNode.Parse(json) ??
                   throw new InvalidDataException($"Файл {path} пуст.");
        if (node is not JsonObject root)
            throw new InvalidDataException($"Файл {path} должен иметь объектный корень.");
        return new StateDocument(path, json, node, root);
    }

    private async Task<StateDocument?> LoadVehiclesAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var path = StorageTransportMoveService.VehiclesPath;
        var json = await _fs.ReadFileAsync(writeLease, path);
        if (string.IsNullOrWhiteSpace(json))
            return null;
        var node = JsonNode.Parse(json) ??
                   throw new InvalidDataException($"Файл {path} пуст.");
        if (node is JsonObject root)
            return new StateDocument(path, json, node, root);
        if (node is JsonArray vehicles)
        {
            var wrapper = new JsonObject { ["vehicles"] = vehicles };
            return new StateDocument(path, json, node, wrapper);
        }
        throw new InvalidDataException($"Файл {path} должен иметь объектный или массивный корень.");
    }

    private static MortalItemCarrierCatalog BuildCatalog(LoadedState state) =>
        MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            state.Inventory?.CatalogRoot,
            state.NpcCore?.CatalogRoot,
            null,
            state.Location?.CatalogRoot,
            state.Vehicles?.CatalogRoot,
            state.Companions));

    private static string? ValidateComposedState(
        MortalItemCarrierCatalog catalog,
        MortalItemIdentityParseResult index)
    {
        if (catalog.Issues.Count > 0)
            return catalog.Issues[0].Message;
        if (index.Issues.Count > 0)
            return index.Issues[0].Message;

        foreach (var occurrence in catalog.Occurrences)
        {
            if (occurrence.ItemId == null)
                return "Локальный перенос не допускает receipt-less или незапечатанный предмет.";
            if (!index.EntriesByItemId.TryGetValue(occurrence.ItemId, out var entry))
                return $"Предмет {occurrence.ItemId} отсутствует в индексе идентичности.";
            if (!string.Equals(ReadExactString(entry["state"]), "active", StringComparison.Ordinal) ||
                !CarrierNodeEquals(entry["currentCarrier"], occurrence.Carrier))
            {
                return $"Носитель предмета {occurrence.ItemId} расходится с индексом идентичности.";
            }

            var itemReceiptId = ReadExactString(
                occurrence.Item[MortalItemMaterializationContract.ReceiptProperty]?["receiptId"]);
            if (!string.Equals(
                    itemReceiptId,
                    ReadExactString(entry["receiptId"]),
                    StringComparison.Ordinal))
            {
                return $"Receipt предмета {occurrence.ItemId} расходится с индексом идентичности.";
            }

            using var document = JsonDocument.Parse(occurrence.Item.ToJsonString());
            var contractIssues = MortalItemMaterializationContract.Validate(
                document.RootElement,
                occurrence.JsonPath,
                MortalItemMaterializationPhase.CanonicalPostSeal);
            if (contractIssues.Count > 0)
                return contractIssues[0].Message;
        }

        foreach (var pair in index.EntriesByItemId)
        {
            catalog.ByItemId.TryGetValue(pair.Key, out var occurrences);
            var state = ReadExactString(pair.Value["state"]);
            if (state == "active" && occurrences?.Count != 1)
                return $"Активная идентичность {pair.Key} должна иметь ровно одного носителя.";
            if (state is "merged" or "consumed" or "destroyed" && occurrences is { Count: > 0 })
                return $"Завершённая идентичность {pair.Key} не может оставаться у носителя.";
        }

        return null;
    }

    private static JsonArray? ResolveCarrierArray(
        LoadedState state,
        MortalItemCarrierCoordinate carrier,
        bool createIfMissing,
        out string? error)
    {
        error = null;
        switch (carrier.Kind)
        {
            case "player_inventory":
            {
                var root = state.Inventory?.CatalogRoot;
                if (root == null)
                {
                    error = "Инвентарь игрока отсутствует.";
                    return null;
                }
                if (root["items"] is JsonArray items)
                    return items;
                if (root["UpdateInventory"] is JsonArray updates)
                    return updates;
                if (!createIfMissing)
                {
                    error = "В инвентаре игрока нет массива предметов.";
                    return null;
                }
                var created = new JsonArray();
                root["items"] = created;
                return created;
            }
            case "npc_inventory":
            {
                var npc = ResolveNpc(state.NpcCore?.CatalogRoot, carrier.OwnerId, out error);
                if (npc == null)
                    return null;
                if (npc["inventory"] is JsonArray inventory)
                    return inventory;
                if (!createIfMissing)
                {
                    error = "У NPC нет массива инвентаря.";
                    return null;
                }
                var created = new JsonArray();
                npc["inventory"] = created;
                return created;
            }
            case "location_storage":
            {
                var root = state.Location?.CatalogRoot;
                var location = root?["currentLocationData"] as JsonObject ?? root;
                if (location == null ||
                    !MatchesAnyIdentity(location, carrier.OwnerId, "locationId", "id"))
                {
                    error = "Точная текущая локация для переноса не найдена.";
                    return null;
                }
                if (location["locationStorages"] is not JsonArray storages)
                {
                    error = "У текущей локации нет массива хранилищ.";
                    return null;
                }
                var storageMatches = storages.OfType<JsonObject>()
                    .Where(storage => MatchesAnyIdentity(storage, carrier.ContainerId!, "storageId"))
                    .ToArray();
                if (storageMatches.Length != 1)
                {
                    error = "Точное хранилище для переноса не найдено или неоднозначно.";
                    return null;
                }
                if (storageMatches[0]["contents"] is JsonArray contents)
                    return contents;
                if (!createIfMissing)
                {
                    error = "У хранилища нет массива содержимого.";
                    return null;
                }
                var created = new JsonArray();
                storageMatches[0]["contents"] = created;
                return created;
            }
            case "vehicle_inventory":
            {
                if (state.Vehicles?.CatalogRoot?["vehicles"] is not JsonArray vehicles)
                {
                    error = "Список транспорта отсутствует.";
                    return null;
                }
                var matches = vehicles.OfType<JsonObject>()
                    .Where(vehicle => MatchesAnyIdentity(vehicle, carrier.OwnerId, "vehicleId", "id"))
                    .ToArray();
                if (matches.Length != 1)
                {
                    error = "Точный транспорт для переноса не найден или неоднозначен.";
                    return null;
                }
                if (matches[0]["inventory"] is JsonArray inventory)
                    return inventory;
                if (!createIfMissing)
                {
                    error = "У транспорта нет массива инвентаря.";
                    return null;
                }
                var created = new JsonArray();
                matches[0]["inventory"] = created;
                return created;
            }
            default:
                error = "Неподдерживаемый носитель предмета.";
                return null;
        }
    }

    private static JsonObject? ResolveNpc(JsonObject? root, string npcId, out string? error)
    {
        error = null;
        if (root == null)
        {
            error = "Состояние NPC отсутствует.";
            return null;
        }

        var matches = new List<JsonObject>();
        foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            if (root[section] is not JsonArray npcs)
                continue;
            matches.AddRange(npcs.OfType<JsonObject>().Where(npc =>
                MatchesAnyIdentity(npc, npcId, "NPCId", "npcId", "id", "initialId")));
        }
        if (matches.Count == 1)
            return matches[0];
        error = "Точный NPC для переноса не найден или неоднозначен.";
        return null;
    }

    private static bool ValidateDestinationContainerPath(
        JsonArray destination,
        string movingItemId,
        IReadOnlyList<string> path,
        out string? error)
    {
        error = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parentId in path)
        {
            if (!IsExactIdentity(parentId) ||
                !seen.Add(parentId) ||
                string.Equals(parentId, movingItemId, StringComparison.Ordinal))
            {
                error = "Путь контейнера содержит неверную или циклическую идентичность.";
                return false;
            }
            var matches = destination.OfType<JsonObject>()
                .Where(item => string.Equals(
                    ReadExactString(item["itemId"]),
                    parentId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1 || !ReadBool(matches[0]["isContainer"]))
            {
                error = "Каждый элемент пути контейнера должен быть активным точным контейнером того же носителя.";
                return false;
            }
        }
        return true;
    }

    private static void ClearInlineEquipmentReference(
        LoadedState state,
        MortalItemCarrierCoordinate source,
        string itemId)
    {
        JsonObject? owner = source.Kind switch
        {
            "player_inventory" => state.Inventory?.CatalogRoot,
            "npc_inventory" => ResolveNpc(state.NpcCore?.CatalogRoot, source.OwnerId, out _),
            _ => null
        };
        if (owner == null)
            return;
        foreach (var property in new[] { "equipment", "equippedItems" })
            RemoveExactEquipmentReference(owner[property], itemId);
    }

    private static void RemoveExactEquipmentReference(JsonNode? node, string itemId)
    {
        if (node is JsonArray array)
        {
            for (var index = array.Count - 1; index >= 0; index--)
            {
                if (string.Equals(ReadExactString(array[index]), itemId, StringComparison.Ordinal))
                    array.RemoveAt(index);
                else
                    RemoveExactEquipmentReference(array[index], itemId);
            }
            return;
        }
        if (node is not JsonObject obj)
            return;
        foreach (var property in obj.ToArray())
        {
            if (string.Equals(ReadExactString(property.Value), itemId, StringComparison.Ordinal))
                obj[property.Key] = null;
            else
                RemoveExactEquipmentReference(property.Value, itemId);
        }
    }

    private static CoordinatedStateWriteHelper.PlannedWrite[] BuildWrites(
        LoadedState state,
        MortalItemCarrierCoordinate source,
        MortalItemCarrierCoordinate destination,
        MortalItemTransitionMutation? mutation) =>
        BuildWrites(
            state,
            new[] { PathForCarrier(source), PathForCarrier(destination) },
            mutation);

    private static CoordinatedStateWriteHelper.PlannedWrite[] BuildWrites(
        LoadedState state,
        IEnumerable<string> carrierPaths,
        MortalItemTransitionMutation? mutation)
    {
        var paths = new HashSet<string>(carrierPaths, StringComparer.OrdinalIgnoreCase);
        foreach (var path in mutation?.AdditionalObjectPaths ?? Array.Empty<string>())
            paths.Add(path);

        var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>();
        if (mutation?.GuardWrites != null)
            writes.AddRange(mutation.GuardWrites);
        foreach (var path in paths)
        {
            var document = state.DocumentForPath(path) ??
                           throw new InvalidOperationException($"Missing touched carrier document {path}.");
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                path,
                document.PreviousJson,
                document.Root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                RequireCurrentBaseline: true));
        }
        writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
            MortalItemIdentityState.StatePath,
            state.IdentityIndexJson,
            state.IdentityIndexRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            RequireCurrentBaseline: true));
        return writes.ToArray();
    }

    private static MortalItemTransitionMutationContext CreateMutationContext(
        LoadedState state,
        JsonObject item)
    {
        var roots = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in state.Documents())
            roots[document.Path] = document.CatalogRoot;
        return new MortalItemTransitionMutationContext(item, roots);
    }

    private static string PathForCarrier(MortalItemCarrierCoordinate carrier) =>
        carrier.Kind switch
        {
            "player_inventory" => StorageTransportMoveService.InventoryPath,
            "npc_inventory" => NpcCoreChangesContract.NpcCorePath,
            "location_storage" => StorageTransportMoveService.CurrentLocationPath,
            "vehicle_inventory" => StorageTransportMoveService.VehiclesPath,
            _ => throw new ArgumentOutOfRangeException(nameof(carrier))
        };

    private static JsonObject CreateCarrierNode(MortalItemCarrierCoordinate carrier) =>
        new()
        {
            ["kind"] = carrier.Kind,
            ["ownerId"] = carrier.OwnerId,
            ["containerId"] = carrier.ContainerId,
            ["containerPath"] = new JsonArray(carrier.ContainerPath
                .Select(value => (JsonNode?)JsonValue.Create(value))
                .ToArray())
        };

    private static bool CarrierNodeEquals(JsonNode? node, MortalItemCarrierCoordinate carrier) =>
        JsonNode.DeepEquals(node, CreateCarrierNode(carrier));

    private static bool SameCarrier(
        MortalItemCarrierCoordinate left,
        MortalItemCarrierCoordinate right) =>
        string.Equals(left.Kind, right.Kind, StringComparison.Ordinal) &&
        string.Equals(left.OwnerId, right.OwnerId, StringComparison.Ordinal) &&
        string.Equals(left.ContainerId, right.ContainerId, StringComparison.Ordinal) &&
        left.ContainerPath.SequenceEqual(right.ContainerPath, StringComparer.Ordinal);

    private static bool IsValidCarrier(MortalItemCarrierCoordinate carrier)
    {
        if (!IsExactIdentity(carrier.Kind) || !IsExactIdentity(carrier.OwnerId) ||
            carrier.ContainerPath.Any(value => !IsExactIdentity(value)) ||
            carrier.ContainerPath.Distinct(StringComparer.Ordinal).Count() != carrier.ContainerPath.Count)
        {
            return false;
        }
        return carrier.Kind switch
        {
            "player_inventory" => carrier.OwnerId == "player" && carrier.ContainerId == null,
            "npc_inventory" or "vehicle_inventory" => carrier.ContainerId == null,
            "location_storage" => IsExactIdentity(carrier.ContainerId),
            _ => false
        };
    }

    private static bool MatchesAnyIdentity(JsonObject value, string expected, params string[] properties) =>
        properties.Any(property => string.Equals(
            ReadExactString(value[property]),
            expected,
            StringComparison.Ordinal));

    private static string? ReadExactString(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text) ||
            !IsExactIdentity(text))
        {
            return null;
        }
        return text;
    }

    private static bool IsExactIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool TryReadPositiveInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue &&
               jsonValue.TryGetValue(out value) &&
               value > 0;
    }

    private static bool ReadBool(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var result) && result;

    private sealed record StateDocument(
        string Path,
        string PreviousJson,
        JsonNode Root,
        JsonObject CatalogRoot);

    private sealed record LoadedState(
        StateDocument? Inventory,
        StateDocument? NpcCore,
        StateDocument? Location,
        StateDocument? Vehicles,
        IReadOnlyDictionary<string, JsonObject> Companions,
        IReadOnlyDictionary<string, StateDocument> AdditionalDocuments,
        string IdentityIndexJson,
        JsonObject InitialIdentityIndexRoot)
    {
        internal JsonObject IdentityIndexRoot { get; set; } = InitialIdentityIndexRoot;

        internal StateDocument? DocumentForPath(string path)
        {
            foreach (var document in Documents())
            {
                if (document != null && string.Equals(
                        document.Path,
                        path,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }
            return null;
        }

        internal IEnumerable<StateDocument> Documents()
        {
            foreach (var document in new[] { Inventory, NpcCore, Location, Vehicles })
            {
                if (document != null)
                    yield return document;
            }
            foreach (var document in AdditionalDocuments.Values)
                yield return document;
        }
    }
}
