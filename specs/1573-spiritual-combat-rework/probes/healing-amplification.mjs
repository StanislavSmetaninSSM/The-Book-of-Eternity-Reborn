// T122: unchanged accepted healing versus established countermeasures, plus utility fixtures.
import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
import {simulateModes,protectedDamageTable} from './healing-modes-probe.mjs';
export {attackProfile} from './healing-modes-probe.mjs';
import {actor,begin,end,damage,applyHeal} from './healing-modes-model.mjs';
import {mark} from './suppression-model.mjs';

export function utilityReport() {
  const critical=Math.max(...protectedDamageTable(130,28,0).map(x=>x.damage));assert.equal(critical,382);
  const cast=(a,t,mode,phase='cast')=>applyHeal(a,t,{id:`${a.id}:${a.started}:${phase}`,mode,phase});
  const burst={damage:critical,withoutHealing:Math.max(0,320-critical)};
  {const a=actor(130,'burst');a.hp=320;begin(a);cast(a,a,'instant');damage(a,critical);burst.instant=a.hp;}
  const twoHits={main:2};
  for(const mode of ['instant','long']){
    const a=actor(130,'two',{regenP:36});a.hp=600;let paid=0;
    for(let turn=0;turn<2;turn++){
      begin(a);paid+=cast(a,a,mode,mode==='long'&&turn===1?'finish':'cast').paid;
      end(a);damage(a,critical);
    }
    twoHits[mode]=a.hp;twoHits[mode+'Paid']=paid/100;
  }
  const revival=[];
  for(const mode of ['instant','long'])for(const bonus of [0,25,50])for(const suppression of [0,50]){
    const h=actor(130,'helper',{regenP:36}),t=actor(130,'patient');h.healingPercent=bonus;damage(t,t.hp);
    if(suppression)mark(t,'enemy',2);
    let mains=0,paid=0,firstHeal=null;
    while(t.collapsed){
      begin(h);const result=cast(h,t,mode,h.pending?'finish':'cast');assert.ok(result.accepted);
      mains++;paid+=result.paid;if(result.completed&&firstHeal===null)firstHeal=result.restored;end(h);
      assert.ok(mains<=10,'Finite hand fixture must reach threshold');
    }
    revival.push({mode,bonus,suppression,threshold:103,firstHeal,mainsToRevival:mains,paidDE:paid/100,finalHP:t.hp});
  }
  return {type:'utility',D:130,burst,twoHits,revival,
    limit:'Ally revival is an isolated fixture, not a whole combat outcome; patient gets no new personal slot.'};
}

const modes=['instant','long','adaptive'],orders=[[0,1],[1,0]];
const configurations=[{healingPercent:0,resistance:0},{healingPercent:25,resistance:25},{healingPercent:50,resistance:25}];
function tactics(){
  const rows=[{style:'ordinary'},{style:'ordinary',strike:'strong'},{style:'ordinary',strike:'large'}];
  for(const reply of ['none','guard','counter'])rows.push({style:'reserve',reply});
  for(const reply of ['guard','counter'])rows.push({style:'reserve',strike:'strong',reply});
  for(const policy of ['heal','cleanse_safe']){
    rows.push({style:'suppress',policy});
    for(const reply of ['guard','counter'])rows.push({style:'suppress_reserve',reply,policy});
  }
  return rows;
}
export function amplificationScenarios(matrix) {
  const rows=[];
  if(matrix==='main'){
    for(const mode of modes)for(const order of orders)for(const harmony of ['normal','healer28','both36'])
      for(const build of configurations)for(const tactic of tactics())rows.push({D:130,mode,order,harmony,...build,...tactic});
  }else if(matrix==='support'){
    for(const mode of modes)for(const build of configurations.slice(1))for(const order of [[0,1,2],[0,2,1],[1,0,2],[1,2,0],[2,0,1],[2,1,0]])
      for(const reply of ['none','guard','counter'])rows.push({D:130,harmony:'both36',mode,order,...build,
        style:reply==='none'?'suppress':'suppress_reserve',reply});
  }else if(matrix==='scaling'){
    for(const D of [0,1000])for(const mode of modes)for(const order of orders)
      for(const tactic of [{style:'ordinary'},{style:'ordinary',strike:'strong'},{style:'ordinary',strike:'large'},
        {style:'reserve',strike:'strong',reply:'guard'},{style:'suppress_reserve',reply:'guard',policy:'cleanse_safe'}])
        rows.push({D,mode,order,harmony:'both36',...configurations[2],...tactic});
  }else if(matrix==='confirm'){
    for(const mode of modes)for(const order of orders)for(const build of configurations.slice(1))
      for(const tactic of [{style:'reserve',strike:'strong',reply:'guard'},{style:'reserve',strike:'strong',reply:'counter'},
        {style:'suppress_reserve',reply:'guard',policy:'cleanse_safe'}])
        rows.push({D:130,mode,order,harmony:'both36',...build,...tactic,seedSuffix:'T122-confirmation'});
  }else throw Error('Unknown amplification matrix');
  return rows;
}
if(process.argv[1]&&pathToFileURL(resolve(process.argv[1])).href===import.meta.url){
  const matrix=process.argv.find(x=>x.startsWith('--mode='))?.split('=')[1]??'main';
  const n=Number(process.argv.find(x=>x.startsWith('--samples='))?.split('=')[1]??1000),started=performance.now();
  if(matrix==='utility')console.log(JSON.stringify(utilityReport()));
  else{
    const rows=amplificationScenarios(matrix);
    console.log(JSON.stringify({type:'metadata',version:1,matrix,cells:rows.length,samples:n,horizon:2000,node:process.version,
      invariant:'T119 healing values unchanged; additive resistance before single rounding; existing attack B/cost only'}));
    for(let index=0;index<rows.length;index++){
      const start=performance.now(),result=simulateModes({...rows[index],n});
      console.log(JSON.stringify({type:'result',index,elapsedMs:performance.now()-start,...result}));
    }
    console.log(JSON.stringify({type:'complete',matrix,cells:rows.length,trajectories:rows.length*n,elapsedMs:performance.now()-started}));
  }
}
