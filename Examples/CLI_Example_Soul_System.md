# CLI Distribution Example: Completed Mortal Life

## Scenario

Player voluntarily ends a mortal life. In current CLI runtime this is a **two-phase flow**:

1. A GM-authored mortal-world turn may write `TriggerLifeEnd` and return the Soul to Chaos Sea.
2. After that accepted turn, the client sends a separate **Life Evaluation** request.
3. Only the later Life Evaluation turn grants Ink Feathers, Soul Relics, chronicle updates, and other meta rewards.

Do **not** mix `TriggerLifeEnd` with the final Life Evaluation reward payload in one GM turn.

---

## Phase 1: Trigger Life End

### API-style response

```json
{
  "response": "Алдрик опускает меч и понимает, что его долг исполнен. Смертная жизнь завершается, и душа начинает путь обратно в Море Хаоса.",
  "gm_thoughts_markdown": "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: none\n- Why relevant: TriggerLifeEnd меняет lifecycle/control surfaces без структурированных NPC или Guardian updates.\n- Actors outside scope: NPC сцены, Хранители\n- Why outside scope: Этот ход только запускает переход и не должен мутировать их состояние.\n\n## Reasoning\n- Игрок добровольно завершает текущую смертную жизнь.\n- Этот ход только запускает переход.\n- Награды будут выданы в отдельном Life Evaluation ходе.",
  "metaStateUpdates": {
    "lifeTransitions": {
      "recordLifeCompletion": {
        "characterFinalState": {
          "name": "Алдрик",
          "level": 8,
          "class": "Knight",
          "finalHP": 100,
          "location": "village_center"
        },
        "majorAchievements": [
          "Защитник деревни",
          "Благородный воин"
        ],
        "relationshipsFormed": [
          {
            "npc": "Староста Ренальд",
            "level": "Trusted Ally"
          }
        ],
        "moralChoices": [
          {
            "choice": "Отказался от награды за спасение деревни",
            "alignment": "Lawful Good"
          }
        ],
        "skillsLearned": [
          "Фехтование",
          "Тактика"
        ],
        "enlightenmentGained": 2
      }
    }
  },
  "TriggerLifeEnd": {
    "reason": "Voluntary",
    "summary": "Алдрик завершил свою миссию по защите деревни и добровольно завершил жизнь"
  }
}
```

### Distributed files

```text
output/narrative_response.json          <- response
output/debug_logs.json                  <- gm_thoughts_markdown
game_state/meta/soul_state.json         <- metaStateUpdates.lifeTransitions / realm metadata
game_state/control/life_transitions.json <- TriggerLifeEnd
ready/turn_complete.json                <- correlated terminal success signal with exact metadata + filesModified
```

### Important notes

- This turn must **not** grant Ink Feathers yet.
- This turn must **not** grant Soul Relics yet.
- This turn must **not** append the completed-life reward summary to `player_chronicle.json` yet.
- This trigger turn is still a **Mortal World** turn, so it must **not** emit `UpdateGuardians`.

---

## Phase 2: Life Evaluation

After Phase 1 is accepted, the client sends a dedicated Life Evaluation request. That later GM turn is where the reward is produced.

### API-style response

```json
{
  "response": "В Море Хаоса тебя встречает Луминара. [ACHIEVEMENT_UNLOCK: Память о Долге] Она склоняет голову в знак уважения и вручает тебе награду за прожитую жизнь.",
  "gm_thoughts_markdown": "## NPC Scope\n- Mode: Guardian-centric\n- Relevant actors: Луминара\n- Why relevant: Life Evaluation is resolved through the active Guardian in the afterlife realm.\n- Actors outside scope: other Guardians, Mortal World NPCs\n- Why outside scope: Only Луминара participates in this evaluation turn.\n\n## Guardian Thoughts\n### Луминара\n- Completed mortal life must grant at least 10 Ink Feathers.\n- Completed mortal life must grant at least one NEW Soul Relic.\n- This evaluation grants 186 Ink Feathers, one new relic, and appends the completed-life summary to the chronicle.",
  "metaStateUpdates": {
    "inkFeatherChanges": {
      "add": 186,
      "reason": "Добровольное завершение жизни с выполненными квестами"
    },
    "soulRelicOperations": {
      "addRelic": {
        "relicId": "sr_guardian_luminara_village_oath_20260301",
        "name": "Клятва Защитника",
        "rarity": "Rare"
      }
    }
  },
  "loreCodexUpdates": [
    {
      "command": "add",
      "entry": {
        "entryId": "codex-luminara-village-oath",
        "title": "Клятва Защитника",
        "category": "history",
        "content": "Жизнь, завершенная ради защиты других, оставила отпечаток в Море Хаоса.",
        "discoveryContext": "Life Evaluation: Луминара раскрыла смысл реликвии после оценки завершённой жизни.",
        "discoveredAt": "2026-03-01T12:30:00Z",
        "incarnation": 4
      }
    }
  ],
  "achievementUnlocks": [
    {
      "achievementId": "ach-memory-of-duty",
      "name": "Память о Долге",
      "description": "Завершить жизнь, пожертвовав собой ради защиты других.",
      "category": "story",
      "rarity": "rare",
      "icon": "🛡️",
      "incarnation": 4,
      "unlockedAt": "2026-03-01T12:30:00Z",
      "progress": {
        "current": 1,
        "target": 1
      }
    }
  ]
}
```

### Canonical resulting state surfaces

```text
game_state/meta/soul_state.json
  - inkFeathers.current/total increased
  - at least one NEW soulRelics.stored[].relicId added
  - livesHistory already contains the completed life record from the transition flow

lore/chaos_sea/player_chronicle.json
  - append one new summary entry for the completed life

game_state/meta/achievements.json
  - unlock/update death-meta achievements if applicable
```

### Distributed files

```text
output/narrative_response.json          <- response
output/debug_logs.json                  <- gm_thoughts_markdown
game_state/meta/soul_state.json         <- meta reward results
lore/chaos_sea/player_chronicle.json    <- append completed-life summary
lore/codex_entries.json                 <- loreCodexUpdates
ready/turn_complete.json                <- correlated terminal success signal with exact metadata + filesModified
```

---

## Canonical CLI Rules Demonstrated

- `TriggerLifeEnd` starts the transition only.
- Life Evaluation reward is a separate accepted GM turn.
- Every completed mortal life must grant:
  - at least `10` Ink Feathers
  - at least one new Soul Relic with a new `relicId`
- Life Evaluation must append a new entry to `lore/chaos_sea/player_chronicle.json`.
- Achievement unlocks in `response` should use `[ACHIEVEMENT_UNLOCK: Achievement Name]`, but that marker accompanies and does not replace `achievementUnlocks` plus the resulting `game_state/meta/achievements.json` update.
