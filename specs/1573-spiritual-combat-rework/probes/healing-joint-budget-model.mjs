// T125 abstract budget forecast. Actual admission, payment and effects stay in the shared encounter machine.
import assert from 'node:assert/strict';

export const jointPolicies=Object.freeze(['never','always','joint']);
const kinds=['ordinary','strong','large','suppress'],phases=['instant','cast','finish'];
const marks=Object.freeze([{amount:0,remaining:0},{amount:25,remaining:1},
  {amount:50,remaining:1},{amount:50,remaining:2}].map(Object.freeze));
const markIndex=mark=>{
  const index=marks.findIndex(m=>m.amount===mark.amount&&m.remaining===mark.remaining);
  assert.ok(index>=0,'Unsupported forecast mark');return index;
};
const age=mark=>mark.remaining<=1?marks[0]:marks[2];

/** One supported suppressor; an active target includes its current opportunity in the countdown. */
export function normalizeJointMarks(source,started,active) {
  assert.ok(source.length<=1,'Joint planner supports one suppression source only');
  if(!source.length)return marks[0];
  const m=source[0],remaining=m.expires-started+Number(active);
  if(remaining<=0)return marks[0];
  return marks[markIndex({amount:m.amount,remaining})];
}

/** Match the accepted same-source mark replacement contract, including weak-under-strong refusal. */
export function forecastJointMark(mark,quality) {
  markIndex(mark);assert.ok(Number.isInteger(quality)&&quality>=0&&quality<=3);
  if(quality===0||quality===1&&mark.amount===50)return mark;
  return quality===1?marks[1]:marks[3];
}

export function forecastJointPhase(phase,cancelled) {
  assert.ok(phases.includes(phase));
  return phase==='instant'?'instant':phase==='cast'&&!cancelled?'finish':'cast';
}

function frozen(value){
  if(value&&typeof value==='object'){for(const item of Object.values(value))frozen(item);Object.freeze(value);}
  return value;
}
const gcd=(a,b)=>b?gcd(b,a%b):a;

