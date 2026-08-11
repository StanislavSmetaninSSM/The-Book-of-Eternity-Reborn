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

## 14. Bounded repair, replay, and rollback evidence

US4 now emits one exact-coordinate item packet for each independently
repairable item and one bounded authority packet for global identity
conflicts. Packets expose route, transition, carrier, companion, expected
authority, actual evidence, and exact corrections, but their target allowlist
can never delegate `game_state/inventory/item_identity_index.json` or another
client/control/output surface to the GM. Protected-path classification remains
case-insensitive while item and `creationRef` identity remains ordinal; values
are never collapsed, and case/whitespace/Unicode-confusable aliases are
rejected explicitly.

Accepted creation and stack commands reject replay before any settlement or
quantity mutation. The identity index retains both materialization-ID and
creation-reference origin history through split, merge, and retirement, and a
single exact/confusable evidence index serves the whole accepted creation
batch. The transition replay key includes authority, transition kind, turn,
source item IDs, and source/destination carrier, so a literal retry is rejected
while a distinct command or a different leg of a multi-carrier route remains
valid. Failed raw repair retains the exact pending snapshot and route request;
correction of the same not-yet-accepted `creationRef` then seals one receipt and
one index transition. Existing one-lease before-images restore carriers,
companions, index, route state, and player-facing outputs byte-for-byte; an
acquisition message written for a rejected item is either explicitly rewritten
after canonical repair or removed by rollback.

Representative US4 red/green and checkpoint evidence:

