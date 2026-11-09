#!/usr/bin/env python3
"""Planning-only #532 duel probe; Python 3 standard library, no game imports.

Accepted #1573 kernel: opposed d20, natural critical before natural 1,
margin >=10/5/0 => critical/full/partial; damage, innate R, final half-up
HP rounding and recipient-turn barrier clocks. D50 is rank, NOT a d50 die.

Deliberately excluded: environment, escorts, control, movement, initiative
rerolls, consumables, surrender and combat engine lifecycle. Averion acts
first by the agreed +3 initiative fixture, rather than claiming +3 guarantees
that order in every encounter. Both use Might for forceful attack accuracy,
Sense for defense; equal rank bonuses cancel. Averion's equipped B28/B36
sound arts get +15%; improvised B14 has no hilt bonus. Damage type does not
change the selected accuracy stat.

All policies receive identical indexed dice per duel/round/strike, including
unused lanes. Decisions precede hidden dice. No initial barrier in the three
required policies. No pre-combat setup opportunity or initial ward is assumed.
Commander defaults to paid defense against every declared B36 and against
B28 Main/counter when at or below one-third HP. --no-last-stand-guard retains
the relaxed B36-only policy for sensitivity comparisons.
"""

import argparse
from dataclasses import dataclass
import math
import random
import statistics


@dataclass
class Actor:
    """Mutable personal resources; a ward retains its unrounded reservoir."""

    hp: int
    energy: int = 100
    reaction: bool = True
    turn: int = 0
    barrier: float = 0.0
    barrier_expiry: int = 0

    def begin(self):
        self.turn += 1
        self.reaction = True

    def end(self):
        self.energy = min(100, self.energy + 12)
        if self.barrier_expiry and self.turn >= self.barrier_expiry:
            self.barrier = 0.0

    def react(self, cost):
        if not self.reaction or self.energy < cost:
            return False
        self.reaction = False
        self.energy -= cost
        return True

    def ward(self, reservoir):
        assert self.energy >= 16
        self.energy -= 16
        self.barrier = reservoir
        self.barrier_expiry = self.turn + 3


def quality(attack_die, defense_die, delta):
    """Favor22 gives natural20 only; opposed defender die has no own critical."""
    if attack_die == 20:
        return 3
    if attack_die == 1:
        return 0
    margin = attack_die - defense_die + delta
    return 3 if margin >= 10 else 2 if margin >= 5 else 1 if margin >= 0 else 0


def damage_table(power, integrity, resistance_pp, accuracy,
                 heavy_accuracy=0, damage_bonus_by_base=None):
    """Precompute quality and unrounded post-resistance damage for legal arts."""
    innate = integrity / (100 + 3 * integrity)
    resistance = min(.9, max(-1, innate + resistance_pp / 100))
    crit = 1.5 + .5 * 22 / 122
    result = {}
    for base in (14, 28, 36):
        strength = base * (1 + .03 * min(45, power)) * 2
        equipment_percent = (damage_bonus_by_base or {}).get(base, 0)
        strength *= (1 + equipment_percent / 100) * (1 - resistance)
        for guard in (False, True):
            delta = accuracy + (heavy_accuracy if base == 36 else 0)
            delta -= 5 if guard else 0
            values = []
            for attack in range(1, 21):
                for defense in range(1, 21):
                    outcome = quality(attack, defense, delta)
                    factor = (0, .5, 1, crit)[outcome]
                    values.append((outcome, strength * factor))
            result[base, guard] = values
    return result


def hit(target, table, base, guarded, dice, lane):
    """Apply R already in table, then ward, then round HP once, half upward."""
    outcome, incoming = table[base, guarded][(dice[lane] - 1) * 20 + dice[lane + 1] - 1]
    absorbed = min(incoming, target.barrier)
    target.barrier -= absorbed
    target.hp -= math.floor(incoming - absorbed + .5)
    return outcome


def choose_attack(energy, strong):
    if strong and energy >= 32:
        return 36, 32
    if energy >= 16:
        return 28, 16
    return 14, 0


