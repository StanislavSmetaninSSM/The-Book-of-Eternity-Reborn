# GitHub Issue Tracking Workflow

## Source of Truth

Operational tracking for this repository now lives in **GitHub Issues**.

Issues are publicly readable project-planning records. **Collaborators only:**
the repository owner and invited collaborators can create them; this policy is
enforced by the GitHub repository setting, not by
`.github/ISSUE_TEMPLATE/config.yml`.
Outside pull requests remain possible, but their implementation must be tied
to a maintainer-created or accepted issue.

Security vulnerabilities must be reported privately through the repository's
GitHub **Report a vulnerability** flow described in [SECURITY.md](SECURITY.md),
not through public Issues or pull requests.

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
4. Run the smallest relevant Focused check, a Fast checkpoint when appropriate,
   and PreMerge immediately before merge.
5. Close the issue through the PR/merge flow.

Preferred linkage:

- branch name contains the issue id when practical
- preparation commits and PRs may use `Refs #<issue>` to link work without
  closing the issue
- completed work normally uses `Fixes #<issue>` only when automatic closure is
  intended

## Migration Note

The old markdown tracking files were retired after moving to GitHub Issues.

At migration time, the latest manual audit backlog showed **zero open tasks**, so there were no active markdown items to import into GitHub Issues.

The next new independent item should start from the next GitHub Issue created for real work, not from retired markdown numbering.
