#!/usr/bin/env python3
"""Planning-only #532 paired duel probe; stdlib, no game imports or writes.

D50 is rank, not a die. Reuses the adjacent convoy-duel.py accepted #1573
quality/damage/hit kernel, including Favor22, innate resistance, half-up HP
rounding after unrounded absorption and no counter after an absorbed hit.

Decision point: V has taken opening Main, paid 16 for shield 71.2, ended
at 108/110 DE (regen14); A now begins his actual turn at 100 or 68/100
(regen12). Both reactions are initially available. No transition refill.
A HP332, Might/Sense26, Integrity22; V HP368, Might/Integrity28, Sense26.
No additional sound resistance. A named sound attacks have +40% damage;
improvised B14 has none. V ordinary/counter B28 and fallback B14 slash;
thunder B36 crushes. A's brigandine adds 15pp against slash only.

First choice: normal B28 (16), strong B36 (32), ally Bell117.6 (16), or
normal plus plain intercept (8, within reach). Intercept reserves A's only
reaction against any intervening V counter, uses A's defense (+2 personal
Sense from Crown) and Integrity22, and grants neither +5 guard nor counter.
After A's first turn, before V, a separate enemy attacks the ally once:
direct crushing B28, Might26 vs Sense26/Integrity22. Ally reaction is spent.
She helps an evacuee if HP>0, with no additional effect on this duel.

Identical subsequent policies (decisions precede dice): A uses B28 or free
B14. Both guard B36 and B28 at <=1/3 initial HP (8); otherwise counter a
full miss if reaction+24 remain. Counterattacks allow guard but no further
counter. V uses due thunder (16), else renews an exhausted/expired shield
under 'renew', else B28 (16)/B14 (0). 'opening-only' forbids later shields.
No self-Bell or additional aid is assumed. These are defensible sample
policies, not an optimizer or a claim about narrative battle outcomes.

A shield's first hostile absorption, including a counter, offers one thunder
on V's next Main only, even if broken. Taking another Main or being unable
to pay loses it. Spending it leaves that shield unable to charge again.
Expiry follows recipient turns: end of third following personal turn.

All cases share fixed independent dice lanes per duel/round/role: threat,
A Main, V Main, A counter, V counter. Unused lanes never shift other draws.
Reported median covers resolved duels; mean ally loss clamps overkill to
initial HP. HP0 is a probe stopping rule, not an accepted surrender script.
"""

import argparse
from dataclasses import dataclass
import math
from pathlib import Path
import random
import runpy
import statistics


_kernel = runpy.run_path(str(Path(__file__).with_name('convoy-duel.py')))
quality = _kernel['quality']
damage_table = _kernel['damage_table']
hit = _kernel['hit']

FIRST_CHOICES = ('normal', 'strong', 'bell', 'intercept')
LANES_PER_ROUND = 8


@dataclass
class Actor:
    """Resources and recipient-turn shield/thunder clocks, kept unrounded."""

    hp: int
    energy: int
    capacity: int
    regen: int
    reaction: bool = True
    turn: int = 0
    main_count: int = 0
    barrier: float = 0.0
    barrier_expiry: int = 0
    thunder_shield: bool = False
    shield_spent: bool = False
    pending_due: int | None = None

    def begin(self):
        """Start a real personal turn; refresh reaction but not energy."""
        self.turn += 1
        self.reaction = True

    def take_main(self):
        """Consume this Main's thunder opportunity, whether used or declined."""
        self.main_count += 1
        due = self.pending_due == self.main_count
        if self.pending_due is not None and self.pending_due <= self.main_count:
            self.pending_due = None
        return due

    def end(self):
        """Regenerate personally, then expire shields on recipient's clock."""
        self.energy = min(self.capacity, self.energy + self.regen)
        if self.barrier_expiry and self.turn >= self.barrier_expiry:
            self.barrier = 0.0
            self.barrier_expiry = 0

    def react(self, cost):
        """Pay for the single shared reaction, without partial expenditure."""
        if not self.reaction or self.energy < cost or self.hp <= 0:
            return False
        self.reaction = False
        self.energy -= cost
        return True


def cast_ward(caster, recipient, reservoir, thunder=False):
    """Pay once; a newly created shield alone rearms its own single charge."""
    if caster.energy < 16:
        raise ValueError('A shield requires 16 DE before casting')
    caster.energy -= 16
    recipient.barrier = reservoir
    recipient.barrier_expiry = recipient.turn + 3
    recipient.thunder_shield = thunder
    recipient.shield_spent = False


