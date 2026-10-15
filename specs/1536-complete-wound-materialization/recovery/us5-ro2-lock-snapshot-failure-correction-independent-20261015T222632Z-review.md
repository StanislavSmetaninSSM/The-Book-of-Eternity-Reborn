# US5-RO2 lock-snapshot correction gate — 20261015T222632Z

**ACCEPT at `d80e77056c4aaa21c650e5744bc551568fe425b2`. Release fresh selected verification.**

Both failures were fixture snapshots taken while their own lease held `canonical-write.lock` with `FileShare.None`. Actual stacks fail in `Snapshot → File.ReadAllBytes` at original test lines69/100. The earlier static gate missed this conflict. This is not an application/parser failure.

The correction preserves the full-tree byte/name/directory check after lease disposal. The second test finishes its controlled write before snapshotting, then uses a separate publication-read lease for the independent refresh, checks active lease/no mutation, disposes and verifies the complete tree plus all captured/new header fields. Snapshot/cleanup helpers, production locking, declarations, selection and budgets are unchanged. No lock exclusion or exception suppression is introduced.

Independently verified **29 compressed/raw/original chains**, **2,644 immutable da source/document pins**, **10 fresh payloads equal to the external pre-runtime record**, **72 full TRX identities**, and **35 strict distinct absent reader roots**. Actual remains native1/15.3929014s: **70 PASS / 2 FAIL / 24 UNRUN**, three of seven descriptors finished. The public status descriptor is unrun. Cleanup success does not turn the two failures into PASS.

The retained pure ROOT reader correctly requires the exact four unrun descriptors. Its metadata preserves the initial ephemeral wrong empty-list assertion/no-output result. No retained original reader source was located for byte-diff proof; the corrected predicate and native evidence were independently checked. Nothing is relabeled GREEN.

Release fresh BOTH selected PlanOnly, standalone NoBuild catalog, exact selected Plan and the same five owners. Counts remain provisional **53 methods / 96 cases / 7 descriptors**, catalog **758/11653**; expected ownership **50 real roots + 2 pure integration + 44 pure unit cases**. Keep the same clean tested HEAD, v3 source/output pins and external ten-payload pre/post record. Freeze/publish the first unexpected discrepancy before correction; final independent EVIDENCE is still required.

Clean d80/source bytes and diff-check verified. Configured/requested independent Astra XHigh retained; backend telemetry unavailable. No reviewer native execution or tracked edits. RO2/full US5/FR063/lawful ACTIVE remain open.

**FINALSTOP — no pending reviewer writes.**

