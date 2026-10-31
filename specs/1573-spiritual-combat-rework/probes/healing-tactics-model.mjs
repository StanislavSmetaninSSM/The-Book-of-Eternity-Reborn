// T123 standalone numerical experiment. Accepted formulas remain in the earlier models.
import assert from 'node:assert/strict';
import {rng,seedFor} from './healing-counters.mjs';
import {price,checkedQuality} from './reaction-model.mjs';
import {strength,remaining,mark,cleanse,recover,profileFor} from './suppression-model.mjs';
import {actor,begin,end,damage,applyHeal,replaceMain,healingCalculator,closeEncounter} from './healing-modes-model.mjs';
import {protectedDamageTable,chooseMode,strikeAttack} from './healing-modes-probe.mjs';
import {jointPolicies,createJointBudgetCache,normalizeJointMarks,chooseJointMain,chooseJointReaction} from './healing-joint-budget-model.mjs';
import {periodicProfiles,applyPeriodicHeal,tickPeriodic,applyPeriodicMark,choosePeriodicMain,choosePeriodicAttack} from './healing-periodic-model.mjs';
import {controlProfiles,controlsFor,blocksControl,applyControl,expireControls,breakControl,chooseControlMain,chooseControlCast} from './healing-control-model.mjs';

export const attackProfiles=Object.freeze(Object.fromEntries(Object.entries({
  ordinary:[28,8],strong:[36,16],large:[44,24],suppress:[24,12]
}).map(([key,[B,K]])=>[key,Object.freeze({B,K})])));
export const attackPolicies=Object.freeze(['fixed_ordinary','fixed_strong','fixed_large','fixed_suppress','greedy','reserve','suppress_reserve']);
export const interruptPolicies=Object.freeze(['never','always','reserve_strong','reserve_large','release_reserve_large']);

/** Decide from current self resources and the visible Heal intent, without mutation or dice. */
export function chooseInterrupt(view,policy) {
  assert.ok(interruptPolicies.includes(policy),'Unknown interruption policy');
  const skip=reason=>({attempt:false,reason});
  if(!view.canAct)return skip('inactive');
  if(!view.hasReaction)return skip('no_reaction');
  if(view.energy<view.interruptCost)return skip('no_energy');
  if(policy==='never')return skip('policy_never');
  if(policy==='release_reserve_large'&&view.mode==='long'&&view.phase==='cast')return skip('long_start');
  const reserve=policy==='reserve_strong'?view.strongCost:
    ['reserve_large','release_reserve_large'].includes(policy)?view.largeCost:0;
  if(view.energy-view.interruptCost<reserve)return skip('reserve');
  return {attempt:true,reason:'attempt'};
}

