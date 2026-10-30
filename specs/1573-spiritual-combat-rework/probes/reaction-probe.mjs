// T116 reproducible investigation. Raw JSONL belongs outside Git, no output quota.
import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
import {rng,seedFor} from './healing-counters.mjs';
import {profileFor,damageTable,strength,remaining,cleanse,recover,attack} from './suppression-model.mjs';
import {createActor,beginTurn,endTurn,attemptHeal,price,checkedQuality} from './reaction-model.mjs';

// Immutable tables shared by every trial and scenario in this operation.
const tables=new Map([0,50,130,210,1000].map(D=>[D,{ordinary:damageTable(profileFor(D),28),suppress:damageTable(profileFor(D),24)}]));
export function simulate({D=130,style='reserve',policy='heal',reply='none',harmony='normal',order=[0,1],willDelta=0,n=2000,horizon=2000,seedSuffix=''}={}) {
  assert.ok(tables.has(D));assert.ok(['ordinary','interrupt','reserve','suppress','suppress_reserve'].includes(style));
  assert.ok(['heal','cleanse_safe','mix50','noheal'].includes(policy));
  assert.ok(['none','guard','counter'].includes(reply));assert.ok(['normal','healer28','both36'].includes(harmony));
  assert.deepEqual([...order].sort(),order.length===2?[0,1]:[0,1,2]);
  assert.ok(Number.isInteger(n)&&n>0&&Number.isInteger(horizon)&&horizon>0);
  const options={D,style,policy,reply,harmony,order,willDelta,n,horizon,seedSuffix};
  const seed=seedFor(JSON.stringify(options)),randomDamage=rng(seed),randomReaction=rng(seedFor(`reaction|${seed}`));
  const roll=()=>{const k=randomReaction()%400;return [Math.floor(k/20)+1,k%20+1];};
  const count={attacks:0,suppression:0,heals:0,interruption:0,cancelled:0,guard:0,counterAttempt:0,counterSucceeded:0,
    noInterruptEnergy:0,noInterruptReaction:0,cleanse:0,helperCleanse:0,recoverA:0,recoverB:0,recoverHelper:0,
    restored:0,defenderAttacks:0,healingWith50:0,healingWith25:0};
  const durations=[],wins=[0,0];let censored=0;
  for(let trial=0;trial<n;trial++){
    const a=createActor(D,'a',{regenP:harmony==='both36'?36:null,willDelta});
    const b=createActor(D,'b',{regenP:harmony==='normal'?null:harmony==='healer28'?28:36});
    const helper=order.length===3?createActor(D,'helper',{regenP:harmony==='both36'?36:null}):null;
    const actors=[a,b,helper];let rounds=0,winner=-1;
    while(rounds<horizon&&winner<0){
      rounds++;
      for(const id of order){
        const me=actors[id];beginTurn(me);
        if(id===0){
          const kind=style.startsWith('suppress')?'suppress':'ordinary';
          const cost=kind==='suppress'?a.p.suppressCost:a.p.costs[0];
          const reserve=style==='reserve'||style==='suppress_reserve';
          const canReserve=Math.min(a.p.max,a.energy-cost+a.regen)>=price(a,8);
          if(a.energy>=cost&&(!reserve||canReserve)){
            const result=tables.get(D)[kind][randomDamage()%400];
            assert.ok(attack(a,b,result,kind));count.attacks++;if(kind==='suppress')count.suppression++;
          }else{assert.ok(recover(a));count.recoverA++;}
        }else if(id===2){
          if(b.marks.length&&cleanse(helper,b))count.helperCleanse++;
          else{assert.ok(recover(helper));count.recoverHelper++;}
        }else{
          const wantsCleanse=policy==='cleanse_safe'&&strength(b)===50&&remaining(b)>1&&b.hp>b.p.hp/4;
          const wantsAttack=policy==='noheal'||policy==='mix50'&&b.hp>b.p.hp/2;
          if(wantsCleanse&&cleanse(b,b))count.cleanse++;
          else if(wantsAttack&&b.energy>=b.p.costs[0]){
            const result=tables.get(D).ordinary[randomDamage()%400];assert.ok(attack(b,a,result,'ordinary'));
            count.defenderAttacks++;if(a.hp<=0&&a.active)a.collapsedThisTurn=true;
          }else if(!wantsAttack&&b.hp<b.p.hp&&b.energy>=b.p.costs[3]){
            const s=strength(b),opponent=['interrupt','reserve','suppress_reserve'].includes(style)?a:null;
            const out=attemptHeal(b,opponent,{reply,roll});assert.ok(out.accepted);count.heals++;
            if(s===50)count.healingWith50++;if(s===25)count.healingWith25++;
            count.interruption+=Number(out.attempted);count.cancelled+=Number(out.cancelled);
            count.guard+=Number(out.defended);count.counterAttempt+=Number(out.counterAttempted);
            count.counterSucceeded+=Number(out.countered);count.noInterruptEnergy+=Number(out.noInterruptEnergy);
            count.noInterruptReaction+=Number(out.noInterruptReaction);count.restored+=out.restored;
          }else{assert.ok(recover(b));count.recoverB++;}
        }
        endTurn(me); // The victorious already-started turn closes once; no artificial further turns.
        if(b.hp<=0){winner=0;break;}if(a.hp<=0){winner=1;break;}
      }
    }
    if(winner<0)censored++;else wins[winner]++;
    durations.push(rounds);
  }
  durations.sort((a,b)=>a-b);
  return {...options,seed,meanObserved:durations.reduce((a,b)=>a+b,0)/n,median:durations[Math.ceil(n/2)-1],
    p95:durations[Math.ceil(n*.95)-1],over8:durations.filter(r=>r>8).length/n,maxObserved:durations[n-1],
    censored,winA:wins[0]/n,winB:wins[1]/n,counts:count,
    actionsPerTrial:Object.fromEntries(Object.entries(count).map(([k,v])=>[k,v/n]))};
}

