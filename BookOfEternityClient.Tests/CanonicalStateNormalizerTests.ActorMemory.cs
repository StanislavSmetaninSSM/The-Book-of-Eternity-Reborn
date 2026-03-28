using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MergesGuardianAndResidentActorJournals()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianThoughtJournalState.StatePath, """
        {
          "guardianThoughtJournalUpdates": [
            {
              "entryId": "gthought_1",
              "guardianId": "guardian_azalia",
              "turn": 12,
              "timestamp": "2026-03-27T10:00:00Z",
              "title": "Внимательный интерес",
              "summary": "Азалия присматривается к душе."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, """
        {
          "entries": [
            {
              "residentId": "resident_liora",
              "guardianId": "guardian_azalia",
              "abodeId": "abode_threads",
              "displayName": "Лиора",
              "residentKind": "wayfaring_soul",
              "originType": "traveler_soul",
              "roleLabel": "Вестница",
              "summary": "Слушает нити дорог.",
              "bondLevel": 61,
              "bondTier": "trusted",
              "canGrantCompanionRelic": true,
              "bondRewardState": "none",
              "linkedSoulQuestId": "",
              "grantedRelicId": "",
              "historyRevealed": false,
              "availableInteractions": ["talk"],
              "isPresent": true,
              "mortalWorldImprint": {
                "originWorldSummary": "Была посланницей.",
                "futureCompanionPrompt": "Messenger"
              }
            }
          ],
          "residentThoughtJournalUpdates": [
            {
              "entryId": "rthought_1",
              "residentId": "resident_liora",
              "turn": 12,
              "timestamp": "2026-03-27T10:01:00Z",
              "title": "Ждёт честности",
              "summary": "Лиора хочет понять, не солжёт ли ей душа."
            }
          ],
          "residentInteractionLogUpdates": [
            {
              "entryId": "revent_1",
              "residentId": "resident_liora",
              "turn": 12,
              "timestamp": "2026-03-27T10:02:00Z",
              "eventType": "conversation",
              "title": "Разговор у края сада",
              "summary": "Лиора призналась, что боится опоздать снова."
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardianThoughtJson = await _fs.ReadFileAsync(GuardianThoughtJournalState.StatePath);
        Assert.NotNull(guardianThoughtJson);
        Assert.Contains("\"entries\": [", guardianThoughtJson, StringComparison.Ordinal);
        Assert.DoesNotContain("guardianThoughtJournalUpdates", guardianThoughtJson, StringComparison.Ordinal);

        var residentJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        Assert.NotNull(residentJson);
        Assert.Contains("\"thoughtJournal\": [", residentJson, StringComparison.Ordinal);
        Assert.Contains("\"interactionLog\": [", residentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("residentThoughtJournalUpdates", residentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("residentInteractionLogUpdates", residentJson, StringComparison.Ordinal);
    }
}
