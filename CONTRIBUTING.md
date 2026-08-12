# Contributing

Thank you for helping with The Book of Eternity: Reborn. The project is
unreleased and changes rapidly.

## Before implementation

Every implementation change must have a tracked GitHub Issue created or
accepted by a collaborator. Public issue creation is intentionally restricted
to collaborators; an outside contributor should discuss a proposed change in
an existing relevant pull request or contact a maintainer before investing in
large work.

## Branch and pull-request workflow

All contributors and collaborators other than the repository owner work on a
feature branch and submit a pull request to `main`. `@StanislavSmetaninSSM` is
the repository-wide code owner and final approver. Do not force-push or delete
`main`.

Name a branch with its issue number and a short description, for example
`1525-public-repository-readiness`. Use `Refs #<issue>` in preparation commits
and pull requests to link work without closing its issue. Use `Fixes #<issue>`
only for completed work when merging the pull request should automatically
close the issue.

The repository owner retains the direct/admin bypass reserved by the GitHub
repository settings. Every other contributor or collaborator must use the
feature-branch and owner-reviewed pull-request workflow above.

## Verification and worktree care

Follow the lane guidance in [docs/testing.md](docs/testing.md): run the
smallest relevant Focused test during implementation, a Fast checkpoint at a
meaningful point, and PreMerge immediately before merge. Preserve
user-owned and unrelated changes in a worktree; do not discard or commit them
as part of your contribution.

## License, assets, and game documentation

Software, code, and script contributions are licensed under
`AGPL-3.0-or-later`; see [LICENSE](LICENSE). Original project-owned narrative
and content contributions are licensed under `CC BY-NC-SA 4.0`; see
[CONTENT_LICENSE.md](CONTENT_LICENSE.md). Third-party/excluded assets are not covered by either blanket grant:
contribute them only with appropriate rights, provenance, and required notices.

Follow the contribution and asset requirements in [AGENTS.md](AGENTS.md),
including its GM and afterlife documentation synchronization rules; changes to
GM-authored or afterlife contracts must keep the required prompts, docs,
examples, manifests, and guards aligned.
