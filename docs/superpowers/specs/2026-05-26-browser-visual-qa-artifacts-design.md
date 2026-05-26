# Browser Visual QA Artifacts Design

Tracked issues: #723 (primary), #718 (parent visual follow-up)

## Context

The Browser Client first screen has already received player-facing copy, launcher, sidebar/status, route icon/state, navigation, detail-surface, and Reborn-panel work. Issue #723 is the final visual QA closure for the #718 series: it should make regression evidence repeatable so future agents do not rely on memory or vague screenshots.

The old React project at `E:\Games\(test-version-0.9.14)-copy-of-the-book-of-eternity_-chronicle-of-the-unwritten-0.9` remains a UI/UX reference only. This task may borrow its review principles—comfortable game launcher, obvious primary CTA, tab/section hierarchy, polished cards, desktop/mobile checks—but must not copy prompts, mortal-life-only mechanics, or runtime rules.

## Chosen approach

Add a dependency-light first-screen visual QA artifact to the existing C# built-frontend smoke test. The artifact is generated under `TestResults/browser-smoke/first-screen-visual-qa.html` alongside existing smoke artifacts. It contains explicit desktop and mobile frames, derives route cards from current React route metadata, documents the old React reference checklist, and fails tests if technical/default-shell wording or emoji route icons return.

This approach is preferred over adding Playwright/Selenium because the repository has no browser automation dependency yet, CI already uploads `TestResults/browser-smoke`, and previous Browser Client closures established HTML visual smoke artifacts as the dependency-light path until a separate tracked screenshot stack exists.

## Components

- `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`
  - Generate `first-screen-visual-qa.html` after the built frontend/root/API smoke captures.
  - Assert desktop and mobile viewport markers exist.
  - Assert the artifact shows the launcher hierarchy, primary CTA, secondary actions, player route order, muted no-session states, and advanced mode as secondary.
  - Assert forbidden default-surface content is absent: raw `/api/`, debug/network/command coverage wording, old technical hero copy, repeated unavailable alerts, and emoji route tiles.

- `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
  - Add source/docs guards tying #723 to `first-screen-visual-qa.html`, the old React UI reference, primary CTA, no technical hero copy, no repeated unavailable alerts, no emoji route icons, and explicit advanced/debug secondary treatment.

- `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs`
  - Guard the runbook text so future maintainers know the command and artifact path.

- `BookOfEternityClient.WebFrontend/README.md`
  - Document the visual QA artifact, where it is written, what criteria it checks, and the fact that it is an HTML visual smoke artifact rather than automated screenshots.

- `docs/web-ui/local-web-host.md`
  - Add #723 to tracked tasks and document the visual review command/runbook and artifact path.

## Data flow

1. `npm run verify --prefix BookOfEternityClient.WebFrontend` builds `dist/`.
2. `dotnet test ... --filter "FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests"` starts the local C# host against `dist/` and writes smoke artifacts under `TestResults/browser-smoke/`.
3. `first-screen-visual-qa.html` is generated from current React source and known player-facing launcher criteria.
4. CI uploads the same path through the existing `browser-smoke-artifacts` artifact upload.

## Error handling and safety

- Missing built frontend remains a clear test failure instructing the worker to run `npm run verify --prefix BookOfEternityClient.WebFrontend` first.
- The artifact is generated under ignored `TestResults/`, not committed.
- No C# runtime, save/load, afterlife contract, or gameplay behavior changes are introduced.
- Raw local paths, secrets, endpoint internals, and debug details remain outside default player UI and outside the artifact.

## Testing strategy

Use TDD:

1. Add focused failing assertions for the new artifact path and documentation guards.
2. Run the focused .NET filter and confirm it fails because `first-screen-visual-qa.html`/docs are missing.
3. Implement minimal artifact generation and docs updates.
4. Run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
5. Run focused .NET browser/docs tests.
6. Run broader browser-related .NET tests.
7. Run `git diff --check` and an added-line static security scan.

## Scope decisions

- Automated PNG screenshots are not added in this task. They need an explicit browser automation dependency decision and separate tracked issue. The #723 artifact is intentionally named and documented as a visual smoke artifact.
- No changes are made to GM-facing afterlife contract docs because this is a Browser Client presentation/test/docs slice only.
- Parent #718 can close with #723 only if the final PR evidence maps all #718 child criteria to closed children and fresh visual QA verification.

## Self-review

- Placeholder scan: no TBD/TODO placeholders remain.
- Consistency: the selected dependency-light artifact matches existing Browser Client visual-smoke references and CI upload paths.
- Scope: the design is a single closure unit for #723 and parent #718 verification, not a new browser automation framework.
- Ambiguity: the artifact is explicitly HTML visual smoke evidence, not claimed screenshot automation.
