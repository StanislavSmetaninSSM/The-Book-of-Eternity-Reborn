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

## 11. All creation routes and US1 checkpoint evidence

The complete-creation slice now seals all eight approved routes with one
turn-wide ordinal `creationRef` map. Exact companion references are rewritten
across player/NPC equipment, nested containers, resources, text, journals,
bonds, recipes, quest rewards, and storage. The validator rejects forged route
authority, unresolved or cross-owner references, duplicate quest authority,
and receipt/index disagreement before acceptance.

Representative red/green evidence from the route and companion TDD loop:

| Selection | Result | Tests | Result directory |
| --- | --- | ---: | --- |
| orphan companion references (RED) | FAIL as expected | 0/2 | `TestResults/test-lanes/20260811-140919-606-11444-5e20ceb66f4f45ffad4af889c4be5c80-focused` |
| orphan companion references (GREEN) | PASS | 2/2 | `TestResults/test-lanes/20260811-141259-393-13516-2d16d5405a194e19a74f97c1eda5dff0-focused` |
| inline references and missing parent (RED) | FAIL as expected | 0/4 | `TestResults/test-lanes/20260811-141819-825-39448-550d405fb2c44940b02fd15883120183-focused` |
| inline references and missing parent (GREEN) | PASS | 4/4 | `TestResults/test-lanes/20260811-142202-794-26804-d86b19aa6228478bb0f74049e4b744a9-focused` |
| craft/trade authority (RED) | FAIL as expected | 0/2 | `TestResults/test-lanes/20260811-143753-670-28648-dc4f15689cd24ec88766ce5512b86666-focused` |
| craft/trade authority and route matrix (GREEN) | PASS | 10/10 | `TestResults/test-lanes/20260811-143914-419-28804-c8e051e0be5846e7a104b59a0f067958-focused` |
| historical quest reward authority (RED) | FAIL as expected | 0/1 | `TestResults/test-lanes/20260811-144107-484-37708-1ba3b18e8a444fd4984bde63870264c6-focused` |
| historical quest reward authority (GREEN) | PASS | 3/3 | `TestResults/test-lanes/20260811-144146-735-38956-d77fb2d483424c0f852c3b997c8fc0c4-focused` |
| duplicate quest creation authority (RED) | FAIL as expected | 0/1 | `TestResults/test-lanes/20260811-144252-066-47908-dd87ade6f20440748047ca34929ccd77-focused` |
| duplicate quest creation authority (GREEN) | PASS | 2/2 | `TestResults/test-lanes/20260811-144327-350-30544-c0d01a5ffda54638bd8ed456f43db727-focused` |
| immutable old `creationRef` reuse (RED) | FAIL as expected | 0/1 | `TestResults/test-lanes/20260811-144445-710-14516-9f1e5019bd75405ba7bd2634e485650d-focused` |
| immutable old `creationRef` reuse (GREEN) | PASS | 9/9 | `TestResults/test-lanes/20260811-144520-543-28184-004057220e114bc3bdfd5a4ac6b45ac7-focused` |
| unrelated NPC equipment preservation (RED) | FAIL as expected | 0/1 | `TestResults/test-lanes/20260811-144702-571-18444-354d803d4a024286a42c028d40404b66-focused` |
| NPC equipment command reconciliation (GREEN) | PASS | 3/3 | `TestResults/test-lanes/20260811-144749-101-28680-6e9fc587887941ef87ff682b0b73409a-focused` |
| cross-owner NPC equipment reference (RED) | FAIL as expected | 0/1 | `TestResults/test-lanes/20260811-145103-105-6720-4f92d489a0884362bfaf6a43e078df68-focused` |
| owner and inline reference validation (GREEN) | PASS | 7/7 | `TestResults/test-lanes/20260811-145354-191-40932-da22c55b371e43c8a4673ce8e4be393b-focused` |
| unknown NPC destination parent (RED) | FAIL as expected | 0/1 | `TestResults/test-lanes/20260811-145803-899-38900-b7fc066d4da6410d9259e3a242429cfa-focused` |
| NPC destination parent validation (GREEN) | PASS | 2/2 | `TestResults/test-lanes/20260811-145838-838-34736-df3042422f274b5ab921795351324e40-focused` |

The accepted US1 checkpoint commands were:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~MortalItemMaterializationContractTests|FullyQualifiedName~MortalItemCarrierCatalogTests|FullyQualifiedName~ValidationPhaseSelectionTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests|FullyQualifiedName~CanonicalStateNormalizerTests|FullyQualifiedName~MortalBootstrapValidationTests|FullyQualifiedName~PendingTurnSnapshotTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

`CanonicalStateNormalizerTests` is the actual partial-class test name; the
planning suffix `.MortalItems` is a source-file label, not a discoverable test
class. The corrected selector prevents a false zero-discovery checkpoint.

