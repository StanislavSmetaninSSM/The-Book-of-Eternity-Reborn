using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static class StorageTransportMoveService
{
    public const string InventoryPath = InventoryEquipmentService.ItemsPath;
    public const string CurrentLocationPath = "game_state/world/current_location.json";
    public const string VehiclesPath = "game_state/misc/vehicles.json";

    public const string DirectionDeposit = "deposit";
    public const string DirectionRetrieve = "retrieve";

    public static async Task<StorageMoveContext> ReadStorageMoveContextAsync(FileSystemManager fs)
    {
        var inventoryRead = await ReadObjectAsync(fs, InventoryPath, "Инвентарь сейчас недоступен.");
        if (!inventoryRead.Success)
            return StorageMoveContext.Failed(inventoryRead.Message);

        var locationRead = await ReadObjectAsync(fs, CurrentLocationPath, "Текущая локация сейчас недоступна.");
        if (!locationRead.Success)
            return StorageMoveContext.Failed(locationRead.Message);

        var inventoryArray = GetPlayerInventoryArrayNode(inventoryRead.Root!, createIfMissing: false);
        if (inventoryArray == null && !HasRecognizedInventoryShape(inventoryRead.Root!))
            return StorageMoveContext.Failed("Инвентарь сейчас не похож на обычный рюкзак. Перемещение предметов временно недоступно.");

        if (locationRead.Root!["locationStorages"] is not JsonArray storagesArray)
            return StorageMoveContext.Failed("В текущей локации нет доступных хранилищ.");

        var storages = EnumerateStorageTargets(storagesArray, accessibleOnly: true)
            .Select(static target => new StorageMoveStorage(
                target.Key,
                target.Identity,
                target.Name,
                target.Contents.Count,
                BuildItemOptions(target.Contents, $"Внутри: {target.Name}.", allowEmptyIdentity: true)))
            .ToArray();

        return new StorageMoveContext(
            true,
            string.Empty,
            BuildItemOptions(inventoryArray ?? new JsonArray(), "В рюкзаке.", allowEmptyIdentity: true),
            storages);
    }

    public static async Task<VehicleMoveContext> ReadVehicleMoveContextAsync(FileSystemManager fs)
    {
        var inventoryRead = await ReadObjectAsync(fs, InventoryPath, "Инвентарь сейчас недоступен.");
        if (!inventoryRead.Success)
            return VehicleMoveContext.Failed(inventoryRead.Message);

        var vehiclesRead = await ReadNodeAsync(fs, VehiclesPath, "Транспорт сейчас недоступен.");
        if (!vehiclesRead.Success)
            return VehicleMoveContext.Failed(vehiclesRead.Message);

        var inventoryArray = GetPlayerInventoryArrayNode(inventoryRead.Root!, createIfMissing: false);
        if (inventoryArray == null && !HasRecognizedInventoryShape(inventoryRead.Root!))
            return VehicleMoveContext.Failed("Инвентарь сейчас не похож на обычный рюкзак. Перемещение предметов временно недоступно.");

        if (!TryGetVehiclesArray(vehiclesRead.Node, out var vehiclesArray))
            return VehicleMoveContext.Failed("Список транспорта сейчас пуст или недоступен.");

        var vehicles = EnumerateVehicleTargets(vehiclesArray, requireInventoryArray: false)
            .Select(static target => new VehicleMoveVehicle(
                target.Key,
                target.Identity,
                target.Name,
                target.Contents.Count,
                BuildItemOptions(target.Contents, $"В транспорте: {target.Name}.", allowEmptyIdentity: true)))
            .ToArray();

        return new VehicleMoveContext(
            true,
            string.Empty,
            BuildItemOptions(inventoryArray ?? new JsonArray(), "В рюкзаке.", allowEmptyIdentity: true),
            vehicles);
    }

    public static async Task<StorageTransportMoveOutcome> ValidateStorageMoveAsync(
        FileSystemManager fs,
        string direction,
        string storageKey,
        string itemKey) =>
        await MoveStorageItemCoreAsync(fs, null, direction, storageKey, itemKey, write: false);

    internal static async Task<StorageTransportMoveOutcome> ValidateStorageMoveAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string direction,
        string storageKey,
        string itemKey) =>
        await MoveStorageItemCoreAsync(fs, writeLease, direction, storageKey, itemKey, write: false);

    public static async Task<StorageTransportMoveOutcome> MoveStorageItemAsync(
        FileSystemManager fs,
        string direction,
        string storageKey,
        string itemKey) =>
        await MoveStorageItemCoreAsync(fs, null, direction, storageKey, itemKey, write: true);

    internal static async Task<StorageTransportMoveOutcome> MoveStorageItemAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string direction,
        string storageKey,
        string itemKey) =>
        await MoveStorageItemCoreAsync(fs, writeLease, direction, storageKey, itemKey, write: true);

    public static async Task<StorageTransportMoveOutcome> ValidateVehicleMoveAsync(
        FileSystemManager fs,
        string direction,
        string vehicleKey,
        string itemKey) =>
        await MoveVehicleItemCoreAsync(fs, null, direction, vehicleKey, itemKey, write: false);

    internal static async Task<StorageTransportMoveOutcome> ValidateVehicleMoveAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string direction,
        string vehicleKey,
        string itemKey) =>
        await MoveVehicleItemCoreAsync(fs, writeLease, direction, vehicleKey, itemKey, write: false);

    public static async Task<StorageTransportMoveOutcome> MoveVehicleItemAsync(
        FileSystemManager fs,
        string direction,
        string vehicleKey,
        string itemKey) =>
        await MoveVehicleItemCoreAsync(fs, null, direction, vehicleKey, itemKey, write: true);

    internal static async Task<StorageTransportMoveOutcome> MoveVehicleItemAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string direction,
        string vehicleKey,
        string itemKey) =>
        await MoveVehicleItemCoreAsync(fs, writeLease, direction, vehicleKey, itemKey, write: true);

    private static async Task<StorageTransportMoveOutcome> MoveStorageItemCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string direction,
        string storageKey,
        string itemKey,
        bool write)
    {
        if (!IsSupportedDirection(direction))
            return StorageTransportMoveOutcome.Failed("Выберите направление перемещения.");
        if (string.IsNullOrWhiteSpace(storageKey))
            return StorageTransportMoveOutcome.Failed("Выберите доступное хранилище.");
        if (string.IsNullOrWhiteSpace(itemKey))
            return StorageTransportMoveOutcome.Failed("Выберите предмет.");

        var inventoryRead = await ReadObjectAsync(
            fs,
            writeLease,
            InventoryPath,
            "Инвентарь сейчас недоступен.");
        if (!inventoryRead.Success)
            return StorageTransportMoveOutcome.Failed(inventoryRead.Message);
        var locationRead = await ReadObjectAsync(
            fs,
            writeLease,
            CurrentLocationPath,
            "Текущая локация сейчас недоступна.");
        if (!locationRead.Success)
            return StorageTransportMoveOutcome.Failed(locationRead.Message);
        if (locationRead.Root!["locationStorages"] is not JsonArray storagesArray)
            return StorageTransportMoveOutcome.Failed("В текущей локации нет доступных хранилищ.");

        var storage = ResolveStorageTarget(storagesArray, storageKey);
        if (!storage.Success)
            return StorageTransportMoveOutcome.Failed(storage.Message);

        var storageContents = EnsureContentsArray(
            storage.Target!.Node,
            "contents",
            createIfMissing: direction == DirectionDeposit && write,
            treatMissingAsEmpty: direction == DirectionDeposit && !write);
        if (storageContents == null && direction == DirectionDeposit)
            return StorageTransportMoveOutcome.Failed("Хранилище сейчас не готово принять предмет.");
        if (storageContents == null)
            return StorageTransportMoveOutcome.Failed("В выбранном хранилище сейчас нет предметов.");

        if (direction == DirectionDeposit)
        {
            var inventoryArray = GetPlayerInventoryArrayNode(inventoryRead.Root!, createIfMissing: false);
            if (inventoryArray == null)
                return StorageTransportMoveOutcome.Failed("В рюкзаке нет предметов для перемещения.");

            var item = ResolveItem(inventoryArray, itemKey, "В рюкзаке нет выбранного предмета.");
            if (!item.Success)
                return StorageTransportMoveOutcome.Failed(item.Message);

            if (write)
            {
                var itemToMove = inventoryArray[item.Item!.Index]!;
                inventoryArray.RemoveAt(item.Item.Index);
                storageContents.Add(itemToMove);
                await WriteAsync(fs, writeLease, InventoryPath, inventoryRead.Root!.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                await WriteAsync(fs, writeLease, CurrentLocationPath, locationRead.Root!.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            }

            return StorageTransportMoveOutcome.Completed(
                item.Item!.Name,
                storage.Target.Name,
                $"«{item.Item.Name}» перемещён в хранилище «{storage.Target.Name}».");
        }

        var storageItem = ResolveItem(storageContents, itemKey, "В хранилище нет выбранного предмета.");
        if (!storageItem.Success)
            return StorageTransportMoveOutcome.Failed(storageItem.Message);

        if (write)
        {
            var playerInventory = GetPlayerInventoryArrayNode(inventoryRead.Root!, createIfMissing: true);
            if (playerInventory == null)
                return StorageTransportMoveOutcome.Failed("Инвентарь сейчас не похож на обычный рюкзак. Перемещение предметов временно недоступно.");

            var itemToMove = storageContents[storageItem.Item!.Index]!;
            storageContents.RemoveAt(storageItem.Item.Index);
            playerInventory.Add(itemToMove);
            await WriteAsync(fs, writeLease, InventoryPath, inventoryRead.Root!.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await WriteAsync(fs, writeLease, CurrentLocationPath, locationRead.Root!.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }

        return StorageTransportMoveOutcome.Completed(
            storageItem.Item!.Name,
            storage.Target.Name,
            $"«{storageItem.Item.Name}» извлечён из хранилища «{storage.Target.Name}» в рюкзак.");
    }

    private static async Task<StorageTransportMoveOutcome> MoveVehicleItemCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string direction,
        string vehicleKey,
        string itemKey,
        bool write)
    {
        if (!IsSupportedDirection(direction))
            return StorageTransportMoveOutcome.Failed("Выберите направление перемещения.");
        if (string.IsNullOrWhiteSpace(vehicleKey))
            return StorageTransportMoveOutcome.Failed("Выберите транспорт.");
        if (string.IsNullOrWhiteSpace(itemKey))
            return StorageTransportMoveOutcome.Failed("Выберите предмет.");

        var inventoryRead = await ReadObjectAsync(
            fs,
            writeLease,
            InventoryPath,
            "Инвентарь сейчас недоступен.");
        if (!inventoryRead.Success)
            return StorageTransportMoveOutcome.Failed(inventoryRead.Message);
        var vehiclesRead = await ReadNodeAsync(
            fs,
            writeLease,
            VehiclesPath,
            "Транспорт сейчас недоступен.");
        if (!vehiclesRead.Success)
            return StorageTransportMoveOutcome.Failed(vehiclesRead.Message);
        if (!TryGetVehiclesArray(vehiclesRead.Node, out var vehiclesArray))
            return StorageTransportMoveOutcome.Failed("Список транспорта сейчас пуст или недоступен.");

        var vehicle = ResolveVehicleTarget(vehiclesArray, vehicleKey);
        if (!vehicle.Success)
            return StorageTransportMoveOutcome.Failed(vehicle.Message);

        var vehicleInventory = EnsureContentsArray(
            vehicle.Target!.Node,
            "inventory",
            createIfMissing: direction == DirectionDeposit && write,
            treatMissingAsEmpty: direction == DirectionDeposit && !write);
        if (vehicleInventory == null && direction == DirectionDeposit)
            return StorageTransportMoveOutcome.Failed("Транспорт сейчас не готов принять предмет.");
        if (vehicleInventory == null)
            return StorageTransportMoveOutcome.Failed("В выбранном транспорте сейчас нет предметов.");

        if (direction == DirectionDeposit)
        {
            var inventoryArray = GetPlayerInventoryArrayNode(inventoryRead.Root!, createIfMissing: false);
            if (inventoryArray == null)
                return StorageTransportMoveOutcome.Failed("В рюкзаке нет предметов для перемещения.");

            var item = ResolveItem(inventoryArray, itemKey, "В рюкзаке нет выбранного предмета.");
            if (!item.Success)
                return StorageTransportMoveOutcome.Failed(item.Message);

            if (write)
            {
                var itemToMove = inventoryArray[item.Item!.Index]!;
                inventoryArray.RemoveAt(item.Item.Index);
                vehicleInventory.Add(itemToMove);
                await WriteAsync(fs, writeLease, InventoryPath, inventoryRead.Root!.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                await WriteAsync(fs, writeLease, VehiclesPath, vehiclesRead.Node!.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            }

            return StorageTransportMoveOutcome.Completed(
                item.Item!.Name,
                vehicle.Target.Name,
                $"«{item.Item.Name}» перемещён в транспорт «{vehicle.Target.Name}».");
        }

        var vehicleItem = ResolveItem(vehicleInventory, itemKey, "В транспорте нет выбранного предмета.");
        if (!vehicleItem.Success)
            return StorageTransportMoveOutcome.Failed(vehicleItem.Message);

        if (write)
        {
            var playerInventory = GetPlayerInventoryArrayNode(inventoryRead.Root!, createIfMissing: true);
            if (playerInventory == null)
                return StorageTransportMoveOutcome.Failed("Инвентарь сейчас не похож на обычный рюкзак. Перемещение предметов временно недоступно.");

            var itemToMove = vehicleInventory[vehicleItem.Item!.Index]!;
            vehicleInventory.RemoveAt(vehicleItem.Item.Index);
            playerInventory.Add(itemToMove);
            await WriteAsync(fs, writeLease, InventoryPath, inventoryRead.Root!.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await WriteAsync(fs, writeLease, VehiclesPath, vehiclesRead.Node!.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }

        return StorageTransportMoveOutcome.Completed(
            vehicleItem.Item!.Name,
            vehicle.Target.Name,
            $"«{vehicleItem.Item.Name}» извлечён из транспорта «{vehicle.Target.Name}» в рюкзак.");
    }

    private static bool IsSupportedDirection(string direction) =>
        string.Equals(direction, DirectionDeposit, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(direction, DirectionRetrieve, StringComparison.OrdinalIgnoreCase);

    private static async Task<JsonObjectRead> ReadObjectAsync(FileSystemManager fs, string path, string unavailableMessage)
    {
        return await ReadObjectAsync(fs, writeLease: null, path, unavailableMessage);
    }

    private static async Task<JsonObjectRead> ReadObjectAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path,
        string unavailableMessage)
    {
        var nodeRead = await ReadNodeAsync(fs, writeLease, path, unavailableMessage);
        if (!nodeRead.Success)
            return JsonObjectRead.Failed(nodeRead.Message);

        return nodeRead.Node is JsonObject obj
            ? JsonObjectRead.Completed(obj)
            : JsonObjectRead.Failed(unavailableMessage);
    }

    private static async Task<JsonNodeRead> ReadNodeAsync(FileSystemManager fs, string path, string unavailableMessage)
    {
        return await ReadNodeAsync(fs, writeLease: null, path, unavailableMessage);
    }

    private static async Task<JsonNodeRead> ReadNodeAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path,
        string unavailableMessage)
    {
        var raw = writeLease == null
            ? await fs.ReadFileAsync(path)
            : await fs.ReadFileAsync(writeLease, path);
        if (string.IsNullOrWhiteSpace(raw))
            return JsonNodeRead.Failed(unavailableMessage);

        try
        {
            var node = JsonNode.Parse(raw);
            return node == null
                ? JsonNodeRead.Failed(unavailableMessage)
                : JsonNodeRead.Completed(node);
        }
        catch (JsonException)
        {
            return JsonNodeRead.Failed("Игровое состояние сейчас не читается. Откройте действие заново после восстановления данных.");
        }
    }

    private static bool HasRecognizedInventoryShape(JsonObject root) =>
        root["items"] == null && root["UpdateInventory"] == null ||
        root["items"] is JsonArray ||
        root["UpdateInventory"] is JsonArray;

    private static JsonArray? GetPlayerInventoryArrayNode(JsonObject root, bool createIfMissing)
    {
        if (root["items"] is JsonArray items)
            return items;
        if (root["UpdateInventory"] is JsonArray updateInventory)
            return updateInventory;
        if (!createIfMissing)
            return null;

        var created = new JsonArray();
        root["UpdateInventory"] = created;
        return created;
    }

    private static bool TryGetVehiclesArray(JsonNode? root, out JsonArray vehicles)
    {
        vehicles = default!;
        if (root is JsonArray direct)
        {
            vehicles = direct;
            return true;
        }

        if (root is JsonObject obj && obj["vehicles"] is JsonArray nested)
        {
            vehicles = nested;
            return true;
        }

        return false;
    }

    private static JsonArray? EnsureContentsArray(
        JsonObject owner,
        string propertyName,
        bool createIfMissing,
        bool treatMissingAsEmpty = false)
    {
        if (owner[propertyName] is JsonArray existing)
            return existing;
        if (owner[propertyName] != null)
            return null;
        if (treatMissingAsEmpty)
            return new JsonArray();
        if (!createIfMissing)
            return null;

        var created = new JsonArray();
        owner[propertyName] = created;
        return created;
    }

    private static IReadOnlyList<StorageTransportItemOption> BuildItemOptions(
        JsonArray items,
        string descriptionPrefix,
        bool allowEmptyIdentity)
    {
        var entries = EnumerateItems(items, allowEmptyIdentity).ToArray();
        var labelCounts = entries
            .GroupBy(static entry => entry.BaseLabel, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        return entries
            .Select(entry =>
            {
                var label = entry.BaseLabel;
                if (labelCounts.TryGetValue(entry.BaseLabel, out var count) && count > 1)
                {
                    seen[entry.BaseLabel] = seen.GetValueOrDefault(entry.BaseLabel) + 1;
                    label = $"{entry.BaseLabel} (вариант {seen[entry.BaseLabel]})";
                }

                var description = string.IsNullOrWhiteSpace(descriptionPrefix)
                    ? entry.CountDescription
                    : $"{descriptionPrefix} {entry.CountDescription}".Trim();
                return new StorageTransportItemOption(entry.Key, entry.Identity, entry.Name, label, description);
            })
            .ToArray();
    }

    private static IEnumerable<ItemCandidate> EnumerateItems(JsonArray items, bool allowEmptyIdentity = true)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is not JsonObject item)
                continue;

            var identity = ReadItemIdentity(item);
            if (!allowEmptyIdentity && string.IsNullOrWhiteSpace(identity))
                continue;

            var name = FirstNonEmpty(GetString(item, "name"), GetString(item, "itemName"), "предмет");
            var count = FirstNonEmpty(GetString(item, "quantity"), GetString(item, "count"), "1");
            var baseLabel = string.Equals(count, "1", StringComparison.Ordinal)
                ? name
                : $"{name} ×{count}";
            var countDescription = string.Equals(count, "1", StringComparison.Ordinal)
                ? "Количество: 1."
                : $"Количество: {count}.";

            yield return new ItemCandidate(
                index,
                item,
                BuildReferenceKey(identity, index, item),
                identity,
                name,
                baseLabel,
                countDescription);
        }
    }

    private static IEnumerable<TargetCandidate> EnumerateStorageTargets(JsonArray storages, bool accessibleOnly)
    {
        for (var index = 0; index < storages.Count; index++)
        {
            if (storages[index] is not JsonObject storage)
                continue;

            var hasAccess = storage["hasFullAccess"] is JsonValue value &&
                            value.TryGetValue<bool>(out var parsed) &&
                            parsed;
            if (accessibleOnly && !hasAccess)
                continue;

            var contents = storage["contents"] as JsonArray ?? new JsonArray();
            var identity = GetString(storage, "storageId");
            var name = FirstNonEmpty(GetString(storage, "name"), identity, "Хранилище");
            yield return new TargetCandidate(
                index,
                storage,
                BuildReferenceKey(identity, index, storage),
                identity,
                name,
                contents);
        }
    }

    private static IEnumerable<TargetCandidate> EnumerateVehicleTargets(JsonArray vehicles, bool requireInventoryArray)
    {
        for (var index = 0; index < vehicles.Count; index++)
        {
            if (vehicles[index] is not JsonObject vehicle)
                continue;

            if (vehicle["inventory"] is not JsonArray inventory)
            {
                if (requireInventoryArray)
                    continue;
                inventory = new JsonArray();
            }

            var identity = FirstNonEmpty(GetString(vehicle, "vehicleId"), GetString(vehicle, "id"));
            var name = FirstNonEmpty(GetString(vehicle, "name"), identity, "Транспорт");
            yield return new TargetCandidate(
                index,
                vehicle,
                BuildReferenceKey(identity, index, vehicle),
                identity,
                name,
                inventory);
        }
    }

    private static ResolveTargetResult ResolveStorageTarget(JsonArray storages, string targetKey)
    {
        var resolved = ResolveTarget(EnumerateStorageTargets(storages, accessibleOnly: true).ToArray(), targetKey);
        if (resolved.Success)
            return resolved;

        return resolved.Message.Length == 0
            ? ResolveTargetResult.Failed("Такого доступного хранилища сейчас нет. Откройте форму заново.")
            : resolved;
    }

    private static ResolveTargetResult ResolveVehicleTarget(JsonArray vehicles, string targetKey)
    {
        var resolved = ResolveTarget(EnumerateVehicleTargets(vehicles, requireInventoryArray: false).ToArray(), targetKey);
        if (resolved.Success)
            return resolved;

        return resolved.Message.Length == 0
            ? ResolveTargetResult.Failed("Такого транспорта сейчас нет. Откройте форму заново.")
            : resolved;
    }

    private static ResolveTargetResult ResolveTarget(IReadOnlyList<TargetCandidate> candidates, string targetKey)
    {
        if (TryParseIdReference(targetKey, out var id))
        {
            var matches = candidates
                .Where(target => string.Equals(target.Identity, id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return matches.Length switch
            {
                1 => ResolveTargetResult.Completed(matches[0]),
                > 1 => ResolveTargetResult.Failed("Есть несколько похожих целей. Откройте форму заново и выберите точный вариант."),
                _ => ResolveTargetResult.Failed(string.Empty)
            };
        }

        if (TryParseIndexReference(targetKey, out var index, out var fingerprint))
        {
            var candidate = candidates.FirstOrDefault(target => target.Index == index);
            if (candidate == null)
                return ResolveTargetResult.Failed(string.Empty);
            return string.Equals(Fingerprint(candidate.Node), fingerprint, StringComparison.Ordinal)
                ? ResolveTargetResult.Completed(candidate)
                : ResolveTargetResult.Failed("Выбранная цель изменилась. Откройте форму заново.");
        }

        var fallbackMatches = candidates
            .Where(target =>
                string.Equals(target.Identity, targetKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target.Name, targetKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return fallbackMatches.Length switch
        {
            1 => ResolveTargetResult.Completed(fallbackMatches[0]),
            > 1 => ResolveTargetResult.Failed("Есть несколько похожих целей. Откройте форму заново и выберите точный вариант."),
            _ => ResolveTargetResult.Failed(string.Empty)
        };
    }

    private static ResolveItemResult ResolveItem(JsonArray items, string itemKey, string missingMessage)
    {
        var candidates = EnumerateItems(items).ToArray();
        if (TryParseIdReference(itemKey, out var id))
        {
            var matches = candidates
                .Where(item => string.Equals(item.Identity, id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return matches.Length switch
            {
                1 => ResolveItemResult.Completed(matches[0]),
                > 1 => ResolveItemResult.Failed("Есть несколько похожих предметов. Откройте форму заново и выберите точный вариант."),
                _ => ResolveItemResult.Failed(missingMessage)
            };
        }

        if (TryParseIndexReference(itemKey, out var index, out var fingerprint))
        {
            if (index < 0 || index >= items.Count || items[index] is not JsonObject current)
                return ResolveItemResult.Failed(missingMessage);
            return string.Equals(Fingerprint(current), fingerprint, StringComparison.Ordinal)
                ? ResolveItemResult.Completed(candidates.First(item => item.Index == index))
                : ResolveItemResult.Failed("Выбранный предмет изменился. Откройте форму заново.");
        }

        var fallbackMatches = candidates
            .Where(item =>
                string.Equals(item.Identity, itemKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Name, itemKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return fallbackMatches.Length switch
        {
            1 => ResolveItemResult.Completed(fallbackMatches[0]),
            > 1 => ResolveItemResult.Failed("Есть несколько похожих предметов. Откройте форму заново и выберите точный вариант."),
            _ => ResolveItemResult.Failed(missingMessage)
        };
    }

    private static string BuildReferenceKey(string identity, int index, JsonObject node) =>
        string.IsNullOrWhiteSpace(identity)
            ? $"idx:{index}:{Fingerprint(node)}"
            : "id:" + Uri.EscapeDataString(identity);

    private static bool TryParseIdReference(string value, out string id)
    {
        id = string.Empty;
        if (!value.StartsWith("id:", StringComparison.Ordinal))
            return false;

        id = Uri.UnescapeDataString(value[3..]);
        return !string.IsNullOrWhiteSpace(id);
    }

    private static bool TryParseIndexReference(string value, out int index, out string fingerprint)
    {
        index = -1;
        fingerprint = string.Empty;
        if (!value.StartsWith("idx:", StringComparison.Ordinal))
            return false;

        var parts = value.Split(':', 3);
        if (parts.Length != 3 || !int.TryParse(parts[1], out index) || string.IsNullOrWhiteSpace(parts[2]))
            return false;

        fingerprint = parts[2];
        return true;
    }

    private static string Fingerprint(JsonObject node)
    {
        var json = node.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }

    private static string ReadItemIdentity(JsonObject item) =>
        FirstNonEmpty(GetString(item, "existedId"), GetString(item, "itemId"), GetString(item, "id"));

    private static string GetString(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            return string.Empty;
        if (value.TryGetValue<string>(out var text))
            return text ?? string.Empty;
        if (value.TryGetValue<int>(out var number))
            return number.ToString();
        if (value.TryGetValue<long>(out var longNumber))
            return longNumber.ToString();
        return string.Empty;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static Task WriteAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path,
        string content) =>
        writeLease == null
            ? fs.WriteFileAtomicAsync(path, content)
            : fs.WriteFileAtomicAsync(writeLease, path, content);

    private sealed record JsonNodeRead(bool Success, JsonNode? Node, string Message)
    {
        public static JsonNodeRead Completed(JsonNode node) => new(true, node, string.Empty);
        public static JsonNodeRead Failed(string message) => new(false, null, message);
    }

    private sealed record JsonObjectRead(bool Success, JsonObject? Root, string Message)
    {
        public static JsonObjectRead Completed(JsonObject root) => new(true, root, string.Empty);
        public static JsonObjectRead Failed(string message) => new(false, null, message);
    }

    private sealed record ItemCandidate(
        int Index,
        JsonObject Node,
        string Key,
        string Identity,
        string Name,
        string BaseLabel,
        string CountDescription);

    private sealed record TargetCandidate(
        int Index,
        JsonObject Node,
        string Key,
        string Identity,
        string Name,
        JsonArray Contents);

    private sealed record ResolveItemResult(bool Success, ItemCandidate? Item, string Message)
    {
        public static ResolveItemResult Completed(ItemCandidate item) => new(true, item, string.Empty);
        public static ResolveItemResult Failed(string message) => new(false, null, message);
    }

    private sealed record ResolveTargetResult(bool Success, TargetCandidate? Target, string Message)
    {
        public static ResolveTargetResult Completed(TargetCandidate target) => new(true, target, string.Empty);
        public static ResolveTargetResult Failed(string message) => new(false, null, message);
    }
}

public sealed record StorageMoveContext(
    bool Success,
    string Message,
    IReadOnlyList<StorageTransportItemOption> InventoryItems,
    IReadOnlyList<StorageMoveStorage> Storages)
{
    public static StorageMoveContext Failed(string message) => new(false, message, [], []);
}

public sealed record StorageMoveStorage(
    string Key,
    string StorageId,
    string Name,
    int ContentsCount,
    IReadOnlyList<StorageTransportItemOption> Contents);

public sealed record VehicleMoveContext(
    bool Success,
    string Message,
    IReadOnlyList<StorageTransportItemOption> InventoryItems,
    IReadOnlyList<VehicleMoveVehicle> Vehicles)
{
    public static VehicleMoveContext Failed(string message) => new(false, message, [], []);
}

public sealed record VehicleMoveVehicle(
    string Key,
    string VehicleId,
    string Name,
    int ContentsCount,
    IReadOnlyList<StorageTransportItemOption> Contents);

public sealed record StorageTransportItemOption(
    string Key,
    string Identity,
    string Name,
    string Label,
    string Description);

public sealed record StorageTransportMoveOutcome(
    bool Success,
    string ItemName,
    string TargetName,
    string Message)
{
    public static StorageTransportMoveOutcome Completed(string itemName, string targetName, string message) =>
        new(true, itemName, targetName, message);

    public static StorageTransportMoveOutcome Failed(string message) =>
        new(false, string.Empty, string.Empty, message);
}
