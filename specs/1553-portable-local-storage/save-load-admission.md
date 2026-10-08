# Save/load admission clarification — read-only, 2026-10-01

## Result

Keep the existing save library outside load's replacement/deletion set. Reject incoming payload files at normalized `saves` or below `saves/`; preserve harmless directory-only archive entries as ignored structure. Preflight file/directory conflicts before any live mutation. Keep archive config optional: present means validate and import its exact bytes; absent means no imported settings, preserving the admitted live config bytes/absence rather than deleting them or inventing defaults.

The implementation direction requires preservation of existing manual/autosave/checkpoint ZIP bytes through successful load and cold recovery under the approved accidental-loss goal. This addendum does not reopen that decision. It separates existing behavior from the smallest recommended preparation rules.

Source-only investigation; no tests/apps executed. Read alongside [the full cutover map](save-load-cutover.md); this note distinguishes current source behavior from the recommended admission rules.

## Source identity

Feature checkout, branch `1553-cross-platform-runtime`; observed HEAD `00e2ce0b68394a3261bf2756f1f3e02b7118e81a`. B3b manager/catalog/test work was active; this is not a clean-source or runtime claim.

Stable files below have no diff from the original mapping checkpoint `41d9104076d59fabacf93c3b1be3cc8d5ccc238f`:

- `Services/SaveLoadService.cs`: blob `8a007d92637bb465edb514a36dfd9da02b3426aa`
- `Core/StateManager.cs`: `67190fae6f7ce651d359cb6738ec23327f90da24`
- `Core/StateManager.LocalSettings.cs`: `561f1c2788f0b6965f087b2d172b5d662ad78b13`
- `Core/TrustedLocalFilePublication.cs`: `25c191d09ba78ad3008a56e3783f0b4f72ed7db3`
- `BookOfEternityClient.IntegrationTests/SaveLoadServiceTests.cs`: `0f3d81ecf1ff1a3dcc1dcf9b4992b0b4ce10e9a5`

Additional inspected working files: `Core/FileSystemManager.cs` blob `ea2037c46788a99b051e7f892fd40edb9f156f7d`; `Core/TrustedLocalFileScope.cs` blob `e87597b5e92c63a3fc343b2a612b788615039702`; `Configuration/GameSettings.cs` blob `23fa4c947e29e74db80ad87fb7e438891cb7bf94`. Production paths otherwise start at `BookOfEternityClient/`.

## 1. Incoming save-library entries

### What the current source permits

- Producer `SaveLoadService.SaveGameAsync:186–275` archives selected game state/lore/mods/world_profiles/stories/images/output and optional config. It never includes `saves/`; `AddDirectoryToArchive:704–751` emits only files, not empty directory entries. A current client-produced archive therefore has no nested save library.
- Loader validation `ValidateArchiveStructureAsync:1176–1299` requires current soul/resource roots, rejects duplicate normalized file names case-insensitively, and validates complete manifest coverage/hash/length when a manifest exists. It has no durable-root allowlist or `saves/` exclusion. A sandboxed `saves/manual_saves/x.zip` payload can pass these checks if the rest of the archive is valid and its manifest covers it; nested ZIP bytes are not recursively interpreted.
- Extraction `LoadGameAsync:411–449` skips directory entries, the integrity manifest and the existing ephemeral set. `IsEphemeralArchivePath:1644–1649` / sets `:63–91` do not include `saves`. Thus a permitted save-library payload is extracted as an ordinary durable file. This is validation/extraction inference, not a successful current load observation; the previously mapped legacy-root admission conflict still applies.
- `FileSystemManager.RequiredDirectories:483–509` requires `saves/manual_saves`, `saves/autosaves`, `saves/checkpoint_saves`. A bare regular-file `saves` entry is not rejected by archive structure validation but would conflict with required structure later.
- No incoming-`saves/` positive test or conflict policy was found in `SaveLoadServiceTests`; its save path references select source ZIPs, not nested archive payloads. Existing integrity/case-collision tests do not establish nested-library import as a required contract.

### Smallest preparation rule