| Checkpoint | Result | Tests | Timeout | Cleanup | Result directory |
| --- | --- | ---: | --- | --- | --- |
| complete normalizer control | PASS | 224/224 | false | complete | `TestResults/test-lanes/20260811-150733-959-47944-cfdc4d9602f24caeab3de10252b322f5-focused` |
| complete materialization control | PASS | 67/67 | false | complete | `TestResults/test-lanes/20260811-151017-091-21616-810b08aefa124ce08ad0bd2446a2fd8e-focused` |
| post-review materialization control | PASS | 68/68 | false | complete | `TestResults/test-lanes/20260811-155421-062-1412-01ad8c8da90643439f80f5be7c421643-focused` |
| quest/trade/snapshot related control | PASS | 22/22 | false | complete | `TestResults/test-lanes/20260811-151200-130-39244-67ce33a64a6a43fcb6f8802864dbd71e-focused` |
| carrier catalog control | PASS | 16/16 | false | complete | `TestResults/test-lanes/20260811-151237-421-7708-94eb6f3812d34c43b790966e71ffbb8b-focused` |
| US1 fast-project checkpoint | PASS | 94/94 | false | complete | `TestResults/test-lanes/20260811-152503-575-45108-8e32c3db81ab41c883a42d64af5b40fb-focused` |
| US1 integration checkpoint | PASS | 326/326 | false | complete | `TestResults/test-lanes/20260811-152528-928-19328-40437ec42fc24982bad007f5fe88157e-focused` |
| complete Fast checkpoint | PASS | 2832/2832 | false | complete | `TestResults/test-lanes/20260811-152059-768-18620-2e5ec8159fe3431a97a08074766a1820-fast` |
| post-review Fast checkpoint | PASS | 2832/2832 | false | complete | `TestResults/test-lanes/20260811-155616-989-12232-4cc376efdb02499e9ecacd2ce3f272a9-fast` |

The first Fast attempt exposed two whitespace-sensitive accepted-turn source
guards after a method was line-wrapped; the guards now inspect the relevant
method bodies and passed 2/2 in
`TestResults/test-lanes/20260811-151620-498-45208-bc906636498544788c1269f711558e51-focused`.
The second Fast attempt exposed a QTE fixture whose “minimal validated Mortal
state” omitted the now-required empty client identity index. The fixture was
migrated (no runtime auto-healing was added) and the failing scenario passed
1/1 in
`TestResults/test-lanes/20260811-152030-354-20376-b81bf8e5cf994685ab1bb3f6ce18ca42-focused`.

Manual merge-gate review then strengthened the positive active quest-reward
test to reject the new transition-authority issue as well as the older generic
lookup issue. The legacy scalar/receipt-less fixture failed 0/1 as expected in
`TestResults/test-lanes/20260811-153256-736-12172-d5f1d32fade2451fae7bb858b92f5fff-focused`.
After migration to a structured reward detail, complete sealed item, and
matching identity-index create transition, the scenario passed 1/1 in
`TestResults/test-lanes/20260811-153405-624-45164-0fbe306e600b45caa8af07cee73629ea-focused`;
the complete quest/trade regression selection passed 19/19 in
`TestResults/test-lanes/20260811-153802-357-7704-7e7bd6f314b64922ac3dea6b625a705f-focused`.

The same review found that a GM could bypass the supported player creation
command by inserting a raw item directly into canonical `items[]`. The new
surface-boundary regression failed 0/1 as expected in
`TestResults/test-lanes/20260811-154927-720-29476-d2d7bb473dce493dbf1b3983c4ca5ab5-focused`.
After the route authority catalog began requiring every raw player-carrier
occurrence to originate under `UpdateInventory`, the scenario passed 1/1 in
`TestResults/test-lanes/20260811-155027-355-16732-78202dbb8e524a2d819cc82df03b7bd7-focused`;
the complete materialization selection then passed 68/68 in the post-review
checkpoint recorded above.

An independent checkpoint review then found four route-boundary gaps. The
remediation kept later trade settlement/transfer work in US2 while closing the
US1 authority boundary: unavailable quest details no longer authorize a live
creation; every location-storage destination must exist in the validated
pre-turn snapshot; a trade output must bind one exact pre-turn request and one
current-turn `UpdateNpcTradeInventoryReceipts` entry, use the unique offer
`slotId` as its raw `creationRef`, and match the offer `itemData` semantic
projection while leaving the current-schema offer template unchanged. Trade
output may enter only the player inventory, ambiguous request/slot evidence
fails closed, and trade/NPC-command lookups are built once with ordinal keys.

