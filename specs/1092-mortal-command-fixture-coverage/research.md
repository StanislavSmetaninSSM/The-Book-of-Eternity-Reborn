# Research: Mortal Command Fixture Coverage

## Decision: Treat catalog group as the source of truth

**Decision**: Use `ExplorerCommandCatalog.Descriptors` entries with `ExplorerCommandGroup.MortalWorld` as the authoritative command list.

**Rationale**: The browser command API and help/migration status are driven from this catalog. A hand-written command list would drift.

**Alternatives considered**: Deriving commands from help text or UI builders. Help can omit implementation details, and builders do not include local-turn commands consistently.

## Decision: Include practical universal Mortal World commands separately

**Decision**: The matrix has a separate section for universal commands commonly previewed in Mortal World, especially `/статус`, `/душа`, `/достижения`, `/кодекс`, `/хроника`, `/перья`, `/жизни`, `/моды`, `/правила_мира`, `/галерея`, `/story`, and `/behavior`.

**Rationale**: The user specifically needs manual preview coverage, and some universal commands are core Mortal World screens even though their catalog group is not MortalWorld.

**Alternatives considered**: Excluding universal commands strictly. That would miss previously reported surfaces like `/статус`.

## Decision: Do not auto-submit local-turn commands

**Decision**: Validate local-turn Mortal World commands only up to prompt/action display unless the command has a safe read-only preflight.

**Rationale**: These commands can create pending GM turns or mutate player state. The fixture goal is display coverage, not automated gameplay progression.

**Alternatives considered**: Submit every command with synthetic choices. That would turn fixture validation into an end-to-end gameplay test and risk corrupting the manual session.
