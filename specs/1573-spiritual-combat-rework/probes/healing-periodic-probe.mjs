// T128 fixed investigation. JSONL stdout; diagnostics belong outside Git without quotas.
import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
import {createOperation} from './healing-tactics-model.mjs';
import {simulateTactics} from './healing-tactics-probe.mjs';
import {periodicProfiles} from './healing-periodic-model.mjs';

const builds=Object.freeze({normal:{harmony:'normal',healingPercent:0,resistance:0},
  fortified_healer:{harmony:'healer28',healingPercent:50,resistance:25},
  fortified_both:{harmony:'both36',healingPercent:50,resistance:25}});
export function periodicScenarios(matrix='main') {
  assert.ok(['main','confirm'].includes(matrix),'Unknown T128 matrix');const rows=[];
  function add(slice,build,mode,attack,R,cleanse,D=130,level=5,defender='passive'){
    for(const activeGuard of [false,true])for(const order of [[0,1],[1,0]])rows.push({
      slice,benchmarkProfile:build,D,kind:'duel',...builds[build],healingRules:'periodic-v1',periodicMode:mode,
      periodicAttack:attack,level,effectResistance:R,cleansePolicy:cleanse,activeGuard,order:[...order],
      defender,interrupt:false,seedSuffix:matrix==='main'?'T128-main-v1':'T128-confirm-v1'
    });
  }
  for(const build of Object.keys(builds))for(const mode of ['direct','hot','combined']){
    add('core',build,mode,'ordinary',0,'never');
    for(const attack of ['suppress_always','suppress_needed'])for(const R of [0,25,50])for(const cleanse of ['never','first'])
      add('core',build,mode,attack,R,cleanse);
  }
  for(const build of Object.keys(builds))for(const attack of ['suppress_always','suppress_needed'])for(const cleanse of ['never','first'])
    add('r90',build,'combined',attack,90,cleanse);
  for(const level of [1,3])for(const build of ['normal','fortified_healer'])for(const R of [0,25])for(const cleanse of ['never','first'])
    add('tiers',build,'combined','suppress_always',R,cleanse,130,level);
  for(const build of ['normal','fortified_both'])for(const R of [0,25])for(const cleanse of ['never','first'])
    add('scaling',build,'combined','suppress_always',R,cleanse,1000);
  for(const build of ['normal','fortified_both']){
    add('offensive',build,'combined','ordinary',0,'never',130,5,'offensive');
    for(const attack of ['suppress_always','suppress_needed'])for(const R of [0,25])for(const cleanse of ['never','first'])
      add('offensive',build,'combined',attack,R,cleanse,130,5,'offensive');
  }
  return rows;
}
export function simulatePeriodic(options={}) {return simulateTactics({seedVersion:'T128-v1',...options});}

export function periodicCommandOptions(args){
  const allowed=['matrix','samples','horizon','shard-index','shard-count'],values={};
  for(const arg of args){const match=/^--([^=]+)=(.+)$/.exec(arg);
    assert.ok(match&&allowed.includes(match[1]),`Unknown T128 argument: ${arg}`);
    assert.ok(!Object.hasOwn(values,match[1]),`Duplicate T128 argument: ${match[1]}`);values[match[1]]=match[2];}
  const matrix=values.matrix??'main',rows=periodicScenarios(matrix);
  function integer(key,fallback,min,max=Number.MAX_SAFE_INTEGER){const raw=values[key]??String(fallback),value=Number(raw);
    assert.ok(/^\d+$/.test(raw)&&Number.isSafeInteger(value)&&value>=min&&value<=max,`Invalid ${key}: ${raw}`);return value;}
  const n=integer('samples',matrix==='confirm'?1000:100,1),horizon=integer('horizon',2000,1);
  const shardCount=integer('shard-count',1,1,rows.length),shardIndex=integer('shard-index',0,0,shardCount-1);
  return {matrix,rows,n,horizon,shardIndex,shardCount};
}
if(process.argv[1]&&pathToFileURL(resolve(process.argv[1])).href===import.meta.url){
  const {matrix,rows,n,horizon,shardIndex,shardCount}=periodicCommandOptions(process.argv.slice(2));
  const indices=rows.map((_,i)=>i).filter(i=>i%shardCount===shardIndex),operation=createOperation(),started=performance.now();
  console.log(JSON.stringify({type:'metadata',version:'T128-v1',baseModel:'T123-v1',matrix,cells:rows.length,samples:n,horizon,
    shardIndex,shardCount,indices,shardCells:indices.length,node:process.version,profiles:periodicProfiles,builds,
    seedScheme:'FNV full cell including matrix suffix + samples + horizon + T128-v1; independent trial/damage/effect-save streams; no index/shard/time',
    scope:'Numerical investigation only. Exact T127; ordinary defense free, optional pre-roll guard +5/4P. No Heal interruptions.',
    periodic:'Source/art/target snapshot, flat once, strongest-family; next three target ends before expiry/regen; all streams age; no payment per tick.',
    suppression:'Hit then independent effect-save even on crit; partial half strength one opportunity, full two; save preserves old mark.',
    HP0:'Explicit fixture revival capability; threshold10%, skipped Main/reaction/regen not reset. All-side defeat ends before future ticks.',
    units:'Paid/Gained counters are hundredths of DE; damage/restored/potentialHealing/overheal are HP; all counters aggregate and byActor.',
    endpoint:'total, firstPatientFall, firstAnyFall separate; unconditional median/p95 null if insufficient events; horizon never a win.',
    limitations:'Visible current-state heuristics, not optimal AI; unequal build costs; fixed effect R is not a progression formula; no group balance inference.'}));
  for(const index of indices){const start=performance.now(),result=simulatePeriodic({...rows[index],n,horizon,operation});
    console.log(JSON.stringify({type:'cell',index,complete:true,elapsedMs:performance.now()-start,...result}));}
  console.log(JSON.stringify({type:'complete',complete:true,version:'T128-v1',matrix,cells:indices.length,matrixCells:rows.length,
    samples:n,horizon,shardIndex,shardCount,indices,trajectories:indices.length*n,elapsedMs:performance.now()-started,
    formulaReuse:{...operation.stats,healing:operation.calculate.stats}}));
}
