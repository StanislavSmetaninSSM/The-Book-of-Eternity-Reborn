#!/usr/bin/env python3
"""Planning-only #532/#1561 true-opening paired duel probe; stdlib only.

Fresh HP332/368, DE100/110 (A68 sensitivity), both reactions available,
no shields/charges. A initiative29 beats V26; reversed order is sensitivity
only, with no probability weight. No ally threat or pre-combat Main.
The adjacent varak-duel.py remains the authority for actors and mechanics.

V: first ward then renew; first ordinary strike then same renew; never ward.
Due thunder always precedes renewal. A: legacy ordinary; reserve24 when
spending on attacks (B36 for32 if V reaction spent and A>=56, B28 for16 if
A>=40, else free B14); self-Bell98 for16 takes priority over that reserve
when V has a positive unspent thunder shield and A has no barrier. An optional
--bell-limit N stops this player's Bell casts after N paid casts per duel;
subsequent choices follow reserve. It never limits V's shields. Both use
the inherited guard/counter policy. Decisions inspect actual resources
before rolling, never future dice.

Optional --respond-to-bell adds V's read-bell policy: ward-first until the
first observed paid self-Bell, then no more wards. Already due thunder keeps
its priority. This experimental response changes no stats, costs or clocks.

Each role has separate Main and counter lanes per round, including unused
lanes. All alternatives receive all sampled duels; no conditioning on who
survived or on an earlier roll. Rounds are pairs of personal opportunities,
including the opening round. HP0 stops before another action or End.
The HP0 rule is numerical; it does not settle surrender or the scene's outcome.

Optimization: load the unchanged kernel once and build immutable damage
tables once per run. One dice packet is reused only within its paired duel.
Mutable actors are fresh per case; decisions, affordability, reactions,
barrier clocks and the stopping rule are always checked on current state.
No process-persistent result cache, filesystem writes, or game imports.
"""

import argparse
import contextlib
from dataclasses import dataclass, field
import io
from pathlib import Path
import random
import runpy
import statistics
import unittest


_kernel = runpy.run_path(str(Path(__file__).with_name('varak-duel.py')))
Actor = _kernel['Actor']
cast_ward = _kernel['cast_ward']
ordinary_attack = _kernel['ordinary_attack']
thunder_attack = _kernel['thunder_attack']
attack_exchange = _kernel['attack_exchange']
make_tables = _kernel['make_tables']
hit = _kernel['hit']
A_POLICIES = ('normal', 'reserve', 'self-bell')
V_POLICIES = ('ward-first', 'strike-first', 'never-ward')
ORDERS = ('A-first', 'V-first')
LANES_PER_ROUND = 8


@dataclass
class Result:
    """Outcome and cheap action counts, without retaining a combat log."""

    winner: str
    rounds: int
    a: Actor
    v: Actor
    actions: dict = field(default_factory=dict)


def choose_a(a, v, policy):
    """Commit A's Main from visible state before any hidden roll."""
    if (policy == 'self-bell' and v.barrier > 0 and v.thunder_shield
            and not v.shield_spent and a.barrier <= 0 and a.energy >= 16):
        cast_ward(a, a, 98)
        return 'self-bell'
    if policy != 'normal':
        if not v.reaction and a.energy >= 56:
            a.energy -= 32
            return 'b36'
        if a.energy < 40:
            return 'b14'
    return f'b{ordinary_attack(a)}'