// One measurement owns these immutable DATA, released with the operation. Current permission is never cached.
export function createOperation() {
  const tables=new Map(),calculate=healingCalculator(),stats={damageComputed:0,damageHits:0};
  const jointProfiles=new Map();let jointCache;
  const operation={stats,calculate,table(D,B,resistance,guardDelta=0){
    const key=`T123-damage-v1|${D}|${B}|${resistance}|${guardDelta}`;
    if(tables.has(key)){stats.damageHits++;return tables.get(key);}
    const base=guardDelta===0?protectedDamageTable(D,B,resistance):operation.table(D,B,resistance).entries;
    const byQuality=guardDelta===0?null:Object.fromEntries(base.map(row=>[row.quality,row.damage]));
    const will=profileFor(D).X;
    const entries=Object.freeze(base.map((row,i)=>{
      if(guardDelta===0)return Object.freeze(row);
      const quality=checkedQuality(Math.floor(i/20)+1,i%20+1,will,guardDelta);
      return Object.freeze({quality,damage:byQuality[quality]});
    }));
    const data=Object.freeze({entries,max:Math.max(...entries.map(x=>x.damage))});
    tables.set(key,data);stats.damageComputed++;return data;
  },jointFor(self,target){
    // Complete public fixture inputs to the unchanged numerical formulas. No enemy energy, HP or future choice.
    const inputs={version:'joint_pressure_v1/public-profile-v1',D:self.p.D,maxEnergy:self.p.max,regen:self.regen,
      recoverGain:self.p.gain,costs:Object.fromEntries(Object.entries(attackProfiles).map(([k,p])=>[k,price(self,p.K)])),
      interruptCost:price(self,8),will:self.p.X,willDelta:self.willDelta,targetWillDelta:target.willDelta,
      targetD:target.p.D,power:target.power,flatHealing:target.flatHealing,healingPercent:target.healingPercent,
      incomingHealingPercent:target.incomingHealingPercent,resistance:target.resistance,attackProfiles};
    const key=JSON.stringify(inputs);
    if(jointProfiles.has(key)){jointCache.stats.hits++;return jointProfiles.get(key);}
    jointCache??=createJointBudgetCache();operation.jointStats=jointCache.stats;
    const attacks=Object.fromEntries(Object.entries(attackProfiles).map(([kind,p])=>{
      const rows=operation.table(inputs.D,p.B,inputs.resistance).entries,groups=[];
      for(let quality=0;quality<=3;quality++){
        const selected=rows.filter(row=>row.quality===quality);
        if(selected.length)groups.push({quality,probability:selected.length/400,
          damage:selected.reduce((sum,row)=>sum+row.damage,0)/selected.length});
      }
      return [kind,groups];
    }));
    const publicHealer={p:{D:inputs.targetD},power:inputs.power,flatHealing:inputs.flatHealing,
      healingPercent:inputs.healingPercent,incomingHealingPercent:inputs.incomingHealingPercent};
    const healing=Object.fromEntries([['instant',7],['long',16]].map(([mode,B])=>[mode,
      Object.fromEntries([0,25,50].map(amount=>[amount,calculate(publicHealer,{...publicHealer,marks:amount?[{amount}]:[]},B)]))]));
    let successes=0;
    for(let a=1;a<=20;a++)for(let b=1;b<=20;b++)
      if(checkedQuality(a,b,inputs.will,inputs.willDelta-inputs.targetWillDelta-5)>=2)successes++;
    const curve=jointCache.prepare({maxEnergy:inputs.maxEnergy,regen:inputs.regen,recoverGain:inputs.recoverGain,
      costs:{...inputs.costs,interrupt:inputs.interruptCost},attacks,healing,interruptSuccess:successes/400});
    jointProfiles.set(key,curve);return curve;
  }};
  return operation;
}

/** Select from current visible data only; this function neither mutates state nor receives dice. */
export function chooseAttack({self,target},policy) {
  assert.ok(attackPolicies.includes(policy));
  const affordable=kind=>self.energy>=price(self,attackProfiles[kind].K);
  const reserved=kind=>affordable(kind)&&Math.min(self.p.max,self.energy-price(self,attackProfiles[kind].K)+self.regen)>=price(self,16);
  if(policy.startsWith('fixed_')){const kind=policy.slice(6);return affordable(kind)?kind:'recover';}
  const full=target?.marks.filter(m=>m.amount===50)??[];
  const suppressionNeeded=strength(target??{marks:[]})<50||full.every(m=>m.expires<=target.started);
  if(policy==='suppress_reserve'&&suppressionNeeded&&reserved('suppress'))return 'suppress';
  return ['large','strong','ordinary'].find(policy==='greedy'?affordable:reserved)??'recover';
}

const living=a=>!a.collapsed&&a.hp>0;
const queue=(a,b)=>a.orderIndex-b.orderIndex;
const fraction=(a,b)=>a.hp*b.maxHp-b.hp*a.maxHp;
export function chooseTarget(actors,actorId,policy) {
  assert.ok(['patient','helper','switch'].includes(policy));
  const me=actors.find(a=>a.id===actorId);assert.ok(me);
  const candidates=actors.filter(a=>a.team!==me.team&&a.attackable&&living(a));
  candidates.sort((a,b)=>policy==='switch'?fraction(a,b)||Number(b.role==='healer')-Number(a.role==='healer')||queue(a,b):
    policy==='patient'?Number(b.role==='healer')-Number(a.role==='healer')||queue(a,b):
    Number(a.role==='healer')-Number(b.role==='healer')||queue(a,b));
  return candidates[0]??null;
}
export function choosePatient(actors,healerId) {
  const me=actors.find(a=>a.id===healerId);assert.ok(me);
  if(me.pending)return actors.find(a=>a.id===me.pending.target)??null;
  const allies=actors.filter(a=>a.team===me.team&&(a.collapsed||a.hp<a.maxHp));
  allies.sort((a,b)=>Number(b.collapsed)-Number(a.collapsed)||fraction(a,b)||Number(b.id===me.id)-Number(a.id===me.id)||queue(a,b));
  return allies[0]??null;
}
export function teamDefeated(actors,team) {
  const members=actors.filter(a=>a.team===team);return members.length>0&&members.every(a=>a.collapsed);
}