| Selection | Result | Tests | Timeout | Cleanup | Result directory |
| --- | --- | ---: | --- | --- | --- |
| packet builder initial contract (RED) | FAIL as expected | build gate | false | complete | `TestResults/test-lanes/20260811-205557-510-20456-18f5bb0d65524b608fd4669e126d8a26-focused` |
| packet source-guard correction (RED) | FAIL as expected | 12/13 | false | complete | `TestResults/test-lanes/20260811-205830-679-40792-020df5760e6a4b7abbd8f8c68ad17ed7-focused` |
| packet builder first GREEN | PASS | 13/13 | false | complete | `TestResults/test-lanes/20260811-205939-400-20244-a0e8686b3a9a477b954521d429f9bfcc-focused` |
| GameEngine item packet mapping (RED) | FAIL as expected | 0/1 | false | complete | `TestResults/test-lanes/20260811-210155-041-33324-ff6877a5e12f4748b265e817b0cb973d-focused` |
| GameEngine item packet mapping (GREEN) | PASS | 1/1 | false | complete | `TestResults/test-lanes/20260811-210348-521-28096-bd4220985249487384d91e1bf8f5a1ac-focused` |
| validator repair context (RED) | FAIL as expected | 0/1 | false | complete | `TestResults/test-lanes/20260811-210659-806-45424-6f006b966767482d923f88ed494dc938-focused` |
| validator repair context (GREEN) | PASS | 1/1 | false | complete | `TestResults/test-lanes/20260811-210953-722-8636-3abe7f9fea2149a3844702fcf83e1a05-focused` |
| same-authority lifecycle matrix (RED) | FAIL as expected | 6/7 | false | complete | `TestResults/test-lanes/20260811-211551-336-25416-a233ad31488d4cd28263681c61f81750-focused` |
| same-authority lifecycle matrix (GREEN) | PASS | 7/7 | false | complete | `TestResults/test-lanes/20260811-211736-303-7936-26ea15629d164e328166292a05bb33ce-focused` |
| identity packet and protected casing (RED) | FAIL as expected | 12/15 | false | complete | `TestResults/test-lanes/20260811-212308-895-42704-2d4b849d072447cbb5246335567a05d3-focused` |
| identity packet and protected casing (GREEN) | PASS | 15/15 | false | complete | `TestResults/test-lanes/20260811-212354-157-32300-a90bb882231940a69b605cd9dfa27ef8-focused` |
| stale acquisition output and rollback characterization | PASS | 2/2 | false | complete | `TestResults/test-lanes/20260811-212534-660-27544-5f0c760e2b0b4627a82aef85b10903db-focused` |
| same-request craft correction and snapshot retention | PASS | 1/1 | false | complete | `TestResults/test-lanes/20260811-212714-027-42680-caae7a47caff4c5da1a032c71a043687-focused` |
| focused packet/transition/source-guard control | PASS | 54/54 | false | complete | `TestResults/test-lanes/20260811-213302-501-47796-563d2169ac2f45edafe7b1d5b8e8d122-focused` |
| focused route/repair/snapshot/rollback integration | PASS | 94/94 | false | complete | `TestResults/test-lanes/20260811-213327-658-3212-65476f153d0a4119822815419e6e0ee1-focused` |
| preliminary US4 Fast checkpoint | PASS | 2880/2880 | false | complete | `TestResults/test-lanes/20260811-213547-826-45700-9c4bb0759d9247ef9de99208f8329831-fast` |
| same-turn sell/buyback/sell local authority (RED) | FAIL as expected | 0/1 | false | complete | `TestResults/test-lanes/20260811-214321-216-25208-b3633f66f215452c8958152ed848869f-focused` |
| same-turn sell/buyback/sell local authority (GREEN) | PASS | 1/1 | false | complete | `TestResults/test-lanes/20260811-214503-701-41200-6afb4161596e4c44b53789e3f760409f-focused` |
| complete Mortal NPC trade service control | PASS | 23/23 | false | complete | `TestResults/test-lanes/20260811-214610-907-31192-e6d9f1976b374c5e80d732a679b64dfe-focused` |
| accepted US4 Fast checkpoint after local-cycle hardening | PASS | 2881/2881 | false | complete | `TestResults/test-lanes/20260811-214901-077-43760-0591249b0d364112bf517e76ce8685ea-fast` |
| item-specific identity grouping after Fast (RED) | FAIL as expected | 15/16 | false | complete | `TestResults/test-lanes/20260811-215348-695-21632-6c3f712278694db080cabae70852067d-focused` |
| item-specific identity grouping after Fast (GREEN) | PASS | 16/16 | false | complete | `TestResults/test-lanes/20260811-215431-308-4628-ac78d5079de04a819a816814468e61a7-focused` |
| protected/unresolved/bounded packet review cases (RED compile gate) | FAIL as expected | build gate | false | complete | `TestResults/test-lanes/20260811-220634-338-18448-7e60cb9d013c4ee9846fabcc54df7061-focused` |
| protected/unresolved/bounded packet review cases (GREEN) | PASS | 22/22 | false | complete | `TestResults/test-lanes/20260811-220844-946-38216-c17bf09f443d470fb1047bc0a62f91b6-focused` |
| retired create-authority replay after destroy/merge (RED) | FAIL as expected | 0/2 | false | complete | `TestResults/test-lanes/20260811-221022-266-16568-184011246560402a836e77e791513426-focused` |
| protected authority dispatch guard (RED) | FAIL as expected | 0/1 | false | complete | `TestResults/test-lanes/20260811-221223-062-26740-29b39af55d0f4597985d0032bc8b1237-focused` |
| retired replay and protected dispatch review cases (GREEN) | PASS | 3/3 | false | complete | `TestResults/test-lanes/20260811-221458-325-20060-99f07675c81e404ea986cc49dc565eec-focused` |
| repair/replay/output lifecycle review control | PASS | 14/14 | false | complete | `TestResults/test-lanes/20260811-221624-070-46116-f17574f8f9814a20bf5ca7f2e9a56309-focused` |
| bounded packet, local authority, and source-guard review control | PASS | 25/25 | false | complete | `TestResults/test-lanes/20260811-221721-457-14948-345e7df6c6474f5798869385999ed90c-focused` |
| expanded item validation/normalizer/lifecycle review control | PASS | 321/321 | false | complete | `TestResults/test-lanes/20260811-221907-715-35796-4380f09af1494c51b82a417a7204b146-focused` |
| post-first-review US4 Fast checkpoint | PASS | 2888/2888 | false | complete | `TestResults/test-lanes/20260811-222300-089-38020-dc04048965c14f389eb4110b2344fe73-fast` |
| durable exact/confusable evidence and exact-coordinate unit control | PASS | 195/195 | false | complete | `TestResults/test-lanes/20260811-230540-017-11304-03a9112fab454cc3a640bab922ef3855-focused` |
| expanded route/snapshot/replay integration control | PASS | 150/150 | false | complete | `TestResults/test-lanes/20260811-230003-467-29416-e815453a6d884284b5efeb474fa23c13-focused` |
| missing-identity sentinel regression (RED) | FAIL as expected | 0/2 | false | complete | `TestResults/test-lanes/20260811-230318-763-19384-32884a7810054ee18b891daa786b6aad-focused` |
| literal/missing identity coordinate regression (GREEN) | PASS | 4/4 | false | complete | `TestResults/test-lanes/20260811-230403-433-18500-7dd18c8992cd4a59aff6cbb773f3b964-focused` |
| partial historical creation key (RED) | FAIL as expected | 4/6 | false | complete | `TestResults/test-lanes/20260811-231052-690-46208-782a5ff04c224a2e88b12abab3fb8f4e-focused` |
| destroy/merge exact, confusable, and partial-key replay (GREEN) | PASS | 6/6 | false | complete | `TestResults/test-lanes/20260811-231211-303-44952-69071a411e4149c08da33ef206de07d1-focused` |
| final post-review repair/contract unit control | PASS | 94/94 | false | complete | `TestResults/test-lanes/20260811-231352-095-7520-d36d6f3193364995a54ceba347f254c9-focused` |
| accepted final US4 Fast checkpoint | PASS | 2895/2895 | false | complete | `TestResults/test-lanes/20260811-231524-300-46376-68e44f3e45cd4db6bbcdac6da011035f-fast` |

