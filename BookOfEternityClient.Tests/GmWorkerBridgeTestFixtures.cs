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
}
