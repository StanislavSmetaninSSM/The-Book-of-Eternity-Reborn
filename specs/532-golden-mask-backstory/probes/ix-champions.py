#!/usr/bin/env python3
"""Planning-only #532/#1561 duel calibration; run --self-test or --help.

Provisional IX cards and personal profiles, not runtime or a raid simulator.
The adjacent VIII probe supplies hit/barrier/rounding behavior; local tables
generalize its D50/Favor22 fixtures to real D60 and natural critical ranges.

A uses VIII's unchanged D60 profile and m40 Silver Edge. Counter uses m1:
mastery in Edge does not silently transfer to another art. Heart+hilt add
40% ONCE to either named sound strike; free B14 has neither bonus nor m40.
Tirsa's aimed ranged accuracy uses Sense; her damage uses Might. The others
use Might accuracy; every passive defense uses Sense. Final Dorvey wears
a cuirass with +15 percentage points sound resistance, in addition to his
Integrity innate defense. No extra light resistance is invented. All
participants can buy +5 defense for 4P.

Tirsa uses m15 B28 Needle (8P); B20 Rain (24P/7 per target) is weaker in
1v1, so the policy uses Needle. Ordis compares direct m20 B28 Voice (8P)
with paid m1 Song (+35%, 24P Main opening, 12P Fast renewal). No free
precombat Song; zero allied damage is added in this personal-duel probe.
Dorvey's m30 B28 Notch (12P) marks only after real HP damage and one
independent status-resistance check. Marked B36 Thrust costs 8P; without
the mark Thrust is B28. A mark is consumed by the next attack, including
a miss, and expires at the end of the next Dorvey turn. There is no
second attack roll for its bonus and no scaling by the previous outcome.

Fresh A: HP398/398 and DE113/113. Worn sensitivity: HP299 and DE77.8,
illustrating ~25% prior HP loss plus one 16P cover-and-counter expenditure
without a transition refill. Champions start full in every independent
duel. This does not predict health/resources across the whole assault.

Melee cases use A-first (initiative score34 vs28/24/29) and enemy-first
as order sensitivity, not weighted initiative probabilities. Tirsa alone
also gets one ranged opening: she shoots, A spends his first Main closing,
then both are reachable. Counters cannot cross the opening distance.
This assumes a traversable route taking one Main, no indefinite kiting,
and no free ranged attack for A. Architecture, movement and rescue duties
in the actual scene can change that route and exposure.

Policies inspect visible state before dice. Champions guard every hero
named strike. A guards marked B36 and any paid attack at <=1/3 max HP;
otherwise a full miss allows his reachable 26.4-DE counter. Ordinary and
strong-first hero policies are compared; strong uses B36/16P when affordable,
then B28/8P, then free B14. Both are Silver Edge m40. The same
single reaction pays guard OR counter. Optional --no-hero-reactions is
a no-reaction baseline. Champions have no invented counter technique.
--guard-all is a Dorvey-only tactical sensitivity: buy defense against
every paid attack instead of preserving counter against Notch.
HP0 stops before regeneration, purely as a numerical endpoint.

Immutable attack tables are built once per profile pairing and invocation;
the imported kernel loads once. No result cache or game/remote writes.
Each sampled duel supplies fixed Main/counter/status lanes per round to
ALL cases, including unused lanes. Fresh actors, affordability, reach,
mark state and reactions are always checked afresh; no hidden-dice policy.
"""

import argparse
from collections import Counter
from dataclasses import dataclass
import math
from pathlib import Path
import random
import runpy
import statistics
import unittest

_kernel = runpy.run_path(str(Path(__file__).with_name('varak-duel.py')))
Actor = _kernel['Actor']
hit = _kernel['hit']
quality = _kernel['quality']


@dataclass(frozen=True)
class Profile:
    """Permanent personal stats; derived budgets follow accepted #1573."""

    development: int
    might: int
    sense: int
    will: int
    integrity: int
    favor: int
    focus: int
    harmony: int
    mastery: int = 25
    sound_resistance: float = 0

    @property
    def p(self):
        return 1 + self.development / 50

    @property
    def hp(self):
        return math.floor((100 + 3 * self.capped(self.integrity)) * self.p + .5)

    @property
    def energy(self):
        return 40 * self.p + self.focus

    @property
    def regen(self):
        return 4 * self.p + self.harmony / 5

    def capped(self, stat):
        return min(20 + self.development / 2, max(0, stat))

    def modifier(self, stat):
        return self.capped(stat) + math.floor(.8 * self.development)


