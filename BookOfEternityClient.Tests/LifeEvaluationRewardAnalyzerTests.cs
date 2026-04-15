using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LifeEvaluationRewardAnalyzerTests
{
    [Fact]
    public void TryComputeDelta_CanonicalSoulState_ReturnsExpectedRewardDelta()
    {
        const string preSoulStateJson = """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": {
            "current": 5
          },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """;

        const string postSoulStateJson = """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": {
            "current": 17
          },
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_life_eval_reward",
                "name": "Реликвия Оценённой Жизни",
                "rarity": "Epic"
              }
            ]
          }
        }
        """;

        var success = LifeEvaluationRewardAnalyzer.TryComputeDelta(
            preSoulStateJson,
            postSoulStateJson,
            out var delta,
            out var error);

        Assert.True(success);
        Assert.NotNull(delta);
        Assert.Null(error);
        Assert.Equal(12, delta!.InkFeathersEarned);
        var newRelic = Assert.Single(delta.NewRelics);
        Assert.Equal("relic_life_eval_reward", newRelic.RelicId);
    }

    [Fact]
    public void TryComputeDelta_MalformedCurrentSoulState_FailsClosed()
    {
        const string preSoulStateJson = """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": {
            "current": 5
          },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """;

        const string postSoulStateJson = """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": {
            "current": "17"
          },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """;

        var success = LifeEvaluationRewardAnalyzer.TryComputeDelta(
            preSoulStateJson,
            postSoulStateJson,
            out var delta,
            out var error);

        Assert.False(success);
        Assert.Null(delta);
        Assert.Contains("invalid current soul_state", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryComputeDelta_MalformedPreTurnSoulStateSnapshot_FailsClosed()
    {
        const string preSoulStateJson = """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": {
            "current": 5
          },
          "soulRelics": []
        }
        """;

        const string postSoulStateJson = """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": {
            "current": 17
          },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """;

        var success = LifeEvaluationRewardAnalyzer.TryComputeDelta(
            preSoulStateJson,
            postSoulStateJson,
            out var delta,
            out var error);

        Assert.False(success);
        Assert.Null(delta);
        Assert.Contains("invalid pre-turn soul_state snapshot", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryComputeDelta_RecordLifeCompletionWithoutTriggerLifeEnd_FailsClosed()
    {
        const string preSoulStateJson = """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": {
            "current": 5
          },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """;

        const string postSoulStateJson = """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 3,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": { "causeOfDeath": "Test" },
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          },
          "inkFeathers": {
            "current": 17
          },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """;

        var success = LifeEvaluationRewardAnalyzer.TryComputeDelta(
            preSoulStateJson,
            postSoulStateJson,
            out var delta,
            out var error);

        Assert.False(success);
        Assert.Null(delta);
        Assert.Contains("recordLifeCompletion", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryComputeDelta_RecordLifeCompletionWithCanonicalTriggerLifeEnd_RemainsReadable()
    {
        const string preSoulStateJson = """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": {
            "current": 5
          },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """;

        const string postSoulStateJson = """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 3,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": { "causeOfDeath": "Test" },
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          },
          "inkFeathers": {
            "current": 5
          },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """;

        var success = LifeEvaluationRewardAnalyzer.TryComputeDelta(
            preSoulStateJson,
            postSoulStateJson,
            hasCanonicalTriggerLifeEnd: true,
            out var delta,
            out var error);

        Assert.True(success);
        Assert.NotNull(delta);
        Assert.Null(error);
        Assert.Equal(0, delta!.InkFeathersEarned);
        Assert.Empty(delta.NewRelics);
    }
}
