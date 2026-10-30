// T116: standalone investigative subset of accepted T114. No production imports.
import assert from 'node:assert/strict';
import {makeActor,begin,end,healAmount} from './suppression-model.mjs';

const criticalThresholds=new Map();
export function checkedQuality(a,b,F,delta=0) {
  let threshold=criticalThresholds.get(F);
  if(threshold===undefined){threshold=20-Number(5n*BigInt(F)/(100n+BigInt(F)));criticalThresholds.set(F,threshold);}
  if(a>=threshold)return 3;
  if(a===1)return 0;
  const margin=a-b+delta;
  return margin>=10?3:margin>=5?2:margin>=0?1:0;
}
export const price=(a,k)=>2*k*(50+a.p.D); // Exact hundredths of DE, one source scale.
export function createActor(D,id,{regenP=null,willDelta=0}={}) {
  const a=makeActor(D,id);
  const P=1+D/50;
  return {...a,reaction:1,collapsedThisTurn:false,willDelta,
    harmony:regenP===null?a.p.H:5*(regenP-4)*P,
    regen:regenP===null?a.regen:price(a,regenP)};
}
export function beginTurn(a) {
  begin(a);a.collapsedThisTurn=a.hp<=0;
  if(!a.collapsedThisTurn)a.reaction=1;
}
export function endTurn(a) {
  const regen=a.regen;
  if(a.collapsedThisTurn||a.hp<=0)a.regen=0;
  end(a);a.regen=regen;
}
export function react(a,costP,root) {
  assert.ok(costP>=0 && root instanceof Set);
  const cost=price(a,costP);
  if(a.hp<=0||a.collapsedThisTurn&&a.active||a.reaction<1||a.energy<cost||root.has(a.id))return false;
  a.energy-=cost;a.reaction=0;root.add(a.id);return true;
}
export function attemptHeal(caster,opponent,{reply='none',roll=()=>{throw Error('Explicit dice required');}}={}) {
  assert.ok(['none','guard','counter'].includes(reply));
  const out={accepted:false,attempted:false,cancelled:false,defended:false,counterAttempted:false,countered:false,
    restored:0,noInterruptEnergy:false,noInterruptReaction:false};
  if(!caster.active||caster.used||caster.collapsedThisTurn||caster.hp<=0||caster.hp>=caster.p.hp||caster.energy<caster.p.costs[3])return out;
  caster.energy-=caster.p.costs[3];caster.used=true;out.accepted=true;
  if(opponent){
    out.noInterruptEnergy=opponent.energy<price(opponent,8);
    out.noInterruptReaction=opponent.reaction<1;
    const root=new Set();
    if(react(opponent,8,root)){
      out.attempted=true;
      // All response decisions and costs precede their own hidden dice.
      if(reply==='guard')out.defended=react(caster,4,root);
      if(reply==='counter'&&react(caster,8,root)){
        out.counterAttempted=true;const [a,b]=roll();
        out.countered=checkedQuality(a,b,caster.p.X,caster.willDelta-opponent.willDelta)>=2;
      }
      if(!out.countered){
        const [a,b]=roll();
        out.cancelled=checkedQuality(a,b,opponent.p.X,opponent.willDelta-caster.willDelta-(out.defended?5:0))>=2;
      }
    }
  }
  if(!out.cancelled){out.restored=healAmount(caster);caster.hp+=out.restored;}
  return out;
}
