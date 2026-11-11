#!/usr/bin/env python3
"""Planning-only X calibration: exact opening and optional seeded full fights.

No production/game imports or writes. Reuses IX's personal-stat class and the
shared opposed-d20 quality kernel. --rounds 2 is an opening comparison, NOT a
full-fight win rate. Order A/L/S is the visible 34/32/28 fixture. Everyone
starts healthy, fully rested, reachable and without barriers; no IX refill is
implied. HP0 is a numerical stop, not surrender or the trap's trigger.

Included: Edge m40, its +40% hilt/Heart bonus; m40 Golden-head Shackles;
Counter m25 (or --counter-mastery 1), only after a reachable full miss with
the same still-available reaction and a separate 12P payment/attack roll;
Lucian m35 lunar B28/B36 slash, Sense accuracy/Might force, enhanced m25
Moon Cut cleansing (12P, +10G25; --free-break disables it); Saref m25 B28
spatial strike, Will accuracy/Might force, or ordinary m30 B20 Will barrier.
Every actor owns Main/DE/reaction. Lucian is D55, the others D60.
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
items, mark, other arts, surrender or trap are included. Each opponent has
his own defense reaction. Lucian's interception spends his reaction before
his passive defense is rolled; Saref keeps his own defense if not intercepted.
The MC-only measured policy uses m25 self-Bell (8P, +25% Heart, B20 Will),
ordinary Edge and prioritizes guarding Lucian. It casts Bell when HP<80%,
no barrier remains, and at least one other Main intervened since the last
Bell. This is a policy choice, not a cooldown or an invented art restriction.
MC Saref dispels a Bell with >100 visible reserve using Main/8P, W+5G20
against its frozen creator Will. A may guard the effect using the same
reaction. At <=100, Saref follows his attack/ward policy. No guaranteed
removal or extra spatial attack follows the dispel Main. MC --duels N runs
to HP0 or --max-rounds and counts unresolved outcomes in the denominator.
These are equal-subset policy comparisons, not optimal or full-loadout odds.
--intercept-policy conditional is an enemy-decision sensitivity: Lucian
intercepts only if he has a barrier, Saref is below half HP, or Saref is
wounded and Lucian would survive the declared full noncritical strike.
Otherwise Saref keeps his own ordinary defense. No policy sees future dice.
"""

import argparse
from collections import Counter, defaultdict
from dataclasses import dataclass, replace
from functools import lru_cache
import math
import random
import statistics
from pathlib import Path
import runpy
import unittest

