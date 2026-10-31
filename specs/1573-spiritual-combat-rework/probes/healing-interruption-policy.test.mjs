import test from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import * as model from './healing-tactics-model.mjs';
import {simulateTactics} from './healing-tactics-probe.mjs';
import {price} from './reaction-model.mjs';

// All energies/prices are exact hundredths of DE. Expectations are hand-derived for D130.
const view=overrides=>Object.freeze({canAct:true,hasReaction:true,energy:11520,
  interruptCost:2880,strongCost:5760,largeCost:8640,mode:'instant',phase:'cast',...overrides});
const noDice={damage:()=>0,reaction:()=>{assert.fail('A skipped interruption must not request reaction dice');}};
const decision=(v,p)=>model.chooseInterrupt?.(v,p);

test('reserve gates include equality and never borrow future regeneration',()=>{
  for(const [policy,energy,want] of [
    ['reserve_strong',8639,false],['reserve_strong',8640,true],
    ['reserve_large',11519,false],['reserve_large',11520,true],
    ['release_reserve_large',11519,false],['release_reserve_large',11520,true],
    ['always',2879,false],['always',2880,true]
  ])assert.deepEqual(decision(view({energy,regen:99999}),policy),
    {attempt:want,reason:want?'attempt':energy<2880?'no_energy':'reserve'},`${policy}:${energy}`);
});

test('selector reports the first applicable reason and reads no hidden opponent or RNG',()=>{
  const cases=[
    [{canAct:false,hasReaction:false,energy:0},'never','inactive'],
    [{hasReaction:false,energy:0},'never','no_reaction'],
    [{energy:2879},'never','no_energy'],[{},'never','policy_never'],
    [{mode:'long',phase:'cast',energy:2880},'release_reserve_large','long_start'],
    [{mode:'long',phase:'finish',energy:11519},'release_reserve_large','reserve'],
    [{mode:'long',phase:'finish'},'release_reserve_large','attempt'],
    [{mode:'long',phase:'cast'},'reserve_large','attempt'],
    [{phase:'cast'},'release_reserve_large','attempt']
  ];
  for(const [overrides,policy,reason] of cases){
    const input=view(overrides),before={...input};
    const guarded=new Proxy(input,{get(target,key){
      assert.ok(Object.hasOwn(target,key),`Unexpected decision input: ${String(key)}`);return target[key];
    }});
    assert.deepEqual(decision(guarded,policy),{attempt:reason==='attempt',reason});assert.deepEqual(input,before);
  }
});

test('never and reserve refusal spend neither energy nor reaction and request no dice',()=>{
  for(const [interruptPolicy,energy,reason] of [['never',11520,'policy_never'],['reserve_large',11519,'reserve']]){
    const e=model.createEncounter({order:[1,0],interruptPolicy,reply:'none'}),[a,h]=e.actors;
    a.energy=energy;h.hp=400;model.stepEncounter(e,noDice);
    assert.equal(a.energy,energy);assert.equal(a.reaction,1);assert.equal(e.counts.interruptions,0);
    assert.equal(e.counts.interruptDecisions,1);assert.equal(e.counts[`interruptDecision_${reason}`],1);
    assert.equal(e.counts.byActor.A[`interruptDecision_${reason}`],1);assert.equal(e.counts.byActor.B.interruptDecisions,0);
    assert.equal(e.counts.healingMains,1);assert.ok(h.hp>400);
  }
});

test('permitted interruption pays current personal costs before reaction dice',()=>{
  const e=model.createEncounter({order:[1,0],interruptPolicy:'reserve_large',reply:'guard'}),[a,h]=e.actors;
  a.energy=11520;h.hp=400;const healerEnergy=h.energy;let calls=0;
  model.stepEncounter(e,{damage:()=>0,reaction:()=>{
    calls++;assert.equal(a.energy,8640);assert.equal(a.reaction,0);
    assert.equal(h.energy,healerEnergy-4320-1440);assert.equal(h.reaction,0);assert.equal(h.used,true);
    assert.equal(e.counts.interruptDecision_attempt,1);return[1,20];
  }});
  assert.equal(calls,1);assert.equal(e.counts.interruptPaid,2880);assert.equal(e.counts.responsePaid,1440);
  assert.equal(e.counts.interruptions,1);assert.equal(e.counts.cancelled,0);assert.ok(h.hp>400);
});

test('fresh applyHeal admission refuses an opponent depleted after the pure decision',()=>{
  const operation=model.createOperation(),calculate=operation.calculate;
  const e=model.createEncounter({order:[1,0],interruptPolicy:'reserve_large',operation}),[a,h]=e.actors;
  a.energy=11520;h.hp=400;
  operation.calculate=(...args)=>{a.energy=2879;return calculate(...args);};
  model.stepEncounter(e,noDice);
  assert.equal(e.counts.interruptDecision_attempt,1);assert.equal(e.counts.interruptions,0);
  assert.equal(a.energy,2879);assert.equal(a.reaction,1);assert.ok(h.hp>400);
});

