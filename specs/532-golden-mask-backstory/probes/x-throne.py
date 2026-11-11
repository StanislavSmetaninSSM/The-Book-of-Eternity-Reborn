#!/usr/bin/env python3
"""Planning-only X opening calibration, exact finite opposed-d20 enumeration.

No production/game imports or writes. Reuses IX's personal-stat class and the
shared opposed-d20 quality kernel. --rounds 2 is an opening comparison, NOT a
full-fight win rate. Order A/L/S is the visible 34/32/28 fixture. Everyone
starts healthy, fully rested, reachable and without barriers; no IX refill is
implied. HP0 is a numerical stop, not surrender or the trap's trigger.

Included: Edge m40, its +40% hilt/Heart bonus; m40 Golden-head Shackles;
Counter m25 (or --counter-mastery 1), only after a reachable full miss with
the same still-available reaction and a separate 12P payment/attack roll;
Lucian m35 lunar B28/B36 slash, Sense accuracy/Might force, optional ordinary
m35 Moon Cut cleansing; Saref m25 B28 spatial strike, Will accuracy/Might
force, or ordinary m30 B20 Will barrier. Every actor owns Main/DE/reaction.
Lucian may intercept a direct Edge aimed at Saref for 4P instead of guard;
the redirected strike uses Lucian's passive defense. Shackles are pure
Will-vs-Will, cannot be intercepted, and permit own Main/free break or a
compatible paid Moon Cut; bound actors have no active reaction. Bound-turn
expiry, personal end-turn DE and a ward's three recipient turns are tracked.

A reserves +5 guard for Saref while both foes live, otherwise for the sole
foe; no reaction is manufactured. Foes guard Edge or control when available.
Saref's ward policy renews an absent barrier on the living ally (self alone)
and otherwise attacks. It is not free setup. Existing wards are replaced,
not stacked. No speculative status resistance, immunity, movement, summons,
items, mark, Bell, other arts, surrender or trap are included.
These are equal-subset policy comparisons, not optimal or full-loadout odds.
"""

import argparse
from collections import Counter, defaultdict
from dataclasses import dataclass, replace
from functools import lru_cache
import math
from pathlib import Path
import runpy
import unittest

_ix = runpy.run_path(str(Path(__file__).with_name('ix-champions.py')))
Profile, quality = _ix['Profile'], _ix['quality']
PROFILES = {
    'A': Profile(60, 31, 31, 37, 27, 27, 25, 25),
    'L': Profile(60, 30, 32, 28, 36, 25, 28, 28),
    'S': Profile(60, 28, 28, 35, 34, 25, 32, 30),
}
INDEX = {'A': 0, 'L': 1, 'S': 2}


def g(mastery):
    return (25 + mastery) / 26


def cost(role, coefficient):
    return round(coefficient * PROFILES[role].p, 2)


@dataclass(frozen=True)
class State:
    hp: int
    de: float
    reaction: bool = True
    bound: int = 0
    ward: float = 0
    ward_turns: int = 0


def change(state, role, **fields):
    return state[:INDEX[role]] + (replace(state[INDEX[role]], **fields),) + state[INDEX[role]+1:]


def spend(state, role, amount, reaction=False):
    actor = state[INDEX[role]]
    assert actor.de + 1e-8 >= amount
    return change(state, role, de=round(actor.de - amount, 2),
                  reaction=False if reaction else actor.reaction)


@lru_cache(maxsize=None)
def outcomes(role, defense, delta):
    """Exact probability of each quality; opposing die has no own critical."""
    favor = PROFILES[role].favor
    natural = 20 - math.floor(5 * favor / (100 + favor))
    counts = Counter(3 if a >= natural else quality(a, d, delta)
                     for a in range(1, 21) for d in range(1, 21))
    return tuple((q, n / 400) for q, n in sorted(counts.items()))


def strike_distribution(state, source, target, base, mastery, guarded, pierce=False):
    p, t = PROFILES[source], PROFILES[target]
    accuracy = p.might if source == 'A' else p.sense if source == 'L' else p.will
    delta = p.modifier(accuracy) - t.modifier(t.sense) - (5 if guarded else 0)
    resistance = t.integrity / (100 + 3 * t.integrity)
    if source == 'L' and target == 'A':
        resistance += .15
    if pierce:
        resistance = max(0, resistance - .15)
    strength = base * g(mastery) * (1 + .03 * p.might) * p.p
    strength *= (1.4 if source == 'A' and base != 14 else 1) * (1 - resistance)
    critical = 1.5 + .5 * p.favor / (100 + p.favor)
    for q, probability in outcomes(source, target, delta):
        incoming = strength * (0, .5, 1, critical)[q]
        actor = state[INDEX[target]]
        absorbed = min(actor.ward, incoming)
        hp = max(0, actor.hp - math.floor(incoming - absorbed + .5))
        yield change(state, target, hp=hp, ward=round(actor.ward-absorbed, 10)), probability, q