const counterKeys=['attacks','ordinary','strong','large','suppress','recover','cleanse','healingMains','instant','starts','finishes',
  'startAttempts','finishAttempts','abandoned','damage','restored','overheal','allyHeals','collapses','revivals','targetChanges',
  'attackPaid','healPaid','cleansePaid','interruptPaid','responsePaid','interruptions','cancelled','guard','counterAttempt',
  'counterSucceeded','skipped','healingWith50','healingWith25'];
function counters(){return Object.fromEntries(counterKeys.map(k=>[k,0]));}
function count(e,a,key,value=1){e.counts[key]+=value;e.counts.byActor[a.id][key]+=value;}

export function createEncounter(options={}) {
  const o={D:130,kind:'duel',mode:'instant',harmony:'normal',healingPercent:0,resistance:0,
    attackPolicy:'suppress_reserve',targetPolicy:'patient',helperAccess:true,defender:'passive',
    healerPolicy:'heal',reply:'guard',interrupt:true,mirror:false,horizon:2000,...options};
  const periodic=o.healingRules==='periodic-v1';
  const controlled=o.controlRules==='control-v1';
  assert.ok(o.controlRules===undefined||controlled,'Unknown control rules');
  if(controlled){
    assert.ok(periodic,'Control rules require periodic healing');
    o.controlProfile??='none';o.controlPolicy??='pressure';o.helperControlRelease??=false;
    assert.ok(o.controlProfile==='none'||Object.hasOwn(controlProfiles,o.controlProfile),'Unknown control profile');
    assert.equal(typeof o.helperControlRelease,'boolean');
    assert.ok(['pressure','window'].includes(o.controlPolicy),'Unknown control policy');
  }else assert.ok(o.controlProfile===undefined&&o.controlPolicy===undefined&&o.helperControlRelease===undefined,'Control options require control rules');
  assert.ok(o.healingRules===undefined||periodic,'Unknown healing rules');
  if(periodic){
    Object.assign(o,{periodicMode:'combined',periodicAttack:controlled?'suppress_needed':'suppress_always',level:5,effectResistance:0,
      cleansePolicy:'never',activeGuard:false,...options,interrupt:false});
    assert.ok(['direct','hot','combined'].includes(o.periodicMode)&&['ordinary','suppress_always','suppress_needed'].includes(o.periodicAttack));
    assert.ok(Number.isInteger(o.level)&&o.level>=1&&o.level<=5&&Number.isFinite(o.effectResistance)&&o.effectResistance>=0&&o.effectResistance<=100);
    assert.ok(['never','first'].includes(o.cleansePolicy)&&typeof o.activeGuard==='boolean');
    assert.equal(o.jointPolicy,undefined);assert.equal(o.interruptPolicy,undefined);
    if(controlled)assert.equal(o.periodicAttack,'suppress_needed','Control variants require the same suppress_needed fallback');
  }
  assert.ok(['duel','support','group'].includes(o.kind)&&['instant','long','adaptive'].includes(o.mode));
  assert.ok(['normal','healer28','both36'].includes(o.harmony)&&attackPolicies.includes(o.attackPolicy));
  assert.ok(['passive','offensive'].includes(o.defender)&&['heal','cleanse_safe'].includes(o.healerPolicy));
  assert.ok(['none','guard','counter'].includes(o.reply)&&Number.isInteger(o.horizon)&&o.horizon>0);
  if(o.jointPolicy!==undefined){
    assert.ok(jointPolicies.includes(o.jointPolicy),'Unknown joint policy');
    assert.equal(o.kind,'duel','Joint policies support duel encounters only');
    assert.equal(o.interruptPolicy,undefined,'Joint and interruption policy options cannot be combined');
    if(options.attackPolicy===undefined)o.attackPolicy='fixed_large';
    assert.equal(o.attackPolicy,'fixed_large','Joint planner requires fixed_large metadata for adaptive maximum damage');
  }
  if(o.interruptPolicy!==undefined){
    assert.ok(interruptPolicies.includes(o.interruptPolicy),'Unknown interruption policy');
    assert.equal(o.kind,'duel','Interruption policies currently support duel encounters only');
  }
  const roles=o.kind==='duel'?['primary','healer']:o.kind==='support'?['primary','healer','helper']:
    o.mirror?['healer','helper','healer','helper']:['primary','striker','healer','helper'];
  const order=o.order??roles.map((_,i)=>i);assert.deepEqual([...order].sort(),roles.map((_,i)=>i));
  const operation=o.operation??createOperation();
  const actors=roles.map((role,i)=>{
    const a=actor(o.D,String.fromCharCode(65+i),{regenP:o.harmony==='both36'?36:o.harmony==='healer28'&&role==='healer'?28:null});
    a.team=o.kind==='group'?Number(i>=2):Number(i>0);a.role=role;a.orderIndex=order.indexOf(i);
    a.attackable=!(o.kind==='support'&&role==='helper'&&!o.helperAccess);
    a.healingPercent=role==='healer'?o.healingPercent:0;a.resistance=role==='healer'?o.resistance:0;
    a.lastTarget=null;
    if(periodic){a.hots=[];a.effectResistance=role==='healer'?o.effectResistance:0;}
    if(controlled)a.controls=[];
    a.tables=periodic?Object.fromEntries([['ordinary',28],['suppress',periodicProfiles[o.level-1].attackB]].flatMap(([kind,B])=>
      [0,-5].map(delta=>[`${kind}|${delta}`,operation.table(o.D,B,a.resistance,delta)]))):
      Object.fromEntries(Object.entries(attackProfiles).map(([kind,p])=>[kind,operation.table(o.D,p.B,a.resistance)]));
    if(controlled&&o.controlProfile!=='none')for(const delta of [0,-5])
      a.tables[`control|${delta}`]=operation.table(o.D,controlProfiles[o.controlProfile].B,a.resistance,delta);
    return a;
  });
  const counts=counters();counts.byActor=Object.fromEntries(actors.map(a=>[a.id,counters()]));
  if(periodic)for(const row of [counts,...Object.values(counts.byActor)])for(const key of [
    'direct','hotCasts','hotTicks','hotSuppressedTicks','potentialHealing','directRestored','hotRestored','markHits','saveAttempts',
    'saves','marksApplied','partialMarks','fullMarks','guardPaid','recoverGained','regenGained'
  ])row[key]=0;
  if(controlled)for(const row of [counts,...Object.values(counts.byActor)])for(const key of [
    'controlCasts','controlPaid','controlHits','controlSaveAttempts','controlSaves','controlsApplied',
    'controlBlockedMains','controlGuardDenials','controlBreakAttempts','controlBreakSucceeded','controlBreakPaid','allyControlBreaks'
  ])row[key]=0;
  if(o.interruptPolicy!==undefined){
    const keys=['interruptDecisions',...['inactive','no_reaction','no_energy','policy_never','long_start','reserve','attempt']
      .map(reason=>`interruptDecision_${reason}`)];
    for(const row of [counts,...Object.values(counts.byActor)])for(const key of keys)row[key]=0;
  }
  let jointCurve;
  if(o.jointPolicy!==undefined){
    jointCurve=operation.jointFor(actors[0],actors[1]);actors[0].jointObservedMode='instant';
    const keys=['jointMainDecisions','jointDecisions',...['inactive','no_reaction','no_energy','policy_never','forecast','attempt']
      .map(reason=>`jointDecision_${reason}`)];
    for(const row of [counts,...Object.values(counts.byActor)])for(const key of keys)row[key]=0;
  }
  return {options:o,operation,actors,order:[...order],counts,round:0,cursor:0,done:false,winner:null,
    ...(jointCurve?{jointCurve}:{}),
    patientId:actors.find(a=>a.team===1&&a.role==='healer').id,
    endpoints:{total:null,firstPatientFall:null,firstAnyFall:null}};
}

