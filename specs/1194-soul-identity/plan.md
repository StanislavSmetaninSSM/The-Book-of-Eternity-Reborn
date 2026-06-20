# Implementation Plan: Player-authored Soul Identity

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1194

## Approach

Reuse the existing `SoulIdentityService` and `/душа` flow. `soulName` is already local player-owned state, so the form description belongs beside it in `soul_state.json` and should use the same canonical write policy.

## Changes

- Add `soulFormDescription` to canonical/patch/lifecycle top-level soul-state key allow-lists.
- Add validation for optional `soulFormDescription`: must be a non-empty string if present.
- Extend `SoulIdentityService` with normalized update behavior for the form description.
- Extend new game flow to ask for the initial form description and persist it.
- Extend `/душа` with display and a local "change soul form" action.
- Extend universal meta/browser read-side soul summaries with the new field.
- Update GM-facing docs/examples to treat `soulName` and `soulFormDescription` as player-authored context.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "SoulIdentityServiceTests|ExplorerModeCommandTests|GuardianPolicyContractDescriptorTests|ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|LocalWebUiHostTests|BrowserApiContractTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ValidationService"
git diff --check
```

## Risks

- Canonical soul-state sanitizers can silently drop unknown top-level keys unless all allow-lists are updated.
- Source-guard tests may require updates when GM-facing documentation changes.
- Browser contract snapshots may need deterministic constructor/order updates.
