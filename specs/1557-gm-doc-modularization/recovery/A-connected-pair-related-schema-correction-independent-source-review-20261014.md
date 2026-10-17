# GM1557 related-schema correction — focused independent review

**SOURCE/SELECTION ACCEPTED** at `4645011fa1745f088b920eec6b4c6a1fd7bd5b51`, base `2e01435a8bee67358d949cb0ee2528f3aed5c753`. The confirmed Important catalog-schema finding is closed. No further confirmed Important/Critical finding in this narrow correction.

The new pair category now has one related object with the same existing `id` and a nonempty `when` explaining intentional overlap and separate retry. This meets the actual unchanged loader at `scripts/testing/TestCategoryCatalog.psm1:76–80`. Independent structural inspection found all **257** related entries valid against existing category IDs and nonempty string explanations.

Reversing only this object block reproduces the entire previous catalog SHA256 `0cdb7317f1ca320e08bdcfd2dac98fd98a1032c4386a316212196e43502e7f64`. Current catalog SHA256 is `e70fbdf34fb855e09247cb0b75bd1b94b7e0feda8f78d6b7afee4b648c7e5b9a`. Thus all other catalog bytes, IDs, selectors, case membership and budgets are unchanged. All **13** author/protected pins match; author report SHA256 `2c9462b80c5809c938972f4b44287422aeff4299fd58feda0f90386dd8dee689` matches. Fixture/helper/rules/driver/loader/runner/selection/daemon pins remain exact. Prior process-fixture source review `53edbe4d…` carries forward within its stated limits.

The active plan/tasks insertion agrees and truthfully retains the historical setup failure: no compilation/discovery/test, wrapper exit1 and native PowerShell exit UNKNOWN. The immutable failed packet and diagnosis `89eb7c30…` are not superseded by a claimed runtime success. The corrected loader itself has not been executed by this review.

Release **fresh BOTH preparation/catalog**, expected **732 categories / 11585 methods**, zero tests; no old-binary `-NoBuild` shortcut. After successful fresh preparation, exact pair PlanOnly **1 method / 2 cases / 1 integration descriptor**, budget2/default7, then only the pair actual, followed by freeze and independent EVIDENCE review. Writer1 remains separately gated. No passing-case, pure44, full-owner or primary repeat is required by this catalog-only change. No content/daemon/Block 5/full A/B release.

Review configuration requested as retained Astra XHigh; no substitution/delegation, and tools expose no independent model/effort telemetry. Only file/JSON/hash reads and this external report write were performed; no repository edits, Git, PowerShell loader/parser, compiler, discovery, project/runtime, provider or agent operation.

**FINAL STOPPED.**
