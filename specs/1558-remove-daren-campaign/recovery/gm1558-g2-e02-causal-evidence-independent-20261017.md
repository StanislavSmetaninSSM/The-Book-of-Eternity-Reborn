E02 causal EVIDENCE: **accepted partial result and fixture/source-guard causes; current GREEN remains incomplete**.

Carrier `386ca3c206d4ea7564374470acdb5624e848b269`, actual `c5e620236f30a02415cafb64e28d7c4d9ffea1f9`, compiled `f07643a8bf611391caffb5bf21afe2ea1b4db88f`. Native1/10.032591380s:2 descriptors,17 unique actual cases, **14 PASS/3 FAIL**. Remaining17 descriptors UNRUN. Verified14 frozen chains, actual-source binding,27 recovered files/all23 metadata paths,7 stable DLLs, settled launcher, unchanged namespace and successful cleanup. No new fixture roots/artifacts.

Two precise causes:

- **G2-E02a:** both DTO failures reach full `JsonNode.DeepEquals` at line132. The only structural differences are missing `loadGeneration:null` in main-menu and `menu.loadGeneration:null` in api-error. Current C# DTO/default WebJson serialization requires those sample null properties. Add only those two fields; keep DTO/serializer/TypeScript contracts and full equality assertion unchanged.
- **G2-E02b:** React guard line210 expects obsolete hook destructuring. Actual ShellContext aliases `loadBrowserStateCore` and wraps it with current continuation/load guards. Moreover, unreached checks217–223 target the old hook file, while real `useShellState` delegates to `hooks/loadShellState.ts`. Correct the guard's actual source acquisition and import/delegation/wrapper assertions; preserve all seven Promise/getter/advanced checks on their real owner. Add exact pins for the two actual hook files, not production changes.

**Count clarification:** the MemberData method expands11 actual fixtures despite discovery estimating1. Thus17 actual cases have7 method testIds; they are not duplicates. The frozen packet's81C#UNRUN wording is incorrect: remaining descriptors contain91 C#estimates+8 frontend file estimates. Preserve frozen history and correct current projections.

Retain **5 independent PASS** (corrected inventory1 and four other unit Facts). The nine successful API rows remain truthful history, but the minimal canonical method-granular correction requalifies the existing whole API11-row method plus React1. No new row framework, generator split or blanket third aggregate is needed.

Move only those two existing methods into a narrow owner; full selection preserves all contracts, while remaining selection excludes exactly5 retained methods. Proposed full18owners/20descriptors and remaining17owners/18descriptors require fresh native confirmation; discovery estimates are not final actual totals. Fresh frontend build, BOTH/catalog, then full and remaining PlanOnly-NoBuild preparation are justified by the changed JSON tree/ownership. Concrete affected SOURCE/PREP and Root persistence/activation must precede actuals. Chromium remains held.

No live gameplay/host/browser behavior or final GREEN is established by these three failures. Prior accepted source/G1 history is retained without replay. No reviewer runtime or repository writes.

Support: `/tmp/gm1558-g2-e02-stop-independent-check-20261017.json`, SHA256`9df6606019c48fe493e5a8ebf8cfacbd3de9a63e07aab2317a83196edf394119`.