/** Owns immutable curves for one operation; every profile dependency participates in its identity. */
export function createJointBudgetCache() {
  const curves=new Map(),stats={computed:0,hits:0,statesComputed:0,valueHits:0,lookups:0,preparationMs:0};
  return {stats,prepare(input){
    const profile={maxEnergy:input.maxEnergy,regen:input.regen,recoverGain:input.recoverGain,
      costs:Object.fromEntries([...kinds,'interrupt'].map(k=>[k,input.costs[k]])),
      attacks:Object.fromEntries(kinds.map(k=>[k,input.attacks[k].map(row=>({quality:row.quality,
        probability:row.probability,damage:row.damage})).sort((a,b)=>a.quality-b.quality)])),
      healing:Object.fromEntries(['instant','long'].map(k=>[k,Object.fromEntries([0,25,50].map(s=>[s,input.healing[k][s]]))])),
      interruptSuccess:input.interruptSuccess};
    assert.ok(Number.isSafeInteger(profile.maxEnergy)&&profile.maxEnergy>0);
    for(const n of [profile.regen,profile.recoverGain,...Object.values(profile.costs)])assert.ok(Number.isSafeInteger(n)&&n>=0);
    assert.ok(profile.costs.ordinary+profile.recoverGain>0);
    assert.ok(Number.isFinite(profile.interruptSuccess)&&profile.interruptSuccess>=0&&profile.interruptSuccess<=1);
    for(const rows of Object.values(profile.attacks)){
      assert.ok(rows.length&&new Set(rows.map(r=>r.quality)).size===rows.length);
      for(const r of rows)assert.ok(Number.isInteger(r.quality)&&r.quality>=0&&r.quality<=3&&
        Number.isFinite(r.damage)&&r.damage>=0&&Number.isFinite(r.probability)&&r.probability>0);
      assert.ok(Math.abs(rows.reduce((sum,r)=>sum+r.probability,0)-1)<1e-12);
    }
    for(const values of Object.values(profile.healing))for(const n of Object.values(values))assert.ok(Number.isFinite(n)&&n>=0);
    const lambda=profile.attacks.ordinary.reduce((sum,r)=>sum+r.damage*r.probability,0)/
      (profile.costs.ordinary+profile.recoverGain);
    const key=JSON.stringify({version:'joint_pressure_v1',horizon:3,credit:'ordinary/(ordinaryCost+recoverGain)',
      forecast:'last-intent-initial-instant;nominal-hp;guard-minus5;personal-end;one-source',lambda,profile});
    if(curves.has(key)){stats.hits++;return curves.get(key);}
    const started=performance.now();frozen(profile);
    const values=new Map();
    function value(energy,mark,phase,h){
      assert.ok(Number.isSafeInteger(energy)&&energy>=0&&energy<=profile.maxEnergy&&Number.isInteger(h)&&h>=0&&h<=3);
      const mi=markIndex(mark);assert.ok(phases.includes(phase));
      if(h===0)return lambda*energy;
      const id=`${h}|${energy}|${mi}|${phase}`;
      if(values.has(id)){stats.valueHits++;return values.get(id).score;}
      let best=null;
      for(const kind of [...kinds,'recover']){
        const cost=kind==='recover'?0:profile.costs[kind];if(energy<cost)continue;
        const afterMain=kind==='recover'?Math.min(profile.maxEnergy,energy+profile.recoverGain):energy-cost;
        const afterEnd=Math.min(profile.maxEnergy,afterMain+profile.regen);
        const outcomes=kind==='recover'?[{quality:0,probability:1,damage:0}]:profile.attacks[kind];
        let score=0;
        for(const out of outcomes){
          const nextMark=kind==='suppress'?forecastJointMark(mark,out.quality):mark;
          // Own begin refreshes the reaction before this future window. No current reaction is carried across it.
          score+=out.probability*(out.damage+window(afterEnd,nextMark,phase,h-1,Infinity).score);
        }
        if(!best||score>best.score||score===best.score&&cost<best.cost)best={kind,score,cost};
      }
      values.set(id,Object.freeze(best));stats.statesComputed++;return best.score;
    }
    function window(energy,mark,phase,h,cap){
      const nominal=phase==='cast'?0:profile.healing[phase==='finish'?'long':'instant'][mark.amount];
      const healing=Math.min(cap,nominal),aged=age(mark),next=forecastJointPhase(phase,false);
      const skipScore=-healing+value(energy,aged,next,h);
      if(energy<profile.costs.interrupt)return {attempt:false,score:skipScore,skipScore};
      const paid=energy-profile.costs.interrupt,q=profile.interruptSuccess;
      const attemptScore=(1-q)*(-healing+value(paid,aged,next,h))+
        q*value(paid,aged,forecastJointPhase(phase,true),h);
      const attempt=attemptScore>skipScore;
      return {attempt,score:attempt?attemptScore:skipScore,skipScore,attemptScore};
    }
    // Prepare the full reachable energy lattice, not a tree on each live turn. Off-lattice diagnostic inputs stay exact.
    const unit=[profile.maxEnergy,profile.regen,profile.recoverGain,...Object.values(profile.costs)].reduce(gcd);
    for(let h=1;h<=3;h++)for(let energy=0;energy<=profile.maxEnergy;energy+=unit)
      for(const mark of marks)for(const phase of phases)value(energy,mark,phase,h);
    const curve=Object.freeze({profile,lambda,
      value(view,h=3){stats.lookups++;return value(view.energy,view.mark,view.phase,h);},
      main(view){
        stats.lookups++;value(view.energy,view.mark,view.phase,3);
        const out=values.get(`3|${view.energy}|${markIndex(view.mark)}|${view.phase}`);return {kind:out.kind,score:out.score};
      },
      reaction(view){stats.lookups++;return window(view.energy,view.mark,view.phase,3,view.missingHp);}
    });
    curves.set(key,curve);stats.computed++;stats.preparationMs+=performance.now()-started;return curve;
  }};
}

/** Identical for all three reaction ablations, including the same hypothetical future joint decisions. */
export function chooseJointMain(curve,view) {
  if(!view.canAct)return {kind:'none',score:null};
  return curve.main(view);
}

/** A forecast never grants live permission; the shared action implementation rechecks it. */
export function chooseJointReaction(curve,view,policy) {
  assert.ok(jointPolicies.includes(policy),'Unknown joint policy');
  const skip=reason=>({attempt:false,reason});
  if(!view.canAct)return skip('inactive');
  if(!view.hasReaction)return skip('no_reaction');
  if(view.energy<curve.profile.costs.interrupt)return skip('no_energy');
  if(policy==='never')return skip('policy_never');
  if(policy==='always')return {attempt:true,reason:'attempt'};
  assert.ok(Number.isFinite(view.missingHp)&&view.missingHp>=0);
  const {attempt,skipScore,attemptScore}=curve.reaction(view);
  return {attempt,reason:attempt?'attempt':'forecast',skipScore,attemptScore};
}
