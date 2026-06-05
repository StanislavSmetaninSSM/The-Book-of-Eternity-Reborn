# Implementation Plan: Structured Authority for Mechanical Inventory Bonus Summaries

**Source Issue**: [#859](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/859)
**Spec**: `specs/859-structured-bonus-authority/spec.md`
**Branch / Worktree**: `fix/859-structured-bonus-authority` at `E:/Games/worktrees/boe-859-structured-bonus-authority`
**Constitution**: `.specify/memory/constitution.md` v1.1.0

## Technical Context

- Runtime: .NET 8 C# client, tests under `BookOfEternityClient.Tests`.
- Relevant validation code: `BookOfEternityClient/Services/Validation/`, especially inventory/player validation and cross-reference partials.
- Relevant mechanics authority: `BookOfEternityClient/Services/CharacteristicsService.cs` applies equipped item/passive skill mechanics from `structuredBonuses`; free-text `bonuses` strings are display summaries, not authority.
- Relevant display code: inventory detail surfaces under `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs` show structured bonuses, combat effects, legacy/fallback text, and unresolved/narrative summary markers.
- Relevant GM docs: `Rules/Block_10.txt` already documents `bonuses`, `structuredBonuses`, `combatEffect`, consumable effects, and readable item contracts; examples under `Examples/` may need a worked item snippet if the existing examples include mechanical bonus authoring.
- Host note: use `-p:IsTestProject=true` for `dotnet test` so SDK 10 discovers real tests.

## Architecture

Add a focused inventory mechanical-summary authority validator that classifies mechanical-looking free-text summaries conservatively, then checks for matching structured authority on the same item. The validator should live with existing validation partials or a small helper under validation services; it must not parse free-text into mechanics or change how `CharacteristicsService` applies bonuses.

Accepted authority sources should be explicit and canonical: `structuredBonuses`, `combatEffect`, canonical consumable effect data already used by the project, or an explicit unresolved/narrative classification field documented for GM output. Structured authority must match the displayed summary through exact display/description/summary text or clear target/value metadata; empty objects or unrelated entries do not authorize a summary. Player-facing inventory rendering can continue showing resolved summaries as ordinary bonuses/effects, but unresolved or narrative-only summaries must be visibly marked so they are not presented as applied mechanics.

## Spec Kit Applicability

Spec Kit is required because #859 changes validation, canonical mechanical authority expectations, and a Mortal World GM-authored inventory contract. This feature directory links #859 in `spec.md`, `plan.md`, and `tasks.md`; tasks stay unchecked until implementation and verification evidence exist.

## Testing Strategy

Use TDD:

1. Add focused failing tests for current validation accepting mechanical-looking item bonus/effect summaries without authority, unrelated authority, or empty authority objects.
2. Verify RED failures against current `origin/main` behavior.
3. Implement the smallest authority classifier/resolver that makes tests pass without broad natural-language parsing.
4. Verify focused validation, `CharacteristicsService`, inventory command/detail neighbor tests, and docs/contract tests when docs change.
5. Run independent review before PR/merge.

## Baseline Evidence

Before implementation on `fix/859-structured-bonus-authority`:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Validation|FullyQualifiedName~CharacteristicsServiceTests|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"
```

Result: passed 1223/1223, 0 failed, 0 skipped.

## Verification Commands

Run these before PR/merge:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~MechanicalBonus|FullyQualifiedName~StructuredBonus|FullyQualifiedName~Inventory" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~Validation" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~CharacteristicsServiceTests|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

If documentation/prompts/examples are changed, also run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|Documentation|PromptDocumentationCoverageTests" --logger "console;verbosity=minimal"
```

Run an added-line static scan for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting, excluding docs/superpowers plan recipes if any are created.

## Docs / Prompt Impact

Expected: yes. Because validation will require structured authority or explicit narrative/unresolved classification for Mortal inventory mechanics text, update the closest GM-facing rules/prompts/examples and any documentation/source-guard tests. Do not change afterlife contract docs unless implementation unexpectedly touches afterlife surfaces.

## Risks

- Overbroad mechanical-text detection can falsely flag flavor strings; require explicit narrative-only classification and keep heuristics focused on numeric/mechanical wording.
- Underbroad detection can miss GM-authored mechanics; include stat, skill, reputation, healing, damage, duration, condition, and activated-action patterns from the issue.
- Do not make `CharacteristicsService` parse free text; doing so would turn display strings into authority and contradict the issue.
- Keep validation output useful for GM/debug surfaces while avoiding raw technical phrasing in player-facing command output.
