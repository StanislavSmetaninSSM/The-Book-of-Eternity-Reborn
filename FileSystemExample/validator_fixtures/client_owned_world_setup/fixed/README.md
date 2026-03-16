# Fixed example

Correct behavior:

- the client owns `incarnation_world_setup.json`
- the client owns `world_directives.json`
- the GM reads them as input contracts and writes ordinary game files instead

If the current accepted turn is an incarnation turn, the client may materialize pending setup into active world directives **after** accepted-turn validation succeeds.