| Review remediation | Result | Tests | Result directory |
| --- | --- | ---: | --- |
| unavailable quest reward authority (RED) | FAIL as expected | 0/1 | `TestResults/test-lanes/20260811-162613-895-18408-b6300bc1f93f44baa71959c648760f37-focused` |
| unavailable quest reward authority (GREEN) | PASS | 1/1 | `TestResults/test-lanes/20260811-162715-646-9344-37fd44ea37dc4003be607bbea0b168c8-focused` |
| same-turn storage targets for non-storage routes (RED) | FAIL as expected | 0/4 | `TestResults/test-lanes/20260811-162906-881-39912-ed17139ba3884c768e176ed9746a24cf-focused` |
| same-turn storage targets for non-storage routes (GREEN) | PASS | 4/4 | `TestResults/test-lanes/20260811-163201-812-17524-af7c164fc1c249c58e40a4e8d7592234-focused` |
| unrelated trade-offer identity (RED) | FAIL as expected | 0/1 | `TestResults/test-lanes/20260811-163745-417-27384-fda014dced0f4afa95927759087fd8b0-focused` |
| existing-storage trade destination (RED) | FAIL as expected | 0/1 | `TestResults/test-lanes/20260811-163956-511-19940-479b8f361b8145e783adbfcdc6eae439-focused` |
| exact trade offer and destination authority (GREEN) | PASS | 3/3 | `TestResults/test-lanes/20260811-164120-848-44272-f5bcaeba2d8240ddb139bc259ba9ac7a-focused` |
| all eight creation routes after trade hardening | PASS | 8/8 | `TestResults/test-lanes/20260811-164249-848-36876-dbba6412bc9b43cd85c2ad554c2661e2-focused` |
| route/NPC-command scale controls (RED compile gate) | FAIL as expected | build gate | `TestResults/test-lanes/20260811-164603-505-31200-e54be79376f242b2b573daa96682ed9c-focused` |
| route/NPC-command 2.5x scale controls (GREEN) | PASS | 2/2 | `TestResults/test-lanes/20260811-165043-506-18616-73f6ba8add6e46649326450498abd7a2-focused` |
| forged route-authority regression after offer evidence split | PASS | 8/8 | `TestResults/test-lanes/20260811-165625-291-7444-514742d30b3244949f183d6e04d6346c-focused` |
| complete materialization control after all remediations | PASS | 75/75 | `TestResults/test-lanes/20260811-165732-537-19320-712f4420dec64fa397d241ca71447a65-focused` |
| related normalizer regression control | PASS | 14/14 | `TestResults/test-lanes/20260811-165944-012-43104-f042b73a265c45bdbbaf8bffd279fcbd-focused` |
| post-remediation Fast checkpoint | PASS | 2834/2834 | `TestResults/test-lanes/20260811-170135-202-15644-32fca72316eb45f685b566670a376cf7-fast` |
| production-schema trade template compatibility (RED) | FAIL as expected | 7/9 | `TestResults/test-lanes/20260811-171332-445-21832-8357767fddd4431ab972f2e4a0cd7eb6-focused` |
| exact request/update-receipt + slot/semantic trade authority (GREEN) | PASS | 13/13 | `TestResults/test-lanes/20260811-172233-518-17784-eb9fadb454a2408e89068a0c59b19fec-focused` |
| sparse and dense trade/NPC-command 2.5x scale controls | PASS | 3/3 | `TestResults/test-lanes/20260811-172448-037-46576-02ef659c112e40e6b440d1def4067d54-focused` |
| complete materialization control after current-schema trade binding | PASS | 77/77 | `TestResults/test-lanes/20260811-172543-854-18324-8668ef3490e8488c91759adbbf3335c9-focused` |
| related normalizer control after current-schema trade binding | PASS | 14/14 | `TestResults/test-lanes/20260811-172811-815-29492-3d421069d747400ba9d7503310247f15-focused` |
| final US1 Fast checkpoint after current-schema trade binding | PASS | 2835/2835 | `TestResults/test-lanes/20260811-172844-250-33132-9c187074d1a4465d9faa5b1ef3102067-fast` |

## 12. Identity-preserving transfer and Mortal trade evidence

US2 routes every supported local move through one client-owned transition
writer. Player, NPC, location-storage, vehicle, buy, sell, and buyback actions
now preserve the exact permanent `itemId`, materialization envelope, root
receipt, quantity, and stable companions. The carrier and identity index move
together under one canonical write lease; injected write failures restore the
exact carrier, index, money, and merchant-state before-images.

Trade offers have two explicit outcomes. A slot already backed by a physical
NPC item transfers that exact item. A ready GM-authored template-only slot is
sealed once as a new independent `trade_output` item whose route authority is
the exact request/receipt and unique `slotId`; offer-local identity metadata is
not copied into the physical item.

Representative red/green evidence from the US2 TDD loop:

