import test from 'node:test';
import assert from 'node:assert/strict';
import * as periodic from './healing-periodic-model.mjs';
import {actor,begin,end,damage,healingCalculator,healingValue} from './healing-modes-model.mjs';
import {createEncounter,stepEncounter,runTrial,createOperation} from './healing-tactics-model.mjs';
import {price} from './reaction-model.mjs';

const options={healingRules:'periodic-v1',periodicMode:'combined',periodicAttack:'suppress_always',level:5,
  effectResistance:0,cleansePolicy:'never',activeGuard:false,interrupt:false};
const miss={damage:()=>0,reaction:()=>{throw Error('Heal cannot be interrupted');},save:()=>99};
const crit={...miss,damage:()=>399};

test('shared calculator keeps exact half percentages and divides before its only rounding',()=>{
  const a=actor(130,'A'),t=actor(130,'T');t.marks=[{amount:22.5}];
  assert.equal(healingValue(a,t,16),128);
  a.flatHealing=30;t.marks=[];
  assert.equal(healingValue(a,t,36,{divisor:3}),134);
});
test('direct and HoT use exact T127 cost and source snapshot with flat once',()=>{
  assert.equal(typeof periodic.applyPeriodicHeal,'function');
  const a=actor(130,'A'),t=actor(130,'T'),calculate=healingCalculator();t.hp=0;t.collapsed=true;
  begin(a);let out=periodic.applyPeriodicHeal(a,t,{mode:'direct',level:5,calculate,revive:true});
  assert.equal(out.restored,165);assert.equal(out.paid,5040);assert.equal(t.collapsed,false);end(a);
  a.flatHealing=30;begin(a);out=periodic.applyPeriodicHeal(a,t,{mode:'hot',level:5,calculate,revive:true});
  assert.equal(out.paid,7200);assert.equal(out.restored,0);assert.equal(t.hots.length,1);
  assert.equal(periodic.tickPeriodic(t,calculate).length,0);a.power=0;a.flatHealing=999;
  begin(t);const tick=periodic.tickPeriodic(t,calculate)[0];assert.equal(tick.potential,134);end(t);
  t.incomingHealingPercent=50;t.marks=[{amount:22.5,expires:10}];begin(t);
  assert.equal(periodic.tickPeriodic(t,calculate)[0].potential,155);end(t);
});
test('HoT refresh costs in full; strongest family ages weak foreign sources and ticks only once',()=>{
  const a=actor(130,'A'),b=actor(130,'B'),t=actor(130,'T'),calculate=healingCalculator();t.hp=1;
  begin(a);periodic.applyPeriodicHeal(a,t,{mode:'hot',level:5,calculate});end(a);
  begin(b);periodic.applyPeriodicHeal(b,t,{mode:'hot',level:1,calculate});end(b);
  begin(a);const prior=a.energy;periodic.applyPeriodicHeal(a,t,{mode:'hot',level:5,calculate});
  assert.equal(prior-a.energy,7200);assert.equal(t.hots.length,2);damage(a,a.hp);
  for(let i=0;i<3;i++){begin(t);const rows=periodic.tickPeriodic(t,calculate);assert.equal(rows.length,2);
    assert.equal(rows.filter(x=>x.applied).length,1);assert.equal(rows.find(x=>x.applied).potential,124);
    assert.equal(periodic.tickPeriodic(t,calculate).length,0);end(t);}
  assert.equal(t.hots.length,0);
});
test('partial II is 22.5, save leaves old mark untouched, weaker same art cannot overwrite',()=>{
  const t=actor(130,'T');
  assert.equal(periodic.applyPeriodicMark(t,'A',1,{level:2,save:99}),true);
  assert.equal(t.marks[0].amount,22.5);assert.equal(t.marks[0].expires,1);
  periodic.applyPeriodicMark(t,'A',3,{level:5,save:99});const old=structuredClone(t.marks);
  assert.equal(periodic.applyPeriodicMark(t,'A',3,{level:5,save:0,effectResistance:25}),false);
  assert.deepEqual(t.marks,old);periodic.applyPeriodicMark(t,'A',1,{level:5,save:99});assert.deepEqual(t.marks,old);
});
test('periodic payments and guard precede dice, misses skip save while crit still saves',()=>{
  const e=createEncounter({...options,activeGuard:true,effectResistance:100}),[a,t]=e.actors;
  stepEncounter(e,{...crit,damage:()=>{assert.equal(a.energy,a.p.max-4320);assert.equal(t.energy,t.p.max-1440);
    assert.equal(t.reaction,0);return 399;},save:()=>{assert.ok(t.hp<t.maxHp);return 0;}});
  assert.equal(e.counts.saveAttempts,1);assert.equal(e.counts.saves,1);assert.equal(t.marks.length,0);
  const f=createEncounter(options);stepEncounter(f,{...miss,save:()=>{throw Error('miss has no save');}});
  assert.equal(f.counts.saveAttempts,0);assert.equal(f.counts.attackPaid,4320);
});
test('guard quality rows retain natural crit and ordinary defense remains free without reaction',()=>{
  const operation=createOperation(),base=operation.table(130,28,0),guard=operation.table(130,28,0,-5);
  assert.equal(base.entries[9*20+5].quality,1);assert.equal(guard.entries[9*20+5].quality,0);
  assert.equal(guard.entries[399].quality,3);assert.equal(guard.entries[399].damage,base.entries[399].damage);
  const n=operation.stats.damageComputed;assert.equal(operation.table(130,28,0,-5),guard);assert.equal(operation.stats.damageComputed,n);
  const e=createEncounter({...options,activeGuard:true});e.actors[1].reaction=0;
  stepEncounter(e,miss);assert.equal(e.counts.guardPaid,0);assert.equal(e.counts.damage,0);
});
test('ticks precede mark expiry and skipped opportunity revival grants no Main reaction or regen',()=>{
  const e=createEncounter({...options,kind:'support',order:[2,0,1]}),[,h,t]=e.actors;
  begin(h);periodic.applyPeriodicHeal(h,t,{mode:'hot',level:5,calculate:e.operation.calculate,revive:true});end(h);
  h.energy=0;h.regen=0;
  damage(t,t.hp);t.energy=0;t.reaction=0;t.marks=[{amount:90,expires:1}];
  stepEncounter(e,miss);assert.equal(t.hp,12);assert.equal(t.collapsed,true);assert.equal(t.marks.length,0);
  stepEncounter(e,miss);stepEncounter(e,miss);stepEncounter(e,miss);
  assert.equal(t.collapsed,false);assert.equal(t.hp,136);assert.equal(t.energy,0);assert.equal(t.reaction,0);assert.equal(t.used,false);
});
test('battle completion clears HoT without rescuing the last collapsed side',()=>{
  const e=createEncounter({...options,order:[0,1]}),[a,t]=e.actors;
  begin(a);periodic.applyPeriodicHeal(a,t,{mode:'hot',level:5,calculate:e.operation.calculate,revive:true});end(a);
  t.hp=1;stepEncounter(e,crit);assert.equal(e.done,true);assert.equal(e.winner,0);assert.equal(t.hp,0);
  assert.equal(t.hots.length,0);assert.equal(t.marks.length,0);
  assert.equal(a.inEncounter,false);assert.equal(t.inEncounter,false);
});
test('direct saves an immediate burst, HoT supplies healing while a later Main recovers',()=>{
  const direct=createEncounter({...options,periodicMode:'direct',order:[1,0]});direct.actors[1].hp=80;
  const full={...miss,damage:()=>15*20+9};
  stepEncounter(direct,miss);assert.equal(direct.actors[1].hp,245);stepEncounter(direct,full);assert.equal(direct.actors[1].hp,19);
  const control=createEncounter({...options,periodicMode:'direct',order:[1,0]});control.actors[1].hp=80;control.actors[1].energy=0;
  stepEncounter(control,miss);stepEncounter(control,full);assert.equal(control.actors[1].hp,0);
  const hot=createEncounter({...options,periodicMode:'hot',periodicAttack:'ordinary',order:[1,0]});hot.actors[1].hp=300;
  stepEncounter(hot,miss);assert.equal(hot.counts.hotTicks,0);stepEncounter(hot,miss);stepEncounter(hot,miss);
  assert.equal(hot.counts.recover,1);assert.equal(hot.counts.hotTicks,1);assert.equal(hot.counts.restored,124);
});
test('a useful HoT frees a later Main for an offensive attack',()=>{
  const e=createEncounter({...options,periodicAttack:'ordinary',defender:'offensive',order:[1,0]});
  e.actors[1].hp=800;stepEncounter(e,miss);stepEncounter(e,miss);
  stepEncounter(e,{...miss,damage:()=>15*20+9});
  assert.equal(e.counts.byActor.B.attacks,1);assert.equal(e.counts.byActor.B.healingMains,1);
  assert.equal(e.counts.byActor.B.hotTicks,1);assert.equal(e.counts.byActor.B.hotRestored,124);assert.equal(e.actors[0].hp,804);
});
test('winning personal turn resolves its due HoT before battle cleanup without starting another actor turn',()=>{
  const e=createEncounter({...options,periodicAttack:'ordinary',defender:'offensive',order:[1,0]}),[enemy,h]=e.actors;
  h.hp=800;stepEncounter(e,miss);stepEncounter(e,miss);enemy.hp=1;
  const enemyStarted=enemy.started,healerEnergy=h.energy;
  stepEncounter(e,{...miss,damage:()=>15*20+9});
  assert.equal(e.done,true);assert.equal(e.winner,1);assert.equal(e.round,2);
  assert.equal(h.hp,924);assert.equal(e.counts.byActor.B.hotTicks,1);assert.equal(e.counts.byActor.B.hotRestored,124);
  assert.equal(h.energy,healerEnergy-price(h,8)+h.regen);assert.equal(enemy.started,enemyStarted);
  assert.equal(h.hots.length,0);assert.equal(stepEncounter(e,crit),false);assert.equal(enemy.started,enemyStarted);
});
test('cleansing consumes one paid Main before healing, without immunity or free HP',()=>{
  const e=createEncounter({...options,cleansePolicy:'first',order:[1,0]}),t=e.actors[1];t.hp=200;
  t.marks=[{amount:90,expires:2}];stepEncounter(e,miss);
  assert.equal(e.counts.cleansePaid,2880);assert.equal(e.counts.healingMains,0);assert.equal(t.hp,200);assert.equal(t.marks.length,0);
});
test('shared cache reuses snapshot math but incoming suppression and HP remain fresh',()=>{
  const a=actor(130,'A'),t=actor(130,'T'),calculate=healingCalculator();begin(a);
  periodic.applyPeriodicHeal(a,t,{mode:'hot',level:5,calculate});end(a);t.hp=1;
  begin(t);periodic.tickPeriodic(t,calculate);end(t);const n=calculate.stats.computed;
  begin(t);periodic.tickPeriodic(t,calculate);end(t);assert.equal(calculate.stats.computed,n);assert.ok(calculate.stats.hits>0);
  t.marks=[{amount:37.5,expires:10}];begin(t);assert.equal(periodic.tickPeriodic(t,calculate)[0].potential,77);
  assert.equal(calculate.stats.computed,n+1);
});
test('needed suppression refreshes when the full mark expires at the next target opportunity',()=>{
  const t=actor(130,'T');t.started=1;t.marks=[{amount:90,expires:2}];
  assert.equal(periodic.choosePeriodicAttack({target:t,policy:'suppress_needed',level:5}),'suppress');
  t.marks[0].expires=3;assert.equal(periodic.choosePeriodicAttack({target:t,policy:'suppress_needed',level:5}),'ordinary');
  t.marks[0].amount=45;assert.equal(periodic.choosePeriodicAttack({target:t,policy:'suppress_needed',level:5}),'suppress');
});
test('HP0 restoration requires explicit effect authority and removed targets never receive a tick',()=>{
  const a=actor(130,'A'),t=actor(130,'T');begin(a);
  periodic.applyPeriodicHeal(a,t,{mode:'hot'});end(a);damage(t,t.hp);begin(t);
  assert.equal(periodic.tickPeriodic(t)[0].restored,0);assert.equal(t.hp,0);assert.equal(t.collapsed,true);end(t);
  t.inEncounter=false;begin(t);assert.deepEqual(periodic.tickPeriodic(t),[]);assert.equal(t.hp,0);
});
test('unaffordable applications do not consume Main or create effects; collapsed source cannot cast',()=>{
  const a=actor(130,'A'),t=actor(130,'T');begin(a);a.energy=7199;
  assert.equal(periodic.applyPeriodicHeal(a,t,{mode:'hot'}).accepted,false);assert.equal(a.energy,7199);assert.equal(a.used,false);
  assert.equal(t.hots,undefined);damage(a,a.hp);a.energy=a.p.max;
  assert.equal(periodic.applyPeriodicHeal(a,t,{mode:'direct',revive:true}).accepted,false);
});
test('effect save handles R0, R100 and exact fractional percentile boundaries',()=>{
  const t=actor(130,'T');
  assert.equal(periodic.applyPeriodicMark(t,'A',3,{save:0,effectResistance:0}),true);
  const old=structuredClone(t.marks);assert.equal(periodic.applyPeriodicMark(t,'A',3,{save:99.99,effectResistance:100}),false);
  assert.deepEqual(t.marks,old);t.marks=[];
  assert.equal(periodic.applyPeriodicMark(t,'A',1,{save:22.49,effectResistance:22.5}),false);
  assert.equal(periodic.applyPeriodicMark(t,'A',1,{save:22.5,effectResistance:22.5}),true);
});
test('absorbed hit still saves once and can mark a just-collapsed recipient',()=>{
  const operation=createOperation(),base=operation.table.bind(operation);
  operation.table=(D,B,R,g=0)=>{const t=base(D,B,R,g);return {...t,entries:t.entries.map(x=>({...x,damage:0}))};};
  const e=createEncounter({...options,operation});let saves=0;stepEncounter(e,{...crit,save:()=>{saves++;return 99;}});
  assert.equal(e.counts.damage,0);assert.equal(saves,1);assert.equal(e.actors[1].marks.length,1);
  const f=createEncounter({...options,kind:'support'});f.actors[1].hp=1;stepEncounter(f,crit);
  assert.equal(f.actors[1].collapsed,true);assert.equal(f.actors[1].marks.length,1);assert.equal(f.counts.saveAttempts,1);
});
test('all actor energy and restoration counters conserve actual resources across the trial',()=>{
  let e;const result=runTrial({...options,activeGuard:true,cleansePolicy:'first',horizon:20,seed:1245,setup:x=>{e=x;}});
  for(const a of e.actors){const c=result.counts.byActor[a.id];
    assert.equal(a.energy,a.p.max+c.recoverGained+c.regenGained-c.attackPaid-c.healPaid-c.cleansePaid-c.guardPaid);
    assert.equal(c.restored,c.directRestored+c.hotRestored);assert.equal(c.potentialHealing,c.restored+c.overheal);
  }
});
test('divisor and immutable outgoing snapshot participate in the shared cache key; failed math is not retained',()=>{
  const a=actor(130,'A'),t=actor(130,'T'),calculate=healingCalculator();
  assert.equal(calculate(a,t,36),371);assert.equal(calculate(a,t,36,{divisor:3}),124);
  assert.equal(calculate.stats.computed,2);assert.equal(calculate(a,t,36,{divisor:3}),124);assert.equal(calculate.stats.hits,1);
  assert.throws(()=>calculate(a,t,36,{divisor:0}));assert.equal(calculate.stats.computed,2);
});
