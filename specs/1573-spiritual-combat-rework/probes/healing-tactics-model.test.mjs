import test from 'node:test';
import assert from 'node:assert/strict';
import {chooseAttack,chooseTarget,choosePatient,teamDefeated,createEncounter,stepEncounter,runTrial,createOperation} from './healing-tactics-model.mjs';
import {actor,begin,end,damage,applyHeal} from './healing-modes-model.mjs';
import {price} from './reaction-model.mjs';
import {mark} from './suppression-model.mjs';

const miss={damage:()=>0,reaction:()=>[1,20]};
const critical={damage:()=>399,reaction:()=>[1,20]};
test('attack choice is pure, pays now and reserves 16P after known capped regeneration',()=>{
  const a=actor(130,'A'),t=actor(130,'B');
  a.energy=price(a,24);a.regen=price(a,8);
  const v={self:a,target:t},copy=structuredClone(v);
  assert.equal(chooseAttack(v,'greedy'),'large');
  assert.equal(chooseAttack(v,'reserve'),'strong');
  a.energy=price(a,8)-1;a.regen=price(a,36);
  assert.equal(chooseAttack(v,'reserve'),'recover');
  a.energy=price(a,12);a.regen=price(a,16);
  assert.equal(chooseAttack(v,'suppress_reserve'),'suppress');
  mark(t,'A',2);assert.equal(chooseAttack(v,'suppress_reserve'),'ordinary');
  t.marks[0].expires=t.started;assert.equal(chooseAttack(v,'suppress_reserve'),'suppress');
  assert.equal(copy.self.hp,a.hp);assert.equal(a.used,false);
  a.p={...a.p,max:price(a,12)};assert.equal(chooseAttack(v,'reserve'),'recover');
});
test('targets and triage use current individual state; long continuation retains patient',()=>{
  const e=createEncounter({kind:'support',helperAccess:true,targetPolicy:'helper'}),[a,h,b]=e.actors;
  assert.equal(chooseTarget(e.actors,a.id,'patient').id,h.id);
  assert.equal(chooseTarget(e.actors,a.id,'helper').id,b.id);
  b.hp=200;assert.equal(chooseTarget(e.actors,a.id,'switch').id,b.id);
  b.attackable=false;assert.equal(chooseTarget(e.actors,a.id,'helper').id,h.id);
  damage(b,b.hp);assert.equal(choosePatient(e.actors,h.id).id,b.id);
  h.pending={target:h.id};assert.equal(choosePatient(e.actors,h.id).id,h.id);
  assert.equal(teamDefeated(e.actors,h.team),false);damage(h,h.hp);
  assert.equal(teamDefeated(e.actors,h.team),true);
});
test('collapsed positive HP never acts or regenerates, while a living actor below return threshold acts',()=>{
  const e=createEncounter({kind:'support',order:[2,0,1],interrupt:false});
  const b=e.actors[2];damage(b,b.hp);b.hp=1;b.energy=0;b.reaction=0;mark(b,'enemy',2);
  stepEncounter(e,miss);assert.equal(b.energy,0);assert.equal(b.reaction,0);assert.equal(b.used,false);
  assert.equal(b.started,1);assert.equal(b.marks[0].expires,2);
  const f=createEncounter({order:[0,1],attackPolicy:'fixed_ordinary',interrupt:false});
  f.actors[0].hp=1;stepEncounter(f,miss);assert.equal(f.actors[0].used,true);
});
test('ally healing revives without resetting energy reaction or a skipped Main; pending target survives target switching',()=>{
  const e=createEncounter({kind:'support',order:[2,1,0],mode:'instant',healingPercent:50,interrupt:false});
  const [,h,b]=e.actors;damage(b,b.hp);b.energy=0;b.reaction=0;
  stepEncounter(e,miss);stepEncounter(e,miss);
  assert.equal(b.collapsed,false);assert.equal(b.energy,0);assert.equal(b.reaction,0);assert.equal(b.used,false);
  assert.equal(e.counts.revivals,1);assert.equal(e.counts.allyHeals,1);assert.equal(h.energy,h.p.max-price(h,12)+h.regen);
  const f=createEncounter({kind:'support',order:[1,0,2],mode:'long',interrupt:false,targetPolicy:'helper'});
  f.actors[2].hp=400;stepEncounter(f,miss);assert.equal(f.actors[1].pending.target,'C');
  stepEncounter(f,miss);stepEncounter(f,miss);stepEncounter(f,miss);
  assert.equal(f.counts.finishes,1);assert.equal(f.actors[2].hp,Math.min(f.actors[2].maxHp,400+f.counts.restored));
});
test('one interrupter is chosen and personally charged before dice; collapsed and poor participants cannot intervene',()=>{
  const e=createEncounter({kind:'group',order:[2,0,1,3],mode:'instant',reply:'none'});
  const [a,b,h]=e.actors;h.hp=500;a.energy=price(a,8);b.energy=price(b,8)+1;
  let seen=false;stepEncounter(e,{damage:()=>0,reaction:()=>{
    seen=true;assert.equal(b.energy,1);assert.equal(b.reaction,0);assert.equal(a.reaction,1);return[20,1];
  }});
  assert.ok(seen);assert.equal(e.counts.interruptions,1);assert.equal(e.counts.cancelled,1);
  assert.equal(e.counts.byActor.B.interruptPaid,price(b,8));assert.equal(e.counts.byActor.A.interruptPaid,0);
});
test('target-specific resistance and explicit operation reuse preserve fresh live state',()=>{
  const operation=createOperation(),e=createEncounter({kind:'support',order:[0,1,2],targetPolicy:'patient',resistance:25,operation,interrupt:false});
  const initial=e.actors[1].hp;stepEncounter(e,critical);const protectedLoss=initial-e.actors[1].hp;
  const f=createEncounter({kind:'support',order:[0,1,2],targetPolicy:'helper',resistance:25,operation,interrupt:false});
  stepEncounter(f,critical);assert.ok(f.actors[2].maxHp-f.actors[2].hp>protectedLoss);
  const computations=operation.stats.damageComputed;createEncounter({resistance:25,operation});
  assert.equal(operation.stats.damageComputed,computations);
  createEncounter({resistance:26,operation});assert.ok(operation.stats.damageComputed>computations);
  assert.notEqual(e.actors[0],f.actors[0]);assert.notEqual(e.actors[1].marks,f.actors[1].marks);
});
test('total group defeat differs from first patient fall and unreachable helper censors group endpoint',()=>{
  const result=runTrial({kind:'support',helperAccess:false,mode:'instant',interrupt:false,horizon:4,
    setup:e=>{e.actors[1].hp=1;e.actors[1].energy=0;e.actors[2].energy=0;},dice:critical});
  assert.equal(result.endpoints.firstPatientFall.kind,'event');assert.equal(result.endpoints.firstPatientFall.time,1);
  assert.equal(result.endpoints.total.kind,'horizon');assert.equal(result.winner,null);
  const e=createEncounter({kind:'support'});damage(e.actors[1],e.actors[1].hp);
  assert.equal(teamDefeated(e.actors,1),false);damage(e.actors[2],e.actors[2].hp);assert.equal(teamDefeated(e.actors,1),true);
});
test('source collapse cancels pending; missed personal opportunities age effects with no extra Main after revival',()=>{
  const e=createEncounter({kind:'support',order:[1,0,2],mode:'long',interrupt:false});
  stepEncounter(e,miss);const h=e.actors[1];assert.ok(h.pending);damage(h,h.hp);assert.equal(h.pending,null);
  mark(h,'A',2);stepEncounter(e,miss);stepEncounter(e,miss);const energy=h.energy;
  stepEncounter(e,miss);assert.equal(h.energy,energy);assert.equal(h.started,2);assert.equal(h.used,false);
  const healer=actor(130,'isolated');
  begin(healer);applyHeal(healer,h,{id:'rescue1'});end(healer);assert.equal(h.collapsed,true);
  begin(healer);applyHeal(healer,h,{id:'rescue2'});end(healer);
  assert.equal(h.collapsed,false);assert.equal(h.used,false);assert.equal(h.active,false);
});
test('attack Main and current energy are consumed before damage RNG',()=>{
  const e=createEncounter({order:[0,1],attackPolicy:'fixed_large',interrupt:false}),a=e.actors[0],energy=a.energy;
  stepEncounter(e,{damage:()=>{assert.equal(a.used,true);assert.equal(a.energy,energy-price(a,24));return 0;},reaction:()=>[1,20]});
});
test('inaccessible support and accessible patient-only control share first-fall trajectory under identical dice',()=>{
  const options={kind:'support',attackPolicy:'fixed_large',targetPolicy:'patient',horizon:8,interrupt:false,seed:42};
  const a=runTrial({...options,helperAccess:false}),b=runTrial({...options,helperAccess:true});
  assert.deepEqual(a.endpoints.firstPatientFall,b.endpoints.firstPatientFall);
});