| Selection | Result | Tests | Result directory |
| --- | --- | ---: | --- |
| console buy/sell/buyback identity (RED) | FAIL as expected | 0/3 | `TestResults/test-lanes/20260811-183907-031-46756-3c946994668f456aaf7cc7403c2c87b3-focused` |
| browser buy/sell/buyback identity (RED) | FAIL as expected | 0/3 | `TestResults/test-lanes/20260811-184048-325-20332-105db3038e034ec3a46381cbf1ad0577-focused` |
| console buy/sell/buyback identity (GREEN) | PASS | 3/3 | `TestResults/test-lanes/20260811-184954-328-37548-ff39fcf2dada4a3eb76ee7736a1f6a6e-focused` |
| browser buy/sell/buyback identity (GREEN) | PASS | 3/3 | `TestResults/test-lanes/20260811-185042-904-21352-9683468a381e4ea8bbf3cd5f62a8110a-focused` |
| template-only trade output (RED) | FAIL as expected | 0/1 | `TestResults/test-lanes/20260811-190756-595-13412-276eef1fe35840a2ae8531d02b4d4299-focused` |
| template-only trade output (GREEN) | PASS | 1/1 | `TestResults/test-lanes/20260811-191047-119-35900-d73b38268b03480e9ae29457ac29707e-focused` |
| transition/storage/trade fast-project control | PASS | 81/81 | `TestResults/test-lanes/20260811-191256-821-23188-3495a5260a644be49e6adc3634ace92d-focused` |
| transfer GM-command integration | PASS | 4/4 | `TestResults/test-lanes/20260811-192030-073-6104-8d60441e6e52498daa2a5dc0d1e56439-focused` |
| creation-route and transfer integration | PASS | 81/81 | `TestResults/test-lanes/20260811-192458-854-29960-8ee2d34e6c68498daebe3c5b53d45980-focused` |
| console storage/vehicle/trade integration | PASS | 5/5 | `TestResults/test-lanes/20260811-192314-781-41872-f4ff42e34a934930a42568fbb3e52b0c-focused` |
| NPC trade request validation | PASS | 9/9 | `TestResults/test-lanes/20260811-192340-216-31232-ad87d97e4cd245239a5df71fdcca6673-focused` |
| related web command integration | PASS | 3/3 | `TestResults/test-lanes/20260811-192413-793-19360-c9c76d52418347a59ade009a69a250fb-focused` |

The first combined integration selector exceeded the five-minute Focused
budget because it included the complete large Explorer test classes; it did
not report a failed test. Splitting the same boundaries into the selections
above completed within their lane budgets. The first US2 Fast checkpoint then
found one source guard whose extraction endpoint named a deliberately removed
local item-upsert helper. The guard still protects the explicit `relicId`
authority rule and passed 1/1 after its endpoint was moved to the next current
helper in
`TestResults/test-lanes/20260811-193115-752-22156-23942a9f0a6a47178c431fb575e2f61f-focused`.

The accepted US2 Fast checkpoint passed 2845/2845 tests with no timeout,
complete owned-process cleanup, and no duplicate test IDs in
`TestResults/test-lanes/20260811-193248-935-20344-05d9fa95249849f78fab1efc0c8799f7-fast`.

## 13. Stack lineage and destructive discard evidence

US3 routes split, merge, and full discard through the same atomic transition
writer used by transfers. A split preserves the parent identity and creates one
derived child receipt with exact parent/root lineage. A merge preserves the
selected survivor receipt, unions every origin, and retires each contributor as
`merged`. Full discard retires the exact item as `destroyed`, clears its inline
equipment reference, and does not create ground loot. Every operation validates
quantity continuity and commits the physical carrier and identity index
together, restoring exact before-images on failure.

The merge compatibility projection covers all governed item semantics,
including readable, sentient, bonded, quest, equipment/container, and
materialization-section dispositions. Offer-local or permanent identity fields
remain outside that projection. Reversed source order, unsafe companion
references, quantity overflow, and unrelated pre-existing carrier/index
quantity corruption all fail before any write.

Representative accepted US3 controls:

| Selection | Result | Tests | Result directory |
| --- | --- | ---: | --- |
| transition writer and browser stack/discard controls | PASS | 41/41 | `TestResults/test-lanes/20260811-204258-571-29364-577be144794e44feb1537995aa17a2e4-focused` |
| console discard parity and canonical quantity authority | PASS | 4/4 | `TestResults/test-lanes/20260811-204328-274-35424-53d5d42965c740418f4af2bd4395c350-focused` |
| accepted post-review Fast checkpoint | PASS | 2863/2863 | `TestResults/test-lanes/20260811-204701-972-24412-e06ff39a2e884f4b94427124cf03252a-fast` |

An independent checkpoint review found no remaining Critical or Important US3
issues after the companion-reference, global quantity, semantic-section, and
overflow regressions were added and made green.
