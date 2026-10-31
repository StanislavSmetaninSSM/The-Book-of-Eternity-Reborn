// T128 opt-in numerical effects. Shares actor, payment, healing math and turn ownership.
import assert from 'node:assert/strict';
import {price} from './reaction-model.mjs';
import {healingSnapshot,healingValue} from './healing-modes-model.mjs';

export const periodicProfiles=Object.freeze([1,2,3,4,5].map(level=>Object.freeze({
  level,directB:6+2*level,directK:4+2*level,hotB:2+2*level,hotK:5+3*level,
  attackB:18+2*level,attackK:7+level,suppression:15+15*level
})));
const profile=level=>{const p=periodicProfiles[level-1];assert.ok(p,'Unknown periodic level');return p;};
const eligible=a=>a.inEncounter&&!a.collapsed&&a.hp>0&&a.active&&!a.used&&!a.collapsedThisTurn;

function restore(t,potential,revive){
  const was=t.collapsed,restored=Math.max(0,Math.min(t.maxHp-t.hp,potential));t.hp+=restored;
  if(revive&&t.collapsed&&t.hp>=Math.max(1,Math.floor(t.maxHp/10)))t.collapsed=false;
  return {restored,revived:was&&!t.collapsed};
}
/** One paid Main; HoT freezes outgoing inputs, while each later tick checks the recipient freshly. */
export function applyPeriodicHeal(a,t,{mode='direct',level=5,calculate=healingValue,revive=false}={}) {
  assert.ok(['direct','hot'].includes(mode));const p=profile(level),cost=price(a,mode==='direct'?p.directK:p.hotK);
  if(!eligible(a)||!t.inEncounter||t.collapsed&&!revive||mode==='direct'&&t.hp>=t.maxHp||a.energy<cost)
    return {accepted:false,paid:0,restored:0};
  a.energy-=cost;a.used=true;a.pending=null;
  if(mode==='direct'){
    const potential=calculate(a,t,p.directB);return {accepted:true,paid:cost,potential,...restore(t,potential,revive)};
  }
  t.hots??=[];const art='periodic-heal-v1',snapshot=healingSnapshot(a,3*p.hotB,{divisor:3});
  t.hots=t.hots.filter(h=>h.source!==a.id||h.art!==art);
  t.hots.push({source:a.id,art,target:t.id,snapshot,remaining:3,next:t.started+1,revive});
  return {accepted:true,paid:cost,restored:0};
}
/** Resolve strongest outgoing family once at this target boundary; all due streams age. */
export function tickPeriodic(t,calculate=healingValue) {
  const due=(t.hots??[]).filter(h=>h.next<=t.started&&h.remaining>0);
  if(!due.length||!t.inEncounter)return [];
  const winner=due.reduce((a,b)=>b.snapshot.numerator*a.snapshot.denominator>
    a.snapshot.numerator*b.snapshot.denominator?b:a);
  const result=due.map(h=>{
    h.remaining--;h.next=t.started+1;
    if(h!==winner)return {source:h.source,applied:false,potential:0,restored:0,revived:false};
    const potential=t.collapsed&&!h.revive?0:calculate(null,t,0,{snapshot:h.snapshot});
    return {source:h.source,applied:true,potential,...restore(t,potential,h.revive)};
  });
  t.hots=t.hots.filter(h=>h.remaining>0);return result;
}
/** A hit has exactly one independent save; a successful save preserves the old mark verbatim. */
export function applyPeriodicMark(t,source,quality,{level=5,save,effectResistance=0}={}) {
  assert.ok(Number.isFinite(effectResistance)&&effectResistance>=0&&effectResistance<=100);
  if(quality===0)return false;
  assert.ok(Number.isFinite(save)&&save>=0&&save<100,'Explicit independent d100 percentile required');
  if(save<effectResistance)return false;
  const p=profile(level),amount=quality===1?p.suppression/2:p.suppression,duration=quality===1?1:2,art='periodic-suppression-v1';
  const old=t.marks.find(m=>m.source===source&&m.art===art);
  if(old&&old.amount>amount)return false;
  if(old){old.amount=amount;old.expires=t.started+duration;}
  else t.marks.push({source,art,amount,expires:t.started+duration});
  return true;
}
/** Policies receive visible current state only. Affordability is rechecked by the common action owner. */
export function choosePeriodicMain({self,target,mode='combined',cleansePolicy='never',offensive=false}) {
  if(cleansePolicy==='first'&&target.marks.length&&self.energy>=price(self,8))return 'cleanse';
  const missing=target.hp<target.maxHp,own=(target.hots??[]).some(h=>h.source===self.id&&h.remaining>0);
  if(mode==='direct')return missing?'direct':'recover';
  if(mode==='hot')return own?'recover':'hot';
  if(missing&&target.hp<=.35*target.maxHp)return 'direct';
  if(!own)return 'hot';
  if(offensive&&target.hp>.65*target.maxHp)return 'attack';
  return missing?'direct':'recover';
}
export function choosePeriodicAttack({target,policy='ordinary',level=5}) {
  if(policy==='ordinary')return 'ordinary';
  const full=target.marks.filter(m=>m.amount>=profile(level).suppression);
  return policy==='suppress_always'||!full.length||full.every(m=>m.expires<=target.started+1)?'suppress':'ordinary';
}
