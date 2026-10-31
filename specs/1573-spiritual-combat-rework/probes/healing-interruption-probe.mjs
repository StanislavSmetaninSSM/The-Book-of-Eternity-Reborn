// T124 standalone investigation. Stdout is JSONL only; measurement artifacts belong outside Git.
import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
import {interruptPolicies,createOperation} from './healing-tactics-model.mjs';
import {simulateTactics} from './healing-tactics-probe.mjs';

/** The preregistered duel cross product; confirmation changes only the independent seed suffix. */
export function interruptionScenarios(matrix='main') {
  assert.ok(['main','confirm'].includes(matrix),'Unknown T124 matrix');
  const rows=[];
  for(const harmony of ['healer28','both36'])for(const order of [[0,1],[1,0]])
    for(const mode of ['instant','long','adaptive'])for(const attackPolicy of ['fixed_large','suppress_reserve'])
      for(const interruptPolicy of interruptPolicies)rows.push({D:130,kind:'duel',healingPercent:50,resistance:25,
        harmony,order:[...order],mode,attackPolicy,interruptPolicy,targetPolicy:'patient',helperAccess:true,
        defender:'passive',healerPolicy:'heal',reply:'guard',interrupt:true,mirror:false,
        seedSuffix:matrix==='main'?'T124-main-v1':'T124-confirm-v1'});
  return rows;
}

function commandOptions(args){
  const allowed=['matrix','samples','horizon','shard-index','shard-count'],values={};
  for(const arg of args){
    const match=/^--([^=]+)=(.+)$/.exec(arg);
    assert.ok(match&&allowed.includes(match[1]),`Unknown T124 argument: ${arg}`);
    assert.ok(!Object.hasOwn(values,match[1]),`Duplicate T124 argument: ${match[1]}`);values[match[1]]=match[2];
  }
  const matrix=values.matrix??'main',rows=interruptionScenarios(matrix);
  function integer(key,fallback,min,max=Number.MAX_SAFE_INTEGER){
    const raw=values[key]??String(fallback),value=Number(raw);
    assert.ok(/^\d+$/.test(raw)&&Number.isSafeInteger(value)&&value>=min&&value<=max,`Invalid ${key}: ${raw}`);
    return value;
  }
  const n=integer('samples',matrix==='confirm'?10000:1000,1),horizon=integer('horizon',2000,1);
  const shardCount=integer('shard-count',1,1,rows.length),shardIndex=integer('shard-index',0,0,shardCount-1);
  return {matrix,rows,n,horizon,shardIndex,shardCount};
}

if(process.argv[1]&&pathToFileURL(resolve(process.argv[1])).href===import.meta.url){
  const {matrix,rows,n,horizon,shardIndex,shardCount}=commandOptions(process.argv.slice(2));
  const indices=rows.map((_,index)=>index).filter(index=>index%shardCount===shardIndex);
  const started=performance.now(),operation=createOperation();
  console.log(JSON.stringify({type:'metadata',version:'T124-v1',baseModel:'T123-v1',matrix,cells:rows.length,
    shardCells:indices.length,samples:n,horizon,shardIndex,shardCount,indices,node:process.version,
    seedSuffix:rows[0].seedSuffix,interruptPolicies,
    conditions:{D:130,kind:'duel',healingPercent:50,resistance:25,fullStartingResources:true,
      harmony:['healer28','both36'],orders:[[0,1],[1,0]],modes:['instant','long','adaptive'],
      attackPolicies:['fixed_large','suppress_reserve'],reply:'guard',healerPolicy:'heal',defender:'passive'},
    seedScheme:'T123-v1 FNV full cell parameters including interruptPolicy/seedSuffix, samples/horizon -> trial -> separate damage/reaction streams; independent of shard/index/time',
    interruptionReserve:'Current energy minus own 8P must be >= own 16P/24P; no future regeneration; release skips long cast only',
    attackReserve:'Existing suppress_reserve 16P with own end regeneration is unchanged, including Never',
    decisionCounters:'Opt-in interruptDecisions and interruptDecision_<reason>, aggregate and byActor; one first reason per enabled affordable Heal window',
    limitations:'Duel visible-state heuristics, not optimal AI; no inference about support/group balance or build acquisition costs',
    endpoint:'total separate from firstPatientFall/firstAnyFall; unconditional median/p95 remain null when censored'}));
  for(const index of indices){
    const start=performance.now(),result=simulateTactics({...rows[index],n,horizon,operation});
    console.log(JSON.stringify({type:'cell',index,complete:true,elapsedMs:performance.now()-start,...result}));
  }
  console.log(JSON.stringify({type:'complete',complete:true,version:'T124-v1',matrix,cells:indices.length,
    matrixCells:rows.length,samples:n,horizon,shardIndex,shardCount,indices,trajectories:indices.length*n,
    elapsedMs:performance.now()-started,formulaReuse:{...operation.stats,healing:operation.calculate.stats}}));
}
