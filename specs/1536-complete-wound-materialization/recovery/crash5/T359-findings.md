# T359 — Windows metadata folding: causal RED

Canonical spec15r27/plan13r27, FOLD-1..4; T359 remains open. No actual game workload, generator, full owner, original five or Linux test was executed.

Compiled source `dcbbfcacb04ce5ecd99f3eb66d1ec75a7c1a04ab`: both XML builds succeeded; current four DLLs and four dependency copies match this source. Catalog and exact PlanOnly passed. Actual selection: **41 cases,32 PASS,9 expected count FAIL,0 other**. All nine failures occur at the final managed-query count assertion, after physical path/kind/exact bytes/outside sentinel/full ordinary snapshot/contained query-path assertions passed. These are managed API attempts, not native syscalls or a speedup measurement.

| Leaf | Depth | Legacy actual | Folded required |
|---|---:|---:|---:|
| directory | 1 | 4 | 2 |
| directory | 2 | 6 | 3 |
| directory | 4 | 10 | 5 |
| file | 1 | 5 | 3 |
| file | 2 | 7 | 4 |
| file | 4 | 11 | 6 |
| missing | 1 | 4 | 3 |
| missing | 2 | 6 | 4 |
| missing | 4 | 10 | 6 |

All27 real Windows cases contain complete, sequential, uncapped query receipts, including failed cases. All27 distinct owned roots are physically absent after test disposal. The remaining32 cases passed, including14 detached controls and real stable root/file/missing/blocker, actual symbolic links/dangling links/junctions, same-name replacement and observer failure. This is not a native permission/abort qualification.

No introduced XML documentation warnings were found on the new APIs. The build reports two new xUnit2009 style warnings on containment assertions; correct these with GREEN. Existing FileSystemManager CS1591 warnings refer to unchanged public declarations; whole-file source projection establishes that they predate this block.

Full raw evidence: `T359-red.json` and `T359-red-raw.zip` (50 indexed files, CRC and every SHA verified), analysis `T359-red-analysis.json`; complete external analysis tools in `T359-red-analysis-tools.zip`. Source equivalence and exact recovery are included in the raw archive. Original scripts' first offline parser attempts rejected CRLF receipts and then correctly exposed the two analyzer warnings; those were parser/reporting issues, no runtime retry or source behavior change.

Next: fresh component-local DirectoryInfo/FileInfo observation under the accepted role/fallback/trailing-separator/temporal contracts, then new and affected boundary controls, exact source/XML/bindings, separate complex SOURCE/PREP review. No claim that full recovery has been fixed or its historical timeout explained. All protected roots remain untouched.
