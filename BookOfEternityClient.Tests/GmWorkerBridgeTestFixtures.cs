using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

internal static class GmWorkerBridgeTestFixtures
{
    public static WorkerBridgeProfile ValidationRepairCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateValidationRepairCodexTemplate() with { Enabled = true };

    public static WorkerBridgeProfile NarrativeDraftCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateNarrativeDraftCodexTemplate() with { Enabled = true };

    public static WorkerBridgeProfile AnalysisCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateAnalysisCodexTemplate() with { Enabled = true };

    public static WorkerBridgeProfile InventoryContentCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateInventoryContentCodexTemplate() with { Enabled = true };

    public static WorkerBridgeProfile SkillContentCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateSkillContentCodexTemplate() with { Enabled = true };

    public static WorkerBridgeProfile NpcContentCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateNpcContentCodexTemplate() with { Enabled = true };

    public static WorkerBridgeProfile GuardianAbodeContentCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateGuardianAbodeContentCodexTemplate() with { Enabled = true };

    public static WorkerBridgeProfile SoulContentCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateSoulContentCodexTemplate() with { Enabled = true };

    public static WorkerTaskPacket ValidationRepairTask() => new()
    {
        TaskId = "worker_task_20260620_0001",
        WorkerId = "validation_repair_codex",
        Role = WorkerRole.ValidationRepair,
        TaskType = WorkerTaskType.ValidationRepair,
        CreatedAtUtc = "2026-06-20T00:00:00Z",
        TimeoutSeconds = 210,
        SourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 12
        },
        ValidationIssues =
        [
            new WorkerValidationIssue
            {
                Code = "normalized_weather_missing_description",
                Path = "game_state/world/weather.json",
                Message = "normalizedWeatherState.description is required."
            }
        ],
        ContextFiles =
        [
            new WorkerFileReference
            {
                Path = "game_state/world/weather.json",
                Sha256 = "example"
            }
        ],
        AllowedProposalPaths = ["game_state/world/weather.json"],
        AcceptanceCriteria =
        [
            "Return a worker-proposal-v1 JSON proposal.",
            "Validation must pass after the apply gate applies proposed changes."
        ],
        ForbiddenActions =
        [
            "Do not edit canonical game_session files directly.",
            "Do not create terminal signals manually."
        ],
        Instructions = "Return a minimal repair proposal. Do not change files outside allowedProposalPaths."
    };

    public static WorkerTaskPacket NarrativeDraftTask() => new()
    {
        TaskId = "worker_task_20260620_0002",
        WorkerId = "narrative_draft_codex",
        Role = WorkerRole.NarrativeDraft,
        TaskType = WorkerTaskType.NarrativeDraft,
        CreatedAtUtc = "2026-06-20T00:05:00Z",
        TimeoutSeconds = 150,
        SourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 12
        },
        DraftRequest = new WorkerDraftRequest
        {
            SceneGoal = "Draft a tense description of the locked manor corridor before the player chooses how to proceed.",
            Tone = "dark fantasy, concise, natural Russian prose",
            ContinuityNotes =
            [
                "The player is currently inside the mortal world.",
                "Do not resolve the player's action.",
                "Do not introduce canonical state changes."
            ],
            TargetLength = "120-180 words"
        },
        ContextFiles =
        [
            new WorkerFileReference
            {
                Path = "game_state/world/current_location.json",
                Sha256 = "example"
            }
        ],
        AllowedProposalPaths = [],
        AcceptanceCriteria =
        [
            "Return a worker-proposal-v1 JSON proposal.",
            "Include draftText for main-GM review."
        ],
        ForbiddenActions =
        [
            "Do not edit canonical game_session files directly.",
            "Do not include changedFiles."
        ],
        Instructions = "Return draftText and optional findings only. Do not include changedFiles."
    };

    public static WorkerTaskPacket InventoryContentTask() => new()
    {
        TaskId = "worker_task_20260620_0003",
        WorkerId = "inventory_content_codex",
        Role = WorkerRole.InventoryContent,
        TaskType = WorkerTaskType.InventoryContent,
        CreatedAtUtc = "2026-06-20T00:45:00Z",
        TimeoutSeconds = 150,
        SourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 14
        },
        AuthoringRequest = new WorkerContentAuthoringRequest
        {
            Domain = WorkerAuthoringDomain.Inventory,
            Goal = "Prepare stealth inventory item proposals for the current manor scene.",
            EntityHints = ["lockpick set"],
            RequiredLinks = ["player inventory"],
            OutputNotes = ["Return structured proposal only."]
        },
        ContextFiles =
        [
            new WorkerFileReference
            {
                Path = "game_state/world/current_location.json",
                Sha256 = "example"
            }
        ],
        AllowedProposalPaths = [],
        AcceptanceCriteria =
        [
            "Return a worker-proposal-v1 JSON proposal.",
            "Include authoringProposal for main-GM review."
        ],
        ForbiddenActions =
        [
            "Do not edit canonical game_session files directly.",
            "Do not include changedFiles."
        ],
        Instructions = "Return authoringProposal only. Do not include changedFiles."
    };

    public static WorkerTaskPacket SkillContentTask() => new()
    {
        TaskId = "worker_task_20260620_0004",
        WorkerId = "skill_content_codex",
        Role = WorkerRole.SkillContent,
        TaskType = WorkerTaskType.SkillContent,
        CreatedAtUtc = "2026-06-20T01:15:00Z",
        TimeoutSeconds = 150,
        SourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 15
        },
        AuthoringRequest = new WorkerContentAuthoringRequest
        {
            Domain = WorkerAuthoringDomain.Skill,
            Goal = "Prepare stealth and court-intrigue skill proposals for the current character.",
            EntityHints = ["stealth skill", "court etiquette"],
            RequiredLinks = ["player skills", "temporary effects"],
            OutputNotes = ["Return detailed player-facing explanations for bonuses and scaling."]
        },
        ContextFiles =
        [
            new WorkerFileReference
            {
                Path = "game_state/skills/skills.json",
                Sha256 = "example"
            }
        ],
        AllowedProposalPaths = [],
        AcceptanceCriteria =
        [
            "Return a worker-proposal-v1 JSON proposal.",
            "Include authoringProposal for main-GM review."
        ],
        ForbiddenActions =
        [
            "Do not edit canonical game_session files directly.",
            "Do not include changedFiles."
        ],
        Instructions = "Return authoringProposal only. Do not include changedFiles."
    };

    public static WorkerTaskPacket NpcContentTask() => new()
    {
        TaskId = "worker_task_20260620_0005",
        WorkerId = "npc_content_codex",
        Role = WorkerRole.NpcContent,
        TaskType = WorkerTaskType.NpcContent,
        CreatedAtUtc = "2026-06-20T01:45:00Z",
        TimeoutSeconds = 150,
        SourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 16
        },
        AuthoringRequest = new WorkerContentAuthoringRequest
        {
            Domain = WorkerAuthoringDomain.Npc,
            Goal = "Prepare an NPC dossier for the manor investigation scene.",
            EntityHints = ["senior steward", "witness", "merchant guild contact"],
            RequiredLinks = ["current location", "faction reputation", "personal quest"],
            OutputNotes = ["Return separate thoughts, quests, relationship hooks, and dialogue seeds."]
        },
        ContextFiles =
        [
            new WorkerFileReference
            {
                Path = "game_state/npcs/npc_core.json",
                Sha256 = "example"
            }
        ],
        AllowedProposalPaths = [],
        AcceptanceCriteria =
        [
            "Return a worker-proposal-v1 JSON proposal.",
            "Include authoringProposal for main-GM review."
        ],
        ForbiddenActions =
        [
            "Do not edit canonical game_session files directly.",
            "Do not include changedFiles."
        ],
        Instructions = "Return authoringProposal only. Do not include changedFiles."
    };

    public static WorkerTaskPacket AfterlifeWorkerTask() => new()
    {
        TaskId = "worker_task_afterlife_contract_0001",
        WorkerId = "analysis_codex",
        Role = WorkerRole.Analysis,
        TaskType = WorkerTaskType.Analysis,
        CreatedAtUtc = "2026-06-20T02:15:00Z",
        TimeoutSeconds = 150,
        SourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 17
        },
        ContextFiles =
        [
            new WorkerFileReference
            {
                Path = "game_state/meta/soul_state.json",
                Sha256 = "example"
            },
            new WorkerFileReference
            {
                Path = "OtherGuides/Afterlife_Contract_Matrix.md",
                Sha256 = "example"
            }
        ],
        AfterlifeContract = new WorkerAfterlifeTaskContract
        {
            RealmGate = WorkerAfterlifeRealmGate.ChaosSea,
            CurrentRealm = "Chaos Sea",
            ProgressionControlPaths =
            [
                "game_state/control/progression_schedule.json"
            ],
            PendingControlFiles =
            [
                "game_state/control/pending_dice_state.json"
            ],
            AllowedAfterlifeSurfaces =
            [
                "game_state/meta/guardians.json",
                "game_state/meta/afterlife_chronicles.json",
                "game_state/meta/afterlife_global_flags.json"
            ],
            RequiredReceipts =
            [
                "afterlifeChronicleUpdates"
            ],
            RequiredReports =
            [
                "progressionProcessingReport"
            ],
            ForbiddenMortalSubstitutes =
            [
                "worldStateFlags",
                "worldEventsLog",
                "Mortal NPC relationships",
                "Mortal combat HP/status",
                "Mortal factions or map files"
            ]
        },
        AllowedProposalPaths = [],
        AcceptanceCriteria =
        [
            "Return a worker-proposal-v1 JSON proposal.",
            "Include afterlifeProposal with realm gate, target surfaces, receipts, reports, validator risks, and GM review notes.",
            "Do not use Mortal World substitutes for afterlife state."
        ],
        ForbiddenActions =
        [
            "Do not edit canonical game_session files directly.",
            "Do not include changedFiles.",
            "Do not use worldStateFlags, worldEventsLog, Mortal NPC relationships, Mortal combat HP/status, Mortal factions, or Mortal map files as afterlife substitutes."
        ],
        Instructions = "Return afterlifeProposal only. Use Afterlife_Contract_Matrix.md to select exact afterlife state surfaces."
    };

    public static WorkerTaskPacket GuardianAbodeContentTask() => new()
    {
        TaskId = "worker_task_guardian_abode_content_0001",
        WorkerId = "guardian_abode_content_codex",
        Role = WorkerRole.GuardianAbodeContent,
        TaskType = WorkerTaskType.GuardianAbodeContent,
        CreatedAtUtc = "2026-06-20T03:15:00Z",
        TimeoutSeconds = 150,
        SourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 18
        },
        AuthoringRequest = new WorkerContentAuthoringRequest
        {
            Domain = WorkerAuthoringDomain.GuardianAbode,
            Goal = "Prepare Guardian and Abode project suggestions for Azalia's Chaos Sea scene.",
            EntityHints = ["guardian_azalia", "abode_azalia_memory_silk_001", "project_memory_silk"],
            RequiredLinks = ["active Guardian", "current Abode", "guardian project tracker", "abode power journal"],
            OutputNotes = ["Return GM-only hidden facts separately from player-visible summary."]
        },
        GuardianAbodeRequest = new WorkerGuardianAbodeRequest
        {
            Realm = "Chaos Sea",
            GuardianIds = ["guardian_azalia"],
            AbodeIds = ["abode_azalia_memory_silk_001"],
            PendingControlFiles =
            [
                "game_state/control/system_guardian_attraction.json",
                "game_state/control/afterlife_return_guard.json"
            ],
            FocusAreas =
            [
                "guardian dossier",
                "abode project",
                "abode power",
                "trade favor",
                "guardian politics"
            ],
            ReadScope =
            [
                "game_state/meta/guardians.json",
                "game_state/meta/guardian_projects.json",
                "game_state/meta/abode_power_journal.json",
                "game_state/meta/chaos_sea_guardian_politics.json"
            ]
        },
        ContextFiles =
        [
            new WorkerFileReference
            {
                Path = "game_state/meta/guardians.json",
                Sha256 = "example"
            },
            new WorkerFileReference
            {
                Path = "game_state/meta/guardian_projects.json",
                Sha256 = "example"
            },
            new WorkerFileReference
            {
                Path = "OtherGuides/Afterlife_Contract_Matrix.md",
                Sha256 = "example"
            }
        ],
        AfterlifeContract = new WorkerAfterlifeTaskContract
        {
            RealmGate = WorkerAfterlifeRealmGate.ChaosSea,
            CurrentRealm = "Chaos Sea",
            ProgressionControlPaths = ["game_state/control/progression_schedule.json"],
            PendingControlFiles =
            [
                "game_state/control/system_guardian_attraction.json",
                "game_state/control/afterlife_return_guard.json"
            ],
            AllowedAfterlifeSurfaces =
            [
                "game_state/meta/guardians.json",
                "game_state/meta/guardian_projects.json",
                "game_state/meta/abode_power_journal.json",
                "game_state/meta/chaos_sea_guardian_politics.json",
                "game_state/meta/afterlife_chronicles.json"
            ],
            RequiredReceipts =
            [
                "guardianProjectUpdates",
                "guardianPowerEvents"
            ],
            RequiredReports =
            [
                "progressionProcessingReport"
            ],
            ForbiddenMortalSubstitutes =
            [
                "UpdateNPCs",
                "NPCRelationshipChanges",
                "Mortal factionDataChanges",
                "worldMapUpdates"
            ]
        },
        AllowedProposalPaths = [],
        AcceptanceCriteria =
        [
            "Return a worker-proposal-v1 JSON proposal.",
            "Include authoringProposal, guardianAbodeProposal, and afterlifeProposal.",
            "Keep hidden guardian facts GM-only."
        ],
        ForbiddenActions =
        [
            "Do not edit canonical game_session files directly.",
            "Do not include changedFiles.",
            "Do not model Guardians as Mortal NPCs or Mortal factions."
        ],
        Instructions = "Return guardianAbodeProposal and afterlifeProposal only; use exact Guardian/Abode surfaces from Afterlife_Contract_Matrix.md."
    };

    public static WorkerTaskPacket SoulContentTask() => new()
    {
        TaskId = "worker_task_soul_content_0001",
        WorkerId = "soul_content_codex",
        Role = WorkerRole.SoulContent,
        TaskType = WorkerTaskType.SoulContent,
        CreatedAtUtc = "2026-06-20T04:15:00Z",
        TimeoutSeconds = 150,
        SourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 19
        },
        AuthoringRequest = new WorkerContentAuthoringRequest
        {
            Domain = WorkerAuthoringDomain.Soul,
            Goal = "Prepare safe Chaos Sea soul progression and next-life preparation notes.",
            EntityHints = ["player_soul", "ink feathers", "next-life preparation"],
            RequiredLinks = ["soul_state", "afterlife chronicles", "progression receipts"],
            OutputNotes = ["Reference player-owned identity fields as readonly; do not overwrite them."]
        },
        SoulContentRequest = new WorkerSoulContentRequest
        {
            Realm = "Chaos Sea",
            SoulContext = "The player soul has just finished a dangerous Chaos Sea exchange and may receive a small reward plus next-life preparation hooks.",
            RequestedScope =
            [
                "safe soul summary",
                "progression suggestion",
                "reward note",
                "next-life preparation hook"
            ],
            ProgressionConstraints =
            [
                "Do not overwrite player-owned identity fields.",
                "Ink Feather and Light Spark changes require explicit receipts.",
                "Next-life preparation hooks must remain review-only until the main GM accepts them."
            ],
            ReadScope =
            [
                "game_state/meta/soul_state.json",
                "game_state/meta/afterlife_chronicles.json",
                "game_state/control/progression_schedule.json"
            ],
            PlayerOwnedIdentityFields =
            [
                "soulName",
                "soulFormDescription"
            ]
        },
        ContextFiles =
        [
            new WorkerFileReference
            {
                Path = "game_state/meta/soul_state.json",
                Sha256 = "example"
            },
            new WorkerFileReference
            {
                Path = "game_state/meta/afterlife_chronicles.json",
                Sha256 = "example"
            },
            new WorkerFileReference
            {
                Path = "OtherGuides/Afterlife_Contract_Matrix.md",
                Sha256 = "example"
            }
        ],
        AfterlifeContract = new WorkerAfterlifeTaskContract
        {
            RealmGate = WorkerAfterlifeRealmGate.ChaosSea,
            CurrentRealm = "Chaos Sea",
            ProgressionControlPaths = ["game_state/control/progression_schedule.json"],
            PendingControlFiles = ["game_state/control/pending_dice_state.json"],
            AllowedAfterlifeSurfaces =
            [
                "game_state/meta/soul_state.json",
                "game_state/meta/afterlife_chronicles.json",
                "game_state/control/progression_schedule.json"
            ],
            RequiredReceipts =
            [
                "metaStateUpdates.inkFeatherChanges",
                "afterlifeChronicleUpdates"
            ],
            RequiredReports =
            [
                "progressionProcessingReport"
            ],
            ForbiddenMortalSubstitutes =
            [
                "UpdateCharacter",
                "game_state/player",
                "Mortal inventory",
                "worldStateFlags"
            ]
        },
        AllowedProposalPaths = [],
        AcceptanceCriteria =
        [
            "Return a worker-proposal-v1 JSON proposal.",
            "Include authoringProposal, afterlifeProposal, and soulContentProposal.",
            "Treat soulName and soulFormDescription as player-owned readonly identity."
        ],
        ForbiddenActions =
        [
            "Do not edit canonical game_session files directly.",
            "Do not include changedFiles.",
            "Do not rewrite soul state as ordinary character, inventory, or Mortal World state."
        ],
        Instructions = "Return soulContentProposal and afterlifeProposal only; use exact soul_state and afterlife surfaces from Afterlife_Contract_Matrix.md."
    };

    public static WorkerProposal ValidationRepairProposal() => new()
    {
        ProposalId = "worker_proposal_20260620_0001",
        TaskId = "worker_task_20260620_0001",
        WorkerId = "validation_repair_codex",
        Status = WorkerProposalStatus.Completed,
        Summary = "Added the missing normalized weather description.",
        ChangedFiles =
        [
            new WorkerChangedFile
            {
                Path = "game_state/world/weather.json",
                ChangeKind = WorkerFileChangeKind.Replace,
                BeforeSha256 = "example",
                AfterSha256 = "example-after",
                ContentRef = "worker_proposals/worker_proposal_20260620_0001/game_state/world/weather.json"
            }
        ],
        SelfCheck = new WorkerSelfCheck
        {
            ScopeReviewed = true,
            ValidationExpectedToPass = true,
            Notes = []
        },
        CreatedAtUtc = "2026-06-20T00:00:15Z"
    };

    public static WorkerProposal NarrativeDraftProposal() => new()
    {
        ProposalId = "worker_proposal_20260620_0002",
        TaskId = "worker_task_20260620_0002",
        WorkerId = "narrative_draft_codex",
        Status = WorkerProposalStatus.Completed,
        Summary = "Drafted corridor narration for main-GM review.",
        ChangedFiles = [],
        Findings =
        [
            new WorkerFinding
            {
                Kind = "continuity-note",
                Message = "Draft avoids resolving the player's next action."
            }
        ],
        DraftText = "Черновик сцены для главного ГМа. Этот текст не показывается игроку автоматически.",
        SelfCheck = new WorkerSelfCheck
        {
            ScopeReviewed = true,
            ValidationExpectedToPass = true,
            Notes = ["Proposal-only task; no file changes included."]
        },
        CreatedAtUtc = "2026-06-20T00:05:20Z"
    };

    public static WorkerProposal InventoryContentProposal() => new()
    {
        ProposalId = "worker_proposal_20260620_0003",
        TaskId = "worker_task_20260620_0003",
        WorkerId = "inventory_content_codex",
        Status = WorkerProposalStatus.Completed,
        Summary = "Prepared stealth inventory item proposals for main-GM review.",
        ChangedFiles = [],
        Findings =
        [
            new WorkerFinding
            {
                Kind = "validator-risk",
                Message = "Accepted items must be linked to an inventory container by the main GM."
            }
        ],
        AuthoringProposal = new WorkerContentAuthoringProposal
        {
            Domain = WorkerAuthoringDomain.Inventory,
            Goal = "Prepare stealth inventory item proposals for the current manor scene.",
            CreatedEntities =
            [
                new WorkerAuthoredEntity
                {
                    EntityType = "item",
                    EntityId = "item_valmont_lockpick_set",
                    DisplayName = "Набор тонких отмычек Вальмонта",
                    Summary = "Компактный набор для тихого вскрытия простых замков.",
                    RequiredFields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "slot",
                            Value = "hands"
                        },
                        new WorkerAuthoredField
                        {
                            Name = "description",
                            Value = "Кожаный футляр с тонкими стальными отмычками и натяжителями; пригоден для простых замков, но не вскрывает магические печати сам по себе."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "quality",
                            Value = "обычное"
                        },
                        new WorkerAuthoredField
                        {
                            Name = "value",
                            Value = "35"
                        },
                        new WorkerAuthoredField
                        {
                            Name = "balanceNote",
                            Value = "Дает повод для lockpicking QTE, но не гарантирует успех и не заменяет проверку навыка."
                        }
                    ],
                    Relationships = ["player inventory", "lockpicking QTE"]
                }
            ],
            RequiredLinks =
            [
                new WorkerRequiredEntityLink
                {
                    Source = "item_valmont_lockpick_set",
                    Target = "player_inventory",
                    Reason = "Main GM must decide whether the item is discovered or already carried."
                }
            ],
            ValidatorRisks =
            [
                new WorkerValidatorRisk
                {
                    Code = "inventory_storage_link_required",
                    Message = "Item proposal is useless unless linked to an inventory container.",
                    Mitigation = "Main GM should add accepted items through the normal inventory state surface."
                }
            ],
            GmReviewNotes = ["Review balance before adding bonuses."]
        },
        SelfCheck = new WorkerSelfCheck
        {
            ScopeReviewed = true,
            ValidationExpectedToPass = true,
            Notes = ["Proposal-only authoring task; no file changes included."]
        },
        CreatedAtUtc = "2026-06-20T00:45:20Z"
    };

    public static WorkerProposal SkillContentProposal() => new()
    {
        ProposalId = "worker_proposal_20260620_0004",
        TaskId = "worker_task_20260620_0004",
        WorkerId = "skill_content_codex",
        Status = WorkerProposalStatus.Completed,
        Summary = "Prepared skill proposals with localized scaling and player-facing bonus explanations.",
        ChangedFiles = [],
        Findings =
        [
            new WorkerFinding
            {
                Kind = "validator-risk",
                Message = "Skill bonuses must be linked to effects/status/combat surfaces before the main GM accepts them."
            }
        ],
        AuthoringProposal = new WorkerContentAuthoringProposal
        {
            Domain = WorkerAuthoringDomain.Skill,
            Goal = "Prepare stealth and court-intrigue skill proposals for the current character.",
            CreatedEntities =
            [
                new WorkerAuthoredEntity
                {
                    EntityType = "skill",
                    EntityId = "skill_shadow_courtesy",
                    DisplayName = "Теневая учтивость",
                    Summary = "Навык помогает скрывать намерения в аристократических сценах и связывает социальное давление с осторожным перемещением.",
                    RequiredFields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "description",
                            Value = "Персонаж умеет вести светскую беседу так, чтобы не выдать цель визита, отвлечь свидетелей и получить шанс уйти из комнаты без лишнего внимания."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "scalingAttribute",
                            Value = "dexterity"
                        },
                        new WorkerAuthoredField
                        {
                            Name = "localizedScalingAttribute",
                            Value = "Ловкость"
                        },
                        new WorkerAuthoredField
                        {
                            Name = "scalingExplanation",
                            Value = "Ловкость влияет на то, насколько тихо персонаж меняет позицию во время разговора; социальная часть остается предметом решения ГМа."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "structuredBonus",
                            Value = "Скрытность +1 в светских сценах"
                        },
                        new WorkerAuthoredField
                        {
                            Name = "bonusExplanation",
                            Value = "Бонус применяется только там, где персонаж одновременно действует в салоне и пытается не привлечь внимание."
                        }
                    ],
                    Relationships = ["player skills", "temporary effects", "social stealth checks"]
                }
            ],
            RequiredLinks =
            [
                new WorkerRequiredEntityLink
                {
                    Source = "skill_shadow_courtesy",
                    Target = "player_skills",
                    Reason = "Main GM must decide whether this skill is learned, granted by background, or only proposed for future progression."
                },
                new WorkerRequiredEntityLink
                {
                    Source = "skill_shadow_courtesy",
                    Target = "temporary_effects",
                    Reason = "If the skill creates a situational bonus, it must be represented through an explicit effect/status surface."
                }
            ],
            ValidatorRisks =
            [
                new WorkerValidatorRisk
                {
                    Code = "skill_bonus_explanation_required",
                    Message = "Structured bonuses without player-facing explanation become unreadable in status/skill screens.",
                    Mitigation = "Keep bonusExplanation beside every proposed structuredBonus."
                }
            ],
            GmReviewNotes = ["Review whether the skill is permanent or a temporary lesson before adding it to canonical state."]
        },
        SelfCheck = new WorkerSelfCheck
        {
            ScopeReviewed = true,
            ValidationExpectedToPass = true,
            Notes = ["Proposal-only skill authoring task; no file changes included."]
        },
        CreatedAtUtc = "2026-06-20T01:15:20Z"
    };

    public static WorkerProposal NpcContentProposal() => new()
    {
        ProposalId = "worker_proposal_20260620_0005",
        TaskId = "worker_task_20260620_0005",
        WorkerId = "npc_content_codex",
        Status = WorkerProposalStatus.Completed,
        Summary = "Prepared an NPC dossier with separate thoughts, quests, relationships, and dialogue hooks.",
        ChangedFiles = [],
        Findings =
        [
            new WorkerFinding
            {
                Kind = "validator-risk",
                Message = "NPC proposals must be linked to location, faction, and quest surfaces before the main GM accepts them."
            }
        ],
        AuthoringProposal = new WorkerContentAuthoringProposal
        {
            Domain = WorkerAuthoringDomain.Npc,
            Goal = "Prepare an NPC dossier for the manor investigation scene.",
            CreatedEntities =
            [
                new WorkerAuthoredEntity
                {
                    EntityType = "npc",
                    EntityId = "npc_marius_de_grand",
                    DisplayName = "Мариус де Гран",
                    Summary = "Старший дворецкий Вальмонтов знает ночной распорядок, боится гильдейского долга и может стать связующим свидетелем расследования.",
                    RequiredFields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "description",
                            Value = "Сухой пожилой дворецкий с безупречной памятью на ключи, лица и поздние визиты; говорит ровно, но выдает тревогу, когда речь заходит о купеческой гильдии."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "publicKnowledge",
                            Value = "Игрок может узнать, что Мариус первым заметил письмо и усилил ночной надзор после странного шороха у северного коридора."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "privateKnowledge",
                            Value = "Мариус скрывает, что долг семьи перед гильдией дает торговцам рычаг давления на прислугу и доступ к боковым дверям."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "thoughtJournal",
                            Value = "Запись мыслей: Мариус боится, что если письмо связано с долгом, обвинят слуг, а не настоящего ночного гостя."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "relationshipHooks",
                            Value = "Доверие растет, если игрок защищает прислугу от обвинений; падает, если он угрожает раскрыть долг без доказательств."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "personalQuests",
                            Value = "Личный квест: найти, кто пользовался боковой дверью после полуночи, не подставив служанку Ирен."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "dialogueSeeds",
                            Value = "Реплики: спросить о ночной вахте; попросить список ключей; осторожно упомянуть купеческую гильдию."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "detailSurfaces",
                            Value = "/нпс Мариус де Гран; /нпс Мариус де Гран мысли; /нпс Мариус де Гран квесты; /торговать если гильдейская связь станет открытой."
                        }
                    ],
                    Relationships = ["current location", "merchant guild faction", "personal quest", "thought journal", "dialogue options"]
                }
            ],
            RequiredLinks =
            [
                new WorkerRequiredEntityLink
                {
                    Source = "npc_marius_de_grand",
                    Target = "location_valmont_manor",
                    Reason = "The NPC must appear in the current manor location or a reachable adjacent room."
                },
                new WorkerRequiredEntityLink
                {
                    Source = "npc_marius_de_grand",
                    Target = "faction_merchant_guild",
                    Reason = "The debt hook only matters if faction reputation and influence can reference it."
                },
                new WorkerRequiredEntityLink
                {
                    Source = "npc_marius_de_grand",
                    Target = "quest_side_door_after_midnight",
                    Reason = "The personal quest must be discoverable through NPC detail menus and dialogue."
                }
            ],
            ValidatorRisks =
            [
                new WorkerValidatorRisk
                {
                    Code = "npc_linked_details_required",
                    Message = "NPC thoughts, quests, and relationships are useless if they are not linked to visible detail commands.",
                    Mitigation = "Keep detailSurfaces and requiredLinks beside the proposed NPC profile."
                }
            ],
            GmReviewNotes = ["Review privateKnowledge privacy before showing the NPC dossier to the player."]
        },
        SelfCheck = new WorkerSelfCheck
        {
            ScopeReviewed = true,
            ValidationExpectedToPass = true,
            Notes = ["Proposal-only NPC authoring task; no file changes included."]
        },
        CreatedAtUtc = "2026-06-20T01:45:20Z"
    };

    public static WorkerProposal AfterlifeWorkerProposal() => new()
    {
        ProposalId = "worker_proposal_afterlife_contract_0001",
        TaskId = "worker_task_afterlife_contract_0001",
        WorkerId = "analysis_codex",
        Status = WorkerProposalStatus.Completed,
        Summary = "Prepared afterlife realm-aware proposal for main-GM review.",
        ChangedFiles = [],
        Findings =
        [
            new WorkerFinding
            {
                Kind = "afterlife-contract-note",
                Message = "Use afterlife chronicles and guardian state, not Mortal World substitutes."
            }
        ],
        AfterlifeProposal = new WorkerAfterlifeProposalContract
        {
            RealmGate = WorkerAfterlifeRealmGate.ChaosSea,
            TargetSurfaces =
            [
                "game_state/meta/guardians.json",
                "game_state/meta/afterlife_chronicles.json"
            ],
            RequiredReceipts =
            [
                "afterlifeChronicleUpdates"
            ],
            RequiredReports =
            [
                "progressionProcessingReport"
            ],
            PlayerVisibleSummary = "В Море Хаоса нужно обновить хронику и реакцию хранителя через поверхности посмертия.",
            GmReviewNotes =
            [
                "Review Afterlife_Contract_Matrix.md before accepting.",
                "Keep hidden guardian motives out of player-visible output."
            ],
            ValidatorRisks =
            [
                new WorkerValidatorRisk
                {
                    Code = "afterlife_surface_receipt_required",
                    Message = "Afterlife updates need exact receipts/reports.",
                    Mitigation = "Use afterlifeChronicleUpdates and progressionProcessingReport surfaces only."
                }
            ]
        },
        SelfCheck = new WorkerSelfCheck
        {
            ScopeReviewed = true,
            ValidationExpectedToPass = true,
            Notes = ["Proposal-only afterlife contract task; no file changes included."]
        },
        CreatedAtUtc = "2026-06-20T02:15:20Z"
    };

    public static WorkerProposal GuardianAbodeContentProposal() => new()
    {
        ProposalId = "worker_proposal_guardian_abode_content_0001",
        TaskId = "worker_task_guardian_abode_content_0001",
        WorkerId = "guardian_abode_content_codex",
        Status = WorkerProposalStatus.Completed,
        Summary = "Prepared Guardian and Abode proposal for main-GM review.",
        ChangedFiles = [],
        Findings =
        [
            new WorkerFinding
            {
                Kind = "afterlife-guardian-risk",
                Message = "Guardian project updates must stay on Guardian/Abode afterlife surfaces."
            }
        ],
        AuthoringProposal = new WorkerContentAuthoringProposal
        {
            Domain = WorkerAuthoringDomain.GuardianAbode,
            Goal = "Prepare Guardian and Abode project suggestions for Azalia's Chaos Sea scene.",
            CreatedEntities =
            [
                new WorkerAuthoredEntity
                {
                    EntityType = "guardian-project",
                    EntityId = "project_azalia_memory_silk",
                    DisplayName = "Шёлковая память Азалии",
                    Summary = "Проект Обители, который укрепляет память и долг перед Азалией.",
                    RequiredFields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "playerFacingSummary",
                            Value = "Азалия предлагает укрепить Обитель через нити памяти."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "gmOnlyHiddenFacts",
                            Value = "GM-only: проект также проверяет, доверяет ли душа древним долгам Азалии."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "exactAfterlifeSurfaces",
                            Value = "game_state/meta/guardian_projects.json; game_state/meta/abode_power_journal.json"
                        }
                    ],
                    Relationships = ["guardian_azalia", "abode_azalia_memory_silk_001", "guardian project tracker"]
                }
            ],
            UpdatedEntities = [],
            RequiredLinks =
            [
                new WorkerRequiredEntityLink
                {
                    Source = "project_azalia_memory_silk",
                    Target = "game_state/meta/guardian_projects.json",
                    Reason = "The main GM must accept the proposal through Guardian project surfaces."
                },
                new WorkerRequiredEntityLink
                {
                    Source = "project_azalia_memory_silk",
                    Target = "game_state/meta/abode_power_journal.json",
                    Reason = "Power consequences must be audited through Abode Power state."
                }
            ],
            ValidatorRisks =
            [
                new WorkerValidatorRisk
                {
                    Code = "guardian_abode_surface_required",
                    Message = "Guardian/Abode proposals are invalid if rewritten as Mortal NPC or Mortal faction updates.",
                    Mitigation = "Use guardianAbodeProposal plus exact afterlife surfaces."
                }
            ],
            GmReviewNotes = ["Keep hidden Guardian motives GM-only."]
        },
        AfterlifeProposal = new WorkerAfterlifeProposalContract
        {
            RealmGate = WorkerAfterlifeRealmGate.ChaosSea,
            TargetSurfaces =
            [
                "game_state/meta/guardians.json",
                "game_state/meta/guardian_projects.json",
                "game_state/meta/abode_power_journal.json",
                "game_state/meta/chaos_sea_guardian_politics.json"
            ],
            RequiredReceipts =
            [
                "guardianProjectUpdates",
                "guardianPowerEvents"
            ],
            RequiredReports =
            [
                "progressionProcessingReport"
            ],
            PlayerVisibleSummary = "Азалия предлагает укрепить Обитель через проект памяти и новую услугу.",
            GmReviewNotes =
            [
                "Review guardian project and Abode Power contracts before accepting.",
                "Do not reveal hidden dependency politics in player-facing output."
            ],
            ValidatorRisks =
            [
                new WorkerValidatorRisk
                {
                    Code = "guardian_project_receipt_required",
                    Message = "Guardian project and Abode Power changes need matching receipts.",
                    Mitigation = "Use guardianProjectUpdates and guardianPowerEvents only if accepted."
                }
            ]
        },
        GuardianAbodeProposal = new WorkerGuardianAbodeProposal
        {
            PlayerVisibleSummary = "Азалия предлагает укрепить Обитель через проект памяти и новую услугу.",
            GuardianUpdates =
            [
                new WorkerGuardianAbodeProposalItem
                {
                    ItemId = "guardian_update_azalia_focus",
                    TargetId = "guardian_azalia",
                    Title = "Позиция Азалии",
                    Summary = "Азалия открыто поддерживает укрепление Обители через память.",
                    Visibility = "visible",
                    TargetSurfaces = ["game_state/meta/guardians.json"],
                    Fields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "relationshipCue",
                            Value = "visible Guardian attitude on afterlife Guardian surfaces"
                        }
                    ]
                }
            ],
            AbodeUpdates =
            [
                new WorkerGuardianAbodeProposalItem
                {
                    ItemId = "abode_update_memory_silk",
                    TargetId = "abode_azalia_memory_silk_001",
                    Title = "Нити памяти в Обители",
                    Summary = "Обитель получает проект, связанный с памятью и долгом.",
                    Visibility = "visible",
                    TargetSurfaces = ["game_state/meta/guardians.json"],
                    Fields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "abodeCue",
                            Value = "current Abode project context"
                        }
                    ]
                }
            ],
            ProjectSuggestions =
            [
                new WorkerGuardianAbodeProposalItem
                {
                    ItemId = "project_suggestion_memory_silk",
                    TargetId = "project_azalia_memory_silk",
                    Title = "Шёлковая память",
                    Summary = "Проект можно начать как медленное укрепление Обители.",
                    Visibility = "visible",
                    TargetSurfaces = ["game_state/meta/guardian_projects.json"],
                    Fields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "projectType",
                            Value = "abode_memory_fortification"
                        }
                    ]
                }
            ],
            PowerReputationConsequences =
            [
                new WorkerGuardianAbodeProposalItem
                {
                    ItemId = "power_consequence_memory_silk",
                    TargetId = "abode_azalia_memory_silk_001",
                    Title = "Резонанс Обители",
                    Summary = "Принятие проекта может дать малый рост силы Обители и доверия Азалии.",
                    Visibility = "visible",
                    TargetSurfaces = ["game_state/meta/abode_power_journal.json", "game_state/meta/guardians.json"],
                    Fields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "powerDelta",
                            Value = "small positive if main GM accepts"
                        }
                    ]
                }
            ],
            TradeFavorHooks =
            [
                new WorkerGuardianAbodeProposalItem
                {
                    ItemId = "trade_favor_azalia_thread",
                    TargetId = "guardian_azalia",
                    Title = "Услуга Азалии",
                    Summary = "Азалия может предложить услугу обмена памятью, если проект принят.",
                    Visibility = "visible",
                    TargetSurfaces = ["game_state/meta/guardians.json"],
                    Fields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "favorHook",
                            Value = "trade/favor hook for Guardian review"
                        }
                    ]
                }
            ],
            DossierNotes =
            [
                new WorkerGuardianAbodeProposalItem
                {
                    ItemId = "dossier_hidden_dependency",
                    TargetId = "guardian_azalia",
                    Title = "Скрытый долг Азалии",
                    Summary = "GM-only: Азалия проверяет, готова ли душа принять долг памяти.",
                    Visibility = "gm-only",
                    TargetSurfaces = ["game_state/meta/chaos_sea_guardian_politics.json"],
                    Fields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "hiddenFact",
                            Value = "hidden_dependency politics; never show in player-visible summary"
                        }
                    ]
                }
            ],
            RequiredReceipts =
            [
                "guardianProjectUpdates",
                "guardianPowerEvents"
            ],
            RequiredReports =
            [
                "progressionProcessingReport"
            ],
            ValidatorRisks =
            [
                new WorkerValidatorRisk
                {
                    Code = "guardian_abode_hidden_leak",
                    Message = "Hidden Guardian politics must stay GM-only.",
                    Mitigation = "Keep hidden dossier notes out of playerVisibleSummary."
                }
            ],
            GmReviewNotes = ["Use Afterlife_Contract_Matrix.md examples 16, 20, and 26E before accepting."]
        },
        SelfCheck = new WorkerSelfCheck
        {
            ScopeReviewed = true,
            ValidationExpectedToPass = true,
            Notes = ["Proposal-only Guardian/Abode task; no file changes included."]
        },
        CreatedAtUtc = "2026-06-20T03:15:20Z"
    };

    public static WorkerProposal SoulContentProposal() => new()
    {
        ProposalId = "worker_proposal_soul_content_0001",
        TaskId = "worker_task_soul_content_0001",
        WorkerId = "soul_content_codex",
        Status = WorkerProposalStatus.Completed,
        Summary = "Prepared safe soul progression and next-life preparation notes for main-GM review.",
        ChangedFiles = [],
        Findings =
        [
            new WorkerFinding
            {
                Kind = "afterlife-soul-boundary",
                Message = "soulName and soulFormDescription are player-owned and must remain readonly."
            }
        ],
        AuthoringProposal = new WorkerContentAuthoringProposal
        {
            Domain = WorkerAuthoringDomain.Soul,
            Goal = "Prepare safe Chaos Sea soul progression and next-life preparation notes.",
            CreatedEntities =
            [
                new WorkerAuthoredEntity
                {
                    EntityType = "soul-progression",
                    EntityId = "soul_progression_echo_mercy_0001",
                    DisplayName = "Отзвук милости",
                    Summary = "Душа может получить малую награду и осторожный след для следующей жизни без изменения имени или формы души.",
                    RequiredFields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "playerFacingSummary",
                            Value = "Душа сохранила отзвук милости и может унести его как мягкий стартовый знак."
                        },
                        new WorkerAuthoredField
                        {
                            Name = "exactAfterlifeSurfaces",
                            Value = "game_state/meta/soul_state.json; game_state/meta/afterlife_chronicles.json"
                        },
                        new WorkerAuthoredField
                        {
                            Name = "readonlyIdentityFields",
                            Value = "soulName; soulFormDescription"
                        }
                    ],
                    Relationships = ["player_soul", "afterlife chronicles", "next-life preparation"]
                }
            ],
            RequiredLinks =
            [
                new WorkerRequiredEntityLink
                {
                    Source = "soul_progression_echo_mercy_0001",
                    Target = "game_state/meta/soul_state.json",
                    Reason = "The main GM must accept any reward through soul_state/metaStateUpdates receipts, not ordinary character state."
                },
                new WorkerRequiredEntityLink
                {
                    Source = "soul_progression_echo_mercy_0001",
                    Target = "game_state/meta/afterlife_chronicles.json",
                    Reason = "The soul-facing summary should be recorded as an afterlife chronicle if accepted."
                }
            ],
            ValidatorRisks =
            [
                new WorkerValidatorRisk
                {
                    Code = "soul_identity_readonly",
                    Message = "Soul content must not overwrite player-owned identity fields.",
                    Mitigation = "Keep soulName and soulFormDescription only in forbiddenReadonlyFields or readonly notes."
                }
            ],
            GmReviewNotes = ["Review rewards and next-life hooks before turning them into canonical receipts."]
        },
        AfterlifeProposal = new WorkerAfterlifeProposalContract
        {
            RealmGate = WorkerAfterlifeRealmGate.ChaosSea,
            TargetSurfaces =
            [
                "game_state/meta/soul_state.json",
                "game_state/meta/afterlife_chronicles.json"
            ],
            RequiredReceipts =
            [
                "metaStateUpdates.inkFeatherChanges",
                "afterlifeChronicleUpdates"
            ],
            RequiredReports =
            [
                "progressionProcessingReport"
            ],
            PlayerVisibleSummary = "Душа сохраняет отзвук милости и получает осторожный крючок для будущей жизни.",
            GmReviewNotes =
            [
                "Review soul_state reward surfaces before accepting.",
                "Do not overwrite soulName or soulFormDescription."
            ],
            ValidatorRisks =
            [
                new WorkerValidatorRisk
                {
                    Code = "soul_reward_receipt_required",
                    Message = "Soul rewards require explicit afterlife receipts.",
                    Mitigation = "Use metaStateUpdates.inkFeatherChanges and afterlifeChronicleUpdates only if accepted."
                }
            ]
        },
        SoulContentProposal = new WorkerSoulContentProposal
        {
            PlayerVisibleSummary = "Душа сохраняет отзвук милости и получает осторожный крючок для будущей жизни.",
            SafeSoulSummaries =
            [
                new WorkerSoulContentProposalItem
                {
                    ItemId = "summary_echo_mercy",
                    Title = "Отзвук милости",
                    Summary = "Публичное описание результата без изменения имени или формы души.",
                    Visibility = "visible",
                    TargetSurfaces = ["game_state/meta/afterlife_chronicles.json"],
                    Fields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "summaryUse",
                            Value = "player-facing chronicle note"
                        }
                    ]
                }
            ],
            ProgressionSuggestions =
            [
                new WorkerSoulContentProposalItem
                {
                    ItemId = "progression_small_spark",
                    Title = "Малый след света",
                    Summary = "Возможная малая progression-связка для души после сцены.",
                    Visibility = "visible",
                    TargetSurfaces = ["game_state/meta/soul_state.json"],
                    Fields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "progressionCue",
                            Value = "review-only Light Spark or resonance suggestion"
                        }
                    ]
                }
            ],
            RewardNotes =
            [
                new WorkerSoulContentProposalItem
                {
                    ItemId = "reward_ink_feather_note",
                    Title = "Награда пером",
                    Summary = "Если главный ГМ принимает награду, она должна пройти через явный receipt.",
                    Visibility = "visible",
                    TargetSurfaces = ["game_state/meta/soul_state.json"],
                    Fields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "receipt",
                            Value = "metaStateUpdates.inkFeatherChanges"
                        }
                    ]
                }
            ],
            NextLifePreparationHooks =
            [
                new WorkerSoulContentProposalItem
                {
                    ItemId = "next_life_hook_mercy",
                    Title = "След милости",
                    Summary = "Крючок для будущей жизни: встреча с похожим выбором, но без автоматического старта новой жизни.",
                    Visibility = "visible",
                    TargetSurfaces = ["game_state/meta/afterlife_chronicles.json"],
                    Fields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "nextLifePrep",
                            Value = "review-only hook for the main GM"
                        }
                    ]
                }
            ],
            RequiredReceipts =
            [
                "metaStateUpdates.inkFeatherChanges",
                "afterlifeChronicleUpdates"
            ],
            RequiredReports =
            [
                "progressionProcessingReport"
            ],
            ForbiddenReadonlyFields =
            [
                "soulName",
                "soulFormDescription"
            ],
            ValidatorRisks =
            [
                new WorkerValidatorRisk
                {
                    Code = "soul_identity_readonly",
                    Message = "Player-owned soul identity must stay readonly.",
                    Mitigation = "Reference identity fields only in forbiddenReadonlyFields or read-only summaries."
                }
            ],
            GmReviewNotes = ["Main GM must rewrite accepted content through afterlife response surfaces."]
        },
        SelfCheck = new WorkerSelfCheck
        {
            ScopeReviewed = true,
            ValidationExpectedToPass = true,
            Notes = ["Proposal-only Soul content task; no file changes included."]
        },
        CreatedAtUtc = "2026-06-20T04:15:20Z"
    };
}
