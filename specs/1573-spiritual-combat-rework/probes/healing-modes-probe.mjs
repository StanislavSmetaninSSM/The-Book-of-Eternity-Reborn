// T120 reproducible investigation. JSONL output has no record/byte quota; keep raw outside Git.
import assert from 'node:assert/strict';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
import { rng,seedFor,round } from './healing-counters.mjs';
import { profileFor,damageTable,strength,remaining,cleanse,recover,attack } from './suppression-model.mjs';
import { price } from './reaction-model.mjs';
import { actor,begin,end,applyHeal,damage,replaceMain,healingCalculator,healingValue } from './healing-modes-model.mjs';

// Immutable inputs shared across this operation, keyed by D and attack kind.
const tables=new Map([0,130,1000].map(D=>[D,{
  ordinary:damageTable(profileFor(D),28),strong:damageTable(profileFor(D),36),large:damageTable(profileFor(D),44),suppress:damageTable(profileFor(D),24)
}]));
const calculate=healingCalculator();
export function attackProfile(strike) {
  const p={ordinary:{B:28,K:8},strong:{B:36,K:16},large:{B:44,K:24}}[strike];
  assert.ok(p,'Unknown attack strength');return p;
}
export function strikeAttack(a,target,result,strike) {
  const cost=price(a,attackProfile(strike).K);
  if(!a.active||a.used||a.collapsed||a.collapsedThisTurn||a.hp<=0||a.energy<cost)return false;
  a.energy-=cost;a.used=true;damage(target,result.damage);return true;
}
export function protectedDamageTable(D,B,extraResistance) {
  const p=profileFor(D),a2=BigInt(Math.min(40+D,2*p.X)),den=200n+3n*a2;
  // Accepted additive resistance, clamped at 90%; no rounding before final HP damage.
  const retain=(100n-BigInt(extraResistance))*den-100n*a2;
  const bounded=retain<10n*den?10n*den:retain>200n*den?200n*den:retain;
  const numerator=BigInt(B)*(200n+3n*a2)*BigInt(50+D)*bounded,denominator=1000000n*den;
  return damageTable(p,B).map(x=>({quality:x.quality,damage:x.quality===0?0:
    x.quality===3?round(numerator*BigInt(150+2*p.X),denominator*BigInt(100+p.X)):
    round(numerator,denominator*(x.quality===1?2n:1n))}));
}
const resistanceTables=new Map();
function patientDamageTables(D,resistance) {
  if(!resistance)return tables.get(D);
  const key=`${D}|${resistance}`;
  if(!resistanceTables.has(key))resistanceTables.set(key,Object.fromEntries(
    [['ordinary',28],['strong',36],['large',44],['suppress',24]].map(([kind,B])=>[kind,protectedDamageTable(D,B,resistance)])));
  return resistanceTables.get(key);
}
export function chooseMode(a,mode,maxNextDamage) {
  if(mode!=='adaptive')return mode;
  if(a.pending)return 'long';
  return a.hp>maxNextDamage&&a.energy>=price(a,24)?'long':'instant';
}
export function simulateModes({D=130,mode='instant',style='ordinary',policy='heal',reply='none',
  harmony='normal',order=[0,1],willDelta=0,healingPercent=0,resistance=0,forceInterruptFailure=false,
  n=2000,horizon=2000,seedSuffix='',strike='ordinary'}={}) {
  assert.ok(tables.has(D)&&['old','instant','long','adaptive'].includes(mode));
  assert.ok(['ordinary','interrupt','reserve','suppress','suppress_reserve'].includes(style));
  assert.ok(['heal','cleanse_safe','mix50','noheal'].includes(policy));
  assert.ok(['none','guard','counter'].includes(reply)&&['normal','healer28','both36'].includes(harmony));
  assert.deepEqual([...order].sort(),order.length===2?[0,1]:[0,1,2]);
  assert.ok(Number.isInteger(n)&&n>0&&Number.isInteger(horizon)&&horizon>0);
  assert.ok(Number.isInteger(healingPercent)&&resistance>=0&&resistance<=90);
  attackProfile(strike);assert.ok(strike==='ordinary'||!style.startsWith('suppress'),'Strong suppression is not an approved profile');
  const options={D,mode,style,policy,reply,harmony,order,willDelta,healingPercent,resistance,forceInterruptFailure,n,horizon,seedSuffix,
    ...(strike==='ordinary'?{}:{strike})};
  const seed=seedFor(JSON.stringify(options)),randomDamage=rng(seed),randomReaction=rng(seedFor(`reaction|${seed}`));
  const roll=()=>{const k=randomReaction()%400;return[Math.floor(k/20)+1,k%20+1];};
  const table=tables.get(D),maxNextDamage=Math.max(...table[strike].map(x=>x.damage));
  // Prepared once for this cell's immutable patient protection, never inside a trajectory.
  const patientTable=patientDamageTables(D,resistance);
  const counts={attacks:0,damageToB:0,suppression:0,healingMains:0,instant:0,old:0,starts:0,finishes:0,
    startAttempts:0,finishAttempts:0,abandoned:0,interruption:0,cancelled:0,cancelledStart:0,cancelledFinish:0,
    guard:0,counterAttempt:0,counterSucceeded:0,noInterruptEnergy:0,noInterruptReaction:0,
    cleanse:0,helperCleanse:0,recoverA:0,recoverB:0,recoverHelper:0,restored:0,defenderAttacks:0,
    healingWith50:0,healingWith25:0,healPaid:0,responsePaid:0,interruptPaid:0};
  const durations=[],wins=[0,0];let censored=0,attackPaid=0;
  for(let trial=0;trial<n;trial++){
    const a=actor(D,'a',{regenP:harmony==='both36'?36:null,willDelta});
    const b=actor(D,'b',{regenP:harmony==='normal'?null:harmony==='healer28'?28:36});b.healingPercent=healingPercent;
    const helper=order.length===3?actor(D,'helper',{regenP:harmony==='both36'?36:null}):null;
    const actors=[a,b,helper];let rounds=0,winner=-1;
    while(rounds<horizon&&winner<0){
      rounds++;
      for(const id of order){
        const me=actors[id];begin(me);
        if(id===0){
          const kind=style.startsWith('suppress')?'suppress':strike,cost=kind==='suppress'?a.p.suppressCost:price(a,attackProfile(strike).K);
          const reserve=style==='reserve'||style==='suppress_reserve';
          const canReserve=Math.min(a.p.max,a.energy-cost+a.regen)>=price(a,8);
          if(a.energy>=cost&&(!reserve||canReserve)){
            const result=patientTable[kind][randomDamage()%400];
            const before=b.hp;assert.ok(kind==='suppress'?attack(a,b,result,kind):strikeAttack(a,b,result,strike));damage(b,0);attackPaid+=cost;
            counts.attacks++;counts.damageToB+=before-b.hp;if(kind==='suppress')counts.suppression++;
          }else{assert.ok(recover(a));counts.recoverA++;}
        }else if(id===2){
          if(b.marks.length&&cleanse(helper,b))counts.helperCleanse++;
          else{assert.ok(recover(helper));counts.recoverHelper++;}
        }else{
          const hadPending=Boolean(b.pending);
          const wantsCleanse=policy==='cleanse_safe'&&strength(b)===50&&remaining(b)>1&&b.hp>b.maxHp/4;
          const wantsAttack=policy==='noheal'||policy==='mix50'&&b.hp>b.maxHp/2;
          if(wantsCleanse&&replaceMain(b,()=>cleanse(b,b))){counts.cleanse++;if(hadPending)counts.abandoned++;}
          else if(wantsAttack&&b.energy>=b.p.costs[0]){
            const outcome=table.ordinary[randomDamage()%400];
            assert.ok(replaceMain(b,()=>attack(b,a,outcome,'ordinary')));damage(a,0);
            counts.defenderAttacks++;if(hadPending)counts.abandoned++;
          }else{
            const chosen=chooseMode(b,mode,maxNextDamage),finishing=chosen==='long'&&Boolean(b.pending);
            const K=finishing?0:chosen==='old'?28:chosen==='long'?24:12;
            const canHeal=!wantsAttack&&(chosen==='long'||b.hp<b.maxHp)&&b.energy>=price(b,K);
            if(canHeal){
              const s=strength(b),opponent=['interrupt','reserve','suppress_reserve'].includes(style)?a:null;
              const out=applyHeal(b,b,{id:`b:${b.started}`,mode:chosen,phase:finishing?'finish':'cast',
                opponent,reply,roll,forceInterruptFailure,calculate});
              assert.ok(out.accepted);counts.healingMains++;counts[chosen==='old'?'old':chosen==='instant'?'instant':finishing?'finishAttempts':'startAttempts']++;
              counts.starts+=Number(out.started);counts.finishes+=Number(out.completed&&chosen==='long');
              counts.interruption+=Number(out.attempted);counts.cancelled+=Number(out.cancelled);
              counts.cancelledStart+=Number(out.cancelled&&chosen==='long'&&!finishing);
              counts.cancelledFinish+=Number(out.cancelled&&finishing);counts.guard+=Number(out.defended);
              counts.counterAttempt+=Number(out.counterAttempted);counts.counterSucceeded+=Number(out.countered);
              counts.noInterruptEnergy+=Number(out.noInterruptEnergy);counts.noInterruptReaction+=Number(out.noInterruptReaction);
              counts.restored+=out.restored;counts.healPaid+=out.paid;counts.responsePaid+=out.responsePaid;counts.interruptPaid+=out.interruptPaid;
              if(out.completed&&s===50)counts.healingWith50++;if(out.completed&&s===25)counts.healingWith25++;
            }else{
              assert.ok(replaceMain(b,()=>recover(b)));counts.recoverB++;if(hadPending)counts.abandoned++;
            }
          }
        }
        end(me);
        assert.ok(a.energy>=0&&b.energy>=0&&a.energy<=a.p.max&&b.energy<=b.p.max);
        if(b.hp<=0){winner=0;break;}if(a.hp<=0){winner=1;break;}
      }
    }
    if(winner<0)censored++;else wins[winner]++;
    durations.push(rounds);
  }
  durations.sort((a,b)=>a-b);
  return {...options,...(strike==='ordinary'?{}:{attackPaid}),seed,meanObserved:durations.reduce((a,b)=>a+b,0)/n,median:durations[Math.ceil(n/2)-1],
    p95:durations[Math.ceil(n*.95)-1],maxObserved:durations[n-1],over8:durations.filter(x=>x>8).length/n,
    censored,winA:wins[0]/n,winB:wins[1]/n,counts,
    actionsPerTrial:Object.fromEntries(Object.entries(counts).map(([k,v])=>[k,v/n]))};
}