def build_profiles(dorvey='gatekeeper'):
    """Single editable configuration for the proposed personal profiles."""
    profiles = {
        'A': Profile(60, 31, 31, 37, 27, 27, 25, 25),
        'T': Profile(40, 22, 28, 22, 22, 20, 20, 20),
        'O': Profile(45, 24, 24, 28, 24, 22, 24, 24),
        'D': Profile(50, 29, 28, 24, 27, 22, 24, 24),
    }
    if dorvey == 'gatekeeper':
        profiles['D'] = Profile(60, 29, 29, 30, 40, 22, 30, 28, 30, 15)
    return profiles


def build_tables(attacker, defender):
    """Build all legal attack variants once, preserving opposed-d20 outcomes."""
    # action: (base at m1, own mastery, damage percent, accuracy stat)
    arts = {
        'edge': (28, 40, 40, attacker.might),
        'strong-edge': (36, 40, 40, attacker.might),
        'counter': (28, 1, 40, attacker.might),
        'free': (14, 1, 0, attacker.might),
        'needle': (28, 15, 0, attacker.sense),
        'rain': (20, 15, 0, attacker.sense),
        'voice': (28, 20, 0, attacker.might),
        'song-voice': (28, 20, 35, attacker.might),
        'song-free': (14, 1, 35, attacker.might),
        'notch': (28, attacker.mastery, 0, attacker.might),
        'thrust': (28, attacker.mastery, 0, attacker.might),
        'marked-thrust': (36, attacker.mastery, 0, attacker.might),
    }
    innate = defender.capped(defender.integrity) / (100 + 3 * defender.capped(defender.integrity))
    favor = attacker.capped(attacker.favor)
    critical_from = 20 - math.floor(5 * favor / (100 + favor))
    crit_factor = 1.5 + .5 * favor / (100 + favor)
    result = {}
    for action, (base, mastery, bonus, accuracy_stat) in arts.items():
        sound = action in ('edge', 'strong-edge', 'counter', 'free', 'voice',
                           'song-voice', 'song-free')
        resistance = min(.9, max(-1, innate + (defender.sound_resistance / 100 if sound else 0)))
        strength = (base * (25 + mastery) / 26
                    * (1 + .03 * attacker.capped(attacker.might)) * attacker.p
                    * (1 + bonus / 100) * (1 - resistance))
        for guarded in (False, True):
            delta = (attacker.modifier(accuracy_stat) - defender.modifier(defender.sense)
                     - (5 if guarded else 0))
            values = []
            for attack in range(1, 21):
                for defense in range(1, 21):
                    outcome = 3 if attack >= critical_from else quality(attack, defense, delta)
                    values.append((outcome, strength * (0, .5, 1, crit_factor)[outcome]))
            result[action, guarded] = values
    return result


def apply_attack(attacker, target, table, action, dice, guarded=False):
    """Use the shared barrier/rounding kernel, returning actual HP damage."""
    hp_before = target.hp
    outcome = hit(target, table, action, guarded, dice, 0)
    return outcome, hp_before - target.hp


def cost(profile, coefficient):
    """Round the full payment upward once to hundredths."""
    return math.ceil(coefficient * profile.p * 100 - 1e-9) / 100


def pay(actor, amount):
    """Keep repeated fractional payments exact to resource hundredths."""
    if actor.energy + 1e-9 < amount:
        return False
    actor.energy = round(actor.energy - amount, 2)
    return True


def defend(actor, profile, paid_attack, enabled=True):
    """Commit a legal reaction before dice; never guard free B14 here."""
    if enabled and paid_attack and actor.reaction and pay(actor, cost(profile, 4)):
        actor.reaction = False
        return True
    return False


def choose_enemy(actor, profile, champion, mark, song):
    """Return Main, new song state and whether its paid opening used Main."""
    if champion == 'O' and song is not None:
        if not song and pay(actor, cost(profile, 24)):
            return 'song-open', True
        if song:
            song = pay(actor, cost(profile, 12))
    if champion == 'D':
        action, coefficient = ('marked-thrust', 8) if mark else ('notch', 12)
    else:
        action, coefficient = ('needle', 8) if champion == 'T' else ('voice', 8)
    if not pay(actor, cost(profile, coefficient)):
        # Unmarked plain thrust is a legal cheaper alternative to Notch.
        action = 'thrust' if champion == 'D' and pay(actor, cost(profile, 8)) else 'free'
    if champion == 'O' and song:
        action = 'song-' + action
    return action, song


