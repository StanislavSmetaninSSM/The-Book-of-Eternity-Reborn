// T120: cheap independent numerical fixtures; not a production combat test suite.
import test from 'node:test';
import assert from 'node:assert/strict';
import { actor, begin, end, applyHeal, cancelChannel, damage, replaceMain, prepareReady,
  healingValue, healingCalculator, closeEncounter } from './healing-modes-model.mjs';
import { mark, strength, cleanse, recover } from './suppression-model.mjs';
import { price } from './reaction-model.mjs';

const heal=(a,t=a,options={})=>applyHeal(a,t,{id:`${a.id}:${a.started}:${options.phase??'cast'}`,mode:'instant',...options});
const start=(a,t=a,options={})=>heal(a,t,{mode:'long',...options});
const finish=(a,t=a,options={})=>heal(a,t,{mode:'long',phase:'finish',...options});
function next(a){end(a);begin(a);}

test('all approved levels preserve source scaling and one rounding after suppression',()=>{
  const a=actor(130,'a');
  for(const [level,instant,long,k] of [[1,31,82,4],[2,41,103,6],[3,51,124,8],[4,62,144,10],[5,72,165,12]]){
    a.hp=1;begin(a);const e=a.energy;const result=heal(a,a,{level});
    assert.equal(result.restored,instant);assert.equal(e-a.energy,price(a,k));end(a);a.energy=a.p.max;
    assert.equal(healingValue(a,a,6+2*level),long);
  }
  mark(a,'enemy',2);assert.equal(healingValue(a,a,16),82);
});
test('long can start at full health; pays once, no initial HP, second Main occupied',()=>{
  const a=actor(130,'a');begin(a);const e=a.energy;const out=start(a);
  assert.equal(out.accepted,true);assert.equal(out.restored,0);assert.equal(a.energy,e-price(a,24));
  assert.equal(a.hp,a.p.hp);assert.equal(a.used,true);assert.ok(a.pending);
  next(a);damage(a,300);const before=a.energy;const done=finish(a);
  assert.equal(done.restored,165);assert.equal(a.energy,before);assert.equal(a.used,true);assert.equal(a.pending,null);
  assert.equal(heal(a,a,{id:'extra'}).accepted,false);
});
test('preflight refusals spend neither Main nor energy and do not inspect dice',()=>{
  const a=actor(0,'a');begin(a);const e=a.energy;
  assert.equal(heal(a).accepted,false);assert.equal(start(a,a,{access:false}).accepted,false);
  a.energy=2399;assert.equal(start(a).accepted,false);assert.equal(a.energy,2399);assert.equal(a.used,false);
  a.energy=e;closeEncounter(a);assert.equal(start(a).accepted,false);
});
test('accepted start/finish each have distinct paid reaction windows',()=>{
  const a=actor(130,'a'),b=actor(130,'b');a.hp=100;begin(a);
  const s=start(a,a,{opponent:b,reply:'guard',roll:()=>[10,5]});
  assert.equal(s.attempted,true);assert.equal(s.defended,true);assert.equal(s.cancelled,false);
  assert.equal(a.reaction,0);assert.equal(b.reaction,0);
  end(a);begin(b);end(b);assert.equal(a.reaction,0);begin(a);assert.equal(a.reaction,1);
  const e=a.energy;const f=finish(a,a,{opponent:b,reply:'guard',roll:()=>[10,5]});
  assert.equal(f.defended,true);assert.equal(f.restored,165);assert.equal(a.energy,e-price(a,4));
});
test('interrupted start loses paid Main; interrupted finish loses its own Main',()=>{
  const a=actor(130,'a'),b=actor(130,'b');a.hp=100;begin(a);const e=a.energy;
  assert.equal(start(a,a,{opponent:b,roll:()=>[10,5]}).cancelled,true);
  assert.equal(a.energy,e-price(a,24));assert.equal(a.pending,null);assert.equal(a.used,true);assert.equal(a.hp,100);
  next(a);start(a);next(a);begin(b);end(b);
  const done=finish(a,a,{opponent:b,roll:()=>[10,5]});
  assert.equal(done.cancelled,true);assert.equal(a.used,true);assert.equal(a.hp,100);assert.equal(a.pending,null);
});
test('break before finish leaves next Main; ordinary damage alone preserves channel',()=>{
  const a=actor(0,'a');begin(a);start(a);damage(a,1);assert.ok(a.pending);next(a);
  cancelChannel(a);assert.equal(a.used,false);assert.equal(heal(a).accepted,true);
});
test('pass or skipped personal opportunity expires channel without automatic payout',()=>{
  const a=actor(130,'a');a.hp=100;begin(a);start(a);next(a);end(a);
  assert.equal(a.pending,null);assert.equal(a.hp,100);begin(a);assert.equal(finish(a).accepted,false);
});
test('replacement self-cleanse/recovery cancels channel but allied cleanse preserves it',()=>{
  const a=actor(130,'a'),helper=actor(130,'helper');a.hp=100;begin(a);start(a);mark(a,'enemy',2);end(a);
  begin(helper);assert.equal(cleanse(helper,a),true);end(helper);assert.ok(a.pending);begin(a);
  assert.equal(finish(a).restored,165);next(a);start(a);next(a);mark(a,'enemy',2);
  assert.equal(replaceMain(a,()=>cleanse(a,a)),true);assert.equal(a.pending,null);
  next(a);start(a);next(a);assert.equal(replaceMain(a,()=>recover(a)),true);assert.equal(a.pending,null);
});
test('finish uses current power, modifiers and suppression; cached DATA cannot freeze them',()=>{
  const a=actor(130,'a'),t=actor(130,'t'),calc=healingCalculator();t.hp=1;begin(a);start(a,t);next(a);
  assert.equal(calc(a,t,16),165);a.power=0;a.healingPercent=25;mark(t,'enemy',2);
  const done=finish(a,t,{calculate:calc});assert.equal(done.restored,36);assert.equal(t.hp,37);
  t.marks=[];assert.equal(calc(a,t,16),72);assert.equal(calc(a,t,16),72);
  assert.equal(calc.stats.computed,3);assert.equal(calc.stats.hits,1);
});
test('target and level fixed; invalid finish closes channel without spending second Main',()=>{
  const a=actor(130,'a'),b=actor(130,'b');a.hp=100;begin(a);start(a);next(a);
  assert.equal(finish(a,b).accepted,false);assert.equal(a.pending,null);assert.equal(a.used,false);
  start(a);next(a);assert.equal(finish(a,a,{level:4}).accepted,false);assert.equal(a.used,false);
  start(a);next(a);assert.equal(finish(a,a,{access:false}).accepted,false);assert.equal(a.used,false);
});
test('finish fresh burden only, no repayment or retrospective repricing',()=>{
  const a=actor(130,'a');a.hp=100;begin(a);const e=a.energy;start(a,a,{burdenP:2});
  assert.equal(a.energy,e-price(a,26));next(a);const before=a.energy;const done=finish(a,a,{burdenP:3});
  assert.equal(done.accepted,true);assert.equal(a.energy,before-price(a,3));
});
test('unaffordable finish burden is preflight refusal with free Main',()=>{
  const a=actor(130,'a');a.hp=100;begin(a);start(a);next(a);a.energy=price(a,3)-1;
  assert.equal(finish(a,a,{burdenP:3}).accepted,false);assert.equal(a.used,false);assert.equal(a.pending,null);
});
test('caster collapse cancels permanently, partial revival cannot act, patient HP0 is valid',()=>{
  const a=actor(130,'a'),h=actor(130,'h');a.hp=100;begin(a);start(a);damage(a,100);
  assert.equal(a.pending,null);assert.equal(a.collapsed,true);end(a);a.energy=0;a.reaction=0;
  begin(a);begin(h);assert.equal(heal(h,a).restored,72);assert.equal(a.collapsed,true);assert.equal(a.hp,72);
  end(h);begin(h);heal(h,a);end(h);assert.equal(a.hp,144);assert.equal(a.collapsed,false);
  assert.equal(heal(a,a,{id:'revived-same-slot'}).accepted,false);end(a);assert.equal(a.energy,0);assert.equal(a.reaction,0);
  begin(a);assert.equal(a.reaction,1);assert.equal(finish(a).accepted,false);
});
test('long heals HP0 ally past threshold; suppression can leave them collapsed',()=>{
  for(const suppressed of [false,true]){
    const a=actor(130,'a'),t=actor(130,'t');damage(t,t.hp);begin(a);start(a,t);next(a);
    if(suppressed)mark(t,'enemy',2);finish(a,t);
    assert.equal(t.hp,suppressed?82:165);assert.equal(t.collapsed,suppressed);
  }
});
test('overflow lost, no retarget; completed id replay survives plain snapshot without new spend',()=>{
  const a=actor(0,'a');a.hp=100;begin(a);start(a);next(a);a.hp=120;
  const result=finish(a,a,{id:'finish-1'});assert.equal(result.restored,1);const snap=JSON.parse(JSON.stringify(a));
  const e=snap.energy;const repeat=finish(snap,snap,{id:'finish-1'});
  assert.equal(repeat.replay,true);assert.equal(snap.hp,121);assert.equal(snap.energy,e);
  assert.equal(heal(snap,snap,{id:'finish-1'}).accepted,false);
});
test('start replay cannot recreate expired channel; encounter end never pays HP',()=>{
  const a=actor(0,'a');a.hp=50;begin(a);start(a,a,{id:'start-1'});next(a);end(a);
  start(a,a,{id:'start-1'});assert.equal(a.pending,null);begin(a);start(a);closeEncounter(a);
  assert.equal(a.pending,null);assert.equal(a.hp,50);assert.equal(finish(a).accepted,false);
});
test('Ready reserves Main and trigger uses shared reaction/price; long has no Ready shortcut',()=>{
  const a=actor(130,'a'),t=actor(130,'t');t.hp=1;begin(a);
  assert.equal(prepareReady(a,'long'),false);assert.equal(a.used,false);
  assert.equal(prepareReady(a,'instant'),true);assert.equal(a.used,true);end(a);const e=a.energy;
  const out=heal(a,t,{context:'ready',id:'ready-1'});assert.equal(out.restored,72);
  assert.equal(a.energy,e-price(a,12));assert.equal(a.reaction,0);
  assert.equal(heal(a,t,{context:'ready',id:'ready-2'}).accepted,false);
});
test('counter cancels interruption before original dice; forced-failure interference still costs',()=>{
  const a=actor(130,'a'),b=actor(130,'b');a.hp=1;begin(a);let calls=0;
  const out=heal(a,a,{opponent:b,reply:'counter',roll:()=>{calls++;return[20,1];}});
  assert.equal(calls,1);assert.equal(out.countered,true);assert.equal(out.restored,72);
  next(a);begin(b);end(b);const e=b.energy;
  const paid=heal(a,a,{opponent:b,forceInterruptFailure:true,roll:()=>[20,1]});
  assert.equal(paid.attempted,true);assert.equal(paid.cancelled,false);assert.equal(b.energy,e-price(b,8));
});
