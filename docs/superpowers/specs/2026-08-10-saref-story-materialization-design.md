# Крылья над Бездной: материализация сюжетного состава и квестов

- Tracking epic: [GitHub issue #1519](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1519)
- Source design: [GitHub issue #532](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/532) and `2026-05-20-wings-over-the-abyss-design.md`
- Date: 2026-08-10
- Status: Draft for user review

## Контекст

Скрытая линия Сарефа уже имеет сюжетный дизайн, канонический progress-state,
команды, validator/normalizer, десять built-in Предвечных Хранителей, полные
досье и сорок написанных dark-fantasy quest blueprints. Однако эти части пока
не образуют единый материализуемый контракт.

Текущий built-in Guardian существует как `manifest.json` и `dossier.md`.
Выбранный на New Game системный Хранитель получает детерминированный Guardian
record и common afterlife profile с Actor Materialization envelope, но часть
содержания остаётся общим техническим seed: `questManagement` и
`personalQuests` пусты, цели и Actor Brain authority не передают всю авторскую
роль, а story questline остаётся отдельным Markdown-документом.

`main_story_saref_state.json.guardianQuestlines[]` хранит progress по
`guardianId`, `questId` и `questOrdinal`, но сейчас не связывает эти значения с
закрытым каталогом десяти Хранителей и сорока квестов. Поэтому произвольный
Guardian или выдуманные quest/revelation/advantage identifiers потенциально
могут выглядеть как допустимая сюжетная линия. Примеры также местами используют
короткие IDs вроде `azalia`, тогда как детерминированный runtime ID системного
Хранителя имеет форму `guard_system_azalia_001`.

Главный продуктовый риск — не утечка скрытого сюжета игроку, а отсутствие
сюжетной компетенции у GM до создания конкретного Хранителя. Решение должно
дать GM знание всей линии с первого хода, не создавая всех десятерых как
действующих акторов раньше встречи.

Игра ещё не вышла. Совместимость старых сохранений, receipt-less state и
runtime migration старых тестовых fixtures не являются требованиями.

## Цели

1. Сделать всю линию `Крылья над Бездной` постоянной структурированной
   компетенцией GM во всех мирах до появления любого сюжетного Хранителя.
2. Дать каждому из десяти Предвечных Хранителей полный детерминированный
   материализационный шаблон и точную Actor Materialization authority.
3. Превратить сорок существующих quest blueprints в закрытый, валидируемый и
   versioned каталог с точными IDs, наградами и правилами progression.
4. Хранить один authoritative progress и строить из него согласованные
   Guardian/Actor Brain projections вместо трёх независимых GM-authored копий.
5. Материализовать Сарефа и `Крылья Ангелов` как конкретные canonical story
   entities поверх общих actor/faction contracts.
6. Сохранить Предвечных Хранителей полноценными живыми акторами после
   завершения четырёх сюжетных квестов: GM продолжает создавать для них новые
   явно несюжетные квесты.

## Не-цели

- GM-worker orchestration или реализация Saref content worker (#1239).
- Multiplayer/network play.
- Полная материализация всех десяти Guardian actor instances при New Game.
- Копирование полного каталога в каждое сохранение.
- Runtime migration старых save/fixture schemas.
- Повторная реализация общих Actor Materialization (#1500), Faction
  Materialization (#1510) или location/Guardian-plane materialization (#1514).
- Замена обычных несюжетных Guardian quests фиксированным каталогом.

## Состав сюжетного каталога

Каталог содержит ровно десять Предвечных Хранителей, по четыре сюжетных квеста
на каждого:

| Preset | Canonical Guardian ID |
|---|---|
| `azalia` | `guard_system_azalia_001` |
| `brann` | `guard_system_brann_001` |
| `elyara` | `guard_system_elyara_001` |
| `ilarion` | `guard_system_ilarion_001` |
| `lissara` | `guard_system_lissara_001` |
| `lucian` | `guard_system_lucian_001` |
| `myriel` | `guard_system_myriel_001` |
| `seret` | `guard_system_seret_001` |
| `varak` | `guard_system_varak_001` |
| `veyra` | `guard_system_veyra_001` |

Для каждого preset каталог резервирует exact quest IDs
`<presetId>_saref_q1` … `<presetId>_saref_q4`. Классификация никогда не
выводится только из строки ID: exact membership в загруженном каталоге и
`storyScope` являются authority.

## Двухуровневая архитектура

### Уровень 1: всегда материализованное знание

В поставке игры существует immutable structured catalog. Предлагаемые корни:

- `BookOfEternityClient/story_content/saref/catalog.json` — общий индекс,
  версии, reveal rules, route categories, exact Guardian/quest/template links,
  Saref и Wings template references;
- `BookOfEternityClient/system_guardians/built_in/<preset>/guardian_materialization.json`
  — структурированный template полного Guardian/common-profile seed;
- `BookOfEternityClient/system_guardians/built_in/<preset>/saref_questline.json`
  — четыре полных структурированных story quest templates;
- существующий `dossier.md` — расширенный GM roleplay source, проверяемый на
  согласованность с materialization template;
- существующий `OtherGuides/Saref_Guardian_Questlines/<preset>.md` —
  человекочитаемое/GM-facing представление structured questline, проверяемое
  source guards или генерируемое из него.

`catalog.json` содержит `storyId`, `schemaVersion`, `catalogVersion`, digest,
компактный synopsis, точные ссылки на десять presets, сорок квестов, quest-4
revelations/advantages, Saref actor template и Wings faction template. Catalog
load является all-or-nothing.

### Уровень 2: лениво материализуемые игровые сущности

Guardian, Saref, Wings faction и quest instances появляются в mutable
canonical state только при реальном игровом основании: New Game selection,
System Guardian attraction, story appearance, recognized trace, explicit quest
acceptance или соответствующий pending/story transition.

Отсутствующий actor instance не означает отсутствие знания о нём у GM.
Отсутствие каталога, напротив, является ошибкой поставки и запрещает story
mutation.

## Постоянная осведомлённость GM

Каждый GM prompt во Mortal World, Chaos Sea и Shining Abode получает компактный
story index независимо от `revealStage`, наличия `main_story_saref_state.json`
в старых fixtures или существования Guardian actors. Индекс содержит:

- смысл скрытой линии и no-spoiler/player-projection boundary;
- список десяти Хранителей и их canonical IDs;
- названия и краткое назначение сорока квестов;
- quest-4 revelation category и advantage каждого Хранителя;
- safe/risky/desperate route rules;
- точные инструкции, когда загрузить полный Guardian/quest package.

Полное досье и полная questline автоматически прикладываются, если relevant
actor/state/request/action/trace ссылается на соответствующий preset или quest.
Это ограничивает prompt size, но не скрывает существование сюжета от GM.

Компактный index достаточен для выбора и записи latent trace. Активация,
progress или завершение конкретного story quest допустимы только в ходе, где
harness приложил полный exact quest package. Если full package не был включён,
GM может создать только exact catalog-backed latent trace; следующий prompt
получает нужный package.

## New Game story root

Current-schema New Game всегда создаёт минимальный
`game_state/meta/main_story_saref_state.json`. Root содержит существующие
canonical empty surfaces и новый immutable binding:

```json
{
  "schemaVersion": 2,
  "catalogBinding": {
    "storyId": "wings_over_the_abyss",
    "catalogVersion": 1,
    "catalogDigest": "<client-computed exact digest>"
  },
  "revealStage": "unknown",
  "guardianQuestlines": [],
  "latentTraces": []
}
```

Полный root сохраняет все уже существующие canonical arrays/objects; fragment
показывает только новую binding authority. Сорок mutable quest states не
создаются заранее: их definitions уже существуют в каталоге, а progress
появляется только после игрового события.

Missing current-schema root или catalog mismatch не получает legacy fallback.
New Game/bootstrap обновляется вместе с fixtures.

## Полная материализация Хранителя

Одна generation-bound atomic operation создаёт или отклоняет весь required
bundle:

1. exact canonical Guardian в `guardians.json` из
   `guardian_materialization.json`;
2. matching `actorType=guardian` common afterlife profile с Actor
   Materialization envelope;
3. initial actor-owned memory в exact Guardian thought-journal authority;
4. exact `sourcePreset`, materialization template binding и Saref story
   catalog binding;
5. проекцию уже recognized/active quest, если latent progress существовал до
   встречи с Хранителем;
6. exact reference на abode/location template без создания параллельной
   location schema вместо #1514.

Materialization template структурированно задаёт identity, manifestation,
appearance, personality, worldview, motivation, goals/plan, authored arts,
relationship posture, capabilities and intentional empty dispositions. Runtime
может добавлять только документированные dynamic values: turn/time, initial
relationship to the current soul и соответствующие receipts. Names, prose,
archetype keywords или genre terms не создают mechanics.

Операция либо публикует весь валидный bundle, либо оставляет все canonical
roots без изменений. Существующий materialized actor нельзя повторно послать
как full profile; дальнейшие изменения используют dedicated deltas.

## Один authoritative lifecycle сюжетного квеста

Immutable quest definition живёт в catalog. Mutable authoritative progress
живёт только в `main_story_saref_state.json.guardianQuestlines[]`.

Поддерживаемый lifecycle:

```text
undiscovered (нет progress record)
  -> latent
  -> recognized
  -> active
  -> ready_to_turn_in
  -> completed
```

`latent` означает реально найденный, но ещё не понятый след; он не используется
как стартовое значение для всех сорока квестов. Official completion остаётся
строго `q1 -> q2 -> q3 -> q4`. Явное принятие игроком требуется перед `active`.

Для story quests вводится один narrow typed transition:
`sarefMainStoryUpdate.mode=advance_guardian_quest`. Он несёт exact Guardian ID,
exact catalog quest ID, expected from/to state, turn, evidence и player-action
authority where required. Этот transition применим и к Mortal progress, и к
afterlife hand-in. Обычный `guardianQuestProgressUpdates` остаётся authority
для несюжетных Guardian quests и не может самостоятельно менять Saref state.

Client normalizer/projector атомарно строит две derived views:

- `guardians[].questManagement` для доступного/активного/завершённого игрового
  квеста;
- matching common profile `personalQuests` для Actor Brain agency.

GM не обязан независимо переписывать три copies. Derived views содержат exact
`questId`, `storyScope`, catalog binding и status, а player-facing текст
разрешается из immutable template.

Quest 4 завершается через существующий playable layer `Воспоминание` и
`record_memory_scene`. Успешная closure одновременно завершает exact q4 и
выдаёт только зарегистрированные для этого Guardian `sarefRevelation` и
`sarefAdvantage`. Physical item transfer остаётся запрещён.

## Сюжетные и возобновляемые несюжетные квесты

Каждый Guardian quest имеет обязательный closed enum:

Allowed values: `saref_main_story` and `non_story`. A story quest therefore
carries, for example:

```json
"storyScope": "saref_main_story"
```

Сорок catalog quests всегда имеют `storyScope=saref_main_story` и
`questOrigin=saref_main_story_catalog`. Только они могут появляться в
`guardianQuestlines[]`, создавать Saref revelations/advantages, влиять на
`revealStage`, route readiness и deep-victory Guardian count.

GM-authored quests имеют `storyScope=non_story` и один существующий либо новый
явно документированный ordinary `questOrigin`, например lore research,
archive consultation, baseline mortal-life hook, politics/project response или
post-story personal request. Они используют обычные Guardian quest contracts,
power cap, difficulty ceiling, acceptance, Mortal progress и afterlife hand-in.

Завершение q4 не исчерпывает Хранителя. После четырёх сюжетных квестов он
бессрочно остаётся eligible для новых GM-authored `non_story` quests,
основанных на текущих целях, отношениях, памяти, проектах, политике, Обители и
последствиях кампании. Это гарантия continued agency, а не обязанность держать
всегда заполненный quest list. До q4 обычные non-story quests также остаются
разрешены существующими правилами и не обязаны ждать завершения линии.

Каждый новый non-story quest обязан иметь stable unique ID, title,
description, objective/success authority, difficulty, reward outline,
`questOrigin`, `storyScope=non_story` и обоснование из текущего actor/world
state. Он не может:

- использовать exact catalog quest ID;
- входить в `guardianQuestlines[]`;
- создавать/изменять Saref revelation, advantage или reveal stage;
- засчитываться как q1..q4 или deep-victory proof;
- маскироваться под story quest через prose или ID pattern.

Validator классифицирует story quest по exact catalog membership plus
`storyScope`, а не по title/prose. Несовпадение scope и membership отклоняется.

## Сареф и Крылья Ангелов

Сареф получает stable exact identity `actorType=saref` и
`actorId=saref_001`, complete current-schema afterlife profile, Actor
Materialization envelope, actor-owned memory, goals, agency, arts,
relationships, public masks и private truth. Новый type намеренно отличает
самого Сарефа от существующего `actorType=saref_agent`, который остаётся только
для его агентов и сторонников. Identity не выводится из prose.

`Крылья Ангелов` получают exact Shining faction ID
`shine_faction_wings_of_angels_001`, complete Faction Materialization envelope,
`story` route, `saref_main_story` authority,
`factionRole=wings_of_angels`, full charter/lifecycle/hall/leadership/memory/
chronicle/capabilities and exact binding to
`main_story_saref_state.json.factionLinks.wingsFactionId`.

GM знает обе сущности с первого хода. Player projections до соответствующего
reveal stage остаются filtered. После reveal faction становится actionable, а
private materialization/story truth не публикуются.

## Validation и repair

Catalog validation выполняется при build/tests и при runtime load. Проверяется:

- ровно 10 exact presets и 40 exact story quests;
- уникальность template, Guardian, quest, revelation и advantage IDs;
- четыре ordinal quests на Guardian;
- exact agreement между manifest, materialization template, questline,
  compact index и GM-facing docs;
- quest-4 reward/link completeness;
- exact case-sensitive cross-file identity;
- catalog digest and schema support.

Runtime validation отклоняет unknown/case-variant Guardian/quest IDs,
scope/membership mismatch, out-of-order transition, missing player acceptance,
wrong source world, incomplete evidence, forged q4 reward, missing actor
materialization, partial projection и unrelated full-object rewrite.

Catalog corruption является installation/content-integrity error и не
загружается частично. Catalog-binding mismatch блокирует story mutation. Он не
разрешает GM переписать catalog state наугад и не конвертирует ordinary turn в
неограниченный repair.

Repair packet называет только exact affected roots/template references.
Proposal cannot rewrite unrelated Guardian, profile, journal, story state,
Wings faction or catalog content. Любая ошибка atomic projection вызывает
rollback всех write-set roots.

## Player visibility

Постоянная осведомлённость GM не меняет player visibility. Console и browser
используют один reveal predicate. До reveal игрок не видит private catalog,
Saref identity, Wings faction, hidden Guardian/Saref bindings,
materialization envelopes или GM compact index. После reveal показывается
только intended actionable story/faction/actor data; private truth и envelopes
остаются internal.

`storyScope` допускается в debug/diagnostic projection. В обычном player UI
оно отображается русским маркером `Сюжетный` или `Несюжетный`, если различение
нужно в списке квестов; raw enum не показывается.

## GitHub decomposition

Issue #1519 становится epic `Крылья над Бездной: authoritative story
materialization` с тремя child issues.

### Child A: catalog и постоянная GM-компетенция

- schema/index/digest;
- structured conversion всех десяти questlines;
- compact all-realm prompt index;
- relevance-driven full package routing;
- manifest/dossier/Markdown/source-guard consistency;
- minimal New Game catalog binding.

### Child B: десять Guardian materializations и quest lifecycle

- ten `guardian_materialization.json` templates;
- atomic Guardian/profile/memory creation;
- exact story trace and typed transition authority;
- derived `questManagement`/`personalQuests` projections;
- all forty story quest paths;
- explicit `storyScope` and renewable GM-authored `non_story` quests;
- integration with #1500 and exact abode/location references owned by #1514.

### Child C: Сареф и Крылья Ангелов

- Saref exact actor type/ID and full actor materialization;
- Wings exact faction ID and full story-route faction materialization;
- cross-links, hidden/revealed lifecycle and player filtering;
- exact repair boundaries and final/deal/post-story integration;
- dependency on #1510.

The epic is complete only after all three children and relevant #1514 links are
valid. It is a prerequisite for Saref GM worker #1239. No worker implementation
is included.

## Test strategy

### Catalog and prompt tests

- A no-Guardian/no-progress prompt in each realm contains the compact story
  index, all ten Guardians and all forty quest references.
- Relevant Guardian/trace/request loads exactly the matching full package.
- Missing/duplicate/case-variant/catalog-drift content fails closed.
- Player output never contains raw prompt/catalog/private story data.

### Materialization tests

- Data-driven materialization of every Guardian validates canonical Guardian,
  common profile, Actor Materialization envelope, journal memory and catalog
  binding.
- A trace recorded before actor creation survives and projects correctly after
  exact Guardian materialization.
- Partial write, wrong actor, wrong case or one invalid projection rolls back
  the entire bundle.

### Quest tests

- All 40 templates are unique, complete and linked to exact Guardian IDs.
- q1..q4 ordering, explicit acceptance, Mortal progress, afterlife hand-in and
  q4 `Воспоминание` closure pass.
- Only exact q4 rewards are issued.
- A post-q4 GM-authored `storyScope=non_story` quest can be offered, accepted,
  progressed and completed through the ordinary lifecycle.
- A non-story quest cannot mutate Saref state or count toward reveal/deep
  victory; a catalog quest cannot be mislabeled `non_story`.
- Existing ordinary Guardian quests retain their current behavior.

### Documentation and bounded verification

Update Mortal/afterlife GM prompts, `TaskGuides/CLI_Step_Main.txt`,
`OtherGuides/Afterlife_Contract_Matrix.md`,
`Examples/E_CLI_Afterlife_Turns.txt`, validation manifest, Saref character/
questline docs, daemon entrypoints and documentation/source guards. Include
worked examples for early latent trace, Guardian materialization, q1 activation,
q4 memory closure, Wings/Saref materialization and a post-q4 non-story quest.

Use the repository test runner only:

1. smallest relevant `Focused` filters during each child;
2. one `Fast` checkpoint after meaningful integration;
3. `FullValidation` when the afterlife docs/examples boundary changes;
4. one final `PreMerge`, without a redundant Fast immediately before it.

## Acceptance criteria

- GM receives authoritative compact knowledge of the complete hidden story in
  every realm before any story actor exists.
- Exactly ten Guardian templates and forty story quest templates validate.
- Every materialized Guardian has complete exact Guardian/profile/memory/
  envelope authority.
- Story state accepts only catalog-backed exact identities and progression.
- One typed transition atomically maintains story progress and its two derived
  runtime projections.
- Saref and Wings are complete exact story entities with valid actor/faction
  materialization and hidden/revealed behavior.
- Completing q4 never turns a Guardian into an empty puppet: new GM-authored
  `non_story` quests remain supported indefinitely and cannot advance Saref.
- No legacy save compatibility, receipt-less state or migration path remains.
- GM docs/examples/manifests/source guards and bounded verification are green.

## Spec self-review

- Placeholder scan: no unresolved `TBD`/`TODO` remains. The digest value shown
  in JSON is illustrative client-computed runtime data, not an unspecified
  design choice.
- Internal consistency: immutable catalog owns definitions; Saref state owns
  progress; Guardian/common-profile quest arrays are derived projections.
- Scope: the epic is decomposed into three independently reviewable children;
  GM workers and generic location/actor/faction implementations stay outside.
- Ambiguity: story/non-story classification, post-q4 renewable quests, exact
  identity, prompt availability, failure behavior and compatibility policy are
  explicit.
