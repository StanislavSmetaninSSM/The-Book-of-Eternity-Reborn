---
name: spec-kit-superpowers-bridge
description: Use when a task combines Spec Kit artifacts or speckit phases with Superpowers, or resumes development governed by both methodologies.
---

# Spec Kit + Superpowers

Spec Kit owns requirements, decisions and task tracking. Superpowers supplies the engineering methods. Apply both to one feature and one set of artifacts; completing one does not replace the other.

## Establish the task

1. Read the current user request, applicable project instructions and `.specify/memory/constitution.md`. User instructions and approved scope govern the workflow; this skill grants no additional permissions.
2. Load `superpowers:using-superpowers` and the applicable phase skills through the current environment's supported mechanism. A catalog entry or remembered procedure is not a loaded skill. If invocation is unavailable but the file is accessible, read it and identify that fallback honestly.
3. Identify the target project and active feature from configured paths, `.specify/feature.json` and existing artifacts. Do not infer it only from a branch name. Resume relevant work; create a new feature only for a distinct task.
4. If project scaffolding is missing and Spec Kit is required, inspect available commands and integrations. Initialize the appropriate project configuration within existing authorization; resolve missing authorization first. Use `speckit-constitution` when governance needs creation or revision, preserving existing principles. Preserve existing files and do not install or switch agent applications as part of the method.

Ordinary questions do not initiate development. Once brainstorming applies, this user's workflow requires Spec Kit for tiny, bounded and investigative work too. Scale document length to scope; a spike records investigation findings and does not authorize production implementation.

## Map the phases

Use the actual available `speckit-*` skills/commands and execute their instructions, including applicable scripts and hooks. This table connects the methods; it does not replace the skills.

| Phase | Apply both layers | Required result |
|---|---|---|
| Define | `superpowers:brainstorming` with `speckit-specify`; `speckit-clarify` for unresolved requirements | `spec.md`: requested outcome, boundaries, preserved contracts, acceptance criteria and unresolved decisions |
| Plan | `superpowers:writing-plans` with `speckit-plan`, then `speckit-tasks` and `speckit-analyze` | Consistent `plan.md` and `tasks.md`; analysis completed before implementation or an agreed probe |
| Execute | `speckit-implement` with `superpowers:executing-plans` or `superpowers:subagent-driven-development`; `test-driven-development` for behavior changes; `systematic-debugging` for defects | Work against tasks, tested behavior and evidence; apply worktree discipline when isolation is needed |
| Review | `superpowers:requesting-code-review`; `receiving-code-review` when addressing findings | Separate independent reviewer of the completed block, preserved requirements checked, validated findings resolved |
| Verify | `superpowers:verification-before-completion` | Relevant checks run, diff checked against the original contract, tasks marked complete only with evidence |

Keep canonical documents in the configured feature directory, normally `specs/NNN-topic/`. Put the brainstorming outcome in `spec.md` and the engineering plan in `plan.md`/`tasks.md`; do not create competing Superpowers copies. Show absolute links and concise scope/acceptance summaries before implementation.

Prior explicit approval of a concrete proposal covers a specification that faithfully records it. Record that approval and proceed; document creation, version changes and routine planning add no approval gate. Ask only about a material unresolved difference or an explicitly requested separate review. A broad goal alone does not approve new material choices.

When approved requirements change or code contradicts the specification, reconcile the affected artifacts through their Spec Kit phases before dependent work. Do not silently redefine the contract to fit the implementation.

## Resume and verify evidence

Read the current artifacts and evidence; resume at the first unfinished or invalidated phase. Reuse completed, still-valid work and prior approvals. Do not regenerate the constitution, spec or plan merely because a session resumed, or reload unchanged instructions already present in context.

If a required phase was skipped, recover it before dependent work. Report the omission and affected checks; a later repair is not proof the original order was followed. Keep original acceptance criteria intact.

Choose tools and any delegation from actual capabilities. An implementation subagent is optional; independent review uses a separate context. Pass a reviewer or delegate the original requirements, active artifacts, exact changes, preserved contracts and verification evidence. If a required capability is unavailable, identify the unmet check, continue only independent authorized work and do not claim full completion.

Finish with changes, actual skill use, checks and results, independent review and remaining limitations. Distinguish installed skills, loaded instructions, applied methods and verified outcomes. A successful command or an agent's final answer alone is insufficient evidence of task acceptance. Publishing or merging still requires the applicable authorization.
