# T148 M01 SOURCE review — HOLD

Candidate `8b1b80f76603a11fd6280e20246262019da8b85f`. One blocking runner finding; no material C# or selection finding. Catalog, PlanOnly and actual are still UNRUN/unreleased.

**M01-01 — preserve native outcome through observer failures.** In `t148-m01-run-phase-20261018.py:104–105`, record persistence and printing are unprotected. A failure there bypasses the return of a known native status and substitutes Python exit1. At85–88, an output-close exception after native0 is recorded only as `launchOrControllerError`; empty `observerErrors` can still permit return0. These are source-established paths, not observed native failures.

Fix only external controller finalization: retain known native status independently; treat output close, record write and summary print failures as observation failures; preserve native nonzero and hold native0 on any observer failure. A bounded fake native0/7 × clean/record/print/output-close proof should verify one invocation and no retry. Save/recover the correction and repins for affected SOURCE review before PREP.

The M01 Fact otherwise follows the accepted plan. Genuine ApplyUpdate audit is persisted before creation signing; two fixed historical rows and sentinel prevent vacuity. The direct lazy isolated-source path bypasses both caches. The new tree getter returns a detached copy; existing capture/materialize and default helper bodies are preserved. Actual item authority/readmission/composition/full refresh order, physical byte equality, current-root expiry and healed wound identity/history assertions are present. Separate Saref validation is scoped honestly. No new runtime effect, spiritual route or L01 crash dependency appears.

One exact new10-minute integration category remains sufficient. No extra broad test family is needed. Static115 checks passed: exact carrier,91 pins, inverse default seams, old catalog semantics, unchanged production and honest99+2 historical L01 accounting. Detailed hashes are in the JSON/static artifact. L0152/57, all original failures, old66529 UNKNOWN and broader holds remain unchanged.

No build, discovery, tests, runtime, observer/process action or repository edit was performed. No product defect or M01 PASS is claimed.
