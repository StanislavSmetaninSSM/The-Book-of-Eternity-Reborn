import test from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {normalizeJointMarks,forecastJointMark,forecastJointPhase,createJointBudgetCache,
  chooseJointMain,chooseJointReaction} from './healing-joint-budget-model.mjs';
import {createEncounter,createOperation,stepEncounter} from './healing-tactics-model.mjs';
import {simulateTactics} from './healing-tactics-probe.mjs';
import {mark as applyMark} from './suppression-model.mjs';
import {begin,end,actor} from './healing-modes-model.mjs';

const none=Object.freeze({amount:0,remaining:0});
function profile(overrides={}){
  return {maxEnergy:10,regen:0,recoverGain:4,costs:{ordinary:2,strong:4,large:6,suppress:3,interrupt:2},
    attacks:Object.fromEntries([['ordinary',4],['strong',5],['large',6],['suppress',3]]
      .map(([kind,damage])=>[kind,[{quality:kind==='suppress'?2:1,probability:1,damage}]])),
    healing:{instant:{0:0,25:0,50:0},long:{0:0,25:0,50:0}},interruptSuccess:0,...overrides};
}
const mainView=(overrides={})=>Object.freeze({canAct:true,energy:10,mark:none,phase:'instant',...overrides});
const reactionView=(overrides={})=>Object.freeze({canAct:true,hasReaction:true,energy:10,mark:none,
  phase:'instant',missingHp:100,...overrides});
const noDice={damage:()=>{assert.fail('No funded attack');},reaction:()=>{assert.fail('No funded reaction');}};

test('mark normalization distinguishes inactive target from current Heal opportunity and rejects multiple sources',()=>{
  const marks=[{source:'A',amount:50,expires:7}];
  assert.deepEqual(normalizeJointMarks(marks,6,false),{amount:50,remaining:1});
  assert.deepEqual(normalizeJointMarks(marks,6,true),{amount:50,remaining:2});
  assert.deepEqual(normalizeJointMarks(marks,7,true),{amount:50,remaining:1});
  assert.deepEqual(normalizeJointMarks(marks,7,false),none);
  assert.throws(()=>normalizeJointMarks([...marks,{source:'C',amount:25,expires:9}],6,false));
});

test('forecast mark renewal matches actual suppression and expiry at target boundaries',()=>{
  const target=actor(130,'B');applyMark(target,'A',2);
  let forecast=forecastJointMark(none,2);assert.deepEqual(forecast,{amount:50,remaining:2});
  begin(target);assert.deepEqual(normalizeJointMarks(target.marks,target.started,true),forecast);
  end(target);assert.deepEqual(normalizeJointMarks(target.marks,target.started,false),{amount:50,remaining:1});
  forecast=forecastJointMark({amount:50,remaining:1},1);applyMark(target,'A',1);
  assert.deepEqual(forecast,{amount:50,remaining:1});
  assert.deepEqual(normalizeJointMarks(target.marks,target.started,false),forecast);
  forecast=forecastJointMark(forecast,2);applyMark(target,'A',2);
  assert.deepEqual(forecast,{amount:50,remaining:2});
  assert.deepEqual(normalizeJointMarks(target.marks,target.started,false),forecast);
  assert.deepEqual(forecastJointMark(none,1),{amount:25,remaining:1});
  assert.deepEqual(forecastJointMark(forecast,0),forecast);
});

test('forecast phase transitions preserve long start cancellation and finish consumption',()=>{
  for(const [phase,cancelled,want] of [['instant',false,'instant'],['instant',true,'instant'],
    ['cast',false,'finish'],['cast',true,'cast'],['finish',false,'cast'],['finish',true,'cast']])
    assert.equal(forecastJointPhase(phase,cancelled),want);
});

