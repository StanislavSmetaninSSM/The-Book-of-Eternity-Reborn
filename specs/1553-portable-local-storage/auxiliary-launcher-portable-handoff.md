# T050-AUXILIARY-LAUNCHER-PORTABLE handoff

Accepted base: audio `0c526b3c2749a545fce9dc5ae41053d7045dbb5a`.
Frozen runtime/tests: `39fb7534a8a492dbb22c9ee530d1b6a9397566e5`.
[Plan](auxiliary-launcher-portable-plan.md) · [qualification](recovery/auxiliary-launcher-qualification.json) · [design](recovery/auxiliary-launcher-design-review.json) · [source review](recovery/auxiliary-launcher-source-review.json).

Existing `Launcher/bookofeternity.ps1 prepare-turn` now starts the sibling client DLL,
requires its deps/runtimeconfig and existing .NET8 shared frameworks/PowerShell7,
and reports precise unavailable resources/capabilities without compiling, installing
or retrying. Selector `Command` preserves positional consumers and prevents native
PowerShell `--action` binding to the former `Action` selector. Preparation binds only
the first launcher-supplied root; an existing-directory payload cannot redirect writes.
Invalid retained journal data uses existing preparation stderr/exit2 instead of an
unhandled process exception. No storage recovery, fence, main launcher or GM rules changed.

The publisher's ordinary client publication already ships launcher, participating
helper and operational guidance; no new launcher/package bypass was added. Real tests
published, relocated to Unicode/spaces, changed cwd and used isolated runtime-only
host/shared8 with no SDK/compiler/player sources. CLI/model/profile/options remain
exact; helper never launches configured CLI or sends/replays a GM operation.

21distinct scoped PASS =20new+1exact affected launcher guard. Evidence combines16new
PASS at721d5483, remaining4at39fb7534 and oldguard1at2a67dd2b. Source reviewer verified
unchanged successful paths; catch-only correction did not repeat them. Catalog valid:
387categories/11097methods/0tests; final PlanOnly21cases/0tests. Historical42executions
retain37PASS/5FAIL, separate binding/csproj/root/storage causality and exact failfast
underfill. One invalid native-comma runner selection refused before any workload.
An initial failure misattribution to documentation was corrected from direct TRX;
no documentation assertion failure occurred.

Final evidence phases contain20own root cleanups (including one storage RED),41guardian
reports all exclusive ECHILD/failure0;19passing-fixture roots/39reports. Ordinary routes
needed0emergency signals. One deliberate parent observation timeout retained the
original guardian/drains until its own500ms scoped deadline and actual reap, with1
original pidfd emergency signal. Missing/unconfirmed report cannot remove a root or
claim cleanup. Physical fixture cleanup does not settle logical production debt.

Source contracts kept: existing participating main pin/worker/storage/generation,
held canonical lease, pending snapshot/authority lifetime, manual explicit second
preparation, no unknown-command replay. Actual Linux tests cover linked UTF8/multiline
options/dice/IDs, precise missing resources/frameworks, invalid action/dice, cold main,
worker and retained storage debt, root redirection/nonexistent root, ordinary positional
status and operational docs. Live original owner behavior reuses accepted M1/F2 source;
it was not requalified by this no-GM helper slice.

Actual provenance: SDK10.0.401, runtime8.0.31, PowerShell7.5.4; publisher guardian
compiler cc(Debian14.2.0-19)14.2.0. Compiler/SDK are absent from player fixture runtime.
No frontend changes/tests or device access. Operational docs separate legacy
Windows/source recipes, current packaged helper and accepted bounded Load/browser debt.

Independent actual Sol6.1/xhigh design/source/evidence/metadata PASS.
[Evidence review](recovery/auxiliary-launcher-evidence-review.json) /
[metadata review](recovery/auxiliary-launcher-metadata-review.json) verified final294artifacts,
29sourcepins and all21passing bindings;
[candidate GitHub restore](recovery/auxiliary-launcher-candidate-restoration.json)
verified21216trackedbytes/clean/fullfsck/fullhistory/noalternates. The final metadata
carrier receives ordinary push/exact remote/byte readback and a fresh GitHub-only
restore before delivery; its exact SHA/proof is returned by the writer separately. No successful runtime execution is repeated for metadata.

Native Windows execution remains unqualified; platform-neutral DLL/PowerShell route
is implemented, legacy Windows main/ConPTY/Job behavior is preserved in source. No
systemd setup/qualification, desktop helpers, Q1/Q2, live browser/provider/GM, real
saves, auth/network/settings or cold guarantee. Full T050 is open. Stop after verified
carrier; separate folder/image capability work and other blocks are not started.