_ix = runpy.run_path(str(Path(__file__).with_name('ix-champions.py')))
Profile, quality = _ix['Profile'], _ix['quality']
PROFILES = {
    'A': Profile(60, 31, 31, 37, 27, 27, 25, 25),
    'L': Profile(55, 30, 32, 28, 36, 25, 28, 28),
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
    bell_round: int = -2


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


def full_strength(source,target,base,mastery):
    """Return full noncritical damage after resistance, before ward/HP rounding."""
    p, t = PROFILES[source], PROFILES[target]
    resistance = t.integrity / (100 + 3 * t.integrity)
    if source == 'L' and target == 'A':
        resistance += .15
    strength = base * g(mastery) * (1 + .03 * p.might) * p.p
    strength *= (1.4 if source == 'A' and base != 14 else 1) * (1 - resistance)
    return strength


def hp_loss(incoming,ward):
    """Subtract unrounded absorption, then round HP loss half upward once."""
    return math.floor(max(0,incoming-ward)+.5)


def strike_distribution(state, source, target, base, mastery, guarded):
    p,t = PROFILES[source],PROFILES[target]
    accuracy = p.might if source == 'A' else p.sense if source == 'L' else p.will
    delta = p.modifier(accuracy) - t.modifier(t.sense) - (5 if guarded else 0)
    strength = full_strength(source,target,base,mastery)
    critical = 1.5 + .5 * p.favor / (100 + p.favor)
    for q, probability in outcomes(source, target, delta):
        incoming = strength * (0, .5, 1, critical)[q]
        actor = state[INDEX[target]]
        absorbed = min(actor.ward, incoming)
        hp = max(0, actor.hp - hp_loss(incoming,actor.ward))
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
    if moon_cut and role == 'L' and state[1].de >= cost('L', 12):
        state = spend(state, role, cost('L', 12))
        delta = p.modifier(p.will) + 10*g(25) - strength
    else:
        delta = p.modifier(max(p.might, p.will)) - strength
    chance = sum(prob for q, prob in outcomes(role, 'A', delta) if q)
    yield change(state, role, bound=0), chance
    yield state, 1-chance


def direct_strike(state, role, target, base, mastery, main=True,guard_l=False,
                  intercept_policy='always'):
    intercepted = False
    if role == 'A' and target == 'S':
        l = state[1]
        loss = hp_loss(full_strength('A','L',base,mastery),l.ward)
        s = state[2]
        willing = (intercept_policy == 'always' or l.ward > 0 or s.hp < PROFILES['S'].hp/2
                   or (s.hp < PROFILES['S'].hp and l.hp > loss))
        if willing and l.hp and l.reaction and not l.bound and l.de >= cost('L', 4):
            state = spend(state, 'L', cost('L', 4), reaction=True)
            target, intercepted = 'L', True
    guarded = False
    if role == 'A':
        if not intercepted:
            state, guarded = guard(state, target)
    elif (role == 'L' and guard_l) or (role == 'S' and not guard_l) or not state[1].hp or state[1].bound or not state[2].hp or state[2].bound:
        state, guarded = guard(state, 'A')
    # The hilt occupies the main hand; the full Mask outfit's glove is absent.
    yield from strike_distribution(state, role, target, base, mastery, guarded)


def main_distribution(state, role, policy, round_index, enemy_policy, moon_cut,
                      counter_mastery=25,intercept_policy='always'):
    own = state[INDEX[role]]
    if own.bound:
        yield from break_distribution(state, role, moon_cut)
        return
    if role == 'A' and policy == 'control-focus' and round_index == 0 and state[1].hp:
        yield from shackle_distribution(state, 'L')
        return
    if role == 'A' and policy == 'measured' and own.hp < .8*PROFILES['A'].hp and not own.ward and round_index > own.bell_round+1 and own.de >= cost('A',8):
        state = spend(state,'A',cost('A',8))
        reserve = 20*g(25)*(1+.03*PROFILES['A'].will)*PROFILES['A'].p*1.25
        yield change(state,'A',ward=round(reserve,10),ward_turns=4,bell_round=round_index),1
        return
    if role == 'S' and 'dispel' in enemy_policy and state[0].ward > 100 and own.de >= cost('S',8):
        state = spend(state,'S',cost('S',8))
        state,guarded = guard(state,'A')
        a,p = PROFILES['A'],PROFILES['S']
        delta = p.modifier(p.will)+5*g(20)-a.modifier(a.will)-5*guarded
        chance = sum(prob for q,prob in outcomes('S','A',delta) if q)
        yield change(state,'A',ward=0,ward_turns=0),chance
        yield state,1-chance
        return
    if role == 'S' and enemy_policy.startswith('ward'):
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
        strong = policy not in ('alternating-ordinary','measured')
        mastery = 40
    else:
        target, strong, mastery = 'A', role == 'L', 35 if role == 'L' else 25
    base, coefficient = (36, 16) if strong and own.de >= cost(role, 16) else (28, 8)
    if own.de < cost(role, coefficient):
        base, coefficient, mastery = 14, 0, 1
    state = spend(state, role, cost(role, coefficient))
    for result, probability, outcome in direct_strike(state,role,target,base,mastery,
                                                     guard_l=policy=='measured',intercept_policy=intercept_policy):
        a = result[0]
        if role != 'A' and outcome == 0 and a.hp and a.reaction and a.de >= cost('A',12):
            result = spend(result,'A',cost('A',12),reaction=True)
            for counter, chance, _ in direct_strike(result,'A',role,28,counter_mastery,
                                                    main=False,intercept_policy=intercept_policy):
                yield counter, probability*chance
        else:
            yield result, probability


def opening(policy, opponents='pair', enemy_policy='attack', rounds=2, moon_cut=True,
            counter_mastery=25,intercept_policy='always'):
    state = initial_state(opponents)
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
                                                        enemy_policy, moon_cut,counter_mastery,intercept_policy):
                    result = finish_turn(result,role)
                    following[result] += probability*branch
            distribution = following
    return distribution