def mark_applies(hp_damage, roll, resistance=0, immune=False):
    """One status check after HP damage; immunity is distinct from 90% cap."""
    return (hp_damage > 0 and not immune
            and roll > min(90, max(0, resistance)) * 100)


@dataclass
class Result:
    winner: str
    rounds: int
    hero_hp: int
    hero_energy: float
    actions: Counter


def duel(packets, champion, profiles, tables, worn=False, order='A-first',
         song_policy=False, status_resistance=0, hero_reactions=True,
         hero_policy='ordinary', guard_all=False, mark_immune=False):
    """Resolve one complete personal duel; never refill at a scene boundary."""
    ap, cp = profiles['A'], profiles[champion]
    a = Actor(299 if worn else ap.hp, 77.8 if worn else ap.energy, ap.energy, ap.regen)
    c = Actor(cp.hp, cp.energy, cp.energy, cp.regen)
    actions = Counter()
    mark, song = False, False if song_policy else None
    roles = ('A', 'C') if order == 'A-first' else ('C', 'A')
    for round_index, packet in enumerate(packets):
        for role in roles:
            actor, target = (a, c) if role == 'A' else (c, a)
            own, other = (ap, cp) if role == 'A' else (cp, ap)
            actor.begin()
            reachable = not (order == 'ranged-opening' and round_index == 0)
            if role == 'A' and not reachable:
                actions['A:close'] += 1
                actor.end()
                continue
            if role == 'A':
                if hero_policy == 'strong' and pay(a, cost(ap, 16)):
                    action = 'strong-edge'
                else:
                    action = 'edge' if pay(a, cost(ap, 8)) else 'free'
                main_dice, counter_dice = packet[0:2], packet[6:8]
            else:
                action, song = choose_enemy(c, cp, champion, mark, song)
                # A previous mark is gone after this next Main, even on miss.
                mark = False
                main_dice, counter_dice = packet[2:4], packet[4:6]
            actions[role + ':' + action] += 1
            if action != 'song-open':
                paid_attack = action not in ('free', 'song-free')
                hero_wants_guard = ((guard_all and champion == 'D')
                                    or action == 'marked-thrust' or a.hp <= ap.hp / 3)
                guarded = defend(target, other, paid_attack,
                                 hero_reactions and hero_wants_guard if role == 'C' else True)
                if guarded:
                    actions[('C' if role == 'A' else 'A') + ':guard'] += 1
                outcome, hp_damage = apply_attack(actor, target, tables[role], action,
                                                 main_dice, guarded)
                if action == 'marked-thrust' and outcome > 0:
                    actions['C:marked-thrust-hit'] += 1
                if role == 'C' and action == 'notch' and hp_damage > 0:
                    mark = mark_applies(hp_damage, packet[8], status_resistance, mark_immune)
                    actions['C:mark'] += int(mark)
                if (role == 'C' and outcome == 0 and hero_reactions and reachable
                        and a.hp > 0 and a.reaction and pay(a, cost(ap, 12))):
                    a.reaction = False
                    actions['A:counter'] += 1
                    guarded = defend(c, cp, True)
                    apply_attack(a, c, tables['A'], 'counter', counter_dice, guarded)
            if a.hp <= 0 or c.hp <= 0:
                return Result('A' if c.hp <= 0 else 'C', round_index + 1,
                              max(0, a.hp), a.energy, actions)
            actor.end()
    return Result('timeout', len(packets), max(0, a.hp), a.energy, actions)


