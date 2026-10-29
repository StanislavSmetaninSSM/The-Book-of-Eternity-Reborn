// #1573 T071: exploratory personal-clock/effect model. No production imports.
import assert from 'node:assert/strict';
import { profile, quality, round } from './healing-counters.mjs';

// Immutable numerical fixtures, built once; never reconstruct game objects per trial.
const fixtures=[0,50,130,210,1000].map(D=>{
  const p=profile(D);
  const numerator=22n*BigInt(200+3*Math.min(40+D,2*p.X))*BigInt(50+D);
  p.healing=[0,25,50].map(s=>round(numerator*BigInt(100-s),1000000n));
  p.suppressCost=24*(50+D);
  return Object.freeze(p);
});
export function profileFor(D) {
  const p=fixtures.find(p=>p.D===D);assert.ok(p,'D must be an explicit fixture');return p;
}
export function damageTable(p,B) {
  const values=profile(p.D,[B]).attacks[0].values;
  return values.map((damage,i)=>Object.freeze({damage,quality:quality(Math.floor(i/20)+1,i%20+1,p.X)}));
}
export function makeActor(D,id,highHarmony=false) {
  const p=profileFor(D);
  return {id,p,hp:p.hp,energy:p.max,regen:highHarmony?p.costs[3]:p.regen,
    marks:[],started:0,active:false,used:false};
}
export function begin(a) {assert.equal(a.active,false);a.started++;a.active=true;a.used=false;}
export function end(a) {
  assert.equal(a.active,true);
  a.energy=Math.min(a.p.max,a.energy+a.regen);
  // All end-of-opportunity effects would resolve before this expiry boundary.
  a.marks=a.marks.filter(m=>m.expires>a.started);a.active=false;
}
function spend(a,cost) {
  if(!a.active || a.used || a.hp<=0 || a.energy<cost)return false;
  a.energy-=cost;a.used=true;return true;
}
export function mark(target,source,quality) {
  if(quality===0)return;
  const amount=quality===1?25:50, duration=quality===1?1:2;
  const old=target.marks.find(m=>m.source===source);
  if(old && old.amount>amount)return;
  if(old){old.amount=amount;old.expires=target.started+duration;}
  else target.marks.push({source,amount,expires:target.started+duration});
}
export function strength(a) {let value=0;for(const m of a.marks)value=Math.max(value,m.amount);return value;}
export function remaining(a) {
  const s=strength(a);
  return a.marks.reduce((n,m)=>m.amount===s?Math.max(n,m.expires-a.started+1):n,0);
}
export function healAmount(a) {return Math.min(a.p.hp-a.hp,a.p.healing[strength(a)/25]);}
export function heal(a) {
  if(a.hp>=a.p.hp || !spend(a,a.p.costs[3]))return false;
  a.hp+=healAmount(a);return true;
}
export function cleanse(a,target) {
  if(target.marks.length===0 || !spend(a,a.p.costs[0]))return false;
  target.marks=[];return true;
}
export function recover(a) {
  if(!spend(a,0))return false;
  a.energy=Math.min(a.p.max,a.energy+a.p.gain);return true;
}
export function attack(a,target,outcome,kind,absorbHpDamage=false) {
  const cost=kind==='ordinary'?a.p.costs[0]:a.p.suppressCost;
  if(!spend(a,cost))return false;
  target.hp-=absorbHpDamage?0:outcome.damage;
  if(kind==='suppress' && target.hp>0)mark(target,a.id,outcome.quality);
  return true;
}
export function sourceDefeated(a) {a.hp=0;}
export function endBattle(a) {a.marks=[];}
