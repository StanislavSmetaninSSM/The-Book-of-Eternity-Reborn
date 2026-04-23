using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ChaosSeaPendingRequestHygieneTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ChaosSeaPendingRequestHygieneTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-chaos-pending-hygiene-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task GuardianAbodeOffering_EnsureHealthyAsync_AfterlifePreservesMalformedPendingFile()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeOfferingState.PendingRequestPath, "{");

        await GuardianAbodeOfferingState.EnsureHealthyAsync(_fs, "Chaos Sea");

        Assert.True(_fs.FileExists(GuardianAbodeOfferingState.PendingRequestPath));
    }

    [Fact]
    public async Task GuardianTradeRequest_EnsureHealthyAsync_AfterlifePreservesMalformedPendingFile()
    {
        await _fs.WriteFileAtomicAsync(GuardianTradeRequestState.PendingRequestPath, "{");

        await GuardianTradeRequestState.EnsureHealthyAsync(_fs, "Chaos Sea");

        Assert.True(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));
    }

    [Fact]
    public async Task GuardianTradeRequest_EnsureHealthyAsync_UnresolvedRealmPreservesPendingFile()
    {
        await _fs.WriteFileAtomicAsync(GuardianTradeRequestState.PendingRequestPath, "{");

        await GuardianTradeRequestState.EnsureHealthyAsync(_fs, "");

        Assert.True(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));
    }

    [Fact]
    public async Task PlayerGuardianFoundation_EnsureHealthyAsync_ChaosSeaPreservesMalformedPendingFile()
    {
        await _fs.WriteFileAtomicAsync(PlayerGuardianFoundationState.PendingRequestPath, "{");

        await PlayerGuardianFoundationState.EnsureHealthyAsync(_fs, "Chaos Sea");

        Assert.True(_fs.FileExists(PlayerGuardianFoundationState.PendingRequestPath));
    }

    [Fact]
    public async Task GuardianAbodeOffering_EnsureHealthyAsync_UnresolvedRealmPreservesPendingFile()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeOfferingState.PendingRequestPath, "{");

        await GuardianAbodeOfferingState.EnsureHealthyAsync(_fs, "");

        Assert.True(_fs.FileExists(GuardianAbodeOfferingState.PendingRequestPath));
    }

    [Fact]
    public async Task GuardianAbodeOffering_WriteAsync_ExistingPendingRequest_ThrowsWithoutOverwrite()
    {
        await GuardianAbodeOfferingState.WriteAsync(_fs, new GuardianAbodeOfferingState.PendingAbodeOfferingRequest
        {
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            OfferingType = GuardianAbodeOfferingState.OfferingTypeInkFeathers,
            InkFeathersOffered = 50,
            ReturnCycleId = "return_2"
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => GuardianAbodeOfferingState.WriteAsync(_fs, new GuardianAbodeOfferingState.PendingAbodeOfferingRequest
        {
            GuardianId = "guardian_beta",
            GuardianName = "Варак",
            OfferingType = GuardianAbodeOfferingState.OfferingTypeInkFeathers,
            InkFeathersOffered = 100,
            ReturnCycleId = "return_2"
        }));

        var pending = await GuardianAbodeOfferingState.ReadAsync(_fs);
        Assert.NotNull(pending);
        Assert.Equal("guardian_alpha", pending!.GuardianId);
        Assert.Equal(50, pending.InkFeathersOffered);
    }

    [Fact]
    public async Task GuardianTradeRequest_WriteAsync_MalformedExistingFile_ThrowsAndPreservesCorruption()
    {
        await _fs.WriteFileAtomicAsync(GuardianTradeRequestState.PendingRequestPath, "{");

        await Assert.ThrowsAsync<InvalidOperationException>(() => GuardianTradeRequestState.WriteAsync(_fs, new GuardianTradeRequestState.PendingGuardianTradeRequest
        {
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            ReturnCycleId = "return_2",
            DerivedTradeSlotCount = 2
        }));

        Assert.True(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));
        Assert.Equal("{", await _fs.ReadFileAsync(GuardianTradeRequestState.PendingRequestPath));
    }

    [Fact]
    public async Task PlayerGuardianFoundation_WriteAsync_MalformedExistingFile_ThrowsAndPreservesCorruption()
    {
        await _fs.WriteFileAtomicAsync(PlayerGuardianFoundationState.PendingRequestPath, "{");

        await Assert.ThrowsAsync<InvalidOperationException>(() => PlayerGuardianFoundationState.WriteAsync(_fs, new PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest
        {
            FounderSoulName = "Душа",
            PreviousGuardianId = "guardian_alpha",
            PreviousGuardianName = "Азалия",
            SourceShiningAvailability = ShiningAbodeState.AvailabilitySealedUntilNextAscension,
            ProposedDisplayName = "Северин",
            MantleSummary = "Страж сумерек",
            MantleCreed = "Храни зов",
            AppearanceMotifs = new List<string> { "нить", "заря" },
            DominantAspect = "path",
            CreatedAtTurn = 7
        }));

        Assert.True(_fs.FileExists(PlayerGuardianFoundationState.PendingRequestPath));
        Assert.Equal("{", await _fs.ReadFileAsync(PlayerGuardianFoundationState.PendingRequestPath));
    }

    [Fact]
    public async Task ManifestationRequests_MalformedBundle_DoesNotRewriteSurvivingSubset()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, """
        {
          "requests": [
            {
              "requestId": "manifest_1",
              "manifestationSource": "resident_relic",
              "relicId": "relic_alpha",
              "relicName": "Эхо Зари",
              "sourceResidentId": "resident_alpha",
              "sourceImprintId": "imprint_alpha",
              "sourceGuardianId": "guardian_alpha",
              "sourceGuardianName": "Азалия",
              "targetIncarnation": 3,
              "companionNameHint": "Ирия",
              "createdAtUtc": "2026-04-20T00:00:00Z"
            },
            {
              "requestId":
            }
          ]
        }
        """);

        var before = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");
        var after = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);

        Assert.Equal(before, after);
        Assert.True(await GuardianAbodeResidentRequestState.IsManifestationRequestFileMalformedAsync(_fs));
    }

    [Fact]
    public async Task ValidatePendingResidentsRequestContextAsync_MalformedBundle_RaisesExplicitError()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, """
        {
          "requests": [
            {
              "requestId": "roster_req_1",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "abodeName": "Сад Нитей",
              "createdAtTurn": 7,
              "createdAtUtc": "2026-03-27T00:00:00Z"
            },
            {
              "requestId":
            }
          ]
        }
        """);

        var issues = await InvokeValidationAsync("ValidatePendingGuardianAbodeResidentsRequestContextAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "abode_resident_roster_malformed_file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingResidentInteractionContextAsync_MalformedBundle_RaisesExplicitError()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, """
        {
          "requests": [
            {
              "requestId": "interaction_req_1",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "abodeName": "Сад Нитей",
              "residentId": "resident_alpha",
              "residentName": "Лиора",
              "interactionType": "talk",
              "createdAtTurn": 7,
              "createdAtUtc": "2026-03-27T00:00:00Z"
            },
            {
              "requestId":
            }
          ]
        }
        """);

        var issues = await InvokeValidationAsync("ValidatePendingGuardianAbodeResidentInteractionRequestContextAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "abode_resident_interactions_malformed_file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingResidentTransferContextAsync_MalformedBundle_RaisesExplicitError()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, """
        {
          "requests": [
            {
              "requestId": "transfer_req_1",
              "residentId": "resident_alpha",
              "residentName": "Лиора",
              "sourceGuardianId": "guardian_alpha",
              "sourceGuardianName": "Азалия",
              "sourceAbodeId": "abode_alpha",
              "sourceAbodeName": "Сад Нитей",
              "targetGuardianId": "guardian_beta",
              "targetGuardianName": "Мириэль",
              "targetAbodeId": "abode_beta",
              "targetAbodeName": "Сад Перекрёстков",
              "transferMode": "accepted_transfer",
              "createdAtTurn": 7,
              "createdAtUtc": "2026-03-27T00:00:00Z"
            },
            {
              "requestId":
            }
          ]
        }
        """);

        var issues = await InvokeValidationAsync("ValidatePendingGuardianAbodeResidentTransferRequestContextAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "abode_resident_transfer_malformed_file", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<ValidationIssue>> InvokeValidationAsync(string methodName)
    {
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(_validator, new object[] { issues }) as Task;
        Assert.NotNull(task);
        await task!;
        return issues;
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
            // best-effort cleanup
        }
    }
}
