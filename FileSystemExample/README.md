# 📁 FileSystemExample - CLI Game Architecture Demo

## 🎯 Overview

This directory demonstrates the CLI file-based architecture for "The Book of Eternity Reborn". It shows how the game operates using distributed JSON files instead of a single API response.

## 📋 Structure

```
game_session/
├── input/                    # CLI monitoring entry point
│   └── turn_request.json     # Turn processing trigger (DEMO)
├── game_state/               # Distributed game data and state examples
│   ├── core/                 # Narrative, player status, manifests (4 files)
│   │   ├── player_status.json (DEMO)
│   │   └── system_mods.json (DEMO CANONICAL ACTIVE MOD MANIFEST)
│   ├── player/               # Player character data (5 files)  
│   ├── inventory/            # Item system (5 files)
│   │   └── items.json (DEMO)
│   ├── world/                # World state (4 files)
│   ├── quests/               # Quest management (3 files)
│   ├── npcs/                 # NPC system (11 files)
│   ├── combat/               # Combat state (3 files)
│   ├── factions/             # Faction system (6 files)
│   ├── meta/                 # Soul System & Guardians (4 files)
│   │   ├── soul_state.json (DEMO)
│   │   └── guardians.json (DEMO)
│   ├── afterlife/            # Afterlife display-state examples
│   │   ├── afterlife_chronicles.json (DEMO)
│   │   ├── afterlife_active_threats.json (DEMO)
│   │   ├── afterlife_global_flags.json (DEMO)
│   │   ├── afterlife_story_outline.json (DEMO)
│   │   └── entity_profiles/ (DEMO)
│   ├── shining_abode/        # Shining Abode display-state examples
│   │   └── faction_chronicles.json (DEMO)
│   ├── chaos_sea/            # Chaos Sea display-state examples
│   │   └── guardian_politics.json (DEMO)
│   ├── misc/                 # Vehicles & storage (2 files)
│   └── control/              # Game flow control (3 files)
│       └── incarnation_world_setup.json (DEMO CLIENT-AUTHORED PENDING SETUP)
├── lore/                     # Lore and active world dossiers
│   ├── chaos_sea/            # Persistent meta-lore (across incarnations)
│   │   └── cosmology.json (DEMO)
│   ├── current_world/        # Current incarnation world lore
│   │   ├── world_setting.json (DEMO)
│   │   ├── world_directives.json (DEMO CLIENT/PLAYER-AUTHORED DOSSIER)
│   │   └── world_directives_draft_example.md (TEXT DRAFT EXAMPLE)
├── world_profiles/           # Reusable mortal-world templates
│   ├── example_world_profile.json (DEMO JSON PROFILE)
│   ├── example_world_profile.txt (DEMO TEXT PROFILE)
│   └── example_world_profile.md (DEMO MARKDOWN PROFILE)
├── mods/                     # Global system mods
│   ├── example_system_mod.json (DEMO JSON MOD)
│   ├── example_system_mod.txt (DEMO TEXT MOD)
│   └── example_system_mod.md (DEMO MARKDOWN MOD)
├── validator_fixtures/       # Broken/fixed validator contract examples
│   ├── terminal_narrative/
│   ├── lifecycle_trigger_life_end/
│   ├── faction_full_object/
│   ├── item_journals/
│   └── client_owned_world_setup/
├── saves/                    # Save/load system
│   ├── autosaves/            # Automatic saves
│   ├── manual_saves/         # Player manual saves
│   │   └── first_character_save_metadata.json (DEMO)
│   └── checkpoint_saves/     # Life transition saves
├── output/                   # Client communication
│   ├── narrative_response.json (DEMO)
│   ├── interface_updates.json (OPTIONAL, omitted when the turn has no dialogue/image payload)
│   └── debug_logs.json (DEMO)
└── ready/                    # Processing completion signals
    ├── turn_complete.json (DEMO)
    └── turn_error.json (OPTIONAL example surface, not included in minimal demo)
```

## 🔧 How It Works

### **1. CLI Agent Workflow:**
1. **Monitor** `input/turn_request.json` for new turns
2. **Load** all relevant files from `game_state/` directories  
3. **Process** using existing rule blocks (Block_1.txt - Block_32.txt)
4. **Distribute** results to appropriate `game_state/` files
5. **Signal** completion via exactly one terminal file: `ready/turn_complete.json` or `ready/turn_error.json`

### **2. Client Workflow:**
1. **Create** `input/turn_request.json` with player action
2. **Wait** for `ready/turn_complete.json` or `ready/turn_error.json`
3. **On success:** read required `output/narrative_response.json` and `output/debug_logs.json`, then optional `output/interface_updates.json`
4. **On success:** update UI from `dialogueOptions` / `image_prompt` if `output/interface_updates.json` exists
5. **On error:** read `ready/turn_error.json` as the authoritative failure payload
6. **Reload** changed sections from `game_state/` files after a successful turn

### **3. Data Persistence:**
- Each JSON file represents a specific game system category
- Files are updated atomically to prevent corruption
- Cross-references between files maintain data integrity
- Backup system ensures rollback on failures

