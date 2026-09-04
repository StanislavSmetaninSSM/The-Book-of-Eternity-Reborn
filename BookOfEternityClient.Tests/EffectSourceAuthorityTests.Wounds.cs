using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectSourceAuthorityWoundTests
{
    [Fact]
    public void Build_AcceptsExactSourceBoundWoundConsequence()
    {
        var definition = CreateSourceBoundWoundDefinition();
        var authority = Build(
            Source("wound", WoundId, definition));

        var result = authority.Resolve(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                WoundId,
                EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject());

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, authority.Issues.Select(static issue =>
                $"{issue.Code}: {issue.FilePath}")));
        Assert.DoesNotContain(authority.Issues, issue =>
            issue.Code?.StartsWith("effect_source_wound_", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("case")]
    [InlineData("confusable")]
    [InlineData("stale")]
    public void Build_RejectsMissingOrInexactWoundConsequenceLinkWithoutRetargeting(
        string mutation)
    {
        var definition = CreateSourceBoundWoundDefinition();
        switch (mutation)
        {
            case "missing":
                definition["links"] = new JsonArray();
                break;
            case "case":
                definition["links"]![0]!["targetId"] = "WOUND_TEST_TORN_SIDE";
                break;
            case "confusable":
                definition["links"]![0]!["targetId"] = "wound_teѕt_torn_side";
                break;
            case "stale":
                definition["components"]![0]!["payload"]!["woundId"] = "wound_retired";
                definition["links"]![0]!["targetId"] = "wound_retired";
                break;
            default:
                throw new InvalidOperationException(mutation);
        }

        var authority = Build(
            new[]
            {
                Source("wound", WoundId, definition),
                Source(
                    "wound",
                    "wound_unrelated_available",
                    definitions: new JsonArray())
            },
            historicalSourceIds: new HashSet<string>(StringComparer.Ordinal)
            {
                "wound_retired"
            });

        Assert.Contains(authority.Issues, issue =>
            issue.Code is "effect_source_wound_link_missing" or
                "effect_source_wound_link_mismatch" or
                "effect_source_wound_payload_mismatch");
        var result = authority.Resolve(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                WoundId,
                EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject());
        Assert.False(result.Success);
        Assert.Null(result.Source);
    }

    [Fact]
    public void Build_RejectsWoundConsequencePayloadThatDoesNotMatchItsExactSourceLink()
    {
        var definition = CreateSourceBoundWoundDefinition();
        definition["components"]![0]!["payload"]!["woundId"] =
            "wound_other_exact";
        var authority = Build(
            Source("wound", WoundId, definition),
            Source(
                "wound",
                "wound_other_exact",
                definitions: new JsonArray()));

        Assert.Contains(authority.Issues, issue =>
            issue.Code == "effect_source_wound_payload_mismatch");
    }

    [Fact]
    public void Build_RejectsSourceBoundWoundConsequenceBoundToNonWoundSource()
    {
        var definition = CreateSourceBoundWoundDefinition();
        var authority = Build(
            Source("skill", "skill_wrong_wound_owner", definition),
            Source("wound", WoundId, definitions: new JsonArray()));

        Assert.Contains(authority.Issues, issue =>
            issue.Code == "effect_source_wound_source_bound_mismatch");
    }

    [Theory]
    [InlineData(false, 2001, "effect_source_wound_pre_turn_aggregate_limit_exceeded")]
    [InlineData(true, 33, "effect_source_wound_same_turn_aggregate_limit_exceeded")]
    public void Build_RejectsAggregateWoundDefinitionOverflowBeforePartialAuthority(
        bool sameTurn,
        int sourceCount,
        string expectedCode)
    {
        var exports = CreateAggregateWoundSources(sourceCount, sameTurn);
        var groups = CreateAggregateWoundGroups(exports, sameTurn);
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            sameTurn ? Array.Empty<EffectSourceExport>() : exports,
            sameTurn ? exports : Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: groups));

        Assert.Contains(authority.Issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.Ordinal));
        var first = exports[0];
        var firstDefinition = first.Definitions[0]!["definitionKey"]!
            .GetValue<string>();
        var resolution = authority.Resolve(
            new EffectSourceKey(
                first.Realm,
                first.Kind,
                first.SourceId,
                firstDefinition),
            "player",
            new JsonObject());
        Assert.False(resolution.Success);
        Assert.Null(resolution.Source);
    }

    [Theory]
    [InlineData(false, 2000)]
    [InlineData(true, 32)]
    public void Build_AcceptsExactAggregateWoundDefinitionBoundary(
        bool sameTurn,
        int sourceCount)
    {
        var exports = CreateAggregateWoundSources(sourceCount, sameTurn);
        var groups = CreateAggregateWoundGroups(exports, sameTurn);
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            sameTurn ? Array.Empty<EffectSourceExport>() : exports,
            sameTurn ? exports : Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: groups));

        Assert.DoesNotContain(authority.Issues, issue =>
            issue.Code is
                "effect_source_wound_pre_turn_aggregate_limit_exceeded" or
                "effect_source_wound_same_turn_aggregate_limit_exceeded" or
                "effect_source_wound_pre_turn_root_aggregate_limit_exceeded" or
                "effect_source_wound_same_turn_root_aggregate_limit_exceeded");
        Assert.Equal(groups.Length, authority.SnapshotWoundGroupAuthorities().Count);
        var last = exports[^1];
        var definitionKey = last.Definitions[^1]!["definitionKey"]!
            .GetValue<string>();
        var resolution = authority.ResolveCanonicalBinding(
            new EffectSourceKey(
                last.Realm,
                last.Kind,
                last.SourceId,
                definitionKey),
            "player");
        Assert.True(
            resolution.Success,
            string.Join(" | ", authority.Issues.Select(static issue =>
                issue.Code + ":" + issue.Message)));
    }

    [Fact]
    public void ResolveTypedWoundRootBinding_AdmitsOnlyExactSealedApplicationRoot()
    {
        const string sourceRef = "draft_wound_typed_root";
        const string applicationRef = "draft_application_typed_root";
        const string sourceExportFingerprint =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var definition = CreateSourceBoundWoundDefinition();
        var definitionKey = definition["definitionKey"]!.GetValue<string>();
        var export = new EffectSourceExport(
            "mortal_world",
            "wound",
            WoundId,
            new JsonArray(definition.DeepClone()),
            Materializable: false,
            Active: true,
            SameTurn: true,
            SourceRef: sourceRef,
            SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal)
            {
                "active"
            });
        var owner = new WoundOwnerCoordinate(
            "mortal_world",
            "player",
            "player_current",
            WoundCarrierCatalog.PlayerPath);
        var target = new EffectTargetKey(
            "mortal_world",
            "player",
            "player_current");
        var key = new EffectSourceKey(
            "mortal_world",
            "wound",
            WoundId,
            definitionKey);
        var root = new WoundRootLineageAuthorityRow(
            applicationRef,
            null,
            definitionKey,
            WoundRootOwnershipDomain.BaseWound);
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            Array.Empty<EffectSourceExport>(),
            new[] { export },
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: new[]
            {
                new WoundSourceGroupAuthority(
                    new EffectIdentitySourceGroup(
                        "mortal_world",
                        "wound",
                        WoundId),
                    owner,
                    target,
                    sameTurn: true,
                    sourceRef,
                    sourceExportFingerprint,
                    new[]
                    {
                        new WoundEffectSourceDefinition(
                            definitionKey,
                            definition)
                    },
                    new[] { root },
                    Array.Empty<WoundRootLineageAuthorityRow>())
            }));
        Assert.Empty(authority.Issues);

        var result = authority.ResolveTypedWoundRootBinding(
            new WoundTypedRootBindingRequest(
                key,
                new WoundEffectSourceSelector(
                    "mortal_world",
                    "wound",
                    null,
                    sourceRef,
                    definitionKey),
                WoundTypedRootSourceIdentityKind.NewSourceRef,
                target,
                sourceExportFingerprint,
                applicationRef,
                WoundRootOwnershipDomain.BaseWound));

        Assert.True(
            result.Success,
            string.Join(" | ", result.Issues.Select(static issue =>
                issue.Code + ":" + issue.Message)));
        Assert.Equal(key, result.Source!.Key);
        Assert.Equal(root, result.Root);
        Assert.Equal(WoundId, result.Group!.Key.SourceId);
        var ordinary = authority.Resolve(
            new JsonObject
            {
                ["kind"] = "wound",
                ["sourceRef"] = sourceRef,
                ["definitionKey"] = definitionKey
            },
            "mortal_world",
            "player",
            new JsonObject());
        Assert.False(ordinary.Success);
        Assert.Contains(ordinary.Issues, static issue =>
            issue.Code == "effect_source_not_materializable");
    }

    [Fact]
    public void ResolveTypedWoundRootBinding_SeverityGenerationKeepsPredecessorOutOfCurrentLineage()
    {
        var fixture = WoundEffectBatchPlannerTests.BuildRetainedWorsenPureFixture();
        var batch = Assert.Single(fixture.Prepared.EffectOperationBatches);
        var root = Assert.Single(batch.RootApplications, static value =>
            value.PriorRootEffectId is not null);
        Assert.DoesNotContain(batch.RootLineageAuthority, static row =>
            row.EffectId is not null);
        var target = root.ExpectedTargetKey;
        var export = new EffectSourceExport(
            batch.SourceExport.Realm,
            batch.SourceExport.Kind,
            batch.SourceExport.SourceId,
            new JsonArray(batch.SourceExport.Definitions.Select(static value =>
                (JsonNode)value.Definition).ToArray()),
            Materializable: false,
            Active: true,
            SameTurn: true,
            SourceRef: batch.SourceExport.SourceRef);
        var group = new WoundSourceGroupAuthority(
            new EffectIdentitySourceGroup(
                batch.SourceExport.Realm,
                batch.SourceExport.Kind,
                batch.SourceExport.SourceId),
            batch.SourceExport.Owner,
            target,
            sameTurn: true,
            batch.SourceExport.SourceRef,
            batch.SourceExportFingerprint,
            batch.SourceExport.Definitions,
            batch.RootLineageAuthority,
            Array.Empty<WoundRootLineageAuthorityRow>());
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            Array.Empty<EffectSourceExport>(),
            new[] { export },
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: new[] { group }));

        var result = authority.ResolveTypedWoundRootBinding(
            new WoundTypedRootBindingRequest(
                root.ExpectedSourceKey,
                root.SourceSelector,
                WoundTypedRootSourceIdentityKind.NewSourceRef,
                target,
                batch.SourceExportFingerprint,
                root.ApplicationRef,
                root.OwnershipDomain));

        Assert.True(result.Success, string.Join(Environment.NewLine,
            result.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        Assert.Equal(root.ApplicationRef, result.Root!.ApplicationRef);
        Assert.Empty(result.Group!.ExistingRootLineage);
        var priorIdentity = Assert.IsType<JsonObject>(Assert.Single(
            fixture.Input.PreTurnEffectIdentityIndex!["entries"]!.AsArray()));
        Assert.Equal(root.PriorRootEffectId,
            priorIdentity["effectId"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("source_ref")]
    [InlineData("source_id_for_new")]
    [InlineData("target")]
    [InlineData("source_export_fingerprint")]
    [InlineData("application_ref")]
    [InlineData("ownership_domain")]
    public void ResolveTypedWoundRootBinding_RejectsAnyChangedAuthorityCoordinate(
        string mutation)
    {
        var fixture = CreateTypedRootFixture();
        var request = fixture.Request;
        request = mutation switch
        {
            "source_ref" => request with
            {
                SourceSelector = request.SourceSelector with
                {
                    SourceRef = "draft_wound_typed_root_changed"
                }
            },
            "source_id_for_new" => request with
            {
                SourceSelector = request.SourceSelector with
                {
                    SourceId = WoundId,
                    SourceRef = null
                }
            },
            "target" => request with
            {
                Target = request.Target with
                {
                    Kind = "npc",
                    TargetId = "npc_foreign"
                }
            },
            "source_export_fingerprint" => request with
            {
                SourceExportFingerprint =
                    "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
            },
            "application_ref" => request with
            {
                ApplicationRef = "draft_application_typed_root_changed"
            },
            "ownership_domain" => request with
            {
                OwnershipDomain = WoundRootOwnershipDomain.ForComplication(
                    "complication_typed_root_changed")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };

        var result = fixture.Authority.ResolveTypedWoundRootBinding(request);

        Assert.False(result.Success);
        Assert.Null(result.Source);
        Assert.Null(result.Group);
        Assert.Null(result.Root);
        Assert.NotEmpty(result.Issues);
    }

    [Theory]
    [InlineData("base_wound", "complication_forbidden")]
    [InlineData("complication", null)]
    [InlineData("unknown_domain", null)]
    public void Build_RejectsMalformedWoundRootOwnershipDomain(
        string domainKind,
        string? complicationId)
    {
        var export = Assert.Single(CreateAggregateWoundSources(1, sameTurn: true));
        var definitions = export.Definitions.OfType<JsonObject>()
            .Select(definition => new WoundEffectSourceDefinition(
                definition["definitionKey"]!.GetValue<string>(),
                definition))
            .ToArray();
        var group = new WoundSourceGroupAuthority(
            new EffectIdentitySourceGroup(
                export.Realm,
                export.Kind,
                export.SourceId),
            new WoundOwnerCoordinate(
                export.Realm,
                "player",
                "player_current",
                WoundCarrierCatalog.PlayerPath),
            new EffectTargetKey(
                export.Realm,
                "player",
                "player_current"),
            sameTurn: true,
            export.SourceRef,
            preparedSourceExportFingerprint: null,
            definitions,
            new[]
            {
                new WoundRootLineageAuthorityRow(
                    "application_invalid_domain",
                    null,
                    definitions[0].DefinitionKey,
                    new WoundRootOwnershipDomain(domainKind, complicationId))
            },
            Array.Empty<WoundRootLineageAuthorityRow>());

        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            Array.Empty<EffectSourceExport>(),
            new[] { export },
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: new[] { group }));

        Assert.Contains(authority.Issues, static issue =>
            issue.Code == "effect_source_wound_group_root_invalid");
        Assert.Empty(authority.SnapshotWoundGroupAuthorities());
    }

    [Theory]
    [InlineData(false, 2001, "effect_source_wound_pre_turn_root_aggregate_limit_exceeded")]
    [InlineData(true, 33, "effect_source_wound_same_turn_root_aggregate_limit_exceeded")]
    public void Build_RejectsAggregateWoundRootOverflowBeforePartialAuthority(
        bool sameTurn,
        int sourceCount,
        string expectedCode)
    {
        var exports = CreateAggregateWoundSources(sourceCount, sameTurn);
        var groups = CreateAggregateWoundGroups(exports, sameTurn);
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            sameTurn ? Array.Empty<EffectSourceExport>() : exports,
            sameTurn ? exports : Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: groups));

        Assert.Contains(authority.Issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.Ordinal));
        Assert.Empty(authority.SnapshotWoundGroupAuthorities());
        var firstDefinition = exports[0].Definitions[0]!["definitionKey"]!
            .GetValue<string>();
        Assert.False(authority.ResolveCanonicalBinding(
            new EffectSourceKey(
                exports[0].Realm,
                exports[0].Kind,
                exports[0].SourceId,
                firstDefinition),
            "player").Success);
    }

    private const string WoundId = "wound_test_torn_side";

    private static TypedRootFixture CreateTypedRootFixture()
    {
        const string sourceRef = "draft_wound_typed_root";
        const string applicationRef = "draft_application_typed_root";
        const string sourceExportFingerprint =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var definition = CreateSourceBoundWoundDefinition();
        var definitionKey = definition["definitionKey"]!.GetValue<string>();
        var owner = new WoundOwnerCoordinate(
            "mortal_world",
            "player",
            "player_current",
            WoundCarrierCatalog.PlayerPath);
        var target = new EffectTargetKey(
            "mortal_world",
            "player",
            "player_current");
        var key = new EffectSourceKey(
            "mortal_world",
            "wound",
            WoundId,
            definitionKey);
        var root = new WoundRootLineageAuthorityRow(
            applicationRef,
            null,
            definitionKey,
            WoundRootOwnershipDomain.BaseWound);
        var export = new EffectSourceExport(
            key.Realm,
            key.Kind,
            key.SourceId,
            new JsonArray(definition.DeepClone()),
            Materializable: false,
            Active: true,
            SameTurn: true,
            SourceRef: sourceRef,
            SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal)
            {
                "active"
            });
        var group = new WoundSourceGroupAuthority(
            new EffectIdentitySourceGroup(key.Realm, key.Kind, key.SourceId),
            owner,
            target,
            sameTurn: true,
            sourceRef,
            sourceExportFingerprint,
            new[]
            {
                new WoundEffectSourceDefinition(definitionKey, definition)
            },
            new[] { root },
            Array.Empty<WoundRootLineageAuthorityRow>());
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            Array.Empty<EffectSourceExport>(),
            new[] { export },
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: new[] { group }));
        Assert.Empty(authority.Issues);
        return new TypedRootFixture(
            authority,
            new WoundTypedRootBindingRequest(
                key,
                new WoundEffectSourceSelector(
                    key.Realm,
                    key.Kind,
                    null,
                    sourceRef,
                    definitionKey),
                WoundTypedRootSourceIdentityKind.NewSourceRef,
                target,
                sourceExportFingerprint,
                applicationRef,
                WoundRootOwnershipDomain.BaseWound));
    }

    private sealed record TypedRootFixture(
        EffectSourceAuthority Authority,
        WoundTypedRootBindingRequest Request);

    private static JsonObject CreateSourceBoundWoundDefinition()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "wound_consequence");
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "expire"
        };
        return definition;
    }

    private static EffectSourceExport[] CreateAggregateWoundSources(
        int sourceCount,
        bool sameTurn) =>
        Enumerable.Range(1, sourceCount).Select(sourceOrdinal =>
        {
            var woundId = $"wound_aggregate_{sourceOrdinal:D4}";
            var definitions = new JsonArray();
            for (var definitionOrdinal = 1;
                 definitionOrdinal <= WoundMaterializationContract.MaxOwnedEffectDefinitions;
                 definitionOrdinal++)
            {
                var definition = CreateSourceBoundWoundDefinition();
                definition["definitionKey"] =
                    $"wound_definition_{sourceOrdinal:D4}_{definitionOrdinal:D2}";
                definition["components"]![0]!["payload"]!["woundId"] = woundId;
                definition["links"]![0]!["targetId"] = woundId;
                definitions.Add(definition);
            }
            return new EffectSourceExport(
                "mortal_world",
                "wound",
                woundId,
                definitions,
                Materializable: false,
                Active: true,
                SameTurn: sameTurn,
                SourceRef: sameTurn
                    ? $"local_wound_aggregate_{sourceOrdinal:D4}"
                    : null,
                SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal)
                {
                    "active"
                });
        }).ToArray();

    private static WoundSourceGroupAuthority[] CreateAggregateWoundGroups(
        IReadOnlyList<EffectSourceExport> exports,
        bool sameTurn) =>
        exports.Select(export =>
        {
            var definitions = export.Definitions.OfType<JsonObject>()
                .Select(definition => new WoundEffectSourceDefinition(
                    definition["definitionKey"]!.GetValue<string>(),
                    definition))
                .ToArray();
            var roots = definitions.Select((definition, index) =>
                new WoundRootLineageAuthorityRow(
                    sameTurn
                        ? $"application_{export.SourceId}_{index + 1:D2}"
                        : null,
                    sameTurn
                        ? null
                        : $"effect_{export.SourceId}_{index + 1:D2}",
                    definition.DefinitionKey,
                    WoundRootOwnershipDomain.BaseWound)).ToArray();
            return new WoundSourceGroupAuthority(
                new EffectIdentitySourceGroup(
                    export.Realm,
                    export.Kind,
                    export.SourceId),
                new WoundOwnerCoordinate(
                    export.Realm,
                    "player",
                    "player_current",
                    WoundCarrierCatalog.PlayerPath),
                new EffectTargetKey(
                    export.Realm,
                    "player",
                    "player_current"),
                sameTurn,
                export.SourceRef,
                preparedSourceExportFingerprint: null,
                definitions,
                sameTurn ? roots : Array.Empty<WoundRootLineageAuthorityRow>(),
                sameTurn ? Array.Empty<WoundRootLineageAuthorityRow>() : roots);
        }).ToArray();

    private static EffectSourceAuthority Build(
        params EffectSourceExport[] exports) =>
        Build(exports, new HashSet<string>(StringComparer.Ordinal));

    private static EffectSourceAuthority Build(
        IReadOnlyList<EffectSourceExport> exports,
        IReadOnlySet<string> historicalSourceIds) =>
        EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            exports,
            Array.Empty<EffectSourceExport>(),
            historicalSourceIds));

    private static EffectSourceExport Source(
        string kind,
        string sourceId,
        JsonObject? definition = null,
        JsonArray? definitions = null) =>
        new(
            "mortal_world",
            kind,
            sourceId,
            definitions ?? new JsonArray(
                definition ?? EffectMaterializationTestFixture.CreateDefinition()),
            Materializable: true,
            Active: true,
            SameTurn: false,
            SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal)
            {
                "active"
            });
}
