# Connected SOURCE delta — 116393ee

Reviewed only immutable `e8fd65a4 → 116393ee`. **Final SOURCE remains open; no build or runtime was run.**

Three concrete corrections remain:

- **S06:** selector mtime assertions use a local declared in another method; move the capture to the actual selector method before compilation.
- **S07:** the legacy uncertain cache precedes selected physical recovery, and the publisher repeats that cache fence. Current selected proof must control recovery without weakening legacy1 or unresolved-state holds.
- **S08:** the new I-close fixture correctly proves immediate retention, but incorrectly requires permanent Held after explicitly entering a new current Main. The accepted contract permits that separate owner to validate and resume Published DATA without another projection. Keep direct Restore refusal distinct.

Residual S01 marker handling and S05 crash-proof/cleanup gaps are addressed in source. New close, returned-read and immutable-Ready controls have real production boundaries; their execution remains unqualified. Verified 82 pins, ledger/digest, selection mirror and static 24 methods/43 rows/8 owners. Two actual reflection-definition dependencies are pinned in the JSON support.

No full NI/HOT, PREP or runtime acceptance is issued. Remaining common/terminal/restart controls and final selection stay open.