test('terminal credit prices the avoided ordinary payment and differentiates equal immediate Recover decisions',()=>{
  const curve=createJointBudgetCache().prepare(profile());
  assert.equal(curve.lambda,2/3);
  assert.equal(curve.value(mainView({energy:1}),0),2/3);
  assert.equal(curve.value(mainView({energy:0}),0),0);
  assert.equal(chooseJointMain(curve,mainView({energy:0})).kind,'recover');
  assert.equal(chooseJointMain(curve,mainView({energy:1})).kind,'recover');
  assert.ok(curve.value(mainView({energy:1}),3)>curve.value(mainView({energy:0}),3));
  const out=chooseJointReaction(curve,reactionView({energy:2}),'joint');
  assert.equal(out.attempt,false);assert.ok(out.skipScore>out.attemptScore);
});

test('end regeneration funds future windows only and hand-derived terminal values include it once',()=>{
  const curve=createJointBudgetCache().prepare(profile({regen:4,recoverGain:0}));
  assert.equal(curve.lambda,2);
  assert.equal(curve.value(mainView({energy:0}),1),8);
  assert.equal(curve.value(mainView({energy:1}),1),10);
  assert.equal(chooseJointMain(curve,mainView({energy:1})).kind,'recover');
});

test('literal budget fixtures select every Main without a fixed reserve',()=>{
  for(const [wanted,damage] of [['ordinary',4],['strong',10],['large',20],['suppress',30]]){
    const data=profile({regen:10});
    for(const kind of Object.keys(data.attacks))data.attacks[kind]=[{quality:1,probability:1,damage:kind===wanted?damage:0}];
    const curve=createJointBudgetCache().prepare(data);
    assert.equal(chooseJointMain(curve,mainView()).kind,wanted);
  }
  const curve=createJointBudgetCache().prepare(profile({regen:10}));
  assert.equal(chooseJointMain(curve,mainView({energy:1})).kind,'recover');
  assert.equal(chooseJointMain(curve,mainView({energy:2})).kind,'ordinary');
});

test('reaction equality, skip ties, current HP cap and future energy use pure visible inputs',()=>{
  const data=profile({regen:10,interruptSuccess:1,healing:{instant:{0:100,25:75,50:50},long:{0:200,25:150,50:100}}});
  const curve=createJointBudgetCache().prepare(data);
  const guarded=v=>new Proxy(v,{get(target,key){assert.ok(Object.hasOwn(target,key),`Hidden input ${String(key)}`);return target[key];}});
  assert.equal(chooseJointReaction(curve,guarded(reactionView({energy:2})),'joint').attempt,true);
  assert.equal(chooseJointReaction(curve,reactionView({energy:1,regen:99999}),'joint').reason,'no_energy');
  assert.equal(chooseJointReaction(curve,reactionView({missingHp:0}),'joint').attempt,false);
  assert.equal(chooseJointReaction(curve,reactionView({canAct:false,hasReaction:false,energy:0}),'always').reason,'inactive');
  assert.equal(chooseJointReaction(curve,reactionView({hasReaction:false,energy:0}),'always').reason,'no_reaction');
  assert.equal(chooseJointReaction(curve,reactionView(),'never').reason,'policy_never');
  assert.equal(chooseJointReaction(curve,reactionView(),'always').attempt,true);
  const tie=createJointBudgetCache().prepare(profile({regen:10,costs:{ordinary:2,strong:4,large:6,suppress:3,interrupt:0}}));
  assert.equal(chooseJointReaction(tie,reactionView(),'joint').attempt,false);
  const v=mainView(),before=JSON.stringify(v);chooseJointMain(curve,guarded(v));assert.equal(JSON.stringify(v),before);
});

test('planner consumes suppression renewal and expiry in its three-Main score, beyond immediate damage',()=>{
  const data=profile({regen:10,healing:{instant:{0:100,25:75,50:50},long:{0:200,25:150,50:100}}});
  data.attacks.suppress=[{quality:2,probability:1,damage:0}];
  const curve=createJointBudgetCache().prepare(data),out=chooseJointMain(curve,mainView());
  // Suppress, large, suppress:6damage - 3*50Heal + terminal10*(4/6).
  assert.equal(out.kind,'suppress');assert.ok(Math.abs(out.score+412/3)<1e-10);
});