def take_turn(role, a, v, a_policy, v_policy, dice, tables, actions, bell_limit=None):
    """Take exactly one real opportunity, unless combat already ended."""
    if a.hp <= 0 or v.hp <= 0:
        return 'stopped'
    actor, target = (a, v) if role == 'A' else (v, a)
    actor.begin()
    due = actor.take_main()
    if role == 'A':
        policy = a_policy
        if (policy == 'self-bell' and bell_limit is not None
                and actions.get('A:self-bell', 0) >= bell_limit):
            policy = 'reserve'
        action = choose_a(a, v, policy)
        table, counter_table = tables['a'], tables['v_slash']
        main_dice, counter_dice = dice[0:2], dice[6:8]
        actor_max, target_max = 332, 368
    else:
        policy = v_policy
        if policy == 'read-bell':
            policy = 'never-ward' if actions.get('A:self-bell', 0) else 'ward-first'
        if thunder_attack(v, due):
            action = 'thunder'
        elif (policy != 'never-ward' and v.barrier <= 0 and v.energy >= 16
              and (v.main_count > 1 or policy == 'ward-first')):
            cast_ward(v, v, 71.2, thunder=True)
            action = 'ward'
        else:
            action = f'b{ordinary_attack(v)}'
        table = tables['v_crush'] if action == 'thunder' else tables['v_slash']
        counter_table = tables['a']
        main_dice, counter_dice = dice[2:4], dice[4:6]
        actor_max, target_max = 368, 332
    key = f'{role}:{action}'
    actions[key] = actions.get(key, 0) + 1
    if role == 'V' and action == 'ward' and actions[key] > 1:
        actions['V:renewal'] = actions.get('V:renewal', 0) + 1
    if action not in ('ward', 'self-bell'):
        base = 36 if action == 'thunder' else int(action[1:])
        attack_exchange(actor, target, table, counter_table, base,
                        main_dice, counter_dice, actor_max, target_max)
    # HP0 stops the entire duel immediately, including End regeneration.
    if a.hp > 0 and v.hp > 0:
        actor.end()
    return action


def duel(dice, a_policy, v_policy, order, a_energy, max_rounds, tables, bell_limit=None):
    """Play a fresh complete duel using fixed role/event dice lanes."""
    a, v = Actor(332, a_energy, 100, 12), Actor(368, 110, 110, 14)
    actions = {}
    roles = ('A', 'V') if order == 'A-first' else ('V', 'A')
    for round_index in range(max_rounds):
        lane = round_index * LANES_PER_ROUND
        round_dice = dice[lane:lane + LANES_PER_ROUND]
        for role in roles:
            take_turn(role, a, v, a_policy, v_policy, round_dice, tables, actions, bell_limit)
            if a.hp <= 0 or v.hp <= 0:
                winner = 'both' if a.hp <= 0 and v.hp <= 0 else 'A' if v.hp <= 0 else 'V'
                return Result(winner, round_index + 1, a, v, actions)
    return Result('timeout', max_rounds, a, v, actions)


def sample_batch(duels, seed, energies, max_rounds, tables, bell_limit=None,
                 respond_to_bell=False):
    """Sample every fixed policy using identical role/round/event dice."""
    v_policies = V_POLICIES + ('read-bell',) if respond_to_bell else V_POLICIES
    cases = [(order, energy, ap, vp) for order in ORDERS for energy in energies
             for ap in A_POLICIES for vp in v_policies]
    results = {case: dict(A=0, V=0, both=0, timeout=0, rounds=[],
                          gain=0, loss=0, shield_gain=0, shield_loss=0,
                          strong=0, bell=0, wards=0, renewals=0)
               for case in cases}
    rng = random.Random(seed)
    for _ in range(duels):
        dice = bytes(rng.randrange(1, 21) for _ in range(max_rounds * LANES_PER_ROUND))
        wins = {}
        for case in cases:
            order, energy, ap, vp = case
            outcome = duel(dice, ap, vp, order, energy, max_rounds, tables, bell_limit)
            values = results[case]
            values[outcome.winner] += 1
            if outcome.winner != 'timeout':
                values['rounds'].append(outcome.rounds)
            wins[case] = outcome.winner == 'A'
            values['strong'] += outcome.actions.get('A:b36', 0)
            values['bell'] += outcome.actions.get('A:self-bell', 0)
            values['wards'] += outcome.actions.get('V:ward', 0)
            values['renewals'] += outcome.actions.get('V:renewal', 0)
        for case in cases:
            baseline = (*case[:2], 'normal', 'ward-first')
            strike_first = (*case[:3], 'strike-first')
            values = results[case]
            values['gain'] += wins[case] and not wins[baseline]
            values['loss'] += wins[baseline] and not wins[case]
            values['shield_gain'] += wins[case] and not wins[strike_first]
            values['shield_loss'] += wins[strike_first] and not wins[case]
    return results


