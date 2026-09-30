namespace BookOfEternityClient.Tests;

internal static class FastPlacementTestInventories
{
    internal const string MortalItemConsumptionPlannerTestsFast = """
        Fact|Plan_PartialStackPreservesIdentityReceiptAndCarrier
        Fact|Plan_FullStackConsumesIdentityClearsEquipmentAndReturnsTerminalOwner
        Theory|Plan_FullNpcStackClearsOnlyExactSourceEquipmentAndPreservesNpcSiblings|bool:false
        Theory|Plan_FullNpcStackClearsOnlyExactSourceEquipmentAndPreservesNpcSiblings|bool:true
        Theory|Plan_FullNpcStackRejectsCrossCarrierOrConfusableEquipmentReference|bool:false
        Theory|Plan_FullNpcStackRejectsCrossCarrierOrConfusableEquipmentReference|bool:true
        Fact|FinalBaseline_PlayerItemAllowsIdenticalPermanentNpcCrossSectionMirror
        Fact|FinalBaseline_PlayerItemAllowsSameTurnNewNpcAbsentFromBackup
        Theory|FinalBaseline_PlayerItemAllowsExactExistingNpcSectionMove|string:UpdateNPCs|string:NPCsInScene
        Theory|FinalBaseline_PlayerItemAllowsExactExistingNpcSectionMove|string:NPCsInScene|string:UpdateNPCs
        Theory|FinalBaseline_RejectsInvalidNpcDuplicateTopology|string:divergent_cross_section
        Theory|FinalBaseline_RejectsInvalidNpcDuplicateTopology|string:same_section_duplicate
        Theory|FinalBaseline_RejectsInvalidNpcDuplicateTopology|string:confusable_cross_section
        Theory|FinalBaseline_RejectsInvalidNpcDuplicateTopology|string:homoglyph_cross_section
        Theory|FinalBaseline_RejectsNpcActorSetChangeWithoutCreationOrDeleteAuthority|string:backup_only_existing
        Theory|FinalBaseline_RejectsNpcActorSetChangeWithoutCreationOrDeleteAuthority|string:current_only_permanent
        Theory|MortalNpcCommandIndex_RejectsInvalidNpcDuplicateTopology|string:divergent_cross_section
        Theory|MortalNpcCommandIndex_RejectsInvalidNpcDuplicateTopology|string:same_section_duplicate
        Theory|MortalNpcCommandIndex_RejectsInvalidNpcDuplicateTopology|string:confusable_cross_section
        Theory|MortalNpcCommandIndex_RejectsInvalidNpcDuplicateTopology|string:conflicting_alias
        Theory|TransferPlanner_UpdatesEveryIdenticalPermanentNpcMirror|bool:true|bool:false
        Theory|TransferPlanner_UpdatesEveryIdenticalPermanentNpcMirror|bool:true|bool:true
        Theory|TransferPlanner_UpdatesEveryIdenticalPermanentNpcMirror|bool:false|bool:false
        Theory|TransferPlanner_UpdatesEveryIdenticalPermanentNpcMirror|bool:false|bool:true
        Theory|Plan_FullStackRejectsContainerQuestBondOrOtherCompanionWithoutAfterImages|string:container
        Theory|Plan_FullStackRejectsContainerQuestBondOrOtherCompanionWithoutAfterImages|string:quest
        Theory|Plan_FullStackRejectsContainerQuestBondOrOtherCompanionWithoutAfterImages|string:bond
        Theory|Plan_FullStackRejectsContainerQuestBondOrOtherCompanionWithoutAfterImages|string:other
        Fact|Plan_RepeatedClaimsEmitSequentialTransitionsInFinalizationOrder
        Fact|Plan_CommonFinalizationOrdinalGapsRemainInStrictOrder
        Theory|Plan_DuplicateOrDecreasingCommonFinalizationOrdinalsReject|literal:2|literal:2
        Theory|Plan_DuplicateOrDecreasingCommonFinalizationOrdinalsReject|literal:3|literal:2
        Fact|Plan_ResourceBearingPartialScalesMaximumAndCurrentExactly
        Fact|Plan_SuspendedLiveItemResourceScalesExactlyAndRemainsActionable
        Theory|Plan_InexactOrNonInstanceFixedCapacityRejectsWithoutAfterImages|string:inexact_quantum
        Theory|Plan_InexactOrNonInstanceFixedCapacityRejectsWithoutAfterImages|string:non_instance_fixed
        Fact|Plan_IsWriteFreeAndDeterministicAcrossDetachedInputs
        Fact|Plan_InvalidIssuesAreDeeplyDetachedFromCallerOwnedRepairContext
        Fact|Plan_CompanionRootPathCollisionRejectsWithoutThrowingOrAfterImages
        Fact|Plan_FingerprintBindsCompleteIssuePayloadAndCapacityReceiptId
        """;

