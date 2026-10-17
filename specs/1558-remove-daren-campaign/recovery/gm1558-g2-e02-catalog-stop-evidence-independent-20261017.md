# Independent E02 catalog preparation causal review

**Verdict: cause accepted; managed SOURCE/PREP remains not ready.** Bound to source `17329db678313999b77ef53660d7ffb8291bcff8` and published STOP `a208c99d7ab62b2239ae770724ae20d8c6f13bea`.

The FIRST frontend build succeeded (native 0, 2.935344375s). The subsequent catalog command failed (native 1, 0.664716533s) before managed compilation, discovery or result-directory creation. Both plans remain unrun; zero tests executed. The two launchers settled, their PIDs are absent, seven existing f076 DLLs and owned namespaces are unchanged.

**G2-E02-P01 — configuration defect.** `tests/categories.json:28515` gives the new `daren-retirement-affected-client-contracts` category a string `related[0]`. Existing loader `scripts/testing/TestCategoryCatalog.psm1:76–80` requires each relation to expose `id` and nonempty `when`. Under StrictMode the string's missing `.id` produces the exact error reported at `scripts/test-csharp.ps1:57`. A static scan of all 817 categories found this sole malformed relation. This is a preparation defect, not product behavior or retirement RED.

Repair only that relation to the existing `{id, when}` shape, retaining its target and documenting its real relationship. Keep the loader, selectors, ownership, budgets, C#/React/fixture bodies, assertions and product unchanged. Update the catalog pin/digest.

All 13 frozen/live/committed chains and all 25 recovered files / 21 metadata paths match independently checked hashes, Git blobs and modes. The successful frontend source tree `8c9c5236b91d587407b2b6ba89070ee25c888d57` and all 113 current dist files match its frozen manifest; this build may be retained across a catalog-only correction. It proves neither frontend tests nor browser behavior.

After decision persistence and exact corrected-source publication, the remaining preparation is canonical BOTH/catalog, full current PlanOnly-NoBuild and remaining PlanOnly-NoBuild, all zero execution. Fresh managed pins and exact selection partition require the pending targeted SOURCE/PREP review before Root releases runtime. No npm replay is needed for unchanged frontend inputs/output. The accepted five earlier guard cases and earlier failure history retain their existing provenance; this review does not replay them.

Support: `/tmp/gm1558-g2-e02-catalog-stop-independent-check-20261017.json` — SHA256 `7d93044b120aa8a0ec12b1489400d0f49999753d4f248f6034f707559144f6ca`. JSON: `/tmp/gm1558-g2-e02-catalog-stop-evidence-independent-20261017.json` — SHA256 `30187f18d090b69931694b280a59c06bf24284c0667e20ad1ba958efae28a88d`.

Reviewer performed read-only source/raw/hash analysis only; no loader, build, test, producer or browser execution.