export function scenarios(mode='main'){
  const result=[];
  if(mode==='main'){
    for(const D of [0,130,1000])for(const order of [[0,1],[1,0]])for(const harmony of ['normal','healer28','both36']){
      for(const style of ['ordinary','suppress'])for(const policy of ['heal','cleanse_safe'])result.push({D,order,harmony,style,policy});
      for(const style of ['interrupt','reserve','suppress_reserve'])for(const policy of ['heal','cleanse_safe'])for(const reply of ['none','guard','counter'])result.push({D,order,harmony,style,policy,reply});
    }
  }else if(mode==='support'){
    for(const D of [130,1000])for(const harmony of ['normal','both36'])for(const order of [[0,1,2],[0,2,1],[1,0,2],[1,2,0],[2,0,1],[2,1,0]])
      for(const style of ['suppress','suppress_reserve'])for(const reply of style==='suppress'?['none']:['none','guard','counter'])result.push({D,harmony,order,style,reply});
  }else if(mode==='sensitivity'){
    for(const D of [0,130,1000])for(const harmony of ['normal','both36'])for(const order of [[0,1],[1,0]]){
      result.push({D,harmony,order,style:'ordinary',policy:'noheal'});
      for(const style of ['ordinary','reserve','suppress_reserve'])result.push({D,harmony,order,style,policy:'mix50',reply:style==='ordinary'?'none':'counter'});
      for(const willDelta of [-5,5])for(const reply of ['none','guard','counter'])result.push({D,harmony,order,style:'reserve',willDelta,reply});
    }
  }else throw Error('Unknown matrix');
  return result;
}

if(process.argv[1]&&pathToFileURL(resolve(process.argv[1])).href===import.meta.url){
  const mode=process.argv.find(x=>x.startsWith('--mode='))?.split('=')[1]??'main';
  const n=Number(process.argv.find(x=>x.startsWith('--samples='))?.split('=')[1]??2000);
  const selected=mode==='confirm'?
    [0,130,1000].flatMap(D=>['normal','both36'].flatMap(harmony=>['ordinary','reserve','suppress_reserve'].flatMap(style=>
      (style==='ordinary'?['none']:['none','guard','counter']).map(reply=>({D,harmony,style,reply,policy:style==='suppress_reserve'?'cleanse_safe':'heal',seedSuffix:'confirmation'}))))):scenarios(mode);
  const started=performance.now();
  console.log(JSON.stringify({type:'metadata',version:1,mode,samples:n,horizon:2000,cells:selected.length,node:process.version,
    profiles:[0,130,1000].map(D=>{const p=profileFor(D);return{D,X:p.X,HP:p.hp,healV:p.heal,maxDE:p.max/100,normalRegen:p.regen/100,normalH:p.H,highH:120*(1+D/50),bothH:160*(1+D/50)};}),
    exactInterruption:[0,130,1000].map(D=>{const F=profileFor(D).X;let full=0,guard=0;for(let a=1;a<=20;a++)for(let b=1;b<=20;b++){full+=Number(checkedQuality(a,b,F)>=2);guard+=Number(checkedQuality(a,b,F,-5)>=2);}return{D,fullOver400:full,guardOver400:guard,counterEffectiveProbability:(full/400)*(1-full/400)};})}));
  for(let index=0;index<selected.length;index++){
    const cellStart=performance.now(),row=simulate({...selected[index],n});
    console.log(JSON.stringify({type:'result',index,elapsedMs:performance.now()-cellStart,...row}));
  }
  console.log(JSON.stringify({type:'complete',mode,cells:selected.length,trajectories:selected.length*n,elapsedMs:performance.now()-started}));
}