const orders=[[0,1],[1,0]],modes=['instant','long','adaptive'];
export function scenarios(matrix='main') {
  const rows=[];
  if(matrix==='main'){
    for(const D of [0,130,1000])for(const order of orders)for(const harmony of ['normal','healer28','both36']){
      rows.push({D,order,harmony,mode:'old',style:'ordinary'});
      for(const mode of modes){
        rows.push({D,order,harmony,mode,style:'ordinary'});
        for(const policy of ['heal','cleanse_safe'])rows.push({D,order,harmony,mode,style:'suppress',policy});
        for(const style of ['interrupt','reserve','suppress_reserve'])for(const reply of ['none','guard','counter']){
          rows.push({D,order,harmony,mode,style,reply});
          if(style==='suppress_reserve')rows.push({D,order,harmony,mode,style,reply,policy:'cleanse_safe'});
        }
      }
    }
  }else if(matrix==='support'){
    for(const D of [130,1000])for(const harmony of ['normal','both36'])for(const mode of modes)
      for(const order of [[0,1,2],[0,2,1],[1,0,2],[1,2,0],[2,0,1],[2,1,0]])
        for(const style of ['suppress','suppress_reserve'])for(const reply of style==='suppress'?['none']:['none','guard','counter'])
          rows.push({D,harmony,mode,order,style,reply});
  }else if(matrix==='sensitivity'){
    for(const D of [0,130,1000])for(const order of orders)for(const harmony of ['normal','both36']){
      rows.push({D,order,harmony,mode:'instant',style:'ordinary',policy:'noheal'});
      for(const mode of modes){
        for(const style of ['ordinary','reserve','suppress_reserve'])rows.push({D,order,harmony,mode,style,policy:'mix50',reply:style==='ordinary'?'none':'counter'});
        for(const reply of ['guard','counter'])rows.push({D,order,harmony,mode,style:'reserve',reply,forceInterruptFailure:true});
        for(const willDelta of [-5,5])for(const reply of ['none','guard','counter'])rows.push({D,order,harmony,mode,style:'reserve',willDelta,reply});
      }
      for(const healingPercent of [25,50])for(const resistance of [0,25])rows.push({D,order,harmony,mode:'instant',style:'ordinary',healingPercent,resistance});
    }
  }else if(matrix==='confirm'){
    for(const D of [0,130,1000])for(const order of orders)for(const harmony of ['healer28','both36'])for(const mode of modes)
      for(const style of ['ordinary','reserve','suppress_reserve'])rows.push({D,order,harmony,mode,style,
        reply:style==='ordinary'?'none':'guard',policy:style==='suppress_reserve'?'cleanse_safe':'heal',seedSuffix:'confirmation'});
  }else throw Error('Unknown matrix');
  return rows;
}
if(process.argv[1]&&pathToFileURL(resolve(process.argv[1])).href===import.meta.url){
  const matrix=process.argv.find(x=>x.startsWith('--mode='))?.split('=')[1]??'main';
  const n=Number(process.argv.find(x=>x.startsWith('--samples='))?.split('=')[1]??2000);
  const rows=scenarios(matrix),started=performance.now();
  console.log(JSON.stringify({type:'metadata',version:1,matrix,samples:n,horizon:2000,cells:rows.length,node:process.version,
    adaptive:'finish pending; otherwise long iff current HP > max next ordinary hit and enough energy, else instant',
    profiles:[0,130,1000].map(D=>{const a=actor(D,'fixture');return{D,HP:a.hp,power:a.power,oldV:healingValue(a,a,22),
      instantV:healingValue(a,a,7),longV:healingValue(a,a,16),normalRegen:a.regen/100,maxDE:a.energy/100,
      averageDamage:tables.get(D).ordinary.reduce((s,x)=>s+x.damage,0)/400};})}));
  for(let index=0;index<rows.length;index++){
    const start=performance.now();const result=simulateModes({...rows[index],n});
    console.log(JSON.stringify({type:'result',index,elapsedMs:performance.now()-start,...result}));
  }
  console.log(JSON.stringify({type:'complete',matrix,cells:rows.length,trajectories:rows.length*n,elapsedMs:performance.now()-started,formulaReuse:calculate.stats}));
}