def format_percent(count, total):
    """Format an exact count/total percentage with two-place half-up rounding."""
    basis_points = (20000 * count + total) // (2 * total)
    return f'{basis_points // 100}.{basis_points % 100:02d}'


class OpeningTests(unittest.TestCase):
    """Literal boundary fixtures; unittest checks remain active under -O."""

    @classmethod
    def setUpClass(cls):
        cls.tables = make_tables()
        cls.full = bytes((15, 10))
        cls.miss = bytes((1, 10))
        cls.partial = bytes((10, 10))

    def fresh(self, a_energy=100):
        return Actor(332, a_energy, 100, 12), Actor(368, 110, 110, 14)

    def turn(self, role, a, v, ap='normal', vp='ward-first', dice=None):
        return take_turn(role, a, v, ap, vp,
                         dice or self.full * 4, self.tables, {})

    def test_ordinary_first_attack_has_no_shield(self):
        a, v = self.fresh()
        self.assertEqual(self.turn('A', a, v), 'b28')
        self.assertEqual((a.energy, a.turn, a.main_count), (96, 1, 1))
        self.assertEqual((v.hp, v.barrier, v.pending_due, v.turn), (250, 0, None, 0))

    def test_reaction_spent_before_first_opportunity_then_refreshed(self):
        a, v = self.fresh()
        # A misses, V counters for 24, before V's first personal turn.
        self.turn('A', a, v, dice=self.miss * 3 + self.full)
        self.assertEqual((v.energy, v.reaction, v.turn), (86, False, 0))
        self.assertEqual(self.turn('V', a, v), 'ward')
        self.assertEqual((v.energy, v.reaction, v.turn, v.main_count), (84, True, 1, 1))
        self.assertEqual((v.barrier, v.barrier_expiry), (71.2, 4))

    def test_ward_on_actual_first_opportunity_without_counter_cost(self):
        a, v = self.fresh()
        self.assertEqual(self.turn('V', a, v), 'ward')
        self.assertEqual((v.energy, a.hp, a.turn, v.turn), (108, 332, 0, 1))

    def test_first_open_attack_matches_independent_400_pair_oracle(self):
        healths = {}
        for attack in range(1, 21):
            for defense in range(1, 21):
                a, v = self.fresh()
                self.turn('A', a, v, dice=bytes((attack, defense)) + self.miss * 3)
                healths[v.hp] = healths.get(v.hp, 0) + 1
                self.assertEqual(v.barrier, 0)
                self.assertEqual(v.energy, 86 if v.hp == 368 else 110)
        self.assertEqual(healths, {368: 191, 309: 84, 250: 60, 180: 65})

    def test_first_choice_does_not_replace_later_renewals(self):
        for policy, first in (('ward-first', 'ward'), ('strike-first', 'b28'),
                              ('never-ward', 'b28')):
            a, v = self.fresh()
            self.assertEqual(self.turn('V', a, v, vp=policy), first)
            v.barrier = 0
            self.assertEqual(self.turn('V', a, v, vp=policy),
                             'b28' if policy == 'never-ward' else 'ward')

    def test_delayed_first_ward_is_not_a_renewal(self):
        a, v = self.fresh()
        actions = {}
        for want in ('b28', 'ward', 'ward'):
            self.assertEqual(take_turn('V', a, v, 'normal', 'strike-first',
                                      self.full * 4, self.tables, actions), want)
            if want == 'ward':
                self.assertEqual(actions.get('V:renewal', 0), actions['V:ward'] - 1)
                v.barrier = 0

    def test_due_thunder_precedes_renewal_and_is_not_rearmed(self):
        a, v = self.fresh()
        self.turn('V', a, v)
        self.turn('A', a, v)
        self.assertEqual((v.barrier, v.pending_due, v.shield_spent), (0, 2, True))
        self.assertEqual(self.turn('V', a, v), 'thunder')
        self.assertEqual((v.pending_due, v.main_count, v.energy), (None, 2, 106))
        self.assertEqual(self.turn('V', a, v), 'ward')
        self.assertFalse(v.shield_spent)

    def test_unaffordable_thunder_loses_next_main_window(self):
        a, v = self.fresh()
        v.energy, v.pending_due = 15, 1
        self.assertEqual(self.turn('V', a, v), 'b14')
        self.assertEqual((v.pending_due, v.energy), (None, 29))
        self.assertEqual(self.turn('V', a, v), 'ward')
        self.assertEqual((v.pending_due, v.energy), (None, 27))

    def test_reserve_thresholds_and_spent_reaction_are_visible(self):
        for energy, reaction, want, left in (
                (56, False, 'b36', 24), (55, False, 'b28', 39),
                (56, True, 'b28', 40), (40, True, 'b28', 24),
                (39, True, 'b14', 39)):
            a, v = self.fresh(energy)
            v.reaction = reaction
            self.assertEqual(choose_a(a, v, 'reserve'), want)
            self.assertEqual(a.energy, left)

    def test_self_bell_requires_live_unspent_enemy_barrier(self):
        for shield, spent, own, energy, want in (
                (71.2, False, 0, 16, 'self-bell'),
                (71.2, True, 0, 40, 'b28'),
                (0, False, 0, 40, 'b28'),
                (71.2, False, 1, 40, 'b28'),
                (71.2, False, 0, 15, 'b14')):
            a, v = self.fresh(energy)
            v.barrier, v.thunder_shield, v.shield_spent = shield, True, spent
            a.barrier = own
            self.assertEqual(choose_a(a, v, 'self-bell'), want)
            if want == 'self-bell':
                self.assertEqual((a.energy, a.barrier, a.thunder_shield), (0, 98, False))

    def test_bell_limit_zero_reproduces_reserve_for_entire_duel(self):
        dice = self.miss * (4 * 12)
        for order in ORDERS:
            reserve = duel(dice, 'reserve', 'ward-first', order, 100, 12, self.tables)
            limited = duel(dice, 'self-bell', 'ward-first', order, 100, 12,
                           self.tables, bell_limit=0)
            self.assertEqual(limited, reserve)

    def test_bell_limits_count_paid_casts_and_reset_for_each_duel(self):
        dice = self.miss * (4 * 16)
        for limit in (1, 2):
            for _ in range(2):
                result = duel(dice, 'self-bell', 'ward-first', 'V-first', 100, 16,
                              self.tables, bell_limit=limit)
                self.assertEqual(result.actions.get('A:self-bell', 0), limit)
                self.assertGreater(result.actions['V:ward'], limit)
        # An unaffordable opportunity consumes no cast allowance; after personal
        # regeneration the next opportunity can spend the one allowed cast.
        a, v = self.fresh(15)
        cast_ward(v, v, 71.2, thunder=True)
        actions = {}
        for want in ('b14', 'self-bell', 'b14'):
            self.assertEqual(take_turn('A', a, v, 'self-bell', 'ward-first',
                                      self.miss * 4, self.tables, actions,
                                      bell_limit=1), want)
            a.barrier = 0
        self.assertEqual(actions.get('A:self-bell', 0), 1)

    def test_negative_bell_limit_is_rejected_before_sampling(self):
        with contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit) as rejection:
                main(['--bell-limit', '-1'])
        self.assertEqual(rejection.exception.code, 2)

    def test_read_bell_observes_paid_cast_instead_of_hidden_policy(self):
        a, v = self.fresh()
        actions = {}
        def turn(role):
            return take_turn(role, a, v, 'self-bell', 'read-bell',
                             self.miss * 4, self.tables, actions)
        self.assertEqual(turn('V'), 'ward')
        v.barrier, a.energy = 0, 0
        self.assertEqual(turn('A'), 'b14')
        self.assertEqual(turn('V'), 'ward')  # No Bell was actually cast.
        a.energy = 16
        self.assertEqual(turn('A'), 'self-bell')
        self.assertEqual(actions.get('A:self-bell', 0), 1)
        v.barrier = 0
        self.assertEqual(turn('V'), 'b28')
        self.assertEqual(actions.get('V:ward', 0), 2)

    def test_read_bell_preserves_already_charged_thunder(self):
        a, v = self.fresh()
        actions = {}
        def turn(role):
            return take_turn(role, a, v, 'self-bell', 'read-bell',
                             self.full * 4, self.tables, actions)
        self.assertEqual(turn('V'), 'ward')
        self.assertEqual(turn('A'), 'self-bell')
        self.assertEqual(turn('A'), 'b28')
        self.assertEqual((v.barrier, v.pending_due), (0, 2))
        self.assertEqual(turn('V'), 'thunder')
        self.assertEqual((v.pending_due, v.energy), (None, 106))
        self.assertEqual(turn('V'), 'b28')
        self.assertEqual(actions.get('V:ward', 0), 1)

    def test_read_bell_is_ward_first_without_observed_bell(self):
        dice = (self.full + self.miss * 3) * 12
        for ap, limit in (('normal', None), ('reserve', None), ('self-bell', 0)):
            for order in ORDERS:
                original = duel(dice, ap, 'ward-first', order, 100, 12,
                                self.tables, bell_limit=limit)
                response = duel(dice, ap, 'read-bell', order, 100, 12,
                                self.tables, bell_limit=limit)
                self.assertEqual(response, original)

    def test_percent_rounds_half_up_at_exact_decimal_boundaries(self):
        self.assertEqual(format_percent(4833, 12000), '40.28')
        self.assertEqual(format_percent(7461, 12000), '62.18')
        self.assertEqual(format_percent(0, 12000), '0.00')
        self.assertEqual(format_percent(12000, 12000), '100.00')

    def test_one_opportunity_each_in_either_order(self):
        for order in ORDERS:
            result = duel(self.miss * 4, 'normal', 'never-ward', order, 100, 1, self.tables)
            self.assertEqual((result.rounds, result.a.turn, result.v.turn), (1, 1, 1))
            self.assertEqual((result.a.main_count, result.v.main_count), (1, 1))
            self.assertEqual(result.winner, 'timeout')

    def test_fixed_role_lanes_survive_reversed_order(self):
        dice = self.full + self.miss * 3
        for order in ORDERS:
            result = duel(dice, 'normal', 'never-ward', order, 100, 1, self.tables)
            self.assertEqual((result.a.hp, result.v.hp), (332, 250))

    def test_fixed_counter_lanes_survive_reversed_order(self):
        dice = self.miss * 2 + self.full + self.miss
        for order in ORDERS:
            result = duel(dice, 'normal', 'never-ward', order, 100, 1, self.tables)
            self.assertEqual((result.a.hp, result.v.hp), (332, 250))

    def test_all_policies_are_sampled_without_cherry_picking(self):
        for respond, expected in ((False, 18), (True, 24)):
            batch = sample_batch(2, 5321573, [100], 1, self.tables,
                                 respond_to_bell=respond)
            self.assertEqual(len(batch), expected)
            for case, values in batch.items():
                self.assertEqual(sum(values[w] for w in ('A', 'V', 'both', 'timeout')), 2)
                if case[2:] == ('normal', 'ward-first'):
                    self.assertEqual((values['gain'], values['loss']), (0, 0))

    def test_round_cap_timeouts_are_not_completed_durations(self):
        batch = sample_batch(1, 5321573, [100], 1, self.tables)
        for values in batch.values():
            self.assertEqual((values['timeout'], values['rounds']), (1, []))

    def test_dead_combatant_has_no_opportunity_or_end_regeneration(self):
        for role in ('A', 'V'):
            a, v = self.fresh()
            v.hp = 0
            self.assertEqual(self.turn(role, a, v), 'stopped')
            self.assertEqual((a.turn, v.turn, a.energy, v.energy), (0, 0, 100, 110))
        # Fatal A Main ends before regeneration and before V's first turn.
        a, v = self.fresh()
        v.hp = 1
        self.turn('A', a, v)
        self.assertEqual((a.energy, a.turn, v.turn, v.reaction, v.energy),
                         (84, 1, 0, False, 102))

    def test_counter_kill_stops_before_end(self):
        a, v = self.fresh()
        a.hp = 1
        self.turn('A', a, v, dice=self.miss * 3 + self.full)
        self.assertLessEqual(a.hp, 0)
        self.assertEqual((a.energy, v.energy, v.reaction), (76, 86, False))


