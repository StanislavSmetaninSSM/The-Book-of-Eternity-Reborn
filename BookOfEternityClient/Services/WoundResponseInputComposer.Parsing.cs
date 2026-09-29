using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Services;

internal static partial class WoundResponseInputComposer
{
    private static readonly IReadOnlySet<string> DecisionFields = Set(
        "opportunityRef", "decision", "woundRef", "proposal");
    private static readonly IReadOnlySet<string> DeclineDecisionFields = Set(
        "opportunityRef", "decision");
    private static readonly IReadOnlySet<string> MaterializeDecisionFields = Set(
        "opportunityRef", "decision", "woundRef", "proposal");
    private static readonly IReadOnlySet<string> ProposalFields = Set(
        "classification", "display", "severity", "complications",
        "consequenceDefinitions", "treatment", "recovery");
    private static readonly IReadOnlySet<string> ClassificationFields = Set(
        "woundType", "locationProfile");
    private static readonly IReadOnlySet<string> LocationFields = Set(
        "kind", "readableLocus", "authorityKind", "authorityRef", "affectedSide");
    private static readonly IReadOnlySet<string> RequiredLocationFields = Set(
        "kind", "readableLocus");
    private static readonly IReadOnlySet<string> DisplayFields = Set(
        "name", "description", "visibleSymptoms", "prognosis", "visibility",
        "acquisitionNarration");
    private static readonly IReadOnlySet<string> ComplicationProposalFields = Set(
        "complicationRef", "kind", "state", "displayName",
        "treatmentDifficultyModifier", "visibility");
    private static readonly IReadOnlySet<string> ConsequenceDefinitionFields = Set(
        "definitionRef", "definition", "root");
    private static readonly IReadOnlySet<string> ConsequenceRootFields = Set(
        "ownership", "slots");
    private static readonly IReadOnlySet<string> RootOwnershipFields = Set(
        "kind", "complicationRef");
    private static readonly IReadOnlySet<string> RootSlotFields = Set(
        "profileKey", "readableSummary");

    private sealed record TransitionComposition(
        WoundAcceptedTransitionDraft? Transition,
        WoundPlayerNotification? Notification);

    private sealed record ParsedComplicationProposal(
        string LocalRef,
        string ComplicationId,
        JsonObject Canonical);

    private sealed record ParsedDefinitionProposal(
        string DefinitionRef,
        string LocalEffectRef,
        JsonObject Definition,
        WoundAcceptedRootApplicationDraft? Root,
        IReadOnlyList<WoundAcceptedConsequenceSlotBinding> Slots);

    private static bool TryParseDecision(
        JsonElement element,
        int index,
        ICollection<ValidationIssue> issues,
        out ParsedDecision? decision)
    {
        decision = null;
        var path = $"woundDecisions[{index}]";
        var start = issues.Count;
        if (!TryReadObject(
                element,
                path,
                DecisionFields,
                DeclineDecisionFields,
                issues,
                out var fields))
        {
            return false;
        }

        var opportunityRef = ReadString(fields, "opportunityRef", path, issues);
        var decisionKind = ReadString(fields, "decision", path, issues);
        string? localWoundRef = null;
        JsonElement? proposal = null;
        int? severityRank = null;
        switch (decisionKind)
        {
            case "none":
                ValidateExactFieldSet(
                    fields.Keys,
                    DeclineDecisionFields,
                    path,
                    issues);
                break;
            case "materialize":
                ValidateExactFieldSet(
                    fields.Keys,
                    MaterializeDecisionFields,
                    path,
                    issues);
                localWoundRef = ReadString(fields, "woundRef", path, issues);
                if (!fields.TryGetValue("proposal", out var proposalElement) ||
                    proposalElement.ValueKind != JsonValueKind.Object)
                {
                    Add(
                        issues,
                        path + ".proposal",
                        "wound_response_invalid_field",
                        "strict proposal object",
                        fields.TryGetValue("proposal", out proposalElement)
                            ? proposalElement.ValueKind.ToString()
                            : "missing");
                    break;
                }
                proposal = proposalElement.Clone();
                if (TryReadUniqueProperty(
                        proposalElement,
                        "severity",
                        path + ".proposal",
                        issues,
                        out var severityElement) &&
                    severityElement.ValueKind == JsonValueKind.String &&
                    TrySeverityRank(severityElement.GetString(), out var rank))
                {
                    severityRank = rank;
                }
                else
                {
                    Add(
                        issues,
                        path + ".proposal.severity",
                        "wound_response_invalid_severity",
                        "I, II, III, or IV",
                        severityElement.ValueKind == JsonValueKind.String
                            ? severityElement.GetString() ?? "null"
                            : severityElement.ValueKind.ToString());
                }
                break;
            default:
                Add(
                    issues,
                    path + ".decision",
                    "wound_decision_kind_invalid",
                    "none or materialize",
                    decisionKind ?? "null");
                break;
        }

        if (issues.Count != start)
            return false;
        var raw = JsonNode.Parse(element.GetRawText()) as JsonObject;
        if (raw is null)
            return false;
        decision = new ParsedDecision(
            index,
            raw,
            opportunityRef!,
            decisionKind!,
            localWoundRef,
            proposal,
            severityRank);
        return true;
    }

