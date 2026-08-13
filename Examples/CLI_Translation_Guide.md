# CLI File Distribution Translation Guide

## ⚠️ CRITICAL FOR CLI AGENTS

**IMPORTANT**: All examples in the Examples/ directory show JSON API responses. In CLI mode, these responses must be DISTRIBUTED to multiple files according to Block_CLI_Operations.txt Rule CLI.3.

## 📋 How to Read Examples in CLI Mode

### **1. Process Examples as Usual**
- Read example scenarios and context
- Generate the SAME JSON response shown in examples
- Apply the SAME game logic and calculations

### **2. Then Distribute the Response**
After generating the JSON response, distribute each field to its corresponding file:

```
JSON Response Field               →    CLI File Location
├── "response": "text"            →    output/narrative_response.json.response
├── "dialogueOptions": [...]      →    output/interface_updates.json.dialogueOptions
├── "image_prompt": "..."         →    output/interface_updates.json.image_prompt
├── "gm_thoughts_markdown": "..." →    output/debug_logs.json.gm_thoughts_markdown
├── "UpdateInventory": [...]      →    game_state/inventory/items.json
├── "playerStatus": {...}         →    game_state/core/player_status.json
├── "UpdateNPCs": [...]           →    game_state/npcs/npc_core.json
├── "NPCCoreChanges": [...]       →    game_state/npcs/npc_core.json (validated non-carrier reducer input)
├── "currentLocationData": {...}  →    validated raw current-scene command; client composes game_state/world/current_location.json
├── "worldMapUpdates": {...}      →    validated raw map commands; client composes game_state/world/world_map.json
├── "metaStateUpdates": {...}     →    game_state/meta/soul_state.json
├── "UpdateGuardians": [...]      →    game_state/meta/guardians.json
└── ... (all other fields)       →    (see Block_CLI_Operations.txt CLI.3)
```

Mortal Location Materialization v1 is an important exception to a literal
field-to-file copy. The GM authors only complete raw routes
`current_scene_creation`, `world_map_creation`, and
`world_map_link_creation`, or exact permanent-ID lifecycle commands such as
`locationDiscoveryTransitions[]`, `linkRemovals[]`, `storageUpdates[]`,
`storagesToRemove[]`, `threatsToAdd[]`, `threatsToUpdate[]`,
`threatsToRemove[]`, and `completeThreatActivities[]`. A changed selection also
needs one exact open directed pre-turn current-to-destination link with no
requirements and player-known non-hidden visibility. The client validates
them, assigns permanent IDs/receipts, writes
`location_identity_index.json`, builds the current projection, and derives
navigation. knownExits and adjacencyMap are client-derived. Never translate a
display name, coordinate, ordinal, alias, case-folded value, or Unicode-normalized
value into location identity.
Storage updates preserve contents, storage removal requires emptiness, and the
owner/access snapshot changes use synchronized `newOwner`,
`newAuthorizedUsers`, and `newHasFullAccess`. The client assigns new threat
identity and owns the completion archive: keep embedded `activeThreats` empty
on raw location creation and use a separate null-ID `threatsToAdd[]` command
with exact `initialTargetLocationId` for a same-turn threat. Immutable
materialization section dispositions remain creation evidence.

For `mortal_location_materialization_repair`, the validated pre-turn baseline
has already been restored. Translation means full-turn resubmission of every
required response path for the same request, not an in-place patch of
`world_map.json`, `current_location.json`, or the identity index.

## 🔄 CLI Translation Process

### **STEP 1: Example Processing**
```
Read: Examples/E_Block_12.txt
Generate: Complete JSON response (exactly as shown)
```

### **STEP 2: Response Distribution**  
```
Parse: JSON response object
Distribute: Each field → appropriate game_state/ file
Leave: session/chat history surfaces to the client runtime
```

### **STEP 3: Client Signal**
```
Create fresh current-turn output/narrative_response.json / debug_logs.json and, when needed, output/interface_updates.json
Do NOT reuse stale output/*.json from a previous turn; these files are transient for the current request only
Create exactly one terminal signal:
- ready/turn_complete.json with sessionId, requestId, turnNumber, timestamp, status, filesModified
- OR ready/turn_error.json with sessionId, requestId, turnNumber, timestamp, status, error
```

## 📚 Example Translation

### **API Example (from E_Block_12.txt):**
```json
{
  "response": "Вы чувствуете магию перчаток...",
  "gm_thoughts_markdown": "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: none\n- Why relevant: This turn changes only player-side state.\n- Actors outside scope: scene NPCs, Guardians\n- Why outside scope: No structured actor updates are emitted.\n\n## Reasoning\n- Проверка силы: 6 (1+5).\n- Ход меняет только playerStatus.",
  "playerStatus": {
    "healthPercentage": "100%",
    "poisePercentage": "85%",
    "energyPercentage": "100%",
    "currentCondition": "Stable",
    "money": 0
  }
}
```