def strike(target, table, base, guarded, dice):
    """Apply shared hit kernel, then trigger thunder on actual absorption."""
    previous = target.barrier
    outcome = hit(target, table, base, guarded, dice, 0)
    if (previous > target.barrier and target.thunder_shield
            and not target.shield_spent):
        target.shield_spent = True
        target.pending_due = target.main_count + 1
    return outcome


def thunder_attack(actor, due):
    """Pay for an available due thunder; it is a separately rolled strike."""
    if due and actor.energy >= 16:
        actor.energy -= 16
        return 36, 16
    return None


def make_tables():
    """Build immutable fixture tables with personal stats and damage types."""
    return {
        'a': damage_table(26, 28, 0, 0, damage_bonus_by_base={28: 40, 36: 40}),
        'v_slash': damage_table(28, 22, 15, 2),
        'v_crush': damage_table(28, 22, 0, 2),
        'threat': damage_table(26, 22, 0, 0),
        'intercept': damage_table(26, 22, 0, -2),
    }


def ordinary_attack(actor):
    """Select affordable ordinary B28 or free improvised B14."""
    if actor.energy >= 16:
        actor.energy -= 16
        return 28
    return 14


def guard(actor, base, initial_hp, reserve=False):
    """Commit defense before dice, using the same pool as counter/intercept."""
    wants_guard = base == 36 or base == 28 and actor.hp <= initial_hp / 3
    return not reserve and wants_guard and actor.react(8)


def counter_available(actor, outcome):
    """Only a complete miss opens this window; a shielded hit does not."""
    return outcome == 0 and actor.hp > 0 and actor.reaction and actor.energy >= 24


def attack_exchange(attacker, target, table, counter_table, base, dice,
                    counter_dice, attacker_max, target_max, reserve=False):
    """Resolve one Main and at most one counter; the latter can be guarded."""
    guarded = guard(target, base, target_max)
    outcome = strike(target, table, base, guarded, dice)
    if (attacker.hp > 0 and counter_available(target, outcome)
            and target.react(24)):
        guarded = guard(attacker, 28, attacker_max, reserve)
        strike(attacker, counter_table, 28, guarded, counter_dice)


@dataclass
class Result:
    """Final personal result plus the independent, always-resolved ally threat."""

    winner: str
    rounds: int
    ally_hp: int
    threat_resolved: bool


def duel(dice, first_choice, start_energy, ally_hp, shield_policy, max_rounds,
         tables, v_hp=368):
    """Play both actors until HP0 or explicit cap, with one ally threat."""
    a = Actor(332, start_energy, 100, 12)
    v = Actor(v_hp, 110, 110, 14)
    ally = Actor(ally_hp, 0, 0, 0, reaction=False)
    v.begin()
    v.take_main()
    cast_ward(v, v, 71.2, thunder=True)
    v.end()
    threat_resolved = False

    for round_index in range(max_rounds):
        lane = 2 + round_index * LANES_PER_ROUND
        a.begin()
        a.take_main()
        reserve = round_index == 0 and first_choice == 'intercept'
        if round_index == 0 and first_choice == 'bell':
            cast_ward(a, ally, 117.6)
        else:
            if round_index == 0 and first_choice == 'strong':
                if a.energy < 32:
                    raise ValueError('A strong first attack requires 32 DE')
                a.energy -= 32
                base = 36
            else:
                base = ordinary_attack(a)
            attack_exchange(a, v, tables['a'], tables['v_slash'], base,
                            dice[lane:lane + 2], dice[lane + 6:lane + 8],
                            332, v_hp, reserve)
        a.end()

        # This distinct enemy action is mandatory even if the preceding Main
        # or counter ended the duel. Plain intercept never opens another counter.
        if round_index == 0:
            intercepted = reserve and a.react(8)
            target = a if intercepted else ally
            table = tables['intercept'] if intercepted else tables['threat']
            strike(target, table, 28, False, dice[:2])
            threat_resolved = True

        if a.hp <= 0 or v.hp <= 0:
            winner = 'both' if a.hp <= 0 and v.hp <= 0 else 'A' if v.hp <= 0 else 'V'
            return Result(winner, round_index + 1, ally.hp, threat_resolved)

        v.begin()
        due = v.take_main()
        thunder = thunder_attack(v, due)
        if thunder:
            base = 36
            table = tables['v_crush']
        elif shield_policy == 'renew' and v.barrier <= 0 and v.energy >= 16:
            cast_ward(v, v, 71.2, thunder=True)
            v.end()
            continue
        else:
            base = ordinary_attack(v)
            table = tables['v_slash']
        attack_exchange(v, a, table, tables['a'], base,
                        dice[lane + 2:lane + 4], dice[lane + 4:lane + 6],
                        v_hp, 332)
        v.end()
        if a.hp <= 0 or v.hp <= 0:
            winner = 'both' if a.hp <= 0 and v.hp <= 0 else 'A' if v.hp <= 0 else 'V'
            return Result(winner, round_index + 1, ally.hp, threat_resolved)
    return Result('timeout', max_rounds, ally.hp, threat_resolved)


