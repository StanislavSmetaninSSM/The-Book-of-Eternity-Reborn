# Wound-only author decisions — pending, 2026-11-02

This is a decision request, not an approved amendment. No option below is
implemented or accepted. The parent conversation will relay the questions;
do not submit duplicate questions from task-local UI. It supersedes treating the
old planning word "ambiguity" as proof that every consumer needs a new rule.

## 1. Harmful margin without a contested roll

Current rule: "`harmfulMargin` is the sealed harmful margin before any normalized
critical-success display adjustment" ([contract](contracts/afterlife-spiritual-wounds-and-healing.md#wound-opportunity-formula), line49).
The ordinary checker legally waives dice audit for explicit voluntary non-contest
(`ValidationService.AfterlifeSpiritualConflict.cs:5824`); it supplies no replacement
number. Source witness currently reconstructs margin from validated side totals.

Game difference: artIII/resilienceI and clear→strained contribute4 before margin.
Margin0 gives pressure4/no wound; margin4 gives pressure8/maximumI. Neither the
voluntary flag nor the strain transition determines that choice.

**Минимальный вопрос:** какое значение или вычисление `harmfulMargin` применять
к добровольно принятому духовному вреду без встречного броска?
**Предложение:**0, сохраняя остальные слагаемые и все caps; это только предложение,
ответа пока нет. Не создавать бросок или фиктивный opposed audit.

## 2. One adverse control step

Current rule: "`controlState` resistance for one binding family" and
"one adverse step at II-IV" (`spec.md:3646`). Ordinary control already has
none→hindered→bound→locked; "Same-level narrowing of opposition
`restrictedOperations` counts as weakened `controlState`"
(`OtherGuides/Afterlife_Combat_Terminology_Glossary.md:280`).

Game difference: strengthening incoming hindered→bound is different from reducing
the wounded actor's successful escape bound→hindered back to unchanged bound.
A successful narrowing with no rank change has no unique numeric rank to undo.
The table defines magnitude, not which of those transformations the wound owns.

**Минимальный вопрос:** что именно делает шаг control burden — усиливает входящие
оковы, уменьшает результат собственного выхода или другое; как он влияет на
успешное сужение ограничений без изменения ранга?

## 3. One counter-payoff step

Current rule: "reduce one payoff step/slot at I-IV" (`spec.md:3649`).
The existing `HasCounterPayoff` reader accepts position gain, opposing strain,
control payoff, or a meaningful `counterPayoff` object
(`ValidationService.AfterlifeSpiritualConflict.cs:5030–5076`). It does not define
one common step or a priority among these axes.

Game difference: counter with +1 position and +1 opposing strain can lose one
selected gain or a step on both axes. If its only gain disappears, keeping a
successful/countered outcome contradicts the ordinary measurable-payoff premise
unless the wound rule defines the resulting outcome. Descriptive payoff also needs
a declared meaning of "one step"; a string is not a numerical decrement.

**Минимальный вопрос:** что считать одной ступенью counter payoff и какой выигрыш
уменьшать при нескольких/описательных результатах; каким становится исход, если
рана убрала последний выигрыш?

## 4. Restricted art at severity III

Current rule: "restrict at III; forbid at IV" (`spec.md:3650`); the payload union
is exactly `restrict|forbid` (`contracts/wound-effects-and-atomicity.md:523`).
It specifies no admission predicate for `restrict`. Ordinary `restrictedOperations`
belongs to opposing control and is not a wound rule by itself.

Game difference: requiring preparation, limiting frequency, and allowing only some
outcomes admit different actions. III cannot silently become the full IV prohibition
or a new resource price.

**Минимальный вопрос:** какое точное условие допуска отличает ограниченное
применение искусства приIII от полного запрета приIV?

## Not an additional author question: strain burden

The current rule is already "one extra strain step on one declared failed
non-safety operation", "capped at `broken`" (`spec.md:3647`). For example, an
ordinary strained→fractured result becomes strained→overwhelmed; broken stays
broken. Implementing the actual ordinary no-wound baseline, operation/side-specific
failure predicate and exactly-once projection is technical work. A universal
`!IsSuccessfulArtOutcome` test is wrong: `countered` is a counter payoff outcome,
but failed binding. Do not ask again about +1, broken, protected safety operations,
position/B2–B5 or existing source caps.

No question above authorizes general aggression, defeat, start, repeated-conflict,
QTE or ACTIVE/F1 changes. Withdrawn T088 remains withdrawn. Independent wound
display/privacy work can continue while these four decisions are pending.