def guard(state, role):
    actor = state[INDEX[role]]
    if actor.hp and actor.reaction and not actor.bound and actor.de >= cost(role, 4):
        return spend(state, role, cost(role, 4), reaction=True), True
    return state, False


def shackle_distribution(state, target):
    state = spend(state, 'A', cost('A', 24))
    state, guarded = guard(state, target)
    a, t = PROFILES['A'], PROFILES[target]
    delta = a.modifier(a.will) + 2*g(40) + 6 - t.modifier(t.will) - 5*guarded
    for q, probability in outcomes('A', target, delta):
        yield change(state, target, bound=2 if q >= 2 else 1 if q else 0), probability


def break_distribution(state, role, moon_cut=False):
    p, a = PROFILES[role], PROFILES['A']
    strength = a.modifier(a.will) + 7*g(40)
    if moon_cut and role == 'L' and state[1].de >= cost('L', 8):
        state = spend(state, role, cost('L', 8))
        delta = p.modifier(p.will) + 5*g(35) - strength
    else:
        delta = p.modifier(max(p.might, p.will)) - strength
    chance = sum(prob for q, prob in outcomes(role, 'A', delta) if q)
    yield change(state, role, bound=0), chance
    yield state, 1-chance


def direct_strike(state, role, target, base, mastery, main=True):
    intercepted = False
    if role == 'A' and target == 'S':
        l = state[1]
        if l.hp and l.reaction and not l.bound and l.de >= cost('L', 4):
            state = spend(state, 'L', cost('L', 4), reaction=True)
            target, intercepted = 'L', True
    guarded = False
    if role == 'A':
        if not intercepted:
            state, guarded = guard(state, target)
    elif role == 'S' or not state[2].hp or state[2].bound:
        state, guarded = guard(state, 'A')
    pierce = role == 'A' and main and bool(state[INDEX[target]].bound)
    yield from strike_distribution(state, role, target, base, mastery, guarded, pierce)


def main_distribution(state, role, policy, round_index, enemy_policy, moon_cut,
                      counter_mastery=25):
    own = state[INDEX[role]]
    if own.bound:
        yield from break_distribution(state, role, moon_cut)
        return
    if role == 'A' and policy == 'control-focus' and round_index == 0 and state[1].hp:
        yield from shackle_distribution(state, 'L')
        return
    if role == 'S' and enemy_policy == 'ward':
        target = 'L' if state[1].hp else 'S'
        if not state[INDEX[target]].ward and own.de >= cost(role, 8):
            state = spend(state, role, cost(role, 8))
            reserve = 20*g(30)*(1+.03*PROFILES[role].will)*PROFILES[role].p
            yield change(state, target, ward=round(reserve, 10),
                         ward_turns=4 if target == role else 3), 1
            return
    if role == 'A':
        if policy.startswith('alternating'):
            target = ('L', 'S')[round_index % 2]
            if not state[INDEX[target]].hp:
                target = 'S' if target == 'L' else 'L'
        else:
            target = 'S' if state[2].hp else 'L'
        strong = policy != 'alternating-ordinary'
        mastery = 40
    else:
        target, strong, mastery = 'A', role == 'L', 35 if role == 'L' else 25
    base, coefficient = (36, 16) if strong and own.de >= cost(role, 16) else (28, 8)
    if own.de < cost(role, coefficient):
        base, coefficient, mastery = 14, 0, 1
    state = spend(state, role, cost(role, coefficient))
    for result, probability, outcome in direct_strike(state,role,target,base,mastery):
        a = result[0]
        if role != 'A' and outcome == 0 and a.hp and a.reaction and a.de >= cost('A',12):
            result = spend(result,'A',cost('A',12),reaction=True)
            for counter, chance, _ in direct_strike(result,'A',role,28,counter_mastery,main=False):
                yield counter, probability*chance
        else:
            yield result, probability


def opening(policy, opponents='pair', enemy_policy='attack', rounds=2, moon_cut=False,
            counter_mastery=25):
    state = tuple(State(p.hp if opponents == 'pair' or role in ('A', opponents) else 0,
                        p.energy) for role, p in PROFILES.items())
    distribution = {state: 1.0}
    for round_index in range(rounds):
        for role in ('A', 'L', 'S'):
            following = defaultdict(float)
            for state, probability in distribution.items():
                if not state[0].hp or not (state[1].hp or state[2].hp) or not state[INDEX[role]].hp:
                    following[state] += probability
                    continue
                state = change(state, role, reaction=True)
                for result, branch in main_distribution(state, role, policy, round_index,
                                                        enemy_policy, moon_cut,counter_mastery):
                    if result[0].hp and (result[1].hp or result[2].hp) and result[INDEX[role]].hp:
                        actor, p = result[INDEX[role]], PROFILES[role]
                        turns = max(0, actor.ward_turns-1)
                        result = change(result, role, de=round(min(p.energy,actor.de+p.regen),2),
                                        bound=max(0,actor.bound-1), ward_turns=turns,
                                        ward=actor.ward if turns else 0)
                    following[result] += probability*branch
            distribution = following
    return distribution