## 📁 Demo Files Included

### **Input Example:**
- `turn_request.json` - Sample player action (осматриваю местность)

### **Output Example:**  
- `narrative_response.json` - Sample narrative response in Russian
- `interface_updates.json` - Optional dialogue/image payload for the current turn; omitted in the minimal success demo when not needed
- `debug_logs.json` - Required GM reasoning/debug markdown for an accepted turn; included in the minimal success demo pack
- `turn_complete.json` / `turn_error.json` - Terminal success/failure signals; only `turn_complete.json` is checked into the minimal success demo

### **Game State Examples:**
- `core/player_status.json` - Player health, poise, energy, conditions
- `inventory/items.json` - Player items and equipment slots  
- `meta/soul_state.json` - Soul System progression and Soul Relics
- `meta/guardians.json` - Guardian relationships and reputation
- `afterlife/afterlife_chronicles.json` - Afterlife external-memory examples with archived events, current consequences, and unresolved private threads
- `afterlife/afterlife_active_threats.json` - Visible and hidden persistent afterlife threat examples for display filtering
- `afterlife/afterlife_global_flags.json` - Visible, hidden, and GM-only afterlife global flag examples
- `afterlife/afterlife_story_outline.json` - Private afterlife Writer's Room planning example
- `afterlife/entity_profiles/` - Split afterlife entity profile examples covering actor goals, Fate Cards, masks, and relationship gates
- `shining_abode/faction_chronicles.json` - Shining faction chronicle and political memory examples with visible and hidden entries
- `chaos_sea/guardian_politics.json` - Chaos Sea Guardian politics examples with known, hidden, and GM-only surfaces
- `core/system_mods.json` - Canonical active system mods manifest for the GM
- `control/incarnation_world_setup.json` - Pending pre-incarnation world setup

### **Lore System Examples:**
- `lore/chaos_sea/cosmology.json` - Universal laws and cosmic structure
- `lore/current_world/world_setting.json` - Current incarnation world (Валендрия)
- `mods/example_system_mod.json` - Example global system mod in canonical JSON form
- `mods/example_system_mod.txt` - Example global system mod as simple plain text
- `mods/example_system_mod.md` - Example global system mod as Markdown
- `world_profiles/example_world_profile.json` - Example reusable mortal-world profile in canonical JSON form
- `world_profiles/example_world_profile.txt` - Example reusable mortal-world profile as simple plain text
- `world_profiles/example_world_profile.md` - Example reusable mortal-world profile as Markdown
- `lore/current_world/world_directives.json` - Example canonical active world dossier materialized from player setup
- `lore/current_world/world_directives_draft_example.md` - Human-readable draft example showing how to think about current-world directives before or while editing the JSON dossier
- `validator_fixtures/` - Broken/fixed validator scenarios with expected error manifests

### **Save System Examples:**
- `saves/manual_saves/first_character_save_metadata.json` - Save file metadata

## 🎯 Benefits

1. **Modularity**: Different systems don't interfere with each other
2. **Performance**: Only changed sections need to be updated/loaded
3. **Debugging**: Easy to inspect specific game state categories
4. **Scalability**: Can add new systems by creating new folders/files
5. **Recovery**: Individual system failures don't corrupt entire state
6. **Lore Management**: Persistent meta-lore across incarnations + world-specific lore
7. **Customization**: Global system mods, reusable world profiles, and player-authored world directives without replacing the core system
8. **Save System**: Comprehensive save/load with versioning and automatic backups
9. **Validator Fixtures**: Ready-made broken/fixed contract examples for debugging validator behavior

## 🧭 Practical Authoring Guidance

- If you want a **global rule layer** that affects the whole game, create a file in `mods/`.
- If you want a **reusable template for a future mortal world**, create a file in `world_profiles/`.
- If you want to **guide one конкретную текущую жизнь**, edit `lore/current_world/world_directives.json`.
- For `mods/` and `world_profiles/`, both plain text (`.txt` / `.md`) and JSON are supported.
- For the active current-world dossier, the canonical runtime file is JSON: `world_directives.json`.
- The sibling `world_directives_draft_example.md` is only a human drafting example, not the canonical runtime file.
- `validator_fixtures/` is the recommended place to study typical contract failures and their corrected versions.

## 📖 Integration with Existing Rules

**CRITICAL**: All existing rule blocks (Block_1.txt - Block_32.txt) work unchanged!

- Same game logic, calculations, formulas, and narrative rules
- Same JSON command structures (metaStateUpdates, UpdateGuardians)  
- Same Russian language requirements and mathematical precision
- Only the input/output mechanism changes from API to files

The CLI system is a pure **ADAPTER LAYER** preserving all existing game design.

## 🚀 Next Steps

1. **Implement CLI Agent** using Rules/Block_CLI_Operations.txt
2. **Create File Schemas** for validation
3. **Test Atomic Operations** to ensure data integrity
4. **Build Client Interface** that monitors the file system
