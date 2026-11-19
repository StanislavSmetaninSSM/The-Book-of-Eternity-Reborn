# Scoped manual mm21 source recovery

These four files are exact authored source copies of the reviewed external manual
workflow, not a second harness and not permission to execute. Recover from the
parent-verified Git commit, compare source hashes, and restore them byte-for-byte
to `/workspace/shared/relay-alternative/mortal-t034/mm21-preparation/`.
Do not restore any game world, owner, queue, packet, live expectation or PID.

The external `mm21-input-pins.json` is deliberately NOT committed: it is a fresh
machine/input snapshot, not portable authority. Recreate it only after establishing
an available accepted owning build and immutable package sources. Required fields
are consumed explicitly by manual-mm21.py. Current checkpoint references:

- Repo: `/workspace/shared/mortal-isolated-20261119`.
- Owning build snapshot: `mortal-t034/declared-endpoint-data-first/built.json`,
  39.66s successful Unit/TestSupport build, eight Unit corroboration DLL/PDB pins.
- Accepted inert producer: `mortal-t034/declared-endpoint-explicit-env/production-main/b0884ddef4a949a69621719d4a6abc28`.
  Outcome SHA256 `94b37f56485397da4f945544c03248d9491395ddff2c63a6bb68dc1d281cb2ff`.
  ONLY `ship/{BookOfEternityClient,BookOfEternityGMBridge,BookOfEternityClient.TestSupport}`
  and four declared native files are reusable; never its `root/` or runtime state.
- Native rows: host-guardian, boe-lineage-supervisor, package-manifest.json,
  build-provenance.json. Source/compiler/flags/hash joins must remain exact.
- Terminal dependencies: existing exact pinned `mortal-t034/terminal-deps`,
  17 files enumerated by tracked terminal/provenance.json; Node fixed hash is
  in tracked relay_phase_evidence.py. No replacement downloads implied.

Snapshot recipe uses only read-only hashes: enumerate current repo source excluding
.git/bin/obj/TestResults/node_modules/__pycache__; enumerate all three complete
bin/Debug/net8.0 producer trees; join current eight Unit outputs to accepted build;
record exact source deltas (only independently reviewed noncompiler changes).
Bind guardian descriptor to its binary/provenance/source/compiler hashes, native
package sourceCommit (not local HEAD), compiler/tool versions, and accepted
producer outcome. Require all239/244/249 package projections to match full producer
outputs plus ONLY explicitly validated native assets/EventLog.Messages restore asset.
Recompute actual counts rather than using those numbers as a future allowance.

If the accepted output artifacts are lost, STOP: source recovery alone does not
recreate owning-build evidence. Use the existing reviewed build/publish workflow
and independently review new provenance instead of inventing old hashes or
reusing an unqualified world. Do not claim this README reproduces lost artifacts.

Semantic drafts remain separately reviewed T034 scenario source: P01 scripted
transport/scenario binding, P02 independent live GM grant-or-deny decision. Do not
turn a draft into a packet or use prior generated IDs/request witnesses.

After restoring source and fresh input pins, independently review source/preparation
again. Destination `/workspace/shared/mm21-20261119` must be absent including links;
all scripts remain one-shot. No automatic execution after recovery.