def self_test():
    """Exercise mechanics independently of Monte Carlo frequencies."""
    def check(condition):
        """Keep every check and its expression active under Python -O."""
        if not condition:
            raise AssertionError('self-test check failed')

    tables = make_tables()
    full = bytes((15, 10))
    partial = bytes((10, 10))
    miss = bytes((1, 10))
    v = Actor(368, 110, 110, 14)
    v.begin()
    check(not v.take_main())
    cast_ward(v, v, 71.2, thunder=True)
    v.end()
    check(v.energy == 108 and v.turn == 1 and v.reaction)
    strike(v, tables['a'], 28, False, full)
    check(v.hp == 321 and v.barrier == 0 and v.pending_due == 2)
    check(v.energy == 108)  # charging does not pay for thunder
    v.begin()
    check(v.take_main() and v.pending_due is None)
    check(thunder_attack(v, True) == (36, 16))
    check(v.energy == 92)
    check(thunder_attack(v, False) is None)
    target = Actor(332, 100, 100, 12)
    check(strike(target, tables['v_crush'], 36, False, miss) == 0)
    check(target.hp == 332)  # thunder does not reuse the charging hit's roll

    v = Actor(368, 110, 110, 14)
    cast_ward(v, v, 1000, thunder=True)
    strike(v, tables['a'], 28, False, partial)
    check(v.pending_due == 1 and v.shield_spent)
    due = v.take_main()
    check(due and v.pending_due is None)
    strike(v, tables['a'], 28, False, partial)
    check(v.pending_due is None)  # same shield cannot rearm
    check(not v.take_main())  # declining the due Main loses it
    v.energy = 15
    check(thunder_attack(v, True) is None and v.energy == 15)

    v = Actor(368, 110, 110, 14)
    cast_ward(v, v, 71.2, thunder=True)
    strike(v, tables['a'], 28, False, miss)
    check(v.pending_due is None and not v.shield_spent)
    for _ in range(3):
        v.begin()
        v.end()
    check(v.barrier == 0)
    strike(v, tables['a'], 28, False, partial)
    check(v.pending_due is None)

    a = Actor(332, 100, 100, 12)
    cast_ward(a, a, 98)
    check(strike(a, tables['v_slash'], 28, False, full) > 0)
    check(a.hp == 332 and not counter_available(a, 2))
    check(a.react(8) and not a.react(24))
    a.begin()
    check(a.reaction and a.energy == 76)  # no start regeneration
    check(counter_available(a, 0))
    check(not counter_available(a, 1))

    a = Actor(332, 100, 100, 12)
    v = Actor(368, 110, 110, 14)
    cast_ward(v, v, 1000, thunder=True)
    v.take_main()
    attack_exchange(v, a, tables['v_slash'], tables['a'], 28,
                    miss, partial, 368, 332)
    check(v.pending_due == 2 and v.shield_spent)  # counter charges next Main
    check(not a.reaction and v.reaction)
    a = Actor(332, 100, 100, 12)
    v = Actor(368, 110, 110, 14)
    attack_exchange(a, v, tables['a'], tables['v_slash'], 28,
                    miss, miss, 332, 368)
    check(a.reaction and a.energy == 100)  # no counter-chain after V misses
    check(not v.reaction and v.energy == 86)  # exactly 24 DE and one reaction

    ally = Actor(75, 0, 0, 0, reaction=False)
    a = Actor(332, 100, 100, 12)
    cast_ward(a, ally, 117.6)
    check(ally.barrier_expiry == 3 and a.energy == 84)
    a.begin()
    a.end()
    check(ally.barrier == 117.6)  # recipient's clock, not creator's
    strike(ally, tables['threat'], 28, False, bytes((20, 1)))
    check(ally.hp == 55)  # worst hit: 137.7 minus 117.6, rounded once
    values = [math.floor(damage + .5) for _, damage in tables['threat'][28, False]]
    check({n: values.count(n) for n in set(values)} == {0: 191, 43: 84, 86: 60, 138: 65})
    check(sum(value < 75 for value in values) == 275)
    check(sum(value < 120 for value in values) == 335)
    check(tables['intercept'][28, False] != tables['threat'][28, False])
    # The threat is a distinct lane and must resolve even after early duel death.
    dice = bytes((20, 1)) * (1 + 8 * 2)
    result = duel(dice, 'bell', 100, 75, 'opening-only', 2, tables, v_hp=1)
    check(result.ally_hp == 55)
    check(result.threat_resolved)
    result = duel(dice, 'normal', 100, 75, 'opening-only', 2, tables, v_hp=1)
    check(result.winner == 'A' and result.rounds == 1)
    check(result.ally_hp == -63 and result.threat_resolved)


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--duels', type=int, default=12000)
    parser.add_argument('--seed', type=int, default=5321573)
    parser.add_argument('--max-rounds', type=int, default=60)
    parser.add_argument('--a-energy', type=int, nargs='+', default=[100, 68],
                        help='decision-point DE, 32..100 so all first choices are legal')
    parser.add_argument('--ally-hp', type=int, nargs='+', default=[75, 120])
    parser.add_argument('--shield-policy', nargs='+', choices=['renew', 'opening-only'],
                        default=['renew', 'opening-only'])
    parser.add_argument('--opening-only', action='store_true',
                        help='shortcut for --shield-policy opening-only')
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args()
    self_test()
    if args.self_test:
        print('self-test: passed')
        return
    if args.duels < 1 or args.max_rounds < 1:
        parser.error('--duels and --max-rounds must be positive')
    if any(value < 32 or value > 100 for value in args.a_energy):
        parser.error('--a-energy must be 32..100 for legal first-choice comparison')
    if any(value < 1 for value in args.ally_hp):
        parser.error('--ally-hp must be positive')
    energies = list(dict.fromkeys(args.a_energy))
    healths = list(dict.fromkeys(args.ally_hp))
    shields = ['opening-only'] if args.opening_only else list(dict.fromkeys(args.shield_policy))
    cases = [(shield, energy, hp, first) for shield in shields for energy in energies
             for hp in healths for first in FIRST_CHOICES]
    results = {case: {'A': 0, 'V': 0, 'both': 0, 'timeout': 0, 'ally': 0,
                      'joint': 0, 'loss': 0, 'rounds': [], 'paired_gain': 0,
                      'paired_loss': 0} for case in cases}
    tables = make_tables()
    rng = random.Random(args.seed)
    for _ in range(args.duels):
        # Fixed lane generation is independent of choices, HP, DE and policies.
        dice = bytes(rng.randrange(1, 21) for _ in range(2 + args.max_rounds * LANES_PER_ROUND))
        sampled = {}
        for case in cases:
            shield, energy, hp, first = case
            outcome = duel(dice, first, energy, hp, shield, args.max_rounds, tables)
            if not outcome.threat_resolved:
                raise RuntimeError('The independent ally threat was not resolved')
            sampled[case] = outcome.winner == 'A'
            result = results[case]
            result[outcome.winner] += 1
            result['ally'] += outcome.ally_hp > 0
            result['joint'] += outcome.winner == 'A' and outcome.ally_hp > 0
            result['loss'] += hp - max(0, outcome.ally_hp)
            if outcome.winner != 'timeout':
                result['rounds'].append(outcome.rounds)
        for case in cases:
            baseline = (*case[:3], 'normal')
            results[case]['paired_gain'] += sampled[case] and not sampled[baseline]
            results[case]['paired_loss'] += sampled[baseline] and not sampled[case]

    print(f'seed={args.seed} duels/case={args.duels} round-cap={args.max_rounds}')
    print('A: HP332 DE cap100/regen12; V: HP368 DE cap110/regen14; '
          'opening shield71.2 already paid, V DE108; initial reactions available.')
    print('Rounds count A/V turn pairs from decision point (opening V turn excluded).')
    print('Median: resolved duels only. Ally loss: mean HP lost, capped at initial HP.')
    print('A uses B28/B14; both guard B36 or B28 at <=1/3 HP, else counter full misses.')
    print('V: due thunder first, then renew depleted/expired shield if enabled, else B28/B14.')
    print('shield       A_DE allyHP first       A_win% V_win% ally>0%  joint% '
          'mean_loss median timeout both paired_A+ paired_A-')
    for case in cases:
        shield, energy, hp, first = case
        result = results[case]
        median = statistics.median(result['rounds']) if result['rounds'] else float('nan')
        percent = 100 / args.duels
        print(f'{shield:12} {energy:4} {hp:6} {first:10} '
              f'{percent * result["A"]:6.2f} {percent * result["V"]:6.2f} '
              f'{percent * result["ally"]:7.2f} {percent * result["joint"]:7.2f} '
              f'{result["loss"] / args.duels:9.2f} {median:6g} '
              f'{result["timeout"]:7} {result["both"]:4} '
              f'{result["paired_gain"]:9} {result["paired_loss"]:9}')
    print('paired_A+/A-: discordant A wins against normal first action '
          'at the same shield/DE/allyHP; draws are paired, not independent samples.')


if __name__ == '__main__':
    main()