The stale-output tests characterize an already shared accepted-turn freshness
and rollback invariant, so they were green when introduced; US4 adds the
Mortal-item-specific regression and proves that item repair participates in
that existing invariant. The first combined fast control exposed an overly
broad replay key: a legal round trip reused one request authority on different
carrier legs. Exact transition coordinates replaced the broad authority-only
key. A second review then proved that local commands need unique operation
tokens: `sell -> buyback -> sell` in one turn is three deliberate operations,
not a replay. After adding the token to local sales, the full 23-test trade
service control and final 2881-test Fast checkpoint passed with zero duplicate
IDs, timeout, or cleanup errors. The final independent US4 review then found
that active carriers alone did not prevent recreation after destroy/merge,
protected evidence could still leak into GM corrections, unresolved coordinates
could become whole-carrier packets, and some evidence values were unbounded.
The review regressions now prove durable retired-origin replay rejection,
client-side fail-closed rollback before GM dispatch, exact `new`/`existing`
grouping only, empty-target suppression, and a 500-character evidence bound.
The final re-review also exercised partial historical keys and legitimate exact
identities such as literal `unknown` and internal whitespace: only standalone
`mortal_item:unknown` is unresolved, while any independently matched historical
materialization ID or creation reference fails closed with no GM packet. The
final static review reported no Critical, Important, or Minor findings and
marked US4 ready to commit.

## 15. Player-safe console and browser projection evidence

US5 keeps the accepted Mortal item object authoritative in its carrier while
projecting only in-world semantics to the two player clients. Player inventory,
equipment, detail, and local-action readers now use canonical `items[]` only;
command-shaped `UpdateInventory` candidates and equipment references that do
not resolve to a canonical item are omitted. NPC views use only the inventory
embedded in canonical `npc_core`; pending `NPCInventoryAdds` never feed either
client. Current-schema `equippedItems` references are accepted only as ordinal-
exact scalar item IDs and are rendered by the matched item's semantic name.
Local drop/split/merge and storage/vehicle prompts follow the same rule and
never serialize or act on rejected command carriers. Items with the same
player-facing name remain distinct because equipment and actions bind only by
their ordinal-exact permanent IDs.

