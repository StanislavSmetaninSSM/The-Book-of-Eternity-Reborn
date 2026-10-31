// T125 fixed investigation. Stdout is JSONL; raw measurements belong outside Git.
import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
import {createOperation} from './healing-tactics-model.mjs';
import {simulateTactics} from './healing-tactics-probe.mjs';

const tactics=Object.freeze([
  {tactic:'legacy_large_never',attackPolicy:'fixed_large',interruptPolicy:'never'},
  {tactic:'legacy_large_always',attackPolicy:'fixed_large',interruptPolicy:'always'},
  {tactic:'legacy_suppress_reserve',attackPolicy:'suppress_reserve',interruptPolicy:'reserve_large'},
  {tactic:'planner_never',attackPolicy:'fixed_large',jointPolicy:'never'},
  {tactic:'planner_always',attackPolicy:'fixed_large',jointPolicy:'always'},
  {tactic:'planner_joint',attackPolicy:'fixed_large',jointPolicy:'joint'}
].map(Object.freeze));
const profiles=Object.freeze([
  {benchmarkProfile:'fortified_healer28',harmony:'healer28',healingPercent:50,resistance:25},
  {benchmarkProfile:'fortified_both36',harmony:'both36',healingPercent:50,resistance:25},
  {benchmarkProfile:'unboosted_normal',harmony:'normal',healingPercent:0,resistance:0}
].map(Object.freeze));

export function jointBudgetScenarios(matrix='main') {
  assert.ok(['main','confirm'].includes(matrix),'Unknown T125 matrix');
  const rows=[];
  for(const profile of profiles)for(const order of [[0,1],[1,0]])for(const mode of ['instant','long','adaptive'])
    for(const tactic of tactics)rows.push({D:130,kind:'duel',...profile,order:[...order],mode,...tactic,
      targetPolicy:'patient',helperAccess:true,defender:'passive',healerPolicy:'heal',reply:'guard',interrupt:true,mirror:false,
      seedSuffix:matrix==='main'?'T125-main-v1':'T125-confirm-v1'});
  return rows;
}

function commandOptions(args){
  const allowed=['matrix','samples','horizon','shard-index','shard-count'],values={};
  for(const arg of args){
    const match=/^--([^=]+)=(.+)$/.exec(arg);
    assert.ok(match&&allowed.includes(match[1]),`Unknown T125 argument: ${arg}`);
    assert.ok(!Object.hasOwn(values,match[1]),`Duplicate T125 argument: ${match[1]}`);values[match[1]]=match[2];
  }
  const matrix=values.matrix??'main',rows=jointBudgetScenarios(matrix);
  function integer(key,fallback,min,max=Number.MAX_SAFE_INTEGER){
    const raw=values[key]??String(fallback),value=Number(raw);
    assert.ok(/^\d+$/.test(raw)&&Number.isSafeInteger(value)&&value>=min&&value<=max,`Invalid ${key}: ${raw}`);return value;
  }
  const n=integer('samples',matrix==='confirm'?10000:1000,1),horizon=integer('horizon',2000,1);
  const shardCount=integer('shard-count',1,1,rows.length),shardIndex=integer('shard-index',0,0,shardCount-1);
  return {matrix,rows,n,horizon,shardIndex,shardCount};
}

if(process.argv[1]&&pathToFileURL(resolve(process.argv[1])).href===import.meta.url){
  const {matrix,rows,n,horizon,shardIndex,shardCount}=commandOptions(process.argv.slice(2));
  const indices=rows.map((_,i)=>i).filter(i=>i%shardCount===shardIndex),operation=createOperation(),started=performance.now();
  console.log(JSON.stringify({type:'metadata',version:'T125-v1',baseModel:'T123-v1',planner:'joint_pressure_v1',
    plannerHorizon:3,terminalCredit:'mean ordinary damage/(ordinary cost + Recover gain), HP per hundredth DE',
    matrix,cells:rows.length,samples:n,horizon,shardIndex,shardCount,indices,shardCells:indices.length,node:process.version,
    seedSuffix:rows[0].seedSuffix,profiles,tactics,
    conditions:{D:130,HP:1030,maxDE:204,fullStartingResources:true,orders:[[0,1],[1,0]],modes:['instant','long','adaptive'],
      reply:'guard',healerPolicy:'heal',defender:'passive'},
    seedScheme:'T123-v1 FNV full cell including tactic/profile/policy/seedSuffix, samples/horizon -> trial -> separate damage/reaction; no shard/index/time',
    forecast:'Three own Mains; last observed actual intent including canceled, initially instant; visible pending overrides; nominal future HP; guard assumed affordable (-5); no enemy energy or future chooseMode',
    reaction:'Current instant/finish restoration capped by visible missing HP; actual payment/admission rechecked; one suppression source only',
    ablations:'All planner Mains use the identical joint forecast; Never/Always override actual reactions only',
    limitations:'Bounded potential-HP heuristic, not optimal AI or equal economic build balance; no group inference',
    counters:'Opt-in jointMainDecisions/jointDecisions/jointDecision_<reason>, aggregate and byActor; Paid in hundredths DE',
    endpoint:'Unconditional median/p95 null when not reached; total/firstPatientFall/firstAnyFall remain separate'}));
  for(const index of indices){
    const start=performance.now(),result=simulateTactics({...rows[index],n,horizon,operation});
    console.log(JSON.stringify({type:'cell',index,complete:true,elapsedMs:performance.now()-start,...result}));
  }
  console.log(JSON.stringify({type:'complete',complete:true,version:'T125-v1',matrix,cells:indices.length,matrixCells:rows.length,
    samples:n,horizon,shardIndex,shardCount,indices,trajectories:indices.length*n,elapsedMs:performance.now()-started,
    formulaReuse:{...operation.stats,healing:operation.calculate.stats,joint:operation.jointStats??null}}));
}