def self_test():
    class CalibrationTests(unittest.TestCase):
        def test_profiles_preserve_viii_and_derive_champion_health(self):
            profiles = build_profiles()
            self.assertEqual(set(profiles), {'A', 'T', 'O', 'D'})
            self.assertEqual([profiles[key].hp for key in ('A', 'T', 'O', 'D')],
                             [398, 299, 327, 484])
            self.assertEqual(profiles['A'].energy, 113)

        def test_viii_reference_damage_and_natural_critical_distribution(self):
            self.assertTrue(callable(globals().get('Profile')), 'Missing stat model')
            a = build_profiles()['A']
            varak = Profile(50, 28, 26, 26, 28, 22, 30, 30)
            tables = build_tables(a, varak)
            self.assertIn(('edge', False), tables, 'Missing real attack tables')
            full = [damage for outcome, damage in tables['edge', False] if outcome == 2]
            self.assertEqual(math.floor(full[0] + .5), 353)
            self.assertEqual(Counter(outcome for outcome, _ in tables['edge', False]),
                             {0: 35, 1: 40, 2: 65, 3: 260})
            self.assertEqual(Counter(outcome for outcome, _ in tables['edge', True]),
                             {0: 75, 1: 65, 2: 84, 3: 176})

        def test_absorbed_hit_is_not_a_miss_or_a_mark(self):
            a, d = Actor(398, 113, 113, 13.8), Actor(362, 104, 104, 12.8)
            d.barrier = 100
            outcome, hp_damage = apply_attack(a, d, {('edge', False): [(2, 80)] * 400},
                                             'edge', [10, 10])
            self.assertEqual((outcome, hp_damage, d.hp, d.barrier), (2, 0, 362, 20))

        def test_song_requires_paid_main_and_paid_fast_renewal(self):
            self.assertTrue(callable(globals().get('choose_enemy')), 'Missing enemy policy')
            p = build_profiles()['O']
            actor = Actor(p.hp, p.energy, p.energy, p.regen)
            self.assertEqual(choose_enemy(actor, p, 'O', False, False), ('song-open', True))
            self.assertAlmostEqual(actor.energy, 54.4)
            self.assertEqual(choose_enemy(actor, p, 'O', False, True), ('song-voice', True))
            self.assertAlmostEqual(actor.energy, 16.4)

        def test_marked_thrust_spends_own_main_and_exact_cost(self):
            self.assertTrue(callable(globals().get('choose_enemy')), 'Missing enemy policy')
            p = build_profiles()['D']
            actor = Actor(p.hp, cost(p, 8), p.energy, p.regen)
            self.assertEqual(choose_enemy(actor, p, 'D', True, None), ('marked-thrust', None))
            self.assertEqual(actor.energy, 0)

        def test_guard_and_counter_share_one_reaction(self):
            self.assertTrue(callable(globals().get('defend')), 'Missing reaction policy')
            p = build_profiles()['A']
            actor = Actor(p.hp, 100, p.energy, p.regen)
            self.assertTrue(defend(actor, p, True))
            self.assertFalse(actor.reaction)
            self.assertFalse(defend(actor, p, True))
            self.assertAlmostEqual(actor.energy, 91.2)

        def test_ranged_opening_prevents_immediate_melee_and_counter(self):
            self.assertTrue(callable(globals().get('duel')), 'Missing duel')
            profiles = build_profiles()
            a, t = profiles['A'], profiles['T']
            tables = {'A': build_tables(a, t), 'C': build_tables(t, a)}
            packets = [[20, 1, 1, 20, 20, 1, 20, 1, 10000]]
            result = duel(packets, 'T', profiles, tables, order='ranged-opening')
            self.assertEqual(result.actions['A:close'], 1)
            self.assertEqual(result.actions['A:counter'], 0)
            self.assertEqual(result.actions['A:edge'], 0)
            self.assertEqual(result.hero_hp, 398)

        def test_final_gatekeeper_and_first_blow_oracle(self):
            profiles = build_profiles()
            d = profiles['D']
            self.assertEqual((d.development, d.might, d.sense, d.mastery,
                              d.hp, d.energy, d.regen, d.sound_resistance),
                             (60, 29, 29, 30, 484, 118, 14.4, 15))
            self.assertEqual(build_profiles('legacy')['D'].hp, 362)
            table = build_tables(profiles['A'], d)
            self.assertEqual([sum(math.floor(dmg + .5) >= d.hp for _, dmg in table[key, True])
                              for key in ('edge', 'strong-edge')], [0, 55])

        def test_mark_consumes_next_attempt_even_on_miss_and_rechecks_resistance(self):
            profiles = build_profiles()
            # Zero-damage A misses keep both actors alive; C alternates a real
            # HP-damaging partial Notch and a missed Thrust, on real turns.
            at = {(action, guarded): [(0, 0)] * 400
                  for action in ('edge', 'strong-edge', 'free', 'counter')
                  for guarded in (False, True)}
            ct = {(action, guarded): [(0 if action == 'marked-thrust' else 1,
                                      0 if action == 'marked-thrust' else 1)] * 400
                  for action in ('notch', 'marked-thrust', 'thrust', 'free')
                  for guarded in (False, True)}
            packets = [[10] * 8 + [5000] for _ in range(3)]
            tables = {'A': at, 'C': ct}
            normal = duel(packets, 'D', profiles, tables)
            self.assertEqual(normal.actions['C:notch'], 2)
            self.assertEqual(normal.actions['C:marked-thrust'], 1)
            self.assertEqual(normal.actions['C:marked-thrust-hit'], 0)
            self.assertEqual(normal.hero_hp, 396)
            resisted = duel(packets, 'D', profiles, tables, status_resistance=50)
            self.assertEqual(resisted.actions['C:notch'], 3)
            self.assertEqual(resisted.actions['C:marked-thrust'], 0)
            immune = duel(packets, 'D', profiles, tables, mark_immune=True)
            self.assertEqual(immune.actions['C:mark'], 0)
            self.assertEqual(immune.actions['C:marked-thrust'], 0)

        def test_barrier_and_status_boundary_are_independent(self):
            self.assertFalse(mark_applies(0, 10000))
            self.assertFalse(mark_applies(1, 2500, 25))
            self.assertTrue(mark_applies(1, 2501, 25))
            self.assertTrue(mark_applies(1, 10000, 200))  # cap90 is not immunity
            self.assertFalse(mark_applies(1, 10000, 0, immune=True))

        def test_hp_zero_stops_before_regeneration_or_next_main(self):
            profiles = build_profiles()
            at = {(action, guarded): [(3, 10000)] * 400
                  for action in ('edge', 'strong-edge', 'free', 'counter')
                  for guarded in (False, True)}
            result = duel([[10] * 8 + [10000]], 'D', profiles, {'A': at, 'C': {}})
            self.assertEqual(result.winner, 'A')
            self.assertAlmostEqual(result.hero_energy, 95.4)
            self.assertEqual(result.actions['C:notch'], 0)
    result = unittest.TextTestRunner(verbosity=2).run(
        unittest.defaultTestLoader.loadTestsFromTestCase(CalibrationTests))
    if not result.wasSuccessful():
        raise SystemExit(1)


