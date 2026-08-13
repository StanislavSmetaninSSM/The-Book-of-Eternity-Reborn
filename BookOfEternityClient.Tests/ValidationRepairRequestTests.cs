using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ValidationRepairRequestTests
{
    [Fact]
    public void MortalLocationPacketSerialization_PreservesExactContextAndExcludesClientOwnedTargets()
    {
        const string actor = "mortal_location:new:locref_turn_24_ashen_bridge";
        var context = new MortalLocationRepairContext(
            "worldMapUpdates.newLocations[3]",
            "mortal_location",
            "locref_turn_24_ashen_bridge",
            "mlocmat_turn_24_ashen_bridge",
            new[] { "description", "coordinates" });
        var missing = CreateIssue(
            "game_state/world/world_map.json.worldMapUpdates.newLocations[3].description",
            "mortal_location_materialization_governed_field_missing",
            actor,
            "complete description",
            "missing",
            context,
            MortalLocationMaterializationContract.WorldMapPath);
        var conflict = CreateIssue(
            "game_state/world/world_map.json.worldMapUpdates.newLocations[3].coordinates",
            "mortal_location_materialization_coordinate_collision",
            actor,
            "unique exact coordinate",
            "x=7,y=-2,z=0 already used",
            context,
            MortalLocationMaterializationContract.WorldMapPath);

        var sourcePacket = Assert.Single(
            MortalLocationRepairPacketBuilder.Build(new[] { missing, conflict }));
        var converter = typeof(GameEngine).GetMethod(
            "BuildMortalLocationRepairHarnessPacket",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(converter);
        var harnessPacket = converter!.Invoke(null, new object[] { sourcePacket });
        Assert.NotNull(harnessPacket);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            harnessPacket,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var packet = document.RootElement;

        Assert.Equal("mortal_location_materialization_repair", packet.GetProperty("kind").GetString());
        Assert.Equal("world_map_creation", packet.GetProperty("transitionClass").GetString());
        Assert.Equal("world_map_creation", packet.GetProperty("route").GetString());
        Assert.Equal("worldMapUpdates", packet.GetProperty("rawCarrier").GetString());
        Assert.Equal("worldMapUpdates.newLocations[3]", packet.GetProperty("rawCoordinate").GetString());
        Assert.Equal(
            new[] { actor },
            packet.GetProperty("canonicalActorNames").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            packet.GetProperty("targetFiles").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            new[] { missing.FilePath },
            packet.GetProperty("missingFields").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            new[] { conflict.FilePath },
            packet.GetProperty("conflicts").EnumerateArray().Select(value => value.GetString()));
        Assert.DoesNotContain(
            packet.GetProperty("targetFiles").EnumerateArray(),
            value => MortalLocationRepairPacketBuilder.IsProtectedClientOwnedTarget(value.GetString() ?? ""));
        Assert.DoesNotContain(
            packet.GetProperty("exactFieldCorrections").EnumerateArray(),
            correction => correction.GetProperty("path").GetString()?.Contains(
                "location_identity_index",
                StringComparison.Ordinal) == true);
    }

    [Fact]
    public void MortalLocationRepairDispatch_FailsClosedBeforeWritingRequestAndHasNoLegacyPacketPath()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            "GameEngine.ValidationAndRepair.cs"));
        var methodStart = source.IndexOf(
            "private async Task<bool> WaitForContractRepairAsync(",
            StringComparison.Ordinal);
        var nextMethod = source.IndexOf("\n    private ", methodStart + 1, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && nextMethod > methodStart);
        var method = source[methodStart..nextMethod];

        var failClosed = method.IndexOf(
            "MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(errors)",
            StringComparison.Ordinal);
        var dispatch = method.IndexOf(
            "WriteValidationRepairRequestForSessionAsync(",
            StringComparison.Ordinal);
        Assert.True(failClosed >= 0 && dispatch > failClosed);
        Assert.Contains("return false;", method[failClosed..dispatch], StringComparison.Ordinal);
        Assert.DoesNotContain("mortal_location_transition_repair", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mortal_world_map_adjacency_repair", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildMortalLocationTransitionRepairPacket", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildMortalWorldMapAdjacencyRepairPacket", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalLocationRepairFailure_PlayerConsoleUsesSafeRussianWithoutOperatorVocabulary()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            "GameEngine.ValidationAndRepair.cs"));
        var methodStart = source.IndexOf(
            "private async Task<bool> WaitForContractRepairAsync(",
            StringComparison.Ordinal);
        var nextMethod = source.IndexOf("\n    private ", methodStart + 1, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && nextMethod > methodStart);
        var method = source[methodStart..nextMethod];
        var failClosed = method.IndexOf(
            "MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(errors)",
            StringComparison.Ordinal);
        var playerMessageStart = method.IndexOf("AnsiConsole.MarkupLine(", failClosed, StringComparison.Ordinal);
        var failureReturn = method.IndexOf("return false;", playerMessageStart, StringComparison.Ordinal);
        Assert.True(failClosed >= 0 && playerMessageStart > failClosed && failureReturn > playerMessageStart);
        var playerMessage = method[playerMessageStart..failureReturn];

        Assert.Contains("Изменения мира не были приняты", playerMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("authority", playerMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accepted turn", playerMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pre-turn", playerMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GM repair", playerMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation", playerMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MortalLocationRepairFailure_CallerRollbackUsesOnlyInWorldLanguage()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            "GameEngine.TurnLifecycle.cs"));

        Assert.DoesNotContain("отклонён проверкой контракта", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("отклонено проверкой контракта", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("отклонена проверкой контракта", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("отклонён после материализации", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Изменения мира не были приняты; состояние до хода восстановлено.",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MortalLocationRepairWait_PlayerStatusUsesOnlyInWorldLanguage()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            "GameEngine.ValidationAndRepair.cs"));
        var methodStart = source.IndexOf(
            "private async Task<bool> WaitForContractRepairAsync(",
            StringComparison.Ordinal);
        var nextMethod = source.IndexOf("\n    private ", methodStart + 1, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && nextMethod > methodStart);
        var method = source[methodStart..nextMethod];

        Assert.DoesNotContain("GM исправляет невалидное состояние", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Ожидание исправления GM", method, StringComparison.Ordinal);
        Assert.DoesNotContain("попытка проверки", method, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ремонтный цикл", method, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GM bridge завис", method, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("остановлен harness", method, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("новую попытку исправления", method, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мир ещё не готов продолжить ход", method, StringComparison.Ordinal);
        Assert.Contains("RestorePreTurnBaselineForRepairSessionAsync", method, StringComparison.Ordinal);
        Assert.DoesNotContain("CleanupBackup", method, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticOnlyRepairFailure_PlayerConsoleUsesSafeRussianWithoutOperatorVocabulary()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            "GameEngine.ValidationAndRepair.cs"));
        var methodStart = source.IndexOf(
            "private async Task<bool> FailClosedDiagnosticOnlyValidationRepairAsync(",
            StringComparison.Ordinal);
        var nextMethod = source.IndexOf("\n    private ", methodStart + 1, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && nextMethod > methodStart);
        var method = source[methodStart..nextMethod];
        var playerMessages = string.Join(
            "\n",
            Regex.Matches(
                    method,
                    "AnsiConsole\\.MarkupLine\\((?<arguments>.*?)\\);",
                    RegexOptions.Singleline)
                .SelectMany(match => Regex.Matches(
                    match.Groups["arguments"].Value,
                    "\"(?<message>[^\"]+)\""))
                .Select(match => match.Groups["message"].Value));

        Assert.Contains("Изменения мира не были приняты", playerMessages, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostic", playerMessages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("repair", playerMessages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", playerMessages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation", playerMessages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("backup", playerMessages, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistingLocationCoordinateMismatch_UsesBoundedLocationPacketWithoutBootstrapScaffold()
    {
        const string locationId = "loc_existing_coordinate_mismatch";
        var issue = new ValidationIssue(
            MortalLocationMaterializationContract.CurrentLocationPath + ".currentLocationData.coordinates",
            IssueSeverity.Error,
            "Current selection coordinates differ from the accepted location.",
            code: "current_location_coordinates_mismatch",
            actor: "mortal_location:existing:" + locationId,
            section: "Location",
            expected: "exact accepted coordinates",
            actual: "x=9,y=0,z=0",
            repairHint: "Reduce the resend to the exact current-selection route.",
            repairTargetFiles: new[] { MortalLocationMaterializationContract.CurrentLocationPath });
        issue.MortalLocationRepairContext = new MortalLocationRepairContext(
            "currentLocationData",
            "mortal_location",
            InitialId: null,
            MaterializationId: "mlocmat_existing_coordinate_mismatch",
            RepairableFields: new[] { "coordinates" },
            ExistingId: locationId);
        var builder = typeof(GameEngine).GetMethod(
            "BuildValidationRepairHarnessPackets",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(builder);

        var packets = builder!.Invoke(null, new object?[] { new[] { issue }, null });
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            packets,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var packet = Assert.Single(document.RootElement.EnumerateArray());

        Assert.Equal("mortal_location_materialization_repair", packet.GetProperty("kind").GetString());
        Assert.Equal("current_selection", packet.GetProperty("transitionClass").GetString());
        Assert.Equal(
            new[] { MortalLocationMaterializationContract.CurrentLocationPath },
            packet.GetProperty("targetFiles").EnumerateArray().Select(value => value.GetString()));
        Assert.DoesNotContain(
            "mortal_bootstrap_scaffold.json",
            packet.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static ValidationIssue CreateIssue(
        string path,
        string code,
        string actor,
        string expected,
        string actual,
        MortalLocationRepairContext context,
        params string[] repairTargets)
    {
        var issue = new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Malformed Mortal location package.",
            code: code,
            actor: actor,
            section: "MortalLocationMaterialization",
            expected: expected,
            actual: actual,
            repairHint: "Repair only this exact GM-owned field.",
            repairTargetFiles: repairTargets);
        issue.MortalLocationRepairContext = context;
        return issue;
    }
}