The console retains its semantic catch-all for setting-specific fields, but a
shared ordinal-insensitive recursive denylist removes envelope, receipt, seal,
source-authority, identity-index, lineage, carrier, path, transition,
validation, and repair fields at any nesting depth. The browser first clones an
explicit top-level semantic allowlist, applies a structured-bonus allowlist,
and then uses the same recursive sanitizer for retained semantic subtrees.
Overview cards, detail blocks, NPC mechanics, and action payloads therefore do
not receive protected evidence. Both clients preserve unknown in-world bonus
fields, real owner-bond current/maximum values, Fate Cards, and quest links.
Companion data is joined only from canonical `entries[]` through an exact
`itemId`/`existedId`; command arrays, display-name fallback, and case-folded IDs
cannot enrich an accepted item.

Fate Card projection now covers the complete current schema used by the item
validator: locked cards show bond, plot, conjunction, and required-material
conditions; unlocked cards show improved bonuses, new combat effects, item
stat boosts, description changes, a player-facing notice when the visual form
changes, and other narrative changes. Embedded and sidecar mirrors are deduped
by ordinal-exact `cardId` (semantic name only when no ID exists). Raw English
image-generation prompts remain hidden. Browser details also preserve the
console distinction between unresolved, narrative-only, and applicable
mechanical summaries instead of presenting unresolved prose as active rules.

The mechanic-bearing fixtures are created with complete envelopes and complete
current-schema Fate Card fields, resealed after semantic test customization,
and explicitly pass `CanonicalPostSeal` contract validation before they are
shown. This prevents a forged or stale receipt from making the privacy test
green for the wrong reason. Navigation retains only the permanent selector
needed for a local action; it is not rendered as item detail.

No `BookOfEternityClient.WebFrontend/src/` change was required. The final C#
results and serialized payloads contain no materialization, receipt, creation
reference, seal, origin, carrier, transition, path, validation, or repair
fields. The frontend therefore never receives those fields; the boundary is
fully enforced before frontend projection.

Representative US5 red/green and parity controls:

| Selection | Result | Tests | Timeout | Cleanup | Result directory |
| --- | --- | ---: | --- | --- | --- |
| initial console authority/raw-candidate projection (RED) | FAIL as expected | 1/3 | false | complete | `TestResults/test-lanes/20260811-232902-192-13204-83574bcc58b241bab6c361cee2fd6547-focused` |
| receipt-bearing browser local-action baseline | PASS | 3/3 | false | complete | `TestResults/test-lanes/20260811-233021-277-46640-3fd4808c0f2b4082a3b798c2129cc6e4-focused` |
| nested authority, raw detail, and pending NPC projection (RED) | FAIL as expected | 0/3 | false | complete | `TestResults/test-lanes/20260811-234806-900-29172-5cce30bc074e4e859023bb7dcfb6507b-focused` |
| first recursive/canonical route correction | PASS | 5/5 | false | complete | `TestResults/test-lanes/20260812-000150-496-42212-b3128e8ea9e64b119b0ae4e5d6af3809-focused` |
| unmatched player equipment references (RED) | FAIL as expected | 0/2 | false | complete | `TestResults/test-lanes/20260812-000421-178-29464-f4bfee200bf2449d86528f896d367793-focused` |
| accepted-only player equipment references (GREEN) | PASS | 2/2 | false | complete | `TestResults/test-lanes/20260812-000529-005-16304-4ec476d19cb1409a92dc3d25fb4bb5d3-focused` |
| source-authority and unmatched console NPC edge (RED) | FAIL as expected | 2/4 | false | complete | `TestResults/test-lanes/20260812-001644-011-32616-dcada7437c254dc686e67d0fbf29f3ac-focused` |
| recursive source-authority correction | PASS | 4/4 | false | complete | `TestResults/test-lanes/20260812-001805-639-32472-23ffded2925a4b65ac74cde3dc025f39-focused` |
| browser semantic equipped-item resolution (RED) | FAIL as expected | 0/1 | false | complete | `TestResults/test-lanes/20260812-002431-826-21632-262da2e686074c39b977b705dda8a723-focused` |
| case-confusable and object/name console equipment resolution (RED) | FAIL as expected | 0/1 | false | complete | `TestResults/test-lanes/20260812-002920-765-38328-149cb5174de340399f10a63efce1b796-focused` |
| final exact console/browser NPC equipment regressions | PASS | 2/2 | false | complete | `TestResults/test-lanes/20260812-003021-064-33880-612bb129b9e24604897892d53a41bfeb-focused` |
| browser Fate Card conditions/reward parity (RED) | FAIL as expected | 0/1 | false | complete | `TestResults/test-lanes/20260812-003437-382-45856-cbbd94eaa7d24295918281ea569da529-focused` |
| sanitized embedded/sidecar Fate Card parity (GREEN) | PASS | 1/1 | false | complete | `TestResults/test-lanes/20260812-003549-337-45064-2015027370ae4bf79bf5c53bb7727067-focused` |
| player equipment name/case spoof (RED) | FAIL as expected | 0/1 | false | complete | `TestResults/test-lanes/20260812-004124-752-37432-aee49a99ffba410d9e1c2ae8d176aac8-focused` |
| raw drop/split prompt candidates (RED) | FAIL as expected | 1/3 | false | complete | `TestResults/test-lanes/20260812-004529-529-30952-3097168e56af46598102911990bd2854-focused` |
| raw storage/vehicle prompt candidates (RED) | FAIL as expected | 0/2 | false | complete | `TestResults/test-lanes/20260812-004731-771-33468-42a4d5f14dfd40f08a1b04f1a4fb1d6a-focused` |
| canonical-only local/storage/vehicle prompts (GREEN) | PASS | 5/5 | false | complete | `TestResults/test-lanes/20260812-005211-931-42524-3fac769f751b4e26b8e43a507428275a-focused` |
| wrong-case/name sidecar enrichment (RED) | FAIL as expected | 0/2 | false | complete | `TestResults/test-lanes/20260812-005009-771-20808-e211405bc7ce452fb5def0ebfe8fc790-focused` |
| exact sidecar and player equipment identity (GREEN) | PASS | 4/4 | false | complete | `TestResults/test-lanes/20260812-005321-560-6604-04408c397fb4423695f3bad4c6747a2c-focused` |
| unknown semantic array catch-all (RED) | FAIL as expected | 0/1 | false | complete | `TestResults/test-lanes/20260812-005416-512-21644-bac8c68c2a8b4ec8a226a6b0dc3f5813-focused` |
| complete Fate/mechanical-summary, semantic array, and exact-name regressions | PASS | 5/5 | false | complete | `TestResults/test-lanes/20260812-011458-578-25860-d779af4f72944e7396cb191f082d1f27-focused` |
| browser inventory/storage/transport local parity | PASS | 50/50 | false | complete | `TestResults/test-lanes/20260812-011808-866-43996-b1ce3f1da3994e2196e392481aed2d4f-focused` |
| broad current-schema fixture checkpoint | FAIL, stale object equipment fixture | 59/60 | false | complete | `TestResults/test-lanes/20260812-011959-965-33448-aaef131b00e54ed99cdea6cf5ab5b943-focused` |
| final console/browser inventory and NPC integration control | PASS | 60/60 | false | complete | `TestResults/test-lanes/20260812-012122-824-13392-36f02a29cbcb4b99ab3241149c32b732-focused` |

The inspected console fixture retained its item name, description,
setting-specific `makerTradition`, unknown nested semantic bonus field,
`12/80` bond, Fate Card, and quest link while every injected private marker was
absent. The browser fixture retained `Рунное дело +2`, its nested in-world
condition, the same bond/fate/quest contours, a locked-card bond/plot/material
condition, every unlocked sidecar reward category, and an accepted NPC
equipped-item name. Mirrored Fate Cards appeared once by exact card ID. Raw
same-file candidates, pending NPC additions, unmatched equipment,
case-confusable IDs, object/name equipment and sidecar fallbacks, raw visual
prompts, and all injected authority markers were absent from the final
serialized results.

