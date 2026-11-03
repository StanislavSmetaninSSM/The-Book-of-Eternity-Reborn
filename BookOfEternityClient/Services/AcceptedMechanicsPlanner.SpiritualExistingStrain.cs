using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    internal sealed partial class ResourceExecutionSession
    {
        internal sealed partial class SpiritualMechanicsContext
        {
            // Immutable DATA only within this exact owner/ordinal/epoch/draft/dependency-cut lifetime.
            // Allows rechecks all candidate siblings; IsCurrent remains fresh on every query.
            private ValidationService.SpiritualStrainDraftCorrection? _existingStrainComparison;

            /// <summary>Derives one signed-existing parent's comparison without retaining permission or replacing other burdens.</summary>
            private ValidationService.SpiritualStrainDraftCorrection? ReadSignedExistingStrainCorrection(
                JsonObject conflict, JsonObject exchange)
            {
                if (!IsCurrent || !string.Equals(exchange["operationType"]?.GetValue<string>(), "maneuver", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(exchange["outcome"]?.GetValue<string>(), "setback", StringComparison.OrdinalIgnoreCase) ||
                    exchange["before"] is not JsonObject before || exchange["after"] is not JsonObject after)
                    return null;
                try
                {
                    var actor = ValidationService.SpiritualWoundSourceSession.ActorKey(
                        conflict["playerSide"]?["leadContestant"] as JsonObject);
                    if (actor is null || !actor.StartsWith("player_soul:", StringComparison.Ordinal)) return null;
                    if (!_source.OwnsSignedStrainActor(_capture, conflict, actor)) return null;
                    _source.RecordExistingStrainComparisonQuery();
                    if (_existingStrainComparison is { } retained)
                        return retained.Allows(exchange) ? retained : null;
                    var actorId = actor["player_soul:".Length..];
                    var matching = Contributions.Where(row => row.ResolvedSide == "player" &&
                        row.Actor.Kind == "player" && row.Actor.TargetId == actorId && row.Operation == "maneuver" &&
                        row.Profile == "spiritual_strain_burden").ToArray();
                    if (matching.Length != 1 || matching[0] is not { CurrentStacks: 1 } contribution ||
                        contribution.WoundSource.Kind != "wound" || contribution.Actor.Realm != contribution.WoundSource.Realm ||
                        !contribution.Magnitude.TryGetInt32(out var magnitude) || magnitude != 1 ||
                        !JsonNode.DeepEquals(before["oppositionSideStrain"], after["oppositionSideStrain"])) return null;
                    var issues = new List<ValidationIssue>();
                    var view = ReadCurrentWoundView(issues);
                    if (view?.WoundCarriers is not { } carriers || view.WoundIdentity is not { } identity ||
                        view.WoundHistory is not { } history || issues.Count != 0) return null;
                    var parent = SpiritualWoundCurrentData.ReadActive(carriers, identity.ToJsonString(), history.ToJsonString(),
                        contribution.WoundSource.Realm, contribution.WoundSource.SourceId, actor);
                    var signed = _source.ReadSignedStrainParent(_capture, conflict, parent.WoundId, actor);
                    var parentJson = WoundMaterializationContract.SerializeCanonical(parent);
                    if (parent.Classification.Domain != "spiritual" || parent.Severity.Rank is not (3 or 4) ||
                        parentJson != WoundMaterializationContract.SerializeCanonical(signed) ||
                        !_owner._routing!.TryReadSpiritualWoundGroup(new(parent.Owner.Realm, "wound", parent.WoundId), out var group) ||
                        !MatchesSignedParentSourceGroup(parent, contribution, group)) return null;
                    var ordinary = exchange.DeepClone().AsObject();
                    ordinary["after"]!["playerSideStrain"] = before["playerSideStrain"]!.DeepClone();
                    ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "currentStrain.ordinary", issues);
                    ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "currentStrain.ordinary", issues);
                    ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
                    if (issues.Any(issue => issue.Severity == IssueSeverity.Error)) return null;
                    var proof = new JsonObject { ["version"] = 2,
                        ["ordinaryExchangeJsonBase64"] = Encode(SpiritualWoundStateJson.Canonical(ordinary)),
                        ["parentWoundJsonBase64"] = Encode(parentJson),
                        ["effectId"] = contribution.EffectId, ["componentId"] = contribution.ComponentId };
                    var comparison = new ValidationService.SpiritualStrainDraftCorrection(
                        "/activeConflict/exchangeLog/" + Ordinal, ordinary, proof.ToJsonString(),
                        contribution.EffectId, contribution.ComponentId);
                    if (!IsCurrent || !comparison.Allows(exchange)) return null;
                    return _existingStrainComparison = comparison;
                }
                catch (Exception error) when (error is InvalidOperationException or FormatException or JsonException or ArgumentException)
                {
                    return null;
                }
            }

            /// <summary>Requires exact current source definitions, owner, target, root lineage and selected component.</summary>
            internal static bool MatchesSignedParentSourceGroup(WoundMaterializationEnvelope parent,
                SpiritualWoundConflictContribution contribution, WoundSourceGroupAuthority group)
            {
                if (group.Key != new EffectIdentitySourceGroup(parent.Owner.Realm, "wound", parent.WoundId) ||
                    contribution.WoundSource.Realm != parent.Owner.Realm || contribution.WoundSource.Kind != "wound" ||
                    contribution.WoundSource.SourceId != parent.WoundId ||
                    group.Owner != parent.Owner || group.Target != contribution.Actor ||
                    group.GraphAuthorityFingerprint != group.RecomputeGraphAuthorityFingerprint()) return false;
                var definitions = group.Definitions;
                var facts = parent.Consequences.OwnedEffectSources.DefinitionFacts;
                if (definitions.Count != facts.Count || facts.Any(fact => definitions.Count(definition =>
                    definition.DefinitionKey == fact.DefinitionKey &&
                    JsonNode.DeepEquals(definition.Definition, SpiritualWoundStateJson.Parse(fact.CanonicalJson))) != 1)) return false;
                var roots = parent.Consequences.OwnedEffectSources.RootBindings;
                var lineage = group.ExistingRootLineage.Concat(group.ApplicationRootLineage).ToArray();
                if (lineage.Length != roots.Count || roots.Any(root => lineage.Count(row =>
                    row.EffectId == root.EffectId && row.DefinitionKey == root.DefinitionKey) != 1)) return false;
                var selected = roots.SingleOrDefault(root => root.EffectId == contribution.EffectId &&
                    root.DefinitionKey == contribution.WoundSource.DefinitionKey);
                if (selected is null) return false;
                var body = definitions.Single(row => row.DefinitionKey == selected.DefinitionKey).Definition;
                return body["components"]!.AsArray().OfType<JsonObject>().Count(component =>
                    component["componentId"]?.GetValue<string>() == contribution.ComponentId &&
                    component["profile"]?.GetValue<string>() == "spiritual_strain_burden") == 1;
            }

            private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
