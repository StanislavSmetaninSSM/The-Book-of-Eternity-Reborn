# Web UI Console Dependency Audit Plan

Tracked task: #560

Goal: inventory direct console/Spectre dependencies that block a local browser UI and map every command group to a migration recommendation.

Steps:
- [x] Collect direct `AnsiConsole`, `Console.Read*`, `IRenderable`, `IPrompt<T>`, `SelectionPrompt`, `TextPrompt`, `ConfirmationPrompt`, `Panel`, and `Table` usages.
- [x] Inspect `ExplorerMode` command groups and lifecycle/QTE entrypoints.
- [x] Produce a durable audit document with command-group recommendations and follow-up issue links.
- [ ] Verify the repository still builds/tests after the documentation-only audit change.
- [ ] Commit, merge to main, push, and close #560.