def commander_guard(commander, base, max_hp, last_stand):
    """Commit a legal defense before any dice for the declared strike."""
    wants_guard = (base == 36 or last_stand and base == 28
                   and commander.hp <= max_hp / 3)
    return wants_guard and commander.react(8)


def duel(dice, policy, start_energy, a_table, c_slash, c_crush, commander_hp,
         max_rounds, last_stand_guard=True):
    """Return winner (A/C/timeout), final round, without counter-chains."""
    a = Actor(332, start_energy)
    c = Actor(commander_hp)
    signalled = False
    ward_used = False
    for round_index in range(max_rounds):
        lane = round_index * 6
        a.begin()
        base, price = choose_attack(a.energy, policy == 'strong')
        a.energy -= price
        # Commander may react before his first personal turn, then refreshes
        # at that turn's start; his only sampled response is paid defense.
        guarded = commander_guard(c, base, commander_hp, last_stand_guard)
        hit(c, a_table, base, guarded, dice, lane)
        if c.hp <= 0:
            return 'A', round_index + 1
        a.end()

        c.begin()
        resolving_heavy = False
        if not ward_used and c.hp <= commander_hp / 2 and c.energy >= 16:
            c.ward(66.4 * 1.2)
            ward_used = True
        else:
            base, price = choose_attack(c.energy, signalled)
            resolving_heavy = base == 36
            c.energy -= price
            guarded = (policy == 'tactical' and resolving_heavy and a.react(8))
            table = c_crush if resolving_heavy else c_slash
            outcome = hit(a, table, base, guarded, dice, lane + 2)
            if a.hp <= 0:
                return 'C', round_index + 1
            if outcome == 0 and policy != 'strong' and a.react(24):
                # This new declared strike offers its own defense window,
                # paid before its hidden dice; it never permits a counter.
                guarded = commander_guard(c, 28, commander_hp, last_stand_guard)
                hit(c, a_table, 28, guarded, dice, lane + 4)
                if c.hp <= 0:
                    return 'A', round_index + 1
        # Fast is free but cannot mark on a heavy-resolution turn. A mark
        # from last turn expires even if this turn's Main became a ward.
        signalled = not resolving_heavy
        c.end()
    return 'timeout', max_rounds