def sample_batch(duels, seed, max_rounds, profiles, status_resistance=0,
                 hero_reactions=True, champions=('T', 'O', 'D'), guard_all=False,
                 pairings=None):
    """Pair all policies on fixed dice; keep immutable DATA local to this run."""
    if pairings is None:
        pairings = {key: {'A': build_tables(profiles['A'], profiles[key]),
                          'C': build_tables(profiles[key], profiles['A'])}
                    for key in champions}
    cases = [(key, worn, order, song, hero_policy)
             for key in champions for worn in (False, True)
             for order in (('A-first', 'C-first', 'ranged-opening') if key == 'T'
                           else ('A-first', 'C-first'))
             for song in ((False, True) if key == 'O' else (False,))
             for hero_policy in ('ordinary', 'strong')]
    records = {case: {'wins': Counter(), 'rounds': [], 'losses': [],
                      'marked_attempt': 0, 'marked_hit': 0} for case in cases}
    rng = random.Random(seed)
    for _ in range(duels):
        packets = [[rng.randint(1, 20) for _ in range(8)] + [rng.randint(1, 10000)]
                   for _ in range(max_rounds)]
        for case in cases:
            key, worn, order, song, hero_policy = case
            result = duel(packets, key, profiles, pairings[key], worn, order,
                          song, status_resistance, hero_reactions, hero_policy, guard_all)
            record = records[case]
            record['wins'][result.winner] += 1
            if result.winner != 'timeout':
                record['rounds'].append(result.rounds)
            record['losses'].append((299 if worn else profiles['A'].hp) - result.hero_hp)
            record['marked_attempt'] += int(result.actions['C:marked-thrust'] > 0)
            record['marked_hit'] += int(result.actions['C:marked-thrust-hit'] > 0)
    return records


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--self-test', action='store_true')
    parser.add_argument('--duels', type=int, default=12000)
    parser.add_argument('--seed', type=int, default=5321561)
    parser.add_argument('--max-rounds', type=int, default=30)
    parser.add_argument('--status-resistance', type=float, default=0,
                        help='A resistance to Dorvey mark, percent 0..90; no default source')
    parser.add_argument('--no-hero-reactions', action='store_true')
    parser.add_argument('--dorvey', choices=('legacy', 'gatekeeper'), default='gatekeeper',
                        help='legacy D50 or proposed stronger final guard with sound cuirass')
    parser.add_argument('--only', choices=('T', 'O', 'D'), help='sample only this champion')
    parser.add_argument('--guard-all', action='store_true',
                        help='A buys defense against every named Dorvey strike, sacrificing counter')
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return
    if args.duels <= 0 or args.max_rounds <= 0:
        parser.error('duels and max-rounds must be positive')
    if not math.isfinite(args.status_resistance) or not 0 <= args.status_resistance <= 90:
        parser.error('status-resistance must be between 0 and 90 percent')
    profiles = build_profiles(args.dorvey)
    pairings = {key: {'A': build_tables(profiles['A'], profiles[key]),
                      'C': build_tables(profiles[key], profiles['A'])}
                for key in ('T', 'O', 'D')}
    print('Planning probe IX; provisional champion profiles; no runtime writes.')
    print(f'Seed={args.seed}; duels per case={args.duels}; round limit={args.max_rounds}')
    print('A: fresh HP398/DE113; worn HP299/DE77.8; no transition refill.')
    print(f'Hero reactions={not args.no_hero_reactions}; guard-all={args.guard_all}; '
          f'mark resistance={args.status_resistance:g}%')
    print('Profile: D Might Sense Will Integrity Favor Focus Harmony | HP DE Regen')
    for key, p in profiles.items():
        print(f'{key}: {p.development} {p.might} {p.sense} {p.will} {p.integrity} '
              f'{p.favor} {p.focus} {p.harmony} | {p.hp} {p.energy:g} {p.regen:g}')
    print(f'Mastery: A Edge40, counter1; T Needle/Rain15; O Voice20/Song1; D both{profiles["D"].mastery}.')
    print(f'Dorvey={args.dorvey}; equipped cuirass sound resistance '
          f'+{profiles["D"].sound_resistance:g} percentage points.')
    print('All costs use source P: A Edge17.60/counter26.40/guard8.80; '
          'T Needle14.40/Rain6.18 per target (three18.52); '
          f'O Voice15.20/Song45.60+22.80; D Notch{cost(profiles["D"], 12):g}/'
          f'Thrust{cost(profiles["D"], 8):g}.')
    print('Rain sum is rounded once after selecting targets; 3*6.18 is not its tariff.')
    print('Exact first guarded blow, all400 pairs (normal/strong one-hit outcomes):')
    for key in ('T', 'O', 'D'):
        table = pairings[key]['A']
        counts = [sum(math.floor(damage + .5) >= profiles[key].hp
                      for _, damage in table[action, True])
                  for action in ('edge', 'strong-edge')]
        print(f'{key}: {counts[0]}/400 normal, {counts[1]}/400 strong')
    print('| C | A start | A policy | Order | Enemy policy | A win% | Timeout | Median rounds | '
          'Mean A HP loss | Marked attempt% | Marked hit% |')
    print('|---|---|---|---|---|---:|---:|---:|---:|---:|---:|')
    records = sample_batch(args.duels, args.seed, args.max_rounds, profiles,
                           args.status_resistance, not args.no_hero_reactions,
                           (args.only,) if args.only else ('T', 'O', 'D'), args.guard_all,
                           pairings)
    for (key, worn, order, song, hero_policy), record in records.items():
        rounds = statistics.median(record['rounds']) if record['rounds'] else float('nan')
        attempt = f"{100 * record['marked_attempt'] / args.duels:.2f}" if key == 'D' else '-'
        landed = f"{100 * record['marked_hit'] / args.duels:.2f}" if key == 'D' else '-'
        print(f'| {key} | {"worn" if worn else "fresh"} | {hero_policy} | {order} | '
              f'{"paid Song" if song else "direct"} | '
              f'{100 * record["wins"]["A"] / args.duels:.2f} | '
              f'{record["wins"]["timeout"]} | {rounds:g} | '
              f'{statistics.mean(record["losses"]):.1f} | {attempt} | {landed} |')
    print('Marked attempt/hit: fraction of ALL started duels with at least one such event; '
          'partial hit counts as landed. Median covers resolved duels only.')
    print('No encounter-wide win claim: terrain, allies, mercy, summons and surrender are excluded.')


if __name__ == '__main__':
    main()
