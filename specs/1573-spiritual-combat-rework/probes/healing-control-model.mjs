// T129 experiment effects only; encounter, HP, payment units and clocks remain shared.
import assert from 'node:assert/strict';
import {price,checkedQuality} from './reaction-model.mjs';

export const controlProfiles=Object.freeze({
  seal:Object.freeze({B:0,K:8,partial:1,full:3,family:'seal',check:'Will against Will'}),
  stun:Object.freeze({B:14,K:16,partial:0,full:1,family:'stun',check:'Delivered attack against defense'}),
  stun_long:Object.freeze({B:14,K:24,partial:1,full:2,family:'stun',check:'Delivered attack against defense'})
});
export function controlFamily(profile){assert.ok(controlProfiles[profile],'Unknown control profile');return controlProfiles[profile].family;}
/** Pressure and window share the same damage fallback; the window reads only current HP and active HoT. */
export function chooseControlCast({target,profile='none',policy='pressure'}){
  assert.ok(['pressure','window'].includes(policy),'Unknown control policy');
  if(profile==='none')return false;
  if(controlsFor(target,controlFamily(profile)).length)return false;
  return policy==='pressure'||target.hp<=.65*target.maxHp||
    target.hp<target.maxHp&&!(target.hots??[]).some(h=>h.remaining>0);
}
/** Current target authority is fresh, including an effect's final active opportunity. */
export function controlsFor(a,family){
  if(!a.inEncounter)return [];
  return (a.controls??[]).filter(c=>(a.active?c.expires>=a.started:c.expires>a.started)&&(!family||c.family===family));
}
export function controlRemaining(a,c){return Math.max(0,c.expires-a.started+(a.active?1:0));}
/** Will's check modifier is distinct from Favor, which controls the natural-critical threshold. */
export function controlWillModifier(a){
  return Math.min(20+a.p.D/2,Math.max(0,a.willStat??a.p.X))+Math.floor(.8*a.p.D);
}
export function blocksControl(a,action){
  if(!a.controls?.length)return false;
  const effects=controlsFor(a);
  return effects.some(c=>c.family==='stun')||['heal','cleanse'].includes(action)&&effects.some(c=>c.family==='seal');
}
/** The caller owns pre-roll payment; an eligible hit gets one independent post-hit save. */
export function applyControl(t,source,profile,quality,{save,resistance=0}={}){
  const p=controlProfiles[profile];assert.ok(p,'Unknown control profile');
  assert.ok(Number.isInteger(quality)&&quality>=0&&quality<=3);
  assert.ok(Number.isFinite(resistance)&&resistance>=0&&resistance<=100);
  const duration=quality===0?0:quality===1?p.partial:p.full;
  if(!t.inEncounter||!duration)return false;
  assert.ok(Number.isFinite(save)&&save>=0&&save<100,'Explicit independent control-save required');
  if(save<resistance)return false;
  t.controls??=[];const art=`control-v1:${profile}`,expires=t.started+duration;
  const old=t.controls.find(c=>c.source===source.id&&c.art===art&&c.target===t.id);
  if(old&&old.expires>expires)return false;
  const data={source:source.id,art,target:t.id,family:p.family,expires,sourceWillModifier:controlWillModifier(source)};
  if(old)Object.assign(old,data);else t.controls.push(data);return true;
}
export function expireControls(a){
  if(a.controls)a.controls=a.inEncounter?a.controls.filter(c=>c.expires>a.started):[];
}
/** Self seal-break or a free ally's release spends one Main; success removes exactly the selected bond. */
export function breakControl(a,t,c,{roll=()=>{throw Error('Explicit break dice required');}}={}){
  const out={accepted:false,succeeded:false,paid:0,ally:a.id!==t.id};
  if(!a.inEncounter||a.collapsed||a.hp<=0||!a.active||a.used||a.collapsedThisTurn||!t.inEncounter||a.team!==t.team||
    blocksControl(a,'main')||!controlsFor(t).includes(c)||a.id===t.id&&c.family!=='seal'||
    a.id!==t.id&&controlsFor(a).length||a.energy<price(a,8))return out;
  a.energy-=price(a,8);a.used=true;a.pending=null;out.accepted=true;out.paid=price(a,8);
  const [x,y]=roll();out.succeeded=checkedQuality(x,y,a.p.X,controlWillModifier(a)-c.sourceWillModifier)>=1;
  if(out.succeeded)t.controls=t.controls.filter(effect=>effect!==c);return out;
}
/** Choose only from current bonds/resources; no future dice or enemy energy enters this policy. */
export function chooseControlMain({self,offensive=false,ally=null,helperControlRelease=false}){
  if(blocksControl(self,'main'))return {kind:'blocked'};
  const seal=controlsFor(self,'seal').sort((a,b)=>b.expires-a.expires)[0];
  if(seal){
    if(controlRemaining(self,seal)<=1)return {kind:offensive?'attack':'recover'};
    return self.energy>=price(self,8)?{kind:'break',target:self,control:seal}:{kind:'recover'};
  }
  if(helperControlRelease&&ally){const effect=controlsFor(ally).sort((a,b)=>b.expires-a.expires)[0];
    if(effect)return self.energy>=price(self,8)?{kind:'break',target:ally,control:effect}:{kind:'recover'};}
  return {kind:'continue'};
}