def metrics(distribution, opponents):
    relevant = ('L', 'S') if opponents == 'pair' else (opponents,)
    return {
        'probability': sum(distribution.values()),
        'A_survives': sum(p for s,p in distribution.items() if s[0].hp),
        'A_HP': sum(p*s[0].hp for s,p in distribution.items()),
        'A_DE': sum(p*s[0].de for s,p in distribution.items()),
        'one_enemy_out': sum(p for s,p in distribution.items() if any(not s[INDEX[r]].hp for r in relevant)),
        'all_enemies_out': sum(p for s,p in distribution.items() if all(not s[INDEX[r]].hp for r in relevant)),
        'enemy_HP': sum(p*sum(s[INDEX[r]].hp for r in relevant) for s,p in distribution.items()),
        'states': len(distribution),
    }


class Tests(unittest.TestCase):
    def test_profiles(self):
        self.assertEqual([p.hp for p in PROFILES.values()], [398,458,444])
        self.assertEqual([p.energy for p in PROFILES.values()], [113,116,120])

    def test_shackle_will_not_sense_and_natural_break(self):
        state = tuple(State(p.hp,p.energy) for p in PROFILES.values())
        actual = list(shackle_distribution(state,'L'))
        self.assertAlmostEqual(sum(p for s,p in actual if s[1].bound==2), .86)
        self.assertAlmostEqual(sum(p for s,p in break_distribution(actual[-1][0],'L')),1)
        self.assertAlmostEqual(next(iter(break_distribution(actual[-1][0],'L')))[1],.1)

    def test_resource_and_bound_turn(self):
        dist = opening('control-focus',rounds=1)
        self.assertAlmostEqual(sum(dist.values()),1)
        self.assertTrue(all(0<=s[0].de<=113 for s in dist))
        self.assertTrue(all(s[1].bound<=1 for s in dist))
        self.assertTrue(all(not s[0].ward for s in dist))

    def test_intercept_uses_own_reaction_and_passive_defense(self):
        state = tuple(State(p.hp,p.energy) for p in PROFILES.values())
        branches = list(main_distribution(state,'A','focus',0,'attack',False))
        self.assertTrue(all(s[2].hp==444 for s,p in branches))
        self.assertTrue(all(not s[1].reaction and s[1].de==107.2 for s,p in branches))
        self.assertAlmostEqual(sum(p for s,p in branches if s[1].hp==0),.17)

    def test_counter_has_one_payment_and_same_reaction(self):
        state = tuple(State(p.hp,p.energy) for p in PROFILES.values())
        branches = list(main_distribution(state,'L','focus',0,'attack',False))
        self.assertTrue(any(s[0].de==86.6 and not s[0].reaction for s,p in branches))
        self.assertTrue(all(s[0].de in (86.6,113) for s,p in branches))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rounds',type=int,choices=(1,2,3),default=2)
    parser.add_argument('--moon-cut',action='store_true')
    parser.add_argument('--counter-mastery',type=int,choices=(1,25),default=25)
    parser.add_argument('--self-test',action='store_true')
    args = parser.parse_args()
    if args.self_test:
        result = unittest.TextTestRunner().run(unittest.defaultTestLoader.loadTestsFromTestCase(Tests))
        raise SystemExit(0 if result.wasSuccessful() else 1)
        return
    print('Exact restricted opening; no complete-fight win probability.')
    print('D60 all; A HP398/DE113/regen13.8; L458/116/14.4; S444/120/14.8.')
    print('Independent own turns/reactions; A/L/S order34/32/28; no initial ward.')
    print('Rounds:',args.rounds,'Moon Cut self-cleanse:',args.moon_cut,
          'Counter mastery:',args.counter_mastery)
    for enemy in ('attack','ward'):
        for opponents in ('L','S','pair'):
            for policy in ('alternating-ordinary','alternating','focus','control-focus'):
                result = metrics(opening(policy,opponents,enemy,args.rounds,args.moon_cut,
                                         args.counter_mastery),opponents)
                assert abs(result.pop('probability')-1)<1e-8
                print(enemy,opponents,policy,' '.join(f'{k}={v:.6f}' for k,v in result.items()))


if __name__ == '__main__':
    main()