def self_test():
    """Run deterministic boundary fixtures without relying on assert statements."""
    _kernel['self_test']()
    result = unittest.TextTestRunner(verbosity=1).run(
        unittest.defaultTestLoader.loadTestsFromTestCase(OpeningTests))
    if not result.wasSuccessful():
        raise SystemExit(1)


def main(argv=None):
    """Validate the CLI, sample paired duels and print denominators and context."""
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--duels', type=int, default=12000)
    parser.add_argument('--seed', type=int, default=5321573)
    parser.add_argument('--max-rounds', type=int, default=60)
    parser.add_argument('--a-energy', type=int, nargs='+', default=[100, 68],
                        help='starting DE, 0..100; default includes full and stress cases')
    parser.add_argument('--bell-limit', type=int, default=None,
                        help='maximum paid self-Bell casts per duel, then reserve; '
                        'default unlimited; does not limit V shields')
    parser.add_argument('--respond-to-bell', action='store_true',
                        help='add V read-bell: ward-first until an observed paid '
                        'self-Bell, then no recasts; due thunder remains first')
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args(argv)
    if args.duels < 1 or args.max_rounds < 1:
        parser.error('--duels and --max-rounds must be positive')
    if any(energy < 0 or energy > 100 for energy in args.a_energy):
        parser.error('--a-energy must be 0..100')
    if args.bell_limit is not None and args.bell_limit < 0:
        parser.error('--bell-limit must be nonnegative')
    self_test()
    if args.self_test:
        print('self-test: passed')
        return
    energies = list(dict.fromkeys(args.a_energy))
    results = sample_batch(args.duels, args.seed, energies, args.max_rounds,
                           make_tables(), args.bell_limit, args.respond_to_bell)
    print(f'seed={args.seed} duels/case={args.duels} round-cap={args.max_rounds}')
    if args.bell_limit is not None:
        print(f'self-bell limit={args.bell_limit} paid casts per duel, then reserve; '
              'V shield policy unchanged.')
    if args.respond_to_bell:
        print('read-bell: V follows ward-first until an observed paid self-Bell, '
              'then stops recasting; due thunder retains priority.')
    print('Fresh A HP332 DEcap100/regen12; V HP368 DE110/110 regen14; '
          'both reactions available; no barrier or charge.')
    print('Initiative29/26: A-first is primary; V-first is unweighted sensitivity.')
    print('Median counts rounds from opening, resolved duels only; all win rates use all N.')
    print('base+/-: discordant A wins versus normal/ward-first at same order/DE.')
    print('strike+/-: discordant A wins versus strike-first at same A policy/order/DE; '
          'ward-first rows isolate first-choice policy effect.')
    print('strong/bell/wards/renew: mean actual Main applications per duel, '
          'renew excludes the first actual V ward, even when delayed.')
    print('end8%: resolved by round8 / all N; timeouts excluded. '
          'Percentages use exact two-place half-up rounding.')
    print('order    A_DE A_policy  V_policy       A_win% V_win% median timeout both '
          'base+ base- strike+ strike- strong  bell wards renew end8%')
    for case, values in results.items():
        order, energy, ap, vp = case
        median = statistics.median(values['rounds']) if values['rounds'] else float('nan')
        ended_by_eight = sum(rounds <= 8 for rounds in values['rounds'])
        print(f'{order:8} {energy:4} {ap:9} {vp:12} '
              f'{format_percent(values["A"], args.duels):>6} '
              f'{format_percent(values["V"], args.duels):>6} '
              f'{median:6g} {values["timeout"]:7} {values["both"]:4} '
              f'{values["gain"]:5} {values["loss"]:5} '
              f'{values["shield_gain"]:7} {values["shield_loss"]:7} '
              f'{values["strong"] / args.duels:6.2f} {values["bell"] / args.duels:5.2f} '
              f'{values["wards"] / args.duels:5.2f} {values["renewals"] / args.duels:5.2f} '
              f'{format_percent(ended_by_eight, args.duels):>6}')
    print('Paired comparisons measure these fixed policies, not optimal play. '
          'No probability is assigned to the reverse order or to narrative outcomes.')


if __name__ == '__main__':
    main()
