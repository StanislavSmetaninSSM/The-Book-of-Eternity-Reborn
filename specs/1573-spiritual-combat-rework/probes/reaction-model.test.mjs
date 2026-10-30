// T116: behavioural guards for the exploratory model, not production combat.
import test from 'node:test';
import assert from 'node:assert/strict';
import { createActor, beginTurn, endTurn, react, checkedQuality, attemptHeal } from './reaction-model.mjs';
import { mark, cleanse, strength } from './suppression-model.mjs';
import { quality } from './healing-counters.mjs';
import { simulate, scenarios } from './reaction-probe.mjs';

test('one common reaction pays once; only own start resets it without stacking',()=>{
  const a=createActor(0,'a'),b=createActor(0,'b'),root=new Set();const e=a.energy;
  assert.equal(react(a,4,root),true);assert.equal(a.energy,e-400);assert.equal(a.reaction,0);
  assert.equal(react(a,8,new Set()),false);beginTurn(b);endTurn(b);assert.equal(a.reaction,0);
  beginTurn(a);assert.equal(a.reaction,1);endTurn(a);assert.equal(a.reaction,1);
  assert.equal(react(a,4,root),false); // A root retains its actor identities even across boundary misuse.
});
test('unaffordable reaction cannot borrow end regeneration or consume its slot',()=>{
  const a=createActor(0,'a');a.energy=799;
  assert.equal(react(a,8,new Set()),false);assert.equal(a.energy,799);assert.equal(a.reaction,1);
});
test('collapsed entry neither refreshes reaction nor gives end regeneration after healing',()=>{
  const a=createActor(0,'a');a.hp=0;a.energy=0;a.reaction=0;beginTurn(a);
  assert.equal(react(a,4,new Set()),false);a.hp=a.p.hp;endTurn(a);
  assert.equal(a.reaction,0);assert.equal(a.energy,0);
  beginTurn(a);assert.equal(a.reaction,1);endTurn(a);assert.equal(a.energy,a.regen);
});
test('full Will success interrupts paid heal; partial success does not',()=>{
  for(const [dice,cancel] of [[[10,5],true],[[9,5],false]]){
    const a=createActor(0,'a'),b=createActor(0,'b');b.hp=1;beginTurn(b);
    const ea=a.energy,eb=b.energy;const out=attemptHeal(b,a,{roll:()=>dice});
    assert.equal(out.cancelled,cancel);assert.equal(a.energy,ea-800);assert.equal(b.energy,eb-2800);
    assert.equal(b.used,true);assert.equal(b.hp,cancel?1:1+b.p.heal);assert.equal(a.reaction,0);
  }
});
test('active defense costs 4P before dice and reduces the interrupt margin by five',()=>{
  const a=createActor(0,'a'),b=createActor(0,'b');b.hp=1;beginTurn(b);
  const eb=b.energy;
  const out=attemptHeal(b,a,{reply:'guard',roll:()=>{
    assert.equal(b.reaction,0);assert.equal(b.energy,eb-2800-400);return [10,5];
  }});
  assert.equal(out.cancelled,false);assert.equal(out.defended,true);assert.equal(b.hp,1+b.p.heal);
});
test('natural critical priority survives active defense and a failed paid interruption',()=>{
  assert.equal(checkedQuality(20,20,7,-100),3);assert.equal(checkedQuality(1,1,7,100),0);
  const a=createActor(0,'a'),b=createActor(0,'b');b.hp=1;beginTurn(b);
  const out=attemptHeal(b,a,{reply:'guard',roll:()=>[20,20]});
  assert.equal(out.cancelled,true);assert.equal(out.defended,true);
});
test('counter-interruption consumes both reactions and prevents the original interrupt',()=>{
  const a=createActor(0,'a'),b=createActor(0,'b');b.hp=1;beginTurn(b);let rolls=0;
  const out=attemptHeal(b,a,{reply:'counter',roll:()=>{rolls++;return [10,5];}});
  assert.equal(out.countered,true);assert.equal(out.cancelled,false);assert.equal(rolls,1);
  assert.equal(a.reaction,0);assert.equal(b.reaction,0);assert.equal(a.energy,a.p.max-800);
  assert.equal(b.energy,b.p.max-2800-800);assert.equal(b.hp,1+b.p.heal);
});
test('failed counter-interruption does not refund and original interrupt still resolves',()=>{
  const a=createActor(0,'a'),b=createActor(0,'b');b.hp=1;beginTurn(b);let rolls=0;
  const out=attemptHeal(b,a,{reply:'counter',roll:()=>++rolls===1?[2,20]:[10,5]});
  assert.equal(out.countered,false);assert.equal(out.cancelled,true);assert.equal(rolls,2);
  assert.equal(b.energy,b.p.max-3600);assert.equal(a.energy,a.p.max-800);
});
test('preflight refusal consumes no resource or reaction and reads no future roll',()=>{
  const a=createActor(0,'a'),b=createActor(0,'b');beginTurn(b);const eb=b.energy;
  const out=attemptHeal(b,a,{roll:()=>assert.fail('no admitted application')});
  assert.equal(out.accepted,false);assert.equal(b.used,false);assert.equal(b.energy,eb);
  assert.equal(a.reaction,1);
});
test('one Main: cleansing restores neither a heal opportunity nor a spent reaction',()=>{
  const a=createActor(130,'a');a.hp=1;mark(a,'enemy',2);beginTurn(a);react(a,4,new Set());
  assert.equal(cleanse(a,a),true);assert.equal(strength(a),0);assert.equal(a.reaction,0);
  assert.equal(attemptHeal(a,null).accepted,false);assert.equal(a.hp,1);
});
test('suppression remains after a cancelled heal and applies before the sole rounding',()=>{
  const a=createActor(130,'a'),b=createActor(130,'b');b.hp=1;mark(b,'a',2);beginTurn(b);
  const out=attemptHeal(b,a,{roll:()=>[10,5]});assert.equal(out.cancelled,true);
  assert.equal(strength(b),50);endTurn(b);beginTurn(b);
  attemptHeal(b,null);assert.equal(b.hp,114);
});
test('high Harmony uses an explicit raw H with the same energy maximum',()=>{
  const a=createActor(130,'a',{regenP:36}),b=createActor(130,'b');
  assert.equal(a.harmony,576);assert.equal(a.regen,12960);assert.equal(a.p.max,b.p.max);
});
test('all 400 equal-Will checks match old outcomes, guard changes margin not natural criticals',()=>{
  for(const [F,fullExpected,guardExpected] of [[7,125,65],[62,131,76],[497,155,115]]){
    let full=0,guard=0;
    for(let a=1;a<=20;a++)for(let b=1;b<=20;b++){
      assert.equal(checkedQuality(a,b,F),quality(a,b,F));
      full+=Number(checkedQuality(a,b,F)>=2);guard+=Number(checkedQuality(a,b,F,-5)>=2);
    }
    assert.equal(full,fullExpected);assert.equal(guard,guardExpected);
  }
});
test('runner is reproducible, reports censored cases separately and preserves event counts',()=>{
  const options={D:130,n:12,horizon:2,style:'reserve',reply:'counter'};
  const first=simulate(options);assert.deepEqual(simulate(options),first);
  assert.equal(first.censored,12);assert.equal(first.winA+first.winB,0);
  assert.ok(first.counts.cancelled<=first.counts.interruption);
  assert.ok(first.counts.interruption<=first.counts.heals);
  assert.ok(first.counts.counterSucceeded<=first.counts.counterAttempt);
  const control=simulate({...options,horizon:40,style:'ordinary',policy:'noheal'});
  assert.equal(control.counts.heals,0);assert.equal(control.counts.interruption,0);
  assert.ok(Math.abs(control.winA+control.winB+control.censored/control.n-1)<1e-12);
});
test('matrix includes both personal orders and all six support orders without duplicate scenarios',()=>{
  for(const [mode,count] of [['main',396],['support',96],['sensitivity',120]]){
    const rows=scenarios(mode);assert.equal(rows.length,count);
    assert.equal(new Set(rows.map(r=>JSON.stringify(r))).size,count);
    assert.equal(new Set(rows.map(r=>r.order.join(','))).size,mode==='support'?6:2);
  }
});
