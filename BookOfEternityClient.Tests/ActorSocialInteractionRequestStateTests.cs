using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ActorSocialInteractionRequestStateTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public ActorSocialInteractionRequestStateTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-actor-social-requests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task WriteGuardianRequestAsync_DedupesByGuardianAndInteractionType()
    {
        await ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, new ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest
        {
            RequestId = "guardian_req_old",
            GuardianId = "guardian_azalia",
            GuardianName = "Азалия",
            InteractionType = ActorSocialInteractionRequestState.GuardianInteractionTypeTalk,
            CreatedAtTurn = 11,
            CreatedAtUtc = "2026-03-27T09:00:00Z"
        });

        await ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, new ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest
        {
            RequestId = "guardian_req_new",
            GuardianId = "guardian_azalia",
            GuardianName = "Азалия",
            InteractionType = ActorSocialInteractionRequestState.GuardianInteractionTypeTalk,
            CreatedAtTurn = 12,
            CreatedAtUtc = "2026-03-27T10:00:00Z"
        });

        var requests = await ActorSocialInteractionRequestState.ReadGuardianRequestsAsync(_fs);

        Assert.Single(requests);
        Assert.Equal("guardian_req_new", requests[0].RequestId);
    }

    [Fact]
    public async Task EnsureHealthyAsync_RemovesResolvedGuardianRequestViaSocialJournal()
    {
        await ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, new ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest
        {
            RequestId = "guardian_req_1",
            GuardianId = "guardian_azalia",
            GuardianName = "Азалия",
            InteractionType = ActorSocialInteractionRequestState.GuardianInteractionTypeLore,
            CreatedAtTurn = 12,
            CreatedAtUtc = "2026-03-27T10:00:00Z"
        });

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianSocialJournalState.StatePath, """
        {
          "entries": [
            {
              "entryId": "guardian_social_entry_1",
              "guardianId": "guardian_azalia",
              "requestId": "guardian_req_1",
              "interactionType": "lore",
              "status": "accepted",
              "responseMode": "lore_revealed",
              "turn": 12,
              "timestamp": "2026-03-27T10:01:00Z",
              "title": "Нить старого знания",
              "summary": "Азалия раскрыла старую правду о мире."
            }
          ]
        }
        """);

        await ActorSocialInteractionRequestState.EnsureHealthyAsync(_fs, "Chaos Sea");

        var requests = await ActorSocialInteractionRequestState.ReadGuardianRequestsAsync(_fs);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_AfterlifeIncludesFullGuardianSocialDto()
    {
        await ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, new ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest
        {
            RequestId = "guardian_req_full",
            GuardianId = "guardian_azalia",
            GuardianName = "Азалия",
            InteractionType = ActorSocialInteractionRequestState.GuardianInteractionTypeLore,
            CreatedAtTurn = 15,
            CreatedAtUtc = "2026-03-27T13:00:00Z"
        });

        var reminder = await ActorSocialInteractionRequestState.BuildSystemReminderFragmentAsync(_fs, "Chaos Sea");

        Assert.NotNull(reminder);
        Assert.Contains("GUARDIAN SOCIAL REQUESTS", reminder, StringComparison.Ordinal);
        Assert.Contains("requestId=guardian_req_full", reminder, StringComparison.Ordinal);
        Assert.Contains("createdAtTurn=15", reminder, StringComparison.Ordinal);
        Assert.Contains("Full pending guardian-social DTO", reminder, StringComparison.Ordinal);
        Assert.Contains("\"requestId\": \"guardian_req_full\"", reminder, StringComparison.Ordinal);
        Assert.Contains("\"createdAtUtc\": \"2026-03-27T13:00:00Z\"", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_MortalRealm_IncludesNpcSocialRequests()
    {
        await ActorSocialInteractionRequestState.WriteNpcRequestAsync(_fs, new ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest
        {
            RequestId = "npc_req_1",
            NpcId = "npc_merchant_01",
            NpcName = "Старый Торговец",
            InteractionType = ActorSocialInteractionRequestState.NpcInteractionTypeTalk,
            CreatedAtTurn = 7,
            CreatedAtUtc = "2026-03-27T08:00:00Z"
        });

        var reminder = await ActorSocialInteractionRequestState.BuildSystemReminderFragmentAsync(_fs, "Mortal World");

        Assert.NotNull(reminder);
        Assert.Contains("NPC SOCIAL REQUESTS", reminder, StringComparison.Ordinal);
        Assert.Contains("npc_merchant_01", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureHealthyAsync_MalformedGuardianRequestFile_PreservesCorruptionAndBlocksOverwrite()
    {
        await _fs.WriteFileAtomicAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, "{");

        await ActorSocialInteractionRequestState.EnsureHealthyAsync(_fs, "Chaos Sea");
        var reminder = await ActorSocialInteractionRequestState.BuildSystemReminderFragmentAsync(_fs, "Chaos Sea");

        Assert.True(_fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
        Assert.NotNull(reminder);
        Assert.Contains("CORRUPTION", reminder, StringComparison.OrdinalIgnoreCase);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, new ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest
            {
                RequestId = "guardian_req_new",
                GuardianId = "guardian_azalia",
                GuardianName = "Азалия",
                InteractionType = ActorSocialInteractionRequestState.GuardianInteractionTypeTalk,
                CreatedAtTurn = 13,
                CreatedAtUtc = "2026-03-27T11:00:00Z"
            }));

        Assert.Equal("{", await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
    }

    [Fact]
    public async Task EnsureHealthyAsync_UnresolvedRealm_PreservesGuardianAndNpcRequests()
    {
        await ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, new ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest
        {
            RequestId = "guardian_req_unresolved",
            GuardianId = "guardian_azalia",
            GuardianName = "Азалия",
            InteractionType = ActorSocialInteractionRequestState.GuardianInteractionTypeTalk,
            CreatedAtTurn = 14,
            CreatedAtUtc = "2026-03-27T12:00:00Z"
        });
        await ActorSocialInteractionRequestState.WriteNpcRequestAsync(_fs, new ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest
        {
            RequestId = "npc_req_unresolved",
            NpcId = "npc_merchant_01",
            NpcName = "Старый Торговец",
            InteractionType = ActorSocialInteractionRequestState.NpcInteractionTypeTalk,
            CreatedAtTurn = 14,
            CreatedAtUtc = "2026-03-27T12:00:00Z"
        });

        await ActorSocialInteractionRequestState.EnsureHealthyAsync(_fs, "");

        var guardianRequests = await ActorSocialInteractionRequestState.ReadGuardianRequestsAsync(_fs);
        var npcRequests = await ActorSocialInteractionRequestState.ReadNpcRequestsAsync(_fs);
        Assert.Single(guardianRequests);
        Assert.Single(npcRequests);
        Assert.True(_fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
        Assert.True(_fs.FileExists(ActorSocialInteractionRequestState.PendingNpcRequestPath));
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
            // ignored
        }
    }
}