function eligibleMain(a){return living(a)&&a.active&&!a.used&&!a.collapsedThisTurn&&!blocksControl(a,'main');}
function doRecover(e,a){const before=a.energy;if(replaceMain(a,()=>recover(a))){count(e,a,'recover');
  if(e.options.healingRules==='periodic-v1')count(e,a,'recoverGained',a.energy-before);}}
function doCleanse(e,a,t){
  if(blocksControl(a,'cleanse'))return false;
  const hadPending=Boolean(a.pending);
  if(!replaceMain(a,()=>cleanse(a,t)))return false;
  count(e,a,'cleanse');count(e,a,'cleansePaid',price(a,8));if(hadPending)count(e,a,'abandoned');return true;
}
function doAttack(e,a,policy,dice){
  const target=chooseTarget(e.actors,a.id,e.options.targetPolicy);
  if(!target){doRecover(e,a);return;}
  if(e.options.healingRules==='periodic-v1'){
    const level=e.options.level,profile=e.options.controlProfile;
    const casting=e.options.controlRules==='control-v1'&&a.role==='primary'&&
      chooseControlCast({target,profile,policy:e.options.controlPolicy});
    const kind=casting?'control':a.role==='primary'?choosePeriodicAttack({target,policy:e.options.periodicAttack,level}):'ordinary';
    const p=casting?controlProfiles[profile]:kind==='ordinary'?{B:28,K:8}:{B:periodicProfiles[level-1].attackB,K:periodicProfiles[level-1].attackK};
    const cost=price(a,p.K);if(a.energy<cost){doRecover(e,a);return;}
    assert.ok(eligibleMain(a));a.used=true;a.energy-=cost;a.pending=null;
    let guard=0;
    if(e.options.activeGuard&&living(target)&&target.inEncounter&&target.reaction>0&&target.energy>=price(target,4)){
      if(blocksControl(target,'guard'))count(e,target,'controlGuardDenials');
      else{guard=-5;target.energy-=price(target,4);target.reaction=0;count(e,target,'guard');count(e,target,'guardPaid',price(target,4));}
    }
    const table=target.tables[`${kind}|${guard}`],outcome=table.entries[dice.damage()];
    assert.ok(outcome,'Damage dice index must be 0..399');const before=target.hp;
    damage(target,outcome.damage);
    if(casting&&outcome.quality>0){
      count(e,a,'controlHits');
      if(outcome.quality>=2||p.partial>0){
        count(e,a,'controlSaveAttempts');const save=dice.controlSave();
        if(save<target.effectResistance)count(e,a,'controlSaves');
        if(applyControl(target,a,profile,outcome.quality,{save,resistance:target.effectResistance}))count(e,a,'controlsApplied');
      }
    }
    if(kind==='suppress'&&outcome.quality>0){
      count(e,a,'markHits');count(e,a,'saveAttempts');const save=dice.save();
      if(save<target.effectResistance)count(e,a,'saves');
      if(applyPeriodicMark(target,a.id,outcome.quality,{level,save,effectResistance:target.effectResistance})){
        count(e,a,'marksApplied');count(e,a,outcome.quality===1?'partialMarks':'fullMarks');
      }
    }
    count(e,a,'attacks');
    if(casting){count(e,a,'controlCasts');count(e,a,'controlPaid',cost);}else count(e,a,kind);
    count(e,a,'attackPaid',cost);count(e,a,'damage',before-target.hp);return;
  }
  let kind;
  if(e.options.jointPolicy!==undefined&&a.role==='primary'){
    kind=chooseJointMain(e.jointCurve,{canAct:eligibleMain(a),energy:a.energy,
      mark:normalizeJointMarks(target.marks,target.started,target.active),
      phase:target.pending?'finish':a.jointObservedMode==='long'?'cast':'instant'}).kind;
    count(e,a,'jointMainDecisions');
  }else kind=chooseAttack({self:a,target},policy);
  if(kind==='recover'){doRecover(e,a);return;}
  const cost=price(a,attackProfiles[kind].K);
  assert.ok(eligibleMain(a)&&a.energy>=cost,'Current resources must fund the chosen action before RNG');
  const before=target.hp,hadPending=Boolean(a.pending);
  if(kind==='suppress'){
    a.used=true;a.energy-=cost;a.pending=null;
  }else assert.ok(replaceMain(a,()=>strikeAttack(a,target,{damage:0},kind)));
  const outcome=target.tables[kind].entries[dice.damage()];assert.ok(outcome,'Damage dice index must be 0..399');
  damage(target,outcome.damage);if(kind==='suppress'&&living(target))mark(target,a.id,outcome.quality);
  if(hadPending)count(e,a,'abandoned');
  if(a.lastTarget!==null&&a.lastTarget!==target.id)count(e,a,'targetChanges');a.lastTarget=target.id;
  count(e,a,'attacks');count(e,a,kind);count(e,a,'attackPaid',cost);count(e,a,'damage',before-target.hp);
}
function chooseInterrupter(e,source,mode,phase){
  if(!e.options.interrupt)return null;
  if(e.options.jointPolicy!==undefined){
    const candidate=e.actors.find(a=>a.team!==source.team);
    const out=chooseJointReaction(e.jointCurve,{canAct:living(candidate)&&candidate.inEncounter,
      hasReaction:candidate.reaction>0,energy:candidate.energy,
      mark:normalizeJointMarks(source.marks,source.started,source.active),
      phase:mode==='instant'?'instant':phase,missingHp:Math.max(0,source.maxHp-source.hp)},e.options.jointPolicy);
    count(e,candidate,'jointDecisions');count(e,candidate,`jointDecision_${out.reason}`);
    return out.attempt?candidate:null;
  }
  if(e.options.interruptPolicy!==undefined){
    // Opt-in is duel-only: identify the sole opponent before checking its current eligibility.
    const candidate=e.actors.find(a=>a.team!==source.team);
    const out=chooseInterrupt({canAct:living(candidate)&&candidate.inEncounter,
      hasReaction:candidate.reaction>0,energy:candidate.energy,interruptCost:price(candidate,8),
      strongCost:price(candidate,16),largeCost:price(candidate,24),mode,phase},e.options.interruptPolicy);
    count(e,candidate,'interruptDecisions');count(e,candidate,`interruptDecision_${out.reason}`);
    return out.attempt?candidate:null;
  }
  return e.actors.filter(a=>a.team!==source.team&&living(a)&&a.reaction>0&&a.energy>=price(a,8))
    .sort((a,b)=>b.energy-a.energy||queue(a,b))[0]??null;
}
function doHeal(e,a,dice){
  const o=e.options,hadPending=Boolean(a.pending);
  if(o.healingRules==='periodic-v1'){
    const target=choosePatient(e.actors,a.id)??a;
    const action=choosePeriodicMain({self:a,target,mode:o.periodicMode,cleansePolicy:o.cleansePolicy,offensive:o.defender==='offensive'});
    if(action==='cleanse'){if(!doCleanse(e,a,target))doRecover(e,a);return;}
    if(action==='attack'){doAttack(e,a,'fixed_ordinary',dice);return;}
    if(action==='recover'){doRecover(e,a);return;}
    const out=applyPeriodicHeal(a,target,{mode:action,level:o.level,calculate:e.operation.calculate,revive:true});
    if(!out.accepted){doRecover(e,a);return;}
    count(e,a,'healingMains');count(e,a,'healPaid',out.paid);count(e,a,action==='direct'?'direct':'hotCasts');
    if(action==='direct'){
      count(e,a,'restored',out.restored);count(e,a,'directRestored',out.restored);count(e,a,'potentialHealing',out.potential);
      count(e,a,'overheal',out.potential-out.restored);if(target.id!==a.id)count(e,a,'allyHeals');
      if(out.revived)count(e,target,'revivals');
    }
    return;
  }
  if(o.healerPolicy==='cleanse_safe'&&strength(a)===50&&remaining(a)>1&&a.hp>a.maxHp/4&&doCleanse(e,a,a))return;
  if(o.defender==='offensive'&&!a.pending&&e.actors.filter(t=>t.team===a.team).every(t=>living(t)&&t.hp>t.maxHp/2)){
    doAttack(e,a,'fixed_ordinary',dice);return;
  }
  // Preserve T120's unresisted known-profile threshold, rather than forecasting a future hit or policy choice.
  const enemyKinds=e.actors.filter(t=>t.team!==a.team).map(t=>t.role==='primary'?
    o.attackPolicy.startsWith('fixed_')?o.attackPolicy.slice(6):'large':'ordinary');
  const maxNextDamage=Math.max(...enemyKinds.map(k=>e.operation.table(o.D,attackProfiles[k].B,0).max));
  const mode=chooseMode(a,o.mode,maxNextDamage),patient=choosePatient(e.actors,a.id)??(mode==='long'?a:null);
  const finish=mode==='long'&&hadPending,cost=price(a,finish?0:mode==='long'?24:12);
  if(!patient||a.energy<cost){doRecover(e,a);if(hadPending)count(e,a,'abandoned');return;}
  if(o.jointPolicy!==undefined)e.actors.find(t=>t.team!==a.team).jointObservedMode=mode;
  const opponent=chooseInterrupter(e,a,mode,finish?'finish':'cast'),suppression=strength(patient),collapsed=patient.collapsed;
  const potential=mode==='long'&&!finish?0:e.operation.calculate(a,patient,mode==='long'?16:7);
  const out=applyHeal(a,patient,{id:`${a.id}:${a.started}`,mode,phase:finish?'finish':'cast',
    opponent,reply:o.reply,roll:dice.reaction,calculate:e.operation.calculate});
  assert.ok(out.accepted,'Prechecked healing must be accepted');
  count(e,a,'healingMains');count(e,a,mode==='instant'?'instant':finish?'finishAttempts':'startAttempts');
  if(out.started)count(e,a,'starts');if(out.completed&&mode==='long')count(e,a,'finishes');
  count(e,a,'healPaid',out.paid);count(e,a,'responsePaid',out.responsePaid);
  if(out.attempted){count(e,opponent,'interruptions');count(e,opponent,'interruptPaid',out.interruptPaid);}
  if(out.cancelled)count(e,a,'cancelled');if(out.defended)count(e,a,'guard');
  if(out.counterAttempted)count(e,a,'counterAttempt');if(out.countered)count(e,a,'counterSucceeded');
  if(out.completed){
    count(e,a,'restored',out.restored);count(e,a,'overheal',potential-out.restored);
    if(patient.id!==a.id)count(e,a,'allyHeals');if(collapsed&&!patient.collapsed)count(e,patient,'revivals');
    if(suppression===50)count(e,a,'healingWith50');if(suppression===25)count(e,a,'healingWith25');
  }
  // Each id is used once here. The accepted receipt API is reused without retaining a whole trial's history.
  delete a.receipts[`${a.id}:${a.started}`];
}
function finish(e,kind){
  if(e.options.healingRules==='periodic-v1')for(const a of e.actors){a.hots=[];a.marks=[];closeEncounter(a);if(a.controls)a.controls=[];}
  e.done=true;e.endpoints.total={time:e.round,kind};
  for(const key of ['firstPatientFall','firstAnyFall'])if(!e.endpoints[key])e.endpoints[key]={time:e.round,kind:kind==='horizon'?'horizon':'no-event'};
}
/** Exact absorbing-state optimization: there is no available target, healer, effect, or future random action. */
export function fastForwardIdle(e) {
  const o=e.options;
  if(o.healingRules==='periodic-v1')return false;
  if(e.done||e.cursor!==0||o.kind!=='support'||o.helperAccess||o.defender!=='passive'||o.trace||o.fastForward===false)return false;
  const [primary,healer,helper]=e.actors;
  if(!healer.collapsed||!living(primary)||!living(helper)||e.actors.some(a=>a.pending||a.marks.length)||
    primary.energy!==primary.p.max||helper.energy!==helper.p.max)return false;
  const opportunities=o.horizon-e.round;if(opportunities<=0)return false;
  for(const a of e.actors){
    a.started+=opportunities;a.active=false;a.ready=false;a.collapsedThisTurn=a.collapsed;
    a.used=!a.collapsed;
    if(a.collapsed)count(e,a,'skipped',opportunities);
    else{a.reaction=1;count(e,a,'recover',opportunities);}
  }
  e.round=o.horizon;finish(e,'horizon');return true;
}