Post-review hardening kept the same accepted-only boundary while extending
semantic parity. Both clients now show physical volume for non-container items,
consumable capability, readable/sealed/locked reasons, open-schema quest-link
conditions, full disassembly and combat mechanics, and setting-specific nested
fields. Transfer or creation into `player_inventory` removes legacy
`isCarried` and location hints before resealing, so carrier authority and local
actions cannot disagree. Full repair packets, transition records, carrier
coordinates, source-authority objects, materialization envelopes, and identity
index entries are recognized by their complete DTO shape and omitted as a
unit; adjacent ordinary `kind`, `title`, `turn`, `realm`, `state`, `sections`,
and `steps` semantics remain visible. The console applies this projection once
at item-detail entry and at structured quest-reward recursion; the browser
applies it before dossier construction.

Representative post-review evidence:

| Selection | Result | Tests | Timeout | Cleanup | Result directory |
| --- | --- | ---: | --- | --- | --- |
| placement normalization and scalar/nested semantic parity | PASS | 3/3 | false | complete | `TestResults/test-lanes/20260812-063037-847-45204-cd2bb772f3be408dbc2e9e8596039826-focused` |
| recursive repair-field privacy | PASS | 2/2 | false | complete | `TestResults/test-lanes/20260812-063258-328-43920-788e6a30e9364261a8811dfd5beefb53-focused` |
| physical/consumable/readability parity | PASS | 2/2 | false | complete | `TestResults/test-lanes/20260812-064613-401-44472-64295d11397f4fce98c50d0c2870314b-focused` |
| full authority DTO shape unit projection | PASS | 1/1 | false | complete | `TestResults/test-lanes/20260812-070327-627-24708-d876b254fbf54feeb42edfc843ce999f-focused` |
| final console/browser item projection checkpoint | PASS | 2/2 | false | complete | `TestResults/test-lanes/20260812-070430-962-45856-cf16ecbcf12549079f11fe5dc4b3b9b3-focused` |

## 16. Current-schema fixture and GM contract migration

The active example save and all shared positive item builders now use complete
Mortal materialization envelopes, receipts, and the client-owned identity
index. Receipt-less objects remain only in explicitly named negative fixtures.
GM rules, CLI/API and daemon guidance, task guides, route examples, the example
manifest, and source guards describe exact permanent IDs, ordinal identity,
creation-reference rewrites, accepted transfer/stack lineage, protected repair
targets, and receipt-less rejection. `contentsPath` contains permanent parent
item IDs rather than display names; `isCarried` and location hints are stated
as presentation fields rather than placement authority. The worked Mortal
example covers mundane acquisition plus mechanic-bearing craft/trade paths.

No Chaos Sea or Shining Abode pending/control, response, receipt, scheduler,
normalizer, or GM-authored contract changed. The shared client projection work
does not add an afterlife authoring surface, so
`OtherGuides/Afterlife_Contract_Matrix.md` and
`Examples/E_CLI_Afterlife_Turns.txt` intentionally remain unchanged. The
afterlife realm-segregation control passed 13/13.

Phase 8 evidence:

| Selection | Result | Tests | Timeout | Cleanup | Result directory |
| --- | --- | ---: | --- | --- | --- |
| validator fixture inventory | PASS | 98/98 | false | complete | `TestResults/test-lanes/20260812-051106-634-26264-abdb3e7fc8dd4315a4701b2784bfd1dc-focused` |
| item-bond source guard | PASS | 2/2 | false | complete | `TestResults/test-lanes/20260812-051023-035-17420-1730a8ac8f60431d944a0e842a337c30-focused` |
| item example and manifest validation | PASS | 2/2 | false | complete | `TestResults/test-lanes/20260812-053746-061-47756-5589bd4280ad45818cf793503305efab-focused` |
| prompt documentation guards | PASS | 22/22 | false | complete | `TestResults/test-lanes/20260812-053837-351-32676-9769a97415374aa29a9456c210f449b7-focused` |
| example documentation validation | PASS | 18/18 | false | complete | `TestResults/test-lanes/20260812-054533-512-30944-59d078e4136348c5b0c27319b8cde69b-focused` |
| afterlife realm segregation | PASS | 13/13 | false | complete | `TestResults/test-lanes/20260812-054721-620-30800-698e00bf5d884f4280cc77f332915184-focused` |
| consolidated fixture/documentation group | PASS | 36/36 | false | complete | `TestResults/test-lanes/20260812-064039-800-7908-6abf77a4ff2b46c397ba49eb6f5adc3f-focused` |
| current-schema placement source guards | PASS | 2/2 | false | complete | `TestResults/test-lanes/20260812-063914-212-4628-5ad507b58d68467fab06c7bacb1b5af4-focused` |

