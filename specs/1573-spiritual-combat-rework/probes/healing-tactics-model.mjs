// T123 standalone numerical experiment. Accepted formulas remain in the earlier models.
import assert from 'node:assert/strict';
import {rng,seedFor} from './healing-counters.mjs';
import {price} from './reaction-model.mjs';
import {strength,remaining,mark,cleanse,recover} from './suppression-model.mjs';
import {actor,begin,end,damage,applyHeal,replaceMain,healingCalculator} from './healing-modes-model.mjs';
import {protectedDamageTable,chooseMode,strikeAttack} from './healing-modes-probe.mjs';

export const attackProfiles=Object.freeze(Object.fromEntries(Object.entries({
  ordinary:[28,8],strong:[36,16],large:[44,24],suppress:[24,12]
}).map(([key,[B,K]])=>[key,Object.freeze({B,K})])));
export const attackPolicies=Object.freeze(['fixed_ordinary','fixed_strong','fixed_large','fixed_suppress','greedy','reserve','suppress_reserve']);

// One measurement owns these immutable DATA, released with the operation. Current permission is never cached.
export function createOperation() {
  const tables=new Map(),calculate=healingCalculator(),stats={damageComputed:0,damageHits:0};
  return {stats,calculate,table(D,B,resistance){
    const key=`T123-damage-v1|${D}|${B}|${resistance}`;
    if(tables.has(key)){stats.damageHits++;return tables.get(key);}
    const entries=Object.freeze(protectedDamageTable(D,B,resistance).map(Object.freeze));
    const data=Object.freeze({entries,max:Math.max(...entries.map(x=>x.damage))});
    tables.set(key,data);stats.damageComputed++;return data;
  }};
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
  assert.ok(['duel','support','group'].includes(o.kind)&&['instant','long','adaptive'].includes(o.mode));
  assert.ok(['normal','healer28','both36'].includes(o.harmony)&&attackPolicies.includes(o.attackPolicy));
  assert.ok(['passive','offensive'].includes(o.defender)&&['heal','cleanse_safe'].includes(o.healerPolicy));
  assert.ok(['none','guard','counter'].includes(o.reply)&&Number.isInteger(o.horizon)&&o.horizon>0);
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
    a.tables=Object.fromEntries(Object.entries(attackProfiles).map(([kind,p])=>[kind,operation.table(o.D,p.B,a.resistance)]));
    return a;
  });
  const counts=counters();counts.byActor=Object.fromEntries(actors.map(a=>[a.id,counters()]));
  return {options:o,operation,actors,order:[...order],counts,round:0,cursor:0,done:false,winner:null,
    patientId:actors.find(a=>a.team===1&&a.role==='healer').id,
    endpoints:{total:null,firstPatientFall:null,firstAnyFall:null}};
}

function eligibleMain(a){return living(a)&&a.active&&!a.used&&!a.collapsedThisTurn;}
function doRecover(e,a){if(replaceMain(a,()=>recover(a)))count(e,a,'recover');}
function doCleanse(e,a,t){
  const hadPending=Boolean(a.pending);
  if(!replaceMain(a,()=>cleanse(a,t)))return false;
  count(e,a,'cleanse');count(e,a,'cleansePaid',price(a,8));if(hadPending)count(e,a,'abandoned');return true;
}
function doAttack(e,a,policy,dice){
  const target=chooseTarget(e.actors,a.id,e.options.targetPolicy);
  if(!target){doRecover(e,a);return;}
  const kind=chooseAttack({self:a,target},policy);
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
function chooseInterrupter(e,source){
  if(!e.options.interrupt)return null;
  return e.actors.filter(a=>a.team!==source.team&&living(a)&&a.reaction>0&&a.energy>=price(a,8))
    .sort((a,b)=>b.energy-a.energy||queue(a,b))[0]??null;
}
function doHeal(e,a,dice){
  const o=e.options,hadPending=Boolean(a.pending);
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
  const opponent=chooseInterrupter(e,a),suppression=strength(patient),collapsed=patient.collapsed;
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
  e.done=true;e.endpoints.total={time:e.round,kind};
  for(const key of ['firstPatientFall','firstAnyFall'])if(!e.endpoints[key])e.endpoints[key]={time:e.round,kind:kind==='horizon'?'horizon':'no-event'};
}
/** Exact absorbing-state optimization: there is no available target, healer, effect, or future random action. */
export function fastForwardIdle(e) {
  const o=e.options;
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
  if(!eligibleMain(a))count(e,a,'skipped');
  else if(a.role==='healer')doHeal(e,a,dice);
  else if(a.role==='helper'){
    const allies=e.actors.filter(t=>t.team===a.team&&t.marks.length).sort((x,y)=>strength(y)-strength(x)||
      Number(y.collapsed)-Number(x.collapsed)||fraction(x,y)||Number(y.role==='healer')-Number(x.role==='healer')||queue(x,y));
    if(!allies[0]||!doCleanse(e,a,allies[0])){
      if(e.options.defender==='offensive')doAttack(e,a,'fixed_ordinary',dice);else doRecover(e,a);
    }
  }else doAttack(e,a,a.role==='striker'?'fixed_ordinary':e.options.attackPolicy,dice);
  end(a);
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
  const randomDamage=rng(seed),randomReaction=rng(seedFor(`reaction|${seed}`));
  const rolls=dice??{damage:()=>randomDamage()%400,reaction:()=>{const k=randomReaction()%400;return[Math.floor(k/20)+1,k%20+1];}};
  const e=createEncounter(rest);setup?.(e);
  while(!e.done){if(!fastForwardIdle(e))stepEncounter(e,rolls);}
  return {seed,winner:e.winner,endpoints:e.endpoints,counts:e.counts};
}