def self_test():
    """Contract boundary checks, executable without Monte Carlo sampling."""
    assert quality(20, 20, -100) == 3
    assert quality(1, 1, 100) == 0
    assert [quality(10, 10, delta) for delta in (-1, 0, 4, 5, 9, 10)] == [0, 1, 1, 2, 2, 3]
    a = Actor(332, 8)
    assert a.react(8) and a.energy == 0 and not a.react(0)
    a.begin()
    assert a.reaction and a.energy == 0  # no start-of-turn regeneration
    a.energy = 16
    a.ward(66.4)
    assert a.energy == 0 and a.barrier_expiry == 4
    a.end()
    assert a.energy == 12 and a.barrier == 66.4
    for _ in range(2):
        a.begin()
        a.end()
        assert a.barrier == 66.4
    a.begin()
    assert a.barrier == 66.4
    a.end()
    assert a.barrier == 0
    target = Actor(100, barrier=1.2)
    table = {(28, False): [(2, 3.6)] * 400}
    hit(target, table, 28, False, bytes((1, 1)), 0)
    assert target.hp == 98 and target.barrier == 0  # 2.4 final, not 4-1
    assert choose_attack(31, True) == (28, 16)
    assert choose_attack(15, False) == (14, 0)
    c = Actor(113, 8)
    assert not commander_guard(c, 28, 338, True)
    assert c.energy == 8 and c.reaction
    c.hp = 112
    assert not commander_guard(c, 14, 338, True)
    assert not commander_guard(c, 28, 338, False)
    assert commander_guard(c, 28, 338, True)
    assert c.energy == 0 and not c.reaction
    assert not commander_guard(c, 36, 338, True)
    c.begin()
    c.energy = 8
    c.hp = 338
    assert commander_guard(c, 36, 338, False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--duels', type=int, default=12000)
    parser.add_argument('--seed', type=int, default=5321573)
    parser.add_argument('--max-rounds', type=int, default=60)
    parser.add_argument('--armor', type=float, nargs='+', default=[6])
    parser.add_argument('--averion-might', type=int, default=22,
                        help='Averion personal Might for attack accuracy and damage')
    parser.add_argument('--averion-sense', type=int, default=22,
                        help='Averion personal Sense for defense')
    parser.add_argument('--commander-power', type=float, default=23)
    parser.add_argument('--commander-integrity', type=float, default=23)
    parser.add_argument('--last-stand-guard', action=argparse.BooleanOptionalAction,
                        default=True,
                        help='Commander also guards B28 Main/counter at <=1/3 HP')
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args()
    self_test()
    if args.self_test:
        print('self-test: passed')
        return
    if args.duels < 1 or args.max_rounds < 1:
        parser.error('--duels and --max-rounds must be positive')
    if args.averion_might < 0 or args.averion_sense < 0:
        parser.error('Averion stats must be nonnegative')
    if not math.isfinite(args.commander_power) or args.commander_power <= 0:
        parser.error('This duel profile requires a commander with positive finite Might')
    if not math.isfinite(args.commander_integrity) or args.commander_integrity < 0:
        parser.error('--commander-integrity must be finite and nonnegative')
    if not all(math.isfinite(armor) for armor in args.armor):
        parser.error('--armor must contain finite resistance bonuses')
    armor_values = list(dict.fromkeys(args.armor))
    # At D50 the applicable stat contribution is capped at C = 20 + D/2.
    power = min(45, args.commander_power)
    integrity = min(45, args.commander_integrity)
    averion_might = min(45, args.averion_might)
    averion_sense = min(45, args.averion_sense)
    commander_hp = math.floor((100 + 3 * integrity) * 2 + .5)
    c_slash = damage_table(power, 22, 15, power - averion_sense, heavy_accuracy=1)
    c_crush = damage_table(power, 22, 0, power - averion_sense, heavy_accuracy=1)
    profiles = [('strong', 100), ('regular', 100), ('tactical', 100),
                ('regular', 68), ('tactical', 68), ('regular', 76),
                ('tactical', 76)]
    cases = [(armor, policy, energy) for armor in armor_values
             for policy, energy in profiles]
    commander_sense = 22
    tables = {armor: damage_table(averion_might, integrity, armor, averion_might - commander_sense,
                                 damage_bonus_by_base={28: 15, 36: 15})
              for armor in armor_values}
    results = {case: {'A': 0, 'C': 0, 'timeout': 0, 'rounds': []}
               for case in cases}
    rng = random.Random(args.seed)
    for _ in range(args.duels):
        # Fixed independent strike lanes: A Main, C Main, A counter. An
        # unused strike never advances another policy's random window.
        dice = bytes(rng.randrange(1, 21) for _ in range(args.max_rounds * 6))
        for case in cases:
            armor, policy, energy = case
            winner, rounds = duel(dice, policy, energy, tables[armor],
                                  c_slash, c_crush, commander_hp, args.max_rounds,
                                  last_stand_guard=args.last_stand_guard)
            results[case][winner] += 1
            if winner != 'timeout':
                results[case]['rounds'].append(rounds)
    print(f'seed={args.seed} duels/profile={args.duels} round-cap={args.max_rounds}')
    print(f'Commander HP={commander_hp}, effective Might={power:g}, '
          f'effective Integrity={integrity:g}; A HP=332, '
          f'effective Might={averion_might}, effective Sense={averion_sense}; '
          f'last-stand-guard={args.last_stand_guard}')
    print('armor_pp policy          A_start_DE A_wins C_wins timeout A_win_pct median_round')
    for case in cases:
        armor, policy, energy = case
        result = results[case]
        median = statistics.median(result['rounds']) if result['rounds'] else float('nan')
        print(f'{armor:8g} {policy:15} {energy:10} {result["A"]:6} '
              f'{result["C"]:6} {result["timeout"]:7} '
              f'{100 * result["A"] / args.duels:9.3f} {median:12g}')


if __name__ == '__main__':
    main()
