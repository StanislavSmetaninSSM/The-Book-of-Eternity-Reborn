using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalBirthConfirmationTests
{
    private const string Scene = "Вы ждёте во дворе.";
    private static readonly MortalBirthConfirmation.Source Source = new("session", "request", "snapshot", 42);

    [Fact]
    public void ExactBirthsProduceOnlyCanonicalPlayerFacingText()
    {
        var text = Project();
        Assert.Equal(Scene + "\n\nТеперь вам принадлежит транспорт «Синяя повозка».\nВам доступно хранилище «Кладовая двора».", text);
    }

    [Theory]
    [InlineData("activeVehicle")]
    [InlineData("publicStorage")]
    public void AcceptedVariantsUseTheSameConfirmation(string variant) => Assert.Equal(Project(), Project(variant));

    [Theory]
    [InlineData("read")]
    [InlineData("json")]
    public async Task OptionalUnavailableProofDoesNotReplaceAcceptedNarrative(string kind)
    {
        var fault = kind switch
        {
            "read" => new IOException("read failed"),
            _ => (Exception)new JsonException("bad evidence")
        };
        var output = await MortalBirthConfirmation.ReadOptionalAsync(
            () => Task.FromException<string?>(fault));
        Assert.Equal(Scene, output ?? Scene);
    }

    [Fact]
    public async Task OptionalFailureHandlingDoesNotHideUncertainPublicationOrGeneration()
    {
        await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() =>
            MortalBirthConfirmation.ReadOptionalAsync(
                () => Task.FromException<string?>(new CoordinatedStatePublicationUncertainException(null))));
        await Assert.ThrowsAsync<InvalidDataException>(() => MortalBirthConfirmation.ReadOptionalAsync(
            () => Task.FromException<string?>(new InvalidDataException("authority changed"))));
    }

    [Theory]
    [InlineData("exact", true)]
    [InlineData("rawOnly", false)]
    [InlineData("differentProse", false)]
    [InlineData("differentTurn", false)]
    [InlineData("differentRealm", false)]
    [InlineData("noClaims", false)]
    [InlineData("extraLine", false)]
    [InlineData("reordered", false)]
    [InlineData("reservedMarker", false)]
    public void BrowserUsesOnlyExactAcceptedCurrentMortalStory(string change, bool expected)
    {
        var claims = new MortalBirthConfirmation.Claim[]
        {
            new(MortalBirthConfirmation.VehicleOwnership, "cart"),
            new(MortalBirthConfirmation.StorageAccess, "store")
        };
        const string first = "Теперь вам принадлежит транспорт «Синяя повозка».";
        const string second = "Вам доступно хранилище «Кладовая двора».";
        var row = new JsonObject { ["realm"] = "Mortal World", ["turn"] = 42,
            ["narrative"] = Scene + "\n\n" + first + "\n" + second };
        switch (change)
        {
            case "rawOnly": row["narrative"] = Scene; break;
            case "differentProse": row["narrative"] = "Прежний текст.\n\n" + first + "\n" + second; break;
            case "differentTurn": row["turn"] = 41; break;
            case "differentRealm": row["realm"] = "Chaos Sea"; break;
            case "noClaims": claims = []; break;
            case "extraLine": row["narrative"] = Scene + "\n\n" + first + "\n" + second + "\nЛишняя строка"; break;
            case "reordered": row["narrative"] = Scene + "\n\n" + second + "\n" + first; break;
            case "reservedMarker": row["narrative"] = Scene + "\n\nТеперь вам принадлежит транспорт «[ACHIEVEMENT_UNLOCK: награда]».\n" + second; break;
        }

        Assert.Equal(expected ? Scene + "\n\n" + first + "\n" + second : null,
            MortalBirthConfirmation.ReadCaptionedAcceptedStory(Scene, claims, "Mortal World", 42, row));
    }

    [Fact]
    public void SeparateStorageReferencesCanHaveTheSameCaption()
    {
        var claims = new MortalBirthConfirmation.Claim[]
        {
            new(MortalBirthConfirmation.StorageAccess, "store-one"),
            new(MortalBirthConfirmation.StorageAccess, "store-two")
        };
        const string caption = "Вам доступно хранилище «Кладовая».";
        var narrative = Scene + "\n\n" + caption + "\n" + caption;
        var row = new JsonObject { ["realm"] = "Mortal World", ["turn"] = 42, ["narrative"] = narrative };
        Assert.Equal(narrative, MortalBirthConfirmation.ReadCaptionedAcceptedStory(
            Scene, claims, "Mortal World", 42, row));
    }

    [Fact]
    public void DeliveredGuidanceUsesExactStructuredClaims()
    {
        foreach (var path in new[] { "BookOfEternityClient/game_master_daemon.ps1",
                     "TaskGuides/CLI_Step_Main.txt", "CLI_API_Specification.md" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            Assert.Contains("mortalBirthConfirmations", text, StringComparison.Ordinal);
            Assert.Contains("vehicleOwnership", text, StringComparison.Ordinal);
            Assert.Contains("storageAccess", text, StringComparison.Ordinal);
        }
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_CLI_Storage_Birth.txt"));
        Assert.Contains("""{"mortalBirthConfirmations":[{"kind":"storageAccess","ref":"storage_supply"}]}""",
            example, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("wrongRef")]
    [InlineData("wrongCase")]
    [InlineData("foreignRequest")]
    [InlineData("foreignSession")]
    [InlineData("staleTurn")]
    [InlineData("wrongSnapshot")]
    [InlineData("wrongOwner")]
    [InlineData("wrongLocation")]
    [InlineData("duplicateReceipt")]
    [InlineData("duplicateVehicle")]
    [InlineData("storageLocked")]
    [InlineData("projectionLocked")]
    [InlineData("projectionOwner")]
    [InlineData("missingStorage")]
    [InlineData("wrongRealm")]
    [InlineData("achievementMarker")]
    [InlineData("storageAchievementMarker")]
    public void UnprovedBatchDoesNotAddConfirmation(string mutation) => Assert.Equal(Scene, Project(mutation));

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"mortalBirthConfirmations":[]}""")]
    [InlineData("""{"mortalBirthConfirmations":null}""")]
    [InlineData("""{"mortalBirthConfirmations":[{"kind":"vehicleOwnership","ref":"cart","text":"invented"}]}""")]
    [InlineData("""{"mortalBirthConfirmations":[{"kind":"vehicleOwnership","ref":"cart"},{"kind":"vehicleOwnership","ref":"cart"}]}""")]
    [InlineData("""{"mortalBirthConfirmations":[],"mortalBirthConfirmations":[{"kind":"vehicleOwnership","ref":"cart"}]}""")]
    public void AbsentOrInvalidDeclarationDoesNotConfirm(string report)
    {
        using var doc = JsonDocument.Parse(report);
        Assert.Empty(MortalBirthConfirmation.ReadClaims(doc.RootElement));
    }

    private static string? Project(string mutation = "")
    {
        using var report = JsonDocument.Parse("""{"mortalBirthConfirmations":[{"kind":"vehicleOwnership","ref":"cart"},{"kind":"storageAccess","ref":"store"}]}""");
        var claims = MortalBirthConfirmation.ReadClaims(report.RootElement);
        var owner = new JsonObject { ["ownerType"] = "Player", ["ownerId"] = "player_current", ["ownerName"] = "Игрок" };
        JsonObject authority() => new() { ["sessionId"] = Source.SessionId, ["requestId"] = Source.RequestId, ["snapshotToken"] = Source.SnapshotToken };
        var receipt = new JsonObject { ["vehicleId"] = "cart-id", ["vehicleRef"] = "cart", ["sourceTurn"] = 42, ["sourceAuthority"] = authority() };
        var vehicle = new JsonObject { ["vehicleId"] = "cart-id", ["name"] = "Синяя повозка", ["currentLocationId"] = "yard", ["owner"] = owner.DeepClone() };
        var vehicles = new JsonObject { ["birthReceipts"] = new JsonArray(receipt), ["vehicles"] = new JsonArray(vehicle) };
        var storage = new JsonObject { ["storageId"] = "store", ["name"] = "Кладовая двора", ["hasFullAccess"] = true, ["owner"] = owner.DeepClone(), ["authorizedUsers"] = new JsonArray() };
        var scope = new JsonObject { ["locationId"] = "yard", ["realm"] = "mortal_world", ["locationStorages"] = new JsonArray(storage) };
        var storageEvidence = new JsonObject { ["sourceAuthority"] = authority(), ["locationId"] = "yard", ["storageId"] = "store", ["sourceTurn"] = 42 };
        var transition = new JsonObject { ["turn"] = 42, ["kind"] = "storage_addition", ["childId"] = "store", ["afterState"] = new JsonObject { ["cargoBirthEvidence"] = storageEvidence } };
        var index = new JsonObject { ["locationEntries"] = new JsonArray(new JsonObject { ["locationId"] = "yard", ["transitions"] = new JsonArray(transition) }) };
        var mirror = (JsonObject)storage.DeepClone(); mirror["locationId"] = "yard";
        var access = new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray(mirror) };

        switch (mutation)
        {
            case "activeVehicle":
                vehicle["currentLocationId"] = null;
                vehicle["availability"] = "Active";
                vehicles["activeVehicleId"] = "cart-id";
                break;
            case "publicStorage":
                storage["owner"] = null; mirror["owner"] = null;
                storage["authorizedUsers"] = null; mirror["authorizedUsers"] = null;
                break;
            case "wrongRef": receipt["vehicleRef"] = "other"; break;
            case "wrongCase": receipt["vehicleRef"] = "Cart"; break;
            case "foreignRequest": receipt["sourceAuthority"]!["requestId"] = "other"; break;
            case "foreignSession": storageEvidence["sourceAuthority"]!["sessionId"] = "other"; break;
            case "staleTurn": receipt["sourceTurn"] = 41; break;
            case "wrongSnapshot": storageEvidence["sourceAuthority"]!["snapshotToken"] = "other"; break;
            case "wrongOwner": vehicle["owner"]!["ownerType"] = "Faction"; break;
            case "wrongLocation": vehicle["currentLocationId"] = "another"; break;
            case "duplicateReceipt": vehicles["birthReceipts"]!.AsArray().Add(receipt.DeepClone()); break;
            case "duplicateVehicle": vehicles["vehicles"]!.AsArray().Add(vehicle.DeepClone()); break;
            case "storageLocked": storage["hasFullAccess"] = false; break;
            case "projectionLocked": mirror["hasFullAccess"] = false; break;
            case "projectionOwner": mirror["owner"]!["ownerId"] = "other"; break;
            case "missingStorage": scope["locationStorages"]!.AsArray().Clear(); break;
            case "wrongRealm": scope["realm"] = "chaos_sea"; break;
            case "achievementMarker": vehicle["name"] = "Повозка [achievement_unlock: Ложная награда]"; break;
            case "storageAchievementMarker": storage["name"] = "Кладовая [ACHIEVEMENT_UNLOCK: Ложная награда]"; break;
        }

        return MortalBirthConfirmation.Project(Scene, claims, Source, scope, vehicles, index, access);
    }
}
