import test from 'node:test';
import assert from 'node:assert/strict';
import * as control from './healing-control-model.mjs';
import {actor,begin,end,damage} from './healing-modes-model.mjs';
import {applyPeriodicHeal} from './healing-periodic-model.mjs';
import {createEncounter,stepEncounter,createOperation,runTrial} from './healing-tactics-model.mjs';
import {price} from './reaction-model.mjs';

const options={healingRules:'periodic-v1',periodicMode:'combined',periodicAttack:'suppress_needed',level:5,
  effectResistance:0,cleansePolicy:'first',activeGuard:false,interrupt:false,controlRules:'control-v1',controlProfile:'seal'};
const miss={damage:()=>0,reaction:()=>{throw Error('No Heal interruption');},save:()=>99,controlSave:()=>99,break:()=>[1,20]};
const full={...miss,damage:()=>15*20+9};
const partial={...miss,damage:()=>9*20+5};
const crit={...miss,damage:()=>399};

test('seal and stun are addressed, independent by controller/art, autonomous and expire on target clocks',()=>{
  assert.equal(typeof control.applyControl,'function');const a=actor(130,'A'),b=actor(130,'B'),t=actor(130,'T');
  control.applyControl(t,a,'seal',2,{save:99});control.applyControl(t,b,'seal',1,{save:99});
  control.applyControl(t,a,'stun_long',2,{save:99});assert.equal(t.controls.length,3);
  control.applyControl(t,a,'seal',1,{save:99});assert.equal(t.controls.find(x=>x.source==='A'&&x.family==='seal').expires,3);
  damage(a,a.hp);assert.equal(control.blocksControl(t,'heal'),true);assert.equal(control.blocksControl(b,'heal'),false);
  begin(t);control.expireControls(t);end(t);assert.equal(t.controls.length,2);
  begin(t);control.expireControls(t);end(t);assert.equal(t.controls.length,1);
  begin(t);control.expireControls(t);end(t);assert.equal(t.controls.length,0);
});
test('control casts and guard are paid before dice, independent save follows hit even for crit',()=>{
  const e=createEncounter({...options,activeGuard:true,effectResistance:100}),[a,t]=e.actors;
  stepEncounter(e,{...crit,damage:()=>{assert.equal(a.energy,a.p.max-2880);assert.equal(t.energy,t.p.max-1440);return 399;},
    controlSave:()=>{assert.equal(a.used,true);return 0;}});
  assert.equal(e.counts.controlCasts,1);assert.equal(e.counts.controlPaid,2880);assert.equal(e.counts.controlSaveAttempts,1);
  assert.equal(e.counts.controlSaves,1);assert.equal(e.counts.controlsApplied,0);assert.equal(t.controls.length,0);assert.equal(e.counts.damage,0);
});
test('miss and partial short stun skip control save, partial long stun and absorbed seal can apply',()=>{
  const e=createEncounter({...options,controlProfile:'stun'});
  stepEncounter(e,{...partial,controlSave:()=>{throw Error('partial stun has no effect save');}});
  assert.equal(e.counts.controlPaid,5760);assert.equal(e.counts.controlsApplied,0);assert.equal(e.counts.damage,56);
  const f=createEncounter({...options,controlProfile:'stun_long'});stepEncounter(f,partial);
  assert.equal(f.counts.controlPaid,8640);assert.equal(f.counts.controlsApplied,1);assert.equal(f.actors[1].controls[0].expires,1);
  const g=createEncounter(options);stepEncounter(g,{...miss,controlSave:()=>{throw Error('miss has no save');}});
  assert.equal(g.counts.controlCasts,1);assert.equal(g.counts.controlSaveAttempts,0);
  const h=createEncounter(options);stepEncounter(h,full);assert.equal(h.counts.damage,0);assert.equal(h.counts.controlsApplied,1);
});
test('seal blocks new Heal and cleansing without charge, but permits guard and ordinary actions',()=>{
  const e=createEncounter(options),[a,h]=e.actors;stepEncounter(e,full);h.hp=200;
  begin(h);const energy=h.energy;
  assert.equal(applyPeriodicHeal(h,h,{mode:'direct'}).accepted,false);assert.equal(h.energy,energy);assert.equal(h.used,false);
  assert.equal(control.blocksControl(h,'guard'),false);assert.equal(control.blocksControl(h,'attack'),false);
  end(h);
  const f=createEncounter({...options,controlProfile:'stun'});stepEncounter(f,full);
  assert.equal(control.blocksControl(f.actors[1],'guard'),true);assert.equal(control.blocksControl(a,'guard'),false);
});
test('stun forfeits Main and active reaction while due HoT, free defense, refresh and regen continue',()=>{
  const e=createEncounter({...options,controlProfile:'stun',activeGuard:true}),[a,h]=e.actors;
  begin(h);applyPeriodicHeal(h,h,{mode:'hot',revive:true});end(h);h.hp=500;h.energy=1000;h.reaction=0;
  stepEncounter(e,full);assert.equal(h.controls.length,1);const before=h.energy;
  stepEncounter(e,miss);assert.equal(h.reaction,1);assert.equal(h.used,false);assert.equal(h.hp,511);
  assert.equal(h.energy,before+h.regen);assert.equal(e.counts.controlBlockedMains,1);assert.equal(e.counts.hotTicks,1);
  assert.equal(h.controls.length,0);assert.equal(e.counts.byActor.B.healingMains,0);
});
test('stun denies paid guard but retains the ordinary free defense before its opportunity',()=>{
  const e=createEncounter({...options,activeGuard:true,controlProfile:'none'}),[a,h]=e.actors;
  control.applyControl(h,a,'stun_long',2,{save:99});h.reaction=1;const energy=h.energy;
  stepEncounter(e,miss);assert.equal(h.energy,energy);assert.equal(e.counts.controlGuardDenials,1);
  assert.equal(e.counts.guardPaid,0);assert.equal(e.counts.damage,0);assert.equal(h.reaction,1);
});
test('seal self-break uses snapshot Will, spends one Main and one8P before dice, removes exactly one bond',()=>{
  const a=actor(130,'A'),b=actor(130,'B'),t=actor(130,'T');
  control.applyControl(t,a,'seal',2,{save:99});control.applyControl(t,b,'seal',2,{save:99});a.willStat=1000;
  begin(t);const energy=t.energy,out=control.breakControl(t,t,t.controls[0],{roll:()=>{
    assert.equal(t.energy,energy-2880);assert.equal(t.used,true);return[10,10];}});
  assert.equal(out.succeeded,true);assert.equal(t.controls.length,1);assert.equal(t.controls[0].source,'B');
  assert.equal(control.breakControl(t,t,t.controls[0],{roll:()=>{throw Error('No extra Main');}}).accepted,false);
});
test('seal policy breaks long bonds, attacks or recovers the final opportunity and never breaks stun itself',()=>{
  const e=createEncounter({...options,order:[0,1]}),[a,h]=e.actors;
  stepEncounter(e,full);stepEncounter(e,{...miss,break:()=>[10,10]});assert.equal(e.counts.controlBreakAttempts,1);
  assert.equal(e.counts.controlBreakSucceeded,1);assert.equal(e.counts.healingMains,0);assert.equal(h.controls.length,0);
  const f=createEncounter({...options,defender:'offensive',order:[1,0]}),[x,y]=f.actors;
  control.applyControl(y,x,'seal',1,{save:99});stepEncounter(f,full);
  assert.equal(f.counts.byActor.B.attacks,1);assert.equal(f.counts.controlBreakAttempts,0);assert.equal(y.controls.length,0);
});
test('helper release before healer changes current admission; later helper cannot refund an already skipped Main',()=>{
  const e=createEncounter({...options,kind:'group',helperControlRelease:true,order:[0,3,2,1]}),[a,,h,b]=e.actors;
  assert.deepEqual(e.actors.map(x=>x.role),['primary','striker','healer','helper']);assert.deepEqual(e.actors.map(x=>x.team),[0,0,1,1]);
  stepEncounter(e,full);stepEncounter(e,{...miss,break:()=>[10,10]});stepEncounter(e,miss);
  assert.equal(e.counts.allyControlBreaks,1);assert.equal(e.counts.byActor.D.controlBreakPaid,2880);assert.equal(e.counts.byActor.C.hotCasts,1);
  const f=createEncounter({...options,controlProfile:'stun',kind:'group',helperControlRelease:true,order:[0,2,3,1]});
  stepEncounter(f,full);stepEncounter(f,miss);stepEncounter(f,{...miss,break:()=>[10,10]});
  assert.equal(f.counts.byActor.C.healingMains,0);assert.equal(f.counts.controlBlockedMains,1);
});
test('cleanse removes antiheal only, control save preserves an old bond, and failed break remains paid',()=>{
  const e=createEncounter({...options,controlProfile:'none',kind:'group',helperControlRelease:false,order:[3,2,0,1]}),[a,,h]=e.actors;
  h.marks=[{amount:90,expires:2}];control.applyControl(h,a,'seal',2,{save:99});const old=structuredClone(h.controls);
  assert.equal(control.applyControl(h,a,'seal',3,{save:0,resistance:100}),false);assert.deepEqual(h.controls,old);
  stepEncounter(e,miss);assert.equal(h.marks.length,0);assert.deepEqual(h.controls,old);
  stepEncounter(e,miss);assert.equal(e.counts.controlBreakPaid,2880);assert.equal(e.counts.controlBreakSucceeded,0);
});
test('break compares fresh Will modifier with the source snapshot while Favor keeps its own crit threshold',()=>{
  const a=actor(130,'A'),t=actor(130,'T');a.willStat=85;t.willStat=60;
  control.applyControl(t,a,'seal',2,{save:99});assert.equal(t.controls[0].sourceWillModifier,189);a.willStat=0;
  begin(t);assert.equal(control.breakControl(t,t,t.controls[0],{roll:()=>[10,10]}).succeeded,false);end(t);
  begin(t);assert.equal(control.breakControl(t,t,t.controls[0],{roll:()=>[19,20]}).succeeded,true);
});
test('a winning controlled encounter resolves its current due HoT and clears all bonds',()=>{
  const e=createEncounter({...options,controlProfile:'none',defender:'offensive',order:[1,0]}),[a,h]=e.actors;
  h.hp=800;stepEncounter(e,miss);stepEncounter(e,miss);a.hp=1;stepEncounter(e,full);
  assert.equal(h.hp,924);assert.equal(e.winner,1);assert.ok(e.actors.every(x=>x.controls.length===0));
  assert.equal(a.started,1);assert.equal(stepEncounter(e,full),false);
});
test('control-none uses the exact existing suppress-needed policy and defaults reject unknown options',()=>{
  const e=createEncounter({...options,controlProfile:'none'});stepEncounter(e,full);
  assert.equal(e.counts.suppress,1);assert.equal(e.counts.controlCasts,0);assert.equal(e.counts.attackPaid,4320);
  assert.throws(()=>createEncounter({...options,controlRules:'unknown'}));assert.throws(()=>createEncounter({...options,controlProfile:'unknown'}));
  assert.throws(()=>createEncounter({...options,helperControlRelease:'yes'}));
  const {periodicAttack,...defaulted}=options;
  assert.equal(createEncounter(defaulted).options.periodicAttack,'suppress_needed');
  assert.throws(()=>createEncounter({...options,periodicAttack:'suppress_always'}));
});
test('window control selector uses visible HP and existing HoT with exact65percent boundary',()=>{
  assert.equal(typeof control.chooseControlCast,'function');const target=actor(130,'T');target.maxHp=1000;target.hp=1000;target.hots=[];
  assert.equal(control.chooseControlCast({target,profile:'seal',policy:'window'}),false);
  assert.equal(control.chooseControlCast({target,profile:'seal',policy:'pressure'}),true);
  target.hp=999;assert.equal(control.chooseControlCast({target,profile:'seal',policy:'window'}),true);
  target.hots=[{remaining:1}];assert.equal(control.chooseControlCast({target,profile:'seal',policy:'window'}),false);
  target.hp=650;assert.equal(control.chooseControlCast({target,profile:'seal',policy:'window'}),true);
  target.hp=651;assert.equal(control.chooseControlCast({target,profile:'seal',policy:'window'}),false);
  target.hp=650;control.applyControl(target,actor(130,'A'),'seal',2,{save:99});
  assert.equal(control.chooseControlCast({target,profile:'seal',policy:'window'}),false);
});
test('window policy damages the full-health healer first and does not pay an early control cast',()=>{
  const e=createEncounter({...options,controlPolicy:'window'});stepEncounter(e,full);
  assert.equal(e.counts.controlCasts,0);assert.equal(e.counts.suppress,1);assert.equal(e.counts.attackPaid,4320);
  assert.throws(()=>createEncounter({...options,controlPolicy:'future'}));
});
test('unaffordable control recovers before dice and source admission denies both direct and HoT without payment',()=>{
  const e=createEncounter({...options,controlProfile:'stun'}),[a,h]=e.actors;a.energy=5759;
  stepEncounter(e,{...miss,damage:()=>{throw Error('No credit from future regeneration');}});
  assert.equal(e.counts.controlCasts,0);assert.equal(e.counts.attackPaid,0);assert.equal(e.counts.byActor.A.recover,1);
  control.applyControl(h,a,'seal',2,{save:99});h.hp=500;begin(h);const energy=h.energy;
  for(const mode of ['direct','hot'])assert.equal(applyPeriodicHeal(h,h,{mode}).accepted,false);
  assert.equal(h.energy,energy);assert.equal(h.used,false);assert.equal(h.hots.length,0);
});
test('stunned or controlled helper cannot break an ally; self-stun cannot be broken and a removed target loses controls',()=>{
  const a=actor(130,'A'),h=actor(130,'H'),t=actor(130,'T');
  control.applyControl(t,a,'seal',2,{save:99});control.applyControl(h,a,'seal',2,{save:99});begin(h);
  const energy=h.energy;assert.equal(control.breakControl(h,t,t.controls[0],{roll:()=>{throw Error('Controlled helper');}}).accepted,false);
  assert.equal(h.energy,energy);assert.equal(h.used,false);end(h);
  control.applyControl(h,a,'stun_long',2,{save:99});begin(h);
  assert.equal(control.breakControl(h,h,h.controls.find(x=>x.family==='stun'),{roll:()=>[20,1]}).accepted,false);
  t.inEncounter=false;control.expireControls(t);assert.equal(t.controls.length,0);
  assert.equal(control.applyControl(t,a,'stun',3,{save:99}),false);
});
test('none preserves the complete old trajectory with the same seed and adds only zero control counters',()=>{
  const base={healingRules:'periodic-v1',periodicMode:'combined',periodicAttack:'suppress_needed',level:5,
    effectResistance:25,cleansePolicy:'first',activeGuard:true,defender:'offensive',interrupt:false,horizon:20,seed:1246};
  for(const order of [[0,1],[1,0]]){
    const prior=runTrial({...base,order}),current=runTrial({...base,order,controlRules:'control-v1',controlProfile:'none'});
    const clean=counts=>Object.fromEntries(Object.entries(counts).filter(([key])=>!/^control|^allyControl/.test(key))
      .map(([key,value])=>[key,key==='byActor'?Object.fromEntries(Object.entries(value).map(([id,c])=>[id,clean(c)])):value]));
    assert.deepEqual({...current,counts:clean(current.counts)},prior);
    for(const [key,value] of Object.entries(current.counts))if(/^control|^allyControl/.test(key))assert.equal(value,0);
  }
});
test('control and break costs conserve individual DE while endpoint and HP accounting remain distinct',()=>{
  for(const profile of ['seal','stun','stun_long']){
    let encounter;const result=runTrial({...options,controlProfile:profile,activeGuard:true,kind:'group',helperControlRelease:true,
      order:[0,3,2,1],horizon:30,seed:1541,setup:e=>{encounter=e;}});
    for(const a of encounter.actors){const c=result.counts.byActor[a.id];
      assert.equal(a.energy,a.p.max+c.recoverGained+c.regenGained-c.attackPaid-c.healPaid-c.cleansePaid-c.guardPaid-c.controlBreakPaid);
      assert.ok(c.controlPaid<=c.attackPaid);assert.equal(c.potentialHealing,c.restored+c.overheal);
      assert.equal(c.restored,c.directRestored+c.hotRestored);
    }
  }
});