/** Advance one personal opportunity, including a collapsed participant's skipped clock boundary. */
export function stepEncounter(e,dice) {
  if(e.done)return false;
  assert.ok(dice&&typeof dice.damage==='function'&&typeof dice.reaction==='function');
  if(e.cursor===0)e.round++;
  const a=e.actors[e.order[e.cursor]],prior=e.actors.map(t=>t.collapsed);begin(a);
  let controlAction={kind:'continue'};
  if(e.options.controlRules==='control-v1'&&living(a)&&!a.collapsedThisTurn){
    const ally=a.role==='helper'?e.actors.filter(t=>t.id!==a.id&&t.team===a.team&&controlsFor(t).length)
      .sort((x,y)=>Number(y.role==='healer')-Number(x.role==='healer')||queue(x,y))[0]??null:null;
    controlAction=chooseControlMain({self:a,offensive:e.options.defender==='offensive',ally,
      helperControlRelease:a.role==='helper'&&e.options.helperControlRelease});
  }
  if(!eligibleMain(a)){count(e,a,'skipped');if(controlAction.kind==='blocked')count(e,a,'controlBlockedMains');}
  else if(controlAction.kind==='break'){
    const out=breakControl(a,controlAction.target,controlAction.control,{roll:dice.break});
    assert.ok(out.accepted,'Fresh prechecked break must be admitted');count(e,a,'controlBreakAttempts');count(e,a,'controlBreakPaid',out.paid);
    if(out.succeeded)count(e,a,'controlBreakSucceeded');if(out.ally)count(e,a,'allyControlBreaks');
  }
  else if(controlAction.kind==='recover')doRecover(e,a);
  else if(controlAction.kind==='attack')doAttack(e,a,'fixed_ordinary',dice);
  else if(a.role==='healer')doHeal(e,a,dice);
  else if(a.role==='helper'){
    const allies=e.actors.filter(t=>t.team===a.team&&t.marks.length).sort((x,y)=>strength(y)-strength(x)||
      Number(y.collapsed)-Number(x.collapsed)||fraction(x,y)||Number(y.role==='healer')-Number(x.role==='healer')||queue(x,y));
    if(!allies[0]||!doCleanse(e,a,allies[0])){
      if(e.options.defender==='offensive')doAttack(e,a,'fixed_ordinary',dice);else doRecover(e,a);
    }
  }else doAttack(e,a,a.role==='striker'?'fixed_ordinary':e.options.attackPolicy,dice);
  // Complete this already-started opportunity before deciding the battle; no future actor begins after finish.
  if(e.options.healingRules==='periodic-v1'){
    for(const tick of tickPeriodic(a,e.operation.calculate)){
      const source=e.actors.find(t=>t.id===tick.source);assert.ok(source);
      if(!tick.applied){count(e,source,'hotSuppressedTicks');continue;}
      count(e,source,'hotTicks');count(e,source,'potentialHealing',tick.potential);count(e,source,'restored',tick.restored);
      count(e,source,'hotRestored',tick.restored);count(e,source,'overheal',tick.potential-tick.restored);
      if(source.id!==a.id)count(e,source,'allyHeals');if(tick.revived)count(e,a,'revivals');
    }
  }
  if(e.options.controlRules==='control-v1')expireControls(a);
  const beforeEnd=a.energy;end(a);
  if(e.options.healingRules==='periodic-v1')count(e,a,'regenGained',a.energy-beforeEnd);
  for(let i=0;i<e.actors.length;i++){
    const t=e.actors[i];assert.ok(t.energy>=0&&t.energy<=t.p.max);
    if(!prior[i]&&t.collapsed){
      count(e,t,'collapses');if(!e.endpoints.firstAnyFall)e.endpoints.firstAnyFall={time:e.round,kind:'event',actorId:t.id};
      if(t.id===e.patientId&&!e.endpoints.firstPatientFall)e.endpoints.firstPatientFall={time:e.round,kind:'event',actorId:t.id};
    }
  }
  if(teamDefeated(e.actors,0)||teamDefeated(e.actors,1)){
    e.winner=teamDefeated(e.actors,1)?0:1;finish(e,'event');
  }
  e.cursor=(e.cursor+1)%e.order.length;
  if(!e.done&&e.cursor===0&&e.round>=e.options.horizon)finish(e,'horizon');
  e.options.trace?.({round:e.round,actor:a.id,done:e.done,winner:e.winner});
  return true;
}

export function runTrial(options={}) {
  const {seed=seedFor('T123-trial'),dice,setup,...rest}=options;
  const randomDamage=rng(seed),randomReaction=rng(seedFor(`reaction|${seed}`)),randomSave=rng(seedFor(`effect-save|${seed}`)),
    randomControlSave=rng(seedFor(`control-save|${seed}`)),randomBreak=rng(seedFor(`control-break|${seed}`));
  const rolls=dice??{damage:()=>randomDamage()%400,save:()=>randomSave()%10000/100,controlSave:()=>randomControlSave()%10000/100,
    break:()=>{const k=randomBreak()%400;return[Math.floor(k/20)+1,k%20+1];},
    reaction:()=>{const k=randomReaction()%400;return[Math.floor(k/20)+1,k%20+1];}};
  const e=createEncounter(rest);setup?.(e);
  while(!e.done){if(!fastForwardIdle(e))stepEncounter(e,rolls);}
  return {seed,winner:e.winner,endpoints:e.endpoints,counts:e.counts};
}
