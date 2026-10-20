# T321: authenticated story readback reuse, actual verification

Compiled source `d41f80f347a4764164de866a2c5ae7a100f373cc`. Actual selected run `20261021-002538-597-36040-6c78380a38184b1f876115762fb1c984-categories`:3/3PASS, no skip/timeout; full selection and owned/runtime cleanup complete. Three independent copied DATA roots removed and independently absent. Immutable ZIP/source record and four PREP DLLs unchanged (three are actual integration dependencies). Raw complete JSONL equals TRX; all sequences and paired intervals verified. See T321-components-outcome.json/raw.zip. Final independent review pending; original recovery remains FAIL/open, LinuxUNRUN.

The real append asserts3source reads, old causalRED4; repeat asserts2, previous observed3. Shared pure predicate controls12PASS cover all expected proof fields/fullhistory maps. Actual positive also validates its proof afresh, rejects foreign files, keeps exactly one row on repeat, and rejects later story corruption without game mutation. Separate preappend narrative change refuses without append; deliberate change inside retained post-append readback returns PublicationUncertain/nullproof and preserves every captured game-file image. Expected proof histories come from original source, not readback. The callback-free comparison/issuance suffix is source-reviewed; later fresh validators/lease/owner checks retained.

| Actual positive component | T318 seconds | T321 seconds |
| --- | ---: | ---: |
| `Component.ReadAppliedPhaseAsync` | 3.000368 | 3.237898 |
| `Component.ReadUnappendedStoryAsync` | 10.783935 | 11.637427 |
| `Component.AppendStoryAsync` | 41.476929 | 33.395220 |
| `Component.ValidateCurrentStoryProofAsync` | 11.084355 | 11.291933 |
| `Component.RepeatAppendStoryAsync` | 30.708067 | 22.330003 |
| `Component.ValidateStoryProofAfterDriftAsync` | 7.866493 | 7.981990 |

Inclusive observed times, not additive across nested rows and not a controlled wall-time speedup experiment. The mechanical reduction is verified by real call counts:

| Positive case operation | T318 count | T321 count |
| --- | ---: | ---: |
| `JointStorySource.VerifyCurrentSessionOperation` | 10 | 8 |
| `JointStorySource.BindContinuationAsync` | 10 | 8 |
| `JointPhase.ReadCompletedProofAsync` | 20 | 16 |

All produced diagnostics are retained without count/byte/lifetime quotas. Fewer records reflect eliminated operations/scopes; no truncation or skipped writer output. One current authenticated readback now covers initial proof equality/issuance under the participating lease; this does not promise immutability against concurrent direct filesystem mutation or equivalence to a later reread.

Next: independent final evidence review, then generate/bind genuinely matching final-source42 DATA once and return to the original seeded cut0 gate at unchanged limits. Do not rebind historical seed, repeat unchanged failures, increase timeouts or end the open recovery objective at this component report. Existing process/NativeLineage path and GM/player/schema contracts are unchanged.