Reserve normalized archive file paths equal to `saves` or starting `saves/` (component boundary, ordinal-ignore-case consistent with archive collision policy). Apply after existing path normalization, so slash variants and in-root aliases cannot bypass it. Reject before extraction/lifecycle mutation, with a clear admission error; do not silently overwrite, rename, merge or ignore a manifested payload. Continue counting/checking the complete raw archive budget; do not relax manifest validation for other entries.

Leave the live `saves` subtree out of the ordinary replacement/deletion inventory, including the selected source archive. Preserving its other contents as well is the smallest no-pruning rule; no extension-based cleanup is needed. Required-directory/link/type checks remain, without following a linked subtree. Directory-only ZIP placeholders remain non-authoritative and never create or remove library members. This intentionally tightens permissive crafted-archive admission, while accepting archives the current producer actually emits. It changes no ZIP/manifest schema and adds no journal.

## 2. File/directory shape

### What the current source permits

- Budget validation `:827–835` accepts zero-byte directory descriptors and rejects directory payload bytes. Structure validation `:1185–1198` and extraction `:411–415` ignore directory entries before filename normalization. A directory-only placeholder has no resulting namespace authority. Do not infer that an archive with `a/` and file `a` currently requests two materialized objects.
- The file dictionary catches equal normalized filenames, not file/ancestor conflicts: payload files `a` and `a/b` can both pass structure validation (including correct manifest hashes). Extraction then encounters a real create-directory/create-file type conflict in either order, before lifecycle acquisition (`:440–449`, `:471`; manager `CreateLoadDirectory:4129+`, `WriteLoadTransactionFileCoreAsync:4200+`). There is no explicit prefix-trie admission today.
- A file at a required directory (for example `lore/current_world`) can pass archive validation and extraction, then fail `EnsureDirectoryStructure` after live publication (`SaveLoadService:495`; manager `:576–594`). Bring that rejection forward.
- Old whole-tree swapping (`SaveLoadService:479–488`) does not compare non-required live shapes with incoming shapes. In principle it permits live file `lore/custom` → incoming `lore/custom/entry.json`, and the reverse, if all other validation succeeds. These shapes can arise between two valid producer snapshots. No direct positive/negative shape-transition test was found. This is source inference and does not bypass the known admission blocker.
- B1 `TrustedLocalFileChange` stores bytes/absence (`TrustedLocalFilePublication:8–9`), validates every file path before intent (`:130–153`, `:312–320`) and all members before recovery (`:245–258`). Scope `ValidateFile:99–109` rejects a directory at a file target; `ValidateDirectories:184–196` rejects a file ancestor. Absence is not a directory. Merely ordering deletion before writing cannot satisfy B1 recovery across a shape conversion.

### Smallest preparation rule

Build the normalized incoming file inventory first. Reject a file that is another selected file's ancestor, or that equals/is an ancestor of a required structural directory. Keep directory-only entries non-authoritative; existing path/budget admission still applies. Under lifecycle → replacement canonical lease, preflight every incoming destination and ancestor against the current non-followed namespace before producing any mutation: file→directory and directory→file collisions block the load, even if the conflicting old file/subtree would otherwise be deleted. Retain harmless empty directories.

This is an explicit destination-admission limit of the bounded file-only cutover, not a claim that the archive is corrupt or that all previously possible whole-tree shape replacements are preserved. Supporting those conversions would need deliberate extension of the decision/recovery model; do not improvise unjournaled pruning, a second journal, or changes to B1's file-image schema in this slice.

## 3. Absent config

### Current valid saves/tests