test('planner subtracts long finish healing once per two windows and keeps Main ties on the cheaper action',()=>{
  const data=profile({regen:10,healing:{instant:{0:100,25:75,50:50},long:{0:200,25:150,50:100}}});
  for(const kind of Object.keys(data.attacks))data.attacks[kind]=[{quality:0,probability:1,damage:0}];
  const curve=createJointBudgetCache().prepare(data);
  for(const [h,cast,finish] of [[1,0,-200],[2,-200,-200],[3,-200,-400]]){
    assert.equal(curve.value(mainView({phase:'cast'}),h),cast);
    assert.equal(curve.value(mainView({phase:'finish'}),h),finish);
  }
  assert.equal(chooseJointMain(curve,mainView()).kind,'recover');
});

test('operation reuse preserves complete immutable profile identity and fresh state lookup',()=>{
  const cache=createJointBudgetCache(),data=profile(),a=cache.prepare(data),b=cache.prepare(profile());
  assert.equal(a,b);assert.equal(cache.stats.computed,1);assert.equal(cache.stats.hits,1);
  for(const mutate of [p=>{p.regen++;},p=>{p.maxEnergy++;},p=>{p.recoverGain++;},
    ...['ordinary','strong','large','suppress','interrupt'].map(k=>p=>{p.costs[k]++;}),
    p=>{p.attacks.large[0].damage++;},p=>{p.attacks.suppress[0].quality=3;},
    p=>{p.attacks.ordinary=[{quality:0,probability:.5,damage:0},{quality:1,probability:.5,damage:4}];},
    ...['instant','long'].flatMap(k=>[0,25,50].map(s=>p=>{p.healing[k][s]++;})),p=>{p.interruptSuccess=.5;}]){
    const changed=profile();mutate(changed);assert.notEqual(cache.prepare(changed),a);
  }
  data.attacks.ordinary[0].damage=999;assert.equal(a.profile.attacks.ordinary[0].damage,4);
  assert.ok(Object.isFrozen(a.profile.attacks.ordinary[0]));assert.equal(createJointBudgetCache().stats.computed,0);
});

test('joint options reject unsupported scope, competing options and unknown policy',()=>{
  for(const options of [{jointPolicy:'typo'},{jointPolicy:'joint',kind:'support'},
    {jointPolicy:'always',kind:'group'},{jointPolicy:'never',interruptPolicy:'never'}])
    assert.throws(()=>createEncounter(options));
});

test('joint real costs precede dice; current shortfall cannot borrow own end regeneration',()=>{
  for(const order of [[0,1],[1,0]]){
    const e=createEncounter({jointPolicy:'always',order,reply:'guard'}),[a,h]=e.actors;
    assert.equal(a.jointObservedMode,'instant');h.hp=400;
    if(order[0]===0){a.energy=2879;const before=a.energy;stepEncounter(e,noDice);
      assert.equal(e.counts.recover,1);assert.equal(a.energy,Math.min(a.p.max,before+a.p.gain+a.regen));
    }else{
      a.energy=2880;const before=h.energy;let calls=0;
      stepEncounter(e,{damage:()=>0,reaction:()=>{calls++;assert.equal(a.energy,0);assert.equal(a.reaction,0);
        assert.equal(h.energy,before-4320-1440);return[1,20];}});
      assert.equal(calls,1);assert.equal(e.counts.jointDecision_attempt,1);assert.equal(e.counts.interruptPaid,2880);
    }
  }
});

test('last observed canceled long intent and actual pending determine forecast without future chooseMode',()=>{
  const e=createEncounter({jointPolicy:'always',order:[1,0],mode:'long',reply:'none'}),[a,h]=e.actors;h.hp=400;
  assert.equal(a.jointObservedMode,'instant');
  stepEncounter(e,{damage:()=>0,reaction:()=>[20,1]});assert.equal(a.jointObservedMode,'long');assert.equal(h.pending,null);
  assert.equal(e.counts.cancelled,1);
  const f=createEncounter({jointPolicy:'never',order:[1,0],mode:'long',healingPercent:50,resistance:25});f.actors[1].hp=400;
  stepEncounter(f,noDice);assert.equal(f.actors[0].jointObservedMode,'long');assert.ok(f.actors[1].pending);
  assert.equal(f.counts.healPaid,8640);assert.equal(f.counts.jointDecision_policy_never,1);
  assert.equal(f.jointCurve.lambda,85.7825/8640);
  // Will62 has critical threshold19 (40 outcomes), plus36 noncritical guarded wins:76/400.
  assert.equal(f.jointCurve.profile.interruptSuccess,.19);
  assert.deepEqual(f.jointCurve.profile.healing,{instant:{0:108,25:81,50:54},long:{0:247,25:185,50:124}});
});