    private static TransitionComposition TryComposeCreateTransition(
        WoundAcceptedTurnBinding binding,
        WoundOpportunityAuthority opportunity,
        ParsedDecision response,
        WoundOpportunityDecisionAuthority decision,
        string? finalSceneText,
        ICollection<ValidationIssue> issues)
    {
        var path = $"woundDecisions[{response.Index}].proposal";
        var start = issues.Count;
        if (response.Proposal is not { } proposalElement ||
            !TryReadObject(
                proposalElement,
                path,
                ProposalFields,
                ProposalFields,
                issues,
                out var proposal))
        {
            return new TransitionComposition(null, null);
        }
        ValidateNoDuplicateProperties(proposalElement, path, issues);
        if (string.Equals(opportunity.Domain, "physical", StringComparison.Ordinal) &&
            JsonNode.Parse(proposalElement.GetRawText()) is JsonObject mortalProposal)
        {
            var recoveryAuthoring =
                MortalWoundRecoveryAuthoringAuthority.ValidateProposal(
                    mortalProposal,
                    path);
            foreach (var issue in recoveryAuthoring.Issues)
                issues.Add(issue);
        }

        var classification = ReadStrictObject(
            proposal,
            "classification",
            path,
            ClassificationFields,
            ClassificationFields,
            issues);
        var location = classification is null
            ? null
            : ReadStrictObject(
                classification,
                "locationProfile",
                path + ".classification",
                LocationFields,
                RequiredLocationFields,
                issues);
        var display = ReadStrictObject(
            proposal,
            "display",
            path,
            DisplayFields,
            DisplayFields,
            issues);
        var treatment = ReadObjectNode(proposal, "treatment", path, issues);
        var recovery = ReadObjectNode(proposal, "recovery", path, issues);
        if (recovery is not null)
            RemoveNullRecoveryAnchorPlaceholders(recovery);
        var complicationElements = ReadArray(
            proposal,
            "complications",
            path,
            WoundMaterializationContract.MaxComplications,
            issues);
        var definitionElements = ReadArray(
            proposal,
            "consequenceDefinitions",
            path,
            WoundMaterializationContract.MaxOwnedEffectDefinitions,
            issues);

        if (location is not null)
        {
            var locationKind = ReadString(location, "kind", path + ".classification.locationProfile", issues);
            if (locationKind is not null &&
                !opportunity.SafeContext.AllowedLocationKinds.Contains(
                    locationKind,
                    StringComparer.Ordinal))
            {
                Add(
                    issues,
                    path + ".classification.locationProfile.kind",
                    "wound_response_location_kind_forbidden",
                    string.Join(",", opportunity.SafeContext.AllowedLocationKinds),
                    locationKind);
            }
        }

        if (issues.Count != start ||
            classification is null ||
            location is null ||
            display is null ||
            treatment is null ||
            recovery is null ||
            complicationElements is null ||
            definitionElements is null ||
            decision.SelectedSeverityRank is null ||
            decision.LocalWoundRef is null)
        {
            return new TransitionComposition(null, null);
        }

        var localWoundRef = decision.LocalWoundRef;
        var localTransitionRef = CreateLocalIdentifier(
            "wound_transition_ref",
            decision.DecisionFingerprint,
            "create");
        var complications = ParseComplications(
            complicationElements,
            path + ".complications",
            decision.DecisionFingerprint,
            issues);
        var complicationByRef = complications.ToDictionary(
            static value => value.LocalRef,
            StringComparer.Ordinal);
        if (opportunity.WorseningTarget is null)
        {
            treatment = RewriteInitialTreatmentComplicationSelectors(
                treatment,
                path + ".treatment",
                complicationByRef,
                issues);
        }
        var definitions = ParseDefinitions(
            definitionElements,
            path + ".consequenceDefinitions",
            localWoundRef,
            decision.DecisionFingerprint,
            complicationByRef,
            issues);
        if (issues.Count != start)
            return new TransitionComposition(null, null);

        var roots = definitions.Where(static value => value.Root is not null)
            .Select(static value => value.Root!)
            .ToArray();
        var slots = definitions.SelectMany(static value => value.Slots).ToArray();
        var canonicalComplications = complications.Select(value =>
        {
            var rootIds = roots
                .Where(root => string.Equals(
                    root.OwnershipDomain.Kind,
                    "complication",
                    StringComparison.Ordinal) &&
                    string.Equals(
                        root.OwnershipDomain.ComplicationId,
                        value.ComplicationId,
                        StringComparison.Ordinal))
                .Select(static root => root.LocalApplicationRef)
                .ToArray();
            var result = value.Canonical.DeepClone().AsObject();
            result["ownedEffectIds"] = new JsonArray(
                rootIds.Select(static id => (JsonNode)JsonValue.Create(id)!).ToArray());
            return result;
        }).ToArray();
        var ephemeralDefinitions = definitions.Select(value =>
            BindDefinitionToLocalWound(value.Definition, localWoundRef)).ToArray();
        var rootBindings = roots.Select(root =>
        {
            var definition = definitions.Single(value => string.Equals(
                value.LocalEffectRef,
                root.LocalEffectRef,
                StringComparison.Ordinal));
            return (JsonNode)new JsonObject
            {
                ["effectId"] = root.LocalApplicationRef,
                ["definitionKey"] = definition.Definition["definitionKey"]!.GetValue<string>()
            };
        }).ToArray();
        var entries = slots.Select(slot => (JsonNode)new JsonObject
        {
            ["slot"] = slot.Slot,
            ["profileKey"] = slot.ProfileKey,
            ["effectId"] = slot.LocalApplicationRef,
            ["readableSummary"] = slot.ReadableSummary
        }).ToArray();

        var classificationNode = new JsonObject
        {
            ["domain"] = opportunity.Domain,
            ["woundType"] = classification["woundType"]?.DeepClone(),
            ["locationProfile"] = classification["locationProfile"]?.DeepClone()
        };
        var severityRank = decision.SelectedSeverityRank.Value;
        var canonical = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["woundId"] = localWoundRef,
            ["lifecycle"] = "active",
            ["owner"] = SerializeOwner(opportunity.Owner),
            ["origin"] = new JsonObject
            {
                ["eventRef"] = opportunity.EventRef,
                ["sourceKind"] = opportunity.SourceKind,
                ["sourceId"] = opportunity.SourceId,
                ["sourceState"] = opportunity.SourceState,
                ["createdAtTurn"] = binding.Turn,
                ["createdAtCycleId"] = null,
                ["opportunityId"] = opportunity.OpportunityId,
                ["guaranteedTriggerId"] = opportunity.GuaranteedTriggerId,
                ["readableCause"] = opportunity.SafeContext.Cause
            },
            ["classification"] = classificationNode,
            ["display"] = display.DeepClone(),
            ["severity"] = new JsonObject
            {
                ["value"] = Roman(severityRank),
                ["rank"] = severityRank,
                ["maximumAtCreation"] = Roman(opportunity.MaximumSeverityRank),
                ["lastChangeEventRef"] = opportunity.EventRef
            },
            ["care"] = new JsonObject
            {
                ["state"] = "untreated",
                ["stabilizedAtTurn"] = null,
                ["activeCourseId"] = null,
                ["lastAttemptId"] = null
            },
            ["complications"] = new JsonArray(
                canonicalComplications.Select(static value => (JsonNode)value).ToArray()),
            ["consequences"] = new JsonObject
            {
                ["slotBudget"] = severityRank,
                ["slotsUsed"] = slots.Length,
                ["ownedEffectSources"] = new JsonObject
                {
                    ["definitions"] = new JsonArray(
                        ephemeralDefinitions.Select(static value => (JsonNode)value).ToArray()),
                    ["rootBindings"] = new JsonArray(rootBindings)
                },
                ["entries"] = new JsonArray(entries)
            },
            ["treatment"] = treatment.DeepClone(),
            ["recovery"] = recovery.DeepClone(),
            ["relations"] = new JsonObject
            {
                ["priorWoundId"] = null,
                ["legacyRefs"] = new JsonArray(),
                ["independentEffectRefs"] = new JsonArray()
            },
            ["lastTransition"] = new JsonObject
            {
                ["transitionId"] = localTransitionRef,
                ["ordinal"] = 1,
                ["turn"] = binding.Turn,
                ["kind"] = "create"
            }
        };
        var parsed = WoundMaterializationContract.Parse(canonical.ToJsonString(), path);
        if (!parsed.IsValid || parsed.Wound is null)
        {
            foreach (var issue in parsed.Issues)
                issues.Add(WoundAcceptedTurnData.CloneIssue(issue));
            return new TransitionComposition(null, null);
        }

