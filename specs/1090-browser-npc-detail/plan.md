# Implementation Plan: Browser NPC Detail Sections

**Branch**: `work/1090-browser-npc-detail` | **Date**: 2026-06-21 | **Spec**: `specs/1090-browser-npc-detail/spec.md`

## Summary

Close #1090 by turning browser NPC output into a summary-plus-drilldown flow. The default `/нпс` view remains a useful overview, and section actions open thoughts, personal quests, relationships, and skills/capabilities without raw JSON. The implementation follows TDD: audit current NPC state handling, add failing xUnit tests, then make minimal C# command-result changes. Frontend changes are only needed if existing action rendering cannot support the flow.

## Technical Context

**Language/Version**: C#/.NET 8 backend command builders; React/TypeScript only if rendering gaps appear.  
**Primary Dependencies**: `ExplorerWebCommandService`, `ExplorerMortalWorldCommandResultBuilder`, command protocol DTOs.  
**Testing**: xUnit for command output and actions; Browser Act smoke evidence.  
**Source Issue(s)**: #1090 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1090

## Verification Commands

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "Npc|NPC|Npcs|ExplorerWebCommand" --logger "console;verbosity=minimal"`
- If frontend files change: `npm run verify --prefix BookOfEternityClient.WebFrontend`
- `git diff --check`

## Constitution Check

- **GitHub traceability**: PASS. #1090 is linked in spec, plan, and tasks.
- **Task tracking**: PASS. Implementation is tied to #1090.
- **Player-facing copy**: PASS. Default output remains Russian and avoids raw debug data.
- **TDD**: PASS. New behavior requires failing tests first.
- **GM contract docs**: Conditional. Update only if NPC data shape changes.

## Likely Source Areas

```text
BookOfEternityClient/
└── UI/ExplorerMortalWorldCommandResultBuilder.cs

BookOfEternityClient.Tests/
└── ExplorerWebCommandServiceTests.cs
```

## Risk Notes

- NPC data may be spread across multiple files or embedded in custom structures. Prefer tolerant readers and existing helper patterns over a new contract.
- Avoid replacing the summary view; the issue explicitly asks to keep it.
