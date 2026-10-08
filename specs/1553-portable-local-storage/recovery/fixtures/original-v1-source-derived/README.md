# Original-v1 source-derived compatibility fixtures

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), T032-A1. **Data/provenance accepted at `92bd725f`; two ordinary compatibility cases accepted on isolated source through `c94469e8`. Full T032-A1 remains open.**

`pending.json` and `committed.json` are independently transcribed from the original byte-only contract at accepted [fc3f8bb9c9e36408c85cf37bbea9f35cb890a972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/fc3f8bb9c9e36408c85cf37bbea9f35cb890a972). They were not emitted by an executed historical binary and were not built through the new shared `Header` helper. Ordinary static JSON/base64/hash calculations produced them. The two files differ only in `Committed`.

[provenance.json](provenance.json) pins the accepted commit/tree and six original source blobs, their SHA-256 and relevant source lines. It records each immutable journal's Git blob, size and SHA-256, every payload's exact size/hash, initial state and source-derived expected result. No application, build, test, discovery runner, benchmark or native operation was executed for this fixture block.

## Original contract represented

- Original six-field v1 journal, three-field member and three-field byte image; no v2 framing, offsets or lengths inside the journal
- Replacement of UTF-8 BOM bytes by binary bytes; creation of an existing zero-byte file from absence; deletion of three known bytes
- An explicitly declared generation transition. Before is UTF-8 with BOM, lower-camel-case current-schema fields and an allowed extension; after is UTF-16 LE with BOM and ordinary Pascal-case fields. Fixed valid GUID-N values identify the fixture transaction/generations; they are not physical file identities
- Both source-described initial states have all member After images and the After generation. Pending recovery should restore every Before image/absence and its exact generation bytes; committed cleanup should retain every After image/absence and generation. These expectations were derived from the historical source; the separately authorized two-case run below subsequently observed them on its exact isolated source

## Path normalization for later isolated use

The immutable data uses `/__boe_v1_fixture__/root` as a neutral absolute placeholder. It is never an instruction to write there. An authorized consumer must verify the fixture hash first, use its own fresh isolated absolute normalized root, and change only each `Members[*].Path` prefix plus native separators. Record the resulting journal hash separately. Preserve all payload bytes, hashes, transaction/generation values and member order. On Windows use the existing accepted path-normalization contract. The provenance contains the exact journal location and initial member/scratch absence requirements. The original data block added no materialization or runtime/test wiring. The subsequently accepted test verifies this normalization contract without changing these fixture bytes.

## Limits and next gate

Static checks can establish field shape, uniqueness, valid base64, exact payload hashes, generation bindings and artifact/source identity. They cannot establish runtime acceptance, cold recovery, stream lifetime or resource behavior. That separate authorized block now has [accepted two-case evidence](../../evidence/original-v1-compatibility/manifest.json): fresh build followed by 2/2 ordinary same-process cases at `d4460793` plus its carrier, on unchanged runtime `26c0327c`. Independent Astra XHigh accepted source/evidence at `bd23ad76` and final docs/restoration proof at `c94469e8`. The main-branch import is source/evidence only; it is not a new runtime execution. The two existing `OriginalV1EvidenceKeepsItsOriginalPendingAndCommittedSemantics` cases remain synthetic smoke controls through the shared new `Header` helper; their historical pass is not proof for these independent files.

The existing 14-case `TrustedLocalStreamRecoveryTests` scaffold remains byte-identical, unbuilt, unrun and without a catalog owner. Its recovery observer remains unwired. The known scaffold ownership/discovery gap is intentionally still open; only the distinct `portable-storage-original-v1-fixtures` owner was added for the accepted test. The isolated source audit does not close this main-branch gap. T032-A1 and full SaveGame/client cutover remain gated. Generation decoding and original v1 JSON still have whole-document allocations; no universal constant-memory claim follows.