        var proposed = parsed.Wound with
        {
            Complications = parsed.Wound.Complications.Select(static value => value with
            {
                OwnedEffectIds = Array.Empty<string>()
            }).ToArray(),
            Consequences = new WoundConsequences(
                parsed.Wound.Consequences.SlotBudget,
                0,
                Array.Empty<WoundConsequenceEntry>())
            {
                OwnedEffectSources = WoundOwnedEffectSources.Empty
            }
        };
        var transition = new WoundAcceptedTransitionDraft(
            "create",
            decision.OperationKey,
            localWoundRef,
            localTransitionRef,
            opportunity.OpportunityId,
            $"Получена рана «{proposed.Display.Name}» ({proposed.Severity.Value}).",
            proposed,
            definitions.Select(static value => new WoundAcceptedEffectDefinitionDraft(
                value.LocalEffectRef,
                value.Definition)).ToArray(),
            roots,
            slots);
        var output = WoundPlayerNotification.Compose(new WoundAcquisitionOutputRequest(
            localWoundRef,
            proposed,
            new WoundAcquisitionNarrationClaim(
                localWoundRef,
                opportunity.EventRef,
                opportunity.Domain,
                proposed.Display.Name,
                severityRank,
                proposed.Display.AcquisitionNarration),
            finalSceneText ?? string.Empty));
        foreach (var issue in output.Issues)
            issues.Add(WoundAcceptedTurnData.CloneIssue(issue));
        return output.Success
            ? new TransitionComposition(transition, output.Notification)
            : new TransitionComposition(null, null);
    }

    /// <summary>
    /// Composes a worsening from the sealed source wound while retaining client-owned recovery anchors.
    /// </summary>
    /// <param name="binding">
    /// The accepted-turn binding selecting the transition turn.
    /// </param>
    /// <param name="opportunity">
    /// The sealed opportunity carrying the exact worsening target before-image.
    /// </param>
    /// <param name="response">
    /// The parsed materialization decision and complete worsening proposal.
    /// </param>
    /// <param name="decision">
    /// The sealed decision supplying the operation and local transition identity.
    /// </param>
    /// <param name="finalSceneText">
    /// The final scene text checked against the worsening narration, or <see langword="null"/> for empty text.
    /// </param>
    /// <param name="issues">
    /// The collection receiving proposal, preservation and narration conflicts.
    /// </param>
    /// <returns>
    /// The composed transition and notification, or an empty composition when validation fails.
    /// </returns>
    private static TransitionComposition TryComposeWorsenTransition(
        WoundAcceptedTurnBinding binding,
        WoundOpportunityAuthority opportunity,
        ParsedDecision response,
        WoundOpportunityDecisionAuthority decision,
        string? finalSceneText,
        ICollection<ValidationIssue> issues)
    {
        var start = issues.Count;
        var composed = TryComposeCreateTransition(
            binding,
            opportunity,
            response,
            decision,
            finalSceneText,
            issues);
        if (issues.Count != start ||
            composed.Transition is not { } createDraft ||
            opportunity.WorseningTarget is not { } target)
        {
            return new TransitionComposition(null, null);
        }

        var before = target.Wound;
        var proposed = createDraft.ProposedAfter;
        var path = $"woundDecisions[{response.Index}].proposal";
        if (before.Classification != proposed.Classification)
        {
            Add(
                issues,
                path + ".classification",
                "wound_response_worsening_classification_changed",
                "the existing wound classification unchanged",
                proposed.Classification.ToString());
        }
        if (!SameCanonicalSection(before, proposed, "treatment"))
        {
            Add(
                issues,
                path + ".treatment",
                "wound_response_worsening_treatment_changed",
                "the existing treatment model unchanged",
                "changed treatment model");
        }

        var complicationIds = new Dictionary<string, string>(StringComparer.Ordinal);
        if (before.Complications.Count != proposed.Complications.Count)
        {
            Add(
                issues,
                path + ".complications",
                "wound_response_worsening_complications_changed",
                "the exact existing complication set",
                $"{before.Complications.Count}->{proposed.Complications.Count}");
        }
        else
        {
            for (var index = 0; index < before.Complications.Count; index++)
            {
                var prior = before.Complications[index];
                var candidate = proposed.Complications[index];
                if (!SameComplicationProposal(prior, candidate) ||
                    !complicationIds.TryAdd(
                        candidate.ComplicationId,
                        prior.ComplicationId))
                {
                    Add(
                        issues,
                        $"{path}.complications[{index}]",
                        "wound_response_worsening_complications_changed",
                        "the same ordered complication facts under response-local refs",
                        candidate.ComplicationId);
                }
            }
        }
        if (proposed.Recovery.CurrentStepProgress != 0)
        {
            Add(
                issues,
                path + ".recovery.currentStepProgress",
                "wound_response_worsening_progress_not_reset",
                "0",
                proposed.Recovery.CurrentStepProgress.ToString(
                    CultureInfo.InvariantCulture));
        }
        var expectedBlockers = WoundTransitionReducer.DeriveWorseningBlockers(before);
        if (!proposed.Recovery.Blockers.SequenceEqual(expectedBlockers, StringComparer.Ordinal))
        {
            Add(issues, path + ".recovery.blockers",
                "wound_response_worsening_blockers_changed",
                "the retained ordered blockers and only the derived stabilization-condition reentry",
                "changed recovery blockers");
        }
        if (issues.Count != start)
            return new TransitionComposition(null, null);

        var roots = createDraft.RootApplications.Select(root =>
        {
            if (!string.Equals(
                    root.OwnershipDomain.Kind,
                    "complication",
                    StringComparison.Ordinal))
            {
                return root;
            }
            if (root.OwnershipDomain.ComplicationId is null ||
                !complicationIds.TryGetValue(
                    root.OwnershipDomain.ComplicationId,
                    out var permanentComplicationId))
            {
                throw new InvalidOperationException(
                    "Worsening root has no exact existing complication binding.");
            }
            return root with
            {
                OwnershipDomain = WoundRootOwnershipDomain.ForComplication(
                    permanentComplicationId)
            };
        }).ToArray();
        var localTransitionRef = CreateLocalIdentifier(
            "wound_transition_ref",
            decision.DecisionFingerprint,
            "worsen");
        var after = before with
        {
            Display = proposed.Display,
            Severity = proposed.Severity with
            {
                MaximumAtCreation = before.Severity.MaximumAtCreation,
                LastChangeEventRef = opportunity.EventRef
            },
            Care = WoundTransitionReducer.DeriveWorseningCare(before),
            Complications = before.Complications.Select(static value => value with
            {
                OwnedEffectIds = Array.Empty<string>()
            }).ToArray(),
            Consequences = proposed.Consequences,
            Recovery = proposed.Recovery with
            {
                Blockers = expectedBlockers,
                RecoveryAnchor = before.Recovery.RecoveryAnchor,
                DeteriorationAnchor = before.Recovery.DeteriorationAnchor
            },
            LastTransition = new WoundLastTransition(
                localTransitionRef,
                checked(before.LastTransition.Ordinal + 1),
                binding.Turn,
                "worsen")
        };
        var transition = new WoundAcceptedTransitionDraft(
            "worsen",
            decision.OperationKey,
            createDraft.LocalWoundRef,
            localTransitionRef,
            opportunity.OpportunityId,
            $"Рана «{after.Display.Name}» ухудшилась до {after.Severity.Value}.",
            after,
            createDraft.EffectDefinitions,
            roots,
            createDraft.SlotBindings);
        var output = WoundPlayerNotification.ComposeWorsening(
            new WoundWorseningOutputRequest(
                createDraft.LocalWoundRef,
                opportunity.EventRef,
                after,
                new WoundAcquisitionNarrationClaim(
                    createDraft.LocalWoundRef,
                    opportunity.EventRef,
                    opportunity.Domain,
                    after.Display.Name,
                    after.Severity.Rank,
                    after.Display.AcquisitionNarration),
                finalSceneText ?? string.Empty));
        foreach (var issue in output.Issues)
            issues.Add(WoundAcceptedTurnData.CloneIssue(issue));
        return output.Success
            ? new TransitionComposition(transition, output.Notification)
            : new TransitionComposition(null, null);
    }

    private static bool SameCanonicalSection(
        WoundMaterializationEnvelope left,
        WoundMaterializationEnvelope right,
        string section)
    {
        var leftRoot = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(left))!.AsObject();
        var rightRoot = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(right))!.AsObject();
        return JsonNode.DeepEquals(leftRoot[section], rightRoot[section]);
    }

    private static bool SameComplicationProposal(
        WoundComplication prior,
        WoundComplication candidate) =>
        string.Equals(prior.Kind, candidate.Kind, StringComparison.Ordinal) &&
        string.Equals(prior.State, candidate.State, StringComparison.Ordinal) &&
        string.Equals(
            prior.DisplayName,
            candidate.DisplayName,
            StringComparison.Ordinal) &&
        prior.TreatmentDifficultyModifier ==
            candidate.TreatmentDifficultyModifier &&
        string.Equals(
            prior.Visibility,
            candidate.Visibility,
            StringComparison.Ordinal);

    private static IReadOnlyList<ParsedComplicationProposal> ParseComplications(
        IReadOnlyList<JsonElement> elements,
        string path,
        string decisionFingerprint,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<ParsedComplicationProposal>(elements.Count);
        var localRefs = new List<string>();
        for (var index = 0; index < elements.Count; index++)
        {
            var itemPath = $"{path}[{index}]";
            if (!TryReadObject(
                    elements[index],
                    itemPath,
                    ComplicationProposalFields,
                    ComplicationProposalFields,
                    issues,
                    out var fields))
            {
                continue;
            }
            var localRef = ReadString(fields, "complicationRef", itemPath, issues);
            if (!ResourceMaterializationContract.IsExactIdentifier(localRef))
            {
                Add(
                    issues,
                    itemPath + ".complicationRef",
                    "wound_response_invalid_local_reference",
                    "one exact response-local complicationRef",
                    localRef ?? "null");
                continue;
            }
            localRefs.Add(localRef!);
            var canonical = new JsonObject
            {
                ["complicationId"] = CreateLocalIdentifier(
                    "wound_complication",
                    decisionFingerprint,
                    localRef),
                ["kind"] = ToNode(fields["kind"]),
                ["state"] = ToNode(fields["state"]),
                ["displayName"] = ToNode(fields["displayName"]),
                ["treatmentDifficultyModifier"] = ToNode(
                    fields["treatmentDifficultyModifier"]),
                ["ownedEffectIds"] = new JsonArray(),
                ["visibility"] = ToNode(fields["visibility"])
            };
            result.Add(new ParsedComplicationProposal(
                localRef!,
                canonical["complicationId"]!.GetValue<string>(),
                canonical));
        }
        if (!ExactAndConfusableUnique(localRefs))
        {
            Add(
                issues,
                path,
                "wound_response_duplicate_local_reference",
                "exact and confusable-unique complicationRef values",
                "duplicate complicationRef");
        }
        return result;
    }

    private static IReadOnlyList<ParsedDefinitionProposal> ParseDefinitions(
        IReadOnlyList<JsonElement> elements,
        string path,
        string localWoundRef,
        string decisionFingerprint,
        IReadOnlyDictionary<string, ParsedComplicationProposal> complicationByRef,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<ParsedDefinitionProposal>(elements.Count);
        var definitionRefs = new List<string>();
        var definitionKeys = new List<string>();
        var nextSlot = 1;
        var rootCount = 0;
        for (var index = 0; index < elements.Count; index++)
        {
            var itemPath = $"{path}[{index}]";
            if (!TryReadObject(
                    elements[index],
                    itemPath,
                    ConsequenceDefinitionFields,
                    ConsequenceDefinitionFields,
                    issues,
                    out var fields))
            {
                continue;
            }
            var definitionRef = ReadString(fields, "definitionRef", itemPath, issues);
            if (!ResourceMaterializationContract.IsExactIdentifier(definitionRef) ||
                fields["definition"].ValueKind != JsonValueKind.Object)
            {
                Add(
                    issues,
                    itemPath,
                    "wound_response_invalid_definition",
                    "exact definitionRef and strict #1535 definition object",
                    "malformed definition wrapper");
                continue;
            }
            definitionRefs.Add(definitionRef!);
            var definition = ConvertProposalDefinition(fields["definition"], localWoundRef, itemPath, issues,
                out var definitionKey);
            // A valid key participates in aggregate duplicate diagnostics even
            // when the definition is rejected for forbidden source links.
            if (definitionKey is not null)
                definitionKeys.Add(definitionKey);
            if (definition is null)
                continue;

            var localEffectRef = CreateLocalIdentifier(
                "wound_definition_ref",
                decisionFingerprint,
                definitionRef);
            WoundAcceptedRootApplicationDraft? root = null;
            var slots = new List<WoundAcceptedConsequenceSlotBinding>();
            var rootElement = fields["root"];
            if (rootElement.ValueKind != JsonValueKind.Null)
            {
                rootCount++;
                if (!TryReadObject(
                        rootElement,
                        itemPath + ".root",
                        ConsequenceRootFields,
                        ConsequenceRootFields,
                        issues,
                        out var rootFields))
                {
                    continue;
                }
                var ownership = ParseRootOwnership(
                    rootFields["ownership"],
                    itemPath + ".root.ownership",
                    complicationByRef,
                    issues);
                var slotElements = ReadArray(
                    rootFields,
                    "slots",
                    itemPath + ".root",
                    WoundMaterializationContract.MaxConsequences,
                    issues);
                if (ownership is null || slotElements is null)
                    continue;
                var localApplicationRef = CreateLocalIdentifier(
                    "wound_application_ref",
                    decisionFingerprint,
                    definitionRef);
                root = new WoundAcceptedRootApplicationDraft(
                    localApplicationRef,
                    localEffectRef,
                    CreateLocalIdentifier(
                        "wound_root_operation",
                        decisionFingerprint,
                        definitionRef),
                    ownership);
                for (var slotIndex = 0; slotIndex < slotElements.Count; slotIndex++)
                {
                    var slotPath = $"{itemPath}.root.slots[{slotIndex}]";
                    if (!TryReadObject(
                            slotElements[slotIndex],
                            slotPath,
                            RootSlotFields,
                            RootSlotFields,
                            issues,
                            out var slotFields))
                    {
                        continue;
                    }
                    var profile = ReadString(slotFields, "profileKey", slotPath, issues);
                    var summary = ReadString(slotFields, "readableSummary", slotPath, issues);
                    if (profile is not null && summary is not null)
                    {
                        foreach (var slot in ConvertProposalRootSlots(
                                     ImmutableArray.Create(new WoundConsequenceSlotProposalDraft(profile, summary)), ref nextSlot))
                            slots.Add(new WoundAcceptedConsequenceSlotBinding(
                                slot.Slot, slot.ProfileKey, localApplicationRef, slot.ReadableSummary));
                    }
                }
            }
            result.Add(new ParsedDefinitionProposal(
                definitionRef!,
                localEffectRef,
                definition,
                root,
                slots));
        }

        if (!ExactAndConfusableUnique(definitionRefs) ||
            !ExactAndConfusableUnique(definitionKeys))
        {
            Add(
                issues,
                path,
                "wound_response_duplicate_definition",
                "exact and confusable-unique definitionRef and definitionKey values",
                "duplicate definition coordinate");
        }
        if (rootCount > WoundMaterializationContract.MaxOwnedEffectRootBindings)
        {
            Add(
                issues,
                path,
                "wound_response_root_limit_exceeded",
                $"at most {WoundMaterializationContract.MaxOwnedEffectRootBindings} roots",
                rootCount.ToString(CultureInfo.InvariantCulture));
        }
        return result;
    }

    private static JsonObject RewriteInitialTreatmentComplicationSelectors(
        JsonObject treatment,
        string path,
        IReadOnlyDictionary<string, ParsedComplicationProposal> complicationByRef,
        ICollection<ValidationIssue> issues)
    {
        var rewritten = treatment.DeepClone().AsObject();
        if (rewritten["routes"] is not JsonArray routes)
            return rewritten;

        for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
        {
            if (routes[routeIndex] is not JsonObject route)
                continue;
            var routePath = $"{path}.routes[{routeIndex}]";
            if (route["outcomes"] is JsonArray outcomes)
            {
                for (var outcomeIndex = 0; outcomeIndex < outcomes.Count; outcomeIndex++)
                {
                    if (outcomes[outcomeIndex] is JsonObject outcome &&
                        outcome["result"] is JsonArray result)
                    {
                        RewriteInitialResultComplicationSelectors(
                            result,
                            $"{routePath}.outcomes[{outcomeIndex}].result",
                            complicationByRef,
                            issues);
                    }
                }
            }
            if (route["interruption"] is JsonObject interruption &&
                interruption["result"] is JsonArray interruptionResult)
            {
                RewriteInitialResultComplicationSelectors(
                    interruptionResult,
                    routePath + ".interruption.result",
                    complicationByRef,
                    issues);
            }
        }
        return rewritten;
    }

    private static void RewriteInitialResultComplicationSelectors(
        JsonArray result,
        string path,
        IReadOnlyDictionary<string, ParsedComplicationProposal> complicationByRef,
        ICollection<ValidationIssue> issues)
    {
        for (var operationIndex = 0; operationIndex < result.Count; operationIndex++)
        {
            if (result[operationIndex] is not JsonObject operation ||
                operation["kind"] is not JsonValue kindNode ||
                !kindNode.TryGetValue<string>(out var kind) ||
                !string.Equals(kind, "remove_complication", StringComparison.Ordinal))
            {
                continue;
            }
            var operationPath = $"{path}[{operationIndex}]";
            if (operation.ContainsKey("complicationId"))
            {
                Add(
                    issues,
                    operationPath + ".complicationId",
                    "wound_response_unknown_field",
                    "response-local complicationRef; permanent IDs are client-owned",
                    "complicationId");
                continue;
            }
            if (operation["complicationRef"] is not JsonValue refNode ||
                !refNode.TryGetValue<string>(out var complicationRef) ||
                !ResourceMaterializationContract.IsExactIdentifier(complicationRef) ||
                !complicationByRef.TryGetValue(complicationRef, out var complication))
            {
                Add(
                    issues,
                    operationPath + ".complicationRef",
                    "wound_response_invalid_local_reference",
                    "one exact complicationRef from this proposal",
                    operation["complicationRef"]?.ToJsonString() ?? "missing");
                continue;
            }
            operation.Remove("complicationRef");
            operation["complicationId"] = complication.ComplicationId;
        }
    }

    private static WoundRootOwnershipDomain? ParseRootOwnership(
        JsonElement element,
        string path,
        IReadOnlyDictionary<string, ParsedComplicationProposal> complicationByRef,
        ICollection<ValidationIssue> issues)
    {
        if (!TryReadObject(
                element,
                path,
                RootOwnershipFields,
                RootOwnershipFields,
                issues,
                out var fields))
        {
            return null;
        }
        var kind = ReadString(fields, "kind", path, issues);
        var complication = fields["complicationRef"];
        if (string.Equals(kind, "base_wound", StringComparison.Ordinal) &&
            complication.ValueKind == JsonValueKind.Null)
        {
            return WoundRootOwnershipDomain.BaseWound;
        }
        if (string.Equals(kind, "complication", StringComparison.Ordinal) &&
            complication.ValueKind == JsonValueKind.String &&
            complicationByRef.TryGetValue(
                complication.GetString() ?? string.Empty,
                out var resolved))
        {
            return WoundRootOwnershipDomain.ForComplication(resolved.ComplicationId);
        }
        Add(
            issues,
            path,
            "wound_response_root_ownership_invalid",
            "base_wound/null or complication/exact complicationRef",
            element.GetRawText());
        return null;
    }


    private static void RemoveNullRecoveryAnchorPlaceholders(JsonObject recovery)
    {
        foreach (var field in new[] { "recoveryAnchor", "deteriorationAnchor" })
        {
            if (recovery.ContainsKey(field) && recovery[field] is null)
                recovery.Remove(field);
        }
    }

    private static JsonObject? ReadStrictObject(
        IReadOnlyDictionary<string, JsonElement> parent,
        string property,
        string parentPath,
        IReadOnlySet<string> allowed,
        IReadOnlySet<string> required,
        ICollection<ValidationIssue> issues)
    {
        if (!parent.TryGetValue(property, out var value) ||
            !TryReadObject(
                value,
                parentPath + "." + property,
                allowed,
                required,
                issues,
                out _))
        {
            return null;
        }
        return JsonNode.Parse(value.GetRawText())!.AsObject();
    }

    private static JsonObject? ReadStrictObject(
        JsonObject parent,
        string property,
        string parentPath,
        IReadOnlySet<string> allowed,
        IReadOnlySet<string> required,
        ICollection<ValidationIssue> issues)
    {
        if (!parent.TryGetPropertyValue(property, out var node) ||
            node is not JsonObject value)
        {
            Add(
                issues,
                parentPath + "." + property,
                "wound_response_invalid_field",
                "strict object",
                node?.GetType().Name ?? "missing");
            return null;
        }
        var element = JsonSerializer.SerializeToElement(value);
        return TryReadObject(
            element,
            parentPath + "." + property,
            allowed,
            required,
            issues,
            out _)
            ? value.DeepClone().AsObject()
            : null;
    }

    private static JsonObject? ReadObjectNode(
        IReadOnlyDictionary<string, JsonElement> parent,
        string property,
        string parentPath,
        ICollection<ValidationIssue> issues)
    {
        if (!parent.TryGetValue(property, out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                parentPath + "." + property,
                "wound_response_invalid_field",
                "strict object",
                parent.TryGetValue(property, out value)
                    ? value.ValueKind.ToString()
                    : "missing");
            return null;
        }
        return JsonNode.Parse(value.GetRawText())!.AsObject();
    }

    private static IReadOnlyList<JsonElement>? ReadArray(
        IReadOnlyDictionary<string, JsonElement> parent,
        string property,
        string parentPath,
        int maximum,
        ICollection<ValidationIssue> issues)
    {
        if (!parent.TryGetValue(property, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            Add(
                issues,
                parentPath + "." + property,
                "wound_response_invalid_field",
                "array",
                parent.TryGetValue(property, out value)
                    ? value.ValueKind.ToString()
                    : "missing");
            return null;
        }
        if (value.GetArrayLength() > maximum)
        {
            Add(
                issues,
                parentPath + "." + property,
                "wound_response_collection_limit_exceeded",
                $"at most {maximum} entries",
                value.GetArrayLength().ToString(CultureInfo.InvariantCulture));
        }
        return value.EnumerateArray().Take(maximum).Select(static item => item.Clone())
            .ToArray();
    }

    private static bool TryReadObject(
        JsonElement element,
        string path,
        IReadOnlySet<string> allowed,
        IReadOnlySet<string> required,
        ICollection<ValidationIssue> issues,
        out IReadOnlyDictionary<string, JsonElement> fields)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        fields = result;
        if (element.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path,
                "wound_response_invalid_field",
                "strict object",
                element.ValueKind.ToString());
            return false;
        }
        var start = issues.Count;
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                Add(
                    issues,
                    path + "." + property.Name,
                    "wound_response_unknown_field",
                    string.Join(",", allowed.OrderBy(static value => value, StringComparer.Ordinal)),
                    property.Name);
            }
            if (!result.TryAdd(property.Name, property.Value.Clone()))
            {
                Add(
                    issues,
                    path + "." + property.Name,
                    "wound_response_duplicate_field",
                    "one exact property",
                    property.Name);
            }
        }
        foreach (var property in required)
        {
            if (!result.ContainsKey(property))
            {
                Add(
                    issues,
                    path + "." + property,
                    "wound_response_missing_field",
                    "required property",
                    "missing");
            }
        }
        return issues.Count == start;
    }

    private static void ValidateExactFieldSet(
        IEnumerable<string> actual,
        IReadOnlySet<string> expected,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var set = actual.ToHashSet(StringComparer.Ordinal);
        foreach (var field in set.Where(field => !expected.Contains(field)))
        {
            Add(
                issues,
                path + "." + field,
                "wound_response_unexpected_decision_field",
                string.Join(",", expected.OrderBy(static value => value, StringComparer.Ordinal)),
                field);
        }
        foreach (var field in expected.Where(field => !set.Contains(field)))
        {
            Add(
                issues,
                path + "." + field,
                "wound_response_missing_field",
                "required decision property",
                "missing");
        }
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, JsonElement> fields,
        string property,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (!fields.TryGetValue(property, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()) ||
            !string.Equals(value.GetString(), value.GetString()!.Trim(), StringComparison.Ordinal))
        {
            Add(
                issues,
                path + "." + property,
                "wound_response_invalid_field",
                "non-empty exact string",
                fields.TryGetValue(property, out value)
                    ? value.GetRawText()
                    : "missing");
            return null;
        }
        return value.GetString();
    }

    private static string? ReadString(
        JsonObject fields,
        string property,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (!fields.TryGetPropertyValue(property, out var node) ||
            node is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text) ||
            !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            Add(
                issues,
                path + "." + property,
                "wound_response_invalid_field",
                "non-empty exact string",
                node?.ToJsonString() ?? "missing");
            return null;
        }
        return text;
    }

    private static bool TryReadUniqueProperty(
        JsonElement element,
        string propertyName,
        string path,
        ICollection<ValidationIssue> issues,
        out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        var matches = element.EnumerateObject()
            .Where(property => string.Equals(
                property.Name,
                propertyName,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            if (matches.Length > 1)
            {
                Add(
                    issues,
                    path + "." + propertyName,
                    "wound_response_duplicate_field",
                    "one exact property",
                    matches.Length.ToString(CultureInfo.InvariantCulture));
            }
            return false;
        }
        value = matches[0].Value.Clone();
        return true;
    }

    private static void ValidateNoDuplicateProperties(
        JsonElement element,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    Add(
                        issues,
                        path + "." + property.Name,
                        "wound_response_duplicate_field",
                        "one exact property",
                        property.Name);
                }
                ValidateNoDuplicateProperties(
                    property.Value,
                    path + "." + property.Name,
                    issues);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                ValidateNoDuplicateProperties(item, $"{path}[{index}]", issues);
                index++;
            }
        }
    }

    internal static string CreateLocalIdentifier(
        string prefix,
        params string?[] fields) => prefix + "_" +
        WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.wound.response_local_coordinate",
                "1",
                prefix
            }.Concat(fields))["sha256:".Length..];

    private static JsonNode? ToNode(JsonElement value) =>
        JsonNode.Parse(value.GetRawText());

    private static bool TrySeverityRank(string? value, out int rank)
    {
        rank = value switch
        {
            "I" => 1,
            "II" => 2,
            "III" => 3,
            "IV" => 4,
            _ => 0
        };
        return rank != 0;
    }

    private static string Roman(int rank) => rank switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        _ => throw new ArgumentOutOfRangeException(nameof(rank))
    };

    private static HashSet<string> Set(params string[] values) =>
        new(values, StringComparer.Ordinal);
}
