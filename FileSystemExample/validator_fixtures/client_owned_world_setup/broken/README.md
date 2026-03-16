# Broken example

This scenario represents a GM-authored mutation of client-owned world setup files.

The GM must **read** these files:

- `game_state/control/incarnation_world_setup.json`
- `lore/current_world/world_directives.json`

The GM must **not write** them in accepted turns.

Typical validator result:

- `client_owned_world_setup_state_modified`