### **CLI Distribution:**
```bash
# Write output files:
echo '{"response": "Вы чувствуете магию перчаток...", "timestamp": "2026-03-01T12:00:00Z"}' > output/narrative_response.json
echo '{"gm_thoughts_markdown": "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: none\n- Why relevant: This turn changes only player-side state.\n- Actors outside scope: scene NPCs, Guardians\n- Why outside scope: No structured actor updates are emitted.\n\n## Reasoning\n- Проверка силы: 6 (1+5).\n- Ход меняет только playerStatus.", "timestamp": "2026-03-01T12:00:00Z"}' > output/debug_logs.json
echo '{"healthPercentage": "100%", "poisePercentage": "85%", "energyPercentage": "100%", "currentCondition": "Stable", "money": 0}' > game_state/core/player_status.json

# Signal terminal success:
echo '{"sessionId": "...", "requestId": "...", "turnNumber": 42, "timestamp": "2026-03-01T12:00:00Z", "status": "success", "filesModified": ["output/narrative_response.json", "output/debug_logs.json", "game_state/core/player_status.json"]}' > ready/turn_complete.json

# Or, if the turn fails terminally, write ready/turn_error.json instead:
echo '{"sessionId": "...", "requestId": "...", "turnNumber": 42, "timestamp": "2026-03-01T12:00:00Z", "status": "error", "error": "short terminal failure summary"}' > ready/turn_error.json
```

## ⚡ CLI-Specific Example Patterns

### **Pattern 1: Simple Action**
**API Response:**
- Generate single JSON with narrative + status changes
**CLI Action:**
- Write narrative → `output/narrative_response.json`
- Rewrite `output/*.json` fresh for the current request only; do not keep stale previous-turn payloads there
- Write status → `core/player_status.json`
- Read `stories/*.jsonl` for continuity when needed; do not hand-author `chat_log.json`

### **Pattern 2: Inventory Update**
**API Response:**
- Generate JSON with UpdateInventory array
**CLI Action:**
- Write items → `inventory/items.json`
- Write movements → `inventory/item_movements.json`
- Write weight changes → `player/weight_calc.json`

### **Pattern 3: NPC Interaction**
**API Response:**
- For a genuinely new NPC or true legacy promotion, generate the complete `UpdateNPCs`/`NPCsInScene` carrier plus relationship commands.
- For an ordinary existing NPC, preserve every actor-owned field in full carriers, generate the exact dedicated commands, and use bounded `NPCCoreChanges` only for supported profile, location, progression, setting-owned characteristic, faction affiliation, or locked/unrealized Fate Card definition changes.
**CLI Action:**
- Route complete carriers and `NPCCoreChanges` → `npcs/npc_core.json`; `NPCCoreChanges` is duplicate-sensitive, validates new Fate Cards through the full production nested skill/Combat Action contract, reduces only an entirely valid command into the exact existing actor, then consumes it. Do not hand-apply arbitrary fields.
- Write relationships → `npcs/npc_relationships.json`
- Keep dialogue in `output/narrative_response.json` / `output/interface_updates.json`

### **Pattern 4: Soul System Operation**
**API Response:**
- Generate JSON with metaStateUpdates
**CLI Action:**
- Write soul state → `meta/soul_state.json`
- Write guardians → `meta/guardians.json`
- Write soul quests → `quests/soul_quests.json`

## 🎯 Key Principles

### **1. Same Logic, Different Output**
- All game mechanics remain identical
- Same calculations, same narrative generation
- Only the final output method changes (files vs single JSON)

### **2. Atomic File Operations**
- Update related files together (see CLI.4 atomic groups)
- Use .backup files before modifications
- Rollback on any failure

### **3. Story Continuity**
- Use `stories/*.jsonl` as the main continuity source
- `game_state/history/chat_log.json` is client-maintained session metadata, not a GM-authored transcript
- Maintain continuity through canonical output/state files, not by hand-writing chat history

### **4. Cross-Reference Integrity**
- Validate NPC IDs exist across files
- Check item consistency between inventory and NPC files
- Ensure location IDs match between world files

## 🚨 Common CLI Mistakes to Avoid

❌ **DON'T**: Generate single JSON response and stop
✅ **DO**: Generate JSON response, then distribute to files

❌ **DON'T**: Modify files individually
✅ **DO**: Use atomic operations for related data

❌ **DON'T**: Hand-author `chat_log.json` as if it were a GM transcript
✅ **DO**: Rely on canonical output/state files and `stories/*.jsonl` for continuity

❌ **DON'T**: Ignore cross-file references
✅ **DO**: Validate data integrity across files

## 📝 Summary

**Examples show you WHAT to generate (the JSON response)**
**CLI rules show you WHERE to put it (the file distribution)**

**Think of CLI as a smart file organizer that takes your JSON response and sorts it into the right drawers of a filing cabinet!**