test('all reaction ablations preserve the same Main mapping and pay the chosen attack before dice',()=>{
  const operation=createOperation(),decisions=[];
  for(const jointPolicy of ['never','always','joint']){
    const e=createEncounter({jointPolicy,order:[0,1],harmony:'healer28',healingPercent:50,resistance:25,operation});
    const [a]=e.actors;let calls=0;
    stepEncounter(e,{damage:()=>{calls++;assert.equal(a.used,true);assert.equal(a.energy,16080);return 0;},reaction:()=>[1,20]});
    assert.equal(calls,1);assert.equal(e.counts.byActor.A.suppress,1);assert.equal(e.counts.jointMainDecisions,1);
    decisions.push(e.counts.byActor.A.attackPaid);
  }
  assert.deepEqual(decisions,[4320,4320,4320]);assert.equal(operation.jointStats.computed,1);
  assert.equal(operation.jointStats.hits,2);
  const fresh=createEncounter({jointPolicy:'joint',operation});assert.equal(fresh.actors[0].jointObservedMode,'instant');
  assert.notEqual(fresh.actors[0],createEncounter({jointPolicy:'joint',operation}).actors[0]);
  const legacy=createOperation();assert.equal(Object.hasOwn(legacy,'jointStats'),false);
});

test('fresh reaction admission rejects loss of reaction after a cached decision',()=>{
  const operation=createOperation(),e=createEncounter({jointPolicy:'always',order:[1,0],operation}),[a,h]=e.actors;
  const calculate=operation.calculate;h.hp=400;
  operation.calculate=(...args)=>{a.reaction=0;return calculate(...args);};stepEncounter(e,noDice);
  assert.equal(e.counts.jointDecision_attempt,1);assert.equal(e.counts.interruptions,0);assert.equal(a.energy,a.p.max);
});

test('applyHeal rechecks resources after selection and revival keeps the existing reaction',()=>{
  const op=createOperation(),e=createEncounter({jointPolicy:'always',order:[1,0],operation:op}),[a,h]=e.actors;
  const calculate=op.calculate;a.energy=2880;h.hp=400;
  op.calculate=(...args)=>{a.energy=2879;return calculate(...args);};stepEncounter(e,noDice);
  assert.equal(e.counts.jointDecision_attempt,1);assert.equal(e.counts.interruptions,0);assert.equal(a.reaction,1);
  const f=createEncounter({jointPolicy:'always',order:[1,0],reply:'none'}),[revived,healer]=f.actors;
  revived.active=true;revived.collapsedThisTurn=true;revived.energy=2880;healer.hp=400;
  stepEncounter(f,{damage:()=>0,reaction:()=>[1,20]});assert.equal(f.counts.interruptions,1);
  assert.equal(revived.collapsedThisTurn,true);assert.equal(revived.used,false);
});

test('legacy T123 and representative T124 results keep exact output and schema',()=>{
  const rows=[];
  for(const order of [[0,1],[1,0]])for(const mode of ['instant','long','adaptive'])for(const interrupt of [true,false])
    rows.push(simulateTactics({n:3,horizon:20,order,mode,interrupt}));
  for(const order of [[0,1],[1,0]])for(const mode of ['instant','long','adaptive'])
    for(const interruptPolicy of ['never','always','reserve_strong','reserve_large','release_reserve_large'])
      rows.push(simulateTactics({n:3,horizon:20,order,mode,interruptPolicy,healingPercent:50,resistance:25,harmony:'healer28'}));
  assert.equal(createHash('sha256').update(JSON.stringify(rows)).digest('hex'),
    '1e11934e9e7e958e11fed7fd8331c8f9117fad88640767cc0777180603756d7c');
  assert.ok(rows.every(row=>!Object.hasOwn(row.counts,'jointDecisions')));
});
