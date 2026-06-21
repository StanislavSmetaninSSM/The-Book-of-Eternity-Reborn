# Feature Specification: Browser NPC Detail Sections

**Feature Branch**: `work/1090-browser-npc-detail`  
**Created**: 2026-06-21  
**Status**: Draft  
**Input**: GitHub issue #1090 — "Browser: разбить NPC detail на summary + мысли/квесты/отношения/навыки"

## Source Issues & Scope

- **Source GitHub issue(s)**: #1090 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1090
- **In scope**: Browser command output for NPC summary and drilldown details.
- **Out of scope**: Changing GM-authored NPC data contracts unless implementation proves existing data cannot support the requested sections.

## User Scenarios & Testing

### User Story 1 - Keep NPC overview useful

A player opens `/нпс` and sees the existing broad NPC summary, with enough information to choose a character without raw JSON or debug wording.

**Acceptance Scenarios**:

1. Given NPC data exists, when the player runs `/нпс`, then browser command output includes NPC names and summary fields.
2. Given a listed NPC has rich data, when the player views the overview, then detail actions are available for thoughts, quests, relationships, and skills when data exists.

### User Story 2 - Inspect one NPC section

A player opens a specific NPC section such as thoughts or personal quests without reading a giant all-in-one dump.

**Acceptance Scenarios**:

1. Given an NPC has thoughts/journal entries, when the player opens that section, then the result shows only those entries in Russian player-facing copy.
2. Given an NPC has personal quests, when the player opens that section, then quest summaries are listed and individual quest detail actions are available.
3. Given a section is missing, when the player opens it, then a short empty state explains that the section has not been recorded yet.

## Functional Requirements

- **FR-001**: `/нпс` and `/npc` MUST keep the current summary behavior as the default overview.
- **FR-002**: NPC overview MUST expose section actions for thoughts/journal, personal quests, relationships/social status, and skills/capabilities when corresponding data exists.
- **FR-003**: NPC section routes MUST render player-facing Russian detail output without raw JSON, file paths, API wording, or debug terms by default.
- **FR-004**: NPC personal quest sections MUST expose detail actions for individual quests when multiple quest records exist.
- **FR-005**: Missing NPC, missing section, and missing quest detail MUST degrade to short human-readable messages.
- **FR-006**: Browser actions MUST use stable commands that work from command output without requiring the user to manually compose selectors.
- **FR-007**: If accepted NPC data shape changes, update GM-facing docs/examples/tests in the same PR.

## Key Entities

- **NPC entry**: Canonical character record with name/id and optional rich sections.
- **NPC section**: A readable subsection such as thoughts, quests, relationships, or skills.
- **NPC quest**: A personal quest record nested in or associated with an NPC.

## Success Criteria

- **SC-001**: xUnit tests cover seeded NPC thoughts, quests, relationships, skills, missing section, and quest detail behavior.
- **SC-002**: Default browser NPC output contains no raw JSON/debug/internal path wording.
- **SC-003**: Browser Act evidence covers `/нпс`, one NPC thoughts section, and one NPC personal quest section.
- **SC-004**: Focused backend tests for NPC/browser command behavior pass.

## Verification Plan

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "Npc|NPC|Npcs|ExplorerWebCommand" --logger "console;verbosity=minimal"`
- Browser Act against local web host: `/нпс`, open NPC detail, open thoughts, open personal quests.

## Assumptions

- Existing canonical NPC test data can represent thoughts, quests, relationships, and skills without a GM contract change.
- Browser command result actions are the preferred drilldown mechanism for this issue.
