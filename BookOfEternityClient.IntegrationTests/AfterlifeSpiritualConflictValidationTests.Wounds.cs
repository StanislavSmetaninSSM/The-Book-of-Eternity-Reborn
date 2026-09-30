using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualConflictValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictFrame_RepeatedEvaluationDoesNotAccumulateNonzeroRewards(bool shining)
    {
        var realm = shining ? "Shining Abode" : "Chaos Sea";
        await WriteSoulStateWithInkFeathersAsync(shining ? 20 : 50, realm);
        if (shining)
            await WriteShiningStateWithLightSparksAsync(8);
        await WriteResolvedConflictRewardStateAsync(BuildConflictRewardAuditJson(
            realm,
            shining ? AfterlifeSpiritualConflictState.RewardCurrencyLightSparks
                : AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            finalAmount: shining ? 3 : 30), realm: realm);
        await WriteRewardTurnSnapshotAsync(
            preTurnSoulJson: BuildSoulStateJson(realm, inkFeathers: 20),
            preTurnShiningJson: shining ? BuildShiningStateJson(5) : null,
            preTurnConflictJson: BuildActiveConflictRootJson(realm: realm));

        var frame = await _validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        var root = JsonNode.Parse(frame.Candidate.Conflict.Text!)!.AsObject();
        Assert.Equal(shining ? 3 : 30,
            root["recentConflicts"]![0]![AfterlifeSpiritualConflictState.RewardAuditProperty]!["finalAmount"]!.GetValue<int>());
        var original = _validator.EvaluateSpiritualConflictValidationFrame(frame);
        Assert.DoesNotContain(original, issue =>
            issue.Code == "afterlife_conflict_reward_currency_delta_mismatch" ||
            issue.Code == "afterlife_conflict_reward_wrong_currency" ||
            issue.Code == "afterlife_conflict_reward_not_allowed" ||
            issue.Code == "afterlife_conflict_reward_missing_currency_baseline");
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCoreAsync()),
            ConflictFrameIssueFingerprint.Create(original));
        for (var run = 0; run < 3; run++)
            Assert.Equal(ConflictFrameIssueFingerprint.Create(original),
                ConflictFrameIssueFingerprint.Create(_validator.EvaluateSpiritualConflictValidationFrame(frame)));

        var changedImages = shining
            ? frame.Candidate with
            {
                Shining = new ValidationService.SpiritualConflictFileImage(true, BuildShiningStateJson(7))
            }
            : frame.Candidate with
            {
                Soul = new ValidationService.SpiritualConflictFileImage(true, BuildSoulStateJson(realm, 49))
            };
        var changed = frame.WithCandidateImages(changedImages);
        Assert.Contains(_validator.EvaluateSpiritualConflictValidationFrame(changed),
            issue => issue.Code == "afterlife_conflict_reward_currency_delta_mismatch");
        Assert.Equal(ConflictFrameIssueFingerprint.Create(original),
            ConflictFrameIssueFingerprint.Create(_validator.EvaluateSpiritualConflictValidationFrame(frame)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictFrame_RepeatedEvaluationDoesNotConsumeHistoricalOccurrences(bool recentProof)
    {
        await WriteSoulStateWithLightIncarnateAsync();
        if (recentProof)
        {
            await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
            {
              "schemaVersion": 1,
              "activeConflict": null,
              "recentConflicts": [
                {
                  "dangerMode": "hostile",
                  "conflictId": "frame_historical_without_turn",
                  "resolutionState": "resolved",
                  "operationType": "guard",
                  "playerOutcome": "won",
                  "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
                }
              ]
            }
            """);
        }
        else
        {
            await WriteConflictStateWithRawExchangeAsync($$"""
            {
              "exchangeId": "frame_historical_exchange_without_turn",
              "operationType": "guard",
              "outcome": "success",
              "before": { "conflictPosition": "contested" },
              "after": { "conflictPosition": "player_advantaged" },
              "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
            }
            """);
        }
        await WriteValidatedConflictSnapshotFromCurrentAsync(
            "Continue with the already accepted historical conflict payload.");

        var frame = await _validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        var original = _validator.EvaluateSpiritualConflictValidationFrame(frame);
        Assert.DoesNotContain(original,
            issue => issue.Code == "afterlife_conflict_light_incarnate_modifier_mismatch");
        Assert.Equal(ConflictFrameIssueFingerprint.Create(await ValidateCoreAsync()),
            ConflictFrameIssueFingerprint.Create(original));
        for (var run = 0; run < 3; run++)
            Assert.Equal(ConflictFrameIssueFingerprint.Create(original),
                ConflictFrameIssueFingerprint.Create(_validator.EvaluateSpiritualConflictValidationFrame(frame)));

        // One accepted occurrence must not authorize a second identical current row.
        var candidateRoot = JsonNode.Parse(frame.Candidate.Conflict.Text!)!.AsObject();
        var rows = (recentProof ? candidateRoot["recentConflicts"]
            : candidateRoot["activeConflict"]!["exchangeLog"])!.AsArray();
        Assert.Single(rows);
        rows.Add(rows[0]!.DeepClone());
        var duplicate = frame.WithCandidateImages(frame.Candidate with
        {
            Conflict = new ValidationService.SpiritualConflictFileImage(true, candidateRoot.ToJsonString())
        });
        var duplicateIssues = _validator.EvaluateSpiritualConflictValidationFrame(duplicate);
        Assert.Contains(duplicateIssues, issue =>
            issue.Code == "afterlife_conflict_light_incarnate_modifier_mismatch" &&
            issue.FilePath.Contains(recentProof ? "recentConflicts[1]" : "exchangeLog[1]", StringComparison.Ordinal) &&
            issue.Actual?.Contains("auditTurn=missing", StringComparison.Ordinal) == true);
        Assert.Equal(ConflictFrameIssueFingerprint.Create(original),
            ConflictFrameIssueFingerprint.Create(_validator.EvaluateSpiritualConflictValidationFrame(frame)));
    }
}