def initial_state(opponents):
    return tuple(State(p.hp if opponents == 'pair' or role in ('A', opponents) else 0,
                       p.energy) for role,p in PROFILES.items())


def finish_turn(state,role):
    if state[INDEX[role]].hp:
        actor,p = state[INDEX[role]],PROFILES[role]
        turns = max(0,actor.ward_turns-1)
        return change(state,role,de=round(min(p.energy,actor.de+p.regen),2),
                      bound=max(0,actor.bound-1),ward_turns=turns,
                      ward=actor.ward if turns else 0)
    return state


def simulate(duels,seed,policy,opponents,enemy_policy,max_rounds,moon_cut,counter_mastery,
             intercept_policy='always'):
    """Sample to actual HP0 endpoints; fixed round/role quantiles across cases."""
    rng = random.Random(seed)
    wins,rounds,losses = Counter(),[],[]
    for _ in range(duels):
        quantiles = [rng.random() for _ in range(max_rounds*3)]
        state = initial_state(opponents)
        winner = 'timeout'
        for round_index in range(max_rounds):
            for role in ('A','L','S'):
                if not state[INDEX[role]].hp:
                    continue
                state = change(state,role,reaction=True)
                quantile = quantiles[round_index*3+INDEX[role]]
                total = 0
                for result,probability in main_distribution(state,role,policy,round_index,
                                                            enemy_policy,moon_cut,counter_mastery,intercept_policy):
                    total += probability
                    if quantile < total+1e-12:
                        state = finish_turn(result,role)
                        break
                else:
                    raise AssertionError('Incomplete probability distribution')
                if not state[0].hp or not(state[1].hp or state[2].hp):
                    winner = 'A' if state[0].hp else 'foes'
                    break
            if winner != 'timeout':
                rounds.append(round_index+1)
                break
        wins[winner] += 1
        losses.append(PROFILES['A'].hp-state[0].hp)
    return dict(wins),statistics.median(rounds) if rounds else None,statistics.mean(losses)


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
        self.assertEqual([p.hp for p in PROFILES.values()], [398,437,444])
        self.assertEqual([p.energy for p in PROFILES.values()], [113,112,120])

    def test_shackle_will_not_sense_and_natural_break(self):
        state = tuple(State(p.hp,p.energy) for p in PROFILES.values())
        actual = list(shackle_distribution(state,'L'))
        self.assertAlmostEqual(sum(p for s,p in actual if s[1].bound==2), .925)
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
        self.assertTrue(all(not s[1].reaction and s[1].de==103.6 for s,p in branches))
        self.assertAlmostEqual(sum(p for s,p in branches if s[1].hp==0),.44)

    def test_counter_has_one_payment_and_same_reaction(self):
        state = tuple(State(p.hp,p.energy) for p in PROFILES.values())
        branches = list(main_distribution(state,'L','focus',0,'attack',False))
        self.assertTrue(any(s[0].de==86.6 and not s[0].reaction for s,p in branches))
        self.assertTrue(all(s[0].de in (86.6,113) for s,p in branches))

    def test_no_glove_piercing_with_hilt_and_moon_cut_is_paid(self):
        state = initial_state('pair')
        ordinary = list(strike_distribution(state,'A','L',28,40,False))
        bound = list(strike_distribution(change(state,'L',bound=2),'A','L',28,40,False))
        self.assertEqual([s[1].hp for s,p,q in ordinary],[s[1].hp for s,p,q in bound])
        cleaned = list(break_distribution(change(state,'L',bound=2),'L',True))
        self.assertAlmostEqual(cleaned[0][1],.1525)
        self.assertTrue(all(s[1].de==86.8 for s,p in cleaned))

    def test_victorious_main_regenerates_and_dispel_uses_sarefs_main(self):
        state = change(initial_state('pair'),'A',de=50)
        state = change(change(state,'L',hp=0),'S',hp=0)
        self.assertEqual(finish_turn(state,'A')[0].de,63.8)
        shielded = change(initial_state('pair'),'A',ward=223.1730769231,ward_turns=3)
        branches = list(main_distribution(shielded,'S','measured',0,'attack-dispel',True))
        self.assertTrue(all(s[0].hp==398 and s[2].de==102.4 for s,p in branches))

    def test_sample_endpoints_and_determinism(self):
        args = (10,53210,'focus','pair','attack',30,True,25)
        a,b = simulate(*args),simulate(*args)
        self.assertEqual(a,b)
        self.assertEqual(sum(a[0].values()),10)

    def test_conditional_interception_leaves_fresh_saref_own_defense(self):
        state = initial_state('pair')
        branches = list(main_distribution(state,'A','focus',0,'attack',True,25,'conditional'))
        self.assertTrue(all(s[1].hp==437 and s[1].reaction and s[1].de==112 for s,p in branches))
        self.assertTrue(all(not s[2].reaction and s[2].de==111.2 for s,p in branches))
        self.assertAlmostEqual(sum(p for s,p in branches if s[2].hp==0),.265)

    def test_free_b14_interception_uses_actual_rounded_hp_loss(self):
        state = change(change(initial_state('pair'),'S',hp=443),'L',hp=50)
        branches = list(direct_strike(state,'A','S',14,1,intercept_policy='conditional'))
        self.assertTrue(all(not s[1].reaction and s[2].hp==443 for s,p,q in branches))
        self.assertTrue(all(s[1].hp==1 for s,p,q in branches if q==2))
        boundary = change(state,'L',hp=49)
        branches = list(direct_strike(boundary,'A','S',14,1,intercept_policy='conditional'))
        self.assertTrue(all(s[1].reaction and s[1].hp==49 for s,p,q in branches))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rounds',type=int,choices=(1,2,3),default=2)
    parser.add_argument('--free-break',action='store_true')
    parser.add_argument('--counter-mastery',type=int,choices=(1,25),default=25)
    parser.add_argument('--duels',type=int,default=0)
    parser.add_argument('--seed',type=int,default=5321011)
    parser.add_argument('--max-rounds',type=int,default=60)
    parser.add_argument('--intercept-policy',choices=('always','conditional'),default='always')
    parser.add_argument('--self-test',action='store_true')
    args = parser.parse_args()
    if args.self_test:
        result = unittest.TextTestRunner().run(unittest.defaultTestLoader.loadTestsFromTestCase(Tests))
        raise SystemExit(0 if result.wasSuccessful() else 1)
        return
    print('Exact restricted opening; no complete-fight win probability.')
    print('A D60 HP398/DE113/regen13.8; L D55 437/112/14; S D60 444/120/14.8.')
    print('Independent own turns/reactions; A/L/S order34/32/28; no initial ward.')
    moon_cut = not args.free_break
    print('Rounds:',args.rounds,'Enhanced m25 Moon Cut self-cleanse:',moon_cut,
          'Counter mastery:',args.counter_mastery,'intercept:',args.intercept_policy)
    for enemy in ('attack','ward'):
        for opponents in ('L','S','pair'):
            for policy in ('alternating-ordinary','alternating','focus','control-focus'):
                result = metrics(opening(policy,opponents,enemy,args.rounds,moon_cut,
                                         args.counter_mastery,args.intercept_policy),opponents)
                assert abs(result.pop('probability')-1)<1e-8
                print(enemy,opponents,policy,' '.join(f'{k}={v:.6f}' for k,v in result.items()))
    if args.duels:
        print('SAMPLED HP0 endpoints; all timeouts retained; no conscious surrender/trap model.')
        print('duels',args.duels,'seed',args.seed,'max_rounds',args.max_rounds)
        cases = [('L','attack','focus'),('S','attack','focus')]
        cases += [('pair',enemy,policy) for enemy in ('attack-dispel','ward-dispel')
                  for policy in ('alternating','focus','control-focus','measured')]
        for opponents,enemy,policy in cases:
            wins,median,loss = simulate(args.duels,args.seed,policy,opponents,enemy,
                                       args.max_rounds,moon_cut,args.counter_mastery,args.intercept_policy)
            print('MC',enemy,opponents,policy,wins,'median_resolved_rounds',median,
                  'A_mean_HP_loss',round(loss,3))


if __name__ == '__main__':
    main()
