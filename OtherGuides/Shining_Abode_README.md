# Shining Abode Docs README

This directory keeps only current GM-facing Shining Abode guidance. Historical
implementation plans and design/audit notes were moved out of `OtherGuides` so
the GM does not accidentally treat old coding-agent planning as live prompt
context.

## Current Source Of Truth

Read these files for live Shining Abode turns:

1. `OtherGuides/Afterlife_Contract_Matrix.md`
   - mandatory afterlife contract matrix;
   - active Shining pending/control rows;
   - realm gates, wrong-realm rules, lifecycle guards, and examples mapping.
2. `OtherGuides/Shining_Abode_Contract.md`
   - compact Shining Abode contract summary;
   - current ownership model, local route rules, politics surfaces, and
     implementation constraints.
3. `Examples/E_CLI_Afterlife_Turns.txt`
   - worked examples for accepted afterlife turns.

If these documents conflict, use this priority:

1. `Afterlife_Contract_Matrix.md`
2. `Shining_Abode_Contract.md`
3. Worked examples
4. Runtime validation error details

## Archived Historical Material

Old Shining design and implementation documents are archived under:

`docs/audits/afterlife/shining-abode/`

They preserve background formulas, design intent, and historical decisions, but
they are not normal GM prompt context and must not override the current contract
matrix or `Shining_Abode_Contract.md`.

Use archived material only when a tracked issue explicitly asks for historical
formula/detail recovery. If a useful rule from the archive becomes live
guidance, copy it into `Shining_Abode_Contract.md`, the contract matrix, or an
example instead of linking the archive back into normal GM reading order.
