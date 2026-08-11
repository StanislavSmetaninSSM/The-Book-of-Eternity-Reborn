# Quickstart: Complete Mortal Item Materialization

**Issue**: [#1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)

This guide is a planning/verification reference. The implementation must update
the production GM rules and examples with the same current-schema contract.

## 1. Author a simple new player item

The GM writes a complete item through `UpdateInventory`. It supplies no
permanent ID or receipt:

```json
{
  "existedId": null,
  "creationRef": "new_item_turn_42_chalk",
  "name": "Кусочек белого мела",
  "description": "Небольшой сухой кусочек мела для коротких отметок на камне.",
  "image_prompt": "small worn piece of white chalk on dark stone, realistic low fantasy item",
  "type": "Бытовой предмет",
  "group": "Инструменты",
  "quality": "Common",
  "rarity": "Common",
  "price": 1,
  "count": 1,
  "weight": 0.02,
  "volume": 0.01,
  "durability": "100%",
  "maxDurability": "100%",
  "bonuses": [],
  "effects": [],
  "structuredBonuses": [],
  "combatEffect": [],
  "customProperties": [],
  "mechanicalSummaryAuthority": null,
  "mechanicalSummaryUnresolvedReason": null,
  "equipmentSlot": null,
  "accessoryForSlot": null,
  "requiresTwoHands": false,
  "isContainer": false,
  "capacity": null,
  "containerWeight": null,
  "weightReduction": null,
  "contentsPath": null,
  "isConsumption": false,
  "textContent": null,
  "journalEntries": [],
  "isSentient": false,
  "unreadableReason": null,
  "sealedReason": null,
  "lockedReason": null,
  "disassembleTo": null,
  "ownerBondLevelCurrent": null,
  "ownerBondLevelMax": null,
  "fateCards": [],
  "questLinks": [],
  "materialization": {
    "schemaVersion": 1,
    "materializationId": "mat_item_turn_42_chalk",
    "realm": "Mortal",
    "route": "player_acquisition",
    "sourceTurn": 42,
    "sourceAuthority": {
      "kind": "turn_outcome",
      "authorityId": "turn_42"
    },
    "creationRef": "new_item_turn_42_chalk",
    "state": "complete",
    "sections": {
      "presentation": { "state": "populated", "reason": null },
      "physical": { "state": "populated", "reason": null },
      "mechanics": { "state": "empty_by_design", "reason": "Мел не даёт самостоятельного механического бонуса." },
      "equipment": { "state": "empty_by_design", "reason": "Мел держат при использовании, но не экипируют." },
      "container": { "state": "empty_by_design", "reason": "Кусочек мела ничего не вмещает." },
      "consumption": { "state": "empty_by_design", "reason": "Мел не является расходником с самостоятельным эффектом." },
      "readableOrSentient": { "state": "empty_by_design", "reason": "На самом кусочке нет читаемого текста и собственного голоса." },
      "craftingAndDisassembly": { "state": "empty_by_design", "reason": "Этот кусочек не задаёт рецепта или полезной разборки." },
      "bondsAndFateCards": { "state": "empty_by_design", "reason": "Обычный мел не образует связь и не имеет Карт Судьбы." },
      "questRole": { "state": "empty_by_design", "reason": "Мел не связан с заданием." },
      "provenance": { "state": "populated", "reason": null },
      "ownershipAndPlacement": { "state": "populated", "reason": null }
    }
  }
}
```

Raw validation rejects this package if any empty field or reason is missing.

## 2. Client sealing result

After raw validation, the client:

1. assigns one permanent ID and writes it equally to `itemId` and
   `existedId`;
2. rewrites same-turn references to that ID;
3. removes top-level `creationRef`;
4. embeds an immutable receipt;
5. adds one active index entry.

The resulting protected fragments resemble:

```json
{
  "existedId": "itm_01J...",
  "itemId": "itm_01J...",
  "materializationReceipt": {
    "schemaVersion": 1,
    "receiptId": "mirec_01J...",
    "itemId": "itm_01J...",
    "materializationId": "mat_item_turn_42_chalk",
    "acceptedAtTurn": 42,
    "creationRef": "new_item_turn_42_chalk",
    "instanceKind": "root",
    "parentItemIds": [],
    "seal": "sha256:..."
  }
}
```

The GM never writes this fragment.

## 3. Author a mechanic-bearing craft output

Use the same complete shape, with these route/evidence differences:

- `route: "craft_output"`;
- `sourceAuthority.kind: "craft_request"`;
- `sourceAuthority.authorityId` equals the exact pending craft request ID;
- `mechanics.state: "populated"`;
- every mechanical line in `bonuses` has matching
  `structuredBonuses`/`combatEffect` authority;
- `craftingAndDisassembly.state: "populated"` when output/ingredient or
  disassembly evidence applies;
- the client transition records exact consumed input IDs and quantities.

Ingredient updates/removals and output creation are one atomic package. Do not
reuse an ingredient ID or call the output a split.

## 4. Transfer an existing item

Resolve the exact permanent ID from current canonical state. The transition
must remove one source copy and add the same semantic object—with unchanged
envelope and receipt—to one destination. The client updates only carrier and
transition authority in the index.

For a local storage/vehicle move, the service receives a display selection but
converts it to exact `itemId` before writing. Name-only authority is rejected.

## 5. Split and merge

Split:

- source `count=10`, requested child count `3`;
- source remains the selected ID at `count=7`;
- child receives a new ID and split-derived receipt at `count=3`;
- both index entries retain the root origin materialization.

Merge:

- select the survivor explicitly;
- require complete compatible semantics and same root carrier/container path;
- survivor count becomes the exact sum;
- survivor receipt does not change;
- contributor entries retire as `merged` and point to the survivor.

## 6. Expected rejection: receipt-less current item

This is invalid current canonical state:

```json
{
  "items": [
    {
      "existedId": "item_old_fixture",
      "itemId": "item_old_fixture",
      "name": "Старый тестовый предмет"
    }
  ]
}
```

It is not promoted or auto-completed. A positive repository fixture must be
migrated to the complete schema; a negative test labels this object as
intentionally malformed.

## 7. TDD verification rhythm

Run the smallest red/green filter after each implementation slice:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemMaterializationContractTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests"
```

At a meaningful checkpoint run one Fast control. Because GM examples and the
manifest change, run focused documentation coverage and FullValidation. Because
accepted-turn normalization/rollback changes, run LifecycleIntegration once.
Immediately before merge run one clean-candidate PreMerge; do not repeat Fast
right before it.

## 8. Manual projection check

Inspect the mundane and mechanic-bearing accepted items in both existing
console and browser detail flows. The player should see their name,
description, physical values, and legitimate mechanics, but never:

- `materialization` or `materializationReceipt`;
- `creationRef`, receipt/index IDs, seal, lineage, or carrier coordinate;
- file paths, validation codes, repair packets, or GM/client authority terms.

## 9. Baseline evidence

The pre-production-code baseline on 2026-08-11 was clean:

| Selection | Result | Tests | Timeout | Cleanup | Result directory |
| --- | --- | ---: | --- | --- | --- |
| `CanonicalStateNormalizerTests.NormalizeAccumulatedStateAsync_StripsPlayerFacingItemJournalTurnAnchors` | PASS | 1/1 | false | complete | `TestResults/test-lanes/20260811-111912-622-33464-28883a6e924f43c38537a1d57aedd539-focused` |
| `BrowserInventoryManagementTests` | PASS | 13/13 | false | complete | `TestResults/test-lanes/20260811-111936-897-6780-bd98b778f37741fe9fb3bd5beb5b9190-focused` |

The first normalizer invocation used the file suffix as if it were a class
name (`CanonicalStateNormalizerTests.Inventory`) and correctly failed the lane
guard with zero discovered tests. Repository inspection showed the actual
partial class/method name; the corrected exact selector above passed 1/1. The
zero-discovery invocation is diagnostic evidence, not an accepted baseline.

## 10. Player sealing and rollback evidence

The first player-sealing test failed before implementation because
`UpdateInventory` remained in the canonical file. The rollback test then
failed at compile time until the one-lease refresh transaction existed.

| Selection | Result | Tests | Timeout | Cleanup | Result directory |
| --- | --- | ---: | --- | --- | --- |
| `CanonicalStateNormalizerTests.Normalize_PlayerCreation_SealsOneCanonicalItemAndIndexEntry` (RED) | FAIL as expected | 0/1 | false | complete | `TestResults/test-lanes/20260811-130547-546-45312-3cc528fe862d4c42b345dd02f9a8230b-focused` |
| same selection (GREEN) | PASS | 1/1 | false | complete | `TestResults/test-lanes/20260811-130930-334-24832-ee218881a25048fbab39179bbe79d9e1-focused` |
| rollback transaction compile gate (RED) | FAIL as expected | build gate | false | complete | `TestResults/test-lanes/20260811-131142-659-17080-baf2530ca9d845efaa21617fdc3f0f04-focused` |
| injected write-failure rollback (GREEN; test was subsequently renamed for precision) | PASS | 1/1 | false | complete | `TestResults/test-lanes/20260811-131245-830-47096-c9a74884b67246e1b609bbdcfb3fa082-focused` |
| normalizer rollback selection, including actual post-seal rejection | PASS | 13/13 | false | complete | `TestResults/test-lanes/20260811-131400-594-18360-330410f0b0e947188637ccbe6c7bba43-focused` |
| normalizer, snapshot, and Mortal item validation checkpoint | PASS | 31/31 | false | complete | `TestResults/test-lanes/20260811-131515-670-21520-7ef9cf4917804c0b95a158246000e671-focused` |
| same-turn container/equipment reference rewrite | PASS | 1/1 | false | complete | `TestResults/test-lanes/20260811-131724-067-30024-8594841a501e451da8a11cf335e8f693-focused` |
| complete `CanonicalStateNormalizerTests` regression control | PASS | 224/224 | false | complete | `TestResults/test-lanes/20260811-132110-187-18448-01cac83525424e7c96cd59a74b303e22-focused` |
| complete Mortal item materialization validation selection | PASS | 16/16 | false | complete | `TestResults/test-lanes/20260811-132343-846-32956-323931797d48455da4561e108afc4955-focused` |

The refresh transaction holds one `CanonicalWriteLease`, captures exact byte
before-images for the shared normalizer/QTE/browser rollback contour, and
restores every changed or newly created tracked path on write exceptions or
post-seal validation errors.
