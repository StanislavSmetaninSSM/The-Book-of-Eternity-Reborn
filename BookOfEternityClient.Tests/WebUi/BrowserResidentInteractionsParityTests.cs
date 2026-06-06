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

namespace BookOfEternityClient.Tests.WebUi;

public sealed class BrowserResidentInteractionsParityTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _commandService;
    private readonly ExplorerWebPromptSessionService _promptSessions;
    private readonly BrowserAfterlifeWriteService _afterlifeWriteService;

    public BrowserResidentInteractionsParityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-residents-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var lockService = new LocalUiSessionLockService(_fs);
        var coordinator = new BrowserLocalWriteCoordinator(_fs, lockService, TimeProvider.System);
        var mortalWriteService = new BrowserMortalWorldWriteService(
            _fs,
            coordinator,
            new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance),
            TimeProvider.System);
        _afterlifeWriteService = new BrowserAfterlifeWriteService(_fs, _stateManager, coordinator);
        _promptSessions = new ExplorerWebPromptSessionService(
            _fs,
            _stateManager,
            lockService: lockService,
            mortalWorldWriteService: mortalWriteService,
            afterlifeWriteService: _afterlifeWriteService);
        _commandService = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), validation, _promptSessions);
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task ExecuteAsync_AbodeResidents_ReturnsPromptWithGuardianAbodeSelection()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: false);

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/abode_residents guardian_alpha",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);

        var abodePrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "guardian_abode_id"));
        Assert.Contains(abodePrompt.Options, option => option.Value == "guardian_alpha::abode_alpha" && option.Label.Contains("Азалия", StringComparison.Ordinal));
        Assert.Contains(abodePrompt.Options, option => option.Value == "guardian_mirror::abode_mirror" && option.Label.Contains("Зеркальный Страж", StringComparison.Ordinal));

        var text = CollectResultAndPromptText(result);
        Assert.Contains("Обитатели Обители", text, StringComparison.Ordinal);
        Assert.Contains("состав", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawResidentDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task SubmitAsync_AbodeResidents_WritesPendingRosterRequestWithTurn()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: false);

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/abode_residents guardian_alpha",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));
        Assert.NotNull(prompt.InteractiveSession);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(("guardian_abode_id", "guardian_alpha::abode_alpha")),
            OwnerId: "browser-resident-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        AssertNoRawResidentDiagnosticText(CollectResultAndPromptText(result));

        var request = AssertSingleRequest(GuardianAbodeResidentRequestState.PendingResidentsRequestPath);
        Assert.StartsWith("abode_residents_", request["requestId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("guardian_alpha", request["guardianId"]!.GetValue<string>());
        Assert.Equal("Азалия", request["guardianName"]!.GetValue<string>());
        Assert.Equal("abode_alpha", request["abodeId"]!.GetValue<string>());
        Assert.Equal("Тестовая обитель", request["abodeName"]!.GetValue<string>());
        Assert.Equal(120, request["currentReputation"]!.GetValue<int>());
        Assert.Equal(GuardianAbodeResidentRequestState.ResidentsRequestModeStandardRoster, request["requestMode"]!.GetValue<string>());
        Assert.Equal(77, request["createdAtTurn"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(request["createdAtUtc"]!.GetValue<string>()));
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task TryApplyAsync_AbodeResidentsDuplicatePending_ReturnsPlayerFacingPendingWithoutOverwrite()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await GuardianAbodeResidentRequestState.WriteResidentsRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest
        {
            RequestId = "abode_residents_existing",
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            AbodeName = "Тестовая обитель",
            CurrentReputation = 110,
            RequestMode = GuardianAbodeResidentRequestState.ResidentsRequestModeStandardRoster,
            CreatedAtTurn = 76,
            CreatedAtUtc = "2026-06-06T01:00:00Z"
        });
        var before = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath);

        var result = await _afterlifeWriteService.TryApplyAsync(
            "/abode_residents guardian_alpha",
            Answers(("guardian_abode_id", "guardian_alpha::abode_alpha")),
            Owner("browser-resident-test"));

        Assert.True(result.Handled);
        Assert.False(result.Success);
        Assert.False(result.KeepSessionOpen);
        Assert.Equal(CommandExecutionState.Pending, result.State);
        Assert.Contains("уже ожидает", result.Message, StringComparison.OrdinalIgnoreCase);
        AssertNoRawResidentDiagnosticText(result.Title + "\n" + result.Message);
        Assert.Equal(before, await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task SubmitAsync_AbodeResidentsMalformedPendingFailure_ReturnsPlayerFacingCopyWithoutDiagnostics()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: false);
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/abode_residents guardian_alpha",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));
        Assert.NotNull(prompt.InteractiveSession);
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, "{ malformed");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(("guardian_abode_id", "guardian_alpha::abode_alpha")),
            OwnerId: "browser-resident-test"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        AssertNoRawResidentDiagnosticText(CollectResultAndPromptText(result));
        Assert.Equal("{ malformed", await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task ExecuteAsync_ResidentInteraction_ReturnsPromptWithCanonicalResidentSelectionAndTalkHistoryChoices()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateWithNestedNonResidentReferencesAsync(includeTransferReady: false);

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/resident_interaction",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);

        var residentPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "resident_id"));
        Assert.Contains(residentPrompt.Options, option => option.Value == "resident_liora" && option.Label.Contains("Лиора", StringComparison.Ordinal));
        Assert.DoesNotContain(residentPrompt.Options, option => option.Value == "resident_nested_receipt");

        var interactionPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "resident_interaction_type"));
        Assert.Contains(interactionPrompt.Options, option => option.Value == GuardianAbodeResidentState.InteractionTypeTalk && option.Label.Contains("Поговорить", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(interactionPrompt.Options, option => option.Value == GuardianAbodeResidentState.InteractionTypeHistory && option.Label.Contains("истор", StringComparison.OrdinalIgnoreCase));

        AssertNoRawResidentDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task SubmitAsync_ResidentInteraction_GenericPromptAllowsLaterResidentHistoryChoice()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateWithFirstResidentNarrowAndLaterReadyAsync();

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/resident_interaction",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));
        Assert.NotNull(prompt.InteractiveSession);

        var residentPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(prompt.Prompts, item => item.Id == "resident_id"));
        Assert.Contains(residentPrompt.Options, option => option.Value == "resident_alina");
        Assert.Contains(residentPrompt.Options, option => option.Value == "resident_liora");
        var interactionPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(prompt.Prompts, item => item.Id == "resident_interaction_type"));
        Assert.Contains(interactionPrompt.Options, option => option.Value == GuardianAbodeResidentState.InteractionTypeHistory && !option.Disabled);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_liora"),
                ("resident_interaction_type", GuardianAbodeResidentState.InteractionTypeHistory)),
            OwnerId: "browser-resident-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleRequest(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath);
        Assert.Equal("resident_liora", request["residentId"]!.GetValue<string>());
        Assert.Equal(GuardianAbodeResidentState.InteractionTypeHistory, request["interactionType"]!.GetValue<string>());
    }

    [Theory]
    [Trait("Category", "BrowserResidentParity")]
    [InlineData(GuardianAbodeResidentState.InteractionTypeTalk)]
    [InlineData(GuardianAbodeResidentState.InteractionTypeHistory)]
    public async Task SubmitAsync_ResidentInteraction_WritesPendingInteractionRequestWithTurn(string interactionType)
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: false);

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/resident_interaction resident_liora",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));
        Assert.NotNull(prompt.InteractiveSession);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_liora"),
                ("resident_interaction_type", interactionType)),
            OwnerId: "browser-resident-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoRawResidentDiagnosticText(CollectResultAndPromptText(result));

        var request = AssertSingleRequest(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath);
        Assert.StartsWith("abode_resident_interaction_", request["requestId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("guardian_alpha", request["guardianId"]!.GetValue<string>());
        Assert.Equal("Азалия", request["guardianName"]!.GetValue<string>());
        Assert.Equal("abode_alpha", request["abodeId"]!.GetValue<string>());
        Assert.Equal("Тестовая обитель", request["abodeName"]!.GetValue<string>());
        Assert.Equal("resident_liora", request["residentId"]!.GetValue<string>());
        Assert.Equal("Лиора", request["residentName"]!.GetValue<string>());
        Assert.Equal(interactionType, request["interactionType"]!.GetValue<string>());
        Assert.Equal(77, request["createdAtTurn"]!.GetValue<int>());
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task TryApplyAsync_ResidentInteractionDuplicatePending_ReturnsPlayerFacingPendingWithoutOverwrite()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: false);
        await GuardianAbodeResidentRequestState.WriteInteractionRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest
        {
            RequestId = "abode_resident_interaction_existing",
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            AbodeName = "Тестовая обитель",
            ResidentId = "resident_liora",
            ResidentName = "Лиора",
            InteractionType = GuardianAbodeResidentState.InteractionTypeTalk,
            CreatedAtTurn = 76,
            CreatedAtUtc = "2026-06-06T01:00:00Z"
        });
        var before = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath);

        var result = await _afterlifeWriteService.TryApplyAsync(
            "/resident_interaction resident_liora",
            Answers(
                ("resident_id", "resident_liora"),
                ("resident_interaction_type", GuardianAbodeResidentState.InteractionTypeTalk)),
            Owner("browser-resident-test"));

        Assert.True(result.Handled);
        Assert.False(result.Success);
        Assert.Equal(CommandExecutionState.Pending, result.State);
        Assert.Contains("уже ожидает", result.Message, StringComparison.OrdinalIgnoreCase);
        AssertNoRawResidentDiagnosticText(result.Title + "\n" + result.Message);
        Assert.Equal(before, await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task SubmitAsync_ResidentInteractionMalformedPendingFailure_ReturnsPlayerFacingCopyWithoutDiagnostics()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: false);
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/resident_interaction resident_liora",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));
        Assert.NotNull(prompt.InteractiveSession);
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, "{ malformed");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_liora"),
                ("resident_interaction_type", GuardianAbodeResidentState.InteractionTypeTalk)),
            OwnerId: "browser-resident-test"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        AssertNoRawResidentDiagnosticText(CollectResultAndPromptText(result));
        Assert.Equal("{ malformed", await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task SubmitAsync_ResidentTransferTarget_WritesPendingTransferRequestWithCompetitionMetadata()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: true);

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/resident_transfer resident_liora",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));
        Assert.NotNull(prompt.InteractiveSession);
        var choicePrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(prompt.Prompts, item => item.Id == "resident_transfer_choice"));
        Assert.Contains(choicePrompt.Options, option => option.Value == "target:guardian_mirror::abode_mirror");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_liora"),
                ("resident_transfer_choice", "target:guardian_mirror::abode_mirror")),
            OwnerId: "browser-resident-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoRawResidentDiagnosticText(CollectResultAndPromptText(result));

        var request = AssertSingleRequest(GuardianAbodeResidentRequestState.PendingTransfersRequestPath);
        Assert.StartsWith("abode_resident_transfer_", request["requestId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("resident_liora", request["residentId"]!.GetValue<string>());
        Assert.Equal("guardian_alpha", request["sourceGuardianId"]!.GetValue<string>());
        Assert.Equal("abode_alpha", request["sourceAbodeId"]!.GetValue<string>());
        Assert.Equal("guardian_mirror", request["targetGuardianId"]!.GetValue<string>());
        Assert.Equal("abode_mirror", request["targetAbodeId"]!.GetValue<string>());
        Assert.Equal(GuardianAbodeResidentState.TransferModeAcceptedTransfer, request["transferMode"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(request["selectionMode"]!.GetValue<string>()));
        Assert.True(request["competitionScore"]!.GetValue<int>() >= 0);
        Assert.False(string.IsNullOrWhiteSpace(request["competitionLabel"]!.GetValue<string>()));
        Assert.Equal(77, request["createdAtTurn"]!.GetValue<int>());
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task SubmitAsync_ResidentTransfer_GenericPromptAllowsLaterReadyResidentTransferChoice()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateWithFirstResidentNarrowAndLaterReadyAsync();

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/resident_transfer",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));
        Assert.NotNull(prompt.InteractiveSession);

        var residentPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(prompt.Prompts, item => item.Id == "resident_id"));
        Assert.Contains(residentPrompt.Options, option => option.Value == "resident_alina");
        Assert.Contains(residentPrompt.Options, option => option.Value == "resident_liora");
        var choicePrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(prompt.Prompts, item => item.Id == "resident_transfer_choice"));
        Assert.Contains(choicePrompt.Options, option => option.Value == "target:guardian_mirror::abode_mirror" && !option.Disabled);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_liora"),
                ("resident_transfer_choice", "target:guardian_mirror::abode_mirror")),
            OwnerId: "browser-resident-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleRequest(GuardianAbodeResidentRequestState.PendingTransfersRequestPath);
        Assert.Equal("resident_liora", request["residentId"]!.GetValue<string>());
        Assert.Equal("guardian_mirror", request["targetGuardianId"]!.GetValue<string>());
        Assert.Equal("abode_mirror", request["targetAbodeId"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task SubmitAsync_ResidentTransferDepartureOnly_WritesDepartureOnlyTransferRequest()
    {
        await SeedStoryTurnAsync(77);
        await SeedSingleGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: true, includeMirrorResident: false);

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/resident_transfer resident_liora",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));
        Assert.NotNull(prompt.InteractiveSession);
        var choicePrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(prompt.Prompts, item => item.Id == "resident_transfer_choice"));
        Assert.Contains(choicePrompt.Options, option => option.Value == "departure_only");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_liora"),
                ("resident_transfer_choice", "departure_only")),
            OwnerId: "browser-resident-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleRequest(GuardianAbodeResidentRequestState.PendingTransfersRequestPath);
        Assert.Equal(GuardianAbodeResidentState.TransferModeDepartureOnly, request["transferMode"]!.GetValue<string>());
        Assert.Equal(GuardianAbodeResidentRequestState.TransferSelectionModeDepartureOnly, request["selectionMode"]!.GetValue<string>());
        Assert.Equal("", request["targetGuardianId"]!.GetValue<string>());
        Assert.Equal("", request["targetAbodeId"]!.GetValue<string>());
        AssertNoRawResidentDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task TryApplyAsync_ResidentTransferNotReady_ReturnsPlayerFacingBlockerWithoutPendingWrite()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: false);

        var result = await _afterlifeWriteService.TryApplyAsync(
            "/resident_transfer resident_liora",
            Answers(
                ("resident_id", "resident_liora"),
                ("resident_transfer_choice", "departure_only")),
            Owner("browser-resident-test"));

        Assert.True(result.Handled);
        Assert.False(result.Success);
        Assert.Contains("не готов", result.Message, StringComparison.OrdinalIgnoreCase);
        AssertNoRawResidentDiagnosticText(result.Title + "\n" + result.Message);
        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingTransfersRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task TryApplyAsync_ResidentTransferDuplicatePending_ReturnsPlayerFacingPendingWithoutOverwrite()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: true);
        await GuardianAbodeResidentRequestState.WriteTransferRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest
        {
            RequestId = "abode_resident_transfer_existing",
            ResidentId = "resident_liora",
            ResidentName = "Лиора",
            SourceGuardianId = "guardian_alpha",
            SourceGuardianName = "Азалия",
            SourceAbodeId = "abode_alpha",
            SourceAbodeName = "Тестовая обитель",
            TargetGuardianId = "",
            TargetGuardianName = "",
            TargetAbodeId = "",
            TargetAbodeName = "",
            AbodeDevotionLevel = 12,
            AbodeDevotionTier = GuardianAbodeResidentState.AbodeDevotionTierAlienated,
            Restlessness = 95,
            MigrationState = GuardianAbodeResidentState.MigrationStateReadyToTransfer,
            TransferMode = GuardianAbodeResidentState.TransferModeDepartureOnly,
            SelectionMode = GuardianAbodeResidentRequestState.TransferSelectionModeDepartureOnly,
            CreatedAtTurn = 76,
            CreatedAtUtc = "2026-06-06T01:00:00Z"
        });
        var before = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath);

        var result = await _afterlifeWriteService.TryApplyAsync(
            "/resident_transfer resident_liora",
            Answers(
                ("resident_id", "resident_liora"),
                ("resident_transfer_choice", "departure_only")),
            Owner("browser-resident-test"));

        Assert.True(result.Handled);
        Assert.False(result.Success);
        Assert.Equal(CommandExecutionState.Pending, result.State);
        Assert.Contains("уже ожидает", result.Message, StringComparison.OrdinalIgnoreCase);
        AssertNoRawResidentDiagnosticText(result.Title + "\n" + result.Message);
        Assert.Equal(before, await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task SubmitAsync_ResidentTransferMalformedPendingFailure_ReturnsPlayerFacingCopyWithoutDiagnostics()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: true);
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/resident_transfer resident_liora",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));
        Assert.NotNull(prompt.InteractiveSession);
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, "{ malformed");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_liora"),
                ("resident_transfer_choice", "departure_only")),
            OwnerId: "browser-resident-test"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        AssertNoRawResidentDiagnosticText(CollectResultAndPromptText(result));
        Assert.Equal("{ malformed", await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath));
    }

    [Theory]
    [Trait("Category", "BrowserResidentParity")]
    [InlineData("/abode_residents")]
    [InlineData("/resident_interaction")]
    [InlineData("/resident_transfer")]
    public async Task ExecuteAsync_ResidentCommandInMortalWorld_ReturnsRealmBlockerWithoutPrompt(string command)
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Mortal World");
        await SeedResidentStateAsync(includeTransferReady: true);

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            command,
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));

        Assert.NotEqual(CommandExecutionState.RequiresInput, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("посмертии", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawResidentDiagnosticText(text);
        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingResidentsRequestPath));
        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath));
        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingTransfersRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserResidentParity")]
    public async Task SubmitAsync_ResidentPromptAfterRealmSwitchToMortalWorld_ReturnsRealmBlockerWithoutPendingWrite()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");
        await SeedResidentStateAsync(includeTransferReady: false);
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/resident_interaction resident_liora",
            OwnerId: "browser-resident-test",
            OwnerLabel: "Browser resident test"));
        Assert.NotNull(prompt.InteractiveSession);
        await SeedSoulRealmAsync("Mortal World");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_liora"),
                ("resident_interaction_type", GuardianAbodeResidentState.InteractionTypeHistory)),
            OwnerId: "browser-resident-test"));

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("посмертии", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawResidentDiagnosticText(text);
        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public void BrowserCommandCoverage_Issue809ResidentCommandsAreCovered()
    {
        var coverage = BrowserCommandCoverageService.Build();

        foreach (var commandId in new[] { "abode_residents", "resident_interaction", "resident_transfer" })
        {
            var command = Assert.Single(coverage.Commands, item => item.Id == commandId);
            Assert.Equal("covered", command.AuditStatus);
            Assert.Equal(nameof(ExplorerCommandMigrationStatus.MutatingParity), command.BrowserStatus);
            Assert.Equal("guided-form", command.FormMode);
            Assert.Equal("player-default", command.Surface);
            Assert.DoesNotContain("#809", command.FollowUpIssue, StringComparison.Ordinal);
            AssertNoRawResidentDiagnosticText(command.PrimaryActionLabel + "\n" + command.Reason + "\n" + command.GapSummary);
        }

        var interactions = Assert.Single(coverage.Commands, item => item.Id == "interactions");
        Assert.DoesNotContain("#809", interactions.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("resident interaction starts remain tracked", interactions.GapSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public async Task Help_ResidentCommandsAreListedInAfterlifeHelp()
    {
        await SeedStoryTurnAsync(77);
        await SeedGuardianStateAsync("Shining Abode");

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest("/help"));

        var text = CollectResultAndPromptText(result);
        Assert.Contains("/abode_residents", text, StringComparison.Ordinal);
        Assert.Contains("/resident_interaction", text, StringComparison.Ordinal);
        Assert.Contains("/resident_transfer", text, StringComparison.Ordinal);
        Assert.Contains("обител", text, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedStoryTurnAsync(int turnNumber)
    {
        await _fs.WriteFileAtomicAsync("stories/web-resident-test.json", $$"""
        {
          "turnNumber": {{turnNumber}}
        }
        """);
    }

    private async Task SeedGuardianStateAsync(string realm)
    {
        await SeedSoulRealmAsync(realm);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "relationshipData": { "currentReputation": 120 },
              "abodePower": 72,
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель", "abodePower": 72 }
            },
            {
              "id": "guardian_mirror",
              "guardianName": "Зеркальный Страж",
              "domain": "Отражения",
              "relationshipData": { "currentReputation": 10 },
              "abodePower": 90,
              "abode": { "abodeId": "abode_mirror", "name": "Зеркальная обитель", "abodePower": 90 }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель", "abodePower": 72 }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);
    }

    private async Task SeedSingleGuardianStateAsync(string realm)
    {
        await SeedSoulRealmAsync(realm);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "relationshipData": { "currentReputation": 120 },
              "abodePower": 72,
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель", "abodePower": 72 }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель", "abodePower": 72 }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);
    }

    private async Task SeedResidentStateAsync(bool includeTransferReady, bool includeMirrorResident = true)
    {
        var migrationState = includeTransferReady
            ? GuardianAbodeResidentState.MigrationStateReadyToTransfer
            : GuardianAbodeResidentState.MigrationStateSettled;
        var restlessness = includeTransferReady ? 95 : 12;

        var entries = new JsonArray
        {
            CreateResident("resident_liora", "Лиора", "guardian_alpha", "abode_alpha", migrationState, restlessness)
        };
        if (includeMirrorResident)
            entries.Add(CreateResident("resident_mirror", "Эхо Зеркал", "guardian_mirror", "abode_mirror", GuardianAbodeResidentState.MigrationStateSettled, 8));

        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = entries
        };
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task SeedResidentStateWithFirstResidentNarrowAndLaterReadyAsync()
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray
            {
                CreateResident(
                    "resident_alina",
                    "Алина",
                    "guardian_alpha",
                    "abode_alpha",
                    GuardianAbodeResidentState.MigrationStateSettled,
                    12,
                    [GuardianAbodeResidentState.InteractionTypeTalk]),
                CreateResident(
                    "resident_liora",
                    "Лиора",
                    "guardian_alpha",
                    "abode_alpha",
                    GuardianAbodeResidentState.MigrationStateReadyToTransfer,
                    95,
                    [GuardianAbodeResidentState.InteractionTypeHistory]),
                CreateResident(
                    "resident_mirror",
                    "Эхо Зеркал",
                    "guardian_mirror",
                    "abode_mirror",
                    GuardianAbodeResidentState.MigrationStateSettled,
                    8)
            }
        };
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task SeedResidentStateWithNestedNonResidentReferencesAsync(bool includeTransferReady)
    {
        var migrationState = includeTransferReady
            ? GuardianAbodeResidentState.MigrationStateReadyToTransfer
            : GuardianAbodeResidentState.MigrationStateSettled;
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray
            {
                CreateResident("resident_liora", "Лиора", "guardian_alpha", "abode_alpha", migrationState, includeTransferReady ? 95 : 12)
            },
            ["interactionReceipts"] = new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "receipt_nested",
                    ["residentId"] = "resident_nested_receipt",
                    ["residentName"] = "Ложная квитанция",
                    ["interactionType"] = GuardianAbodeResidentState.InteractionTypeTalk,
                    ["status"] = "accepted"
                }
            }
        };
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static JsonObject CreateResident(
        string residentId,
        string displayName,
        string guardianId,
        string abodeId,
        string migrationState,
        int restlessness,
        IEnumerable<string>? availableInteractions = null)
    {
        var interactions = new JsonArray();
        foreach (var interaction in availableInteractions ?? [])
            interactions.Add(interaction);

        return new JsonObject
        {
            ["residentId"] = residentId,
            ["displayName"] = displayName,
            ["residentKind"] = "attendant_spirit",
            ["guardianId"] = guardianId,
            ["abodeId"] = abodeId,
            ["roleLabel"] = "Смотритель порога",
            ["summary"] = "Тестовый обитатель Обители.",
            ["bondLevel"] = 55,
            ["isPresent"] = true,
            ["availableInteractions"] = interactions,
            ["abodeDevotionLevel"] = migrationState == GuardianAbodeResidentState.MigrationStateReadyToTransfer ? 12 : 70,
            ["abodeDevotionTier"] = migrationState == GuardianAbodeResidentState.MigrationStateReadyToTransfer
                ? GuardianAbodeResidentState.AbodeDevotionTierAlienated
                : GuardianAbodeResidentState.AbodeDevotionTierDevoted,
            ["restlessness"] = restlessness,
            ["migrationState"] = migrationState,
            ["abodeDisposition"] = new JsonObject
            {
                ["powerSensitivity"] = GuardianAbodeResidentState.PowerSensitivityHigh,
                ["migrationDisposition"] = GuardianAbodeResidentState.MigrationDispositionOpportunistic,
                ["communalOrientation"] = GuardianAbodeResidentState.CommunalOrientationMedium,
                ["stabilityNeed"] = GuardianAbodeResidentState.StabilityNeedMedium
            }
        };
    }

    private async Task SeedSoulRealmAsync(string realm)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", $$"""
        {
          "soulName": "Тестовая Душа",
          "currentRealm": {{JsonSerializer.Serialize(realm)}},
          "currentIncarnation": 1
        }
        """);
    }

    private JsonObject AssertSingleRequest(string path)
    {
        var root = JsonNode.Parse((_fs.ReadFileAsync(path).GetAwaiter().GetResult())!)!.AsObject();
        return Assert.Single(root["requests"]!.AsArray())!.AsObject();
    }

    private static Dictionary<string, JsonNode?> Answers(params (string Key, object Value)[] pairs)
    {
        var answers = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            answers[key] = value switch
            {
                bool flag => JsonValue.Create(flag),
                int number => JsonValue.Create(number),
                string text => JsonValue.Create(text),
                _ => JsonSerializer.SerializeToNode(value)
            };
        }

        return answers;
    }

    private static LocalUiSessionLockOwner Owner(string id) =>
        new(id, "browser", "Browser resident test", TimeSpan.FromSeconds(120));

    private static string CollectResultAndPromptText(ExplorerCommandResult result) =>
        CollectBlockText(result.Blocks) + "\n" +
        string.Join("\n", result.Prompts.Select(CollectPromptText)) + "\n" +
        string.Join("\n", result.Notifications.Select(notification => $"{notification.Title}\n{notification.Message}"));

    private static string CollectPromptText(UiPrompt prompt)
    {
        var parts = new List<string> { prompt.Prompt };
        if (prompt is UiSelectionPrompt selection)
        {
            foreach (var option in selection.Options)
            {
                parts.Add(option.Label);
                parts.Add(option.Description);
            }
        }

        if (prompt is UiTextInputPrompt textInput)
            parts.Add(textInput.Placeholder);
        if (prompt is UiLongTextInputPrompt longTextInput)
            parts.Add(longTextInput.Placeholder);

        return string.Join("\n", parts);
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
            case UiTextBlock text:
                parts.Add(text.Text);
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts);
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                parts.AddRange(table.Columns);
                foreach (var row in table.Rows)
                    parts.AddRange(row.Cells);
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                {
                    parts.Add(item.Key);
                    parts.Add(item.Value);
                }
                break;
        }
    }

    private static void AssertNoRawResidentDiagnosticText(string text)
    {
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("snapshot", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", text, StringComparison.OrdinalIgnoreCase);
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
            // Ignore temp cleanup failures.
        }
    }
}
