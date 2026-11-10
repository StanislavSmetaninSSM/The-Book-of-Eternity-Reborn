# FR063 scope1 observer correction — independent SOURCE/FIXTURE/SELECTION review

Base: `79e6c7a4e2d35910212f1005a240eb799a6973f7`.
Reviewed head: `33dc6a7bb5755f1d390fbf0a283c9e778584eea8`.
Branch: `1536-wound-b2-cloud-20261012`.
Requested configuration: Astra XHigh. Actual backend model/effort telemetry unavailable; no self-switch or substitution is claimed.

**SOURCE ACCEPTED / FIXTURE ACCEPTED / SELECTION ACCEPTED** for the bounded scope1 observer correction and first exact Late2 outcome. No confirmed Critical or Important finding. Static acceptance is neither compilation nor runtime PASS and does not release scope3 or the later actual cohorts.

## Reviewed diff and rationale

Exactly four files changed: the existing JointCompletion.cs fixture, identical 18-line WIP fronts in plan.md/tasks.md, and tests/selection.json. No production, category budget, public authority or publisher-member change. `git diff --check base..head` passed; reviewed head was clean.

`JointCompletion.cs:465–536` retains both actual transaction IDs and complete cloned member images in ReportRemovalHistory. The baseline classification requires no prior baseline/phase record, pre-dispatch zero repairReplies/no repair or common-marker milestone, absent draft and exact held command. Phase consumption requires exactly schedule/report/same-marker, exactly three members, a distinct transaction from the baseline, one repair response and no earlier consumption.

The phase before-marker must equal the actual previously observed common marker; before is current original-bound schema-v2/unapplied and after is applied. Removing only marker fingerprint and changing only phase must leave deep equality. Schedule before/after and report before bind the marker's exact images; schedule/marker physical readback and actual report absence are checked. Only the real phase increments the counter, from zero once; final existing phase/full-union assertions remain. The observer still captures exceptions as fixture failures and does not deduplicate callbacks or merely permit two consumptions.

Ordering is grounded in existing producer and publisher, not guessed: `MortalQteDecisionPublication.MortalJointProgressionPublication.cs:65–76` emits schedule/report/marker, and `TrustedLocalFilePublication.cs:225–231` preserves array order. The report callback therefore sees only the prior common-marker milestone. Encoding.UTF8.GetString/GetBytes preserves the marker's UTF-8 BOM in the existing Raw capture.

The zero/nonzero Late category owns the actual affected path and preserves its 5-minute budget (`tests/categories.json:5–33`). tests/selection.json selects only that category and gives current FR012/FR063 reasons. BOTH preparation/PlanOnly/catalog are zero-execution requirements; checkpoint5 is unchanged and should not be repeated. This limited first run measures observer correction. Shared live/cold/refusal helpers remain unqualified; no inference of their runtime success is made.

Known independent blockers are intact: canonical severity is still incorrectly read as scalar at reviewed JointCompletion.cs:793, and the completed-v2 consumed-draft/full-output problem remains as documented in the separate design review. The first actual result must be frozen and classified even if these produce a native failure.

## Restored external runner

The external `/workspace/boe-fr063-tools-20261015/fr063-root-runner.py` differs from tracked `recovery/fr063-bounded-root-runner-v4-20261014T224131Z-d5b9895d.py.txt` in exactly the REPO absolute path. Independent SHA256 readback:
- Tracked source: `d94fa4c79ecb523b3cca5e52921fd1a0801cacab70825eaeebc7a7191e4888e8`.
- Adaptation: `206e12c10c20e063bdf2b9924c000d12fa2d02005024ff3076c873d73a17e78a`.

Path-only adaptation is sufficient for this existing runner's use after fresh current-source gate proof/pins and exact remapped evidence hashes. It does not establish environment/build compatibility; preparation does that. The runner's historical len(permittedActualSelections)==14 assertion (line39) is an inventory constraint, not authorization to execute all fourteen. Gate documentation must state current actual release is only Late2, later12 HELD and checkpoint5 unchanged; actual invocation must select Late2. Do not spoof old SOURCE acceptance for changed fixture, omit selection pins, reuse old binaries, or interpret preparation success as test success.

No build, discovery, tests, runner action or paid provider call was executed by this reviewer. No repository file was changed. Only expressly authorized external review artifacts were authored. See companion JSON for exact Git-object and external-file pins.