    internal const string MortalItemConsumptionPlannerTestsIntegration = """
        Theory|Project_SameTurnCreateAndTransferUseSnapshotDeterministicReceiptAndTransitionIds|bool:true
        Theory|Project_SameTurnCreateAndTransferUseSnapshotDeterministicReceiptAndTransitionIds|bool:false
        Fact|AcceptedCreationIdentityIds_UseFiveCollectorProductionOrderInsteadOfCreationRefOrder
        Fact|RouteCatalog_RejectsDirectRawCreationInPermanentNpcMirrorAfterCatalogCoalescing
        """;

    internal const string ShiningAbodeTradeAndForgeStateTestsFast = """
        Fact|GetTradeStockItemCount_DormantTradeIgnoresProvisionAndResourceSupport
        Fact|TryQuoteForgeAction_WithForgeSupportAppliesDiscount
        Fact|ForgeBlessingEntitlements_MakeReshapeFreeAndConsumeTheFlag
        Theory|ConsumeForgeEntitlements_ClosesOnlyAfterBoundResourceAllocationIsExhausted|literal:1|syntax:ShiningBlessingEffectState.RelicStatusPendingEntitlement
        Theory|ConsumeForgeEntitlements_ClosesOnlyAfterBoundResourceAllocationIsExhausted|literal:0|syntax:ShiningBlessingEffectState.GenericStatusConsumed
        Fact|TryApplyForgeAction_DebitsInkFeathersAndLightSparks
        Fact|TryQuoteForgeAction_UpliftCountsOnlyObjectAddedProperties
        Fact|TryApplyForgeAction_UpliftSynchronizesQualityAndRarityAliases
        """;

    internal const string ShiningAbodeTradeAndForgeStateTestsIntegration = """
        Fact|ConsumeRelicRerollAsync_DecrementsPendingBlessingPool
        """;

    internal const string GmWorkerAuditLogTestsFast = """
        Fact|SharedAuditEventIdGenerator_DeterministicInputsProduceReadableStableId
        Fact|SharedAuditEventIdGenerator_ConcurrentCallsProduceUniqueReadableIds
        """;

    internal const string GmWorkerAuditLogTestsIntegration = """
        Fact|AppendEventAsync_AppendsDurableJsonLineAuditEvents
        Fact|TypedAuditHelpers_RecordDispatchProposalAndApplyEvents
        Fact|AppendEventAsync_ConcurrentWritersPreserveEveryEvent
        Theory|SelfAcquiringAppendEntryPoints_SerializeBeforeCanonicalFileLock|syntax:AuditAppendEntryPoint.BestEffort
        Theory|SelfAcquiringAppendEntryPoints_SerializeBeforeCanonicalFileLock|syntax:AuditAppendEntryPoint.CurrentSession
        Theory|SelfAcquiringAppendEntryPoints_SerializeBeforeCanonicalFileLock|syntax:AuditAppendEntryPoint.RequiredOnce
        Theory|CancellationAwareAppendEntryPoints_CancelWhileWaitingForAdmission|syntax:AuditAppendEntryPoint.CurrentSession
        Theory|CancellationAwareAppendEntryPoints_CancelWhileWaitingForAdmission|syntax:AuditAppendEntryPoint.RequiredOnce
        Fact|AppendEventIfCurrentSessionAsync_StaleGeneration_DropsAuditEvent
        Fact|AppendRequiredEventOnceIfCurrentSessionAsync_RepeatedEventIsIdempotent
        Fact|AppendRequiredEventOnceIfCurrentSessionAsync_WriteFailurePropagatesForRetry
        """;

}
