import test from 'node:test';
import assert from 'node:assert/strict';
import { simulateModes, scenarios, chooseMode } from './healing-modes-probe.mjs';
import { actor } from './healing-modes-model.mjs';

test('adaptive mode uses current HP/energy and known damage ceiling, never future dice',()=>{
  const a=actor(130,'a');assert.equal(chooseMode(a,'adaptive',300),'long');
  a.hp=300;assert.equal(chooseMode(a,'adaptive',300),'instant');
  a.hp=500;a.energy=5000;assert.equal(chooseMode(a,'adaptive',300),'instant');
  a.pending={due:1};assert.equal(chooseMode(a,'adaptive',300),'long');
});
test('new runner deterministic and reports actual stages, payments and complete outcomes',()=>{
  const opts={D:130,mode:'long',harmony:'both36',n:40,horizon:2000};
  const a=simulateModes(opts),b=simulateModes(opts);assert.deepEqual(a,b);
  assert.equal(a.censored,0);assert.equal(a.winA,1);assert.ok(a.counts.starts>0&&a.counts.finishes>0);
  assert.equal(a.counts.healingMains,a.counts.starts+a.counts.finishes);
  assert.equal(a.counts.healPaid,a.counts.starts*8640);assert.equal(a.counts.interruption,0);
});
test('constant immediate and long policies are both actually exercised, adaptive uses both',()=>{
  for(const mode of ['instant','long','adaptive']){
    const a=simulateModes({mode,D:130,harmony:'both36',n:100});
    if(mode!=='instant')assert.ok(a.counts.starts>0);
    if(mode!=='long')assert.ok(a.counts.instant>0);
    if(mode==='instant')assert.equal(a.counts.starts,0);
    if(mode==='long')assert.equal(a.counts.instant,0);
  }
});
test('suppression cleansing and protection/counter actions occur with legal energy budgets',()=>{
  for(const reply of ['guard','counter']){
    const a=simulateModes({mode:'long',style:'suppress_reserve',reply,policy:'cleanse_safe',harmony:'both36',n:100});
    assert.ok(a.counts.interruption>0&&a.counts.cleanse>0);
    assert.ok(a.counts[reply==='guard'?'guard':'counterAttempt']>0);assert.ok(a.counts.abandoned>0);
  }
});
test('helper cleanses without taking patient Main, all three positions are observed',()=>{
  const a=simulateModes({mode:'long',style:'suppress_reserve',reply:'guard',harmony:'both36',order:[0,2,1],n:100});
  assert.ok(a.counts.helperCleanse>0&&a.counts.finishes>0);assert.equal(a.counts.cleanse,0);
});
test('forced unsuccessful interventions are still paid, and horizon is explicit censoring',()=>{
  const a=simulateModes({mode:'long',style:'reserve',reply:'guard',forceInterruptFailure:true,harmony:'both36',n:100});
  assert.ok(a.counts.interruption>0&&a.counts.interruptPaid>0);assert.equal(a.counts.cancelled,0);
  const b=simulateModes({n:10,horizon:1});assert.equal(b.censored,10);assert.equal(b.meanObserved,1);
  assert.equal(b.winA+b.winB,0);
});
test('matrix contains required scales/orders/policies and separate reinforced patient sensitivity',()=>{
  const main=scenarios('main'),support=scenarios('support'),sensitivity=scenarios('sensitivity');
  assert.deepEqual([...new Set(main.map(x=>x.D))].sort((a,b)=>a-b),[0,130,1000]);
  for(const mode of ['instant','long','adaptive'])for(const order of ['0,1','1,0'])
    assert.ok(main.some(x=>x.mode===mode&&x.order.join()===order&&x.style==='ordinary'&&x.harmony==='both36'));
  assert.equal(new Set(support.map(x=>x.order.join())).size,6);
  assert.ok(sensitivity.some(x=>x.healingPercent===50&&x.resistance===25));
  assert.ok(sensitivity.some(x=>x.forceInterruptFailure));
  assert.ok(sensitivity.some(x=>x.policy==='mix50'));assert.ok(sensitivity.some(x=>x.willDelta===-5));
});