## 17. Final verification record

The bounded Focused schedule was kept within the runner's five-minute hard
limit. The broad browser class was narrowed to every touched item, equipment,
books, NPC, quest, interaction, effect, storage, and transport command family;
the final PreMerge lane remains the full integration control. Two stale
positive fixtures found during this pass were migrated to accepted
current-schema items: the storage/vehicle prompt inventory and the occupied
accessory reference. No production relaxation was made.

| Focused group | Result | Tests | Timeout | Cleanup | Result directory |
| --- | --- | ---: | --- | --- | --- |
| materialization contract and identity transitions | PASS | 98/98 | false | complete | `TestResults/test-lanes/20260812-071003-718-16220-63f1cc7b4e71487ebf5ea591a01db34f-focused` |
| validation and canonical normalization | PASS | 307/307 | false | complete | `TestResults/test-lanes/20260812-072135-558-9248-99a8e6df240442e2a0d4afe258e93fa2-focused` |
| quest reward and NPC trade authority | PASS | 19/19 | false | complete | `TestResults/test-lanes/20260812-073337-638-27296-8b44d5cfb6d349ca9f0368747ba7a89c-focused` |
| console Explorer projection and actions | PASS | 366/366 | false | complete | `TestResults/test-lanes/20260812-073413-669-41508-685801f244b34e5bae4c7c865041e7de-focused` |
| touched browser item command families | PASS | 37/37 | false | complete | `TestResults/test-lanes/20260812-074649-516-43920-ada99d1ff48c4e20831c20b5c867949b-focused` |
| active fixture and documentation validation | PASS | 36/36 | false | complete | `TestResults/test-lanes/20260812-074731-545-34920-08109ac8029142dc888c4bfc360635a3-focused` |
| prompt guards and browser local-action parity | PASS | 85/85 | false | complete | `TestResults/test-lanes/20260812-075036-887-30272-d77a2d0dde624c52a96f6b809ff172fb-focused` |

Repository hygiene passed `git diff --check`. The artifact audit retained the
pre-existing `.serena/` and generated `bin/obj` trees without staging or
deleting them; every other untracked path is a feature-owned source, example,
or current-schema fixture intended for the candidate.

The final read-only code review reported **0 Critical** and **0 Important**
findings and marked the candidate ready to commit. The review covered exact
identity and route authority, receipt/identity privacy, repair and rollback,
equipment and local-action atomicity, sidecar ambiguity, console/browser
semantic parity, current-schema fixtures, and GM documentation boundaries.

The development Fast checkpoint completed before the final lifecycle-specific
hardening. The final `LifecycleIntegration` then exposed and verified two
current-life boundaries: accepted-turn snapshots must compare raw item state
against the staged current baseline rather than previous-life rollback bytes,
and a fresh Mortal bootstrap must create empty identity-bound item sidecars.
The bootstrap now initializes `item_resources.json`, `item_bonds.json`,
`item_text_updates.json`, and `item_journals.json` beside the empty inventory
and identity index. Previous-life bytes remain available only to roll back a
rejected bootstrap. The daemon specification, API, operational prompt, main GM
guide, worked bootstrap example, and source guard all state this ownership
boundary explicitly.

Final development controls before the clean-candidate PreMerge:

| Lane / selection | Result | Tests | Duplicate IDs | Timeout | Cleanup | Result directory |
| --- | --- | ---: | ---: | --- | --- | --- |
| Fast checkpoint | PASS | 2937/2937 | 0 | false | complete | `TestResults/test-lanes/20260812-075851-508-35436-f6e8e55c10de417fb01b0775abc6232d-fast` |
| FullValidation after final GM docs | PASS | 1735/1735 | 0 | false | complete | `TestResults/test-lanes/20260812-085742-923-34964-f1a33612435e479d8eb1ab3c1728bdf4-fullvalidation` |
| LifecycleIntegration after bootstrap/snapshot correction | PASS | 217/217 | 0 | false | complete | `TestResults/test-lanes/20260812-084518-429-16268-ec86024e4a6a4a15a94cbeb1200cb6ae-lifecycleintegration` |
| Bootstrap plus prompt documentation source guards | PASS | 22/22 | 0 | false | complete | `TestResults/test-lanes/20260812-085720-521-16464-4ee1ceb2fbc8425f96f6a7d346f975aa-focused` |

The first clean-candidate PreMerge exposed one repository-owned positive
fixture that the earlier source-tree inventory had missed: the reusable Mortal
command-display ZIP still stored receipt-less player and location items. The
accepted-only clients correctly hid those objects, so the inventory parity and
localized-slot assertions failed. No compatibility fallback was restored. The
archive was migrated instead: 10 player items, 3 NPC items, and 2 location-
storage items now carry complete envelopes and receipts, the accepted NPC adds
live in `npc_core.json`, stale NPC item commands are empty, and one 15-entry
identity index covers every durable carrier. A permanent archive integrity test
validates canonical post-seal shape, receipt/index agreement, carrier
coordinates, and canonical `equippedItems`.

Fixture correction evidence before the replacement clean-candidate PreMerge:

| Lane / selection | Result | Tests | Duplicate IDs | Timeout | Cleanup | Result directory |
| --- | --- | ---: | ---: | --- | --- | --- |
| first clean-candidate PreMerge | FAIL, receipt-less reusable Mortal save | 3019/3020 C#; 138/138 frontend | 0 | false | complete | `TestResults/test-lanes/20260812-091241-707-18488-16f35420e9a84c4287cd4a8ca9f93db9-premerge` |
| migrated save contract, validation, localization, and console/browser anchors | PASS | 56/56 | 0 | false | complete | `TestResults/test-lanes/20260812-093350-174-29540-c87bbcd78c4440f1bbb65c3eca645afa-focused` |
| all repository fixture-integrity controls | PASS | 19/19 | 0 | false | complete | `TestResults/test-lanes/20260812-093651-907-13156-1cd703b2713045fea694dfc977deb773-focused` |
| replacement clean-candidate PreMerge | FAIL, unscoped diagnostic call in one lifecycle test | 3964/3966 C#; 138/138 frontend | 0 | false | complete | `TestResults/test-lanes/20260812-093921-936-27684-686144eabeb744f18d8c8f600b1d582b-premerge` |
| scoped lifecycle diagnostic and broad-validation source guards | PASS | 5/5 | 0 | false | complete | `TestResults/test-lanes/20260812-094921-831-8052-5525d141eeff46fbb566e6296c41b66a-focused` |

Manual console/browser inspection passed. The simple and mechanic-bearing
fixtures retained their Russian in-world description, physical facts,
structured mechanics, bond/Fate/quest semantics, and exact action affordances.
Injected receipt, materialization, creation-reference, identity-index,
carrier, transition, file-path, validation, repair-packet, and private-marker
data were absent from both serialized player projections. No frontend source
changed, so no frontend build or visual check was required: the C# projection
boundary prevents those fields from reaching React.

The requirement reconciliation found task coverage for all FR-001–FR-043 and
SC-001–SC-010. In User Story 2, the phrase `loot carrier` is interpreted only
as the FR-002 loot route into a real canonical destination; no durable ground-
loot carrier was implemented. SC-010 remains pending solely for the exact-
commit clean-checkout PreMerge in T079, followed by PR integration in T080.
