# Guardian Policy Hardening Prompts

Использование по циклу:

1. Сначала дать на чтение `01_Hardening_Review_Prompt.txt`.
2. После review-ответа дать `02_Hardening_Fix_Plan_Prompt.txt`.
3. После плана дать `03_Hardening_Implement_Prompt.txt`.

Если нужен более жёсткий режим, можно в review prompt вручную добавить:

`Игнорируй leaf bugs, если они не открывают authority bypass, destructive-path bypass или validator/runtime drift.`

Цель этих prompt-файлов:

- не делать общий code review;
- не распыляться на косметику;
- добивать архитектурную консолидацию policy-sensitive guardian paths;
- фиксировать найденные seams через shared gateway/kernel layers, regressions и source guards.
