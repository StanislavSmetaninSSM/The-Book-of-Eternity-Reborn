// #1573 T071: approved v1 suppression experiment, NOT production combat.
// All output goes to stdout; save raw results outside Git. No diagnostic quota.
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
import { rng, seedFor } from './healing-counters.mjs';
import { profileFor, damageTable, makeActor, begin, end, strength, remaining,
  heal, cleanse, recover, attack } from './suppression-model.mjs';

const fixtures=[0,50,130,210,1000].map(D=>({D,
  ordinary:damageTable(profileFor(D),28), suppression:damageTable(profileFor(D),24)}));

function choice(defender,policy) {
  const s=strength(defender);
  if(policy==='cleanse_all' && s===50)return 'cleanse';
  if(policy==='cleanse_safe' && s===50 && remaining(defender)>1 && defender.hp>defender.p.hp/4)return 'cleanse';
  if(policy==='wait' && s>0)return 'recover';
  if(policy==='mix50' && defender.hp>defender.p.hp/2)return 'attack';
  return defender.hp<defender.p.hp ? 'heal' : 'recover';
}
export function simulate({D,style='suppress',policy='heal',order=[0,1],highHarmony=false,n=10000,horizon=2000}) {
  const tables=fixtures.find(p=>p.D===D);
  if(!tables)throw new Error('Unknown fixture');
  const random=rng(seedFor(`suppression-v1|${D}|${style}|${policy}|${order.join(',')}|${highHarmony}`));
  const durations=[],wins=[0,0];let censored=0;
  const counts={attack:0,suppress:0,heal:0,cleanse:0,helperCleanse:0,defenderAttack:0,
    recoverA:0,recoverB:0,recoverHelper:0,healWith50:0,healWith25:0,healWithout:0,restored:0};
  for(let trial=0;trial<n;trial++) {
    const a=makeActor(D,'attacker'), b=makeActor(D,'defender',highHarmony);
    const helper=order.includes(2)?makeActor(D,'helper'):null;
    const actors=[a,b,helper];let winner=-1,rounds=0;
    while(rounds<horizon && winner<0) {
      rounds++;
      for(const id of order) {
        const me=actors[id];begin(me);
        if(id===0) {
          const kind=style==='upkeep'?(strength(b)===0?'suppress':'ordinary'):style;
          const cost=kind==='ordinary'?a.p.costs[0]:a.p.suppressCost;
          if(a.energy>=cost) {
            const outcome=(kind==='ordinary'?tables.ordinary:tables.suppression)[random()%400];
            attack(a,b,outcome,kind);counts.attack++;if(kind==='suppress')counts.suppress++;
          } else {recover(a);counts.recoverA++;}
        } else if(id===2) {
          if(b.marks.length>0 && cleanse(helper,b))counts.helperCleanse++;
          else {recover(helper);counts.recoverHelper++;}
        } else {
          const action=choice(b,policy);
          if(action==='cleanse' && cleanse(b,b))counts.cleanse++;
          else if(action==='heal' && b.energy>=b.p.costs[3]) {
            const before=b.hp,s=strength(b);heal(b);counts.heal++;counts.restored+=b.hp-before;
            counts[s===50?'healWith50':s===25?'healWith25':'healWithout']++;
          } else if(action==='attack' && b.energy>=b.p.costs[0]) {
            attack(b,a,tables.ordinary[random()%400],'ordinary');counts.defenderAttack++;
          } else {recover(b);counts.recoverB++;}
        }
        end(me);
        if(b.hp<=0){winner=0;break;}if(a.hp<=0){winner=1;break;}
      }
    }
    if(winner<0)censored++;else wins[winner]++;
    durations.push(rounds);
  }
  durations.sort((a,b)=>a-b);
  return {D,style,policy,order,highHarmony,n,horizon,meanObserved:durations.reduce((a,b)=>a+b,0)/n,
    median:durations[Math.ceil(n/2)-1],p95:durations[Math.ceil(n*.95)-1],maxObserved:durations[n-1],
    censored,winA:wins[0]/n,winB:wins[1]/n,
    actions:Object.fromEntries(Object.entries(counts).map(([k,v])=>[k,v/n]))};
}

if(process.argv[1] && pathToFileURL(resolve(process.argv[1])).href===import.meta.url) {
  const started=performance.now(), rows=[];
  const groupOnly=process.argv.includes('--groups');
  if(!groupOnly) {
    for(const D of [0,50,130,210,1000])for(const order of [[0,1],[1,0]])for(const highHarmony of [false,true]) {
      for(const style of ['ordinary','placebo'])for(const policy of ['heal','mix50']) {
        rows.push(simulate({D,style,policy,order,highHarmony}));
      }
      for(const style of ['suppress','upkeep'])for(const policy of ['heal','cleanse_all','cleanse_safe','wait','mix50']) {
        rows.push(simulate({D,style,policy,order,highHarmony}));
      }
    }
  } else {
    // Exact 1v2 upper-bound support experiment: helper cannot be attacked and only cleanses/recovers.
    for(const D of [130,1000])for(const highHarmony of [false,true]) {
      for(const order of [[0,1,2],[0,2,1],[1,0,2],[1,2,0],[2,0,1],[2,1,0]]) {
        for(const style of ['ordinary','suppress'])rows.push(simulate({D,style,policy:'heal',order,highHarmony}));
      }
    }
  }
  console.log(JSON.stringify({version:1,groupOnly,samples:10000,horizon:2000,elapsedMs:performance.now()-started,rows},null,2));
}
