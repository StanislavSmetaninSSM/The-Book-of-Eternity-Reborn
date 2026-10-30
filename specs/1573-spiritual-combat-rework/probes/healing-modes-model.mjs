// T120 standalone numerical state machine. No production imports or game-object setup.
import assert from 'node:assert/strict';
import { round } from './healing-counters.mjs';
import { strength } from './suppression-model.mjs';
import { createActor, beginTurn, endTurn, react, price, checkedQuality } from './reaction-model.mjs';

export function actor(D,id,options={}) {
  const a=createActor(D,id,options);
  return {...a,maxHp:a.p.hp,power:a.p.X,flatHealing:0,healingPercent:0,incomingHealingPercent:0,
    collapsed:false,pending:null,ready:false,inEncounter:true,receipts:{}};
}
export function begin(a) {
  const priorReaction=a.reaction;
  if(a.hp<=0){a.collapsed=true;cancelChannel(a);}
  beginTurn(a);
  if(a.collapsed){a.collapsedThisTurn=true;a.reaction=priorReaction;}
  a.ready=false; // Readiness expires at the next normal personal boundary.
}
export function end(a) {
  if(a.pending && a.started>=a.pending.due)cancelChannel(a);
  endTurn(a);
}
export function cancelChannel(a) {a.pending=null;}
export function damage(a,amount) {
  assert.ok(amount>=0);
  a.hp=Math.max(0,a.hp-amount);
  if(a.hp===0){a.collapsed=true;cancelChannel(a);if(a.active)a.collapsedThisTurn=true;}
}
export function closeEncounter(a) {a.inEncounter=false;a.ready=false;cancelChannel(a);}
export function replaceMain(a,action) {
  if(!a.active||a.used||a.collapsed||a.collapsedThisTurn)return false;
  // An invalid proposed replacement does not consume a still-available continuation.
  const accepted=action();if(accepted)cancelChannel(a);return accepted;
}
export function prepareReady(a,mode) {
  if(mode!=='instant'||!a.inEncounter||!a.active||a.used||a.collapsed||a.collapsedThisTurn)return false;
  cancelChannel(a);a.used=true;a.ready=true;return true;
}

function healingInputs(a,t,B) {
  return [B,a.p.D,Math.min(2000+50*a.p.D,Math.max(0,Math.round(a.power*100))),
    Math.round(a.flatHealing*100),a.healingPercent,t.incomingHealingPercent,strength(t)];
}
function evaluateHealing([B,D,A100,flat100,outgoing,incoming,suppression]) {
  // Exact rational evaluation for probe profiles: stats/flat to 0.01, integer percentages.
  let n=BigInt(B)*BigInt(10000+3*A100)*BigInt(50+D)+BigInt(flat100)*5000n;
  if(n<0n)n=0n;
  n*=BigInt(Math.max(0,100+outgoing))*BigInt(Math.max(0,100+incoming))*BigInt(100-suppression);
  return round(n,500000000000n);
}
export function healingValue(a,t,B) {return evaluateHealing(healingInputs(a,t,B));}
export function healingCalculator() {
  // Owned by one numerical operation. Full formula identity, no HP/authority cached.
  const values=new Map(),stats={computed:0,hits:0};
  const calculate=(a,t,B)=>{
    const input=healingInputs(a,t,B),key=input.join('|');
    if(values.has(key)){stats.hits++;return values.get(key);}
    const value=evaluateHealing(input);values.set(key,value);stats.computed++;return value;
  };
  calculate.stats=stats;return calculate;
}

