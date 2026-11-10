# US5-RO2 header correction — 20261015T215922Z

**ACCEPT H1/F1** at source `b07853b1358aaec14bc1f39676f8970865ed0756`, carried unchanged by clean `f2df7a320af53875afa6536c19cbc2b953d8a570`. Release fresh selected verification.

The afterlife identity header now uses captured `State.SoulName`, `SoulFormDescription` and `Incarnation`; no post-lease `soulRoot` read remains in that header. Escaping/fallbacks and independent supplementary panels are preserved.

The existing real snapshot test now seeds distinct identity values before its baseline, retains one-lock/no-mutation/no-repair/wound assertions, changes all three fields plus realm afterward, and checks both new State and retained original snapshot. The bounded source guard rejects `soulRoot` and requires all captured fields. Existing APIs bind correctly by inspection; no declarations, selection, budgets or case counts changed. Source bytes match b078/f2df; prior final review copies are exact; diff-check passes.

Release, in order: fresh BOTH `-SelectionFile tests/selection.json -Parallelism 1 -PlanOnly`; standalone `-ValidateCatalog -NoBuild`; exact selected NoBuild Plan; then the same five selected runtime owners. Provisional **52 methods / 95 cases / 7 descriptors**, catalog **758/11652**, with **50 real cleanup roots + 2 pure repository integration cases + 43 pure unit cases**. Official discovery must confirm. The narrow accepted evidence-v2 classification remains unchanged.

Use fresh outputs. Freeze/publish the first unexpected failure before correction; final independent EVIDENCE remains required. Current GREEN is UNRUN. Full US5/FR063/lawful ACTIVE remain open.

Requested/configured Astra XHigh retained; backend telemetry unavailable. No reviewer tracked edits or native execution.

**FINALSTOP — no pending reviewer writes.**

