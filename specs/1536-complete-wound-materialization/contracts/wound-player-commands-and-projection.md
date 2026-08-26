# Contract: Player Wound Commands, Targeting, and Projection

**Feature**: `1536-complete-wound-materialization`  
**Issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)

## Commands

| Command | Scope | Meaning |
| --- | --- | --- |
| `/раны`, `/wounds` | all realms | Open the player's current-realm active wounds |
| `/лечить`, `/treat` | all realms | Guided diagnosis/treatment flow |
| `/исцелить`, `/heal` | afterlife aliases | Same guided handler as `/лечить` |

Commands do not accept a raw name or opaque ID parameter. They use interactive choices
and client-owned hidden bindings. Console and browser call the same C# application
service and projection model.

## `/раны` flow

### Primary view

The primary view contains only active wounds owned by the player in the current realm.
Each row/card includes authorized:

- readable wound name and Roman severity;
- known location/cause;
- short visible symptoms and consequence summaries;
- care/recovery state in player language;
- available detail, diagnosis, treatment, and help actions.

Healed wounds never appear in this list. An empty state clearly says there are no
active wounds and offers History separately.

### Detail view

The detail view may show:

- known origin, location, nature, symptoms, prognosis, and complications;
- visible linked-effect summaries;
- stabilization, treatment, and recovery progress;
- known route requirements/risks;
- reachable self/provider/help options and a sealed quote after provider selection.

It must not expose canonical file paths, IDs, schema/property names, source seals,
fingerprints, hidden symptoms/routes, private NPC facts, GM instructions, validator
codes, or agent terminology.

### History

History is a distinct action/tab/panel. It contains readable healed/terminal wound
records and cosmetic or independent legacy summaries. It is not mixed into the active
list. Technical transition rows may be summarized for readability without changing
canonical history authority.

## `/лечить` guided flow

The semantic steps are identical in console/browser:

1. **Target**: `Себя` first, followed by visible reachable nearby entities.
2. **Wound or diagnosis**: choose a known active wound or permitted diagnosis action.
3. **Method/helper**: choose a known self route, reachable helper, or available public/
   authorized service.
4. **Requirements and risk**: show known requirements, expected world-time/action cost,
   consumption policy, provider access, and exact quoted compensation.
5. **Confirmation**: create one sealed client command.
6. **Resolution**: revalidate fresh canonical authority and resolve through the common
   handler.
7. **Result**: show readable outcome, consumed compensation/resources, wound change,
   world-cycle/action cost, and remaining known options.

Canceling before accepted resolution creates no attempt, charge, roll, cycle, or wound
transition.

## Target selection

Target choices are produced from current interaction/reachability authority. Each
choice contains an opaque session-local binding not rendered to the player.

- Self is always first when it is a legal target.
- Only visible and reachable entities are listed.
- Known wound markers are shown only with discovery authority.
- Duplicate names receive visible context such as role, location, affiliation, or
  appearance.
- Array position and display name never become canonical identity.
- Conscious capable targets must consent outside conflict.
- A selected target that moves, becomes hidden, changes realm, withdraws consent, or
  loses the wound before confirmation is rejected before resource use.

Example display choices:

```text
1. Себя
2. Мирра — лекарь у северного костра
3. Мирра — раненая разведчица у ворот
```

No `npcId` is shown or accepted.

## Projection visibility

Projection intersects wound visibility, target visibility, effect visibility,
diagnosis discoveries, provider access, relationship/quest authority, and current
realm. The most restrictive applicable authority wins.

- A visible effect may link to a visible wound.
- An active wound may summarize only visible owned effects.
- Hidden treatment routes show only a reachable diagnosis need.
- Private NPC wounds are not returned simply because the NPC is nearby.
- An inaccessible visible faction healer remains visible but shows the known access
  condition; hidden conditions remain a general negotiation/relationship need.
- Independent hidden effects are not revealed through wound detail.

## Console contract

- Russian in-world terminology is primary; English aliases are commands only.
- Dynamic GM/world text is escaped before Spectre.Console markup.
- Selection prompts never print technical IDs.
- A stale or invalid action returns one concise player-facing explanation and safe next
  actions, not a validation dump.
- Notifications include the wound name/severity and `/раны` discovery hint.

## Browser contract

- The browser exposes the same choices, requirements, quotes, confirmation, result,
  and blocking reasons as console.
- Canonical JSON is not sent to the frontend; typed sanitized blocks are.
- The UI uses established dark-fantasy components/tokens and remains keyboard-usable,
  responsive, and readable.
- Dynamic text is rendered as text/sanitized content, never injected raw HTML.
- Browser reload/retry cannot submit a consumed binding twice.

## Semantic parity assertions

For one canonical snapshot, console and browser must agree on:

- active wound count/order and History separation;
- visible detail fields and hidden omissions;
- target/helper/route availability;
- exact price, resource requirements, action/world-time cost;
- treatment result and remaining wound state;
- stale target, insufficient tier, missing resource, and access-block explanations.

Visual layout may differ. Semantics may not.

## Application service boundary

The shared service owns:

- canonical read and agreement validation;
- visibility-safe projection;
- target and provider discovery/binding;
- quote construction and sealing;
- action staging into `wound_commands.json`;
- fresh revalidation and accepted-turn submission;
- typed result blocks.

Console/browser layers own only presentation and user interaction. They must not parse
wound JSON, calculate healing, subtract Ink Feathers, advance time, or mutate files.

## Error copy examples

```text
Цель больше не находится рядом. Выберите её снова.
```

```text
Вашего уровня «Духовного исцеления» достаточно для диагностики, но недостаточно,
чтобы ослабить эту рану.
```

```text
Этот способ лечения пока неизвестен. Найдите специалиста или проведите диагностику.
```

These messages are projections of typed errors; internal error codes remain available
only to validation/repair logs.
