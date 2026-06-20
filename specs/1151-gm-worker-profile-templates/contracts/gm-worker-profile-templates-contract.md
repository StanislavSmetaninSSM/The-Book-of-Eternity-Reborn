# Contract: GM Worker Profile Templates

## Template Catalog

`GmWorkerBridgeProfileTemplates.CreateDefaultTemplates()` returns:

- `validation_repair_codex`
- `narrative_draft_codex`
- `analysis_codex`

## Required Template Properties

Every template must satisfy:

- `enabled`: `false`
- `launchVisibility`: `hidden`
- `maxConcurrentTasks`: `1`
- `launchCommand`: contains `BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1`
- `launchCommand`: contains `-AgentCommand`
- `GmWorkerContractValidator.ValidateProfile(template).IsValid == true`

## Preservation Rule

When `GameSettings.ApplyLoadedValues` receives one or more configured worker profiles, it normalizes and preserves those profiles. It must not append templates, replace profile ids, or enable a worker implicitly.

When it receives no worker profiles, it supplies the disabled template catalog for discoverability.
