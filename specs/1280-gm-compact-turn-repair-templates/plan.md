# Plan: GM Compact Turn And Repair Templates

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1280

## Approach

- Extend `Write-GmContextPack` to generate a `Templates` directory in the
  session-local context pack.
- Add a small writer helper for generated template files and manifest entries.
- Keep copied guides/examples in place but add manifest roles for compact
  templates.
- Add `GmCompactTemplateDirective` beside existing context-pack and doc-path
  directives.
- Update normal turn and validation repair prompts so compact templates are the
  first mandatory source for common executable shapes.
- Relax afterlife example routing so full examples are opened only for
  route-specific details not covered by compact templates and matrix guidance.

## Risks

- Overly short templates could omit important fields. Mitigation: keep large
  docs available and point to them for route-specific contracts.
- Templates could drift from validation rules. Mitigation: source-guard tests
  require their generation and prompt wiring; future schema changes should
  update these templates in the same task.

## Tests

- RED/GREEN source-guard tests in `GmTurnHelperContractTests`.
- Existing documentation and harness-focused suites for regression coverage.
- Live Chaos Sea smoke after implementation.
