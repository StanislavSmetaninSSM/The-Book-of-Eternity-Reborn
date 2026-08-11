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
