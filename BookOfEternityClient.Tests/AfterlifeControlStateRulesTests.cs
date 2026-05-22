using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeControlStateRulesTests
{
    [Fact]
    public void ChangedSemantically_TreatsMissingNullAndNoneAsEquivalentNoControl()
    {
        JsonNode? missing = null;
        JsonNode? none = JsonNode.Parse("""{ "level": "none" }""");

        Assert.False(AfterlifeControlStateRules.ChangedSemantically(missing, none));
        Assert.True(AfterlifeControlStateRules.IsNoActiveSnapshot(missing));
        Assert.True(AfterlifeControlStateRules.IsNoActiveSnapshot(none));
    }

    [Fact]
    public void AuditSnapshotMatchesPrior_RequiresFullCanonicalControlSnapshot()
    {
        var prior = ParseObject("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver", "binding" ],
          "summary": "Old binding."
        }
        """);
        var changedRestrictions = ParseObject("""
        {
          "level": "bound",
          "controllerSide": "opposition",
          "controlId": "control_001",
          "sourceOperation": "binding",
          "restrictedOperations": [ "maneuver" ],
          "summary": "Old binding."
        }
        """);

        Assert.False(AfterlifeControlStateRules.AuditSnapshotMatchesPrior(prior, changedRestrictions));
    }

    [Fact]
    public void HasAntiControlDelta_TreatsSameLevelRestrictionReductionAsWeakening()
    {
        var before = ParseObject("""
        {
          "controlState": {
            "level": "bound",
            "controllerSide": "opposition",
            "controlId": "control_001",
            "sourceOperation": "binding",
            "restrictedOperations": [ "maneuver", "binding" ],
            "summary": "Old binding."
          }
        }
        """);
        var after = ParseObject("""
        {
          "controlState": {
            "level": "bound",
            "controllerSide": "opposition",
            "controlId": "control_001",
            "sourceOperation": "binding",
            "restrictedOperations": [ "maneuver" ],
            "summary": "Weakened binding."
          }
        }
        """);

        Assert.True(AfterlifeControlStateRules.HasAntiControlDelta(before, after));
    }

    [Fact]
    public void CounterAdvancesPlayerControl_DetectsFreshOrStrengthenedPlayerControlOnly()
    {
        var noControl = ParseObject("""{ "controlState": { "level": "none" } }""");
        var playerHindered = ParseObject("""
        {
          "controlState": {
            "level": "hindered",
            "controllerSide": "player",
            "controlId": "control_player_001",
            "sourceOperation": "counter",
            "restrictedOperations": [ "pressure" ],
            "summary": "Counter grip."
          }
        }
        """);
        var oppositionBound = ParseObject("""
        {
          "controlState": {
            "level": "bound",
            "controllerSide": "opposition",
            "controlId": "control_opposition_001",
            "sourceOperation": "binding",
            "restrictedOperations": [ "maneuver" ],
            "summary": "Opposition binding."
          }
        }
        """);

        Assert.True(AfterlifeControlStateRules.CounterAdvancesPlayerControl(noControl, playerHindered));
        Assert.False(AfterlifeControlStateRules.CounterAdvancesPlayerControl(oppositionBound, playerHindered));
    }

    [Fact]
    public void HasForcedIncarnationControl_ReadsCanonicalSourceOperation()
    {
        var forceControl = ParseObject("""
        {
          "controlState": {
            "level": "locked",
            "controllerSide": "opposition",
            "controlId": "control_force_001",
            "sourceOperation": "force_incarnation",
            "restrictedOperations": [ "withdraw", "surrender" ],
            "summary": "Guardian coercion."
          }
        }
        """);
        var ordinaryControl = ParseObject("""
        {
          "controlState": {
            "level": "locked",
            "controllerSide": "opposition",
            "controlId": "control_binding_001",
            "sourceOperation": "binding",
            "restrictedOperations": [ "maneuver" ],
            "summary": "Binding."
          }
        }
        """);

        Assert.True(AfterlifeControlStateRules.HasForcedIncarnationControl(forceControl));
        Assert.False(AfterlifeControlStateRules.HasForcedIncarnationControl(ordinaryControl));
    }

    private static JsonObject ParseObject(string json) =>
        JsonNode.Parse(json)!.AsObject();
}
