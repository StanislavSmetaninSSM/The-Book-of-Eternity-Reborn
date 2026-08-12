# Public Repository Readiness Design

**Date:** 2026-08-12

**Tracked task:** [GitHub Issue #1525](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1525)
**Repository:** `StanislavSmetaninSSM/The-Book-of-Eternity-Reborn`

## Goal

Publish the existing repository and its history for outside development while
keeping the project owner's workflow authoritative, separating the licenses of
code, original game content, and restricted or third-party assets, and
preventing unreviewed collaborator changes to `main`.

The game is unreleased, under active development, and non-commercial. Public
visibility is not a release announcement or a compatibility promise.

## Chosen Approach

Publish the existing repository in place with mixed licensing and preserve its
Git history. This is preferred over either maintaining a separate public mirror
or applying one custom non-commercial license to the entire repository.

- An in-place public repository keeps forks and pull requests simple and avoids
  a two-repository synchronization process.
- Mixed licensing keeps the software genuinely open while avoiding an
  accidental grant of commercial rights to original story content or assets
  governed by separate service or third-party terms.
- A single custom non-commercial license was rejected because it would not be
  an open-source software license and would unnecessarily restrict reuse of the
  code after restricted assets are removed or replaced.

## Publication Package

The preparation pull request will add or update the following surfaces:

1. `README.md`
   - English summary followed by a complete Russian description.
   - Explicit unreleased and non-commercial status.
   - Project concept, current architecture, supported clients, prerequisites,
     local development, bounded test commands, current limitations, and
     contribution workflow.
   - No claim that an unimplemented integration or provider is available.
2. `LICENSE`
   - Standard GNU Affero General Public License v3 text.
   - Repository notices identify the software license as
     `AGPL-3.0-or-later`.
3. `CONTENT_LICENSE.md`
   - Original setting, story, lore, characters, rules prose, examples, and
     other project-authored game content are offered under
     `CC BY-NC-SA 4.0` unless a file carries a more specific notice.
   - The notice does not claim rights in third-party material.
4. `THIRD_PARTY_NOTICES.md`
   - Music generated with the Suno Basic tier remains outside both the AGPL and
     the project content license and is identified as non-commercial material
     subject to the applicable Suno terms and any third-party rights.
   - Existing attribution to source compositions or inspirations is preserved.
   - Freesound assets retain their individual CC BY 4.0 attribution.
   - Generated visual assets retain their existing provenance and per-asset
     usage notices.
5. `CONTRIBUTING.md`
   - Work must have a tracked task before implementation.
   - Collaborators use feature branches and pull requests.
   - Required project test commands and documentation synchronization rules are
     summarized and linked to their authoritative documents.
   - The project owner is the final reviewer and merger.
6. `.github/CODEOWNERS`
   - `* @StanislavSmetaninSSM` makes the owner the code owner for the complete
     repository.
7. Pull request template
   - Requires the tracked issue, scope summary, verification evidence, license
     or asset impact, and documentation impact.
8. Existing music, sound, and generated-asset provenance files
   - Updated only where needed to agree with the root licensing notices.

The copyright notice will identify:

`Copyright © 2026 Stanislav Smetanin (Lottarend)`

## Repository Presentation

The GitHub description will be:

> Unreleased non-commercial dark-fantasy RPG driven by an external AI Game
> Master. Built with .NET 8 and React.

Repository topics may describe only current facts, such as `rpg`,
`dark-fantasy`, `ai-game-master`, `dotnet`, `react`, and `non-commercial`.
Wiki and Discussions remain disabled.

## Branch Governance

The default branch is `main`; no `master` branch is introduced.

After the repository becomes public, classic branch protection or an equivalent
repository ruleset will enforce:

- collaborators cannot push directly to `main`;
- collaborator changes reach `main` only through a pull request;
- one current approving review is required;
- code-owner review is required, making `@StanislavSmetaninSSM` the required
  approver;
- stale approvals are dismissed after new commits;
- pull-request conversations must be resolved;
- force pushes and deletion of `main` are forbidden;
- the repository owner retains administrator bypass and may push or merge
  directly when necessary.

The existing CI workflow is not a required status check in this change because
its recent runs are consistently failing and it is not currently reliable as a
merge gate. A separate tracked follow-up will repair CI and decide when its
successful check becomes mandatory.

## Issue Tracker Policy

Issues remain enabled and publicly readable because they are the project's
durable task planner. Issue creation is configured as `Collaborators only`:

- the owner and invited collaborators can create and maintain tasks;
- other users can read existing tasks but cannot create new ones;
- issue templates remain maintainer tooling rather than a public support
  channel.

Pull requests remain available to outside contributors. A contribution that
needs implementation must first be associated with a task created or accepted
by a collaborator, consistent with repository governance.

## Pre-Public Security Gate

Public visibility is applied only after all of the following checks complete:

1. Scan the current tracked tree and reachable Git history for credentials,
   tokens, private keys, environment files, and other secret-shaped values.
2. Inspect remote branches and tags because every reachable public reference
   becomes visible.
3. Inspect GitHub Actions history and downloadable artifacts for sensitive
   values; remove an artifact or rotate a credential before publication when a
   confirmed secret is found.
4. Review public-facing contact information and provenance notices.
5. Confirm no license notice accidentally grants rights to excluded music,
   sounds, or visual assets.

A confirmed credential, private key, or other operational secret blocks the
visibility change until it is revoked. Its literal value must also be removed
from the current tracked tree and from downloadable GitHub artifacts before
publication. A historical occurrence may remain only when revocation is
confirmed, the owner explicitly accepts the residual scanner finding, and the
audit proves there are no additional active exposures. History rewriting is
reserved for a secret that cannot be made harmless by revocation, or for a
separate owner-approved cleanup after weighing the disruption to clones,
branches, tags, and pull requests. Mere internal story information does not
block publication.

Once public, enable the security features available to public repositories:

- secret scanning;
- secret push protection;
- dependency graph and Dependabot alerts.

Code scanning is outside this task unless it is already automatically available
without adding or repairing a workflow.

## Rollout Sequence

1. Complete the security and licensing audit while the repository is private.
2. Prepare the documentation, license, contribution, ownership, and pull
   request files on the issue branch.
3. Run documentation checks, focused relevant checks, and the repository's
   required pre-merge verification.
4. Open and merge the preparation pull request while the repository is still
   private.
5. Update the GitHub description and topics.
6. Change repository visibility from private to public, accepting that code,
   reachable history, branches, Issues, and Actions history become visible and
   the repository can be forked.
7. Immediately configure `main` protection, `Collaborators only` Issues, and
   the public-repository security features.
8. Verify the public page without relying on the owner's authenticated view,
   then verify repository settings through the GitHub API or settings UI.
9. Create the CI repair follow-up task and record the final settings and audit
   result in Issue #1525.

## Failure Handling

- If the audit finds a confirmed active secret, stop before changing visibility
  and report the exact remediation without printing the secret value. A known
  historical finding may be accepted only after confirmed revocation, removal
  from the current tree and artifacts, and an owner-recorded audit disposition.
- If visibility succeeds but protection configuration fails, stop all other
  work and apply protection immediately; until verified, the owner must not
  grant collaborator write access.
- If `Collaborators only` cannot be applied, temporarily disable Issues rather
  than accept unrestricted issue creation, then resolve the setting before
  declaring publication complete.
- If GitHub changes or rejects a security setting, record the actual available
  state and do not claim the feature is enabled.
- Returning a public repository to private does not reliably retract clones,
  forks, cached history, or already downloaded artifacts, so rollback of
  visibility is not treated as a secrecy mechanism.

## Verification

Repository files:

- license detection identifies GNU AGPL v3;
- README links and documented commands resolve;
- the mixed-license boundary is stated consistently in README, content license,
  third-party notices, and asset provenance files;
- `git diff --check` is clean;
- the project-mandated `PreMerge` lane passes immediately before merge.

GitHub state:

- repository visibility is `PUBLIC`;
- description, topics, default branch, Wiki, and Discussions match this design;
- Issues accept creation by collaborators only;
- `main` requires a pull request, one non-stale code-owner approval, and resolved
  conversations for non-admin collaborators;
- `main` rejects force pushes and deletion;
- the owner retains administrator bypass;
- no unreliable CI check is required;
- selected security features report enabled or are documented as unavailable.

## Non-Goals

- Releasing a playable or stable game version.
- Promising save compatibility for the unreleased game.
- Implementing a new model provider, network multiplayer, or another gameplay
  feature.
- Repairing the existing CI workflow inside this task.
- Removing music from the repository or rewriting Git history solely because
  the tracks use the Suno Basic tier; the owner accepts the non-commercial and
  rights-management risk and will handle a future claim separately.