test('revived participants can spend an existing reaction even when their current Main remains lost',()=>{
  const e=model.createEncounter({order:[1,0],interruptPolicy:'always',reply:'none'}),[a,h]=e.actors;
  a.hp=100;a.collapsed=false;a.active=true;a.collapsedThisTurn=true;a.reaction=1;a.energy=2880;h.hp=400;
  let calls=0;model.stepEncounter(e,{damage:()=>0,reaction:()=>{calls++;return[1,20];}});
  assert.equal(calls,1);assert.equal(a.reaction,0);assert.equal(a.energy,0);
  assert.equal(e.counts.interruptDecision_attempt,1);assert.equal(e.counts.interruptions,1);
  assert.equal(a.collapsedThisTurn,true);assert.equal(a.used,false);
});

test('release waits through a prepaid long start and a personal refresh before interrupting its finish',()=>{
  const e=model.createEncounter({mode:'long',order:[1,0],harmony:'both36',interruptPolicy:'release_reserve_large',reply:'none'});
  const [a,h]=e.actors;a.energy=11520;a.reaction=0;h.hp=400;
  // Fresh no-reaction takes precedence over phase. A personal boundary then restores the reaction.
  model.stepEncounter(e,noDice);assert.equal(e.counts.interruptDecision_no_reaction,1);
  assert.equal(h.pending.target,'B');assert.equal(h.pending.due,2);assert.equal(e.counts.healPaid,8640);
  assert.equal(h.hp,400);assert.equal(a.reaction,0);model.stepEncounter(e,noDice);assert.equal(a.reaction,1);
  let calls=0;model.stepEncounter(e,{damage:()=>0,reaction:()=>{calls++;assert.equal(a.reaction,0);return[20,1];}});
  assert.equal(calls,1);assert.equal(e.counts.healingMains,2);assert.equal(e.counts.finishAttempts,1);
  assert.equal(e.counts.healPaid,8640);assert.equal(e.counts.interruptDecision_attempt,1);
  assert.equal(e.counts.cancelled,1);assert.equal(h.pending,null);assert.equal(e.counts.finishes,0);
});

test('release long-start refusal preserves reaction and prepaid pending until ordinary completion',()=>{
  const e=model.createEncounter({mode:'long',order:[1,0],harmony:'both36',interruptPolicy:'release_reserve_large',reply:'none'});
  const [a,h]=e.actors;a.energy=11520;h.hp=400;
  model.stepEncounter(e,noDice);assert.equal(e.counts.interruptDecision_long_start,1);
  assert.equal(a.energy,11520);assert.equal(a.reaction,1);assert.equal(h.pending.due,2);
  assert.equal(e.counts.healPaid,8640);model.stepEncounter(e,noDice);
  model.stepEncounter(e,{damage:()=>0,reaction:()=>[1,20]});
  assert.equal(e.counts.interruptDecisions,2);assert.equal(e.counts.finishes,1);
  assert.equal(e.counts.healPaid,8640);assert.equal(h.pending,null);assert.ok(h.hp>400);
});

test('opt-in reason accounting covers live eligibility and excludes non-Heal or explicitly disabled windows',()=>{
  for(const [mutate,reason] of [
    [a=>{a.collapsed=true;a.hp=1;},'inactive'],[a=>{a.inEncounter=false;},'inactive'],
    [a=>{a.reaction=0;},'no_reaction'],[a=>{a.energy=2879;a.regen=99999;},'no_energy']
  ]){
    const e=model.createEncounter({order:[1,0],interruptPolicy:'always'});e.actors[1].hp=400;mutate(e.actors[0]);
    model.stepEncounter(e,noDice);assert.equal(e.counts[`interruptDecision_${reason}`],1);
    assert.equal(e.counts.interruptDecisions,1);assert.equal(e.counts.interruptions,0);
  }
  for(const options of [{interrupt:false},{}, {mode:'instant'}]){
    const e=model.createEncounter({order:[1,0],interruptPolicy:'always',...options});
    if(options.interrupt===false)e.actors[1].hp=400;
    model.stepEncounter(e,noDice);assert.equal(e.counts.interruptDecisions,0);
  }
});

test('policy option rejects unknown policies and non-duel scope',()=>{
  for(const options of [{interruptPolicy:'typo'},{kind:'group',interruptPolicy:'never'},
    {kind:'support',interruptPolicy:'always',interrupt:false}])assert.throws(()=>model.createEncounter(options));
  assert.throws(()=>model.chooseInterrupt?.(view(),'typo'));
});

test('default T123 controls keep byte-identical results and legacy counter schema',()=>{
  const rows=[];
  for(const order of [[0,1],[1,0]])for(const mode of ['instant','long','adaptive'])for(const interrupt of [true,false]){
    const result=simulateTactics({n:3,horizon:20,order,mode,interrupt});delete result.reuse;rows.push(result);
    assert.equal(Object.hasOwn(result.counts,'interruptDecisions'),false);
  }
  assert.equal(createHash('sha256').update(JSON.stringify(rows)).digest('hex'),
    'bb1fb7aeefa417362301b1d00611c5994903ad3567dc5faa26b590ab4a80b03c');
});