- Producer `:265–275` reads config bytes and simply omits the entry when absent. Structure validation has no required config or config-content check. Present config is treated as arbitrary durable bytes until the later settings load.
- Integration test constructor `SaveLoadServiceTests:23–71` creates current soul/resource/owner roots and runtime `GameSettings`, but never creates config or calls B2 bootstrap. Positive `SaveAndLoad_PreservesUnifiedResourceRootsAndExcludesTransientCommand:351–394` and `LoadGameAsync_UsesIntegrityManifestWithoutAddingItToCanonicalSession:1942–1953` use current `SaveGameAsync`, then expect successful load. Consequently config omission is real current producer behavior, not only a fixture named “legacy”. These are inspected assertions, not newly executed passes.
- Old post-load `StateManager.LoadSettingsJson:75–91` returns on absence; malformed content is caught/logged and also leaves runtime values unchanged. Whole-tree replacement nevertheless removes an old disk config if the archive lacks it. On a later real client bootstrap `StateManager:50–60`, absence creates config from that process's supplied settings; invalid existing bytes instead fail strict admission. Thus old successful absence handling is not durable preservation of existing choices.
- The explicit config rollback test `LoadGameAsync_WhenCommitJournalWriteFails_RestoresDiskAndRuntimeSnapshot:2463–2511` asserts old Russian settings after a failed load of English config. It does not require deleting old config on a successful load of a config-less archive. No explicit such deletion assertion was found.
- Normal clients establish config through `GameEngine.cs:191` and `WebUi/LocalWebUiHost.cs:87` calling B2 bootstrap before menu use. The both-absent service fixtures instead begin with a fresh `GameSettings` (`SaveLoadServiceTests:68–71`). `StateManager.CaptureRuntimeSnapshot:571–575` copies mutable runtime settings; it is not an independent proof of persisted config authority. Console preview explicitly has accepted/draft/effect lifetimes (`ConsoleSettingsPreview.cs:7–39,55–71`). No separate committed absent-config baseline was found on the inspected save/load path.

### Smallest compatible preparation rule

- Present archive config: reuse the existing B2 settings decoder (`StateManager.LocalSettings:27–40`) in detached preparation; reject malformed/null/non-object/duplicate settings before the live decision. Preserve the exact admitted bytes for publication. Apply the existing `GameSettings.ApplyLoadedValues:150–177` runtime rules only after the committed decision; do not reserialize the archive bytes or silently replace invalid settings with defaults.
- Absent archive config: treat as “no imported settings”. Exclude current config from load's deletion set, preserving its exact bytes/absence. If live config exists, it must satisfy current B2 admission; bind post-load runtime to the resulting committed canonical config, respecting existing runtime/preview ownership. Do not retain an arbitrary pre-load settings snapshot, preview, draft or save-list metadata as authority. If both configs are absent, retain disk absence; any runtime result needs an explicit existing absent-config baseline from the caller, not an inference from mutable `Settings`. The low-level fixtures' fresh supplied baseline is evidence of their setup, not a general preview policy. Normal clients already bootstrap config; the exact both-absent caller/baseline contract still needs focused specification/verification. Do not synthesize a new config inside load, replay settings normalization, or infer a historical format/version.

This preserves the current optional archive contract and avoids losing saved choices at restart. Preserving an old disk config in this case is a proposed accidental-loss correction, not a description of the old whole-tree path. It introduces no compatibility parser or default fallback, and does not make unrelated whole-normalizer atomicity part of this migration.

## Focused proof additions

- Current-produced manifested archive with no config: load onto admitted nondefault config, verify exact config bytes and committed runtime choices through commit and fresh-process recovery, including a differing draft/preview; cover the both-absent low-level service case against its explicit baseline without manufacturing defaults or importing transient state
- Present invalid/null/duplicate config: fail before live mutation, preserve complete current bytes/generation/library; valid BOM-bearing config retains exact bytes
- Incoming normalized `saves/...` payload (new name and same existing name, manifested/unmanifested, slash/case/in-root alias variants) and bare `saves` file: reject before mutation; empty directory-only placeholders create no library writes
- Existing manual/autosave/checkpoint ZIPs, including selected ZIP: same bytes after successful load and cold B1 committed/uncommitted recovery, alongside actual loaded state
- Incoming file/ancestor collision in both orders, required-directory file, live file→incoming subtree, live empty/nonempty directory→incoming file: fail before first live change; ordinary same-shape replacement and harmless empty residue remain admitted
- Retain raw ZIP preflight/budgets, manifest coverage/tamper/case collision and the existing config rollback selector. New assertions need focused portable-load ownership, not the complete historical save/load category

These are source-selected RED scenarios only. The separate scratch namespace / legacy-original-handler-or-block requirements and committed-vs-uncertain outcome mapping remain exactly as in the main map.
