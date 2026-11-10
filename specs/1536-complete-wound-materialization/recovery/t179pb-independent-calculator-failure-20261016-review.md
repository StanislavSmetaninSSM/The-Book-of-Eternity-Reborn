# T179-Pb calculator failure classification

**Confirmed test assertion defect; no further actual release.** The two failures do not identify a product-parser defect. Correct only `MortalJointStoryRowCalculationTests.cs:85` from `Assert.Throws<JsonException>(Calculate)` to `Assert.ThrowsAny<JsonException>(Calculate)`. Keep the null-root case's exact InvalidDataException expectation and all other scenarios. No catch/wrap/conversion or production parser change is warranted.

Compiled source `68db745c69b7e128c2799f5f3c9cbf1f39165f05`, tested clean `fd4519248e659b19297b60c1d7319e0b10b47fc3`, frozen clean carrier `0b6e68e99a83ec9e9b0e1b2341901f54b0ef6dbd`. Independent read-only inspection using systematic-debugging: no edits, reproduction run, build or runtime operation. Parent-requested GPT-6 Astra XHigh; backend resolution telemetry unavailable.

## Root cause and evidence

All4 stored/decompressed/local raw hash chains verified. Plan/summary/full TRX agree20executed,18passed/2failed,1descriptor20cases complete, exit1/no timeout/duplicates/skips,6.3713499s. Source/test/catalog/runner drift from68db is empty and all4 current wired DLLs match preparation pins. This is an actual failed run, not GREEN; story-owner5/retained1 remain UNRUN.

Both failures are the same exact-type assertion at test line85:

- guardianId=g with `malformed`: actual System.Text.Json.JsonReaderException, line0/byte0, invalid start `m`.
- residentId=r with `[`: actual System.Text.Json.JsonReaderException, line0/byte1, unclosed array.

Both full raw stacks trace JsonDocument.Parse → unchanged StrictJsonAuthority.Deserialize line12 → ReadJointStoryObject → calculator's named byte reader → conditional guardian/resident reference lookup. The required earlier roots are the fixed valid fixtures. The separate unreferenced-malformed-root and all18other cases pass. Thus the intended refusal occurs at the correct conditional source, while the assertion incorrectly excludes the parser's JsonException subtype.

Installed xunit.assert2.9.2 XML explicitly documents Throws<Action> as exact type (line1311) and ThrowsAny<Action> as allowing derived types (line1352). The existing SpiritualWoundDependentDraftPolicyTests.cs:109 uses ThrowsAny<JsonException> for the same malformed-JSON family. StrictJsonAuthority is byte-identical to d13 and68db; preserving its public exception-family behavior is the intended extraction contract. Test correction preserves the semantic rejection and cannot allow no exception or an unrelated exception family. Additional line/byte offset, internal subtype-name or stack-string assertions are unnecessary coupling to parser/runtime details; raw stack evidence already establishes origin. No fix has been applied or verified in this review.

## G5 correction gate ruling

**Repeat first Late2 after fresh preparation; do not carry the old wiredLate PASS as the correction-cycle gate.** [Canonical G5](/workspace/The-Book-of-Eternity-Reborn/specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md) explicitly says “Late2 первый actual”, “Failure останавливает дальнейшие actuals; correction требует fresh preparation”, and limits its stated carry-forward exemption to UI-only/evidence-only changes without new impact/failure.

Applying those rules here: this remains the affected shared story-owner implementation stage, now with a new actual failure and a test-code correction; it is outside that exemption. The requirement is a policy application to this failed cycle, not a claim that the assertion change modifies the production story algorithm. Historic wiredLate2PASS remains valid at its original pins; it is not erased or relabeled. No cost-based waiver is made.

1. Root makes the single assertion correction, retains frozen failure and publishes clean WIP.
2. Fresh BOTH builds/catalog/PlanOnly, corrected source/binary pins and narrow affected SOURCE/FIXTURE/SELECTION gate.
3. First actual Late2 only, raw freeze and independent EVIDENCE classification.
4. On release, calculator20 retry; after complete PASS, separately existing story-owner5 then retained-storyfreshPID1. Stop/freeze first failure or source/binary drift; no aggregate/unrelated repeats.

## Integrity and deliverables

Exact evidence diff argv `["git", "diff", "--no-ext-diff", "--binary", "584c25db42300cd2b90bd11d2c6e8c5a451af7ac", "0b6e68e99a83ec9e9b0e1b2341901f54b0ef6dbd", "--"]` SHA256 `7e79b2726912b9a2a4cea0a20a2fe6166bfd1872f29396f0913ca0ad55487a10`; bytes `/tmp/t179pb-calculator-failure-classification-20261016-reviewed.diff`. JSON `/tmp/t179pb-calculator-failure-classification-20261016.json` pins27source files,5stored evidence files,4raw chains,4binaries, exact failure identities/stacks, local xUnit XML and all8prior report files unchanged.

Initial source reviews did not detect this subtype assertion mismatch; this classification preserves that history and the failed result. Full T179/T182/genuine S/C/B/M3/F1/T197/new-QTE and other deferred gates stay open. FINALSTOP; no pending report writes.
