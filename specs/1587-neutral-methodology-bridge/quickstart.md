# Bounded verification

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1587

1. Run skill-creator quick_validate.py on the bridge; expect exit 0.
2. Compare both SHA-256 values with approved upstream; expect 2/2 exact matches.
3. Inspect explicit changed paths; expect only bridge and this feature.
4. Read full remote commit SHA; download listed changed files and required governance instructions at that SHA into a fresh empty directory; verify blob/content hashes.
5. Independent reviewer checks original issue, exact diff, preserved contracts and test-selection rationale. API downloads prove no full checkout, game build or runtime execution.