function interruption(a,opponent,{reply,roll,forceInterruptFailure},out) {
  if(!opponent)return;
  out.noInterruptEnergy=opponent.energy<price(opponent,8);
  out.noInterruptReaction=opponent.reaction<1;
  const root=new Set();
  // A Ready trigger already used this actor's one reaction in the same root.
  if(out.ready)root.add(a.id);
  if(!opponent.collapsed&&react(opponent,8,root)){
    out.attempted=true;out.interruptPaid=price(opponent,8);
    if(reply==='guard'&&react(a,4,root)){out.defended=true;out.responsePaid=price(a,4);}
    if(reply==='counter'&&react(a,8,root)){
      out.counterAttempted=true;out.responsePaid=price(a,8);
      const [x,y]=roll();out.countered=checkedQuality(x,y,a.p.X,a.willDelta-opponent.willDelta)>=2;
    }
    if(!out.countered){
      const [x,y]=roll();
      out.cancelled=!forceInterruptFailure&&checkedQuality(x,y,opponent.p.X,
        opponent.willDelta-a.willDelta-(out.defended?5:0))>=2;
    }
  }
}

export function applyHeal(a,t,{id,mode='instant',phase='cast',level=5,access=true,context='main',
  burdenP=0,opponent=null,reply='none',roll=()=>{throw Error('Explicit dice required');},
  forceInterruptFailure=false,calculate=healingValue}={}) {
  assert.ok(typeof id==='string'&&id.length>0);
  assert.ok(['instant','long','old'].includes(mode)&&['cast','finish'].includes(phase));
  assert.ok(['main','ready'].includes(context)&&['none','guard','counter'].includes(reply));
  assert.ok(Number.isInteger(level)&&level>=1&&level<=5&&burdenP>=0);
  const identity=JSON.stringify([mode,phase,level,t.id,context]);
  const previous=a.receipts[id];
  if(previous)return previous.identity===identity?{...previous.out,replay:true}:{accepted:false,reason:'id-conflict'};
  const out={accepted:false,phase,mode,attempted:false,cancelled:false,defended:false,counterAttempted:false,
    countered:false,restored:0,paid:0,interruptPaid:0,responsePaid:0,noInterruptEnergy:false,
    noInterruptReaction:false,ready:context==='ready',started:false,completed:false};
  const finishing=mode==='long'&&phase==='finish';
  const alive=a.inEncounter&&!a.collapsed&&!a.collapsedThisTurn&&a.hp>0;
  const slot=context==='main'?a.active&&!a.used:a.ready&&a.reaction>0&&mode==='instant'&&phase==='cast';
  const pending=a.pending;
  const continuation=finishing&&pending&&pending.target===t.id&&pending.level===level&&
    pending.due===a.started&&context==='main';
  if(!alive||!slot||!access||phase==='finish'&&!finishing||finishing&&!continuation){
    if(finishing&&pending)cancelChannel(a);return out;
  }
  if(!finishing&&mode!=='long'&&t.hp>=t.maxHp)return out;
  const K=finishing?0:mode==='old'?28:mode==='long'?4*(level+1):2*(level+1);
  const cost=price(a,K+burdenP);
  if(a.energy<cost){if(finishing)cancelChannel(a);return out;}
  if(context==='ready'){
    if(!react(a,0,new Set()))return out;a.ready=false;
  }else a.used=true;
  a.energy-=cost;out.paid=cost;out.accepted=true;
  cancelChannel(a); // Completion consumes old channel; any accepted new Main replaces it.
  interruption(a,opponent,{reply,roll,forceInterruptFailure},out);
  if(!out.cancelled){
    if(mode==='long'&&!finishing){
      a.pending={id,target:t.id,level,due:a.started+1};out.started=true;
    }else{
      const B=mode==='old'?22:mode==='long'?6+2*level:2+level;
      out.restored=Math.max(0,Math.min(t.maxHp-t.hp,calculate(a,t,B)));
      t.hp+=out.restored;
      if(t.collapsed&&t.hp>=Math.max(1,Math.floor(t.maxHp/10)))t.collapsed=false;
      out.completed=true;
    }
  }
  // A plain serializable receipt proves probe retries don't repay/reapply. Not a runtime persistence implementation.
  a.receipts[id]={identity,out:{...out}};return out;
}
