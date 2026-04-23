# GitHub Issue Tracking Workflow

## Source of Truth

Operational tracking for this repository now lives in **GitHub Issues**.

Use GitHub Issues for:

- bugs
- audit findings
- tasks
- user stories
- technical debt

Do **not** create or maintain markdown backlog / handoff logs for day-to-day issue tracking.

## Labels

Use the repository labels already configured in GitHub:

- type:
  - `bug`
  - `audit-finding`
  - `task`
  - `story`
  - `tech-debt`
- severity:
  - `severity: high`
  - `severity: medium`
  - `severity: low`
- subsystem:
  - `subsystem: chaos-sea`
  - `subsystem: shining-abode`
  - `subsystem: afterlife-ui`
  - `subsystem: runtime`
  - `subsystem: validation`
- source / workflow:
  - `source: audit`
  - `status: triaged`
  - `status: in-progress`
  - `status: verified`

## Working Rules

For every new confirmed defect or work item:

1. Create or update a GitHub Issue.
2. Put implementation on a dedicated branch.
3. Reference the issue in commits and PRs.
4. Run focused checks and the full suite as appropriate.
5. Close the issue through the PR/merge flow.

Preferred linkage:

- branch name contains the issue id when practical
- commit message contains `refs #<issue>`
- PR body contains `Fixes #<issue>` when the merge should auto-close the issue

## Migration Note

The old markdown tracking files were retired after moving to GitHub Issues.

At migration time, the latest manual audit backlog showed **zero open tasks**, so there were no active markdown items to import into GitHub Issues.

The next new independent item should start from the next GitHub Issue created for real work, not from retired markdown numbering.
