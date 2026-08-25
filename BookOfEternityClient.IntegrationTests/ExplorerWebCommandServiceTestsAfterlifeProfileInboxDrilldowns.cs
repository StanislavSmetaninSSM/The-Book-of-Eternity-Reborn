using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class ExplorerWebCommandServiceTestsAfterlifeProfileInboxDrilldowns : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ExplorerWebCommandService _service;

    public ExplorerWebCommandServiceTestsAfterlifeProfileInboxDrilldowns()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-web-afterlife-profile-inbox-drilldowns-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        var stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _service = new ExplorerWebCommandService(_fs, stateManager, new LocalizationManager(), validation);
    }

    [Theory]
    [InlineData("/afterlife_threats", "afterlife-threat-detail-threat_moth", "/afterlife_threats угроза threat_moth", "Моль Сомнений", "Видимые угрозы")]
    [InlineData("/afterlife_chronicles", "afterlife-chronicle-detail-chronicle_mirror", "/хроники_посмертия хроника \"Зал зеркальной клятвы\"", "Зал зеркальной клятвы", "Видимые хроники")]
    public async Task ExecuteAsync_AfterlifeThreatChronicleOverviews_ExposeIssue1066ReadOnlyDetailActions(
        string command,
        string expectedActionId,
        string expectedCommand,
        string expectedLabelText,
        string expectedOverviewText)
    {
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1066TechnicalLeak(result);
        Assert.Contains(expectedOverviewText, CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
        AssertIssue1066Action(result, expectedActionId, expectedCommand, expectedLabelText);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfileOverview_UsesOpaqueActionThatSurvivesProfileReordering()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        const string actorId = "guardian_mirror";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));

        Assert.Equal(CommandExecutionState.Completed, overview.State);
        var action = Assert.Single(overview.Actions, candidate =>
            candidate.Label.Contains("Хранитель Зеркал", StringComparison.Ordinal));
        Assert.Matches(
            "^afterlife-profile-detail-afterlife_profile_[0-9a-f]{24}$",
            action.Id);
        Assert.Matches(
            "^/afterlife_profiles действие afterlife_profile_[0-9a-f]{24}$",
            action.Command);
        Assert.DoesNotContain(actorId, SerializePlayerFacingResult(overview), StringComparison.Ordinal);

        var root = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        root["profiles"]!.AsArray().Insert(0, new JsonObject
        {
            ["actorType"] = "resident",
            ["actorId"] = "resident_ember",
            ["displayName"] = "Резидент Угля",
            ["realm"] = "Chaos Sea"
        });
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));

        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(action.Command));

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        var detailText = CollectBlockText(detail.Blocks);
        Assert.Contains("Профиль посмертия: Хранитель Зеркал", detailText, StringComparison.Ordinal);
        Assert.DoesNotContain("Профиль посмертия: Резидент Угля", detailText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfileOpaqueAction_DuplicateAuthorityFailsClosed()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var action = Assert.Single(overview.Actions, candidate =>
            candidate.Label.Contains("Хранитель Зеркал", StringComparison.Ordinal));
        var root = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        var profiles = root["profiles"]!.AsArray();
        var duplicate = profiles[0]!.DeepClone().AsObject();
        duplicate["displayName"] = "Двойник Зеркала";
        profiles.Insert(1, duplicate);
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));

        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(action.Command));

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        Assert.Contains("Профиль недоступен", CollectBlockText(detail.Blocks), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfileActions_UseTypedCanonicalAuthority()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var root = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        root["profiles"]!.AsArray().Insert(1, new JsonObject
        {
            ["actorType"] = "resident",
            ["actorId"] = "guardian_mirror",
            ["displayName"] = "Резидент Зеркал",
            ["realm"] = "Chaos Sea"
        });
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));

        var guardianAction = Assert.Single(overview.Actions, candidate =>
            candidate.Label.Contains("Хранитель Зеркал", StringComparison.Ordinal));
        var residentAction = Assert.Single(overview.Actions, candidate =>
            candidate.Label.Contains("Резидент Зеркал", StringComparison.Ordinal));
        Assert.NotEqual(guardianAction.Id, residentAction.Id);
        Assert.NotEqual(guardianAction.Command, residentAction.Command);

        var guardianDetail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(guardianAction.Command));
        var residentDetail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(residentAction.Command));
        Assert.Contains(
            "Профиль посмертия: Хранитель Зеркал",
            CollectBlockText(guardianDetail.Blocks),
            StringComparison.Ordinal);
        Assert.Contains(
            "Профиль посмертия: Резидент Зеркал",
            CollectBlockText(residentDetail.Blocks),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfileOpaqueAction_DoesNotRetargetAfterActorTypeChanges()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var action = Assert.Single(overview.Actions, candidate =>
            candidate.Label.Contains("Хранитель Зеркал", StringComparison.Ordinal));
        var root = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        var profile = root["profiles"]![0]!.AsObject();
        profile["actorType"] = "resident";
        profile["displayName"] = "Резидент Зеркал";
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));

        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(action.Command));

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        Assert.Contains("Профиль недоступен", CollectBlockText(detail.Blocks), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeInboxProfileAction_UsesTypedLegacyActorRefAuthority()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var root = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        var profiles = root["profiles"]!.AsArray();
        var guardian = profiles[0]!.AsObject();
        guardian.Remove("actorId");
        guardian["actorRef"] = "guardian_mirror";
        profiles.Insert(1, new JsonObject
        {
            ["actorType"] = "resident",
            ["actorRef"] = "guardian_mirror",
            ["displayName"] = "Резидент Зеркал",
            ["realm"] = "Chaos Sea"
        });
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));

        var inbox = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_inbox"));
        var action = Assert.Single(inbox.Actions, static candidate =>
            candidate.Id.StartsWith(
                "afterlife-inbox-profile-notif_guardian_profile-",
                StringComparison.Ordinal));
        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(action.Command));

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        Assert.Contains(
            "Профиль посмертия: Хранитель Зеркал",
            CollectBlockText(detail.Blocks),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Профиль посмертия: Резидент Зеркал",
            CollectBlockText(detail.Blocks),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfileWithoutCanonicalIdentity_HasNoGeneratedAction()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var root = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        var profile = root["profiles"]![0]!.AsObject();
        profile.Remove("actorId");
        profile["profileId"] = "legacy_profile_mirror";
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var payload = SerializePlayerFacingResult(overview);

        Assert.Contains("Хранитель Зеркал", CollectBlockText(overview.Blocks), StringComparison.Ordinal);
        Assert.DoesNotContain("afterlife-profile-detail-", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("/afterlife_profiles действие", payload, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("7")]
    [InlineData("true")]
    public async Task ExecuteAsync_AfterlifeProfileWithNonStringActorId_HasNoGeneratedAction(
        string actorIdJson)
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var root = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        var profile = root["profiles"]![0]!.AsObject();
        profile["actorId"] = JsonNode.Parse(actorIdJson);
        profile.Remove("actorRef");
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));

        Assert.Contains("Хранитель Зеркал", CollectBlockText(overview.Blocks), StringComparison.Ordinal);
        Assert.DoesNotContain(overview.Actions, candidate =>
            candidate.Label.Contains("Хранитель Зеркал", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfileWithNonStringActorId_UsesStringActorRefAuthority()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var root = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        var profile = root["profiles"]![0]!.AsObject();
        profile["actorId"] = 7;
        profile["actorRef"] = "guardian_mirror";
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));
        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var action = Assert.Single(overview.Actions, candidate =>
            candidate.Label.Contains("Хранитель Зеркал", StringComparison.Ordinal));

        profile["actorId"] = 8;
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));

        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(action.Command));

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        Assert.Contains(
            "Профиль посмертия: Хранитель Зеркал",
            CollectBlockText(detail.Blocks),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeInboxProfileAction_RejectsNonStringExplicitActorId()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        const string notificationsPath = "game_state/control/afterlife_notifications.json";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var profilesRoot = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        profilesRoot["profiles"]![0]!["actorId"] = "7";
        await _fs.WriteFileAtomicAsync(profilesPath, profilesRoot.ToJsonString(JsonOptions));
        var notificationsRoot = JsonNode.Parse((await _fs.ReadFileAsync(notificationsPath))!)!.AsObject();
        var notification = notificationsRoot["notifications"]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(candidate =>
                candidate["notificationId"]?.GetValue<string>() == "notif_guardian_profile");
        notification["profileActorId"] = 7;
        notification["profileActorType"] = "guardian";
        notification.Remove("guardianId");
        notification.Remove("residentId");
        await _fs.WriteFileAtomicAsync(notificationsPath, notificationsRoot.ToJsonString(JsonOptions));

        var inbox = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_inbox"));

        Assert.DoesNotContain(inbox.Actions, static candidate =>
            candidate.Id.StartsWith(
                "afterlife-inbox-profile-notif_guardian_profile-",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeInboxProfileAction_DoesNotGuessBetweenGuardianAndResidentCoordinates()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        const string notificationsPath = "game_state/control/afterlife_notifications.json";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var profilesRoot = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        profilesRoot["profiles"]!.AsArray().Insert(1, new JsonObject
        {
            ["actorType"] = "resident",
            ["actorId"] = "resident_ember",
            ["displayName"] = "Резидент Угля",
            ["realm"] = "Chaos Sea"
        });
        await _fs.WriteFileAtomicAsync(profilesPath, profilesRoot.ToJsonString(JsonOptions));
        var notificationsRoot = JsonNode.Parse((await _fs.ReadFileAsync(notificationsPath))!)!.AsObject();
        var notification = notificationsRoot["notifications"]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(candidate =>
                candidate["notificationId"]?.GetValue<string>() == "notif_guardian_profile");
        notification.Remove("profileActorId");
        notification.Remove("profileActorType");
        notification["guardianId"] = "guardian_mirror";
        notification["residentId"] = "resident_ember";
        await _fs.WriteFileAtomicAsync(notificationsPath, notificationsRoot.ToJsonString(JsonOptions));

        var inbox = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_inbox"));

        Assert.DoesNotContain(inbox.Actions, static candidate =>
            candidate.Id.StartsWith(
                "afterlife-inbox-profile-notif_guardian_profile-",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeInboxProfileAction_UntypedDuplicateActorIdFailsClosed()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        const string notificationsPath = "game_state/control/afterlife_notifications.json";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var profilesRoot = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        profilesRoot["profiles"]!.AsArray().Insert(1, new JsonObject
        {
            ["actorType"] = "resident",
            ["actorId"] = "guardian_mirror",
            ["displayName"] = "Резидент Зеркал",
            ["realm"] = "Chaos Sea"
        });
        await _fs.WriteFileAtomicAsync(profilesPath, profilesRoot.ToJsonString(JsonOptions));
        var notificationsRoot = JsonNode.Parse((await _fs.ReadFileAsync(notificationsPath))!)!.AsObject();
        var notification = notificationsRoot["notifications"]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(candidate =>
                candidate["notificationId"]?.GetValue<string>() == "notif_guardian_profile");
        notification.Remove("profileActorId");
        notification.Remove("profileActorType");
        await _fs.WriteFileAtomicAsync(notificationsPath, notificationsRoot.ToJsonString(JsonOptions));

        var inbox = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_inbox"));

        Assert.DoesNotContain(inbox.Actions, static candidate =>
            candidate.Id.StartsWith(
                "afterlife-inbox-profile-notif_guardian_profile-",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfileDirectSelector_WithOpaquePrefixRemainsReadable()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        const string actorId = "afterlife_profile_named_guardian";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var root = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        root["profiles"]![0]!["actorId"] = actorId;
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));

        var detail = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest($"/afterlife_profiles профиль {actorId}"));

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        Assert.Contains(
            "Профиль посмертия: Хранитель Зеркал",
            CollectBlockText(detail.Blocks),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfileDirectSelector_WithExactOpaqueShapeRemainsReadable()
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        const string actorId = "afterlife_profile_0123456789abcdef01234567";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var root = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        root["profiles"]![0]!["actorId"] = actorId;
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));

        var direct = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest($"/afterlife_profiles профиль {actorId}"));
        var generated = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest($"/afterlife_profiles действие {actorId}"));

        Assert.Contains(
            "Профиль посмертия: Хранитель Зеркал",
            CollectBlockText(direct.Blocks),
            StringComparison.Ordinal);
        Assert.Contains(
            "Профиль недоступен",
            CollectBlockText(generated.Blocks),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("afterlife_profile_0123456789ABCDEF01234567")]
    [InlineData("afterlife_profile_0123456789abcdef0123456")]
    public async Task ExecuteAsync_AfterlifeProfileActionSelector_WithMalformedShapeDoesNotUseDirectLookup(
        string actorId)
    {
        const string profilesPath = "game_state/meta/afterlife_entity_profiles.json";
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();
        var root = JsonNode.Parse((await _fs.ReadFileAsync(profilesPath))!)!.AsObject();
        root["profiles"]![0]!["actorId"] = actorId;
        await _fs.WriteFileAtomicAsync(profilesPath, root.ToJsonString(JsonOptions));

        var direct = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest($"/afterlife_profiles профиль {actorId}"));
        var generated = await _service.ExecuteAsync(
            new ExplorerWebCommandRequest($"/afterlife_profiles действие {actorId}"));

        Assert.Contains(
            "Профиль посмертия: Хранитель Зеркал",
            CollectBlockText(direct.Blocks),
            StringComparison.Ordinal);
        Assert.Contains(
            "Профиль недоступен",
            CollectBlockText(generated.Blocks),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task T155_ExecuteAsync_AfterlifeProfiles_HidesPrivateAndSystemProfiles()
    {
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var payload = SerializePlayerFacingResult(result);
        Assert.Contains("Хранитель Зеркал", CollectBlockText(result.Blocks), StringComparison.Ordinal);
        Assert.DoesNotContain("hidden_profile_marker", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("gm_only_profile_marker", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("secret_profile_marker", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("system_profile_marker", payload, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/afterlife_profiles профиль guardian_mirror", "Профиль посмертия: Хранитель Зеркал", "Собирает отражения", "hidden_profile_marker")]
    [InlineData("/afterlife_threats угроза threat_moth", "Угроза посмертия: Моль Сомнений", "плетёт сомнения", "hidden_threat_marker")]
    [InlineData("/afterlife_chronicles хроника chronicle_mirror", "Хроника посмертия: Зал зеркальной клятвы", "Игрок впервые вошёл", "hidden_chronicle_marker")]
    public async Task ExecuteAsync_AfterlifeIssue1066Details_RenderFocusedPlayerFacingDetailWithoutRawJson(
        string command,
        string expectedTitle,
        string expectedText,
        string excludedText)
    {
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1066TechnicalLeak(result);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(excludedText, text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/afterlife_profiles профиль missing_profile")]
    [InlineData("/afterlife_threats угроза missing_threat")]
    [InlineData("/afterlife_chronicles хроника missing_chronicle")]
    [InlineData("/afterlife_inbox уведомление missing_notice")]
    public async Task ExecuteAsync_AfterlifeIssue1066Details_UnknownIdsReturnPlayerFacingUnavailableText(string command)
    {
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1066TechnicalLeak(result);
        Assert.Contains("не удалось открыть", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeInboxOverview_ExposesReadOnlyFollowThroughActionsWithoutRawOrMutation()
    {
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_inbox"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains(result.Prompts, static prompt => prompt.Id == "notification_action");
        Assert.Contains(result.Prompts, static prompt => prompt.Id == "notification_id");
        AssertNoIssue1066TechnicalLeak(result);
        AssertIssue1066Action(
            result,
            "afterlife-inbox-detail-notif_guardian_profile",
            "/afterlife_inbox уведомление notif_guardian_profile",
            "Хранитель Зеркал");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-guardian-notif_guardian_profile-guardian_mirror",
            "/guardians хранитель guardian_mirror",
            "Хранитель Зеркал");
        var profileAction = Assert.Single(result.Actions, static candidate =>
            candidate.Id.StartsWith(
                "afterlife-inbox-profile-notif_guardian_profile-",
                StringComparison.Ordinal));
        Assert.Matches(
            "^afterlife-inbox-profile-notif_guardian_profile-afterlife_profile_[0-9a-f]{24}$",
            profileAction.Id);
        Assert.Matches(
            "^/afterlife_profiles действие afterlife_profile_[0-9a-f]{24}$",
            profileAction.Command);
        Assert.Contains("Хранитель Зеркал", profileAction.Label, StringComparison.Ordinal);
        Assert.DoesNotContain("guardian_mirror", profileAction.Id, StringComparison.Ordinal);
        Assert.DoesNotContain("guardian_mirror", profileAction.Command, StringComparison.Ordinal);
        AssertIssue1066Action(
            result,
            "afterlife-inbox-threat-notif_guardian_profile-threat_moth",
            "/afterlife_threats угроза threat_moth",
            "Моль Сомнений");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-chronicle-notif_guardian_profile-chronicle_mirror",
            "/хроники_посмертия хроника chronicle_mirror",
            "Зал зеркальной клятвы");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-archive-notif_archive_project-archive_mirror",
            "/afterlife_archive запись archive_mirror",
            "Песнь Зеркала");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-project-notif_archive_project-guardian_mirror-project_mirror",
            "/guardian_projects проект guardian_mirror::project_mirror",
            "Проект Зеркала");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-shining-politics-notif_shining_foundation",
            "/shining_politics",
            "Сияющую Обитель");

        var profileDetail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(profileAction.Command));
        Assert.Equal(CommandExecutionState.Completed, profileDetail.State);
        Assert.Contains(
            "Профиль посмертия: Хранитель Зеркал",
            CollectBlockText(profileDetail.Blocks),
            StringComparison.Ordinal);

        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_inbox уведомление notif_guardian_profile"));

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        AssertNoIssue1066TechnicalLeak(detail);
        Assert.Contains("Уведомление загробья", CollectBlockText(detail.Blocks), StringComparison.OrdinalIgnoreCase);
        var storedJson = await _fs.ReadFileAsync("game_state/control/afterlife_notifications.json");
        Assert.NotNull(storedJson);
        var stored = JsonNode.Parse(storedJson)!;
        Assert.All(stored["notifications"]!.AsArray().OfType<JsonObject>(), notification =>
            Assert.Equal("unread", notification["status"]?.GetValue<string>()));
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfilesMalformedState_DefaultModeReturnsPlayerFacingUnavailableText()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", "{ broken profile state");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1066TechnicalLeak(result);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Профили посмертия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("не удалось прочитать", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JSON повреждён", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Path:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LineNumber", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BytePositionInLine", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplorerCommandCatalog_AfterlifeIssue1066Commands_AcceptDetailArguments()
    {
        foreach (var commandId in new[] { "afterlife_profiles", "afterlife_threats", "afterlife_chronicles", "afterlife_inbox" })
        {
            var descriptor = ExplorerCommandCatalog.Require(commandId);

            Assert.Equal(ExplorerCommandBrowserHandlerKind.AfterlifeCombat, descriptor.BrowserHandlerKind);
            Assert.True(
                descriptor.AcceptsArguments,
                $"{commandId} must preserve read-only selected-detail arguments for #1066 / #949 AFD-005 browser drill-down actions.");
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for temporary test files.
        }
    }

    private async Task SeedRichAfterlifeProfileInboxDrilldownFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 12,
          "afterlifeCombatProfile": {
            "artTiers": { "pressure": 2, "guard": 1 }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", """
        {
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 4, "lightSparks": 1 },
              "progression": {
                "enlightenment": { "tier": 2, "experience": 8 },
                "radiance": { "tier": 1, "experience": 3 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [
                {
                  "artId": "mirror_oath",
                  "displayName": "Клятва Зеркала",
                  "tier": 1,
                  "effectSummary": "видит отражённые обещания"
                }
              ],
              "fateCards": [
                { "cardId": "mirror_card", "nameRu": "Песнь зеркальной двери", "status": "unlocked" }
              ],
              "goals": { "shortTermGoal": "защитить зеркальный зал" },
              "currentActivity": { "summary": "Собирает отражения у тихой воды" },
              "personalQuests": [
                { "questId": "quest_mirror", "title": "Собрать осколки клятвы", "status": "active" }
              ]
            },
            {
              "actorType": "guardian",
              "actorId": "hidden_profile",
              "displayName": "hidden_profile_marker",
              "isPlayerVisible": false,
              "currentActivity": { "summary": "не показывать игроку" }
            },
            {
              "actorType": "guardian",
              "actorId": "gm_only_profile",
              "displayName": "gm_only_profile_marker",
              "gmOnly": true
            },
            {
              "actorType": "guardian",
              "actorId": "secret_profile",
              "displayName": "secret_profile_marker",
              "visibility": "secret"
            },
            {
              "actorType": "system_actor",
              "actorId": "system_profile",
              "displayName": "system_profile_marker"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_active_threats.json", """
        {
          "threats": [
            {
              "threatId": "threat_moth",
              "displayName": "Моль Сомнений",
              "visibleToPlayer": true,
              "realm": "Chaos Sea",
              "intensity": 6,
              "threatArchetype": {
                "motivation": "subversion",
                "method": "deceptive",
                "summary": "подтачивает клятвы"
              },
              "currentActivity": {
                "summary": "плетёт сомнения вокруг зеркального зала",
                "activeState": "active",
                "startedAtTurn": 44
              },
              "impactProfile": {
                "primaryTargetType": "guardian",
                "primaryTargetName": "Хранитель Зеркал",
                "primaryImpact": "relationship",
                "baseImpactValue": 3
              },
              "linkedGuardianId": "guardian_mirror"
            },
            {
              "threatId": "hidden_threat",
              "displayName": "hidden_threat_marker",
              "visibleToPlayer": false
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_chronicles.json", """
        {
          "chronicles": [
            {
              "chronicleId": "chronicle_mirror",
              "displayName": "Зал зеркальной клятвы",
              "isPlayerVisible": true,
              "scopeType": "guardian",
              "scopeId": "Хранитель Зеркал",
              "lastUpdatedTurn": 45,
              "lastEventsDescription": "Игрок впервые вошёл в зал зеркальной клятвы",
              "eventDescriptions": [
                "[Turn 44] Игрок впервые вошёл к зеркальной воде",
                "[Turn 45] Хранитель Зеркал признал клятву"
              ],
              "participants": [
                { "displayName": "Хранитель Зеркал", "actorType": "guardian" },
                "Душа игрока"
              ],
              "persistentConsequences": [ "клятва стала видимой" ],
              "openThreads": [ "найти второй осколок" ]
            },
            {
              "chronicleId": "hidden_chronicle_marker",
              "displayName": "hidden_chronicle_marker",
              "isPlayerVisible": false,
              "lastEventsDescription": "не раскрывать"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_mirror",
              "canonicalName": "Хранитель Зеркал",
              "domain": "Зеркальный зал",
              "abode": { "abodeId": "abode_mirror", "name": "Приют отражений", "abodePower": 41 },
              "projects": [
                { "projectId": "project_mirror", "name": "Проект Зеркала", "state": "active", "progressPercent": 42 }
              ]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_archive.json", """
        {
          "entries": [
            {
              "archiveId": "archive_mirror",
              "title": "Песнь Зеркала",
              "summary": "Память о первом отражении",
              "fullText": "Полный текст песни зеркала",
              "isPlayerVisible": true
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/afterlife_notifications.json", """
        {
          "notifications": [
            {
              "notificationId": "notif_guardian_profile",
              "notificationType": "guardian_quest_available",
              "requestId": "hidden_request_guardian",
              "status": "unread",
              "guardianId": "guardian_mirror",
              "guardianName": "Хранитель Зеркал",
              "summary": "Хранитель Зеркал ждёт разговора у тихой воды",
              "createdAtTurn": 46,
              "profileActorId": "guardian_mirror",
              "profileActorType": "guardian",
              "threatId": "threat_moth",
              "threatName": "Моль Сомнений",
              "chronicleId": "chronicle_mirror",
              "chronicleTitle": "Зал зеркальной клятвы"
            },
            {
              "notificationId": "notif_archive_project",
              "notificationType": "archive_project_fuel_accepted",
              "requestId": "hidden_request_archive",
              "status": "unread",
              "guardianId": "guardian_mirror",
              "guardianName": "Хранитель Зеркал",
              "archiveId": "archive_mirror",
              "archiveTitle": "Песнь Зеркала",
              "targetProjectId": "project_mirror",
              "targetProjectName": "Проект Зеркала",
              "summary": "Архивное знание подпитало Проект Зеркала",
              "createdAtTurn": 47
            },
            {
              "notificationId": "notif_shining_foundation",
              "notificationType": "shining_faction_founding_resolved",
              "requestId": "hidden_request_shining",
              "status": "unread",
              "summary": "Сияющая Обитель приняла основание дома",
              "createdAtTurn": 48
            }
          ]
        }
        """);
    }

    private static void AssertIssue1066Action(
        ExplorerCommandResult result,
        string expectedActionId,
        string expectedCommand,
        string expectedLabelText)
    {
        var action = Assert.Single(result.Actions, candidate => candidate.Id == expectedActionId);
        Assert.Equal(expectedCommand, action.Command);
        Assert.Contains(expectedLabelText, action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, action.Style);
        Assert.False(action.RequiresConfirmation);
        Assert.DoesNotContain("/", action.Label, StringComparison.Ordinal);
        Assert.DoesNotContain("DTO", action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", action.Label, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoIssue1066TechnicalLeak(ExplorerCommandResult result)
    {
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var payload = SerializePlayerFacingResult(result);
        foreach (var forbidden in new[]
                 {
                     ".json", "game_state/", "DTO", "API", "endpoint", "debug", "exception",
                     "UiRawJsonBlock", "pending_", "requestId", "actionType", "hidden_",
                     "не раскрывать", "не показывать игроку", "gmThoughts"
                 })
        {
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string CollectBlockText(IEnumerable<UiBlock> blocks)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
            CollectBlockText(block, parts);

        return string.Join("\n", parts);
    }

    private static void CollectBlockText(UiBlock block, List<string> parts)
    {
        switch (block)
        {
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts);
                break;
            case UiEntityDossierBlock dossier:
                parts.Add(dossier.Title);
                parts.Add(dossier.Subtitle);
                parts.Add(dossier.Summary);
                parts.AddRange(dossier.Badges.Select(static badge => badge.Label));
                CollectEntityFacts(dossier.Facts, parts);
                CollectEntityMetrics(dossier.Metrics, parts);
                CollectEntityHints(dossier.Hints, parts);
                parts.AddRange(dossier.List);
                foreach (var card in dossier.Cards)
                    CollectEntityCardText(card, parts);
                foreach (var section in dossier.Sections)
                {
                    parts.Add(section.Title);
                    parts.Add(section.Summary);
                    parts.Add(section.CollectionLabel);
                    CollectEntityFacts(section.Facts, parts);
                    CollectEntityMetrics(section.Metrics, parts);
                    CollectEntityHints(section.Hints, parts);
                    parts.AddRange(section.List);
                    foreach (var card in section.Cards)
                        CollectEntityCardText(card, parts);
                    foreach (var child in section.Blocks)
                        CollectBlockText(child, parts);
                }
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiTextBlock text:
                parts.Add(text.Text);
                break;
            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                {
                    parts.Add(item.Key);
                    parts.Add(item.Value);
                }
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                foreach (var column in table.Columns)
                    parts.Add(column);
                foreach (var row in table.Rows)
                foreach (var cell in row.Cells)
                    parts.Add(cell);
                break;
        }
    }

    private static void CollectEntityCardText(UiEntityCard card, List<string> parts)
    {
        parts.Add(card.Title);
        parts.Add(card.Subtitle);
        parts.Add(card.Summary);
        parts.AddRange(card.Badges.Select(static badge => badge.Label));
        CollectEntityFacts(card.Facts, parts);
        CollectEntityMetrics(card.Metrics, parts);
        CollectEntityHints(card.Hints, parts);
        parts.AddRange(card.List);
        foreach (var child in card.Nested)
            CollectEntityCardText(child, parts);
        foreach (var child in card.Cards)
            CollectEntityCardText(child, parts);
    }

    private static void CollectEntityFacts(IEnumerable<UiEntityFact> facts, List<string> parts)
    {
        foreach (var fact in facts)
        {
            parts.Add(fact.Label);
            parts.Add(fact.Value);
        }
    }

    private static void CollectEntityMetrics(IEnumerable<UiEntityMetric> metrics, List<string> parts)
    {
        foreach (var metric in metrics)
        {
            parts.Add(metric.Label);
            parts.Add(metric.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            parts.Add(metric.Max.ToString(System.Globalization.CultureInfo.InvariantCulture));
            parts.Add(metric.Note);
        }
    }

    private static void CollectEntityHints(IEnumerable<UiEntityHint> hints, List<string> parts)
    {
        foreach (var hint in hints)
        {
            parts.Add(hint.Title);
            parts.Add(hint.Text);
        }
    }

    private static string SerializePlayerFacingResult(ExplorerCommandResult result) =>
        JsonSerializer.Serialize(
            new
            {
                result.Blocks,
                result.Actions,
                result.Prompts,
                result.Notifications
            },
            JsonOptions);
}
